$caseRoot = 'K:\70.ArasUpgradeCases'
$caseNameCodePoints = [int[]](33455,27954)
$caseName = [string]::Concat(($caseNameCodePoints | ForEach-Object { [char]::ConvertFromUtf32($_) }))
$casePath = Join-Path -Path $caseRoot -ChildPath $caseName
$relativeDirectories = @(
  'core-tree\inputs\customer-11sp9\tree','core-tree\inputs\customer-11sp9\evidence',
  'core-tree\inputs\ootb-11sp9\tree','core-tree\inputs\ootb-11sp9\evidence',
  'core-tree\inputs\ootb-r38\tree','core-tree\inputs\ootb-r38\evidence',
  'core-tree\attempts','core-tree\completions','core-tree\deliveries','core-tree\requests','core-tree\review-approvals',
  'package-upgrade\customer-baseline','package-upgrade\hops','package-upgrade\pending-route',
  'db-upgrade\hops','handoff','.orchestrator','.orchestrator\locks',
  'inputs\source-11sp9\version-evidence','inputs\source-11sp9\db-backup-evidence'
)
$relativeFiles = @(
  'core-tree\requests\preflight-request.template.json','core-tree\requests\comparison-request.template.json',
  'core-tree\inputs\customer-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md',
  'core-tree\inputs\ootb-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md',
  'core-tree\inputs\ootb-r38\evidence\VERSION-EVIDENCE-TEMPLATE.md',
  'package-upgrade\pending-route\route-input.template.json','handoff\preparation-checklist.md'
)
$forbidden = @('aras-upgrade-case.json','.orchestrator\history.jsonl')
$allPaths = @($relativeDirectories + $relativeFiles + $forbidden) | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ }
foreach($p in $allPaths){ if(Test-Path -LiteralPath $p){ throw "Preflight failed: existing path $p" } }
$templates = @{}
$templates['core-tree\requests\preflight-request.template.json'] = '{"status":"<TO_PROVIDE>","customerInput":"customer-11sp9","sourceOotbInput":"ootb-11sp9","targetOotbInput":"ootb-r38","evidence":"<TO_PROVIDE>"}'
$templates['core-tree\requests\comparison-request.template.json'] = '{"status":"<TO_PROVIDE>","comparisonName":"customer-11sp9-To-ootb-r38","inputs":["customer-11sp9","ootb-11sp9","ootb-r38"]}'
$templates['package-upgrade\pending-route\route-input.template.json'] = '{"status":"<TO_PROVIDE>","sourceVersion":"11.0 SP9","targetVersion":"R38","hops":"<TO_PROVIDE>","support":"<TO_PROVIDE>"}'
$templates['handoff\preparation-checklist.md'] = '# Handoff preparation checklist`n`n- Case: <TO_PROVIDE>`n- Operator: <TO_PROVIDE>`n- Core Tree inputs: <TO_PROVIDE>`n- Package/DB route: <TO_PROVIDE>`n- Status: <TO_PROVIDE>'
foreach($p in $relativeDirectories){ [System.IO.Directory]::CreateDirectory((Join-Path -Path $casePath -ChildPath $p)) | Out-Null }
foreach($p in $relativeFiles){ $full=Join-Path -Path $casePath -ChildPath $p; $content = if($templates.ContainsKey($p)){$templates[$p]}else{'# Version evidence template`n`n- Version: <TO_PROVIDE>`n- Build: <TO_PROVIDE>`n- Checksum: <TO_PROVIDE>`n- Status: <TO_PROVIDE>'}; [System.IO.File]::WriteAllText($full,$content,(New-Object System.Text.UTF8Encoding($false))) }
Write-Output "Created scaffold at $casePath"
