namespace Surface.Quarantine.Ordinal;

/// <summary>
/// M2 — the use sites, in a file that keeps its place when the two parts are swapped.
/// </summary>
/// <remarks>
/// <para>
/// Clause 12.8.10 (invocation expressions) and 12.6.4 (overload resolution). Two calls to
/// <c>Send</c>, one per overload: with both overloads' identity merged onto one symbol,
/// the cross-reference relation seeked on <c>Send(int)</c>'s symbol holds both of them
/// where it should hold exactly one.
/// </para>
/// <para>
/// The <c>Take</c> call is the tie face's probe. It binds to <c>Take(int)</c> — the
/// more specific parameter type wins — but the ordinal it is <i>described</i> with is read
/// off <c>OrdinalSub&lt;int&gt;.GetMembers()</c>, where both overloads carry one
/// documentation id and the sort cannot separate them.
/// </para>
/// </remarks>
public class OrdinalUse
{
    /// <summary>Calls both <c>Send</c> overloads, and returns something read.</summary>
    public int Go()
    {
        var courier = new OrdinalCourier();
        courier.Send(1);
        courier.Send("x");
        return courier.Sent + courier.Length;
    }

    /// <summary>Calls <c>Take</c> through the constructed type, where the ids tie.</summary>
    public int Constructed() => new OrdinalSub<int>().Take(5);
}
