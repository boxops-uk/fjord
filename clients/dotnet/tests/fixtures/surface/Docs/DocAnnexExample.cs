// Annex D.5 (an example), D.5.1 (C# source code) and D.5.2 (resulting XML) — the annex's own
// worked example, transliterated. The annex writes it as `Graphics.Point`; here it is
// `Surface.Docs.Graphics.DocAnnexPoint`, because twenty-two projects are indexed in one run
// and every one of them wants to call something `Point`. Nothing else about it is changed:
// the tags, their order, the parameter names (`xor`, `yor`, `dx`, `dy`, `o`, `p1`, `p2`) and
// the crefs are the annex's, including the ones the annex writes without a signature.
//
// It is here as a whole rather than as pieces because D.5 is the annex's only statement about
// how the tags compose: a `<summary>` containing a `<see>`, an `<example>` containing a
// `<code>`, a `<param>` referenced by a `<paramref>` in the same comment, and a `<seealso>`
// naming an operator. Every construct in this project appears somewhere else in a smaller
// form; only this file has them arranged the way a reader would actually write them.
//
// D.5.1's hazards are the ones the annex walked into by accident, and they are the useful
// part:
//
//   * Every cref in the annex's example is written **without a signature**: `<see
//     cref="Translate"/>`, `<seealso cref="Equals"/>`, `<seealso cref="operator=="/>`. All of
//     them resolve here — to `M:…DocAnnexPoint.Translate(System.Int32,System.Int32)`,
//     `M:…DocAnnexPoint.Equals(System.Object)` and
//     `M:…DocAnnexPoint.op_Equality(…DocAnnexPoint,…DocAnnexPoint)` — with no warning, because
//     each name has exactly one candidate in this type. They resolve to a *fully signed* ID
//     string, so the reference in the file is more precise than the reference in the source,
//     and nothing in the file records that the author did not say which overload they meant.
//     Add one overload of any of them and the same source text becomes CS0419 and silently
//     keeps pointing somewhere: `DocCrefForms.OverloadSet` in DocCrefs.cs is that case.
//   * A constructor and its type are both written `DocAnnexPoint`, four lines apart, one
//     documented `<summary>This constructor initializes…</summary>` and the other
//     `<summary>Class <c>DocAnnexPoint</c> models…</summary>`.
//   * `X` and `Y` are documented with `<value>` and no `<summary>`, so their entries in the
//     documentation file have no summary at all — which is the shape a hover has to survive.
//
// D.5.2 is the XML the annex prints for this source. The block at the end of this file is the
// XML *this* compiler actually emitted for these declarations, copied out of the emitted
// `Docs.xml` rather than transcribed from the annex, so the two can be compared.

namespace Surface.Docs.Graphics;

/// <summary>
/// Class <c>DocAnnexPoint</c> models a point in a two-dimensional plane.
/// </summary>
public class DocAnnexPoint
{
    /// <summary>
    /// Instance variable <c>x</c> represents the point's x-coordinate.
    /// </summary>
    private int x;

    /// <summary>
    /// Instance variable <c>y</c> represents the point's y-coordinate.
    /// </summary>
    private int y;

    /// <value>
    /// Property <c>X</c> represents the point's x-coordinate.
    /// </value>
    public int X
    {
        get { return x; }
        set { x = value; }
    }

    /// <value>
    /// Property <c>Y</c> represents the point's y-coordinate.
    /// </value>
    public int Y
    {
        get { return y; }
        set { y = value; }
    }

    /// <summary>
    /// This constructor initializes the new DocAnnexPoint to (0,0).
    /// </summary>
    public DocAnnexPoint()
        : this(0, 0)
    {
    }

    /// <summary>
    /// This constructor initializes the new DocAnnexPoint to
    /// (<paramref name="xor"/>,<paramref name="yor"/>).
    /// </summary>
    /// <param name="xor">the new DocAnnexPoint's x-coordinate.</param>
    /// <param name="yor">the new DocAnnexPoint's y-coordinate.</param>
    public DocAnnexPoint(int xor, int yor)
    {
        X = xor;
        Y = yor;
    }

    /// <summary>
    /// This method changes the point's location to the given coordinates.
    /// <see cref="Translate"/>
    /// </summary>
    /// <param name="xor">the new x-coordinate.</param>
    /// <param name="yor">the new y-coordinate.</param>
    public void Move(int xor, int yor)
    {
        X = xor;
        Y = yor;
    }

    /// <summary>
    /// This method changes the point's location by the given x- and y-offsets.
    /// <example>
    /// For example:
    /// <code>
    /// DocAnnexPoint p = new DocAnnexPoint(3, 5);
    /// p.Translate(-1, 8);
    /// </code>
    /// results in <c>p</c>'s having the value (2,13).
    /// </example>
    /// <see cref="Move"/>
    /// </summary>
    /// <param name="dx">the relative x-offset.</param>
    /// <param name="dy">the relative y-offset.</param>
    public void Translate(int dx, int dy)
    {
        X += dx;
        Y += dy;
    }

    /// <summary>
    /// This method determines whether two DocAnnexPoints have the same location.
    /// </summary>
    /// <param name="o">the object to be compared to the current object.</param>
    /// <returns>
    /// true if the DocAnnexPoints have the same location and they have the exact same type;
    /// false otherwise.
    /// </returns>
    /// <seealso cref="operator=="/>
    /// <seealso cref="operator!="/>
    public override bool Equals(object o)
    {
        if (o == null || GetType() != o.GetType())
        {
            return false;
        }

        DocAnnexPoint p = (DocAnnexPoint)o;
        return X == p.X && Y == p.Y;
    }

    /// <summary>
    /// Report a point's location as a string.
    /// </summary>
    /// <returns>
    /// A string representing a point's location, in the form (x,y), without any leading,
    /// trailing, or embedded whitespace.
    /// </returns>
    public override string ToString() => "(" + X + "," + Y + ")";

    /// <summary>
    /// This operator determines whether two DocAnnexPoints have the same location.
    /// </summary>
    /// <param name="p1">the first DocAnnexPoint to be compared.</param>
    /// <param name="p2">the second DocAnnexPoint to be compared.</param>
    /// <returns>
    /// true if the DocAnnexPoints have the same location and they have the exact same type;
    /// false otherwise.
    /// </returns>
    /// <seealso cref="Equals"/>
    /// <seealso cref="operator!="/>
    public static bool operator ==(DocAnnexPoint p1, DocAnnexPoint p2)
    {
        if ((object)p1 == null || (object)p2 == null)
        {
            return (object)p1 == (object)p2;
        }

        return p1.X == p2.X && p1.Y == p2.Y;
    }

    /// <summary>
    /// This operator determines whether two DocAnnexPoints have different locations.
    /// </summary>
    /// <param name="p1">the first DocAnnexPoint to be compared.</param>
    /// <param name="p2">the second DocAnnexPoint to be compared.</param>
    /// <returns>
    /// true if the DocAnnexPoints have different locations and they have the exact same type;
    /// false otherwise.
    /// </returns>
    /// <seealso cref="Equals"/>
    /// <seealso cref="operator=="/>
    public static bool operator !=(DocAnnexPoint p1, DocAnnexPoint p2) => !(p1 == p2);

    /// <summary>
    /// This is the entry point of the DocAnnexPoint class testing program.
    /// <para>
    /// This program tests each method and operator, and is intended to be run after any
    /// non-trivial maintenance has been performed on the DocAnnexPoint class.
    /// </para>
    /// </summary>
    /// <returns>
    /// Zero when every check passed. The annex's version is a <c>void Main</c>; this one
    /// returns a result instead, because a fixture that is indexed rather than run has no
    /// other way to say so, and because a second entry point in a corpus of twenty-two
    /// projects is a build error rather than a fact.
    /// </returns>
    public static int Main()
    {
        DocAnnexPoint p = new DocAnnexPoint(3, 5);
        p.Translate(-1, 8);

        if (p.ToString() != "(2,13)")
        {
            return 1;
        }

        p.Move(0, 0);
        return p.Equals(new DocAnnexPoint(0, 0)) && p == new DocAnnexPoint(0, 0) ? 0 : 2;
    }
}

// ---------------------------------------------------------------------------------------
// Annex D.5.2 — resulting XML.
//
// What follows is the `<members>` content this compiler emitted for the declarations above,
// copied out of `bin/…/Docs.xml` after a build rather than transcribed from the annex, so
// that the annex's printed XML and a real compiler's can be compared line for line. Three
// things in it are the point of the whole file:
//
//   * **Every cref was rewritten.** The source says `<see cref="Translate"/>`; the file says
//     `<see cref="M:Surface.Docs.Graphics.DocAnnexPoint.Translate(System.Int32,System.Int32)"/>`.
//     The reference in the documentation file is more precise than the reference the author
//     wrote, and the file keeps no record of the imprecision.
//   * **`X` and `Y` have a `<value>` and no `<summary>`.** That is the annex's own choice and
//     it is legal; a consumer that expects a summary gets nothing for these two members.
//   * **`x` and `y` are in the file.** They are private. The generator emits an entry for
//     every documented declaration and the ID string records no accessibility, so nothing in
//     the documentation file distinguishes them from the public members beside them.
//
// <member name="T:Surface.Docs.Graphics.DocAnnexPoint">
//     <summary>
//     Class <c>DocAnnexPoint</c> models a point in a two-dimensional plane.
//     </summary>
// </member>
// <member name="F:Surface.Docs.Graphics.DocAnnexPoint.x">
//     <summary>
//     Instance variable <c>x</c> represents the point's x-coordinate.
//     </summary>
// </member>
// <member name="F:Surface.Docs.Graphics.DocAnnexPoint.y">
//     <summary>
//     Instance variable <c>y</c> represents the point's y-coordinate.
//     </summary>
// </member>
// <member name="P:Surface.Docs.Graphics.DocAnnexPoint.X">
//     <value>
//     Property <c>X</c> represents the point's x-coordinate.
//     </value>
// </member>
// <member name="P:Surface.Docs.Graphics.DocAnnexPoint.Y">
//     <value>
//     Property <c>Y</c> represents the point's y-coordinate.
//     </value>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.#ctor">
//     <summary>
//     This constructor initializes the new DocAnnexPoint to (0,0).
//     </summary>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.#ctor(System.Int32,System.Int32)">
//     <summary>
//     This constructor initializes the new DocAnnexPoint to
//     (<paramref name="xor"/>,<paramref name="yor"/>).
//     </summary>
//     <param name="xor">the new DocAnnexPoint's x-coordinate.</param>
//     <param name="yor">the new DocAnnexPoint's y-coordinate.</param>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.Move(System.Int32,System.Int32)">
//     <summary>
//     This method changes the point's location to the given coordinates.
//     <see cref="M:Surface.Docs.Graphics.DocAnnexPoint.Translate(System.Int32,System.Int32)"/>
//     </summary>
//     <param name="xor">the new x-coordinate.</param>
//     <param name="yor">the new y-coordinate.</param>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.Translate(System.Int32,System.Int32)">
//     <summary>
//     This method changes the point's location by the given x- and y-offsets.
//     <example>
//     For example:
//     <code>
//     DocAnnexPoint p = new DocAnnexPoint(3, 5);
//     p.Translate(-1, 8);
//     </code>
//     results in <c>p</c>'s having the value (2,13).
//     </example>
//     <see cref="M:Surface.Docs.Graphics.DocAnnexPoint.Move(System.Int32,System.Int32)"/>
//     </summary>
//     <param name="dx">the relative x-offset.</param>
//     <param name="dy">the relative y-offset.</param>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.Equals(System.Object)">
//     <summary>
//     This method determines whether two DocAnnexPoints have the same location.
//     </summary>
//     <param name="o">the object to be compared to the current object.</param>
//     <returns>
//     true if the DocAnnexPoints have the same location and they have the exact same type;
//     false otherwise.
//     </returns>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.op_Equality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)"/>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.op_Inequality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)"/>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.ToString">
//     <summary>
//     Report a point's location as a string.
//     </summary>
//     <returns>
//     A string representing a point's location, in the form (x,y), without any leading,
//     trailing, or embedded whitespace.
//     </returns>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.op_Equality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)">
//     <summary>
//     This operator determines whether two DocAnnexPoints have the same location.
//     </summary>
//     <param name="p1">the first DocAnnexPoint to be compared.</param>
//     <param name="p2">the second DocAnnexPoint to be compared.</param>
//     <returns>
//     true if the DocAnnexPoints have the same location and they have the exact same type;
//     false otherwise.
//     </returns>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.Equals(System.Object)"/>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.op_Inequality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)"/>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.op_Inequality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)">
//     <summary>
//     This operator determines whether two DocAnnexPoints have different locations.
//     </summary>
//     <param name="p1">the first DocAnnexPoint to be compared.</param>
//     <param name="p2">the second DocAnnexPoint to be compared.</param>
//     <returns>
//     true if the DocAnnexPoints have different locations and they have the exact same type;
//     false otherwise.
//     </returns>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.Equals(System.Object)"/>
//     <seealso cref="M:Surface.Docs.Graphics.DocAnnexPoint.op_Equality(Surface.Docs.Graphics.DocAnnexPoint,Surface.Docs.Graphics.DocAnnexPoint)"/>
// </member>
// <member name="M:Surface.Docs.Graphics.DocAnnexPoint.Main">
//     <summary>
//     This is the entry point of the DocAnnexPoint class testing program.
//     <para>
//     This program tests each method and operator, and is intended to be run after any
//     non-trivial maintenance has been performed on the DocAnnexPoint class.
//     </para>
//     </summary>
//     <returns>
//     Zero when every check passed. The annex's version is a <c>void Main</c>; this one
//     returns a result instead, because a fixture that is indexed rather than run has no
//     other way to say so, and because a second entry point in a corpus of twenty-two
//     projects is a build error rather than a fact.
//     </returns>
// </member>
// ---------------------------------------------------------------------------------------
