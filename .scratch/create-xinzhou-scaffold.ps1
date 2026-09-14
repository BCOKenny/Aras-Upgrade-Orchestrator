$caseRoot = 'K:\70.ArasUpgradeCases'
$caseNameCodePoints = [int[]](33455,27954)
$caseName = [string]::Concat(($caseNameCodePoints | ForEach-Object { [char]::ConvertFromUtf32($_) }))
$target = Join-Path -Path $caseRoot -ChildPath $caseName
$sourceSlug = '11sp9'
$targetSlug = 'r38'
$relativeDirectories = @(
  'core-tree\inputs\customer-11sp9\tree',
  'core-tree\inputs\customer-11sp9\evidence',
  'core-tree\inputs\ootb-11sp9\tree',
  'core-tree\inputs\ootb-11sp9\evidence',
  'core-tree\inputs\ootb-r38\tree',
  'core-tree\inputs\ootb-r38\evidence',
  'core-tree\attempts', 'core-tree\completions', 'core-tree\deliveries',
  'core-tree\requests', 'core-tree\review-approvals',
  'package-upgrade\customer-baseline', 'package-upgrade\hops', 'package-upgrade\pending-route',
  'db-upgrade\hops', 'handoff', '.orchestrator', '.orchestrator\locks',
  'inputs\source-11sp9\version-evidence', 'inputs\source-11sp9\db-backup-evidence'
)
$templateFiles = @{
  'core-tree\requests\preflight-request.template.json' = '{"status":"待提供","customerInput":"core-tree/inputs/customer-11sp9","sourceOotbInput":"core-tree/inputs/ootb-11sp9","targetOotbInput":"core-tree/inputs/ootb-r38"}'
  'core-tree\requests\comparison-request.template.json' = '{"status":"待提供","comparisonName":"customer-11sp9-To-ootb-r38","inputs":["customer-11sp9","ootb-11sp9","ootb-r38"]}'
  'core-tree\inputs\customer-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence`n`n- Version: <待提供>`n- Build: <待提供>`n- Evidence path: <待提供>`n- Status: 待提供'
  'core-tree\inputs\ootb-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence`n`n- Version: <待提供>`n- Build: <待提供>`n- Evidence path: <待提供>`n- Status: 待提供'
  'core-tree\inputs\ootb-r38\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence`n`n- Version: <待提供>`n- Build: <待提供>`n- Evidence path: <待提供>`n- Status: 待提供'
  'package-upgrade\pending-route\route-input.template.json' = '{"status":"待提供","sourceVersion":"11.0 SP9","targetVersion":"R38","hops":"<待提供>"}'
  'handoff\preparation-checklist.md' = '# Preparation checklist`n`n- Operator: BCO\\kenny`n- Source version: 11.0 SP9`n- Target version: R38`n- Package/DB route: <待提供>`n- Status: 待提供'
}
$formalFiles = @((Join-Path -Path $target -ChildPath 'aras-upgrade-case.json'), (Join-Path -Path $target -ChildPath '.orchestrator\history.jsonl'))
if (Test-Path -LiteralPath $target) { throw "Target already exists: $target" }
foreach ($p in $formalFiles) { if (Test-Path -LiteralPath $p) { throw "Formal file already exists: $p" } }
foreach ($r in $relativeDirectories) { $p = Join-Path -Path $target -ChildPath $r; if (Test-Path -LiteralPath $p) { throw "Path already exists: $p" } }
foreach ($r in $templateFiles.Keys) { $p = Join-Path -Path $target -ChildPath $r; if (Test-Path -LiteralPath $p) { throw "Template already exists: $p" } }
$guid = [guid]::NewGuid()
foreach ($r in $relativeDirectories) { [System.IO.Directory]::CreateDirectory((Join-Path -Path $target -ChildPath $r)) | Out-Null }
foreach ($r in $templateFiles.Keys) { $p = Join-Path -Path $target -ChildPath $r; [System.IO.File]::WriteAllText($p, $templateFiles[$r], (New-Object System.Text.UTF8Encoding($false))) }
Write-Output "CASE_GUID=$guid"
Write-Output "TARGET=$target"
Write-Output "DIRECTORIES_CREATED=$($relativeDirectories.Count)"
Write-Output "TEMPLATES_CREATED=$($templateFiles.Count)"
