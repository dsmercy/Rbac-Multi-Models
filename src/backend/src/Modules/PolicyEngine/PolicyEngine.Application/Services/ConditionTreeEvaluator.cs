using PermissionEngine.Domain.Models;
using System.Text.Json;

namespace PolicyEngine.Application.Services;

public sealed class ConditionTreeEvaluator
{
    /// <summary>
    /// Evaluates a JSON condition tree against the provided EvaluationContext.
    /// Returns true if the condition tree is satisfied.
    /// </summary>
    public bool Evaluate(string conditionTreeJson, EvaluationContext context)
    {
        var node = JsonSerializer.Deserialize<ConditionNode>(
            conditionTreeJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (node is null)
            return false;

        return EvaluateNode(node, context);
    }

    private bool EvaluateNode(ConditionNode node, EvaluationContext context)
    {
        if (node.IsGroup)
        {
            var children = node.Conditions ?? [];

            return node.Operator?.ToUpperInvariant() switch
            {
                "AND" => children.All(c => EvaluateNode(c, context)),
                "OR"  => children.Any(c => EvaluateNode(c, context)),
                "NOT" => children.Count == 1 && !EvaluateNode(children[0], context),
                _     => false
            };
        }

        if (node.IsLeaf)
            return EvaluateLeaf(node, context);

        return false;
    }

    private bool EvaluateLeaf(ConditionNode node, EvaluationContext context)
    {
        var actualValue = ResolveAttribute(node.Attribute!, context);

        if (actualValue is null)
            return false;

        return node.Op?.ToUpperInvariant() switch
        {
            "EQ"         => Compare(actualValue, node.Value) == 0,
            "NEQ"        => Compare(actualValue, node.Value) != 0,
            "GT"         => Compare(actualValue, node.Value) > 0,
            "GTE"        => Compare(actualValue, node.Value) >= 0,
            "LT"         => Compare(actualValue, node.Value) < 0,
            "LTE"        => Compare(actualValue, node.Value) <= 0,
            "IN"         => EvaluateIn(actualValue, node.Value),
            "NOTIN"      => !EvaluateIn(actualValue, node.Value),
            "BETWEEN"    => EvaluateBetween(actualValue, node.Value),
            "CONTAINS"   => actualValue.ToString()!.Contains(node.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            "STARTSWITH" => actualValue.ToString()!.StartsWith(node.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase),
            _            => false
        };
    }

    private static object? ResolveAttribute(string attribute, EvaluationContext context)
    {
        // Attribute paths: "user.X", "resource.X", "env.X"
        var parts = attribute.Split('.', 2);
        if (parts.Length != 2) return null;

        var dict = parts[0].ToLowerInvariant() switch
        {
            "user"     => context.UserAttributes,
            "resource" => context.ResourceAttributes,
            "env"      => context.EnvironmentAttributes,
            _          => null
        };

        return dict is not null && dict.TryGetValue(parts[1], out var val) ? val : null;
    }

    private static int Compare(object actual, object? expected)
    {
        if (expected is null) return -1;
        var a = actual.ToString()!;
        var e = expected.ToString()!;

        if (double.TryParse(a, out var da) && double.TryParse(e, out var de))
            return da.CompareTo(de);

        if (DateTimeOffset.TryParse(a, out var dta) && DateTimeOffset.TryParse(e, out var dte))
            return dta.CompareTo(dte);

        return string.Compare(a, e, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateIn(object actual, object? expected)
    {
        if (expected is JsonElement { ValueKind: JsonValueKind.Array } arr)
        {
            foreach (var item in arr.EnumerateArray())
                if (string.Equals(actual.ToString(), item.ToString(), StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }

    private static bool EvaluateBetween(object actual, object? expected)
    {
        if (expected is not JsonElement { ValueKind: JsonValueKind.Array } arr) return false;
        var items = arr.EnumerateArray().ToList();
        if (items.Count != 2) return false;

        return Compare(actual, items[0].GetString()) >= 0
            && Compare(actual, items[1].GetString()) <= 0;
    }
}
