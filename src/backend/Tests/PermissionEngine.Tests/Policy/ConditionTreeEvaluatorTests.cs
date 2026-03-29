// =============================================================================
//  ConditionTreeEvaluatorTests.cs  –  ABAC policy engine (20 cases)
// =============================================================================

using FluentAssertions;
using PermissionEngine.Domain.Models;
using PolicyEngine.Application.Services;
using Xunit;

namespace PermissionEngine.Tests.Policy;

public sealed class ConditionTreeEvaluatorTests
{
    private readonly ConditionTreeEvaluator _eval = new();

    // ── Context factory ───────────────────────────────────────────────────────
    private static EvaluationContext Ctx(
        string? dept = null, string? time = null,
        string? ip = null, string? level = null,
        string? country = null)
    {
        var u = new Dictionary<string, object>();
        var e = new Dictionary<string, object>();
        var r = new Dictionary<string, object>();

        if (dept != null) u["department"] = dept;
        if (level != null) u["clearance"] = level;
        if (country != null) u["country"] = country;
        if (time != null) e["time_utc"] = time;
        if (ip != null) e["ip"] = ip;

        return new EvaluationContext(Guid.NewGuid(), Guid.NewGuid(),
            userAttributes: u, environmentAttributes: e, resourceAttributes: r);
    }

    // ── Leaf operators ────────────────────────────────────────────────────────

    [Fact(DisplayName = "CT01 – Eq: matching value returns true")]
    public void CT01_Eq_Match_True()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Eq","value":"Engineering"}""",
                Ctx(dept: "Engineering")).Should().BeTrue();

    [Fact(DisplayName = "CT02 – Eq: non-matching value returns false")]
    public void CT02_Eq_NoMatch_False()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Eq","value":"Engineering"}""",
                Ctx(dept: "Marketing")).Should().BeFalse();

    [Fact(DisplayName = "CT03 – Neq: different value returns true")]
    public void CT03_Neq_Different_True()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Neq","value":"Engineering"}""",
                Ctx(dept: "Marketing")).Should().BeTrue();

    [Fact(DisplayName = "CT04 – Neq: same value returns false")]
    public void CT04_Neq_Same_False()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Neq","value":"Engineering"}""",
                Ctx(dept: "Engineering")).Should().BeFalse();

    [Fact(DisplayName = "CT05 – Between: value inside range returns true")]
    public void CT05_Between_InsideRange_True()
        => _eval.Evaluate("""{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}""",
                Ctx(time: "13:30")).Should().BeTrue();

    [Fact(DisplayName = "CT06 – Between: value outside range returns false")]
    public void CT06_Between_OutsideRange_False()
        => _eval.Evaluate("""{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}""",
                Ctx(time: "23:45")).Should().BeFalse();

    [Fact(DisplayName = "CT07 – Between: value at lower boundary returns true")]
    public void CT07_Between_AtLower_True()
        => _eval.Evaluate("""{"attribute":"env.time_utc","op":"Between","value":["08:00","18:00"]}""",
                Ctx(time: "08:00")).Should().BeTrue();

    [Fact(DisplayName = "CT08 – In: value in list returns true")]
    public void CT08_In_ValueInList_True()
        => _eval.Evaluate("""{"attribute":"user.department","op":"In","value":["Engineering","DevOps","Security"]}""",
                Ctx(dept: "DevOps")).Should().BeTrue();

    [Fact(DisplayName = "CT09 – In: value not in list returns false")]
    public void CT09_In_ValueNotInList_False()
        => _eval.Evaluate("""{"attribute":"user.department","op":"In","value":["Engineering","DevOps"]}""",
                Ctx(dept: "Marketing")).Should().BeFalse();

    [Fact(DisplayName = "CT10 – NotIn: value not in list returns true")]
    public void CT10_NotIn_ValueNotInList_True()
        => _eval.Evaluate("""{"attribute":"user.department","op":"NotIn","value":["Blocked1","Blocked2"]}""",
                Ctx(dept: "Engineering")).Should().BeTrue();

    [Fact(DisplayName = "CT11 – StartsWith: matching prefix returns true")]
    public void CT11_StartsWith_Match_True()
        => _eval.Evaluate("""{"attribute":"env.ip","op":"StartsWith","value":"10.0."}""",
                Ctx(ip: "10.0.1.55")).Should().BeTrue();

    [Fact(DisplayName = "CT12 – StartsWith: non-matching prefix returns false")]
    public void CT12_StartsWith_NoMatch_False()
        => _eval.Evaluate("""{"attribute":"env.ip","op":"StartsWith","value":"10.0."}""",
                Ctx(ip: "192.168.1.1")).Should().BeFalse();

    [Fact(DisplayName = "CT13 – Contains: substring match returns true")]
    public void CT13_Contains_Match_True()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Contains","value":"Eng"}""",
                Ctx(dept: "Engineering")).Should().BeTrue();

    // ── Missing attribute ─────────────────────────────────────────────────────

    [Fact(DisplayName = "CT14 – missing attribute returns false (fail-closed)")]
    public void CT14_MissingAttribute_False()
        => _eval.Evaluate("""{"attribute":"user.department","op":"Eq","value":"Engineering"}""",
                Ctx()) // no dept set
             .Should().BeFalse();

    [Fact(DisplayName = "CT15 – unknown attribute namespace returns false")]
    public void CT15_UnknownNamespace_False()
        => _eval.Evaluate("""{"attribute":"tenant.name","op":"Eq","value":"acme"}""",
                Ctx()).Should().BeFalse();

    // ── Logical operators ─────────────────────────────────────────────────────

    [Fact(DisplayName = "CT16 – And: all conditions true returns true")]
    public void CT16_And_AllTrue_True()
    {
        var json =
            "{\"operator\":\"And\",\"conditions\":[" +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"Engineering\"}," +
            "{\"attribute\":\"env.time_utc\",\"op\":\"Between\",\"value\":[\"08:00\",\"18:00\"]}" +
            "]}";
        _eval.Evaluate(json, Ctx(dept: "Engineering", time: "10:00")).Should().BeTrue();
    }

    [Fact(DisplayName = "CT17 – And: one condition false returns false")]
    public void CT17_And_OneFalse_False()
    {
        var json =
            "{\"operator\":\"And\",\"conditions\":[" +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"Engineering\"}," +
            "{\"attribute\":\"env.time_utc\",\"op\":\"Between\",\"value\":[\"08:00\",\"18:00\"]}" +
            "]}";
        _eval.Evaluate(json, Ctx(dept: "Marketing", time: "10:00")).Should().BeFalse();
    }

    [Fact(DisplayName = "CT18 – Or: one condition true returns true")]
    public void CT18_Or_OneTrue_True()
    {
        var json =
            "{\"operator\":\"Or\",\"conditions\":[" +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"Engineering\"}," +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"DevOps\"}" +
            "]}";
        _eval.Evaluate(json, Ctx(dept: "DevOps")).Should().BeTrue();
    }

    [Fact(DisplayName = "CT19 – Or: all conditions false returns false")]
    public void CT19_Or_AllFalse_False()
    {
        var json =
            "{\"operator\":\"Or\",\"conditions\":[" +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"Engineering\"}," +
            "{\"attribute\":\"user.department\",\"op\":\"Eq\",\"value\":\"DevOps\"}" +
            "]}";
        _eval.Evaluate(json, Ctx(dept: "Marketing")).Should().BeFalse();
    }

    [Fact(DisplayName = "CT20 – Not: negates inner condition correctly for both outcomes")]
    public void CT20_Not_Negates_InnerCondition()
    {
        var json =
            "{\"operator\":\"Not\",\"conditions\":[" +
            "{\"attribute\":\"env.time_utc\",\"op\":\"Between\",\"value\":[\"08:00\",\"18:00\"]}" +
            "]}";

        // Outside business hours → Not(false) = true
        _eval.Evaluate(json, Ctx(time: "23:00")).Should().BeTrue();

        // Inside business hours → Not(true) = false
        _eval.Evaluate(json, Ctx(time: "12:00")).Should().BeFalse();
    }
}