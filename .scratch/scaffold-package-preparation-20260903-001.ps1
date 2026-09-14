$caseRoot = 'K:\70.ArasUpgradeCases\' + ([char]33437) + ([char]27954)
$prepName = 'preparation-20260903-001'
$baselineId = 'customer-11sp9'
$actor = 'BCO\kenny'
$hops = @(
    @{ Source = '11.0 SP8'; Target = '11.0 SP15' }
    @{ Source = '11.0 SP15'; Target = '12.0 SP18' }
    @{ Source = '12.0 SP18'; Target = 'R38' }
)
$allowedRoot = 'K:\70.ArasUpgradeCases'
$caseFull = [IO.Path]::GetFullPath($caseRoot)
$packageRoot = Join-Path $caseRoot 'package-upgrade'
$prepRoot = Join-Path $packageRoot $prepName

if (-not $caseFull.StartsWith($allowedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'BLOCKED: case root outside allowlisted root' }
if (-not [IO.Directory]::Exists($caseRoot)) { throw 'BLOCKED: case root does not exist' }
if (-not [IO.Directory]::Exists($packageRoot)) { throw 'BLOCKED: package-upgrade does not exist' }
if ([IO.Directory]::Exists($prepRoot) -or [IO.File]::Exists($prepRoot)) { throw 'BLOCKED: preparation target already exists' }
if ([string]::IsNullOrWhiteSpace($baselineId) -or $baselineId.Contains('\') -or $baselineId.Contains('/') -or $baselineId.Contains('..') -or [IO.Path]::IsPathRooted($baselineId)) { throw 'BLOCKED: invalid baseline id' }
$seen = @{}
foreach ($hop in $hops) {
    if ([string]::IsNullOrWhiteSpace($hop.Source) -or [string]::IsNullOrWhiteSpace($hop.Target) -or $hop.Source -eq $hop.Target) { throw 'BLOCKED: invalid hop' }
    $hopId = $hop.Source + '->' + $hop.Target
    if ($seen.ContainsKey($hopId)) { throw 'BLOCKED: duplicate hop' }
    $seen[$hopId] = $true
}

$dirs = @(
    (Join-Path $caseRoot 'rule-sets')
    (Join-Path $prepRoot 'customer-package-baseline\' + $baselineId + '\input')
    (Join-Path $prepRoot 'customer-package-baseline\' + $baselineId + '\evidence')
    (Join-Path $prepRoot 'hops')
    (Join-Path $prepRoot 'manifests')
)
$plans = @()
$evidenceRequests = @()
foreach ($hop in $hops) {
    $hopId = (($hop.Source + '-to-' + $hop.Target) -replace '[^A-Za-z0-9.-]', '-').Trim('-')
    $hopRoot = Join-Path $prepRoot ('hops\' + $hopId + '\customer-patch-comparison')
    $dirs += @(
        (Join-Path $hopRoot 'patch-support-input')
        (Join-Path $hopRoot 'evidence')
        (Join-Path $hopRoot 'comparison-attempts')
        (Join-Path $hopRoot 'candidate-package-imports')
    )
    $targetPath = ('hops/' + $hopId + '/customer-patch-comparison/patch-support-input')
    $plans += [ordered]@{ comparisonId=$hopId; source=[ordered]@{role='CustomerBaseline'; baselineId=$baselineId; actualVersion='11.0 SP9'; inputRelativePath=('customer-package-baseline/' + $baselineId + '/input')}; target=[ordered]@{role='TargetPackage'; targetRelease=$hop.Target; inputRelativePath=$targetPath}; customerBaselineInput=('customer-package-baseline/' + $baselineId + '/input'); patchSupportInput=$targetPath; evidence=('hops/' + $hopId + '/customer-patch-comparison/evidence'); comparisonAttempts=('hops/' + $hopId + '/customer-patch-comparison/comparison-attempts'); candidatePackageImports=('hops/' + $hopId + '/customer-patch-comparison/candidate-package-imports'); status='PENDING' }
    $evidenceRequests += [ordered]@{ caseRoot=$caseRoot; preparationId=$prepName; hopId=$hopId; evidenceRelativePath=('hops/' + $hopId + '/customer-patch-comparison/evidence/patch-support-evidence.request.json'); patchSupportSource=$targetPath; actor=$actor; source=[ordered]@{baselineId=$baselineId; actualVersion='11.0 SP9'; inputRelativePath=('customer-package-baseline/' + $baselineId + '/input')}; target=[ordered]@{targetRelease=$hop.Target; inputRelativePath=$targetPath} }
}

foreach ($dir in $dirs) { [IO.Directory]::CreateDirectory($dir) | Out-Null }
$planPath = Join-Path $prepRoot 'manifests\package-preparation-plan.template.json'
$plan = [ordered]@{ preparationId=$prepName; caseRoot=$caseRoot; workflow='CUSTOMER_PATCH_COMPARISON'; baselineId=$baselineId; createdBy=$actor; comparisons=$plans }
if ([IO.File]::Exists($planPath)) { throw 'ERROR: template already exists' }
[IO.File]::WriteAllText($planPath, ($plan | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
foreach ($request in $evidenceRequests) {
    $path = Join-Path $prepRoot ($request.evidenceRelativePath -replace '/', '\')
    if ([IO.File]::Exists($path)) { throw 'ERROR: evidence template already exists' }
    [IO.File]::WriteAllText($path, ($request | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
}
'CREATED: ' + $prepRoot
'TEMPLATES: ' + (1 + $evidenceRequests.Count)
'VALIDATION: scaffold only; no inputs read'
