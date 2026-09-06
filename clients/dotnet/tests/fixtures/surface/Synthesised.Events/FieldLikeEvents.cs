// M20 — 15.8.2, field-like events.
//
// `CsharpEntities.Build` ends its event arm at `IEventSymbol => Dropped()`, and `Declare`
// returns on a null entity BEFORE it writes `SymbolOf`, `DefinitionBySymbol` or `Markup`.
// So nothing in this file produces a `csharp` entity row, a `csharp.DefinitionLocation` or a
// `codemarkup` row of any kind — while `ScipSymbols.Of` spells each of them perfectly well
// (`…/Surface/Synthesised/Events/EvGauge#Read.`, the `.` term suffix an event shares with a
// field and a property) and `Reference` writes an xref from every use in `EventUses.cs`.
//
// The two source forms also arrive by different nodes, which is why both are here and why
// one declaration carries three declarators: `EventFieldDeclarationSyntax` derives from
// `BaseFieldDeclarationSyntax` and `GetDeclaredSymbol` on it returns null, so the walk can
// only reach these through their `VariableDeclarator`s. A walk that switched on the
// declaration would drop `First`, `Second` and `Third` as one; a walk that did both would
// count them twice.
namespace Surface.Synthesised.Events;

/// <summary>A gauge that reports readings, in every field-like shape (15.8.2, 15.8.4).</summary>
public class EvGauge
{
    /// <summary>One field-like event, one declarator.</summary>
    public event EvReadingHandler? Read;

    /// <summary>
    /// Three field-like events in ONE declaration: one `EventFieldDeclarationSyntax`, three
    /// `VariableDeclarator`s, three `IEventSymbol`s, three synthesised backing fields.
    /// </summary>
    public event EvReadingHandler? First, Second, Third;

    /// <summary>A static field-like event: the backing field is static too (15.8.4).</summary>
    public static event EvReadingHandler? Discarded;

    /// <summary>A field-like event of a constructed generic delegate type.</summary>
    public event EvPayloadHandler<string>? Labelled;

    /// <summary>
    /// Raises every event this type declares. Inside the declaring type a field-like
    /// event's name may be read as well as assigned (15.8.2), which is the only place the
    /// bare name appears in an invocation position rather than beside `+=` or `-=`.
    /// </summary>
    /// <param name="reading">The reading to report.</param>
    public void Sweep(int reading)
    {
        Read?.Invoke(reading);
        First?.Invoke(reading);
        Second?.Invoke(reading);
        Third?.Invoke(reading);
        Discarded?.Invoke(reading);
        Labelled?.Invoke($"reading {reading}");
    }

    /// <summary>Drops every subscriber by assigning the backing field, which only the declaring type can do.</summary>
    public void Silence()
    {
        Read = null;
        First = null;
        Second = null;
        Third = null;
        Labelled = null;
    }
}

/// <summary>A ticket a struct punches: a field-like event in a struct (16.4.14).</summary>
public struct EvTicket
{
    /// <summary>Raised when the ticket is punched.</summary>
    public event EvReadingHandler? Punched;

    /// <summary>Punches the ticket and reports the stop it was punched at.</summary>
    /// <param name="stop">The stop the ticket was punched at.</param>
    public void Punch(int stop) => Punched?.Invoke(stop);
}

/// <summary>An abstract event (15.8.5): field-like syntax, no backing field, no accessor bodies.</summary>
public abstract class EvChannel
{
    /// <summary>Raised when something on this channel needs a person.</summary>
    public abstract event EvReadingHandler? Escalated;

    /// <summary>Escalates, in whatever way the derived channel wired up.</summary>
    /// <param name="severity">How bad it is.</param>
    public abstract void Escalate(int severity);
}

/// <summary>An override of an abstract event, written field-like — so the override HAS a backing field.</summary>
public sealed class EvUrgentChannel : EvChannel
{
    /// <inheritdoc/>
    public override event EvReadingHandler? Escalated;

    /// <inheritdoc/>
    public override void Escalate(int severity) => Escalated?.Invoke(severity);
}
