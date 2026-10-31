using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Corpses;

/// <summary>
/// Server config for corpses, stored in ModConfig/createacolony-corpses.json.
/// Ported from Player Corpse by DArkHekRoMaNT (MIT, see THIRD_PARTY_NOTICES.md); the settler options are ours.
/// </summary>
public class CorpseConfig
{
    public const string FileName = "createacolony-corpses.json";

    /// <summary>A player who dies leaves a corpse holding their things. Off when the Player Corpse mod is installed, which does the same.</summary>
    public bool PlayerCorpses = true;
    /// <summary>A settler who dies or is knocked out leaves a corpse holding their things, instead of dropping them on the ground.</summary>
    public bool SettlerCorpses = true;

    /// <summary>Burns in 15 seconds in fire or lava.</summary>
    public bool CanBurn = false;
    /// <summary>Has 100 health and can be broken, which drops everything.</summary>
    public bool HasHealth = false;

    /// <summary>Which of a player's inventories go into their corpse.</summary>
    public string[] SaveInventoryTypes =
    {
        GlobalConstants.hotBarInvClassName,
        GlobalConstants.backpackInvClassName,
        GlobalConstants.craftingInvClassName,
        GlobalConstants.mousecursorInvClassName,
        GlobalConstants.characterInvClassName
    };

    /// <summary>The privilege needed for /returnthings.</summary>
    public string NeedPrivilegeForReturnThings = Privilege.gamemode;
    /// <summary>How many deaths per player are kept on disk for /returnthings. 0 turns it off.</summary>
    public int MaxDeathContentSavedPerPlayer = 10;

    /// <summary>Auto, Always or None. Auto leaves the death waypoint to other mods that add one.</summary>
    public string CreateWaypoint = "Auto";
    /// <summary>circle, bee, cave, home, ladder, pick, rocks, ruins, spiral, star1, star2, trader, vessel, etc.</summary>
    public string WaypointIcon = "bee";
    /// <summary>A colour name or #RRGGBB (https://www.99colors.net/dot-net-colors).</summary>
    public string WaypointColor = "crimson";
    public bool PinWaypoint = true;
    /// <summary>The waypoint goes when the corpse is collected.</summary>
    public bool RemoveWaypointOnCollect = true;
    public bool DebugMode = false;

    /// <summary>Anyone may take from a corpse after this many in-game hours (0: always, below 0: never).
    /// A settler's corpse is always open to members of their colony.</summary>
    public int FreeCorpseAfterTime = 240;
    /// <summary>How long right-click is held to collect a corpse, in seconds.</summary>
    public float CorpseCollectionTime = 1;
    /// <summary>When false the Corpse Compass cannot be made, and compasses already made turn into unknown items.</summary>
    public bool CorpseCompassEnabled = true;

    /// <summary>Vanilla, Armor or ArmorAndCloth: whether a player's armour, and clothes, go into the corpse.</summary>
    public string DropArmorOnDeath = "Vanilla";
}
