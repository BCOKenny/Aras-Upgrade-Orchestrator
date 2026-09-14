$ErrorActionPreference = 'Stop'

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

$templateFiles = @{
    'core-tree\requests\preflight-request.template.json' = '{"mode":"<待提供>","casePath":"<待提供>","inputs":{"customer":"core-tree\\inputs\\customer-11sp9\\tree","sourceOotb":"core-tree\\inputs\\ootb-11sp9\\tree","targetOotb":"core-tree\\inputs\\ootb-r38\\tree"},"status":"<待提供>"}'
    'core-tree\requests\comparison-request.template.json' = '{"comparisonName":"customer-11sp9-To-ootb-r38","customerInput":"customer-11sp9","sourceOotbInput":"ootb-11sp9","targetOotbInput":"ootb-r38","status":"<待提供>"}'
    'core-tree\inputs\customer-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# 版本證據範本`n`n- 版本：11.0 SP9`n- Build：<待提供>`n- 證據路徑：<待提供>`n- Checksum：<待提供>`n- 狀態：待確認`n'
    'core-tree\inputs\ootb-11sp9\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# 版本證據範本`n`n- 版本：11.0 SP9 OOTB`n- Build：<待提供>`n- 證據路徑：<待提供>`n- Checksum：<待提供>`n- 狀態：待確認`n'
    'core-tree\inputs\ootb-r38\evidence\VERSION-EVIDENCE-TEMPLATE.md' = '# 版本證據範本`n`n- 版本：R38 OOTB`n- Build：<待提供>`n- 證據路徑：<待提供>`n- Checksum：<待提供>`n- 狀態：待確認`n'
    'package-upgrade\pending-route\route-input.template.json' = '{"sourceVersion":"11.0 SP9","targetVersion":"R38","hops":"<待提供>","officialSop":"<待提供>","status":"待確認"}'
    'handoff\preparation-checklist.md' = '# 交接準備檢查表`n`n- 客戶 Core Tree：待人工放置`n- 來源 OOTB Core Tree：待人工放置`n- 目標 OOTB Core Tree：待人工放置`n- 三份版本證據：待人工提供`n- Package／DB 路徑與官方資訊：待人工提供`n- Core Tree 結構確認：待人工確認`n'
}

$forbiddenFiles = @(
    (Join-Path -Path $casePath -ChildPath 'aras-upgrade-case.json'),
    (Join-Path -Path $casePath -ChildPath '.orchestrator\history.jsonl')
)
$allPaths = @($relativeDirectories | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ }) + @($templateFiles.Keys | ForEach-Object { Join-Path -Path $casePath -ChildPath $_ })
if(-not [System.IO.Directory]::Exists($caseRoot)){ throw '案件根目錄不存在' }
if(-not [System.IO.Directory]::Exists($casePath)){ throw '案件目標目錄不存在' }
foreach($path in $forbiddenFiles){ if([System.IO.File]::Exists($path)){ throw "禁止覆寫既有正式檔案: $path" } }
foreach($path in $allPaths){ if([System.IO.File]::Exists($path) -or [System.IO.Directory]::Exists($path)){ throw "目標已存在，停止以避免覆寫: $path" } }

$created = @()
foreach($relative in $relativeDirectories){
    $target = Join-Path -Path $casePath -ChildPath $relative
    [System.IO.Directory]::CreateDirectory($target) | Out-Null
    $created += $target
}
foreach($entry in $templateFiles.GetEnumerator()){
    $target = Join-Path -Path $casePath -ChildPath $entry.Key
    $parent = [System.IO.Path]::GetDirectoryName($target)
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    [System.IO.File]::WriteAllText($target, $entry.Value, [System.Text.UTF8Encoding]::new($false))
    $created += $target
}
Write-Output 'SCAFFOLD_CREATED'
$created | ForEach-Object { Write-Output $_ }
