using System.Collections.Generic;
using Kiln;

namespace AutoDoorsKiln;

/// <summary>The mod's settings: kiln config auto-doors-kiln.json, changed in the mod's settings window (Shift+F7), the Kiln menu
/// (Insert, Mods, Auto Doors) or by saving the file. Distances are in metres; their Kiln sliders move in steps of 10 cm
/// ([Range(min, max, step)], Kiln 0.2.0-dev.31). Every setting applies at once.</summary>
public sealed class DoorSettings
{
    [ConfigKey("enabled")]
    [Comment("Enabled. Doors open and close by themselves. Switched in the settings window (Shift+F7).")]
    public bool Enabled { get; set; } = true;

    [ConfigKey("openDistance")]
    [Comment("Open Distance, in metres. A door opens when someone comes this close to it, measured along the ground.")]
    [Range(DoorRules.MinDistance, DoorRules.MaxOpenDistance, DoorRules.Step)]
    public double OpenDistance { get; set; } = 4.0;

    [ConfigKey("closeDistance")]
    [Comment("Close Distance, in metres. A door closes once everyone is farther than this. Never less than the Open Distance.")]
    [Range(DoorRules.MinDistance, DoorRules.MaxCloseDistance, DoorRules.Step)]
    public double CloseDistance { get; set; } = 4.6;

    [ConfigKey("heightReach")]
    [Comment("Height Reach, in metres. Someone more than this far above or below a door (on another floor) neither opens it nor keeps it open.")]
    [Range(DoorRules.MinHeightReach, DoorRules.MaxHeightReach, DoorRules.Step)]
    public double HeightReach { get; set; } = 2.5;

    [ConfigKey("closeDelaySeconds")]
    [Comment("Close Delay, in seconds. How long a door stays open after everyone has left. 0 closes it at once.")]
    [Range(0, 30)]
    public int CloseDelaySeconds { get; set; } = 0;

    [ConfigKey("onlyInsideBases")]
    [Comment("Only Inside Bases. Only doors inside a flame altar's area open by themselves. Off: every door in the world does; door modes, their key and the label stay for base doors.")]
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
    [Comment("Doors with their own mode, as x,y,z=mode (mode: Players, PlayersAndSurvivors or Manual). Doors not listed open for everyone; secret doors not listed are Close Only (listed: Players or Manual). Set with the Door Mode key (Shift + middle mouse button, or LB + Y on a controller) while looking at a door.")]
    public List<string> Doors { get; set; } = new();
}

/// <summary>Who opens a door.</summary>
public enum DoorMode
{
    Everyone,
    Players,              // players and their build camera
    PlayersAndSurvivors,  // players, their build camera and survivors (no animals)
    Manual,               // the mod leaves the door alone
    CloseOnly,            // secret pieces only (their default): never opens by itself, closes once the players have left
}

/// <summary>Limits shared by the settings and the code.</summary>
public static class DoorRules
{
    public const double Step = 0.1;
    public const double MinDistance = 1.0;
    public const double MaxOpenDistance = 10.0;
    public const double MaxCloseDistance = 15.0;
    public const double MinHeightReach = 0.5;
    public const double MaxHeightReach = 10.0;
}
