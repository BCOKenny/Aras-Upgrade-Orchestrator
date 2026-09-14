[CmdletBinding()]
param(
    [string]$CaseRoot,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$requestRelativePath = 'rule-sets\publication-requests\customer-patch-common-v1.request.json'
$cliDll = Join-Path $PSScriptRoot 'ArasUpgradeOrchestrator.Package.Cli.dll'

Add-Type -AssemblyName System.Windows.Forms

function Show-LauncherMessage([string]$Message, [string]$Title, [System.Windows.Forms.MessageBoxIcon]$Icon) {
    [void][System.Windows.Forms.MessageBox]::Show(
        $Message,
        $Title,
        [System.Windows.Forms.MessageBoxButtons]::OK,
        $Icon)
}

function Get-WindowsActor {
    if ([string]::IsNullOrWhiteSpace($env:USERDOMAIN) -or [string]::IsNullOrWhiteSpace($env:USERNAME)) {
        throw 'Unable to determine the signed-in Windows identity.'
    }

    return "$($env:USERDOMAIN)\$($env:USERNAME)"
}

function Select-CaseRoot {
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select the Customer-Patch rule publication case root.'
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return $null
    }

    return $dialog.SelectedPath
}

function Invoke-PackageCli([string]$Command, [string]$RequestPath) {
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo.FileName = 'dotnet'
    $process.StartInfo.Arguments = ('"{0}" {1} "{2}"' -f $cliDll, $Command, $RequestPath)
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StandardOutput = $stdout
        StandardError = $stderr
    }
}

try {
    if (-not (Test-Path -LiteralPath $cliDll -PathType Leaf)) {
        throw "Delivered Package CLI was not found: $cliDll"
    }

    if ([string]::IsNullOrWhiteSpace($CaseRoot)) {
        $CaseRoot = Select-CaseRoot
    }

    if ([string]::IsNullOrWhiteSpace($CaseRoot)) {
        exit 0
    }

    $caseRootFullPath = [System.IO.Path]::GetFullPath($CaseRoot).TrimEnd('\')
    $requestPath = Join-Path $caseRootFullPath $requestRelativePath
    if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) {
        throw "Publication request was not found: $requestPath"
    }

    $request = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
    $actor = Get-WindowsActor
    $requestCaseRoot = [System.IO.Path]::GetFullPath([string]$request.caseRoot).TrimEnd('\')
    if ($requestCaseRoot -ne $caseRootFullPath) {
        throw 'The request caseRoot does not match the selected case root.'
    }

    if ($request.actor -ne $actor) {
        throw "The signed-in Windows actor '$actor' does not match request actor '$($request.actor)'."
    }

    $preflight = Invoke-PackageCli '--preflight-customer-patch-common-rule' $requestPath
    if ($preflight.ExitCode -ne 0) {
        throw ("Publication preflight failed:`n" + $preflight.StandardError + $preflight.StandardOutput)
    }

    $summary = $preflight.StandardOutput | ConvertFrom-Json
    $message = "Case: $caseRootFullPath`nActor: $actor`nRule: $($summary.ruleSetKind) / $($summary.ruleSetScope) / $($summary.stepId)`nRequest: $requestPath"
    if ($WhatIf) {
        Show-LauncherMessage $message 'Customer-Patch publication preflight passed' ([System.Windows.Forms.MessageBoxIcon]::Information)
        exit 0
    }

    $answer = [System.Windows.Forms.MessageBox]::Show(
        $message,
        'Confirm Customer-Patch rule publication',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Warning)
    if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) {
        exit 0
    }

    $publication = Invoke-PackageCli '--publish-customer-patch-common-rule' $requestPath
    if ($publication.ExitCode -ne 0) {
        throw ("Publication failed:`n" + $publication.StandardError + $publication.StandardOutput)
    }

    $result = $publication.StandardOutput | ConvertFrom-Json
    $published = $result.publishedRuleSet
    Show-LauncherMessage (
        "Publication succeeded.`nRuleSetId: $($published.ruleSetId)`nVersion: $($published.version)`nChecksum: $($published.contentChecksum)"),
        'Customer-Patch rule published',
        ([System.Windows.Forms.MessageBoxIcon]::Information)
    exit 0
}
catch {
    Show-LauncherMessage $_.Exception.Message 'Customer-Patch publication failed' ([System.Windows.Forms.MessageBoxIcon]::Error)
    exit 1
}
