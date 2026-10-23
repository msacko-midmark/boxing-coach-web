[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repoRoot "BoxingCoach.Web.slnx"
$projectPath = Join-Path $repoRoot "BoxingCoach.Web\BoxingCoach.Web.csproj"

function Test-PathIsInsideRepo {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$RepoRoot
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\\')
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)

    return $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)
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

function Fail {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $solutionPath)) {
    Fail "Required solution file not found: $solutionPath"
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    Fail "Project file not found: $projectPath"
}

$allowedCleanupPaths = @(
    (Join-Path $repoRoot "BoxingCoach.Web\bin"),
    (Join-Path $repoRoot "BoxingCoach.Web\obj"),
    (Join-Path $repoRoot "BoxingCoach.Web.Tests\bin"),
    (Join-Path $repoRoot "BoxingCoach.Web.Tests\obj")
)

foreach ($path in $allowedCleanupPaths) {
    if (-not (Test-PathIsInsideRepo -Path $path -RepoRoot $repoRoot)) {
        Fail "Refusing to delete path outside the repo: $path"
    }
}

Write-Host "Allowed cleanup paths:"
$allowedCleanupPaths | ForEach-Object { Write-Host "  - $_" }
Write-Host "Starting safe clean build for repo: $repoRoot"

$projectProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
if ($projectProcesses.Count -gt 0) {
    Write-Host "Project is currently running; stopping only the matching project process(es):"
    $projectProcesses | ForEach-Object {
        Write-Host "  PID $($_.ProcessId) :: $($_.CommandLine)"
    }

    foreach ($process in $projectProcesses) {
        try {
            Stop-Process -Id $process.ProcessId -ErrorAction Stop
        }
        catch {
            Fail "Failed to stop project process PID $($process.ProcessId): $($_.Exception.Message)"
        }
    }

    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Seconds 1
        $projectProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
    } while ((Get-Date) -lt $deadline -and $projectProcesses.Count -gt 0)

    $remainingProjectProcesses = @(Get-ProjectProcesses -ProjectPath $projectPath)
    if ($remainingProjectProcesses.Count -gt 0) {
        Fail "The project process did not exit cleanly after the stop request."
    }

    Write-Host "Stopped the project process before cleanup."
}
else {
    Write-Host "No matching project process is running."
}

foreach ($path in $allowedCleanupPaths) {
    if (Test-Path -LiteralPath $path) {
        Write-Host "Removing build output: $path"
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    else {
        Write-Host "Skipping missing build output: $path"
    }
}

Write-Host "Restoring dependencies..."
& dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) {
    Fail "Restore failed for $solutionPath (exit code: $LASTEXITCODE)"
}

Write-Host "Building solution..."
& dotnet build $solutionPath --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Fail "Build failed for $solutionPath (exit code: $LASTEXITCODE)"
}

Write-Host "SUCCESS: clean build completed for $solutionPath"
exit 0
