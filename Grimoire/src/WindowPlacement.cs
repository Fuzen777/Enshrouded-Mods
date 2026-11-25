using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kiln;

namespace Grimoire;

/// <summary>
/// Where the player last left the grimoire's window and how big it was, kept in the mod's data folder (kiln\data\grimoire\window.json)
/// so the window opens there at the next game start. A Kiln screen always opens centred with a fixed size, so the grimoire is a Kiln
/// window (Ui.Window): the player can move and resize it, ImGui keeps both while the game runs, and the window's first position and size
/// (UiWindowOptions.Position, Width, Height) come from this file.
/// Since 1.0.1 the window can also open beside Better Spells' window (BesideBetterSpells). That place is the other window's, not the
/// player's: it is not written unless the player moves the window from there (a resize there keeps the player's own place).
/// </summary>
internal sealed class WindowPlacement
{
    // A size or move counts as done once the window stays the same this long; then it is written.
    private const long SettleMilliseconds = 500;
    private const float MinWidth = 280f;
    private const float MinHeight = 360f;

    public sealed class Data
    {
        // The window's centre as a fraction of the screen (0..1), as UiWindowOptions.Position takes it.
        public float CenterX { get; set; } = 0.5f;
        public float CenterY { get; set; } = 0.5f;
        // Size in pixels at UI scale 1, as UiWindowOptions.Width and Height take it.
        public float Width { get; set; }
        public float Height { get; set; }
    }

    private readonly string _path;
    private readonly Logger _log;
    private Data _saved;
    private Data? _seen;
    private long _seenAt;
    private int _writing;
    // Opened beside Better Spells' window: the first settled rect is that place (not written); _besideReset asks Track to forget the last
    // one, as the window opens there again.
    private volatile bool _beside;
    private volatile bool _besideReset;
    private Data? _besidePlace;
    private (float Left, float Top) _besideCorner;
    private volatile float _widthFraction;

    public WindowPlacement(string directory, Logger log, float defaultWidth, float defaultHeight)
    {
        _path = Path.Combine(directory, "window.json");
        _log = log;
        _saved = new Data { Width = defaultWidth, Height = defaultHeight };
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<Data>(File.ReadAllText(_path)) is { } read)
            {
                _saved = Clamp(read, defaultWidth, defaultHeight);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Warn($"window placement not read ({_path}): {error.Message}");
        }
    }

    // The options the window is created with: the last saved centre and size.
    public UiWindowOptions Options(UiWindowOptions options) =>
        options with { Position = (_saved.CenterX, _saved.CenterY), Width = _saved.Width, Height = _saved.Height };

    // The window's width as a fraction of the viewport when it was drawn last; 0 before its first frame.
    public float WidthFraction => _widthFraction;

    // The player's own place: the centre last written (or read), as a fraction of the viewport.
    public (float X, float Y) Center => (_saved.CenterX, _saved.CenterY);

    // The window opens beside Better Spells' window (true) or at the player's own place (false). Any thread.
    public void SetBeside(bool beside)
    {
        _besideReset = true;
        _beside = beside;
    }

    public bool Beside => _beside;

    // Called inside the window's draw callback (render thread): follows the window's rect and writes it once it settles.
    public void Track(UiDraw ui)
    {
        if (ui.Width <= 0 || ui.Height <= 0 || ui.Scale <= 0)
        {
            return;
        }
        (float x, float y, float w, float h) = ui.WindowRect;
        _widthFraction = w / ui.Width;
        // The top left corner, as fractions: a resize keeps it, a move does not.
        (float left, float top) = (x / ui.Width, y / ui.Height);
        var now = new Data
        {
            CenterX = (x + w / 2f) / ui.Width,
            CenterY = (y + h / 2f) / ui.Height,
            Width = MathF.Round(w / ui.Scale),
            Height = MathF.Round(h / ui.Scale),
        };
        long time = Environment.TickCount64;
        if (_seen == null || !Same(_seen, now))
        {
            _seen = now;
            _seenAt = time;
            return;
        }
        if (time - _seenAt < SettleMilliseconds)
        {
            return;
        }
        if (_besideReset)
        {
            _besideReset = false;
            _besidePlace = null;
        }
        if (_beside)
        {
            if (_besidePlace == null)
            {
                // Where the window settled beside Better Spells' window: not the player's choice.
                _besidePlace = now;
                _besideCorner = (left, top);
                return;
            }
            if (Same(_besidePlace, now))
            {
                return;
            }
            if (MathF.Abs(_besideCorner.Left - left) < 0.0005f && MathF.Abs(_besideCorner.Top - top) < 0.0005f)
            {
                // Resized beside it: the new size, at the player's own place.
                var resized = new Data { CenterX = _saved.CenterX, CenterY = _saved.CenterY, Width = now.Width, Height = now.Height };
                if (Same(_saved, resized) || Save(resized))
                {
                    _saved = resized;
                    _besidePlace = now;
                }
                return;
            }
            // Moved away from it by the player: this is the player's place now.
            _beside = false;
        }
        if (Same(_saved, now))
        {
            return;
        }
        if (Save(now))
        {
            _saved = now;
        }
    }

    // Writes the file off the render thread; false while the previous write still runs (tried again at a later frame).
    private bool Save(Data data)
    {
        if (Interlocked.Exchange(ref _writing, 1) == 1)
        {
            return false;
        }
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        _ = Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, json);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                _log.Warn($"window placement not saved ({_path}): {error.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _writing, 0);
            }
        });
        return true;
    }

    private static bool Same(Data a, Data b) =>
        MathF.Abs(a.CenterX - b.CenterX) < 0.0005f && MathF.Abs(a.CenterY - b.CenterY) < 0.0005f && a.Width == b.Width && a.Height == b.Height;

    // A file from another screen or edited by hand: the centre stays on the screen and the size is at least the minimum.
    private static Data Clamp(Data data, float defaultWidth, float defaultHeight) => new Data
    {
        CenterX = float.IsFinite(data.CenterX) ? Math.Clamp(data.CenterX, 0.05f, 0.95f) : 0.5f,
        CenterY = float.IsFinite(data.CenterY) ? Math.Clamp(data.CenterY, 0.05f, 0.95f) : 0.5f,
        Width = float.IsFinite(data.Width) && data.Width > 0 ? Math.Clamp(data.Width, MinWidth, 4000f) : defaultWidth,
        Height = float.IsFinite(data.Height) && data.Height > 0 ? Math.Clamp(data.Height, MinHeight, 4000f) : defaultHeight,
    };
}
