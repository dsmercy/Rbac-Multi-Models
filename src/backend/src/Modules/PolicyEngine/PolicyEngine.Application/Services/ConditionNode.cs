namespace PolicyEngine.Application.Services;

/// <summary>
/// Represents a node in the JSON condition tree (AWS IAM-style).
/// A node is either a leaf condition or a logical group (And/Or/Not).
///
/// Example JSON:
/// {
///   "operator": "And",
///   "conditions": [
///     { "attribute": "user.department", "op": "Eq", "value": "Engineering" },
///     { "attribute": "env.time_utc",    "op": "Between", "value": ["08:00","18:00"] }
///   ]
/// }
/// </summary>
public sealed class ConditionNode
{
    /// <summary>Logical operator for group nodes: And, Or, Not</summary>
    public string? Operator { get; set; }

    /// <summary>Child nodes for group operators</summary>
    public List<ConditionNode>? Conditions { get; set; }

    /// <summary>Attribute path for leaf nodes: "user.department", "resource.classification", "env.time_utc"</summary>
    public string? Attribute { get; set; }

    /// <summary>Comparison operator for leaf nodes: Eq, Neq, Gt, Gte, Lt, Lte, In, NotIn, Between, Contains, StartsWith</summary>
    public string? Op { get; set; }

    /// <summary>Expected value(s) for comparison. Can be a string, number, or array.</summary>
    public object? Value { get; set; }

    public bool IsGroup => Operator is not null;
    public bool IsLeaf => Attribute is not null && Op is not null;
}
