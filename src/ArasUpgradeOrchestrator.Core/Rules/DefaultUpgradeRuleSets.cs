namespace ArasUpgradeOrchestrator.Core.Rules;

public static class DefaultUpgradeRuleSets
{
    public static RuleSetDraft CreateRule1Draft(string actor, DateTimeOffset createdAt, Guid? ruleSetId = null) => new(
        Guid.NewGuid(), ruleSetId ?? Guid.NewGuid(), "Rule 1 OOTB 跳點差異共同準則", RuleSetKind.Rule1, RuleSetScope.Common,
        null, null,
        [new RuleStepDefinition("rule1-item-disposition", 1, RuleStepKind.Rule1ItemDisposition, [], [], null)],
        createdAt, actor);

    public static RuleSetDraft CreateRule2Draft(string actor, DateTimeOffset createdAt, Guid? ruleSetId = null) => new(
        Guid.NewGuid(), ruleSetId ?? Guid.NewGuid(), "Rule 2 客戶 Package 適配共同準則", RuleSetKind.Rule2, RuleSetScope.Common,
        null, null,
        [
            Step("remove-equal-scalar", 1, RuleStepKind.RemoveEqualScalarProperties),
            Step("remove-named-properties", 2, RuleStepKind.RemoveNamedProperties,
                ["sort_order", "x", "y", "font_family", "image", "from_date"]),
            Step("prefer-greater-source-number", 3, RuleStepKind.PreferGreaterSourceNumber,
                ["stored_length", "column_width"]),
            Step("prefer-source-under-target-path", 4, RuleStepKind.PreferSourceUnderTargetPath,
                ["permission_id", "data_type", "label", "icon", "value"], targetPath: "OOTB_R38/PLM/Import"),
            Step("keep-target-value-pairs", 5, RuleStepKind.KeepTargetForValuePairs, valuePairs:
            [
                Pair("font_color", "#000000", "#333333"), Pair("bg_color", "#5f6871", "#f5f5f5"),
                Pair("structure_view", "tabs off", "tabs on"), EmptyToNonEmpty("label"),
                Pair("color", "#8959ab", "#7b1fa2"), Pair("color", "#7ec678", "#4eb600"),
                Pair("color", "#a76163", "#bf360c"), EmptyToNonEmpty("default_value"),
                EmptyToNonEmpty("data_source"), EmptyToNonEmpty("is_discoverable"),
                EmptyToNonEmpty("is_federated"), Pair("pattern", "long_date_time", "short_date_time"),
                EmptyToNonEmpty("keyed_name_order"), EmptyToNonEmpty("behavior"),
                EmptyToNonEmpty("password")
            ]),
            Step("keep-target-named-properties", 6, RuleStepKind.KeepTargetNamedProperties,
            [
                "can_discover", "html_code", "field_type", "additional_data", "on_init_handler", "on_click_handler",
                "tooltip_template", "include_events", "on_keydown_handler", "command_alias", "keyed_name", "name",
                "core_toc_sorting_type", "sealed", "prevent_default_event_handlers", "show_help", "css", "is_disabled",
                "field_event", "use_magic_bytes", "use_regular_expression", "execute_post_in_main_txn", "related_id",
                "data_template", "is_setter_allowed", "cell_view_type", "report_query", "xsl_stylesheet", "search_handler",
                "template", "sqlserver_body", "stylesheet_id", "content", "text", "inactive"
            ]),
            Step("default-prefer-source", 7, RuleStepKind.DefaultPreferSourceUnlessSourceEmpty)
        ],
        createdAt, actor);

    private static RuleStepDefinition Step(
        string id,
        int order,
        RuleStepKind kind,
        IReadOnlyList<string>? properties = null,
        IReadOnlyList<RuleValuePair>? valuePairs = null,
        string? targetPath = null) =>
        new(id, order, kind, properties ?? [], valuePairs ?? [], targetPath);

    private static RuleValuePair Pair(string propertyName, string sourceValue, string targetValue) =>
        new(propertyName, RuleValueCondition.Exact(sourceValue), RuleValueCondition.Exact(targetValue));

    private static RuleValuePair EmptyToNonEmpty(string propertyName) =>
        new(propertyName, RuleValueCondition.Empty(), RuleValueCondition.NonEmpty());
}
