using Kiln;

namespace Grimoire;

/// <summary>
/// One kind of ammunition the grimoire keeps: its storage, the equipment slot it fills, the HUD list it orders and how its items are
/// recognised. Spells (equipment slot type 7) and arrows (6).
/// </summary>
internal sealed record BookKind(
    string Name,
    string Singular,
    string StorageKey,
    string OrderKey,
    EquipmentSlot Slot,
    PlayerEquipSlot PlayerSlot,
    AmmoListKind ListKind,
    AmmoKinds AmmoKind,
    byte EquipSlotType,
    string SlotsKey)
{
    // The game's EquipmentSlotType of a spell (ItemInfo.EquipSlot): Kiln docs/re/items.md section 16.
    public static readonly BookKind Spells = new BookKind("Spells", "Spell", "spells", "order-spells", EquipmentSlot.Spell, PlayerEquipSlot.Spell,
        AmmoListKind.Spell, AmmoKinds.Spell, 7, GrimoireSettings.SpellSlotsKey);

    public static readonly BookKind Arrows = new BookKind("Arrows", "Arrow", "arrows", "order-arrows", EquipmentSlot.Arrow, PlayerEquipSlot.Arrow,
        AmmoListKind.Arrow, AmmoKinds.Arrow, 6, GrimoireSettings.ArrowSlotsKey);

    public bool Holds(ItemId item) => Items.Get(item) is { } info && info.Category == ItemCategory.Ammunition && info.EquipSlot == EquipSlotType;
}
