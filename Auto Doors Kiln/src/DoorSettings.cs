using System.Collections.Generic;
using Kiln;

namespace AutoDoorsKiln;

/// <summary>The mod's settings: kiln config auto-doors-kiln.json, changed in the mod's settings window (Shift+F7), the Kiln menu
/// (Insert, Mods, Auto Doors) or by saving the file. Distances are whole numbers of decimetres so Kiln's sliders move in steps of 10 cm (Kiln draws a whole
/// number as a slider with steps and a decimal number as a free one). Every setting applies at once.</summary>
public sealed class DoorSettings
{
    [ConfigKey("enabled")]
    [Comment("Enabled. Doors open and close by themselves. Switched in the settings window (Shift+F7).")]
    public bool Enabled { get; set; } = true;

    [ConfigKey("openDistanceDm")]
    [Comment("Open Distance, in decimetres (40 = 4 m). A door opens when someone comes this close to it, measured along the ground.")]
    [Range(DoorRules.MinDistanceDm, DoorRules.MaxOpenDistanceDm)]
    public int OpenDistanceDm { get; set; } = 40;

    [ConfigKey("closeDistanceDm")]
    [Comment("Close Distance, in decimetres (46 = 4.6 m). A door closes once everyone is farther than this. Never less than the Open Distance.")]
    [Range(DoorRules.MinDistanceDm, DoorRules.MaxCloseDistanceDm)]
    public int CloseDistanceDm { get; set; } = 46;

    [ConfigKey("heightReachDm")]
    [Comment("Height Reach, in decimetres (25 = 2.5 m). Someone more than this far above or below a door (on another floor) neither opens it nor keeps it open.")]
    [Range(DoorRules.MinHeightReachDm, DoorRules.MaxHeightReachDm)]
    public int HeightReachDm { get; set; } = 25;

    [ConfigKey("closeDelaySeconds")]
    [Comment("Close Delay, in seconds. How long a door stays open after everyone has left. 0 closes it at once.")]
    [Range(0, 30)]
    public int CloseDelaySeconds { get; set; } = 0;

    [ConfigKey("onlyInsideBases")]
    [Comment("Only Inside Bases. Only doors inside a flame altar's area open by themselves. Off (Work Outside Bases in the settings window): every door in the world does; door modes, their key and the label stay for base doors.")]
    public bool OnlyInsideBases { get; set; } = true;

    [ConfigKey("openForPlayers")]
    [Comment("Open For Players.")]
    public bool OpenForPlayers { get; set; } = true;

    [ConfigKey("openForSurvivors")]
    [Comment("Open For Survivors. Survivors and workshop NPCs open doors as they walk about the base.")]
    public bool OpenForSurvivors { get; set; } = true;

    [ConfigKey("openForBuildCamera")]
    [Comment("Open For Build Camera. The free camera of build mode opens doors it comes near.")]
    public bool OpenForBuildCamera { get; set; } = true;

    [ConfigKey("openForAnimals")]
    [Comment("Open For Animals. Farm animals and pets open doors too (they may wander out).")]
    public bool OpenForAnimals { get; set; } = false;

    [ConfigKey("closeDoorsOpenedByHand")]
    [Comment("Close Doors Opened By Hand. A door someone opened by hand closes too once everyone has left. Off: only doors this mod opened close by themselves.")]
    public bool CloseDoorsOpenedByHand { get; set; } = true;

    [ConfigKey("respectHandToggles")]
    [Comment("Respect Hand Toggles. A door you open or close by hand stays that way while you are near it; it goes back to opening and closing by itself once everyone has left.")]
    public bool RespectHandToggles { get; set; } = true;

    [ConfigKey("doorLabel")]
    [Comment("Door Label. Looking at a door inside a base shows its mode under the crosshair (Players, Players & Survivors or Manual); nothing for Everyone.")]
    public bool DoorLabel { get; set; } = true;

    [ConfigKey("toasts")]
    [Comment("Messages. Show a message on screen when the Door Mode key changes a door's mode.")]
    public bool Toasts { get; set; } = true;

    /// <summary>A copy, for the settings window to show while a change is on its way to the tick thread.</summary>
    public DoorSettings Clone() => (DoorSettings)MemberwiseClone();
}

/// <summary>Per-door modes of the open world: kiln/data/worlds/&lt;world&gt;/config/auto-doors-kiln-doors-world.json. Set with the
/// Door Mode key (Shift + middle mouse button, or LB + Y on a controller) while looking at a door. Doors are named by position because entity ids change between sessions.</summary>
public sealed class DoorWorldRules
{
    [ConfigKey("doors")]
    [Comment("Doors with their own mode, as x,y,z=mode (mode: Players, PlayersAndSurvivors or Manual). Doors not listed open for everyone. Set with the Door Mode key (Shift + middle mouse button, or LB + Y on a controller) while looking at a door.")]
    public List<string> Doors { get; set; } = new();
}

/// <summary>Who opens a door.</summary>
public enum DoorMode
{
    Everyone,
    Players,              // players and their build camera
    PlayersAndSurvivors,  // players, their build camera and survivors (no animals)
    Manual,               // the mod leaves the door alone
}

/// <summary>Limits shared by the settings and the code.</summary>
public static class DoorRules
{
    public const int MinDistanceDm = 10;
    public const int MaxOpenDistanceDm = 100;
    public const int MaxCloseDistanceDm = 150;
    public const int MinHeightReachDm = 5;
    public const int MaxHeightReachDm = 100;
}
