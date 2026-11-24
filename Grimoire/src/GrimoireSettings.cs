using Kiln;

namespace Grimoire;

/// <summary>The settings: kiln config grimoire.json in the profile's config folder, used by the world's host while playing.</summary>
public sealed class GrimoireSettings
{
    public const string EnabledKey = "enabled";
    public const string SpellSlotsKey = "spellSlots";
    public const string ArrowSlotsKey = "arrowSlots";

    [ConfigKey(EnabledKey)]
    [Comment("Mod Enabled. Spells and arrows kept in the grimoire stay out of the backpack (the equipped one takes one slot), and the HUD's spell and arrow lists follow the grimoire's order. Off: the game's own lists; the grimoire keeps what it holds. Works in a world you host.")]
    public bool Enabled { get; set; } = true;

    [ConfigKey(SpellSlotsKey)]
    [Comment("Spell Slots. How many spells the grimoire holds, one slot per spell; more of a spell already in it tops up its slot. Lowering it keeps the spells already in it.")]
    [Range(GrimoireRules.MinSlots, GrimoireRules.MaxSlots, 1)]
    public int SpellSlots { get; set; } = GrimoireRules.DefaultSlots;

    [ConfigKey(ArrowSlotsKey)]
    [Comment("Arrow Slots. How many kinds of arrows the grimoire holds, one slot per kind; more of a kind already in it tops up its slot. Lowering it keeps the arrows already in it.")]
    [Range(GrimoireRules.MinSlots, GrimoireRules.MaxSlots, 1)]
    public int ArrowSlots { get; set; } = GrimoireRules.DefaultSlots;
}
