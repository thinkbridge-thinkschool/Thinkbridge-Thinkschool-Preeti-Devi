$iterations = 1000
$id = 1

$efTimes = [System.Collections.Generic.List[double]]::new()
$dapperTimes = [System.Collections.Generic.List[double]]::new()

# Warm-up
Invoke-RestMethod "http://localhost:5263/api/quotes/$id" | Out-Null
Invoke-RestMethod "http://localhost:5263/api/quotes/dapper/$id" | Out-Null

for ($i = 0; $i -lt $iterations; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    Invoke-RestMethod "http://localhost:5263/api/quotes/$id" | Out-Null

    $sw.Stop()
    $efTimes.Add($sw.Elapsed.TotalMilliseconds)
}

for ($i = 0; $i -lt $iterations; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    Invoke-RestMethod "http://localhost:5263/api/quotes/dapper/$id" | Out-Null

    $sw.Stop()
    $dapperTimes.Add($sw.Elapsed.TotalMilliseconds)
}

$efAverage = ($efTimes | Measure-Object -Average).Average
$dapperAverage = ($dapperTimes | Measure-Object -Average).Average

$efSorted = $efTimes | Sort-Object
$dapperSorted = $dapperTimes | Sort-Object

$efP95 = $efSorted[[math]::Floor($efSorted.Count * 0.95)]
$dapperP95 = $dapperSorted[[math]::Floor($dapperSorted.Count * 0.95)]

$efP99 = $efSorted[[math]::Floor($efSorted.Count * 0.99)]
$dapperP99 = $dapperSorted[[math]::Floor($dapperSorted.Count * 0.99)]

Write-Host ""
Write-Host "===== EF Core ====="
Write-Host "Iterations: $iterations"
Write-Host "Average:    $([math]::Round($efAverage, 3)) ms"
Write-Host "p95:        $([math]::Round($efP95, 3)) ms"
Write-Host "p99:        $([math]::Round($efP99, 3)) ms"

Write-Host ""
Write-Host "===== Dapper ====="
Write-Host "Iterations: $iterations"
Write-Host "Average:    $([math]::Round($dapperAverage, 3)) ms"
Write-Host "p95:        $([math]::Round($dapperP95, 3)) ms"
Write-Host "p99:        $([math]::Round($dapperP99, 3)) ms"

Write-Host ""
Write-Host "===== Comparison ====="
Write-Host "Average improvement: $([math]::Round((1 - ($dapperAverage / $efAverage)) * 100, 2))%"
Write-Host "p99 improvement:     $([math]::Round((1 - ($dapperP99 / $efP99)) * 100, 2))%"