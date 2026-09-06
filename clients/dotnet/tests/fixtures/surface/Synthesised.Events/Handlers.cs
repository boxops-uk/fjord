// The delegate types every event in this project is an event OF (21.2). They are the
// project's own rather than `System.EventHandler` so that the event declarations and the
// delegate declarations sit in one index and can be joined: a delegate type IS expressible
// (`CsharpEntities` has a `csharp.Delegate`), an event is not, and the contrast is the
// point.
namespace Surface.Synthesised.Events;

/// <summary>A gauge reported a reading. The non-generic handler for this project.</summary>
/// <param name="reading">The value the gauge reported.</param>
public delegate void EvReadingHandler(int reading);

/// <summary>
/// A labelled payload arrived. The generic handler, so at least one event in this project
/// has a CONSTRUCTED generic delegate type: `EvPayloadHandler&lt;string&gt;` is a different
/// type symbol from the declaration `EvPayloadHandler&lt;TPayload&gt;`, and only the
/// declaration has any syntax.
/// </summary>
/// <param name="payload">The payload that arrived.</param>
public delegate void EvPayloadHandler<TPayload>(TPayload payload);
