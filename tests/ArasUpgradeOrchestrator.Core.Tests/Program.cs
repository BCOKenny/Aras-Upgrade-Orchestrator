using ArasUpgradeOrchestrator.Core.Aml;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.CoreTrees;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Packages;
using ArasUpgradeOrchestrator.Core.Rules;
using ArasUpgradeOrchestrator.Core.Safety;
using ArasUpgradeOrchestrator.Core.Tasks;

var tests = new (string Name, Func<Task> Run)[]
{
    ("升級路徑拒絕不連續跳點", RouteRejectsDiscontinuity),
    ("任務圖區分 Package 子任務與依序跳點執行", TaskGraphBuildsDependencies),
    ("案件清單可建立及驗證讀回", CaseManifestRoundTrips),
    ("既有升級路徑只能追加新版不能改寫", ExistingRouteCannotBeRewritten),
    ("歷程只追加且更正保留原事件", HistoryIsAppendOnly),
    ("失敗後無安全證據不得重試", RetryRequiresEvidence),
    ("重新開啟案件將未完成嘗試標記中斷", RecoveryMarksInterrupted),
    ("安全白名單與必要條件產生三級判定", SafetyPolicyUsesThreeLevels),
    ("目錄鎖阻擋重疊並允許獨立目錄", DirectoryLeasePreventsOverlap),
    ("受控執行保存確認、快照與結果", ControlledExecutionRecordsOutcome),
    ("執行快照與動作不一致時阻擋", SnapshotMismatchIsBlocked),
    ("預設外部執行器不執行正式操作", DefaultExecutorBlocksExternalAction),
    ("Skill Map 包含主入口與八個無重疊功能", SkillMapDefinesAllRoutes),
    ("主 Skill 路由未實作功能時明確停止", MainSkillStopsAtUnavailableRoute),
    ("案件管理 Skill 引用正式核心並守住責任邊界", CaseSkillReferencesTestedCore)
    ,("一次性 Package 流程首次 DB 變更前鎖定且不可重開", CustomerPackageFlowLocksOnce)
    ,("一次性 Package 流程僅接受相符 DB 備份還原證據", CustomerPackageRollbackRequiresMatchingBackup)
    ,("客戶 Package 基準完成後永久鎖定", CustomerPackageCompletionIsPermanent)
    ,("未處置 Export 排除項目阻擋 Package 基準完成", CustomerPackageExclusionsBlockCompletion)
    ,("Package 外部動作必須通過流程鎖與固定 Checksum", CustomerPackageActionRequiresLockAndChecksum)
    ,("客戶 Package 功能 Skill 引用正式受測核心", CustomerPackageSkillReferencesTestedCore)
    ,("AML 公開模型分類六種節點並遞迴所有巢狀層級", AmlClassifiesAndTraversesNestedNodes)
    ,("Package CompareKey 依 id 或 canonicalized where 建立", PackageCompareKeyUsesSpecifiedIdentity)
    ,("Package CompareKey 缺失或同側重複時轉人工確認", PackageCompareKeyAmbiguityRequiresManualReview)
    ,("AML 安全解析拒絕 DTD 並保留宣告 Namespace 與 CDATA", AmlParsingIsSafeAndPreservesSubtree)
    ,("Package XML 僅依根目錄相對路徑配對", PackageXmlPairsByRelativePathOnly)
    ,("AML 語意相等忽略格式 Attribute 與 Relationship 順序", AmlSemanticEqualityIgnoresPureFormatting)
    ,("AML 語意比較將重複 Scalar Property 轉人工確認", AmlSemanticEqualityBlocksAmbiguousScalarProperties)
    ,("預設 Rule 2 規則集包含可驗證的七個步驟", DefaultRule2DraftContainsValidatedSteps)
    ,("規則草稿拒絕重複順序與不支援步驟", RuleDraftValidationRejectsAmbiguity)
    ,("AI 不得建立或發布規則版本", AiCannotCreateOrPublishRules)
    ,("規則發布只追加不可覆寫版本", PublishedRuleVersionsAreImmutable)
    ,("規則儲存拒絕冒用建立者與已發布內容竄改", RuleStoreRejectsIdentityMismatchAndTampering)
    ,("版本例外依 StepId 覆蓋共用規則", VersionExceptionOverridesCommonStep)
    ,("多個版本例外對同一步驟結果不同時阻擋", ConflictingVersionExceptionsAreBlocked)
    ,("規則管理 Skill 引用正式受測核心並限制 AI 權限", RuleManagementSkillReferencesTestedCore)
    ,("Rule 1 依四種 Item 結果產生 SourceDiff 與 TargetDiff", Rule1ProducesTwoSidedItemDiff)
    ,("Rule 1 遇到同側重複 CompareKey 時保留兩端並轉人工確認", Rule1RetainsAmbiguousPairing)
    ,("Rule 1 遇到不可靠 Scalar Property 配對時保留差異並轉人工確認", Rule1RecordsSemanticManualReview)
    ,("OOTB 跳點差異建立器只寫入新產出並保持原始 Package 不變", OotbHopDiffBuilderPreservesInputs)
    ,("OOTB 跳點差異建立器隔離單一 XML 錯誤並保持整體阻擋", OotbHopDiffBuilderIsolatesXmlErrors)
    ,("完成的 OOTB 跳點差異封裝雙端內容並以單一 Checksum 驗證重用", OotbHopDiffArtifactPackagesAndVerifies)
    ,("未解除人工確認時只保存 Incomplete 摘要且不產生封裝", BlockedOotbHopDiffCannotBePackaged)
    ,("OOTB 跳點差異封裝缺少任一端內容時不得重用", OotbHopDiffVerifierRequiresBothSides)
    ,("OOTB 跳點差異 Skill 引用正式核心並守住不可變輸入", OotbHopDiffSkillReferencesTestedCore)
    ,("OOTB 跳點差異固定共同規則與版本例外的解析快照", OotbHopDiffPinsResolvedRuleSnapshot)
    ,("OOTB 跳點差異封裝缺少處理摘要時不得重用", OotbHopDiffVerifierRequiresSummary)
    ,("OOTB 跳點差異封裝含路徑逸出項目時不得重用", OotbHopDiffVerifierRejectsUnsafeEntries)
    ,("Rule 2 依七步規則更新直接 Scalar 且不修改 Item Attribute", Rule2AppliesSevenScalarSteps)
    ,("Rule 2 遞迴處理 Relationship 並轉換 federated Property", Rule2RecursesAndCopiesFederatedProperty)
    ,("Rule 2 遇到重複 Scalar 時局部繼續但整體阻擋", Rule2AmbiguousScalarRequiresManualReview)
    ,("正式適配 Package 先驗證差異包與備份再建立雙端工作副本", AdaptedPackagePreparesAfterVerificationAndBackup)
    ,("Solutions 備份失敗時 Rule 2 不得寫入任何工作副本", AdaptedPackageBackupFailurePreservesSolutions)
    ,("正式適配 Package Skill 引用受測核心與備份關卡", AdaptedPackageSkillReferencesTestedCore)
    ,("Package 整合拒絕將已驗證差異包套用到不同跳點", PackageIntegrationRejectsHopIdentityMismatch)
    ,("Package 整合由一次性基準串接 Rule 1 與 Rule 2 完成正式適配", PackageIntegrationCompletesEndToEnd)
    ,("Package 整合在 Rule 1 封裝遭竄改時保持 Solutions 零寫入", PackageIntegrationRejectsTamperedArtifactBeforeWrite)
    ,("Package 整合在 Rule 2 人工確認未解除時阻擋正式完成", PackageIntegrationBlocksFinalizationForManualReview)
    ,("Core Tree 必須驗證三份版本證據與 Client Server 結構", CoreTreeValidatesInputs)
    ,("Core Tree Client 文字比較只忽略換行與 BOM", CoreTreeClientTextComparisonFollowsRules)
    ,("Core Tree Server 只依固定規則選擇文字比較", CoreTreeServerComparisonPinsRuleSet)
    ,("Core Tree 無法解碼時記錄二進位 fallback 原因", CoreTreeRecordsBinaryFallbackReason)
    ,("Core Tree 邏輯配對限制相同目錄主檔名並回報多候選", CoreTreeLogicalPathResolutionIsDeterministic)
    ,("Core Tree 正確分類 A B C 並忽略未修改檔案", CoreTreeClassifiesCustomerFiles)
    ,("Core Tree 多候選與 A 類碰撞保持人工確認", CoreTreeAmbiguityBlocksClassification)
    ,("Core Tree builder 建立新嘗試產出且保持三份輸入不變", CoreTreeBuilderProducesCompletedOutput)
    ,("Core Tree 人工確認只能產生 Incomplete 且不得覆寫", CoreTreeBuilderBlocksIncompleteAndOverwrite)
    ,("Core Tree Skill 引用正式核心並守住完成與外部邊界", CoreTreeSkillReferencesTestedCore)
    ,("Core Tree 細項能力 Skill 架構具有 ADR 與領域術語", CoreTreeCapabilitySkillArchitectureIsRecorded)
    ,("Core Tree 細項能力 Skill 具有穩定共用契約", CoreTreeCapabilityContractIsStable)
    ,("Core Tree 輸入驗證 Skill 具有完整契約與驗收案例", CoreTreeInputValidationSkillPackageIsComplete)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception}");
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"\n{tests.Length - failures.Count}/{tests.Length} tests passed.");
if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine + Environment.NewLine, failures));
    return 1;
}
return 0;

static Task RouteRejectsDiscontinuity()
{
    Assert.Throws<ArgumentException>(() => UpgradeRoute.Create(1,
    [
        new UpgradeHop("11SP5", "11SP15", @"C:\customer\11SP15\Support"),
        new UpgradeHop("12SP9", "R38", @"C:\customer\R38\Support")
    ], DateTimeOffset.UtcNow));
    return Task.CompletedTask;
}

static Task TaskGraphBuildsDependencies()
{
    var route = TestRoute();
    var graph = TaskGraph.Build(route);
    var first = graph.Get("hop.1.execute");
    var second = graph.Get("hop.2.execute");
    var packageTwo = graph.Get("hop.2.package");

    Assert.Equal(UpgradeTaskKind.HopExecution, first.Kind);
    Assert.True(first.IsExternalManualAction, "跳點執行應明確標示為外部人工操作。 ");
    Assert.SequenceEqual(new[] { "hop.1.package" }, first.Dependencies);
    Assert.SequenceEqual(new[] { "hop.2.package", "hop.1.record-db-backup" }, second.Dependencies);
    Assert.Equal(UpgradeTaskKind.HopPackage, packageTwo.Kind);
    Assert.SequenceEqual(new[] { "hop.1.execute" }, graph.Get("hop.1.validate-login").Dependencies);
    Assert.SequenceEqual(new[] { "hop.1.validate-login" }, graph.Get("hop.1.record-db-backup").Dependencies);
    Assert.SequenceEqual(new[] { "hop.2.record-db-backup", "core-tree.compare" }, graph.Get("delivery.final").Dependencies);
    return Task.CompletedTask;
}

static async Task CaseManifestRoundTrips()
{
    await using var scope = TestScope.Create();
    var route = TestRoute(scope.Root);
    var manifest = CaseManifest.Create(Guid.NewGuid(), "CUST-A", "11SP5", "R38", route, DateTimeOffset.Parse("2026-08-03T00:00:00Z"));
    var store = new CaseStore(scope.CaseRoot);
    await store.CreateAsync(manifest);
    var loaded = await store.LoadAsync();

    Assert.Equal(manifest.CaseId, loaded.CaseId);
    Assert.Equal("CUST-A", loaded.CustomerCode);
    Assert.Equal(2, loaded.CurrentRoute.Hops.Count);
    Assert.True(File.Exists(Path.Combine(scope.CaseRoot, CaseStore.ManifestFileName)), "案件清單必須位於案件根層。 ");
}

static async Task HistoryIsAppendOnly()
{
    await using var scope = TestScope.Create();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var caseId = Guid.NewGuid();
    var original = await history.AppendAsync(caseId, "task.note", "task-1", "operator", new { value = "wrong" }, DateTimeOffset.UtcNow);
    await history.AppendCorrectionAsync(caseId, original.EventId, "task-1", "operator", "輸入錯誤", new { value = "right" }, DateTimeOffset.UtcNow);

    var events = await ReadAll(history);
    Assert.Equal(2, events.Count);
    Assert.Equal("wrong", events[0].Payload.GetProperty("value").GetString());
    Assert.Equal(original.EventId, events[1].CorrectsEventId);
    Assert.Equal(2, File.ReadLines(history.Path).Count());
}

static async Task ExistingRouteCannotBeRewritten()
{
    await using var scope = TestScope.Create();
    var manifest = CaseManifest.Create(Guid.NewGuid(), "CUST-A", "11SP5", "R38", TestRoute(scope.Root), DateTimeOffset.UtcNow);
    var store = new CaseStore(scope.CaseRoot);
    await store.CreateAsync(manifest);
    var rewritten = manifest with
    {
        Routes =
        [
            UpgradeRoute.Create(1,
            [
                new UpgradeHop("11SP5", "12SP9", Path.Combine(scope.Root, "12SP9", "Support")),
                new UpgradeHop("12SP9", "R38", Path.Combine(scope.Root, "R38", "Support"))
            ], manifest.CurrentRoute.CreatedAt)
        ]
    };

    await Assert.ThrowsAsync<InvalidOperationException>(() => store.SavePlanningUpdateAsync(rewritten));
}

static async Task RetryRequiresEvidence()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var service = new ExecutionAttemptService(caseId, history);
    var snapshot = TestSnapshot("task.retry", scope.Root);
    var first = await service.StartAsync(snapshot, "operator");
    await service.FailAsync(first, "operator", "failed", "log-1");

    await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(snapshot, "operator"));
    var second = await service.StartAsync(snapshot, "operator", new RetryEvidence(RetryBasis.RolledBack, "rollback-proof-1"));
    Assert.Equal(2, second.Sequence);
    Assert.NotEqual(first.AttemptId, second.AttemptId);
}

static async Task RecoveryMarksInterrupted()
{
    await using var scope = TestScope.Create();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var service = new ExecutionAttemptService(Guid.NewGuid(), history);
    await service.StartAsync(TestSnapshot("task.interrupted", scope.Root), "operator");

    Assert.Equal(1, await service.RecoverInterruptedAsync("recovery"));
    var attempt = (await service.GetAttemptsAsync("task.interrupted")).Single();
    Assert.Equal(AttemptState.Interrupted, attempt.State);
    Assert.Equal(0, await service.RecoverInterruptedAsync("recovery"));
}

static Task SafetyPolicyUsesThreeLevels()
{
    var root = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "safe-root"));
    var prerequisites = new Dictionary<string, bool> { ["manifest"] = true, ["backup"] = true };
    var entry = new SafetyWhitelistEntry("copy.work", "1", [root], new HashSet<string> { "manifest", "backup" });
    var policy = new SafetyPolicy([entry]);

    var automatic = policy.Evaluate(new ControlledAction("copy.work", "1", Path.Combine(root, "child"), "ABC", false, prerequisites));
    var confirmation = policy.Evaluate(new ControlledAction("external.db", "1", root, "ABC", true, prerequisites));
    var blocked = policy.Evaluate(new ControlledAction("external.db", "1", root, "ABC", true,
        new Dictionary<string, bool> { ["manifest"] = true, ["backup"] = false }));

    Assert.Equal(SafetyLevel.Automatic, automatic.Level);
    Assert.Equal(SafetyLevel.SingleConfirmation, confirmation.Level);
    Assert.Equal(SafetyLevel.Blocked, blocked.Level);
    return Task.CompletedTask;
}

static async Task DirectoryLeasePreventsOverlap()
{
    await using var scope = TestScope.Create();
    var managerOne = new DirectoryLeaseManager(scope.ToolDataRoot);
    var managerTwo = new DirectoryLeaseManager(scope.ToolDataRoot);
    var work = Path.Combine(scope.Root, "work");
    var separate = Path.Combine(scope.Root, "separate");
    Directory.CreateDirectory(work);
    Directory.CreateDirectory(separate);

    await using var first = await managerOne.AcquireAsync([work]);
    await Assert.ThrowsAsync<InvalidOperationException>(() => managerTwo.AcquireAsync([Path.Combine(work, "child")]));
    await using var independent = await managerTwo.AcquireAsync([separate]);
    Assert.NotEqual(first.Record.LeaseId, independent.Record.LeaseId);
}

static async Task ControlledExecutionRecordsOutcome()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var attempts = new ExecutionAttemptService(caseId, history);
    var leases = new DirectoryLeaseManager(scope.ToolDataRoot);
    var policy = new SafetyPolicy([]);
    var executor = new FakeExternalExecutor(new ExternalActionResult(true, "ok", "evidence-1"));
    var coordinator = new ControlledExecutionCoordinator(caseId, policy, leases, attempts, history, executor);
    var snapshot = new ExecutionSnapshot(
        "hop.1.execute",
        "manual.upgrade",
        "1",
        scope.CaseRoot,
        new Dictionary<string, string> { ["input"] = "value" },
        "test-tool-1",
        "ABC123");
    var action = new ControlledAction("manual.upgrade", "1", scope.CaseRoot, snapshot.ComputeInputDigest(), true,
        new Dictionary<string, bool> { ["manifest"] = true });
    var decision = policy.Evaluate(action);
    var request = new ControlledExecutionRequest(
        snapshot,
        action,
        [scope.CaseRoot],
        "operator",
        Confirmation: new ActionConfirmation(decision.DecisionDigest, "operator", DateTimeOffset.UtcNow));

    var outcome = await coordinator.ExecuteAsync(request);
    Assert.True(outcome.Executed, "有效單次確認後應呼叫已注入的執行介面。 ");
    Assert.True(outcome.Result?.Succeeded == true, "替身結果應被保存。 ");
    Assert.Equal(1, executor.CallCount);
    var events = await ReadAll(history);
    Assert.SequenceEqual(
        new[] { HistoryEventTypes.ConfirmationRecorded, HistoryEventTypes.AttemptStarted, HistoryEventTypes.AttemptSucceeded },
        events.Select(entry => entry.EventType));
}

static async Task DefaultExecutorBlocksExternalAction()
{
    var executor = new BlockedExternalActionExecutor();
    var result = await executor.ExecuteAsync(new ExternalActionContext(Guid.NewGuid(), "db.restore", "1", @"K:\customer", new Dictionary<string, string>()));
    Assert.False(result.Succeeded, "預設外部執行器不得產生成功結果。 ");
    Assert.True(result.Message.Contains("不連接 DB", StringComparison.Ordinal), "阻擋原因應清楚說明安全邊界。 ");
}

static async Task SnapshotMismatchIsBlocked()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var attempts = new ExecutionAttemptService(caseId, history);
    var executor = new FakeExternalExecutor(new ExternalActionResult(true, "should-not-run"));
    var coordinator = new ControlledExecutionCoordinator(
        caseId,
        new SafetyPolicy([]),
        new DirectoryLeaseManager(scope.ToolDataRoot),
        attempts,
        history,
        executor);
    var snapshot = TestSnapshot("task.mismatch", scope.CaseRoot);
    var action = new ControlledAction("different.action", "1", scope.CaseRoot, snapshot.ComputeInputDigest(), true,
        new Dictionary<string, bool> { ["manifest"] = true });

    var outcome = await coordinator.ExecuteAsync(new ControlledExecutionRequest(snapshot, action, [scope.CaseRoot], "operator"));
    Assert.Equal(SafetyLevel.Blocked, outcome.Decision.Level);
    Assert.False(outcome.Executed);
    Assert.Equal(0, executor.CallCount);
    Assert.Equal(0, (await attempts.GetAttemptsAsync()).Count);
}

static Task SkillMapDefinesAllRoutes()
{
    var map = File.ReadAllText(ProjectPath("docs", "design", "skill-map.md"));
    var expectedSkills = new[]
    {
        "aras-innovator-upgrade",
        "aras-manage-upgrade-case",
        "aras-build-customer-package",
        "aras-prepare-ootb-hop-diff",
        "aras-prepare-adapted-package",
        "aras-manage-upgrade-rules",
        "aras-compare-core-tree",
        "aras-coordinate-upgrade-hop",
        "aras-assemble-upgrade-delivery"
    };
    foreach (var skill in expectedSkills)
        Assert.True(map.Contains($"`{skill}`", StringComparison.Ordinal), $"Skill Map 缺少 {skill}。 ");
    Assert.True(map.Contains("不互相複製責任", StringComparison.Ordinal));
    Assert.True(map.Contains("正式程式 command/action", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task MainSkillStopsAtUnavailableRoute()
{
    var content = File.ReadAllText(ProjectPath(".agents", "skills", "aras-innovator-upgrade", "SKILL.md"));
    Assert.True(content.Contains("aras-manage-upgrade-case", StringComparison.Ordinal));
    Assert.True(content.Contains("Skill Map 標示對應功能尚未建立", StringComparison.Ordinal));
    Assert.True(content.Contains("不得由主 Skill 即席模擬功能細節", StringComparison.Ordinal));
    AssertSkillFrontmatter(content, "aras-innovator-upgrade");
    AssertAgentMetadata(ProjectPath(".agents", "skills", "aras-innovator-upgrade", "agents", "openai.yaml"), "aras-innovator-upgrade");
    return Task.CompletedTask;
}

static Task CaseSkillReferencesTestedCore()
{
    var skill = File.ReadAllText(ProjectPath(".agents", "skills", "aras-manage-upgrade-case", "SKILL.md"));
    var capabilities = File.ReadAllText(ProjectPath(".agents", "skills", "aras-manage-upgrade-case", "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-manage-upgrade-case");
    AssertAgentMetadata(ProjectPath(".agents", "skills", "aras-manage-upgrade-case", "agents", "openai.yaml"), "aras-manage-upgrade-case");
    Assert.True(skill.Contains("不得手工改寫案件清單或 `history.jsonl`", StringComparison.Ordinal));
    Assert.True(skill.Contains("Package 一次性產生", StringComparison.Ordinal));
    foreach (var typeName in new[] { "CaseStore", "TaskGraph", "AppendOnlyHistoryStore", "SafetyPolicy", "DirectoryLeaseManager", "ControlledExecutionCoordinator" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"核心能力對照缺少 {typeName}。 ");
    return Task.CompletedTask;
}

static async Task CustomerPackageFlowLocksOnce()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flow = new CustomerPackageOneTimeFlow(caseId, history);
    var flowAttemptId = Guid.NewGuid();
    var request = TestPackageLockRequest(flowAttemptId, scope.CaseRoot);

    var locked = await flow.LockAsync(request, "operator");
    Assert.Equal(CustomerPackageFlowState.Locked, locked.State);
    Assert.Equal("db-backup-before-package", locked.DatabaseBackupId);
    await Assert.ThrowsAsync<InvalidOperationException>(() => flow.LockAsync(TestPackageLockRequest(Guid.NewGuid(), scope.CaseRoot), "operator"));

    var events = await ReadAll(history);
    Assert.Equal(1, events.Count(entry => entry.EventType == HistoryEventTypes.CustomerPackageFlowLocked));
}

static async Task CustomerPackageRollbackRequiresMatchingBackup()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flow = new CustomerPackageOneTimeFlow(caseId, history);
    var firstId = Guid.NewGuid();
    await flow.LockAsync(TestPackageLockRequest(firstId, scope.CaseRoot), "operator");

    await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MarkRolledBackAsync(
        firstId, "other-backup", "restore-proof", "operator"));
    var rolledBack = await flow.MarkRolledBackAsync(
        firstId, "db-backup-before-package", "restore-proof", "operator");
    Assert.Equal(CustomerPackageFlowState.RolledBack, rolledBack.State);

    var secondId = Guid.NewGuid();
    var relocked = await flow.LockAsync(TestPackageLockRequest(secondId, scope.CaseRoot), "operator");
    Assert.Equal(CustomerPackageFlowState.Locked, relocked.State);
    Assert.Equal(secondId, relocked.FlowAttemptId);
}

static async Task CustomerPackageCompletionIsPermanent()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flow = new CustomerPackageOneTimeFlow(caseId, history);
    var flowAttemptId = Guid.NewGuid();
    await flow.LockAsync(TestPackageLockRequest(flowAttemptId, scope.CaseRoot), "operator");
    var completed = await flow.CompleteAsync(flowAttemptId, "customer-package-baseline", [], "operator");

    Assert.Equal(CustomerPackageFlowState.Completed, completed.State);
    await Assert.ThrowsAsync<InvalidOperationException>(() => flow.MarkRolledBackAsync(
        flowAttemptId, "db-backup-before-package", "restore-proof", "operator"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => flow.LockAsync(
        TestPackageLockRequest(Guid.NewGuid(), scope.CaseRoot), "operator"));
}

static async Task CustomerPackageExclusionsBlockCompletion()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flow = new CustomerPackageOneTimeFlow(caseId, history);
    var flowAttemptId = Guid.NewGuid();
    await flow.LockAsync(TestPackageLockRequest(flowAttemptId, scope.CaseRoot), "operator");
    var unresolved = new PackageExportExclusion("broken-item", "ItemType", "export stopped", null, null);

    await Assert.ThrowsAsync<InvalidOperationException>(() => flow.CompleteAsync(
        flowAttemptId, "customer-package-baseline", [unresolved], "operator"));
    Assert.Equal(CustomerPackageFlowState.Locked, (await flow.GetStateAsync()).State);
}

static async Task CustomerPackageActionRequiresLockAndChecksum()
{
    await using var scope = TestScope.Create();
    var caseId = Guid.NewGuid();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flow = new CustomerPackageOneTimeFlow(caseId, history);
    var executor = new RecordingExternalExecutor(new ExternalActionResult(true, "ok", "db-evidence"));
    var gate = new CustomerPackageActionGate(flow, executor);
    var flowAttemptId = Guid.NewGuid();
    var context = new ExternalActionContext(Guid.NewGuid(), CustomerPackageActions.DeletePackageTables, "1", scope.CaseRoot,
        new Dictionary<string, string>
        {
            [CustomerPackageActions.ChecksumInput] = "ABC123",
            [CustomerPackageActions.FlowAttemptIdInput] = flowAttemptId.ToString()
        });

    var beforeLock = await gate.ExecuteAsync(context);
    Assert.False(beforeLock.Succeeded);
    await flow.LockAsync(TestPackageLockRequest(flowAttemptId, scope.CaseRoot) with
    {
        ApprovedActions = [new ApprovedPackageAction(CustomerPackageActions.DeletePackageTables, "1", "ABC123")]
    }, "operator");
    var wrongChecksum = await gate.ExecuteAsync(context with
    {
        Inputs = new Dictionary<string, string>
        {
            [CustomerPackageActions.ChecksumInput] = "WRONG",
            [CustomerPackageActions.FlowAttemptIdInput] = flowAttemptId.ToString()
        }
    });
    Assert.False(wrongChecksum.Succeeded);
    var accepted = await gate.ExecuteAsync(context);
    Assert.True(accepted.Succeeded);
    Assert.Equal(1, executor.CallCount);
}

static Task CustomerPackageSkillReferencesTestedCore()
{
    var skillRoot = ProjectPath(".agents", "skills", "aras-build-customer-package");
    var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
    var capabilities = File.ReadAllText(Path.Combine(skillRoot, "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-build-customer-package");
    AssertAgentMetadata(Path.Combine(skillRoot, "agents", "openai.yaml"), "aras-build-customer-package");
    foreach (var typeName in new[] { "CustomerPackageOneTimeFlow", "CustomerPackageActionGate", "IExternalActionExecutor" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"客戶 Package 核心能力對照缺少 {typeName}。 ");
    Assert.True(skill.Contains("不得產生、修改或自由組合 SQL", StringComparison.Ordinal));
    Assert.True(skill.Contains("不得手工改寫", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task AmlClassifiesAndTraversesNestedNodes()
{
    var document = AmlDocument.Load(ProjectPath("tests", "fixtures", "aml", "nested_relationships.xml"));
    var nodes = document.Root.DescendantsAndSelf().ToArray();

    Assert.Equal(AmlNodeKind.AmlRoot, document.Root.Kind);
    Assert.Equal(2, document.TopLevelItems.Count);
    Assert.True(nodes.Any(node => node.Kind == AmlNodeKind.ScalarProperty && node.Name == "label"));
    Assert.True(nodes.Any(node => node.Kind == AmlNodeKind.ItemProperty && node.Name == "data_source"));
    Assert.True(nodes.Any(node => node.Kind == AmlNodeKind.RelationshipsContainer));
    Assert.Equal(2, nodes.Count(node => node.Kind == AmlNodeKind.RelationshipItem));
    var deepest = nodes.Single(node => node.Kind == AmlNodeKind.Item && node.Path.EndsWith("Item[type=ItemType, name=List]", StringComparison.Ordinal));
    Assert.True(deepest.Depth >= 5, "AML 遞迴不得限制為兩層或三層。 ");
    Assert.Equal(
        "/AML/Item[type=ItemType, id=ROOT]/Relationships/Item[type=Property, name=created_by_id]/ItemProperty[name=data_source]/Item[type=ItemType, name=List]/Relationships/Item[type=Value, name=A]",
        deepest.Children.Single(node => node.Kind == AmlNodeKind.RelationshipsContainer).Children.Single().Path);
    return Task.CompletedTask;
}

static Task PackageCompareKeyUsesSpecifiedIdentity()
{
    var document = AmlDocument.Parse("""
        <AML>
          <Item type=" Part " id=" abc " where="[Part].[name] = 'ignored'" action=" edit " />
          <Item type="Part" where="  [Part].[name]   =   'A  B'  " action="get" />
        </AML>
        """);

    var byId = PackageCompareKey.Create(document.TopLevelItems[0]);
    var byWhere = PackageCompareKey.Create(document.TopLevelItems[1]);
    Assert.Equal(CompareKeyStatus.Success, byId.Status);
    Assert.Equal("PART|ABC|EDIT", byId.Key);
    Assert.Equal("PART|[Part].[name] = 'A  B'|GET", byWhere.Key);
    return Task.CompletedTask;
}

static Task PackageCompareKeyAmbiguityRequiresManualReview()
{
    var document = AmlDocument.Parse("""
        <AML>
          <Item type="Part" id="A" />
          <Item type="Part" id="DUP" action="edit" />
          <Item type="part" id="dup" action="EDIT" />
        </AML>
        """);

    var missingAction = PackageCompareKey.Create(document.TopLevelItems[0]);
    Assert.Equal(CompareKeyStatus.ManualReview, missingAction.Status);
    Assert.Equal(CompareKeyIssue.MissingAction, missingAction.Issue);
    Assert.Equal("/AML/Item[type=Part, id=A]", missingAction.AmlPath);

    var index = PackageCompareKeyIndex.Build(document.TopLevelItems);
    Assert.Equal(0, index.UniqueItems.Count);
    Assert.Equal(3, index.ManualReviews.Count);
    Assert.Equal(2, index.ManualReviews.Count(review => review.Issue == CompareKeyIssue.DuplicateOnSameSide));
    var invalidWhere = PackageCompareKey.Create(AmlDocument.Parse(
        "<AML><Item type=\"Part\" where=\"[Part].[name]='A\" action=\"get\" /></AML>").TopLevelItems.Single());
    Assert.Equal(CompareKeyIssue.InvalidWhere, invalidWhere.Issue);
    return Task.CompletedTask;
}

static Task AmlParsingIsSafeAndPreservesSubtree()
{
    var document = AmlDocument.Load(ProjectPath("tests", "fixtures", "aml", "nested_relationships.xml"));
    var xml = document.ToXml();
    Assert.True(xml.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal));
    Assert.True(xml.Contains("xml:lang=\"en\"", StringComparison.Ordinal));
    Assert.True(xml.Contains("<![CDATA[Document]]>", StringComparison.Ordinal));
    Assert.True(document.TopLevelItems[0].CloneSubtree().Descendants().Any(element => element.Name.LocalName == "Relationships"));
    Assert.Throws<AmlParseException>(() => AmlDocument.Parse("<!DOCTYPE AML [<!ENTITY x SYSTEM 'file:///secret'>]><AML>&x;</AML>"));
    return Task.CompletedTask;
}

static async Task PackageXmlPairsByRelativePathOnly()
{
    await using var scope = TestScope.Create();
    var source = Path.Combine(scope.Root, "source");
    var target = Path.Combine(scope.Root, "target");
    Directory.CreateDirectory(Path.Combine(source, "nested"));
    Directory.CreateDirectory(Path.Combine(target, "other"));
    await File.WriteAllTextAsync(Path.Combine(source, "same.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(target, "SAME.XML"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(source, "nested", "module.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(target, "other", "module.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(source, "ignored.txt"), "not xml");

    var pairs = PackageXmlPathMatcher.Match(source, target);
    Assert.Equal(3, pairs.Count);
    Assert.True(pairs.Any(pair => pair.RelativePath == "same.xml" && pair.SourcePath is not null && pair.TargetPath is not null));
    Assert.True(pairs.Any(pair => pair.RelativePath == "nested/module.xml" && pair.SourcePath is not null && pair.TargetPath is null));
    Assert.True(pairs.Any(pair => pair.RelativePath == "other/module.xml" && pair.SourcePath is null && pair.TargetPath is not null));
    Assert.False(pairs.Any(pair => pair.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)));
}

static Task AmlSemanticEqualityIgnoresPureFormatting()
{
    var left = AmlDocument.Parse("""
        <AML xmlns:x="urn:test"><Item type="Part" id="A" action="edit" x:flag="1">
          <label condition="eq">Name</label>
          <Relationships>
            <Item type="Rel" id="R1" action="add"><name>One</name></Item>
            <Item type="Rel" id="R2" action="add"><name>Two</name></Item>
          </Relationships>
        </Item></AML>
        """);
    var right = AmlDocument.Parse("""
        <AML xmlns:y="urn:test">
          <Item action="edit" id="A" y:flag="1" type="Part">
            <Relationships>
              <Item action="add" id="R2" type="Rel"><name>Two</name></Item>
              <Item id="R1" type="Rel" action="add"><name>One</name></Item>
            </Relationships>
            <label condition="eq">Name</label>
          </Item>
        </AML>
        """);

    var equal = AmlSemanticComparer.Compare(left, right);
    Assert.Equal(AmlComparisonStatus.Equal, equal.Status);
    var changed = AmlSemanticComparer.Compare(left, AmlDocument.Parse(right.ToXml().Replace(">Name<", ">Changed<", StringComparison.Ordinal)));
    Assert.Equal(AmlComparisonStatus.Different, changed.Status);
    return Task.CompletedTask;
}

static Task AmlSemanticEqualityBlocksAmbiguousScalarProperties()
{
    var left = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>A</label><label>B</label></Item></AML>");
    var right = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>A</label></Item></AML>");

    var result = AmlSemanticComparer.Compare(left, right);
    Assert.Equal(AmlComparisonStatus.ManualReview, result.Status);
    Assert.True(result.Issues.Any(issue => issue.Code == AmlComparisonIssueCode.DuplicateScalarProperty));
    Assert.True(result.Issues.Any(issue => issue.LeftPath?.Contains("ScalarProperty[name=label]", StringComparison.Ordinal) == true));
    return Task.CompletedTask;
}

static Task DefaultRule2DraftContainsValidatedSteps()
{
    var draft = DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.Parse("2026-08-03T00:00:00Z"));
    var validation = RuleSetValidator.Validate(draft);

    Assert.True(validation.IsValid, string.Join("; ", validation.Errors.Select(error => error.Message)));
    Assert.Equal(RuleSetScope.Common, draft.Scope);
    Assert.SequenceEqual(new[]
    {
        RuleStepKind.RemoveEqualScalarProperties,
        RuleStepKind.RemoveNamedProperties,
        RuleStepKind.PreferGreaterSourceNumber,
        RuleStepKind.PreferSourceUnderTargetPath,
        RuleStepKind.KeepTargetForValuePairs,
        RuleStepKind.KeepTargetNamedProperties,
        RuleStepKind.DefaultPreferSourceUnlessSourceEmpty
    }, draft.Steps.OrderBy(step => step.Order).Select(step => step.Kind));
    Assert.True(draft.Steps.Single(step => step.Kind == RuleStepKind.RemoveNamedProperties).PropertyNames.Contains("sort_order"));
    Assert.True(draft.Steps.Single(step => step.Kind == RuleStepKind.KeepTargetForValuePairs).ValuePairs.Any(pair =>
        pair.PropertyName == "font_color" && pair.Source.Kind == RuleValueConditionKind.Exact && pair.Source.Value == "#000000" &&
        pair.Target.Kind == RuleValueConditionKind.Exact && pair.Target.Value == "#333333"));
    Assert.True(draft.Steps.Single(step => step.Kind == RuleStepKind.KeepTargetForValuePairs).ValuePairs.Any(pair =>
        pair.PropertyName == "label" && pair.Source.Kind == RuleValueConditionKind.Empty && pair.Target.Kind == RuleValueConditionKind.NonEmpty));
    return Task.CompletedTask;
}

static Task RuleDraftValidationRejectsAmbiguity()
{
    var valid = DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.UtcNow);
    var invalid = valid with
    {
        Steps =
        [
            valid.Steps[0],
            valid.Steps[1] with { Order = valid.Steps[0].Order },
            new RuleStepDefinition("unsafe", 9, (RuleStepKind)999, [], [], null)
        ]
    };

    var result = RuleSetValidator.Validate(invalid);
    Assert.False(result.IsValid);
    Assert.True(result.Errors.Any(error => error.Code == RuleValidationErrorCode.DuplicateStepOrder));
    Assert.True(result.Errors.Any(error => error.Code == RuleValidationErrorCode.UnsupportedStep));
    return Task.CompletedTask;
}

static async Task AiCannotCreateOrPublishRules()
{
    await using var scope = TestScope.Create();
    var store = new RuleSetStore(Path.Combine(scope.ToolDataRoot, "rules"));
    var draft = DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.UtcNow);

    await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDraftAsync(draft, new RuleActor("codex", RuleActorKind.Ai)));
    await store.SaveDraftAsync(draft, new RuleActor("operator", RuleActorKind.Human));
    await Assert.ThrowsAsync<InvalidOperationException>(() => store.PublishAsync(
        draft.DraftId,
        new RulePublicationApproval(new RuleActor("codex", RuleActorKind.Ai), "approval-1")));
}

static async Task PublishedRuleVersionsAreImmutable()
{
    await using var scope = TestScope.Create();
    var store = new RuleSetStore(Path.Combine(scope.ToolDataRoot, "rules"), () => DateTimeOffset.Parse("2026-08-03T01:00:00Z"));
    var actor = new RuleActor("operator", RuleActorKind.Human);
    var firstDraft = DefaultUpgradeRuleSets.CreateRule2Draft(actor.Name, DateTimeOffset.UtcNow);
    await store.SaveDraftAsync(firstDraft, actor);
    var first = await store.PublishAsync(firstDraft.DraftId, new RulePublicationApproval(actor, "approval-v1"));
    var secondDraft = firstDraft with
    {
        DraftId = Guid.NewGuid(),
        DisplayName = "Rule 2 common v2",
        Steps = firstDraft.Steps.Select(step => step.StepId == "remove-named-properties"
            ? step with { PropertyNames = [.. step.PropertyNames, "new_property"] }
            : step).ToArray()
    };
    await store.SaveDraftAsync(secondDraft, actor);
    var second = await store.PublishAsync(secondDraft.DraftId, new RulePublicationApproval(actor, "approval-v2"));

    Assert.Equal(1, first.Version);
    Assert.Equal(2, second.Version);
    Assert.NotEqual(first.ContentChecksum, second.ContentChecksum);
    Assert.Equal(firstDraft.DisplayName, (await store.GetPublishedAsync(first.RuleSetId, 1)).DisplayName);
    Assert.Equal(2, (await store.ListPublishedAsync()).Count);
}

static async Task RuleStoreRejectsIdentityMismatchAndTampering()
{
    await using var scope = TestScope.Create();
    var root = Path.Combine(scope.ToolDataRoot, "rules");
    var store = new RuleSetStore(root);
    var draft = DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow);
    await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveDraftAsync(draft, new RuleActor("other", RuleActorKind.Human)));
    await store.SaveDraftAsync(draft, new RuleActor("operator", RuleActorKind.Human));
    var published = await store.PublishAsync(draft.DraftId,
        new RulePublicationApproval(new RuleActor("operator", RuleActorKind.Human), "approval"));
    var path = Path.Combine(root, "published", published.RuleSetId.ToString("N"), "00000001.json");
    var content = await File.ReadAllTextAsync(path);
    await File.WriteAllTextAsync(path, content.Replace(published.ContentChecksum, new string('0', 64), StringComparison.Ordinal));

    await Assert.ThrowsAsync<InvalidDataException>(() => store.GetPublishedAsync(published.RuleSetId, 1));
}

static Task VersionExceptionOverridesCommonStep()
{
    var commonDraft = DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.UtcNow);
    var common = Published(commonDraft, 3);
    var replacement = commonDraft.Steps.Single(step => step.StepId == "default-prefer-source") with
    {
        Kind = RuleStepKind.KeepTargetNamedProperties,
        PropertyNames = ["name"]
    };
    var exception = Published(new RuleSetDraft(
        Guid.NewGuid(), Guid.NewGuid(), "11SP5 to R38 exception", RuleSetKind.Rule2, RuleSetScope.VersionException,
        "11SP5", "R38", [replacement], DateTimeOffset.UtcNow, "operator"), 1);

    var result = RuleSetResolver.Resolve([common, exception], RuleSetKind.Rule2, "11SP5", "R38");
    Assert.Equal(RuleResolutionStatus.Resolved, result.Status);
    Assert.Equal(RuleStepKind.KeepTargetNamedProperties, result.Steps.Single(step => step.StepId == "default-prefer-source").Kind);
    Assert.SequenceEqual(new[] { 3, 1 }, result.PinnedVersions.Select(reference => reference.Version));
    return Task.CompletedTask;
}

static Task ConflictingVersionExceptionsAreBlocked()
{
    var commonDraft = DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.UtcNow);
    var common = Published(commonDraft, 1);
    var baseStep = commonDraft.Steps.Single(step => step.StepId == "default-prefer-source");
    PublishedRuleSet Exception(string propertyName) => Published(new RuleSetDraft(
        Guid.NewGuid(), Guid.NewGuid(), $"exception-{propertyName}", RuleSetKind.Rule2, RuleSetScope.VersionException,
        "11SP5", "R38", [baseStep with { Kind = RuleStepKind.KeepTargetNamedProperties, PropertyNames = [propertyName] }],
        DateTimeOffset.UtcNow, "operator"), 1);

    var result = RuleSetResolver.Resolve([common, Exception("name"), Exception("label")], RuleSetKind.Rule2, "11SP5", "R38");
    Assert.Equal(RuleResolutionStatus.Blocked, result.Status);
    Assert.True(result.Issues.Any(issue => issue.Code == RuleResolutionIssueCode.ConflictingVersionExceptions));
    Assert.False(result.Steps.Any(step => step.StepId == "default-prefer-source"));
    Assert.Equal(common.Steps.Count - 1, result.Steps.Count);
    return Task.CompletedTask;
}

static PublishedRuleSet Published(RuleSetDraft draft, int version) => PublishedRuleSet.Create(
    draft, version, DateTimeOffset.Parse("2026-08-03T01:00:00Z"), "operator", $"approval-v{version}");

static Task RuleManagementSkillReferencesTestedCore()
{
    var skillRoot = ProjectPath(".agents", "skills", "aras-manage-upgrade-rules");
    var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
    var capabilities = File.ReadAllText(Path.Combine(skillRoot, "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-manage-upgrade-rules");
    AssertAgentMetadata(Path.Combine(skillRoot, "agents", "openai.yaml"), "aras-manage-upgrade-rules");
    foreach (var typeName in new[] { "RuleSetValidator", "RuleSetStore", "RuleSetResolver", "DefaultUpgradeRuleSets" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"規則管理核心能力對照缺少 {typeName}。 ");
    Assert.True(skill.Contains("AI 不得建立、修改、發布或啟用規則", StringComparison.Ordinal));
    Assert.True(skill.Contains("不得手工改寫已發布版本", StringComparison.Ordinal));
    Assert.True(skill.Contains("不執行 Rule 1 或 Rule 2", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task Rule1ProducesTwoSidedItemDiff()
{
    var source = AmlDocument.Parse("""
        <?xml version="1.0" encoding="utf-8"?>
        <AML>
          <Item type="Part" id="SOURCE" action="add"><name>source only</name></Item>
          <Item type="Part" id="SAME" action="edit"><name>same</name></Item>
          <Item type="Part" id="CHANGED" action="edit"><name>before</name></Item>
        </AML>
        """);
    var target = AmlDocument.Parse("""
        <?xml version="1.0" encoding="utf-8"?>
        <AML>
          <Item type="Part" id="TARGET" action="add"><name>target only</name></Item>
          <Item type="Part" id="SAME" action="edit"><name>same</name></Item>
          <Item type="Part" id="CHANGED" action="edit"><name>after</name></Item>
        </AML>
        """);

    var result = Rule1DiffEngine.Compare(source, target);

    Assert.SequenceEqual(new[] { "CHANGED" }, result.SourceDiff.TopLevelItems.Select(item => item.ItemId));
    Assert.SequenceEqual(new[] { "TARGET", "CHANGED" }, result.TargetDiff.TopLevelItems.Select(item => item.ItemId));
    Assert.Equal(1, result.Summary.SourceOnlyDeleted);
    Assert.Equal(1, result.Summary.TargetOnlyRetained);
    Assert.Equal(1, result.Summary.EqualPairsDeleted);
    Assert.Equal(1, result.Summary.DifferentPairsRetained);
    Assert.Equal(0, result.ManualReviews.Count);
    Assert.True(result.SourceDiff.ToXml().StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task Rule1RetainsAmbiguousPairing()
{
    var source = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"DUP\" action=\"edit\"><name>source</name></Item></AML>");
    var target = AmlDocument.Parse("""
        <AML>
          <Item type="Part" id="DUP" action="edit"><name>target one</name></Item>
          <Item type="part" id="dup" action="EDIT"><name>target two</name></Item>
        </AML>
        """);

    var result = Rule1DiffEngine.Compare(source, target);

    Assert.Equal(1, result.SourceDiff.TopLevelItems.Count);
    Assert.Equal(2, result.TargetDiff.TopLevelItems.Count);
    Assert.Equal(0, result.Summary.SourceOnlyDeleted);
    Assert.Equal(0, result.Summary.TargetOnlyRetained);
    Assert.Equal(2, result.ManualReviews.Count);
    return Task.CompletedTask;
}

static Task Rule1RecordsSemanticManualReview()
{
    var source = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>A</label><label>B</label></Item></AML>");
    var target = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>A</label></Item></AML>");

    var result = Rule1DiffEngine.Compare(source, target);

    Assert.Equal(1, result.SourceDiff.TopLevelItems.Count);
    Assert.Equal(1, result.TargetDiff.TopLevelItems.Count);
    Assert.True(result.ManualReviews.Any(review => review.Code == "DuplicateScalarProperty"));
    Assert.Equal(0, result.Summary.DifferentPairsRetained);
    return Task.CompletedTask;
}

static async Task OotbHopDiffBuilderPreservesInputs()
{
    await using var scope = TestScope.Create();
    var sourceRoot = Path.Combine(scope.Root, "ootb-source");
    var targetRoot = Path.Combine(scope.Root, "ootb-target");
    var outputRoot = Path.Combine(scope.Root, "attempt-output");
    Directory.CreateDirectory(Path.Combine(sourceRoot, "nested"));
    Directory.CreateDirectory(Path.Combine(targetRoot, "other"));
    const string sourceXml = "<AML><Item type=\"Part\" id=\"SOURCE\" action=\"add\"/><Item type=\"Part\" id=\"CHANGED\" action=\"edit\"><name>before</name></Item></AML>";
    const string targetXml = "<AML><Item type=\"Part\" id=\"TARGET\" action=\"add\"/><Item type=\"Part\" id=\"CHANGED\" action=\"edit\"><name>after</name></Item></AML>";
    await File.WriteAllTextAsync(Path.Combine(sourceRoot, "paired.xml"), sourceXml);
    await File.WriteAllTextAsync(Path.Combine(targetRoot, "paired.xml"), targetXml);
    await File.WriteAllTextAsync(Path.Combine(sourceRoot, "nested", "only.xml"), "<AML><Item type=\"Part\" id=\"S\" action=\"add\"/></AML>");
    await File.WriteAllTextAsync(Path.Combine(targetRoot, "other", "only.xml"), "<AML><Item type=\"Part\" id=\"T\" action=\"add\"/></AML>");
    await File.WriteAllTextAsync(Path.Combine(sourceRoot, "ignored.txt"), "unchanged");
    var rule = Published(DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow), 2);

    var result = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(
        Guid.NewGuid(), "12SP9", "12SP18", sourceRoot, targetRoot, outputRoot, ResolvedRule1(rule, "12SP9", "12SP18"),
        DateTimeOffset.Parse("2026-08-03T02:00:00Z")));

    Assert.Equal(OotbHopDiffBuildStatus.ReadyToPackage, result.Status);
    Assert.Equal(sourceXml, await File.ReadAllTextAsync(Path.Combine(sourceRoot, "paired.xml")));
    Assert.Equal(targetXml, await File.ReadAllTextAsync(Path.Combine(targetRoot, "paired.xml")));
    Assert.False(File.Exists(Path.Combine(result.SourceDiffRoot, "ignored.txt")));
    Assert.SequenceEqual(new[] { "CHANGED" }, AmlDocument.Load(Path.Combine(result.SourceDiffRoot, "paired.xml")).TopLevelItems.Select(item => item.ItemId));
    Assert.SequenceEqual(new[] { "TARGET", "CHANGED" }, AmlDocument.Load(Path.Combine(result.TargetDiffRoot, "paired.xml")).TopLevelItems.Select(item => item.ItemId));
    Assert.Equal(0, AmlDocument.Load(Path.Combine(result.SourceDiffRoot, "nested", "only.xml")).TopLevelItems.Count);
    Assert.Equal("T", AmlDocument.Load(Path.Combine(result.TargetDiffRoot, "other", "only.xml")).TopLevelItems.Single().ItemId);
    Assert.Equal(3, result.Summary.XmlFilesProcessed);
    Assert.Equal(0, result.Errors.Count);
}

static async Task OotbHopDiffBuilderIsolatesXmlErrors()
{
    await using var scope = TestScope.Create();
    var sourceRoot = Path.Combine(scope.Root, "source");
    var targetRoot = Path.Combine(scope.Root, "target");
    Directory.CreateDirectory(sourceRoot);
    Directory.CreateDirectory(targetRoot);
    await File.WriteAllTextAsync(Path.Combine(sourceRoot, "good.xml"), "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>old</name></Item></AML>");
    await File.WriteAllTextAsync(Path.Combine(targetRoot, "good.xml"), "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>new</name></Item></AML>");
    await File.WriteAllTextAsync(Path.Combine(sourceRoot, "broken.xml"), "<AML><Item>");
    await File.WriteAllTextAsync(Path.Combine(targetRoot, "broken.xml"), "<AML />");
    var result = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(
        Guid.NewGuid(), "12SP9", "12SP18", sourceRoot, targetRoot, Path.Combine(scope.Root, "output"),
        ResolvedRule1(Published(DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow), 1), "12SP9", "12SP18"), DateTimeOffset.UtcNow));

    Assert.Equal(OotbHopDiffBuildStatus.Blocked, result.Status);
    Assert.Equal(1, result.Errors.Count);
    Assert.Equal("broken.xml", result.Errors[0].RelativePath);
    Assert.Equal(1, result.Summary.XmlFilesProcessed);
    Assert.True(File.Exists(Path.Combine(result.SourceDiffRoot, "good.xml")));
    Assert.False(File.Exists(Path.Combine(result.SourceDiffRoot, "broken.xml")));
    Assert.False(File.Exists(Path.Combine(result.TargetDiffRoot, "broken.xml")));
}

static async Task OotbHopDiffArtifactPackagesAndVerifies()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archive = Path.Combine(scope.Root, "artifacts", "12SP9-to-12SP18.zip");

    var artifact = await OotbHopDiffPackager.PackageAsync(
        build, archive, DateTimeOffset.Parse("2026-08-03T03:00:00Z"));

    Assert.Equal(OotbHopDiffArtifactState.Completed, artifact.State);
    Assert.True(File.Exists(archive));
    Assert.Equal(64, artifact.ArchiveChecksum!.Length);
    using (var zip = System.IO.Compression.ZipFile.OpenRead(archive))
    {
        Assert.True(zip.Entries.Any(entry => entry.FullName == "SourceDiff/paired.xml"));
        Assert.True(zip.Entries.Any(entry => entry.FullName == "TargetDiff/paired.xml"));
        Assert.True(zip.Entries.Any(entry => entry.FullName == "completion-manifest.json"));
        Assert.True(zip.Entries.Any(entry => entry.FullName == "processing-summary.json"));
    }
    var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(archive, new OotbHopDiffReuseRequirement(
        build.SourceVersion, build.TargetVersion, build.RuleSets, build.EffectiveRuleChecksum, artifact.ArchiveChecksum));
    Assert.True(verification.IsReusable, string.Join("; ", verification.Issues));
}

static async Task BlockedOotbHopDiffCannotBePackaged()
{
    await using var scope = TestScope.Create();
    var source = Path.Combine(scope.Root, "blocked-source");
    var target = Path.Combine(scope.Root, "blocked-target");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(target);
    await File.WriteAllTextAsync(Path.Combine(source, "manual.xml"), "<AML><Item type=\"Part\" id=\"A\" /></AML>");
    await File.WriteAllTextAsync(Path.Combine(target, "manual.xml"), "<AML><Item type=\"Part\" id=\"A\" /></AML>");
    var build = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(
        Guid.NewGuid(), "12SP9", "12SP18", source, target, Path.Combine(scope.Root, "blocked-output"),
        ResolvedRule1(Published(DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow), 1), "12SP9", "12SP18"), DateTimeOffset.UtcNow));
    var archive = Path.Combine(scope.Root, "blocked.zip");

    var artifact = await OotbHopDiffPackager.PackageAsync(build, archive, DateTimeOffset.UtcNow);

    Assert.Equal(OotbHopDiffArtifactState.Incomplete, artifact.State);
    Assert.True(File.Exists(artifact.ProcessingSummaryPath));
    Assert.False(File.Exists(archive));
    Assert.True(artifact.Manifest is null);
    Assert.True(artifact.ArchiveChecksum is null);
}

static async Task OotbHopDiffVerifierRequiresBothSides()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "artifact.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Update))
    {
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("TargetDiff/", StringComparison.Ordinal)).ToArray())
            entry.Delete();
    }
    var tamperedChecksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(archivePath)));

    var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(archivePath, new OotbHopDiffReuseRequirement(
        build.SourceVersion, build.TargetVersion, build.RuleSets, build.EffectiveRuleChecksum, tamperedChecksum));

    Assert.False(verification.IsReusable);
    Assert.True(verification.Issues.Any(issue => issue.Contains("SourceDiff", StringComparison.Ordinal) || issue.Contains("TargetDiff", StringComparison.Ordinal)));
}

static Task OotbHopDiffSkillReferencesTestedCore()
{
    var skillRoot = ProjectPath(".agents", "skills", "aras-prepare-ootb-hop-diff");
    var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
    var capabilities = File.ReadAllText(Path.Combine(skillRoot, "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-prepare-ootb-hop-diff");
    AssertAgentMetadata(Path.Combine(skillRoot, "agents", "openai.yaml"), "aras-prepare-ootb-hop-diff");
    foreach (var typeName in new[] { "Rule1DiffEngine", "OotbHopDiffBuilder", "OotbHopDiffPackager", "OotbHopDiffArtifactVerifier" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"OOTB 跳點差異核心能力對照缺少 {typeName}。 ");
    Assert.True(skill.Contains("不得修改來源或目標 OOTB", StringComparison.Ordinal));
    Assert.True(skill.Contains("不得手工建立或改寫完成標記", StringComparison.Ordinal));
    Assert.True(skill.Contains("Rule 2", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static async Task OotbHopDiffPinsResolvedRuleSnapshot()
{
    await using var scope = TestScope.Create();
    var source = Path.Combine(scope.Root, "pin-source");
    var target = Path.Combine(scope.Root, "pin-target");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(target);
    await File.WriteAllTextAsync(Path.Combine(source, "same.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(target, "same.xml"), "<AML />");
    var commonDraft = DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow);
    var common = Published(commonDraft, 3);
    var exception = Published(new RuleSetDraft(
        Guid.NewGuid(), Guid.NewGuid(), "12SP9 to 12SP18 exception", RuleSetKind.Rule1, RuleSetScope.VersionException,
        "12SP9", "12SP18", commonDraft.Steps, DateTimeOffset.UtcNow, "operator"), 1);
    var resolution = RuleSetResolver.Resolve([common, exception], RuleSetKind.Rule1, "12SP9", "12SP18");

    var build = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(
        Guid.NewGuid(), "12SP9", "12SP18", source, target, Path.Combine(scope.Root, "pin-output"), resolution, DateTimeOffset.UtcNow));

    Assert.Equal(2, build.RuleSets.Count);
    Assert.Equal(resolution.EffectiveChecksum, build.EffectiveRuleChecksum);
    Assert.SequenceEqual(new[] { 3, 1 }, build.RuleSets.Select(rule => rule.Version));
}

static async Task OotbHopDiffVerifierRequiresSummary()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "without-summary.zip");
    await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Update))
        archive.GetEntry("processing-summary.json")!.Delete();
    var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(archivePath)));

    var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(archivePath, new OotbHopDiffReuseRequirement(
        build.SourceVersion, build.TargetVersion, build.RuleSets, build.EffectiveRuleChecksum, checksum));

    Assert.False(verification.IsReusable);
    Assert.True(verification.Issues.Any(issue => issue.Contains("processing-summary", StringComparison.Ordinal)));
}

static async Task OotbHopDiffVerifierRejectsUnsafeEntries()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "unsafe-entry.zip");
    await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Update))
    {
        var entry = archive.CreateEntry("../escape.xml");
        await using var writer = new StreamWriter(entry.Open());
        await writer.WriteAsync("<AML />");
    }
    var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(archivePath)));

    var verification = await OotbHopDiffArtifactVerifier.VerifyAsync(archivePath, new OotbHopDiffReuseRequirement(
        build.SourceVersion, build.TargetVersion, build.RuleSets, build.EffectiveRuleChecksum, checksum));

    Assert.False(verification.IsReusable);
    Assert.True(verification.Issues.Any(issue => issue.Contains("安全相對路徑", StringComparison.Ordinal)));
}

static async Task<OotbHopDiffBuildResult> CreateReadyHopDiffAsync(string root)
{
    var source = Path.Combine(root, "ready-source");
    var target = Path.Combine(root, "ready-target");
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(target);
    await File.WriteAllTextAsync(Path.Combine(source, "paired.xml"),
        "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>old</name></Item></AML>");
    await File.WriteAllTextAsync(Path.Combine(target, "paired.xml"),
        "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>new</name></Item></AML>");
    return await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(
        Guid.NewGuid(), "12SP9", "12SP18", source, target, Path.Combine(root, "ready-output"),
        ResolvedRule1(Published(DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow), 4), "12SP9", "12SP18"),
        DateTimeOffset.Parse("2026-08-03T02:00:00Z")),
        () => DateTimeOffset.Parse("2026-08-03T02:30:00Z"));
}

static RuleSetResolutionResult ResolvedRule1(PublishedRuleSet rule, string sourceVersion, string targetVersion) =>
    RuleSetResolver.Resolve([rule], RuleSetKind.Rule1, sourceVersion, targetVersion);

static Task Rule2AppliesSevenScalarSteps()
{
    var source = AmlDocument.Parse("""
        <AML><Item type="Part" id="A" action="edit" keyed_name="Customer"><sort_order>1</sort_order><stored_length>20</stored_length><label>Customer</label><description>Customer text</description></Item></AML>
        """);
    var target = AmlDocument.Parse("""
        <AML><Item type="Part" id="A" action="edit" keyed_name="OOTB"><sort_order>2</sort_order><stored_length>10</stored_length><label>OOTB</label><description>OOTB text</description></Item></AML>
        """);
    var result = Rule2AdaptationEngine.Apply(source, target, ResolvedRule2(), @"OOTB_R38\PLM\Import\part.xml");

    var targetItem = result.TargetWorkCopy.TopLevelItems.Single();
    Assert.Equal("OOTB", targetItem.Attributes.Single(attribute => attribute.Key.LocalName == "keyed_name").Value);
    Assert.False(targetItem.Children.Any(child => child.Name == "sort_order"));
    Assert.Equal("20", targetItem.Children.Single(child => child.Name == "stored_length").ScalarValue);
    Assert.Equal("Customer", targetItem.Children.Single(child => child.Name == "label").ScalarValue);
    Assert.Equal("Customer text", targetItem.Children.Single(child => child.Name == "description").ScalarValue);
    Assert.Equal(Rule2AdaptationStatus.Ready, result.Status);
    return Task.CompletedTask;
}

static Task Rule2RecursesAndCopiesFederatedProperty()
{
    var source = AmlDocument.Parse("""
        <AML><Item type="ItemType" id="IT" action="edit"><Relationships><Item type="Property" id="P1" action="add"><name>remote_code</name><data_type>federated</data_type></Item></Relationships></Item></AML>
        """);
    var target = AmlDocument.Parse("""
        <AML><Item type="ItemType" id="IT" action="edit"><Relationships /></Item></AML>
        """);
    var result = Rule2AdaptationEngine.Apply(source, target, ResolvedRule2(), "Import/itemtype.xml");

    Assert.False(result.SourceWorkCopy.Root.DescendantsAndSelf().Any(node => node.ItemId == "P1"));
    var copied = result.TargetWorkCopy.Root.DescendantsAndSelf().Single(node => node.ItemId == "P1");
    Assert.Equal("text", copied.Children.Single(child => child.Name == "data_type").ScalarValue);
    Assert.Equal("1", copied.Children.Single(child => child.Name == "is_federated").ScalarValue);
    Assert.Equal("1", copied.Children.Single(child => child.Name == "is_discoverable").ScalarValue);
    Assert.Equal(1, result.Summary.FederatedPropertiesCopied);
    return Task.CompletedTask;
}

static Task Rule2AmbiguousScalarRequiresManualReview()
{
    var source = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>one</label><label>two</label><description>source</description></Item></AML>");
    var target = AmlDocument.Parse("<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>target</label><description>target</description></Item></AML>");
    var result = Rule2AdaptationEngine.Apply(source, target, ResolvedRule2(), "part.xml");

    Assert.Equal(Rule2AdaptationStatus.Blocked, result.Status);
    Assert.True(result.ManualReviews.Any(review => review.Code == "AmbiguousScalarProperty"));
    Assert.Equal("source", result.TargetWorkCopy.TopLevelItems.Single().Children.Single(child => child.Name == "description").ScalarValue);
    return Task.CompletedTask;
}

static RuleSetResolutionResult ResolvedRule2()
{
    var rule = Published(DefaultUpgradeRuleSets.CreateRule2Draft("operator", DateTimeOffset.UtcNow), 1);
    return RuleSetResolver.Resolve([rule], RuleSetKind.Rule2, "12SP9", "12SP18");
}

static async Task AdaptedPackagePreparesAfterVerificationAndBackup()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    var baseline = Path.Combine(scope.Root, "baseline");
    var solutions = Path.Combine(scope.Root, "Support", "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    await File.WriteAllTextAsync(Path.Combine(baseline, "paired.xml"),
        "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>customer</name></Item></AML>");
    await File.WriteAllTextAsync(Path.Combine(baseline, "source.bin"), "source-binary");
    await File.WriteAllTextAsync(Path.Combine(solutions, "old.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(solutions, "keep.bin"), "target-binary");
    var request = new AdaptedPackageRequest(Guid.NewGuid(), "12SP9", "12SP18", baseline, solutions,
        Path.Combine(scope.Root, "backups"), Path.Combine(scope.Root, "attempt"), archivePath,
        new OotbHopDiffReuseRequirement(build.SourceVersion, build.TargetVersion, build.RuleSets,
            build.EffectiveRuleChecksum, artifact.ArchiveChecksum!), ResolvedRule2(), DateTimeOffset.UtcNow);

    var result = await AdaptedPackageBuilder.BuildAsync(request);

    Assert.Equal(AdaptedPackageStatus.ReadyForFinalization, result.Status);
    Assert.True(Directory.Exists(result.SolutionsBackupRoot));
    Assert.True(File.Exists(Path.Combine(result.SolutionsBackupRoot, "old.xml")));
    Assert.False(File.Exists(Path.Combine(solutions, "old.xml")));
    Assert.Equal("target-binary", await File.ReadAllTextAsync(Path.Combine(solutions, "keep.bin")));
    Assert.False(File.Exists(Path.Combine(result.SourceWorkRoot, "source.bin")));
    Assert.True(File.Exists(Path.Combine(result.SourceWorkRoot, "paired.xml")));
    Assert.True(File.Exists(Path.Combine(solutions, "paired.xml")));
    var completion = await AdaptedPackageFinalizer.FinalizeAsync(result, Path.Combine(scope.Root, "completed"), DateTimeOffset.UtcNow);
    Assert.Equal(AdaptedPackageStatus.Completed, completion.Status);
    Assert.True(File.Exists(completion.CompletionManifestPath));
}

static async Task AdaptedPackageBackupFailurePreservesSolutions()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    var baseline = Path.Combine(scope.Root, "baseline");
    var solutions = Path.Combine(scope.Root, "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    await File.WriteAllTextAsync(Path.Combine(baseline, "paired.xml"), "<AML />");
    var originalPath = Path.Combine(solutions, "original.xml");
    await File.WriteAllTextAsync(originalPath, "<AML />");
    var invalidBackupRoot = Path.Combine(scope.Root, "backup-file");
    await File.WriteAllTextAsync(invalidBackupRoot, "not-a-directory");
    var request = new AdaptedPackageRequest(Guid.NewGuid(), "12SP9", "12SP18", baseline, solutions,
        invalidBackupRoot, Path.Combine(scope.Root, "attempt"), archivePath,
        new OotbHopDiffReuseRequirement(build.SourceVersion, build.TargetVersion, build.RuleSets,
            build.EffectiveRuleChecksum, artifact.ArchiveChecksum!), ResolvedRule2(), DateTimeOffset.UtcNow);

    await Assert.ThrowsAsync<IOException>(() => AdaptedPackageBuilder.BuildAsync(request));
    Assert.True(File.Exists(originalPath));
    Assert.False(Directory.Exists(request.AttemptRoot));
}

static Task AdaptedPackageSkillReferencesTestedCore()
{
    var skillRoot = ProjectPath(".agents", "skills", "aras-prepare-adapted-package");
    var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
    var capabilities = File.ReadAllText(Path.Combine(skillRoot, "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-prepare-adapted-package");
    AssertAgentMetadata(Path.Combine(skillRoot, "agents", "openai.yaml"), "aras-prepare-adapted-package");
    foreach (var typeName in new[] { "Rule2AdaptationEngine", "AdaptedPackageBuilder", "AdaptedPackageFinalizer", "OotbHopDiffArtifactVerifier" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"正式適配 Package 核心能力對照缺少 {typeName}。 ");
    Assert.True(skill.Contains("備份失敗時不得寫入工作副本", StringComparison.Ordinal));
    Assert.True(skill.Contains("不得把 Item Property 當 Scalar", StringComparison.Ordinal));
    Assert.True(skill.Contains("不連接正式 DB", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static async Task PackageIntegrationRejectsHopIdentityMismatch()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    var baseline = Path.Combine(scope.Root, "baseline");
    var solutions = Path.Combine(scope.Root, "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    await File.WriteAllTextAsync(Path.Combine(baseline, "paired.xml"),
        "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>customer</name></Item></AML>");
    var original = Path.Combine(solutions, "original.xml");
    await File.WriteAllTextAsync(original, "<AML />");
    var request = new AdaptedPackageRequest(Guid.NewGuid(), "11SP5", "R38", baseline, solutions,
        Path.Combine(scope.Root, "backups"), Path.Combine(scope.Root, "attempt"), archivePath,
        new OotbHopDiffReuseRequirement(build.SourceVersion, build.TargetVersion, build.RuleSets,
            build.EffectiveRuleChecksum, artifact.ArchiveChecksum!), ResolvedRule2(), DateTimeOffset.UtcNow);

    await Assert.ThrowsAsync<InvalidOperationException>(() => AdaptedPackageBuilder.BuildAsync(request));
    Assert.True(File.Exists(original));
    Assert.False(Directory.Exists(request.BackupRoot));
    Assert.False(Directory.Exists(request.AttemptRoot));
}

static async Task PackageIntegrationCompletesEndToEnd()
{
    await using var scope = TestScope.Create();
    var history = new AppendOnlyHistoryStore(scope.ToolDataRoot);
    var flowAttemptId = Guid.NewGuid();
    var flow = new CustomerPackageOneTimeFlow(Guid.NewGuid(), history);
    await flow.LockAsync(TestPackageLockRequest(flowAttemptId, Path.Combine(scope.Root, "rehearsal-db")), "operator");
    var flowResult = await flow.CompleteAsync(flowAttemptId, "baseline-evidence", [], "operator");
    Assert.Equal(CustomerPackageFlowState.Completed, flowResult.State);

    var ootbSource = Path.Combine(scope.Root, "ootb-source");
    var ootbTarget = Path.Combine(scope.Root, "ootb-target");
    Directory.CreateDirectory(ootbSource);
    Directory.CreateDirectory(ootbTarget);
    const string sourceOotb = "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>old</name><label>Old</label></Item></AML>";
    const string targetOotb = "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>new</name><label>New</label></Item></AML>";
    await File.WriteAllTextAsync(Path.Combine(ootbSource, "nested.xml"), sourceOotb);
    await File.WriteAllTextAsync(Path.Combine(ootbTarget, "nested.xml"), targetOotb);
    var rule1 = Published(DefaultUpgradeRuleSets.CreateRule1Draft("operator", DateTimeOffset.UtcNow), 3);
    var rule1Resolution = ResolvedRule1(rule1, "12SP9", "12SP18");
    var diff = await OotbHopDiffBuilder.BuildAsync(new OotbHopDiffRequest(Guid.NewGuid(), "12SP9", "12SP18",
        ootbSource, ootbTarget, Path.Combine(scope.Root, "rule1-output"), rule1Resolution, DateTimeOffset.UtcNow));
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(diff, archivePath, DateTimeOffset.UtcNow);

    var baseline = Path.Combine(scope.Root, "customer-baseline");
    var solutions = Path.Combine(scope.Root, "Support", "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    const string customerXml = "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><name>new</name><label>Customer</label></Item></AML>";
    await File.WriteAllTextAsync(Path.Combine(baseline, "nested.xml"), customerXml);
    await File.WriteAllTextAsync(Path.Combine(baseline, "customer.bin"), "customer-binary");
    await File.WriteAllTextAsync(Path.Combine(solutions, "original.xml"), "<AML />");
    await File.WriteAllTextAsync(Path.Combine(solutions, "upgrade.bin"), "upgrade-binary");
    var rule2 = ResolvedRule2();
    var adapted = await AdaptedPackageBuilder.BuildAsync(new AdaptedPackageRequest(Guid.NewGuid(), "12SP9", "12SP18",
        baseline, solutions, Path.Combine(scope.Root, "backups"), Path.Combine(scope.Root, "rule2-attempt"), archivePath,
        new OotbHopDiffReuseRequirement("12SP9", "12SP18", diff.RuleSets, diff.EffectiveRuleChecksum,
            artifact.ArchiveChecksum!), rule2, DateTimeOffset.UtcNow));
    var completed = await AdaptedPackageFinalizer.FinalizeAsync(adapted, Path.Combine(scope.Root, "completion"), DateTimeOffset.UtcNow);

    Assert.Equal(AdaptedPackageStatus.Completed, completed.Status);
    Assert.Equal(customerXml, await File.ReadAllTextAsync(Path.Combine(baseline, "nested.xml")));
    Assert.Equal(sourceOotb, await File.ReadAllTextAsync(Path.Combine(ootbSource, "nested.xml")));
    Assert.Equal(targetOotb, await File.ReadAllTextAsync(Path.Combine(ootbTarget, "nested.xml")));
    Assert.Equal("upgrade-binary", await File.ReadAllTextAsync(Path.Combine(solutions, "upgrade.bin")));
    Assert.False(File.Exists(Path.Combine(completed.SourceWorkRoot, "customer.bin")));
    Assert.SequenceEqual(rule2.PinnedVersions, completed.Rule2RuleSets);
    Assert.Equal(rule2.EffectiveChecksum, completed.Rule2EffectiveChecksum);
    Assert.True(File.Exists(completed.CompletionManifestPath));
}

static async Task PackageIntegrationRejectsTamperedArtifactBeforeWrite()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    await File.AppendAllTextAsync(archivePath, "tampered");
    var baseline = Path.Combine(scope.Root, "baseline");
    var solutions = Path.Combine(scope.Root, "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    await File.WriteAllTextAsync(Path.Combine(baseline, "paired.xml"), "<AML />");
    var original = Path.Combine(solutions, "original.xml");
    await File.WriteAllTextAsync(original, "<AML />");
    var request = new AdaptedPackageRequest(Guid.NewGuid(), build.SourceVersion, build.TargetVersion, baseline, solutions,
        Path.Combine(scope.Root, "backups"), Path.Combine(scope.Root, "attempt"), archivePath,
        new OotbHopDiffReuseRequirement(build.SourceVersion, build.TargetVersion, build.RuleSets,
            build.EffectiveRuleChecksum, artifact.ArchiveChecksum!), ResolvedRule2(), DateTimeOffset.UtcNow);

    await Assert.ThrowsAsync<InvalidOperationException>(() => AdaptedPackageBuilder.BuildAsync(request));
    Assert.Equal("<AML />", await File.ReadAllTextAsync(original));
    Assert.False(Directory.Exists(request.BackupRoot));
    Assert.False(Directory.Exists(request.AttemptRoot));
}

static async Task PackageIntegrationBlocksFinalizationForManualReview()
{
    await using var scope = TestScope.Create();
    var build = await CreateReadyHopDiffAsync(scope.Root);
    var archivePath = Path.Combine(scope.Root, "rule1.zip");
    var artifact = await OotbHopDiffPackager.PackageAsync(build, archivePath, DateTimeOffset.UtcNow);
    var baseline = Path.Combine(scope.Root, "baseline");
    var solutions = Path.Combine(scope.Root, "Solutions");
    Directory.CreateDirectory(baseline);
    Directory.CreateDirectory(solutions);
    await File.WriteAllTextAsync(Path.Combine(baseline, "paired.xml"),
        "<AML><Item type=\"Part\" id=\"A\" action=\"edit\"><label>one</label><label>two</label><description>customer</description></Item></AML>");
    await File.WriteAllTextAsync(Path.Combine(solutions, "original.xml"), "<AML />");
    var adapted = await AdaptedPackageBuilder.BuildAsync(new AdaptedPackageRequest(Guid.NewGuid(), build.SourceVersion,
        build.TargetVersion, baseline, solutions, Path.Combine(scope.Root, "backups"), Path.Combine(scope.Root, "attempt"),
        archivePath, new OotbHopDiffReuseRequirement(build.SourceVersion, build.TargetVersion, build.RuleSets,
            build.EffectiveRuleChecksum, artifact.ArchiveChecksum!), ResolvedRule2(), DateTimeOffset.UtcNow));
    var completionRoot = Path.Combine(scope.Root, "completion");

    Assert.Equal(AdaptedPackageStatus.Blocked, adapted.Status);
    Assert.True(adapted.ManualReviews.Any(review => review.Code == "AmbiguousScalarProperty"));
    await Assert.ThrowsAsync<InvalidOperationException>(() => AdaptedPackageFinalizer.FinalizeAsync(adapted, completionRoot, DateTimeOffset.UtcNow));
    Assert.False(Directory.Exists(completionRoot));
}

static async Task CoreTreeValidatesInputs()
{
    await using var scope = TestScope.Create();
    var request = CreateCoreTreeRequest(scope.Root);
    Directory.Delete(Path.Combine(request.Customer.RootPath, "Innovator", "Server"));

    Assert.Throws<InvalidOperationException>(() => CoreTreeInputValidator.Validate(request));

    Directory.CreateDirectory(Path.Combine(request.Customer.RootPath, "Innovator", "Server"));
    var mismatch = request with { Customer = request.Customer with { InnovatorVersion = "11SP5" } };
    Assert.Throws<InvalidOperationException>(() => CoreTreeInputValidator.Validate(mismatch));
    var tamperedRules = request with { ServerTextRules = request.ServerTextRules with { Checksum = "TAMPERED" } };
    Assert.Throws<InvalidOperationException>(() => CoreTreeInputValidator.Validate(tamperedRules));
}

static async Task CoreTreeClientTextComparisonFollowsRules()
{
    await using var scope = TestScope.Create();
    var left = Path.Combine(scope.Root, "left.js");
    var right = Path.Combine(scope.Root, "right.js");
    await File.WriteAllTextAsync(left, "\uFEFFconst x = 1;\r\n");
    await File.WriteAllTextAsync(right, "const x = 1;\n");
    var rules = new CoreTreeServerTextRuleSet("server-text-1", "ABC123", ["Server/method-config.xml"]);

    Assert.True(await CoreTreeContentComparer.AreEqualAsync(left, right, "Client/scripts/app.js", rules));
    await File.WriteAllTextAsync(right, "const  x = 1;\n");
    Assert.False(await CoreTreeContentComparer.AreEqualAsync(left, right, "Client/scripts/app.js", rules));
}

static async Task CoreTreeServerComparisonPinsRuleSet()
{
    await using var scope = TestScope.Create();
    var left = Path.Combine(scope.Root, "left.xml");
    var right = Path.Combine(scope.Root, "right.xml");
    await File.WriteAllTextAsync(left, "<x a=\"1\" />\r\n");
    await File.WriteAllTextAsync(right, "<x a=\"1\" />\n");
    var rules = new CoreTreeServerTextRuleSet("server-text-1", "ABC123", ["Server/method-config.xml"]);

    Assert.True(await CoreTreeContentComparer.AreEqualAsync(left, right, "Server/method-config.xml", rules));
    Assert.False(await CoreTreeContentComparer.AreEqualAsync(left, right, "Server/other.xml", rules));
}

static async Task CoreTreeRecordsBinaryFallbackReason()
{
    await using var scope = TestScope.Create();
    var left = Path.Combine(scope.Root, "left.js");
    var right = Path.Combine(scope.Root, "right.js");
    await File.WriteAllBytesAsync(left, [0xFF, 0xFE, 0xFD]);
    await File.WriteAllBytesAsync(right, [0xFF, 0xFE, 0xFC]);
    var rules = DefaultCoreTreeServerTextRules.Create();

    var result = await CoreTreeContentComparer.CompareAsync(left, right, "Client/app.js", rules);

    Assert.Equal(CoreTreeContentComparisonMode.BinaryFallback, result.Mode);
    Assert.False(result.AreEqual);
    Assert.True(!string.IsNullOrWhiteSpace(result.FallbackReason));
    Assert.True(rules.RelativePaths.Contains("Server/method-config.xml"));
}

static async Task CoreTreeLogicalPathResolutionIsDeterministic()
{
    await using var scope = TestScope.Create();
    var r38 = Path.Combine(scope.Root, "r38", "Innovator");
    Directory.CreateDirectory(Path.Combine(r38, "Client", "scripts"));
    await File.WriteAllTextAsync(Path.Combine(r38, "Client", "scripts", "app.ts"), "ts");
    var unique = CoreTreeLogicalPathResolver.Resolve("Client/scripts/app.js", r38);
    Assert.Equal(CoreTreeLogicalMatchStatus.Unique, unique.Status);
    Assert.Equal("Client/scripts/app.ts", unique.Candidates.Single());

    await File.WriteAllTextAsync(Path.Combine(r38, "Client", "scripts", "app.tsx"), "tsx");
    var ambiguous = CoreTreeLogicalPathResolver.Resolve("Client/scripts/app.js", r38);
    Assert.Equal(CoreTreeLogicalMatchStatus.Ambiguous, ambiguous.Status);
    Assert.Equal(2, ambiguous.Candidates.Count);

    Directory.CreateDirectory(Path.Combine(r38, "Client", "other"));
    await File.WriteAllTextAsync(Path.Combine(r38, "Client", "other", "only.ts"), "ts");
    Assert.Equal(CoreTreeLogicalMatchStatus.None,
        CoreTreeLogicalPathResolver.Resolve("Client/scripts/only.js", r38).Status);
}

static CoreTreeComparisonRequest CreateCoreTreeRequest(string root)
{
    static CoreTreeInputEvidence CreateInput(string rootPath, string name, string version)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(Path.Combine(path, "Innovator", "Client"));
        Directory.CreateDirectory(Path.Combine(path, "Innovator", "Server"));
        return new CoreTreeInputEvidence(path, version, $"{name}-version-evidence");
    }

    return new CoreTreeComparisonRequest(
        Guid.NewGuid(),
        "12SP18",
        "R38",
        CreateInput(root, "customer", "12SP18"),
        CreateInput(root, "source-ootb", "12SP18"),
        CreateInput(root, "r38-ootb", "R38"),
        Path.Combine(root, "output"),
        CoreTreeServerTextRuleSet.Create("server-text-1", ["Server/method-config.xml"]),
        DateTimeOffset.UtcNow);
}

static async Task CoreTreeClassifiesCustomerFiles()
{
    await using var scope = TestScope.Create();
    var request = CreateCoreTreeRequest(scope.Root);
    var customer = Path.Combine(request.Customer.RootPath, "Innovator");
    var source = Path.Combine(request.SourceOotb.RootPath, "Innovator");
    var target = Path.Combine(request.TargetOotb.RootPath, "Innovator");
    await WriteCoreTreeFile(customer, "Client/custom.js", "customer-only");
    await WriteCoreTreeFile(customer, "Server/removed.bin", "customer-change");
    await WriteCoreTreeFile(source, "Server/removed.bin", "ootb-old");
    await WriteCoreTreeFile(customer, "Client/pages/view.htm", "customer-view");
    await WriteCoreTreeFile(source, "Client/pages/view.htm", "ootb-view");
    await WriteCoreTreeFile(target, "Client/pages/view.cshtml", "r38-view");
    await WriteCoreTreeFile(customer, "Client/unchanged.js", "same");
    await WriteCoreTreeFile(source, "Client/unchanged.js", "same");

    var result = await CoreTreeComparisonEngine.CompareAsync(request);

    Assert.Equal(CoreTreeComparisonStatus.ReadyToComplete, result.Status);
    Assert.Equal(CoreTreeClassification.A, result.Items.Single(item => item.SourceRelativePath == "Client/custom.js").Classification);
    Assert.Equal(CoreTreeClassification.B, result.Items.Single(item => item.SourceRelativePath == "Server/removed.bin").Classification);
    var c = result.Items.Single(item => item.SourceRelativePath == "Client/pages/view.htm");
    Assert.Equal(CoreTreeClassification.C, c.Classification);
    Assert.Equal("Client/pages/view.cshtml", c.TargetRelativePath);
    Assert.False(result.Items.Any(item => item.SourceRelativePath == "Client/unchanged.js"));
}

static async Task CoreTreeAmbiguityBlocksClassification()
{
    await using var scope = TestScope.Create();
    var request = CreateCoreTreeRequest(scope.Root);
    var customer = Path.Combine(request.Customer.RootPath, "Innovator");
    var source = Path.Combine(request.SourceOotb.RootPath, "Innovator");
    var target = Path.Combine(request.TargetOotb.RootPath, "Innovator");
    await WriteCoreTreeFile(customer, "Client/scripts/app.js", "customer");
    await WriteCoreTreeFile(source, "Client/scripts/app.js", "source");
    await WriteCoreTreeFile(target, "Client/scripts/app.ts", "target-ts");
    await WriteCoreTreeFile(target, "Client/scripts/app.tsx", "target-tsx");
    await WriteCoreTreeFile(customer, "Client/new.htm", "new");
    await WriteCoreTreeFile(target, "Client/new.html", "collision");

    var result = await CoreTreeComparisonEngine.CompareAsync(request);

    Assert.Equal(CoreTreeComparisonStatus.Blocked, result.Status);
    Assert.Equal(2, result.ManualReviews.Count);
    Assert.True(result.ManualReviews.Any(review => review.Code == "MultipleR38Candidates"));
    Assert.True(result.ManualReviews.Any(review => review.Code == "CustomerAdditionCollidesWithR38"));
    Assert.False(result.Items.Any());
}

static async Task CoreTreeBuilderProducesCompletedOutput()
{
    await using var scope = TestScope.Create();
    var request = CreateCoreTreeRequest(scope.Root);
    var customer = Path.Combine(request.Customer.RootPath, "Innovator");
    var source = Path.Combine(request.SourceOotb.RootPath, "Innovator");
    var target = Path.Combine(request.TargetOotb.RootPath, "Innovator");
    await WriteCoreTreeFile(customer, "Client/new.js", "new");
    await WriteCoreTreeFile(customer, "Server/removed.dll", "customer");
    await WriteCoreTreeFile(source, "Server/removed.dll", "source");
    await WriteCoreTreeFile(customer, "Client/view.htm", "customer-view");
    await WriteCoreTreeFile(source, "Client/view.htm", "source-view");
    await WriteCoreTreeFile(target, "Client/view.cshtml", "target-view");
    var before = await File.ReadAllTextAsync(Path.Combine(customer, "Client", "view.htm"));
    var leases = new DirectoryLeaseManager(scope.ToolDataRoot);

    var result = await CoreTreeComparisonBuilder.BuildAsync(request, leases);

    Assert.Equal(CoreTreeComparisonStatus.Completed, result.Status);
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "A", "CustomerSource", "Client", "new.js")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "B", "CustomerSource", "Server", "removed.dll")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "B", "OOTBSource", "Server", "removed.dll")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "C", "CustomerSource", "Client", "view.cshtml")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "C", "OOTBSource", "Client", "view.cshtml")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "C", "OOTBR38", "Client", "view.cshtml")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "processing-summary.json")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "completion-manifest.json")));
    Assert.False(File.Exists(Path.Combine(request.OutputRoot, "incomplete-manifest.json")));
    Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(customer, "Client", "view.htm")));
}

static async Task CoreTreeBuilderBlocksIncompleteAndOverwrite()
{
    await using var scope = TestScope.Create();
    var request = CreateCoreTreeRequest(scope.Root);
    var customer = Path.Combine(request.Customer.RootPath, "Innovator");
    var source = Path.Combine(request.SourceOotb.RootPath, "Innovator");
    var target = Path.Combine(request.TargetOotb.RootPath, "Innovator");
    await WriteCoreTreeFile(customer, "Client/app.js", "customer");
    await WriteCoreTreeFile(source, "Client/app.js", "source");
    await WriteCoreTreeFile(target, "Client/app.ts", "target");
    await WriteCoreTreeFile(target, "Client/app.tsx", "target");
    var leases = new DirectoryLeaseManager(scope.ToolDataRoot);

    var result = await CoreTreeComparisonBuilder.BuildAsync(request, leases);

    Assert.Equal(CoreTreeComparisonStatus.Incomplete, result.Status);
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "manual-reviews.json")));
    Assert.True(File.Exists(Path.Combine(request.OutputRoot, "incomplete-manifest.json")));
    Assert.False(File.Exists(Path.Combine(request.OutputRoot, "completion-manifest.json")));
    Assert.False(Directory.Exists(Path.Combine(request.OutputRoot, "C")));
    await Assert.ThrowsAsync<InvalidOperationException>(() => CoreTreeComparisonBuilder.BuildAsync(request, leases));
}

static async Task WriteCoreTreeFile(string innovatorRoot, string relativePath, string content)
{
    var path = Path.Combine(innovatorRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await File.WriteAllTextAsync(path, content);
}

static Task CoreTreeSkillReferencesTestedCore()
{
    var skillRoot = ProjectPath(".agents", "skills", "aras-compare-core-tree");
    var skill = File.ReadAllText(Path.Combine(skillRoot, "SKILL.md"));
    var capabilities = File.ReadAllText(Path.Combine(skillRoot, "references", "core-capabilities.md"));
    AssertSkillFrontmatter(skill, "aras-compare-core-tree");
    AssertAgentMetadata(Path.Combine(skillRoot, "agents", "openai.yaml"), "aras-compare-core-tree");
    foreach (var typeName in new[] { "CoreTreeInputValidator", "CoreTreeContentComparer", "CoreTreeLogicalPathResolver", "CoreTreeComparisonEngine", "CoreTreeComparisonBuilder" })
        Assert.True(capabilities.Contains($"`{typeName}`", StringComparison.Ordinal), $"Core Tree 核心能力對照缺少 {typeName}。 ");
    Assert.True(skill.Contains("不合併或修改 R38 Core Tree", StringComparison.Ordinal));
    Assert.True(skill.Contains("多候選", StringComparison.Ordinal));
    Assert.True(skill.Contains("不得手工建立或改寫 `Completed`", StringComparison.Ordinal));
    Assert.True(skill.Contains("未取得實際客戶目錄授權", StringComparison.Ordinal));
    return Task.CompletedTask;
}

static Task CoreTreeCapabilitySkillArchitectureIsRecorded()
{
    var context = File.ReadAllText(ProjectPath("CONTEXT.md"));
    foreach (var term in new[] { "細項能力 Skill", "Skill 契約", "語言中立驗收案例", "符合契約的實作" })
        Assert.True(context.Contains($"**{term}**:", StringComparison.Ordinal), $"CONTEXT.md 缺少 {term}。 ");

    var adr2 = File.ReadAllText(ProjectPath("docs", "adr", "0002-three-layer-skill-architecture.md"));
    var adr3 = File.ReadAllText(ProjectPath("docs", "adr", "0003-language-neutral-capability-skills.md"));
    Assert.True(adr2.Contains("ADR 0003", StringComparison.Ordinal));
    Assert.True(adr3.Contains("語言中立驗收案例", StringComparison.Ordinal));
    Assert.True(adr3.Contains("不依程式語言或類別切分", StringComparison.Ordinal));
    Assert.True(adr3.Contains("父 Skill 的 `references`", StringComparison.Ordinal));
    Assert.True(adr3.Contains("`mode`", StringComparison.Ordinal));

    var skillMap = File.ReadAllText(ProjectPath("docs", "design", "skill-map.md"));
    foreach (var (skillName, referenceType) in new[]
    {
        ("aras-validate-core-tree-inputs", "CoreTreeInputValidator"),
        ("aras-compare-core-tree-content", "CoreTreeContentComparer"),
        ("aras-resolve-core-tree-file-mappings", "CoreTreeLogicalPathResolver"),
        ("aras-classify-core-tree-differences", "CoreTreeComparisonEngine"),
        ("aras-build-core-tree-delivery", "CoreTreeComparisonBuilder")
    })
    {
        Assert.True(skillMap.Contains($"| `{skillName}` | 依 ADR 0003 建置中 |", StringComparison.Ordinal), $"Skill Map 缺少 {skillName} 或正確狀態。 ");
        Assert.True(skillMap.Contains($"未來驗收 `{skillName}/assets/acceptance-cases/`", StringComparison.Ordinal), $"Skill Map 缺少 {skillName} 驗收路徑。 ");
        Assert.True(skillMap.Contains($"現有 C# 參考實作 `{referenceType}`，Skill 契約為規格來源", StringComparison.Ordinal), $"Skill Map 缺少 {skillName} 的 C# 參考實作定位。 ");
    }
    return Task.CompletedTask;
}

static Task CoreTreeCapabilityContractIsStable()
{
    var contract = File.ReadAllText(ProjectPath("docs", "design", "core-tree-capability-contract.md"));
    foreach (var token in new[]
    {
        "core-tree-capabilities/1", "Equal", "Different", "None", "Unique", "Ambiguous",
        "TextDecodeFallback", "MultipleTargetMappings", "CustomerAdditionCollidesWithTarget",
        "InvalidRequest", "InputDirectoryMissing", "VersionEvidenceMismatch", "RequiredTreeStructureMissing",
        "InputDirectoryOverlap", "InputOutputOverlap", "InvalidServerRuleSet", "RuleChecksumMismatch",
        "OutputAttemptAlreadyExists", "FileReadError",
        "ReadyToComplete", "Blocked", "Incomplete", "Completed"
    })
        Assert.True(contract.Contains($"`{token}`", StringComparison.Ordinal), $"共用契約缺少穩定 token `{token}`。");
    return Task.CompletedTask;
}

static Task CoreTreeInputValidationSkillPackageIsComplete()
{
    CoreTreeCapabilitySkillTests.AssertPackage("aras-validate-core-tree-inputs",
        ["valid-inputs", "version-mismatch", "missing-structure", "overlapping-output", "rule-checksum-mismatch"]);
    return Task.CompletedTask;
}

static CustomerPackageLockRequest TestPackageLockRequest(Guid flowAttemptId, string target) => new(
    flowAttemptId,
    "customer-package.generate",
    "rehearsal",
    target,
    "db-backup-before-package",
    "db-backup-proof",
    "original-package-backup",
    "original-package-proof",
    []);

static string ProjectPath(params string[] segments)
{
    var current = new DirectoryInfo(Environment.CurrentDirectory);
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "ArasUpgradeOrchestrator.sln")))
        current = current.Parent;
    if (current is null) throw new DirectoryNotFoundException("找不到專案根目錄。 ");
    return segments.Aggregate(current.FullName, Path.Combine);
}

static void AssertSkillFrontmatter(string content, string expectedName)
{
    var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    Assert.Equal("---", lines[0]);
    var closing = Array.IndexOf(lines, "---", 1);
    Assert.True(closing > 2, "Skill frontmatter 未正確關閉。 ");
    var fields = lines[1..closing].Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    Assert.Equal(2, fields.Length);
    Assert.Equal($"name: {expectedName}", fields[0]);
    Assert.True(fields[1].StartsWith("description: ", StringComparison.Ordinal) && fields[1].Length > "description: ".Length + 20,
        "Skill description 必須包含清楚觸發條件。 ");
    Assert.True(expectedName.Length <= 64 && expectedName.All(character => char.IsAsciiLetterLower(character) || char.IsDigit(character) || character == '-'));
    var description = fields[1]["description: ".Length..];
    Assert.True(description.Length <= 1024 && !description.Contains('<') && !description.Contains('>'));
}

static void AssertAgentMetadata(string path, string skillName)
{
    var content = File.ReadAllText(path);
    Assert.True(content.Contains("display_name: \"", StringComparison.Ordinal));
    Assert.True(content.Contains("short_description: \"", StringComparison.Ordinal));
    Assert.True(content.Contains($"default_prompt: \"使用 ${skillName}", StringComparison.Ordinal));
}

static UpgradeRoute TestRoute(string? root = null)
{
    root ??= Environment.CurrentDirectory;
    return UpgradeRoute.Create(1,
    [
        new UpgradeHop("11SP5", "12SP18", Path.Combine(root, "12SP18", "Support")),
        new UpgradeHop("12SP18", "R38", Path.Combine(root, "R38", "Support"))
    ], DateTimeOffset.Parse("2026-08-03T00:00:00Z"));
}

static ExecutionSnapshot TestSnapshot(string taskId, string root) => new(
    taskId,
    "test.action",
    "1",
    root,
    new Dictionary<string, string> { ["input"] = "value" },
    "test-tool-1",
    "ABC123");

static async Task<List<HistoryEntry>> ReadAll(AppendOnlyHistoryStore history)
{
    var result = new List<HistoryEntry>();
    await foreach (var entry in history.ReadAllAsync()) result.Add(entry);
    return result;
}

sealed class FakeExternalExecutor(ExternalActionResult result) : IExternalActionExecutor
{
    public int CallCount { get; private set; }
    public Task<ExternalActionResult> ExecuteAsync(ExternalActionContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

sealed class RecordingExternalExecutor(ExternalActionResult result) : IExternalActionExecutor
{
    public int CallCount { get; private set; }
    public Task<ExternalActionResult> ExecuteAsync(ExternalActionContext context, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

sealed class TestScope : IAsyncDisposable
{
    private TestScope(string root)
    {
        Root = root;
        CaseRoot = Path.Combine(root, "case");
        ToolDataRoot = Path.Combine(CaseRoot, CaseStore.ToolDataDirectoryName);
        Directory.CreateDirectory(ToolDataRoot);
    }

    public string Root { get; }
    public string CaseRoot { get; }
    public string ToolDataRoot { get; }

    public static TestScope Create()
    {
        var testParent = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".test-output"));
        Directory.CreateDirectory(testParent);
        return new TestScope(Path.Combine(testParent, Guid.NewGuid().ToString("N")));
    }

    public ValueTask DisposeAsync()
    {
        var testParent = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, ".test-output"));
        var resolved = Path.GetFullPath(Root);
        if (resolved.StartsWith(testParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            Directory.Delete(resolved, true);
        return ValueTask.CompletedTask;
    }
}

static class Assert
{
    public static void True(bool condition, string message = "Expected true.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message = "Expected false.") => True(!condition, message);

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    public static void NotEqual<T>(T unexpected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(unexpected, actual))
            throw new InvalidOperationException($"Did not expect '{actual}'.");
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
    }

    public static void Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    public static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
