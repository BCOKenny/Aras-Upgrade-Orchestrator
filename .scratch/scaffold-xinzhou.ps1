$caseRoot = 'K:\70.ArasUpgradeCases'
$caseNameCodePoints = [int[]](33455,27954)
$caseName = [string]::Concat(($caseNameCodePoints | ForEach-Object { [char]::ConvertFromUtf32($_) }))
$casePath = Join-Path -Path $caseRoot -ChildPath $caseName
$sourceSlug = '11sp9'
$targetSlug = 'r38'

$relativeDirectories = @(
    'core-tree\inputs\customer-11sp9\tree',
    'core-tree\inputs\customer-11sp9\evidence',
    'core-tree\inputs\ootb-11sp9\tree',
    'core-tree\inputs\ootb-11sp9\evidence',
    'core-tree\inputs\ootb-r38\tree',
    'core-tree\inputs\ootb-r38\evidence',
    'core-tree\attempts',
    'core-tree\completions',
    'core-tree\deliveries',
    'core-tree\requests',
    'core-tree\review-approvals',
    'package-upgrade\customer-baseline',
    'package-upgrade\hops',
    'package-upgrade\pending-route',
    'db-upgrade\hops',
    'handoff',
    '.orchestrator',
    '.orchestrator\locks',
    'inputs\source-11sp9\version-evidence',
    'inputs\source-11sp9\db-backup-evidence'
)

$templates = @{
    'core-tree\requests\preflight-request.template.json' = '{"mode":"PREFLIGHT","status":"待提供","customerInput":"core-tree/inputs/customer-11sp9","sourceOotbInput":"core-tree/inputs/ootb-11sp9","targetOotbInput":"core-tree/inputs/ootb-r38"}'
    'core-tree\requests\comparison-request.template.json' = '{"mode":"COMPARISON","status":"待提供","comparisonName":"customer-11sp9-To-ootb-r38","sourceVersion":"11.0 SP9","targetVersion":"R38"}'
    'core-tree\inputs\customer-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version Evidence Template\n\n- Version: 11.0 SP9\n- Build: <待提供>\n- Evidence path: <待提供>\n- Status: 待提供\n'
    'core-tree\inputs\ootb-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version Evidence Template\n\n- Version: 11.0 SP9\n- Build: <待提供>\n- Evidence path: <待提供>\n- Status: 待提供\n'
    'core-tree\inputs\ootb-r38\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version Evidence Template\n\n- Version: R38\n- Build: <待提供>\n- Evidence path: <待提供>\n- Status: 待提供\n'
    'package-upgrade\pending-route\route-input.template.json' = '{"status":"待提供","sourceVersion":"11.0 SP9","targetVersion":"R38","hops":"<待提供>","supportEvidence":"<待提供>"}'
    'handoff\preparation-checklist.md' = '# Preparation Checklist\n\n- Core Tree inputs: 待提供\n- Version evidence: 待提供\n- Package/DB route: 待提供\n- Official SOP/Patch/Support: 待提供\n- Formal case manifest: 不建立\n- History log: 不建立\n'
}

$forbidden = @(
    (Join-Path -Path $casePath -ChildPath 'aras-upgrade-case.json'),
    (Join-Path -Path $casePath -ChildPath '.orchestrator\history.jsonl')
)
$planned = @($relativeDirectories | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ }) + @($templates.Keys | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ })

if (-not (Test-Path -LiteralPath $caseRoot -PathType Container)) { throw 'Fixed case root is unavailable.' }
if (-not (Test-Path -LiteralPath $casePath -PathType Container)) { throw 'Case directory must already exist for this scaffold.' }
foreach ($path in $forbidden) { if (Test-Path -LiteralPath $path) { throw "Forbidden existing path: $path" } }
foreach ($path in $planned) { if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite existing path: $path" } }

foreach ($relativeDirectory in $relativeDirectories) {
    $target = Join-Path -Path $casePath -ChildPath $relativeDirectory
    [System.IO.Directory]::CreateDirectory($target) | Out-Null
}
foreach ($entry in $templates.GetEnumerator()) {
    $target = Join-Path -Path $casePath -ChildPath $entry.Key
    [System.IO.File]::WriteAllText($target, $entry.Value, [System.Text.UTF8Encoding]::new($true))
}
Write-Output "Created directories: $($relativeDirectories.Count)"
Write-Output "Created templates: $($templates.Count)"
