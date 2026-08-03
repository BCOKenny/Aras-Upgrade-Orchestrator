using ArasUpgradeOrchestrator.Core.Aml;
using ArasUpgradeOrchestrator.Core.Cases;
using ArasUpgradeOrchestrator.Core.Execution;
using ArasUpgradeOrchestrator.Core.Packages;
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
