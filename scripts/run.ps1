[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "BoxingCoach.Web\BoxingCoach.Web.csproj"
$launchSettingsPath = Join-Path $repoRoot "BoxingCoach.Web\Properties\launchSettings.json"

function Fail {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Get-ProjectProcesses {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath
    )

    $projectPathFull = [System.IO.Path]::GetFullPath($ProjectPath)
    $matches = @()

    foreach ($process in (Get-CimInstance Win32_Process -ErrorAction Stop)) {
        if (-not $process.CommandLine) { continue }

        $commandLine = $process.CommandLine
        $name = $process.Name

        $isDotnetHost = ($name -eq "dotnet.exe") -or ($name -eq "dotnet")
        $isProjectHost = ($name -eq "BoxingCoach.Web.exe") -or ($name -eq "BoxingCoach.Web")

        $matchesProjectPath = $commandLine.Contains($projectPathFull)
        $matchesCompiledDll = $commandLine.Contains("BoxingCoach.Web.dll")

        if (($isDotnetHost -and $matchesProjectPath) -or ($isProjectHost -and $matchesCompiledDll)) {
            $matches += $process
        }
    }

    return $matches
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    Fail "Project file not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $launchSettingsPath)) {
    Fail "Launch settings file not found: $launchSettingsPath"
}

$launchSettings = Get-Content -Raw -LiteralPath $launchSettingsPath | ConvertFrom-Json
$selectedProfile = $launchSettings.profiles.https
if (-not $selectedProfile) {
    Fail "No HTTPS launch profile found in $launchSettingsPath"
}

$expectedUrls = @()
if ($selectedProfile.applicationUrl) {
    foreach ($url in ($selectedProfile.applicationUrl -split ';')) {
        if (-not [string]::IsNullOrWhiteSpace($url)) {
            $expectedUrls += $url.Trim()
        }
    }
}

if ($expectedUrls.Count -eq 0) {
    Fail "No application URLs configured for the HTTPS profile in $launchSettingsPath"
}

Write-Host "Project path: $projectPath"
Write-Host "Matching process criteria: dotnet command line contains the exact project file path, or launched app contains BoxingCoach.Web.dll"
Write-Host "Expected URLs: $($expectedUrls -join ', ')"
Write-Host "Checking for an existing project instance..."

$existingProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
if ($existingProcesses.Count -gt 0) {
    Write-Host "Found existing project process(es):"
    $existingProcesses | ForEach-Object {
        Write-Host "  PID $($_.ProcessId) :: $($_.CommandLine)"
    }

    foreach ($process in $existingProcesses) {
        try {
            Write-Host "Stopping process PID $($process.ProcessId) ..."
            Stop-Process -Id $process.ProcessId -ErrorAction Stop
        }
        catch {
            Fail "Failed to stop project process PID $($process.ProcessId): $($_.Exception.Message)"
        }
    }

    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Seconds 1
        $stillRunning = @(Get-ProjectProcesses -ProjectPath $projectPath)
    } while ((Get-Date) -lt $deadline -and $stillRunning.Count -gt 0)

    $remainingProjectProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
    if ($remainingProjectProcesses.Count -gt 0) {
        Fail "Existing project process did not exit cleanly after stop request."
    }

    Write-Host "Existing project process stopped."
}
else {
    Write-Host "No project instance is currently running."
}

$launchProfile = "https"
Write-Host "Starting project with: dotnet run --project `"$projectPath`" --launch-profile `"$launchProfile`""

try {
    $process = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $projectPath, "--launch-profile", $launchProfile) -WorkingDirectory $repoRoot -PassThru -ErrorAction Stop
}
catch {
    Fail "Failed to start the project: $($_.Exception.Message)"
}

Write-Host "Started new process with PID $($process.Id)"

$confirmed = $false
$lastError = $null
for ($attempt = 1; $attempt -le 30; $attempt++) {
    Start-Sleep -Seconds 1

    $currentProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
    $matchingProcess = $currentProcesses | Where-Object { $_.ProcessId -eq $process.Id }
    if (-not $matchingProcess) {
        $lastError = "The launched process exited before the app was confirmed to be ready."
        break
    }

    foreach ($url in $expectedUrls) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                Write-Host "SUCCESS: Project confirmed running and responding at $url"
                $confirmed = $true
                break
            }
        }
        catch {
            $lastError = "App started but the HTTP check did not yet respond on $url."
        }
    }

    if ($confirmed) { break }
}

if (-not $confirmed) {
    if ($lastError) {
        Fail $lastError
    }

    Fail "Project process started but the application could not be confirmed ready."
}

Write-Host "SUCCESS: Project start script completed successfully."
exit 0
