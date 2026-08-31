# 芯洲案件目錄範例

以下是建議的正式案件根目錄；尖括號內容必須由操作人員替換，不能直接當成正式路徑。

```text
<CASE_ROOT>\\Xinzhou-To-12SP9\\
├─ aras-upgrade-case.json
├─ .orchestrator\\
│  ├─ history.jsonl
│  └─ locks\\
├─ inputs\\
│  ├─ source-11sp9\\
│  │  ├─ version-evidence\\
│  │  ├─ db-backup-evidence\\
│  │  ├─ customer-core-tree\\
│  │  └─ original-package-export\\
│  └─ ootb-11sp9\\
│     └─ Solutions\\
├─ hops\\
│  └─ 11sp9-to-12sp9\\
│     ├─ hop.json
│     ├─ support\\
│     │  ├─ source-11sp9\\
│     │  └─ target-12sp9\\
│     ├─ package\\
│     │  ├─ customer-baseline\\
│     │  ├─ ootb-difference\\
│     │  ├─ solutions-backups\\
│     │  ├─ work\\
│     │  └─ completion\\
│     ├─ db\\
│     │  ├─ pre-hop-backup-evidence\\
│     │  ├─ operator-run\\
│     │  ├─ login-validation\\
│     │  └─ post-hop-backup-evidence\\
│     └─ logs\\
├─ core-tree\\
├─ deliveries\\
└─ handoff\\
```

原始輸入、原始 `Support`、原始 Package 與正式 DB 不應放入專案 Git；案件目錄只登錄其位置、版本、Checksum 與證據索引。
