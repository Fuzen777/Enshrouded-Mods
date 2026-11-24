using Kiln;

namespace Grimoire;

/// <summary>The settings: kiln config grimoire.json in the profile's config folder, used by the world's host while playing.</summary>
public sealed class GrimoireSettings
{
    public const string EnabledKey = "enabled";
    public const string SpellSlotsKey = "spellSlots";

    [ConfigKey(EnabledKey)]
    [Comment("Mod Enabled. Spells kept in the grimoire take no backpack slot, and the HUD's spell list follows the grimoire's order. Off: the game's own list; the grimoire keeps its spells. Works in a world you host.")]
    public bool Enabled { get; set; } = true;

    [ConfigKey(SpellSlotsKey)]
    [Comment("Spell Slots. How many spells the grimoire holds, one slot per spell; more of a spell already in it tops up its slot. Lowering it keeps the spells already in it.")]
    [Range(GrimoireRules.MinSlots, GrimoireRules.MaxSlots, 1)]
    public int SpellSlots { get; set; } = GrimoireRules.DefaultSlots;
}
