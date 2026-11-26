using Kiln;

namespace Grimoire;

/// <summary>The settings: kiln config grimoire.json in the profile's config folder, each player's own: a joined player's game sends them to the server, which runs the steps.</summary>
public sealed class GrimoireSettings
{
    public const string EnabledKey = "enabled";
    public const string SpellSlotsKey = "spellSlots";
    public const string ArrowSlotsKey = "arrowSlots";
    public const string HideOthersKey = "hideOthers";

    [ConfigKey(EnabledKey)]
    [Comment("Mod Enabled. Spells and arrows kept in the grimoire stay out of the backpack (the equipped one takes one slot), and the HUD's spell and arrow lists follow the grimoire's order. Off: the game's own lists; the grimoire keeps what it holds. In multiplayer the server needs Grimoire too.")]
    public bool Enabled { get; set; } = true;

    [ConfigKey(HideOthersKey)]
    [Comment("Hide Other Spells And Arrows. The HUD's spell and arrow lists show only what is in the grimoire (and the equipped one). Off: the other spells and arrows in the backpack follow the grimoire's.")]
    public bool HideOthers { get; set; } = true;

    [ConfigKey(SpellSlotsKey)]
    [Comment("Spell Slots. How many spells the grimoire holds, one slot per spell; more of a spell already in it tops up its slot. Lowering it keeps the spells already in it.")]
    [Range(GrimoireRules.MinSlots, GrimoireRules.MaxSlots, 1)]
    public int SpellSlots { get; set; } = GrimoireRules.DefaultSlots;

    [ConfigKey(ArrowSlotsKey)]
    [Comment("Arrow Slots. How many kinds of arrows the grimoire holds, one slot per kind; more of a kind already in it tops up its slot. Lowering it keeps the arrows already in it.")]
    [Range(GrimoireRules.MinSlots, GrimoireRules.MaxSlots, 1)]
    public int ArrowSlots { get; set; } = GrimoireRules.DefaultSlots;
}
