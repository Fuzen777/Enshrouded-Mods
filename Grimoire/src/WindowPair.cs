using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Kiln;
using static Grimoire.WindowPairRules;

namespace Grimoire;

/// <summary>
/// Keeps this mod's window and the partner mod's window together (Grimoire and Better Spells, asked 26 Nov 2026). The same file is in
/// both mods; only the namespace differs.
/// <list type="bullet">
/// <item>Each mod tells the other where its window is while it is drawn (<c>&lt;own id&gt;.window</c>: left, top, right, bottom as fractions
/// of the viewport, then the seconds since it opened and since the player last moved or resized it, -1 for never), when that changes and
/// every half second, and null when it closes. A window not heard from for 1.5 s counts as closed.</item>
/// <item>A window that opens while the other one is open goes beside it, on its preferred side (Grimoire on the right, Better Spells on the
/// left) when it fits there.</item>
/// <item>Both opened by one key press: the leader (Better Spells) stays where it is and the other one goes beside it, whichever opened
/// first.</item>
/// <item>While the two touch side by side they are docked: moving or resizing one moves the other with it. Hold Shift while dragging to
/// move one alone. Of two docked windows that no longer touch, the one the player moved last stays and the other follows it (both
/// never moved: the leader stays), so the two never chase each other. A window dropped within a few pixels of the other one's side snaps
/// to it and docks.</item>
/// </list>
/// A place the window was given beside the other one is not the player's own (WindowPlacement.SetBeside): opened alone, the window goes
/// back to where the player left it. Moving a window needs Kiln's UiWindow.MoveTo (0.2.0-dev.31.11, mod API 0.2.20): on an older Kiln the
/// windows open where they were left, as before.
/// </summary>
internal sealed class WindowPair : IDisposable
{
    // The partner sends every 500 ms while its window is drawn: three missed messages mean it is closed (or its mod is gone).
    private const long StaleMilliseconds = 1500;
    private const long RepeatMilliseconds = 500;
    // Opened this soon after the partner (or before it): one key press opened both.
    private const long TogetherMilliseconds = 500;
    // How long after it opens a follower still goes beside a partner it only now hears of (the partner's first message can take a frame
    // or a tick to come).
    private const long JoinMilliseconds = 1000;
    // A window that moved by itself (the player dragging or resizing it) within this long is the one being moved: it does not follow.
    private const long SelfMoveMilliseconds = 250;
    // Frames after our own MoveTo in which a change of the rect is ours, not the player's.
    private const int OwnMoveFrames = 3;
    // Shift held: a press or auto-repeat seen this recently (a release lost to alt-tab must not leave it held).
    private const long ShiftMilliseconds = 1000;
    // In pixels at UI scale 1.
    private const float DockTolerance = 3f;
    private const float SnapDistance = 16f;

    // MovedAt: when the player last moved or resized it (0: never).
    private sealed record Partner(Rect Edges, long OpenedAt, long MovedAt, long At);

    private readonly object _gate = new();
    private readonly Logger _log;
    private readonly string _partnerId;
    private readonly string _ownMethod;
    private readonly bool _leader;
    private readonly bool _preferRight;
    private readonly UiWindow _window;
    private readonly WindowPlacement _placement;
    private readonly IDisposable _subscription;
    private readonly KeyListener? _shiftListener;

    private volatile Partner? _partner;
    private volatile bool _canMove = true;
    private long _shiftAt;
    private volatile bool _shiftDown;

    // Render thread (and Opening, under _gate).
    private long _openedAt;
    private bool _joinPending;
    private Rect? _last;           // pixels
    private Rect? _lastFractions;  // the last drawn rect as fractions of the viewport (where ImGui shows it again)
    private int _ownMoveFrames;
    private long _selfMovedAt;     // when the player last moved or resized this window (0: never)
    private bool _dragging;
    private bool _wasActive;
    private bool _docked;
    private bool _onRight;
    private float _offsetY;        // pixels
    private float[]? _told;
    private long _toldAt;

    /// <param name="leader">Stays put when both windows open together (the partner goes beside it).</param>
    /// <param name="preferRight">The side of the partner's window this window goes on when it fits there.</param>
    public WindowPair(ModContext context, Logger log, string partnerId, bool leader, bool preferRight, UiWindow window, WindowPlacement placement)
    {
        _log = log;
        _partnerId = partnerId;
        _ownMethod = context.Info.Id + ".window";
        _leader = leader;
        _preferRight = preferRight;
        _window = window;
        _placement = placement;
        _subscription = context.Messages.On(partnerId + ".window", OnMessage);
        try
        {
            _shiftListener = KeyInput.Listen(OnKey);
        }
        catch (Exception error) when (error is MissingMethodException or TypeLoadException or InvalidOperationException)
        {
            _log.Info($"Shift can't be read here ({error.Message}): docked windows always move together");
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _shiftListener?.Dispose();
    }

    // The game's window thread: follows Shift (either one). Never keeps the key from the game.
    private bool OnKey(KeyPress press)
    {
        if (press.VirtualKey is 0x10 or 0xA0 or 0xA1)
        {
            _shiftDown = press.Down;
            Volatile.Write(ref _shiftAt, Environment.TickCount64);
        }
        return false;
    }

    private bool ShiftHeld => _shiftDown && Environment.TickCount64 - Volatile.Read(ref _shiftAt) < ShiftMilliseconds;

    // Tick thread: the partner's window, or null when it closed.
    private void OnMessage(ImcMessage message)
    {
        if (message.Sender != _partnerId)
        {
            return;
        }
        float[]? rect = message.PayloadAs<float[]>();
        if (rect is not { Length: >= 4 } || !Array.TrueForAll(rect, float.IsFinite) || rect[2] <= rect[0] || rect[3] <= rect[1])
        {
            _partner = null;
            return;
        }
        long now = Environment.TickCount64;
        // Better Spells 1.1.1 and 1.1.2 sent no age: taken as open for long.
        long age = rect.Length >= 5 ? (long)(Math.Max(rect[4], 0f) * 1000f) : long.MaxValue / 4;
        Partner? before = _partner;
        // The same opening keeps its time (the age rounds differently from one message to the next).
        long openedAt = before != null && now - before.At <= StaleMilliseconds && Math.Abs(before.OpenedAt - (now - age)) < 250 ? before.OpenedAt : now - age;
        long movedAt = rect.Length >= 6 && rect[5] >= 0f ? Math.Max(now - (long)(rect[5] * 1000f), 1L) : 0L;
        _partner = new Partner(new Rect(rect[0], rect[1], rect[2], rect[3]), openedAt, movedAt, now);
    }

    private Partner? FreshPartner(long now)
    {
        Partner? partner = _partner;
        return partner != null && now - partner.At <= StaleMilliseconds ? partner : null;
    }

    /// <summary>
    /// Called right before the window is shown (any thread): beside the partner's window when that is open (for the leader, only when it
    /// was not opened by the same key press), else where the player left it.
    /// </summary>
    public void Opening()
    {
        lock (_gate)
        {
            long now = Environment.TickCount64;
            _openedAt = now;
            _joinPending = !_leader;
            _docked = false;
            _last = null;
            _ownMoveFrames = 0;
            _dragging = false;
            _wasActive = false;
            _told = null;
            Partner? partner = FreshPartner(now);
            if (partner != null && (!_leader || now - partner.OpenedAt > TogetherMilliseconds) && MoveBeside(partner.Edges))
            {
                _joinPending = false;
                _placement.SetBeside(true);
                return;
            }
            Rect? shown = _lastFractions;
            if (_placement.Beside)
            {
                // Opened beside the partner last time: back to the player's own place.
                (float x, float y) = _placement.Center;
                Move(x, y, 0.5f, 0.5f);
                shown = shown is { } last ? new Rect(x - last.Width / 2f, y - last.Height / 2f, x + last.Width / 2f, y + last.Height / 2f) : null;
            }
            _placement.SetBeside(false);
            if (_leader && shown is { } edges)
            {
                // Tells a partner opened by the same key where this window is going, before its first frame.
                Tell(edges, now);
            }
        }
    }

    /// <summary>The window closed (any thread).</summary>
    public void Closed()
    {
        lock (_gate)
        {
            _docked = false;
            _joinPending = false;
            _last = null;
            if (_told == null)
            {
                return;
            }
            _told = null;
        }
        if (Mods.IsLoaded(_partnerId))
        {
            Mods.Send(_partnerId, _ownMethod, null);
        }
    }

    /// <summary>Inside the window's draw callback (render thread), every frame.</summary>
    public void Track(UiDraw ui)
    {
        if (ui.Width <= 0 || ui.Height <= 0)
        {
            return;
        }
        float viewWidth = ui.Width;
        float viewHeight = ui.Height;
        float scale = ui.Scale > 0 ? ui.Scale : 1f;
        (float x, float y, float w, float h) = ui.WindowRect;
        var me = new Rect(x, y, x + w, y + h);
        bool buttonHeld = ui.ButtonHeld(UiMouseButton.Left);
        lock (_gate)
        {
            long now = Environment.TickCount64;
            bool ours = _ownMoveFrames > 0;
            if (ours)
            {
                _ownMoveFrames--;
            }
            if (_last is { } last && !Same(last, me) && !ours)
            {
                // Moved or resized by the player.
                _selfMovedAt = now;
                _dragging = buttonHeld;
            }
            if (!buttonHeld)
            {
                _dragging = false;
            }
            bool active = _dragging || (_selfMovedAt != 0 && now - _selfMovedAt < SelfMoveMilliseconds);
            _last = me;
            var fractions = new Rect(me.Left / viewWidth, me.Top / viewHeight, me.Right / viewWidth, me.Bottom / viewHeight);
            _lastFractions = fractions;
            Tell(fractions, now);

            Partner? partner = FreshPartner(now);
            if (partner == null)
            {
                _docked = false;
                _wasActive = active;
                return;
            }
            Rect other = new(partner.Edges.Left * viewWidth, partner.Edges.Top * viewHeight, partner.Edges.Right * viewWidth, partner.Edges.Bottom * viewHeight);
            if (_joinPending)
            {
                _joinPending = false;
                if (!active && now - _openedAt <= JoinMilliseconds && MoveBeside(partner.Edges))
                {
                    // Opened by the same key press as the partner, whose window was not known yet.
                    _placement.SetBeside(true);
                    _wasActive = active;
                    return;
                }
            }
            if (Adjacent(me, other, DockTolerance * scale))
            {
                _docked = true;
                _onRight = OnRight(me, other);
                _offsetY = me.Top - other.Top;
            }
            else if (_docked)
            {
                if (ShiftHeld)
                {
                    // Shift: this one or the partner moves alone.
                    _docked = false;
                }
                else if (!active && PartnerLeads(partner))
                {
                    // The partner moved or was resized: follow it. Not the player's own place.
                    (float left, float top) = Docked(other, me.Width, _onRight, _offsetY);
                    if (Move(left / viewWidth, top / viewHeight, 0f, 0f))
                    {
                        _placement.SetBeside(true);
                    }
                }
            }
            else if (_wasActive && !active && Snap(me, other, SnapDistance * scale) is { } snapped)
            {
                // Dropped near the partner's side: against it (the player's own place, docked from the next frame).
                Move(snapped.Left / viewWidth, snapped.Top / viewHeight, 0f, 0f);
            }
            _wasActive = active;
        }
    }

    // Of two docked windows apart, the one the player moved last stays and the other follows; never moved either: the leader stays.
    private bool PartnerLeads(Partner partner) =>
        partner.MovedAt != 0 || _selfMovedAt != 0 ? partner.MovedAt > _selfMovedAt : !_leader;

    // Beside the partner's window, tops level. Under _gate.
    private bool MoveBeside(Rect partner)
    {
        float width = _lastFractions?.Width ?? 0f;
        bool right = BesideOnRight(partner.Left, partner.Right, width, _preferRight);
        return Move(right ? partner.Right : partner.Left, partner.Top, right ? 0f : 1f, 0f);
    }

    // Sends the window's edges when they changed or every half second. Under _gate.
    private void Tell(Rect fractions, long now)
    {
        float moved = _selfMovedAt == 0 ? -1f : (now - _selfMovedAt) / 1000f;
        float[] edges = { fractions.Left, fractions.Top, fractions.Right, fractions.Bottom, (now - _openedAt) / 1000f, moved };
        if (_told != null && SameEdges(_told, edges) && now - _toldAt < RepeatMilliseconds)
        {
            return;
        }
        _told = edges;
        _toldAt = now;
        if (Mods.IsLoaded(_partnerId))
        {
            Mods.Send(_partnerId, _ownMethod, edges);
        }
    }

    // Moves the window (applied at its next frame); false on a Kiln without window moves. Under _gate.
    private bool Move(float x, float y, float pivotX, float pivotY)
    {
        if (!_canMove || !float.IsFinite(x) || !float.IsFinite(y))
        {
            return false;
        }
        try
        {
            MoveTo(_window, x, y, pivotX, pivotY);
            _ownMoveFrames = OwnMoveFrames;
            return true;
        }
        catch (MissingMethodException)
        {
            // Kiln before 0.2.0-dev.31.11 (mod API 0.2.20): UiWindow.MoveTo does not exist.
            _canMove = false;
            _log.Info($"this Kiln ({Mods.RuntimeApiVersion}) cannot move windows (mod API 0.2.20 can): the window opens where you left it");
            return false;
        }
    }

    // Kept apart so that a Kiln without UiWindow.MoveTo fails here, at its first call, where Move catches it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MoveTo(UiWindow window, float x, float y, float pivotX, float pivotY) => window.MoveTo(x, y, pivotX, pivotY);

    private static bool Same(Rect a, Rect b) =>
        MathF.Abs(a.Left - b.Left) < 0.5f && MathF.Abs(a.Top - b.Top) < 0.5f && MathF.Abs(a.Right - b.Right) < 0.5f && MathF.Abs(a.Bottom - b.Bottom) < 0.5f;

    // The ages (4 and 5) are left out: they grow every frame.
    private static bool SameEdges(float[] a, float[] b)
    {
        for (int i = 0; i < 4; i++)
        {
            if (MathF.Abs(a[i] - b[i]) >= 0.0005f)
            {
                return false;
            }
        }
        return true;
    }
}
