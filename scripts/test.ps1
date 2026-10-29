[CmdletBinding()]
param(
    [switch]$Sandbox,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [string[]]$TestArguments = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Inspect native exit codes ourselves so cleanup also runs on failed builds and tests.
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repositoryRoot 'tests\AuroraUpdater.Tests\AuroraUpdater.Tests.csproj'
$resultsDirectory = Join-Path $repositoryRoot 'artifacts\test-results'
$savedEnvironment = @{}
$temporaryDirectory = $null
$locationPushed = $false
$exitCode = 1

try {
    Push-Location -LiteralPath $repositoryRoot
    $locationPushed = $true

    if ($Sandbox) {
        $temporaryDirectory = Join-Path $repositoryRoot ('artifacts\test-tmp-' + [Guid]::NewGuid().ToString('N'))
        [System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
        $environmentOverrides = @{
            TEMP = $temporaryDirectory
            TMP = $temporaryDirectory
            DOTNET_CLI_HOME = Join-Path $repositoryRoot 'artifacts\dotnet-home'
            DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
            DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
            DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
            DOTNET_CLI_TELEMETRY_OPTOUT = '1'
            DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
            MSBUILDDISABLENODEREUSE = '1'
        }
        foreach ($name in $environmentOverrides.Keys) {
            $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
            [Environment]::SetEnvironmentVariable($name, $environmentOverrides[$name], 'Process')
        }

        $buildArguments = @('build', $testProject, '--configuration', $Configuration)
        if ($NoRestore) { $buildArguments += '--no-restore' }
        & dotnet @buildArguments
        $exitCode = $LASTEXITCODE
        if ($exitCode -eq 0) {
            # dotnet run launches the standalone test app without dotnet test's named-pipe connection.
            # --no-build uses the successful build above and avoids pinning an output path or framework.
            & dotnet run --project $testProject --configuration $Configuration --no-build --no-launch-profile -- --results-directory $resultsDirectory @TestArguments
            $exitCode = $LASTEXITCODE
        }
    }
    else {
        $runnerArguments = @('test', '--project', $testProject, '--configuration', $Configuration, '--results-directory', $resultsDirectory)
        if ($NoRestore) { $runnerArguments += '--no-restore' }
        $runnerArguments += '--'
        $runnerArguments += $TestArguments
        & dotnet @runnerArguments
        $exitCode = $LASTEXITCODE
    }
}
catch {
    Write-Error $_ -ErrorAction Continue
    $exitCode = 1
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        if ($null -eq $savedEnvironment[$name]) {
            # Newer .NET versions distinguish an empty value from an absent variable.
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
        }
    }
    if ($locationPushed) { Pop-Location }

    if ($temporaryDirectory -and (Test-Path -LiteralPath $temporaryDirectory)) {
        try {
            $fullTemporaryPath = [System.IO.Path]::GetFullPath($temporaryDirectory)
            $workspacePrefix = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([char[]]'\/') + [System.IO.Path]::DirectorySeparatorChar
            if (-not $fullTemporaryPath.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'The test temporary directory is outside the workspace.'
            }
            Remove-Item -LiteralPath $fullTemporaryPath -Recurse -Force
        }
        catch {
            Write-Error $_ -ErrorAction Continue
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
    }
}

exit $exitCode
