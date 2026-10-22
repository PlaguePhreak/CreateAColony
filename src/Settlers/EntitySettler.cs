using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// A colony settler. Reuses the vanilla dressed-humanoid base (seraph model, random vanilla outfits,
/// hand slots from the "villagerinventory" behavior) so no new art is needed for the MVP.
/// </summary>
public class EntitySettler : EntityDressedHumanoid
{
    public const string ColonyIdAttr = "createacolony:colonyId";
    public const string CitizenIdAttr = "createacolony:citizenId";

    public string? ColonyId
    {
        get => WatchedAttributes.GetString(ColonyIdAttr);
        set { WatchedAttributes.SetString(ColonyIdAttr, value); WatchedAttributes.MarkPathDirty(ColonyIdAttr); }
    }

    public int CitizenId
    {
        get => WatchedAttributes.GetInt(CitizenIdAttr);
        set { WatchedAttributes.SetInt(CitizenIdAttr, value); WatchedAttributes.MarkPathDirty(CitizenIdAttr); }
    }

    /// <summary>Walking on a Pathblock (a road) is faster, by config PathSpeedMultiplier.</summary>
    public override double GetWalkSpeedMultiplier(double groundDragFactor = 0.3)
    {
        double multiplier = base.GetWalkSpeedMultiplier(groundDragFactor);
        return PathBlocks.SpeedMultiplier != 1f && PathBlocks.StandsOnPath(this) ? multiplier * PathBlocks.SpeedMultiplier : multiplier;
    }
}
