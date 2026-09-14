[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\artifacts\customer-patch-rule-publisher')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$cliProject = Join-Path $projectRoot 'tools\ArasUpgradeOrchestrator.Package.Cli\ArasUpgradeOrchestrator.Package.Cli.csproj'
$launcherScript = Join-Path $PSScriptRoot 'Publish-CustomerPatchRule.ps1'
$launcherCmd = Join-Path $PSScriptRoot 'Publish-CustomerPatchRule.cmd'

if (Test-Path -LiteralPath $outputFullPath) {
    throw "Refusing to overwrite an existing delivery folder: $outputFullPath"
}

New-Item -ItemType Directory -Path $outputFullPath | Out-Null
try {
    & dotnet publish $cliProject --configuration Release --output $outputFullPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    Copy-Item -LiteralPath $launcherScript -Destination (Join-Path $outputFullPath 'Publish-CustomerPatchRule.ps1')
    Copy-Item -LiteralPath $launcherCmd -Destination (Join-Path $outputFullPath 'Publish-CustomerPatchRule.cmd')
    Write-Output "Customer-Patch rule publisher delivery created: $outputFullPath"
}
catch {
    if (Test-Path -LiteralPath $outputFullPath) {
        Remove-Item -LiteralPath $outputFullPath -Recurse -Force
    }

    throw
}
