<#
  Runs the exercise against the real Azure Service Bus namespace and writes the
  evidence into day-19/evidence.

  Run:  ./verify-azure.ps1
#>

param(
    [string]$Namespace     = 'sb-day19-quotedemo',
    [string]$ResourceGroup = 'thinkschool-rg',
    [string]$Topic         = 'quote-events',
    [string[]]$Subscriptions = @('sub-a', 'sub-b'),
    [int]$Port             = 5219
)

$ErrorActionPreference = 'Stop'

$root     = Split-Path $PSScriptRoot -Parent
$evidence = Join-Path $root 'evidence'
$project  = Join-Path $root 'src/Day19.Events'
$origin   = "http://localhost:$Port"
$fqdn     = "$Namespace.servicebus.windows.net"

New-Item -ItemType Directory -Force -Path $evidence | Out-Null

$consoleLog = Join-Path $evidence 'consumer-output.log'
$transcript = Join-Path $evidence 'azure-transcript.txt'

function Say($text) { Write-Host $text -ForegroundColor Cyan; Add-Content $transcript $text }

Set-Content $transcript "Day 19 - real Azure Service Bus verification"
Add-Content $transcript "Recorded $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
Add-Content $transcript ""

Say "== Azure topology =="
$account = az account show --query "{subscription:name, subscriptionId:id, user:user.name}" -o json | ConvertFrom-Json
Add-Content $transcript ($account | ConvertTo-Json)

$ns = az servicebus namespace show -n $Namespace -g $ResourceGroup `
    --query "{name:name, sku:sku.name, location:location, status:status, endpoint:serviceBusEndpoint}" -o json | ConvertFrom-Json
Add-Content $transcript ($ns | ConvertTo-Json)

$subsBefore = foreach ($s in $Subscriptions) {
    az servicebus topic subscription show -n $s --topic-name $Topic --namespace-name $Namespace -g $ResourceGroup `
        --query "{subscription:name, maxDeliveryCount:maxDeliveryCount, active:countDetails.activeMessageCount, deadLettered:countDetails.deadLetterMessageCount}" `
        -o json | ConvertFrom-Json
}
Say "`nSubscription state BEFORE the run:"
$subsBefore | Format-Table | Out-String | ForEach-Object { Write-Host $_; Add-Content $transcript $_ }

Say "== Starting the app against $fqdn =="

$app = Start-Process -FilePath 'dotnet' -PassThru -NoNewWindow `
    -RedirectStandardOutput $consoleLog -RedirectStandardError (Join-Path $evidence 'consumer-errors.log') `
    -ArgumentList @(
        'run', '--project', $project, '--no-launch-profile',
        '--urls', $origin,
        '--ServiceBus:FullyQualifiedNamespace', $fqdn,
        '--ServiceBus:TopicName', $Topic,

        # Required on machines running the Azure Arc agent, which otherwise
        # hijacks the credential chain before the CLI identity is reached.
        '--ServiceBus:ExcludeManagedIdentity', 'true'
    )

function Wait-For([scriptblock]$Check, [string]$What, [int]$Seconds = 60) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) {
        try { if (& $Check) { return $true } } catch { }
        Start-Sleep -Milliseconds 750
    }
    Write-Warning "Timed out waiting for: $What"
    return $false
}

function Publish($body) {
    Invoke-RestMethod "$origin/events" -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json)
}

try {
    if (-not (Wait-For { (Invoke-RestMethod "$origin/state" -TimeoutSec 3) -ne $null } 'the app to start' 120)) {
        Get-Content $consoleLog -Tail 40
        throw "The app did not start."
    }

    $drained = Invoke-RestMethod "$origin/dlq" -Method Delete
    Say "`nDrained $($drained.drained) pre-existing dead-lettered message(s) so this run's evidence is unambiguous."

    $eventId = [guid]::NewGuid()
    Say "`n== [1] Publish one event; both subscriptions should receive it =="
    Say "eventId = $eventId"

    $receipt = Publish @{ eventId = $eventId; quoteId = 101; eventType = 'QuotePublished' }
    $receipt | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'publisher-output.json') -Encoding utf8
    Add-Content $transcript ($receipt | ConvertTo-Json -Depth 5)

    Wait-For {
        $c = (Invoke-RestMethod "$origin/state").countBySubscription
        ($Subscriptions | ForEach-Object { $c.$_ } | Where-Object { $_ -ge 1 }).Count -eq $Subscriptions.Count
    } 'both subscriptions to receive the event' 90 | Out-Null

    $afterFirst = (Invoke-RestMethod "$origin/state").countBySubscription
    Say "counts after first publish: $($afterFirst | ConvertTo-Json -Compress)"

    Say "`n== [2] Re-publish the SAME eventId; duplicate must be suppressed =="
    Publish @{ eventId = $eventId; quoteId = 101; eventType = 'QuotePublished' } | Out-Null
    Start-Sleep -Seconds 8

    $afterDuplicate = (Invoke-RestMethod "$origin/state").countBySubscription
    Say "counts after duplicate:     $($afterDuplicate | ConvertTo-Json -Compress)"

    @{
        messageId       = $receipt.messageId
        beforeDuplicate = $afterFirst
        afterDuplicate  = $afterDuplicate
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'duplicate-check.json') -Encoding utf8

    Say "`n== [3] Burst of 12, so the two workers per subscription compete =="
    1..12 | ForEach-Object { Publish @{ quoteId = (200 + $_); eventType = 'QuotePublished' } | Out-Null }

    Wait-For {
        $c = (Invoke-RestMethod "$origin/state").countBySubscription
        ($Subscriptions | ForEach-Object { $c.$_ } | Where-Object { $_ -ge 13 }).Count -eq $Subscriptions.Count
    } 'the burst to be consumed' 180 | Out-Null

    $split = Select-String -Path $consoleLog -Pattern 'worker (\S+) handled MessageId=(\S+)' |
        ForEach-Object {
            [pscustomobject]@{ Worker = $_.Matches[0].Groups[1].Value; MessageId = $_.Matches[0].Groups[2].Value }
        }
    $perWorker = $split | Group-Object Worker | Sort-Object Name |
        ForEach-Object { [pscustomobject]@{ worker = $_.Name; handled = $_.Count } }

    Say "messages handled per worker:"
    $perWorker | Format-Table -AutoSize | Out-String | ForEach-Object { Write-Host $_; Add-Content $transcript $_ }

    @{
        perWorker          = $perWorker
        totalHandled       = $split.Count
        distinctMessageIds = ($split.MessageId | Sort-Object -Unique).Count
        workersThatDidWork = $perWorker.Count
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'competing-consumers.json') -Encoding utf8

    Say "`n== [4] Poison message (UnsupportedEvent) -> dead-lettered on delivery 1 =="
    $poison = Publish @{ quoteId = 909; eventType = 'UnsupportedEvent' }
    Say "poison MessageId = $($poison.messageId)"

    Say "`n== [5] Transient probe -> retried -> dead-lettered by Service Bus at MaxDeliveryCount =="
    $probe = Publish @{ quoteId = 910; eventType = 'TransientFailureProbe' }
    Say "probe MessageId  = $($probe.messageId)"

    Say "`nWaiting for the real dead-letter queues to fill..."
    Wait-For { (Invoke-RestMethod "$origin/dlq").Count -ge ($Subscriptions.Count * 2) } 'DLQ to receive both messages' 300 | Out-Null

    $state = Invoke-RestMethod "$origin/state"
    $state | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'subscription-state.json') -Encoding utf8

    $dlq = Invoke-RestMethod "$origin/dlq"
    $dlq | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'dlq-messages.json') -Encoding utf8

    Say "`n== REAL AZURE DEAD-LETTER QUEUE =="
    $dlq | Format-List Subscription, MessageId, DeadLetterReason, DeadLetterErrorDescription, DeliveryCount |
        Out-String | ForEach-Object { Write-Host $_; Add-Content $transcript $_ }

    @{ poisonMessageId = $poison.messageId; transientProbeMessageId = $probe.messageId; firstEventId = $eventId } |
        ConvertTo-Json | Set-Content (Join-Path $evidence 'message-ids.json') -Encoding utf8
}
finally {
    Write-Host "`nStopping the app..." -ForegroundColor Cyan
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
}

Start-Sleep -Seconds 5
Say "`n== Azure control-plane counts AFTER the run (az CLI, independent of the app) =="

$subsAfter = foreach ($s in $Subscriptions) {
    az servicebus topic subscription show -n $s --topic-name $Topic --namespace-name $Namespace -g $ResourceGroup `
        --query "{subscription:name, maxDeliveryCount:maxDeliveryCount, active:countDetails.activeMessageCount, deadLettered:countDetails.deadLetterMessageCount}" `
        -o json | ConvertFrom-Json
}
$subsAfter | Format-Table | Out-String | ForEach-Object { Write-Host $_; Add-Content $transcript $_ }

@{
    subscriptionId = $account.subscriptionId
    namespace      = $ns.name
    endpoint       = $ns.endpoint
    sku            = $ns.sku
    location       = $ns.location
    topic          = $Topic
    before         = $subsBefore
    after          = $subsAfter
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidence 'azure-topology.json') -Encoding utf8

Write-Host "`nEvidence written to $evidence" -ForegroundColor Green
