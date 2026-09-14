$ErrorActionPreference = 'Stop'
$caseRoot = 'K:\70.ArasUpgradeCases'
function U([int[]]$points) { [string]::Concat(($points | ForEach-Object { [char]::ConvertFromUtf32($_) })) }
$caseName = U ([int[]](33455,27954))
$pending = U ([int[]](24453,25552,20379))
$casePath = Join-Path -Path $caseRoot -ChildPath $caseName
$newline = [Environment]::NewLine
$relativeDirectories = @(
  'core-tree\inputs\customer-11sp9\tree', 'core-tree\inputs\customer-11sp9\evidence',
  'core-tree\inputs\ootb-11sp9\tree', 'core-tree\inputs\ootb-11sp9\evidence',
  'core-tree\inputs\ootb-r38\tree', 'core-tree\inputs\ootb-r38\evidence',
  'core-tree\attempts', 'core-tree\completions', 'core-tree\deliveries',
  'core-tree\requests', 'core-tree\review-approvals',
  'package-upgrade\customer-baseline', 'package-upgrade\hops',
  'package-upgrade\pending-route', 'db-upgrade\hops', 'handoff',
  '.orchestrator', '.orchestrator\locks',
  'inputs\source-11sp9\version-evidence', 'inputs\source-11sp9\db-backup-evidence'
)
$templates = @{
  'core-tree\requests\preflight-request.template.json' = '{"status":"' + $pending + '","inputPaths":{"customer":"core-tree/inputs/customer-11sp9/tree","sourceOotb":"core-tree/inputs/ootb-11sp9/tree","targetOotb":"core-tree/inputs/ootb-r38/tree"},"evidence":"<' + $pending + '>"}'
  'core-tree\requests\comparison-request.template.json' = '{"status":"' + $pending + '","comparisonName":"customer-11sp9-To-ootb-r38","preflightAttempt":"<' + $pending + '>","inputs":"<' + $pending + '>"}'
  'core-tree\inputs\customer-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence' + $newline + $newline + '- version: <' + $pending + '>' + $newline + '- build: <' + $pending + '>' + $newline + '- source: customer-11sp9' + $newline + '- status: ' + $pending
  'core-tree\inputs\ootb-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence' + $newline + $newline + '- version: <' + $pending + '>' + $newline + '- build: <' + $pending + '>' + $newline + '- source: ootb-11sp9' + $newline + '- status: ' + $pending
  'core-tree\inputs\ootb-r38\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# Version evidence' + $newline + $newline + '- version: <' + $pending + '>' + $newline + '- build: <' + $pending + '>' + $newline + '- source: ootb-r38' + $newline + '- status: ' + $pending
  'package-upgrade\pending-route\route-input.template.json' = '{"status":"' + $pending + '","sourceVersion":"11.0 SP9","targetVersion":"R38","hops":"<' + $pending + '>","support":"<' + $pending + '>","patch":"<' + $pending + '>"}'
  'handoff\preparation-checklist.md' = '# Preparation checklist' + $newline + $newline + '- operator: BCO\kenny' + $newline + '- customer: <' + $pending + '>' + $newline + '- official SOP: <' + $pending + '>' + $newline + '- Package/DB route: <' + $pending + '>' + $newline + '- status: ' + $pending
}
$forbidden = @((Join-Path -Path $casePath -ChildPath 'aras-upgrade-case.json'), (Join-Path -Path $casePath -ChildPath '.orchestrator\history.jsonl'))
if (-not (Test-Path -LiteralPath $caseRoot -PathType Container)) { throw 'Case root is unavailable.' }
if (Test-Path -LiteralPath $casePath) {
  $existing = @(Get-ChildItem -LiteralPath $casePath -Force)
  if ($existing.Count -ne 0) { throw 'Case target already contains data; refusing to overwrite.' }
}
$allPaths = @($relativeDirectories | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ }) + @($templates.Keys | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ }) + $forbidden
$basePath = [System.IO.Path]::GetFullPath($caseRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
foreach ($path in $allPaths) {
  if (Test-Path -LiteralPath $path) { throw 'Planned path already exists; refusing to overwrite.' }
  $fullPath = [System.IO.Path]::GetFullPath($path)
  if (-not $fullPath.StartsWith($basePath, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Planned path escapes the fixed case root.' }
}
foreach ($relativeDirectory in $relativeDirectories) { [System.IO.Directory]::CreateDirectory((Join-Path -Path $casePath -ChildPath $relativeDirectory)) | Out-Null }
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
foreach ($relativeTemplate in $templates.Keys) {
  $templatePath = Join-Path -Path $casePath -ChildPath $relativeTemplate
  [System.IO.File]::WriteAllText($templatePath, $templates[$relativeTemplate], $utf8NoBom)
}
Write-Output ('CASE_PATH=' + $casePath)
Write-Output ('CREATED_DIRECTORIES=' + $relativeDirectories.Count)
Write-Output ('CREATED_TEMPLATES=' + $templates.Count)
