<#
  Captures graceful-shutdown evidence by sending a real Ctrl+C to the host.

  The app is started from its built DLL rather than via `dotnet run`, because
  `dotnet run` launches the host as a child process that a console signal never
  reaches. The host gets its own console, which this script attaches to so the
  Ctrl+C lands there and not on PowerShell.

  Run:  ./verify-shutdown.ps1
#>

param(
    [int]$Port = 5262,
    [string]$Namespace = 'sb-day19-quotedemo',
    [string]$Topic     = 'quote-events'
)

$fqdn = "$Namespace.servicebus.windows.net"

$ErrorActionPreference = 'Stop'

$root     = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root 'evidence'
$log      = Join-Path $evidence 'graceful-shutdown.log'
$origin   = "http://localhost:$Port"

New-Item -ItemType Directory -Force -Path $evidence | Out-Null

Write-Host "Building..." -ForegroundColor Cyan
dotnet build (Join-Path $root 'src/Day19.Events') -c Debug -v quiet --nologo | Out-Null

$dll = Join-Path $root 'src/Day19.Events/bin/Debug/net10.0/Day19.Events.dll'
if (-not (Test-Path $dll)) { throw "Build output not found at $dll" }

Add-Type -Namespace ConsoleSignal -Name Native -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool AttachConsole(uint dwProcessId);
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool FreeConsole();
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
public static extern bool SetConsoleCtrlHandler(System.IntPtr handler, bool add);
'@

# cd first: the host takes its content root from the working directory, and
# appsettings.json sits beside the DLL rather than beside this script.
$binDir  = Split-Path $dll -Parent
$command = "cd /d `"$binDir`" && dotnet `"$dll`" --urls $origin --ServiceBus:FullyQualifiedNamespace $fqdn --ServiceBus:TopicName $Topic --ServiceBus:ExcludeManagedIdentity true > `"$log`" 2>&1"
$host_ = Start-Process cmd.exe -ArgumentList '/c', $command -PassThru -WindowStyle Hidden

Write-Host "Waiting for the host to come up (pid $($host_.Id))..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds(90)
$up = $false
while ((Get-Date) -lt $deadline) {
    try { Invoke-RestMethod "$origin/state" -TimeoutSec 2 | Out-Null; $up = $true; break }
    catch { Start-Sleep -Milliseconds 500 }
}
if (-not $up) { Stop-Process -Id $host_.Id -Force -EA SilentlyContinue; throw "Host did not start. See $log" }

Write-Host "Publishing an event so there is work in flight..." -ForegroundColor Cyan
Invoke-RestMethod "$origin/events" -Method Post -ContentType 'application/json' `
    -Body (@{ quoteId = 777; eventType = 'QuotePublished' } | ConvertTo-Json) | Out-Null
Start-Sleep -Seconds 3

Write-Host "Sending Ctrl+C to the host's console..." -ForegroundColor Cyan
[ConsoleSignal.Native]::FreeConsole() | Out-Null
if ([ConsoleSignal.Native]::AttachConsole([uint32]$host_.Id)) {
    # Deafen ourselves first, or the event we raise stops this script too.
    [ConsoleSignal.Native]::SetConsoleCtrlHandler([IntPtr]::Zero, $true) | Out-Null
    [ConsoleSignal.Native]::GenerateConsoleCtrlEvent(0, 0) | Out-Null   # 0 = CTRL_C_EVENT
    Start-Sleep -Seconds 10
    [ConsoleSignal.Native]::SetConsoleCtrlHandler([IntPtr]::Zero, $false) | Out-Null
    [ConsoleSignal.Native]::FreeConsole() | Out-Null
}

if (-not $host_.HasExited) { Stop-Process -Id $host_.Id -Force -EA SilentlyContinue }

# Write-Output, not Write-Host: FreeConsole detached this process from a
# console, which Write-Host's colour handling requires.
Write-Output ""
Write-Output "--- graceful shutdown ---"
Get-Content $log | Select-String -Pattern 'Application is shutting down|stopped cleanly|still had work' |
    ForEach-Object { Write-Output $_.Line }
Write-Output ""
Write-Output "Full log: $log"
