using Kiln;

namespace Grimoire;

/// <summary>
/// One kind of ammunition the grimoire keeps: its storage, the equipment slot it fills, the HUD list it orders and how its items are
/// recognised. Spells today; arrows are the same book with the Arrow values (equipment slot type 6, EquipmentSlot.Arrow).
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

    public bool Holds(ItemId item) => Items.Get(item) is { } info && info.Category == ItemCategory.Ammunition && info.EquipSlot == EquipSlotType;
}
