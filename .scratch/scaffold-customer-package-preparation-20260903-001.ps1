$caseRoot = 'K:\70.ArasUpgradeCases\' + ([string]([char]33455) + [char]27954)
$preparationId = 'preparation-20260903-001'
$baselineId = 'customer-11sp9'
$actualVersion = '11.0 SP9'
$actor = 'BCO\kenny'
$packageUpgrade = Join-Path $caseRoot 'package-upgrade'
$preparation = Join-Path $packageUpgrade $preparationId
$ruleSets = Join-Path $caseRoot 'rule-sets'
$hops = @(
    @{ HopId = '11.0-sp8-to-11.0-sp15'; TargetRelease = '11.0 SP15' },
    @{ HopId = '11.0-sp15-to-12.0-sp18'; TargetRelease = '12.0 SP18' },
    @{ HopId = '12.0-sp18-to-r38'; TargetRelease = 'R38' }
)

# Complete every zero-write preflight before creating any directory or file.
if (-not (Test-Path -LiteralPath $caseRoot -PathType Container)) { Write-Error 'BLOCKED: case root does not exist'; exit 2 }
if (-not (Test-Path -LiteralPath $packageUpgrade -PathType Container)) { Write-Error 'BLOCKED: package-upgrade does not exist'; exit 2 }
if (Test-Path -LiteralPath $preparation) { Write-Error 'BLOCKED: preparation directory already exists'; exit 2 }
if ([string]::IsNullOrWhiteSpace($baselineId) -or $baselineId.Contains('\') -or $baselineId.Contains('/') -or $baselineId.Contains('..') -or $baselineId.Contains(':')) { Write-Error 'BLOCKED: invalid baseline identifier'; exit 2 }
if ([string]::IsNullOrWhiteSpace($actualVersion)) { Write-Error 'BLOCKED: actual baseline version is empty'; exit 2 }
if ($hops.Count -eq 0) { Write-Error 'BLOCKED: no hops supplied'; exit 2 }
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($hop in $hops) {
    if ([string]::IsNullOrWhiteSpace($hop.HopId) -or [string]::IsNullOrWhiteSpace($hop.TargetRelease)) { Write-Error 'BLOCKED: hop is incomplete'; exit 2 }
    if (-not $seen.Add($hop.HopId)) { Write-Error 'BLOCKED: duplicate hop'; exit 2 }
}

$directories = New-Object 'System.Collections.Generic.List[string]'
$directories.Add($ruleSets)
$baselineRoot = Join-Path $preparation ('customer-package-baseline\' + $baselineId)
$directories.Add((Join-Path $baselineRoot 'input'))
$directories.Add((Join-Path $baselineRoot 'evidence'))
$directories.Add((Join-Path $preparation 'manifests'))
$comparisons = New-Object 'System.Collections.Generic.List[object]'
foreach ($hop in $hops) {
    $comparisonRoot = Join-Path $preparation ('hops\' + $hop.HopId + '\customer-patch-comparison')
    $directories.Add((Join-Path $comparisonRoot 'patch-support-input'))
    $directories.Add((Join-Path $comparisonRoot 'evidence'))
    $directories.Add((Join-Path $comparisonRoot 'comparison-attempts'))
    $directories.Add((Join-Path $comparisonRoot 'candidate-package-imports'))
    $relativeBase = 'package-upgrade/' + $preparationId
    $sourceRelative = $relativeBase + '/customer-package-baseline/' + $baselineId + '/input'
    $targetRelative = $relativeBase + '/hops/' + $hop.HopId + '/customer-patch-comparison/patch-support-input'
    $evidenceRelative = $relativeBase + '/hops/' + $hop.HopId + '/customer-patch-comparison/evidence'
    $comparisons.Add([ordered]@{
        comparisonId = $hop.HopId
        comparison = [ordered]@{
            source = [ordered]@{ role = 'CustomerBaseline'; baselineId = $baselineId; actualVersion = $actualVersion; inputRelativePath = $sourceRelative }
            target = [ordered]@{ role = 'TargetPackage'; targetRelease = $hop.TargetRelease; inputRelativePath = $targetRelative }
        }
        customerBaselineInput = $sourceRelative
        patchSupportInput = $targetRelative
        evidence = $evidenceRelative
        comparisonAttempts = ($relativeBase + '/hops/' + $hop.HopId + '/customer-patch-comparison/comparison-attempts')
        candidatePackageImports = ($relativeBase + '/hops/' + $hop.HopId + '/customer-patch-comparison/candidate-package-imports')
        status = 'PENDING'
    })
}
$plan = [ordered]@{ preparationId = $preparationId; caseRoot = $caseRoot; workflow = 'CUSTOMER_PATCH_COMPARISON'; customerPackageBaseline = [ordered]@{ baselineId = $baselineId; actualVersion = $actualVersion; inputRelativePath = ('package-upgrade/' + $preparationId + '/customer-package-baseline/' + $baselineId + '/input') }; comparisons = @($comparisons) }
$files = New-Object 'System.Collections.Generic.List[object]'
$files.Add(@{ Path = (Join-Path $preparation 'manifests\package-preparation-plan.template.json'); Content = ($plan | ConvertTo-Json -Depth 10) })
foreach ($hop in $hops) {
    $comparisonRoot = Join-Path $preparation ('hops\' + $hop.HopId + '\customer-patch-comparison')
    $sourceRelative = 'package-upgrade/' + $preparationId + '/customer-package-baseline/' + $baselineId + '/input'
    $targetRelative = 'package-upgrade/' + $preparationId + '/hops/' + $hop.HopId + '/customer-patch-comparison/patch-support-input'
    $evidenceRelative = 'package-upgrade/' + $preparationId + '/hops/' + $hop.HopId + '/customer-patch-comparison/evidence'
    $request = [ordered]@{ caseRoot = $caseRoot; preparationId = $preparationId; hopId = $hop.HopId; evidenceRelativePath = $evidenceRelative; patchSupportSource = $targetRelative; actor = $actor; source = [ordered]@{ baselineId = $baselineId; actualVersion = $actualVersion; inputRelativePath = $sourceRelative }; target = [ordered]@{ targetRelease = $hop.TargetRelease; inputRelativePath = $targetRelative } }
    $files.Add(@{ Path = (Join-Path $comparisonRoot 'evidence\patch-support-evidence.request.json'); Content = ($request | ConvertTo-Json -Depth 10) })
}
foreach ($directory in $directories) { [System.IO.Directory]::CreateDirectory($directory) | Out-Null }
foreach ($file in $files) { if (Test-Path -LiteralPath $file.Path) { Write-Error 'FAILED: output file appeared before write'; exit 3 }; [IO.File]::WriteAllText($file.Path, $file.Content, (New-Object Text.UTF8Encoding($true))) }
Write-Output ('PREPARED: ' + $preparation)
Write-Output ('TEMPLATE_FILES: ' + $files.Count)
Write-Output 'VALIDATION: CUSTOMER_PATCH_COMPARISON; no input read; no comparison, DB, SQL, Import, or Aras Export executed'
