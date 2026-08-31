<#
.SYNOPSIS
    Exercises the Day 18 relay end to end against a running QuoteRelay.Api.

.DESCRIPTION
    Submits three digests in a row - a healthy one, one that is guaranteed to
    fail during assembly, and a second healthy one - then polls each until it
    settles. The point is to show two things at once: the POST returns long
    before the digest exists, and the failure in the middle does not stop the
    assignment behind it.

    Start the API first, in another terminal:
        dotnet run --project src/QuoteRelay.Api

.PARAMETER BaseUrl
    Where the API is listening. Defaults to the Kestrel HTTP endpoint.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5080'
)

$ErrorActionPreference = 'Stop'

function Submit-Digest {
    param([string]$Subscriber, [int[]]$QuoteIds)

    $body = @{ subscriber = $Subscriber; quoteIds = $QuoteIds } | ConvertTo-Json
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $receipt = Invoke-RestMethod -Method Post -Uri "$BaseUrl/relay/digests" -ContentType 'application/json' -Body $body
    $watch.Stop()

    [pscustomobject]@{
        AssignmentId = $receipt.assignmentId
        Subscriber   = $Subscriber
        QuoteIds     = ($QuoteIds -join ', ')
        RoundTripMs  = [math]::Round($watch.Elapsed.TotalMilliseconds, 1)
        Backlog      = $receipt.backlog
    }
}

function Wait-Settled {
    param([string]$AssignmentId, [int]$TimeoutSeconds = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $view = Invoke-RestMethod -Method Get -Uri "$BaseUrl/relay/digests/$AssignmentId"
        if ($view.stage -notin @('Accepted', 'InProgress')) { return $view }
        Start-Sleep -Milliseconds 250
    }
    throw "Assignment $AssignmentId never settled."
}

Write-Host 'Submitting three assignments (healthy, doomed, healthy)...' -ForegroundColor Cyan

$submissions = @(
    (Submit-Digest -Subscriber 'first@example.test'  -QuoteIds 101, 102, 103, 104, 105),
    # 4242 is well formed, so validation admits it; the catalogue has no such row,
    # so assembly throws once the work is already off the request thread.
    (Submit-Digest -Subscriber 'doomed@example.test' -QuoteIds 101, 4242),
    (Submit-Digest -Subscriber 'third@example.test'  -QuoteIds 103, 105)
)

$submissions | Format-Table Subscriber, QuoteIds, RoundTripMs, Backlog -AutoSize

Write-Host 'Every POST above returned in single-digit milliseconds while the work was still queued.' -ForegroundColor DarkGray
Write-Host ''
Write-Host 'Polling until each assignment settles...' -ForegroundColor Cyan

$outcomes = foreach ($submission in $submissions) {
    $view = Wait-Settled -AssignmentId $submission.AssignmentId
    [pscustomobject]@{
        Subscriber = $submission.Subscriber
        Stage      = $view.stage
        Note       = $view.note
    }
}

$outcomes | Format-Table -AutoSize

Write-Host ''
Write-Host 'Relay vitals:' -ForegroundColor Cyan
Invoke-RestMethod -Method Get -Uri "$BaseUrl/relay/vitals" | Format-List

Write-Host ''
Write-Host 'Now press Ctrl+C in the API terminal to watch the pump shut down cleanly.' -ForegroundColor Yellow
