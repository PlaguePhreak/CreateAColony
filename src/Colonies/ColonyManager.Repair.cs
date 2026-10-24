using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using CreateAColony.Blocks;

namespace CreateAColony.Colonies;

/// <summary>
/// The Repair button on a hut's Main tab (Aaron, 0.118.0). A builder puts the finished building back as its
/// blueprint has it: missing or broken blocks go back, blocks that should not be there come out, ground that got
/// in is dug out and gaps under the floor are filled. Chests, furniture and decorations stay where they are.
/// </summary>
public partial class ColonyManager
{
    /// <summary>Asks a builder to repair a finished building. Returns a lang key on failure.</summary>
    public string? StartRepair(Colony colony, Building building)
    {
        if (building.Removing) return "createacolony:remove-already";
        if (building.Repairing || building.TargetLevel > building.Level) return "createacolony:build-already";
        if (building.Level <= 0) return "createacolony:repair-not-built";
        if (building.Type == BuildingTypes.MineshaftEntrance) return "createacolony:repair-no";
        if (Blueprints.Get(building, building.Level) is not { Steps.Count: > 0 }) return "createacolony:build-no-blueprint";

        // An upkeep order already open becomes the repair, which does all of its work and more.
        building.Upkeep = false;
        building.Repairing = true;
        building.TargetLevel = building.Level;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        CancelBuildRequests(colony, building);
        MarkDirty(colony);
        LogEvent(colony, "event-repair", "@building-" + building.Type);
        return null;
    }

    /// <summary>The repair is done: the building stays at its level, and the colony is told.</summary>
    void FinishRepair(Colony colony, Building building)
    {
        building.Repairing = false;
        building.Upkeep = false;
        building.TargetLevel = 0;
        building.Cursor = 0;
        building.BuilderCitizenId = 0;
        BuildStatus.Remove(building.BuildingId);
        CancelBuildRequests(colony, building);
        MarkDirty(colony);
        LogEvent(colony, "event-repaired", "@building-" + building.Type);
        string message = Lang.Get("createacolony:repair-finished", BuildingName(building));
        foreach (var member in colony.Members)
        {
            if (sapi.World.PlayerByUid(member.PlayerUid) is IServerPlayer p && p.ConnectionState == EnumClientState.Playing)
            {
                p.SendMessage(0, message, EnumChatType.Notification);
            }
        }
    }

    /// <summary>
    /// Things a repair does not take out where the blueprint has air: anything with a block entity (beds,
    /// racks, shelves, signs, chests) and paintings.
    /// </summary>
    bool IsKeptInRepair(BuildStep step, BlockPos pos)
    {
        if (step.BlockId != 0) return false;
        var ba = sapi.World.BlockAccessor;
        return ba.GetBlockEntity(pos) != null || ba.GetBlock(pos) is BlockPainting;
    }
}
