// Clause 13.8.3 — the switch statement, and the switch *expression* the language grew later.
//
// The row is marked `both`, and it earns both: a switch section's statement list is a
// declaration space, so a case label's pattern and the section's locals are declarations,
// while `goto case` and `goto default` are references to a *label* that may not even be
// written as an identifier — `goto case 3;` refers to `case 3:` by its constant value.
//
// The hazard is `hit`, declared in three switch sections of one switch statement. A section is
// its own declaration space (unlike an `if` branch, whose pattern variable escapes to the
// enclosing block), so three sections may each declare `hit` at three different types. This is
// the one place in the language where the same name can be declared three times in one member
// without a single enclosing block between them.

using System;

namespace Surface.Statements;

/// <summary>What a <c>switch</c> can select on, beyond a number.</summary>
public enum StmtSwitchGate
{
    /// <summary>Nothing doing.</summary>
    Idle = 0,

    /// <summary>Working.</summary>
    Running = 1,

    /// <summary>Finished.</summary>
    Done = 2,
}

/// <summary>Clause 13.8.3 — the switch statement, in every shape it has.</summary>
public static class StmtSwitch
{
    /// <summary>
    /// Three switch sections, each declaring <c>hit</c>.
    /// </summary>
    /// <remarks>
    /// 13.8.3, and its hazard. Each section is a declaration space of its own, so the three
    /// declarations are legal and none of them is visible to the others. The types are
    /// `string`, `int` and `StmtSwitchGate`.
    /// </remarks>
    /// <param name="input">Something to match.</param>
    /// <returns>A label for what matched.</returns>
    public static string ThreeHits(object? input)
    {
        switch (input)
        {
            // 13.8.3 — a case label with a declaration pattern. `hit` is a string here.
            case string hit:
                return $"string {hit.Length}";

            // 13.8.3 — `hit` again, an int, in the next section down.
            case int hit when hit > 0:
                return $"positive {hit}";

            // 13.8.3 — and a third `hit`, an enum. Three declarations, one identifier, one
            // containing member, no block boundaries.
            case StmtSwitchGate hit:
                return $"gate {hit}";

            // 13.8.3 — the default section, which declares nothing.
            default:
                return "other";
        }
    }

    /// <summary>
    /// Every structural feature of a switch statement.
    /// </summary>
    /// <remarks>
    /// 13.8.3. Two labels on one section; a section whose statement list declares a local; a
    /// `default` written in the middle rather than at the end, which is legal and changes
    /// nothing; `goto case`, which is a reference to a label named by a constant; `goto
    /// default`; and a fall-through that is not one — every section must end in a jump, so C#
    /// has no implicit fall-through and `goto case` is how it is spelled.
    /// </remarks>
    /// <param name="gate">A number to switch on.</param>
    /// <returns>A label for the path taken.</returns>
    public static string EveryFeature(int gate)
    {
        string trail = string.Empty;

        switch (gate)
        {
            // 13.8.3 — two case labels on one section. One statement list, two labels, and
            // `goto case 1;` below can name either of them.
            case 0:
            case 1:
                trail += "low";
                goto case 5;

            // 13.8.3 — a section whose statement list declares a local. Its scope is the
            // section, which is why `step` may be declared here and again below.
            case 2:
            {
                int step = gate * 2;
                trail += $"two:{step}";
                goto default;
            }

            // 13.8.3 — `default` in the middle of the section list. The order of sections is
            // not the order they are tried in: `default` is tried last whatever it is written
            // next to.
            default:
                trail += "default";
                break;

            // 13.8.3 — the target of the `goto case 5` above, reached from a section written
            // before it.
            case 5:
            {
                int step = gate + 5;
                trail += $"five:{step}";
                break;
            }

            // 13.8.3 — a section that is a single jump, and the second target of a `goto`.
            case 9:
                goto case 2;
        }

        return trail;
    }

    /// <summary>
    /// Switching on the other governing types.
    /// </summary>
    /// <remarks>
    /// 13.8.3. The governing type may be an integral, a char, a string, an enum, a bool or a
    /// nullable of any of those — and, post-standard, anything at all once patterns are
    /// allowed as case labels. Each of the four below binds its constants differently: an
    /// enum label is a reference to a member, a string label is a literal, and a `null` label
    /// is a constant pattern that no pre-pattern switch could express.
    /// </remarks>
    /// <param name="gate">An enum to switch on.</param>
    /// <param name="name">A string to switch on.</param>
    /// <param name="marker">A char to switch on.</param>
    /// <returns>A label for all three.</returns>
    public static string EveryGoverningType(StmtSwitchGate gate, string? name, char marker)
    {
        string trail = string.Empty;

        // 13.8.3 — an enum governing type. Each case label is a reference to an enum member.
        switch (gate)
        {
            case StmtSwitchGate.Idle:
                trail += "idle";
                break;

            case StmtSwitchGate.Running:
            case StmtSwitchGate.Done:
                trail += "busy";
                break;
        }

        // 13.8.3 — a string governing type, including the `null` label, which is a constant
        // pattern and not a value the old grammar admitted.
        switch (name)
        {
            case null:
                trail += "|null";
                break;

            case "":
                trail += "|empty";
                break;

            case "seed":
                trail += "|seed";
                break;

            default:
                trail += $"|{name.Length}";
                break;
        }

        // 13.8.3 — a char governing type, with a relational pattern label, which is
        // post-standard.
        switch (marker)
        {
            case 'x':
                trail += "|x";
                break;

            case >= 'a' and <= 'z':
                trail += "|lower";
                break;

            default:
                trail += "|other";
                break;
        }

        return trail;
    }

    /// <summary>
    /// The switch <em>expression</em>, which is post-standard and is not clause 13.8.3 at all.
    /// </summary>
    /// <remarks>
    /// Written here because it is what most of a modern codebase's switching is, and because
    /// it is the contrast that makes 13.8.3 legible: an expression has arms rather than
    /// sections, no `break`, no `goto case`, no fall-through and no statement list — so an
    /// arm cannot declare a local and its pattern variable is scoped to the arm. Everything
    /// clause 13.8.3 says about declaration spaces stops applying here.
    /// </remarks>
    /// <param name="input">Something to match.</param>
    /// <returns>A label for what matched.</returns>
    public static string AsExpression(object? input) => input switch
    {
        string arm => $"string {arm.Length}",
        int arm when arm > 0 => $"positive {arm}",
        int arm => $"other int {arm}",
        StmtSwitchGate.Done => "done",
        null => "null",
        _ => "other",
    };
}
