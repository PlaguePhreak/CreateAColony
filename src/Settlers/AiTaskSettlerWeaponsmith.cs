using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace CreateAColony.Settlers;

/// <summary>
/// A weaponsmith works from a Weaponsmith's Hut exactly like a blacksmith, with the forge and anvil, but makes
/// the weapons: swords, spears, maces, shields, blades and arrowheads (ColonyManager.SmithCategory).
/// </summary>
public class AiTaskSettlerWeaponsmith : AiTaskSettlerBlacksmith
{
    public AiTaskSettlerWeaponsmith(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig) { }

    protected override string Job => Jobs.Weaponsmith;
    protected override string HutType => BuildingTypes.Weaponsmith;
}
