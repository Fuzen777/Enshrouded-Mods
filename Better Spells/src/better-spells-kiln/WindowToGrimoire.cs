using System;
using Kiln;

namespace BetterSpells;

/// <summary>
/// Tells Grimoire where the settings window is, so that the grimoire opens beside it (1.1.1, asked 25 Nov 2026; Grimoire 1.0.1+ moves its
/// window with Kiln 0.2.0-dev.31.11's UiWindow.MoveTo). While the window is drawn it sends its edges, as fractions of the viewport
/// ([left, top, right, bottom]), when they change and every half second; null when it closes. Grimoire counts a window it has not heard
/// from for a while as closed, which covers every way of closing it. Nothing is sent when Grimoire is not loaded.
/// </summary>
internal sealed class WindowToGrimoire
{
    public const string GrimoireId = "grimoire";
    public const string Method = "better-spells.window";
    private const long RepeatMilliseconds = 500;

    private readonly object _gate = new();
    private float[]? _told;
    private long _toldAt;

    // Inside the window's draw callback (render thread).
    public void Track(UiDraw ui)
    {
        if (ui.Width <= 0 || ui.Height <= 0)
        {
            return;
        }
        (float x, float y, float w, float h) = ui.WindowRect;
        float[] edges = { x / ui.Width, y / ui.Height, (x + w) / ui.Width, (y + h) / ui.Height };
        long now = Environment.TickCount64;
        lock (_gate)
        {
            if (_told != null && Same(_told, edges) && now - _toldAt < RepeatMilliseconds)
            {
                return;
            }
            _told = edges;
            _toldAt = now;
        }
        if (Mods.IsLoaded(GrimoireId))
        {
            Mods.Send(GrimoireId, Method, edges);
        }
    }

    // The window closed (any thread).
    public void Closed()
    {
        lock (_gate)
        {
            if (_told == null)
            {
                return;
            }
            _told = null;
        }
        if (Mods.IsLoaded(GrimoireId))
        {
            Mods.Send(GrimoireId, Method, null);
        }
    }

    private static bool Same(float[] a, float[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (MathF.Abs(a[i] - b[i]) >= 0.0005f)
            {
                return false;
            }
        }
        return true;
    }
}
