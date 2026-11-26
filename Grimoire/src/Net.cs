using System;
using System.Collections.Generic;
using Kiln;

namespace Grimoire;

// The grimoire's channel between a player's game and the server that runs the world (1.0.2). A dedicated server, or another player's
// hosted world, keeps the player's storage, character data and inventory, so every step runs there; the player's game draws the screen
// and the HUD lists from the views the server sends back and asks for steps with commands. In a world the player hosts, the books call
// themselves directly and nothing goes over the channel.

/// <summary>What a book's command asks the server to do.</summary>
internal enum BookOp : byte
{
    Refresh = 0,
    Pick = 1,
    Add = 2,
    TakeOut = 3,
    AddAll = 4,
    TakeAllOut = 5,
    Move = 6,
    AddAt = 7,
}

/// <summary>Player to server: the player's settings that the server's steps follow (Mod Enabled and each book's slots).</summary>
internal sealed record PrefsMessage(bool Enabled, int SpellSlots, int ArrowSlots) : INetMessage<PrefsMessage>
{
    public void Encode(NetWriter w) => w.Bool(Enabled).VarInt(SpellSlots).VarInt(ArrowSlots);

    public static PrefsMessage Decode(ref NetReader r) => new(r.Bool(), (int)r.VarInt(), (int)r.VarInt());
}

/// <summary>Player to server: a step for one book (0 spells, 1 arrows) on an item, with an entry index for Move and AddAt.</summary>
internal sealed record CommandMessage(byte Book, BookOp Op, uint Item, int Index) : INetMessage<CommandMessage>
{
    public void Encode(NetWriter w) => w.Byte(Book).Byte((byte)Op).UInt32(Item).VarInt(Index);

    public static CommandMessage Decode(ref NetReader r) => new(r.Byte(), (BookOp)r.Byte(), r.UInt32(), (int)r.VarInt());
}

/// <summary>Server to player: a toast for that player's game.</summary>
internal sealed record ToastMessage(string Text, byte Kind, float Seconds) : INetMessage<ToastMessage>
{
    public void Encode(NetWriter w) => w.String(Text).Byte(Kind).Single(Seconds);

    public static ToastMessage Decode(ref NetReader r) => new(r.String(512), r.Byte(), r.Single());
}

/// <summary>Server to player: what one book holds for that player, as the server last read it. Names are the player's game's own.</summary>
internal sealed record ViewMessage(byte Book, string? NotReady, uint Equipped, int EquippedCount, uint OwnEquipped,
    ViewMessage.EntryRow[] Entries, ViewMessage.BagRow[] Bag) : INetMessage<ViewMessage>
{
    public const int MaxRows = 64;

    public readonly record struct EntryRow(uint Item, int Stored, bool Equipped, int EquippedCount);

    public readonly record struct BagRow(uint Item, int Count, bool Equipped);

    public void Encode(NetWriter w)
    {
        w.Byte(Book).Bool(NotReady != null).String(NotReady ?? "").UInt32(Equipped).VarInt(EquippedCount).UInt32(OwnEquipped);
        w.VarUInt((ulong)Math.Min(Entries.Length, MaxRows));
        for (int i = 0; i < Math.Min(Entries.Length, MaxRows); i++)
        {
            w.UInt32(Entries[i].Item).VarInt(Entries[i].Stored).Bool(Entries[i].Equipped).VarInt(Entries[i].EquippedCount);
        }
        w.VarUInt((ulong)Math.Min(Bag.Length, MaxRows));
        for (int i = 0; i < Math.Min(Bag.Length, MaxRows); i++)
        {
            w.UInt32(Bag[i].Item).VarInt(Bag[i].Count).Bool(Bag[i].Equipped);
        }
    }

    public static ViewMessage Decode(ref NetReader r)
    {
        byte book = r.Byte();
        bool hasNotReady = r.Bool();
        string notReady = r.String(256);
        uint equipped = r.UInt32();
        int equippedCount = (int)r.VarInt();
        uint ownEquipped = r.UInt32();
        int entryCount = (int)Math.Min(r.VarUInt(), MaxRows);
        var entries = new EntryRow[entryCount];
        for (int i = 0; i < entryCount; i++)
        {
            entries[i] = new EntryRow(r.UInt32(), (int)r.VarInt(), r.Bool(), (int)r.VarInt());
        }
        int bagCount = (int)Math.Min(r.VarUInt(), MaxRows);
        var bag = new BagRow[bagCount];
        for (int i = 0; i < bagCount; i++)
        {
            bag[i] = new BagRow(r.UInt32(), (int)r.VarInt(), r.Bool());
        }
        return new ViewMessage(book, hasNotReady ? notReady : null, equipped, equippedCount, ownEquipped, entries, bag);
    }
}

/// <summary>
/// Where a book's steps run and where its views and toasts go. The server half: commands from players' games come in on the tick thread
/// with the sender's player (never a player id from the message). The player's half: the views and toasts the server sends.
/// </summary>
internal sealed class GrimoireNet : IDisposable
{
    public const string ChannelName = "grimoire:net";

    private readonly Mod _mod;
    private readonly NetChannel? _channel;
    private readonly object _prefsGate = new object();
    private readonly Dictionary<EntityId, PrefsMessage> _prefs = new Dictionary<EntityId, PrefsMessage>();
    private Action<CommandMessage, EntityId>? _onCommand;
    private Action<ViewMessage>? _onView;

    public GrimoireNet(Mod mod)
    {
        _mod = mod;
        try
        {
            // Not required: a player without Grimoire joins as before, and only misses the grimoire.
            _channel = Network.Channel(ChannelName, "1", required: false)
                .Register<PrefsMessage>(OnPrefs, NetDirection.ToServer)
                .Register<CommandMessage>(OnCommandMessage, NetDirection.ToServer)
                .Register<ViewMessage>(OnViewMessage, NetDirection.ToClient)
                .Register<ToastMessage>(OnToastMessage, NetDirection.ToClient);
        }
        catch (Exception error) when (error is KilnUnavailableException or InvalidOperationException)
        {
            _mod.Context.Log.Warn($"no channel {ChannelName}: {error.Message}; the grimoire works only in a world you host");
        }
    }

    private Logger Log => _mod.Context.Log;

    /// <summary>True where the world runs: a dedicated server, or a game hosting its world (single player too). A game joined to another's
    /// world has no world of its own: its steps go to the server.</summary>
    public static bool WorldHere => !Client.Available || !Client.IsClient || Client.IsHosting;

    /// <summary>The player this game plays, when the world runs here (the host), else none.</summary>
    public static bool IsLocal(EntityId player) => Client.Available && Client.IsHosting && !player.IsNone && player == Client.LocalPlayer;

    /// <summary>Server half: whether this player's game runs Grimoire (the host's own game, or a joined game with the channel). The steps
    /// leave everyone else alone: a player who removed Grimoire from their game keeps what is in their backpack.</summary>
    public static bool Serves(EntityId player) =>
        !player.IsNone && (IsLocal(player) || Network.PeerOf(player)?.HasChannel(ChannelName) == true);

    /// <summary>On a joined game: whether the server runs Grimoire (has the channel).</summary>
    public bool ServerHasGrimoire => _channel != null && Network.ServerHasKiln && Network.Server?.HasChannel(ChannelName) == true;

    public void Handle(Action<CommandMessage, EntityId> onCommand, Action<ViewMessage> onView)
    {
        _onCommand = onCommand;
        _onView = onView;
    }

    // Player's half --------------------------------------------------------------------------------------------------------

    public bool SendCommand(CommandMessage command) => _channel?.SendToServer(command) == true;

    public bool SendPrefs(PrefsMessage prefs) => _channel?.SendToServer(prefs) == true;

    private void OnViewMessage(ViewMessage view, NetContext ctx) => _onView?.Invoke(view);

    private void OnToastMessage(ToastMessage toast, NetContext ctx) =>
        Hud.Toast(toast.Text, Enum.IsDefined((ToastKind)toast.Kind) ? (ToastKind)toast.Kind : ToastKind.Info, Math.Clamp(toast.Seconds, 1f, 10f));

    // Server half ---------------------------------------------------------------------------------------------------------

    private void OnPrefs(PrefsMessage prefs, NetContext ctx)
    {
        if (ctx.Player.IsNone)
        {
            return;
        }
        lock (_prefsGate)
        {
            _prefs[ctx.Player] = prefs;
        }
        Log.Info($"settings of {ctx.Sender.Name}: {(prefs.Enabled ? "on" : "off")}, {GrimoireRules.ClampSlots(prefs.SpellSlots)} spell slots, {GrimoireRules.ClampSlots(prefs.ArrowSlots)} arrow slots");
    }

    private void OnCommandMessage(CommandMessage command, NetContext ctx)
    {
        if (!ctx.Player.IsNone)
        {
            _onCommand?.Invoke(command, ctx.Player);
        }
    }

    /// <summary>A joined player's settings as their game last sent them, or null (not sent yet: the defaults apply).</summary>
    public PrefsMessage? PrefsOf(EntityId player)
    {
        lock (_prefsGate)
        {
            return _prefs.TryGetValue(player, out PrefsMessage? prefs) ? prefs : null;
        }
    }

    public void Forget(EntityId player)
    {
        lock (_prefsGate)
        {
            _prefs.Remove(player);
        }
    }

    /// <summary>Sends a view to a joined player's game (tick thread). False when that game has no Grimoire.</summary>
    public bool SendView(EntityId player, ViewMessage view) => _channel?.SendTo(player, view) == true;

    public bool SendToast(EntityId player, string text, ToastKind kind, double seconds) =>
        _channel?.SendTo(player, new ToastMessage(text, (byte)kind, (float)seconds)) == true;

    public void Dispose()
    {
        _onCommand = null;
        _onView = null;
    }
}
