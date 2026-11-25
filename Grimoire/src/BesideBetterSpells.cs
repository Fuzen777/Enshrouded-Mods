using System;
using System.Runtime.CompilerServices;
using Kiln;

namespace Grimoire;

/// <summary>
/// Opens the grimoire's window beside Better Spells' settings window while that one is open (asked 25 Nov 2026, once Kiln 0.2.0-dev.31.11
/// let a mod move its window, #146). Better Spells (1.1.1+) sends its window's edges to Grimoire about twice a second while it is drawn, and
/// null when it closes; a window not heard from for a while counts as closed. Moving a window needs mod API 0.2.20: on an older Kiln the
/// grimoire opens where the player left it, as before.
/// </summary>
internal sealed class BesideBetterSpells : IDisposable
{
    public const string BetterSpellsId = "better-spells";
    public const string Method = "better-spells.window";
    // Better Spells sends every 500 ms while its window is drawn; three missed messages mean it is closed (or Better Spells is gone).
    private const long StaleMilliseconds = 1500;

    private sealed record Edges(float Left, float Top, float Right, float Bottom, long At);

    private readonly Logger _log;
    private readonly IDisposable _subscription;
    private volatile Edges? _edges;
    private volatile bool _canMove = true;

    public BesideBetterSpells(ModContext context, Logger log)
    {
        _log = log;
        _subscription = context.Messages.On(Method, OnMessage);
    }

    public void Dispose() => _subscription.Dispose();

    // Tick thread: [left, top, right, bottom] as fractions of the viewport, or null when Better Spells' window closed.
    private void OnMessage(ImcMessage message)
    {
        if (message.Sender != BetterSpellsId)
        {
            return;
        }
        float[]? rect = message.PayloadAs<float[]>();
        _edges = rect is { Length: 4 } && Array.TrueForAll(rect, float.IsFinite) && rect[2] > rect[0] && rect[3] > rect[1]
            ? new Edges(rect[0], rect[1], rect[2], rect[3], Environment.TickCount64)
            : null;
    }

    /// <summary>
    /// Called as the grimoire opens: moves it beside Better Spells' window when that is open, on the side with room for it (the right
    /// first), tops level. <paramref name="widthFraction"/> is the grimoire's width as a fraction of the viewport (0: not known yet).
    /// False when it was not moved.
    /// </summary>
    public bool Place(UiWindow window, float widthFraction)
    {
        Edges? edges = _edges;
        if (!_canMove || edges == null || Environment.TickCount64 - edges.At > StaleMilliseconds)
        {
            return false;
        }
        bool right = GrimoireRules.BesideOnRight(edges.Left, edges.Right, widthFraction);
        return Move(window, right ? edges.Right : edges.Left, edges.Top, right ? 0f : 1f, 0f);
    }

    /// <summary>Moves the window's centre to (x, y) for this open only; false on a Kiln without window moves.</summary>
    public bool MoveCenter(UiWindow window, float x, float y) => _canMove && Move(window, x, y, 0.5f, 0.5f);

    private bool Move(UiWindow window, float x, float y, float pivotX, float pivotY)
    {
        try
        {
            MoveTo(window, x, y, pivotX, pivotY);
            return true;
        }
        catch (MissingMethodException)
        {
            // Kiln before 0.2.0-dev.31.11 (mod API 0.2.20): UiWindow.MoveTo does not exist.
            _canMove = false;
            _log.Info($"this Kiln ({Mods.RuntimeApiVersion}) cannot move windows (mod API 0.2.20 can): the grimoire opens where you left it");
            return false;
        }
    }

    // Kept apart so that a Kiln without UiWindow.MoveTo fails here, at its first call, where Move catches it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MoveTo(UiWindow window, float x, float y, float pivotX, float pivotY) => window.MoveTo(x, y, pivotX, pivotY);
}
