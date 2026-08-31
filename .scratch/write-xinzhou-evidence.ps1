$ErrorActionPreference = 'Stop'
try {
  $caseRoot = 'K:\70.ArasUpgradeCases'
  $caseName = [string]::Concat(([int[]](33455,27954) | ForEach-Object { [char]::ConvertFromUtf32($_) }))
  $case = Join-Path -Path $caseRoot -ChildPath $caseName
  $now = (Get-Date).ToString('o')
  $inputs = @(
    @{ Id = 'customer-11sp9'; SourceVersion = '11.0 SP9'; Slug = '11sp9'; Tree = (Join-Path -Path $case -ChildPath 'core-tree\inputs\customer-11sp9\tree'); Evidence = (Join-Path -Path $case -ChildPath 'core-tree\inputs\customer-11sp9\evidence') },
    @{ Id = 'ootb-11sp9'; SourceVersion = '11.0 SP9'; Slug = '11sp9'; Tree = (Join-Path -Path $case -ChildPath 'core-tree\inputs\ootb-11sp9\tree'); Evidence = (Join-Path -Path $case -ChildPath 'core-tree\inputs\ootb-11sp9\evidence') },
    @{ Id = 'ootb-r38'; SourceVersion = 'R38'; Slug = 'r38'; Tree = (Join-Path -Path $case -ChildPath 'core-tree\inputs\ootb-r38\tree'); Evidence = (Join-Path -Path $case -ChildPath 'core-tree\inputs\ootb-r38\evidence') }
  )
  function Get-TreeRelativePathCompat {
    param([string]$TreePath, [string]$FilePath)
    $treeFull = [System.IO.Path]::GetFullPath($TreePath).TrimEnd([char]92)
    $fileFull = [System.IO.Path]::GetFullPath($FilePath)
    $treePrefix = $treeFull + [char]92
    if (-not $fileFull.StartsWith($treePrefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'The file path is outside the verified Tree path.' }
    return $fileFull.Substring($treePrefix.Length).Replace([char]92, [char]47)
  }
  $planned = @()
  foreach ($i in $inputs) {
    if (-not (Test-Path -LiteralPath $i.Tree)) { throw ('Missing Tree: ' + $i.Tree) }
    if (-not (Test-Path -LiteralPath (Join-Path -Path $i.Tree -ChildPath 'Innovator\Client'))) { throw ('Missing Innovator\Client: ' + $i.Tree) }
    if (-not (Test-Path -LiteralPath (Join-Path -Path $i.Tree -ChildPath 'Innovator\Server'))) { throw ('Missing Innovator\Server: ' + $i.Tree) }
    if (-not (Test-Path -LiteralPath $i.Evidence)) { throw ('Missing Evidence directory: ' + $i.Evidence) }
    $i.Files = @(Get-ChildItem -LiteralPath $i.Tree -File -Recurse -Force | Sort-Object FullName)
    $i.IomPath = Join-Path -Path $i.Tree -ChildPath 'Innovator\Server\bin\IOM.dll'
    if (-not (Test-Path -LiteralPath $i.IomPath)) { throw ('Missing IOM.dll: ' + $i.IomPath) }
    $i.Iom = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($i.IomPath)
    foreach ($name in @('version-primary.md','integrity.md','integrity.sha256','source-provenance.md')) { $planned += (Join-Path -Path $i.Evidence -ChildPath $name) }
  }
  $existing = @($planned | Where-Object { Test-Path -LiteralPath $_ })
  if ($existing.Count -gt 0) { throw ('Refusing to overwrite existing evidence files: ' + ($existing -join '; ')) }
  $outputs = @()
  foreach ($i in $inputs) {
    $hashLines = @()
    foreach ($file in $i.Files) {
      $relative = Get-TreeRelativePathCompat -TreePath $i.Tree -FilePath $file.FullName
      $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
      $hashLines += ($hash + '  ' + $relative)
    }
    $hashPath = Join-Path -Path $i.Evidence -ChildPath 'integrity.sha256'
    $primaryPath = Join-Path -Path $i.Evidence -ChildPath 'version-primary.md'
    $integrityPath = Join-Path -Path $i.Evidence -ChildPath 'integrity.md'
    $provenancePath = Join-Path -Path $i.Evidence -ChildPath 'source-provenance.md'
    $primary = @("# Version Primary Evidence", '', "- Input identifier: $($i.Id)", "- Product version: $($i.SourceVersion) (provided by case parameters; pending evidence confirmation)", '- Edition: Enterprise (case default; Aras installation edition not separately identified)', "- Collector: BCO\kenny", "- Collection time: $now", "- IOM.dll FileVersion: $($i.Iom.FileVersion)", "- IOM.dll ProductVersion: $($i.Iom.ProductVersion)", "- IOM.dll path: $($i.IomPath)", '- Formal Aras Build: <PENDING>', '- Hotfix: <PENDING; operator confirmation required>', '- Status: PENDING') -join [Environment]::NewLine
    $integrity = @("# Integrity Evidence", '', "- Input identifier: $($i.Id)", "- Tree root: $($i.Tree)", "- Collection time: $now", '- Collector: BCO\kenny', "- File count: $($i.Files.Count)", "- SHA-256 list: integrity.sha256", '- Method: recursively enumerate files, sort by full path, hash file content with SHA-256, and record verified Tree-relative paths', "- IOM.dll FileVersion: $($i.Iom.FileVersion)", "- IOM.dll ProductVersion: $($i.Iom.ProductVersion)", "- Formal Aras Build: <PENDING>", '- Status: PENDING') -join [Environment]::NewLine
    $provenance = @("# Source Provenance", '', "- Input identifier: $($i.Id)", "- Destination: $($i.Tree)", '- Original source location: <PENDING; operator to provide>', '- Copy or export method: Export (default reference; operator to confirm)', '- Copy time: <PENDING>', '- Operator: BCO\kenny', '- Chain-of-custody references: source-provenance.md; version-primary.md; integrity.md; integrity.sha256', '- Human confirmation: BCO\kenny (case operator; date and scope pending)', '- Exclusions and reasons: <PENDING>', '- Status: PENDING') -join [Environment]::NewLine
    [System.IO.File]::WriteAllLines($hashPath, $hashLines, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($primaryPath, $primary, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($integrityPath, $integrity, [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::WriteAllText($provenancePath, $provenance, [System.Text.UTF8Encoding]::new($false))
    $outputs += @($hashPath,$primaryPath,$integrityPath,$provenancePath)
    Write-Output ("WRITTEN=$($i.Id)|FILES=$($i.Files.Count)|IOM_FILE_VERSION=$($i.Iom.FileVersion)|IOM_PRODUCT_VERSION=$($i.Iom.ProductVersion)")
  }
  Write-Output ('TOTAL_OUTPUTS=' + $outputs.Count)
} catch {
  Write-Error ('EVIDENCE_WRITE_FAILED: ' + $_.Exception.Message)
  exit 1
}
