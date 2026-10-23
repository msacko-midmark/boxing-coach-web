[CmdletBinding()]
param(
    [string]$BrowserPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "BoxingCoach.Web.slnx"
$resultsDir = Join-Path $repoRoot ".test-results"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$trxFileName = "test-results-$timestamp.trx"
$trxPath = Join-Path $resultsDir $trxFileName
$reportHtmlPath = Join-Path $resultsDir "test-report-$timestamp.html"
$pdfPath = Join-Path $resultsDir "test-report-$timestamp.pdf"
$logPath = Join-Path $resultsDir "test-output-$timestamp.log"

function Get-BrowserPath {
    if (-not [string]::IsNullOrWhiteSpace($BrowserPath)) {
        if (Test-Path -LiteralPath $BrowserPath) {
            return (Resolve-Path -LiteralPath $BrowserPath).Path
        }
        else {
            throw "Browser path was supplied but does not exist: $BrowserPath"
        }
    }

    $candidates = @(
        "msedge",
        "microsoft-edge",
        "chrome",
        "google-chrome",
        "google-chrome-stable"
    )

    foreach ($candidate in $candidates) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    $browserDirs = @(
        "$env:LOCALAPPDATA\Microsoft\Edge\Application",
        "$env:LOCALAPPDATA\Microsoft\Edge SxS\Application",
        "$env:ProgramFiles\Microsoft\Edge\Application",
        "$env:ProgramFiles(x86)\Microsoft\Edge\Application",
        "$env:LOCALAPPDATA\Google\Chrome\Application",
        "$env:LOCALAPPDATA\Google\Chrome SxS\Application",
        "$env:ProgramFiles\Google\Chrome\Application",
        "$env:ProgramFiles(x86)\Google\Chrome\Application"
    )

    foreach ($dir in $browserDirs) {
        if (-not (Test-Path -LiteralPath $dir)) { continue }

        $edgePath = (Get-ChildItem -LiteralPath $dir -Filter "msedge.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName | Sort-Object -Descending | Select-Object -First 1)
        if ($edgePath) { return $edgePath }

        $chromePath = (Get-ChildItem -LiteralPath $dir -Filter "chrome.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName | Sort-Object -Descending | Select-Object -First 1)
        if ($chromePath) { return $chromePath }
    }

    $regPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"
    )

    foreach ($regPath in $regPaths) {
        try {
            $pathValue = (Get-Item -LiteralPath $regPath -ErrorAction Stop).GetValue("")
            if ($pathValue) {
                if (Test-Path -LiteralPath $pathValue) {
                    return $pathValue
                }
            }
        }
        catch {
        }
    }

    return $null
}

function Get-TestSummaryFromTrx {
    param(
        [Parameter(Mandatory = $true)][string]$TrxPath
    )

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        return $null
    }

    try {
        $trxXml = [xml](Get-Content -LiteralPath $TrxPath -Raw)
    }
    catch {
        return $null
    }

    $counters = $trxXml.TestRun.ResultSummary.Counters
    if (-not $counters) {
        return $null
    }

    return [pscustomobject]@{
        Total   = [int]($counters.total)
        Passed  = [int]($counters.passed)
        Failed  = [int]($counters.failed)
        Skipped = [int]($counters.notExecuted)
    }
}

function Get-TestDetailsFromTrx {
    param(
        [Parameter(Mandatory = $true)][string]$TrxPath
    )

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        return @()
    }

    try {
        $trxXml = [xml](Get-Content -LiteralPath $TrxPath -Raw)
    }
    catch {
        return @()
    }

    $unitTestResults = @($trxXml.TestRun.Results.UnitTestResult)
    if ($unitTestResults.Count -eq 0) {
        return @()
    }

    $details = @()
    foreach ($result in $unitTestResults) {
        $errorInfo = $null
        if ($result.PSObject.Properties.Name -contains 'Output') {
            if ($result.Output -and $result.Output.ErrorInfo) {
                $errorInfo = ($result.Output.ErrorInfo.Message + " " + $result.Output.ErrorInfo.StackTrace).Trim()
            }
        }

        $details += [pscustomobject]@{
            TestName = $result.testName
            Outcome  = $result.outcome
            Duration = $result.duration
            Error    = $errorInfo
        }
    }

    return $details
}

function Fail {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $solutionPath)) {
    Fail "Solution file not found: $solutionPath"
}

New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

$commandText = "dotnet test `"$solutionPath`" --configuration Debug --logger `"trx;LogFileName=$trxFileName`" --results-directory `"$resultsDir`""
Write-Host "Running tests: $commandText"

& dotnet test $solutionPath --configuration Debug --logger "trx;LogFileName=$trxFileName" --results-directory $resultsDir 2>&1 | Tee-Object -FilePath $logPath
$testExitCode = $LASTEXITCODE

if (-not (Test-Path -LiteralPath $trxPath)) {
    Fail "Tests completed without producing a TRX report at $trxPath (exit code: $testExitCode)."
}

$summary = Get-TestSummaryFromTrx -TrxPath $trxPath
if (-not $summary) {
    Fail "Could not parse the TRX summary from $trxPath."
}

$details = Get-TestDetailsFromTrx -TrxPath $trxPath
$testOutput = Get-Content -LiteralPath $logPath -Raw
$runDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss K")

$reportHtml = @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>BoxingCoach.Web Test Report</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 32px; color: #1f2937; }
    h1, h2 { color: #111827; }
    .meta { background: #f3f4f6; border: 1px solid #d1d5db; padding: 12px 16px; border-radius: 8px; margin-bottom: 24px; }
    table { border-collapse: collapse; width: 100%; margin-top: 10px; }
    th, td { border: 1px solid #d1d5db; padding: 8px 10px; text-align: left; vertical-align: top; }
    th { background: #e5e7eb; }
    .pass { color: #166534; font-weight: 600; }
    .fail { color: #991b1b; font-weight: 600; }
    .skip { color: #92400e; font-weight: 600; }
    pre { white-space: pre-wrap; word-wrap: break-word; background: #111827; color: #f9fafb; padding: 12px; border-radius: 8px; }
  </style>
</head>
<body>
  <h1>BoxingCoach.Web Test Report</h1>

  <div class="meta">
    <p><strong>Run date:</strong> $runDate</p>
    <p><strong>Command:</strong> $commandText</p>
    <p><strong>Exit code:</strong> $testExitCode</p>
    <p><strong>Total:</strong> $($summary.Total) &nbsp;|&nbsp; <span class="pass">Passed:</span> $($summary.Passed) &nbsp;|&nbsp; <span class="fail">Failed:</span> $($summary.Failed) &nbsp;|&nbsp; <span class="skip">Skipped:</span> $($summary.Skipped)</p>
  </div>

  <h2>Test summary</h2>
  <p>Result: $(if ($testExitCode -eq 0) { 'SUCCESS' } else { 'FAILURE' })</p>

  <h2>Detailed results</h2>
  <table>
    <thead>
      <tr>
        <th>Test</th>
        <th>Outcome</th>
        <th>Duration</th>
        <th>Error</th>
      </tr>
    </thead>
    <tbody>
      $(foreach ($item in $details) {
        $outcomeClass = if ($item.Outcome -eq 'Passed') { 'pass' } elseif ($item.Outcome -eq 'Failed') { 'fail' } else { 'skip' }
        $errorHtml = if ([string]::IsNullOrWhiteSpace($item.Error)) { '&nbsp;' } else { [System.Security.SecurityElement]::Escape($item.Error) }
        "<tr><td>$([System.Security.SecurityElement]::Escape($item.TestName))</td><td class=`"$outcomeClass`">$($item.Outcome)</td><td>$($item.Duration)</td><td>$errorHtml</td></tr>"
      })
    </tbody>
  </table>

  <h2>Command output</h2>
  <pre>$([System.Security.SecurityElement]::Escape($testOutput))</pre>
</body>
</html>
"@

Set-Content -LiteralPath $reportHtmlPath -Value $reportHtml -Encoding UTF8

$browserPath = Get-BrowserPath
if (-not $browserPath) {
    Fail "No Chromium-based browser found. Install Microsoft Edge or Google Chrome to export the HTML report to PDF."
}

$reportUri = [System.Uri]::new((Resolve-Path -LiteralPath $reportHtmlPath).Path).AbsoluteUri
$browserArgs = @(
    "--headless=new",
    "--disable-gpu",
    "--no-sandbox",
    "--run-all-compositor-stages-before-draw",
    "--print-to-pdf=$pdfPath",
    $reportUri
)

Write-Host "Exporting HTML report to PDF using: $browserPath"
& $browserPath @browserArgs *> $null
if ($LASTEXITCODE -ne 0) {
    Fail "PDF export failed for $reportHtmlPath (browser exit code: $LASTEXITCODE)."
}

$createdPdf = $false
for ($attempt = 1; $attempt -le 15; $attempt++) {
    if (Test-Path -LiteralPath $pdfPath) {
        $item = Get-Item -LiteralPath $pdfPath -ErrorAction SilentlyContinue
        if ($item -and $item.Length -gt 0) {
            $createdPdf = $true
            break
        }
    }

    Start-Sleep -Seconds 1
}

if (-not $createdPdf) {
    Fail "PDF file was not created at $pdfPath."
}

Write-Host "Test report HTML: $reportHtmlPath"
Write-Host "Test report PDF: $pdfPath"

if ($testExitCode -ne 0 -or $summary.Failed -gt 0) {
    Write-Host "FAILURE: Tests failed or the report was not fully successful. Summary: Total=$($summary.Total), Passed=$($summary.Passed), Failed=$($summary.Failed), Skipped=$($summary.Skipped)" -ForegroundColor Red
    exit 1
}

Write-Host "SUCCESS: Tests passed and PDF report was generated successfully." -ForegroundColor Green
exit 0
