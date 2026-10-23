using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace CreateAColony.Settlers;

/// <summary>
/// What a settler does with nothing else to do: now and then it strolls to a spot near its home, using the
/// pathfinder like every other walk, and otherwise stands about.
///
/// This replaces the game's "wander" task, which settlers used to have. That task walks in a straight line
/// with no pathfinding, so settlers walked into walls and fences and stood pressed against them. It also
/// pulls an entity back to where it first spawned (often the Town Hall or the colony border) whenever it is
/// more than a few blocks away, and after two minutes away with no player near, it teleports it there. Workers
/// whose hut was further away kept marching off between jobs and popping back to the spawn point.
///
/// Before strolling, an idle settler tidies up: a guard drops off what it picked up at its tower, anyone
/// walks over to items lying nearby to pick them up, and a settler without a torch fetches one from its
/// hut or the warehouse for the dark.
/// </summary>
public class AiTaskSettlerIdle : SettlerTaskBase
{
    /// <summary>How long a settler stands about between strolls, at least and at most.</summary>
    const long MinPauseMs = 8_000;
    const long MaxPauseMs = 25_000;
    /// <summary>A stroll that has not arrived by now is cut short.</summary>
    const long MaxStrollMs = 30_000;
    /// <summary>Further than this from home, the next stroll heads back there.</summary>
    const double FarFromHome = 12;

    readonly int strollRadius;
    long nextStrollMs;
    long startedMs;
    Vec3d? spot;
    enum Errand { Stroll, PickUp, Torch, Deposit }
    Errand errand;
    BlockPos? chestPos;

    public AiTaskSettlerIdle(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        strollRadius = System.Math.Max(1, taskConfig["strollRadius"].AsInt(6));
    }

    public override bool ShouldExecute()
    {
        if (manager == null) return false;
        long now = entity.World.ElapsedMilliseconds;
        if (now < nextStrollMs) return false;
        nextStrollMs = now + MinPauseMs + (long)(entity.World.Rand.NextDouble() * (MaxPauseMs - MinPauseMs));
        if (Colony is not { } colony || Citizen is not { } citizen || citizen.IsDowned) return false;
        if (PlanErrand(colony, citizen)) return true;
        errand = Errand.Stroll;

        var home = Home(colony, citizen);
        // Back towards home when far from it, else anywhere within the stroll radius of it.
        bool far = entity.Pos.XYZ.HorizontalSquareDistanceTo(home.X + 0.5, home.Z + 0.5) > FarFromHome * FarFromHome;
        int r = far ? 2 : strollRadius;
        var rand = entity.World.Rand;
        var around = home.AddCopy(rand.Next(-r, r + 1), 0, rand.Next(-r, r + 1));
        spot = manager.FreeSpotNear(entity, around, 2);
        return spot != null && spot.SquareDistanceTo(entity.Pos.XYZ) > 2 * 2;
    }

    /// <summary>Picks a chore to do before strolling. True when there is one; spot (and chestPos) say where.</summary>
    bool PlanErrand(Colony colony, CitizenRecord citizen)
    {
        if (citizen.Visitor || citizen.IsChild) return false;
        chestPos = null;

        // A guard's things go to its tower; other jobs drop theirs off themselves, and couriers take loose loads to the warehouse.
        if (citizen.JobCode == Jobs.Guard && !manager!.PackEmpty(citizen) && citizen.WorkplacePos != null
            && manager.GetBuilding(colony, citizen.WorkplacePos) is { } tower && manager.HutStorage(colony, tower).FirstOrDefault() is { } chest)
        {
            errand = Errand.Deposit;
            chestPos = chest.Pos.Copy();
            spot = chestPos.ToVec3d().Add(0.5, 0, 0.5);
            return true;
        }

        if (manager!.Config.PickUpRange > 0 && manager.GroundItemNear(colony, citizen, entity, manager.Config.PickUpRange) is { } item)
        {
            errand = Errand.PickUp;
            spot = item.Pos.XYZ.Clone();
            return true;
        }

        if (manager.TorchChest(colony, citizen) is { } torchChest)
        {
            errand = Errand.Torch;
            chestPos = torchChest.Pos.Copy();
            spot = chestPos.ToVec3d().Add(0.5, 0, 0.5);
            return true;
        }
        return false;
    }

    /// <summary>On arrival: does the chore.</summary>
    void DoErrand()
    {
        if (Colony is not { } colony || Citizen is not { } citizen) return;
        switch (errand)
        {
            case Errand.PickUp:
                manager!.PickUpNearby(colony, citizen, entity, 2f);
                break;
            case Errand.Torch:
                if (entity.World.BlockAccessor.GetBlockEntity(chestPos!) is Vintagestory.GameContent.BlockEntityContainer torchChest) manager!.TakeTorch(colony, citizen, torchChest);
                break;
            case Errand.Deposit:
                if (citizen.WorkplacePos != null && manager!.GetBuilding(colony, citizen.WorkplacePos) is { } tower)
                {
                    manager.UnloadPack(colony, citizen, manager.HutStorage(colony, tower));
                }
                break;
        }
    }

    /// <summary>
    /// Where a settler hangs about: its workplace if it has one, else the spot the colony gave it (a child's home,
    /// where a visitor waits), else its bed, else the Town Hall.
    /// </summary>
    BlockPos Home(Colony colony, CitizenRecord citizen)
    {
        if (citizen.JobCode != null && citizen.WorkplacePos != null) return citizen.WorkplacePos;
        var attrs = entity.Attributes;
        if (attrs.HasAttribute("spawnX"))
        {
            return new BlockPos((int)System.Math.Floor(attrs.GetDouble("spawnX")), (int)System.Math.Floor(attrs.GetDouble("spawnY")),
                (int)System.Math.Floor(attrs.GetDouble("spawnZ")), entity.Pos.Dimension);
        }
        return citizen.HomeBedPos ?? colony.TownHallPos;
    }

    protected override bool CountsForWillpower => false;

    public override void StartExecute()
    {
        base.StartExecute();
        startedMs = entity.World.ElapsedMilliseconds;
        WalkTo(spot!, errand == Errand.Stroll ? 1f : 1.5f);
    }

    public override bool ContinueExecute(float dt)
    {
        // A stroll is given up at the first failure: it is not worth retrying, nor building Willpower for.
        if (Arrived || PathFailedOnce || entity.World.ElapsedMilliseconds - startedMs > MaxStrollMs)
        {
            StopWalking();
            if (Arrived || entity.Pos.XYZ.DistanceTo(spot!) < 2.5) DoErrand();
            if (errand != Errand.Stroll) nextStrollMs = entity.World.ElapsedMilliseconds + 1000; // straight on to the next chore
            return false;
        }
        KeepWalking();
        return true;
    }

    public override string Describe(Colony colony, CitizenRecord citizen) => errand switch
    {
        Errand.PickUp => Lang.Get("createacolony:activity-picking-up"),
        Errand.Torch => Lang.Get("createacolony:activity-fetching-torch"),
        Errand.Deposit => Lang.Get("createacolony:activity-dropping-off"),
        _ => Lang.Get(citizen.Visitor ? "createacolony:activity-visitor-waiting" : "createacolony:activity-idle")
    };
}
