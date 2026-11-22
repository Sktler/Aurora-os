<#
.SYNOPSIS
  Builds and launches Aurora in a throwaway test environment.
.DESCRIPTION
  Aurora's settings, profiles and memory go to artifacts\test-env instead of %APPDATA%\Aurora,
  so your real setup is untouched. The first-run welcome and permission screens appear every
  time unless you keep the folder. Use -Reset to start fresh, -RunTests to run the unit tests first.
.EXAMPLE
  .\scripts\run-test-app.ps1 -Reset
#>
[CmdletBinding()]
param(
    [switch]$Reset,
    [switch]$RunTests,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testEnv = Join-Path $root 'artifacts\test-env'

if ($Reset -and (Test-Path -LiteralPath $testEnv)) { Remove-Item -LiteralPath $testEnv -Recurse -Force }
New-Item -ItemType Directory -Force -Path $testEnv | Out-Null

Push-Location $root
try {
    if ($RunTests) {
        & (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Not launching the app.' }
    }

    dotnet build Aurora.App\Aurora.App.csproj --configuration $Configuration --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    $env:AURORA_CONFIG_DIR = $testEnv
    Write-Host "Launching Aurora with test data in $testEnv"
    dotnet run --project Aurora.App\Aurora.App.csproj --configuration $Configuration --no-build
}
finally {
    Remove-Item Env:AURORA_CONFIG_DIR -ErrorAction SilentlyContinue
    Pop-Location
}
