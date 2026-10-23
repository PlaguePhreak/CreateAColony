using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Colonies;

public partial class ColonyManager
{
    const string StarterKitKey = "createacolony:starterkits";

    HashSet<string>? starterKitsTaken;

    HashSet<string> StarterKitsTaken =>
        starterKitsTaken ??= new HashSet<string>(sapi.WorldManager.SaveGame.GetData<List<string>>(StarterKitKey) ?? new List<string>());

    /// <summary>True if the player has already had the supply camp's starter kit in this world.</summary>
    public bool HasStarterKit(string playerUid) => StarterKitsTaken.Contains(playerUid);

    /// <summary>
    /// Marks the starter kit as taken by the player. Saved straight away, so a crash right after placing
    /// the camp cannot hand out a second kit. Kept on the world, not on a colony, so it holds before one exists.
    /// </summary>
    public void MarkStarterKit(string playerUid)
    {
        if (!StarterKitsTaken.Add(playerUid)) return;
        sapi.WorldManager.SaveGame.StoreData(StarterKitKey, new List<string>(StarterKitsTaken));
    }

    /// <summary>
    /// Puts the supply camp's own building around it at once, if there is a blueprint for it
    /// (blueprints/supplycamp/level1.json, like the huts' Testing blueprints). Nothing is built where the
    /// player may not build: then none of it is, and the lang key saying why is returned.
    /// </summary>
    public string? BuildSupplyCamp(IServerPlayer player, BlockPos campPos)
    {
        var blueprint = Blueprints.Get(BlueprintStyles.Testing, BuildingTypes.SupplyCamp, 1, RotationFor(player, BuildingTypes.SupplyCamp));
        if (blueprint == null) return null;

        var ba = sapi.World.BlockAccessor;
        var spots = new List<(BuildStep Step, BlockPos Pos)>();
        foreach (var step in blueprint.Steps)
        {
            var pos = campPos.AddCopy(step.Dx, step.Dy, step.Dz);
            bool foreign = GetAt(pos) is { } colony && colony.RoleOf(player.PlayerUID) < ColonyRole.Member;
            if (foreign || sapi.World.Claims.TestAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak) != EnumWorldAccessResponse.Granted) return "createacolony:supplycamp-nobuild";
            spots.Add((step, pos));
        }
        foreach (var (step, pos) in spots)
        {
            ba.SetBlock(step.BlockId, pos);
            RestoreSavedDetails(step, pos);
        }
        return null;
    }
}
