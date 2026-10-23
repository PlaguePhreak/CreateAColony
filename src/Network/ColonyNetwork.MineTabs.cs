using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Network;

/// <summary>
/// The Miner's Hut's Levels, Settings, Guards and Required resources tabs, and the give button on every
/// Required resources tab (the player's items go straight into the hut's storage).
/// </summary>
public partial class ColonyNetwork
{
    string? MineTabAction(Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Officer) return "createacolony:denied-officer";
        var mine = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (mine == null || mine.Type != BuildingTypes.Miner) return "createacolony:bad-building";
        int.TryParse(packet.Text, out int number);
        switch (packet.Action)
        {
            case ColonyAction.SetMineLevel: Manager.SetMineLevel(colony, mine, number); return null;
            case ColonyAction.RepairMineLevel: Manager.RepairMineLevel(colony, mine, number); return null;
            case ColonyAction.SetMaxMineLevels: Manager.SetMaxMineLevels(colony, mine, number); return null;
            case ColonyAction.SetFillBlock: return Manager.SetFillBlock(colony, mine, packet.Text);
            case ColonyAction.AssignMineGuard:
                var guard = colony.Citizens.Find(c => c.CitizenId == packet.CitizenId);
                return guard == null ? "createacolony:bad-citizen" : Manager.AssignMineGuard(colony, mine, guard);
            case ColonyAction.UnassignMineGuard: Manager.UnassignMineGuard(colony, mine, packet.CitizenId); return null;
        }
        return null;
    }

    /// <summary>The viewer's hotbar and backpack slots, where the give button takes items from.</summary>
    static IEnumerable<ItemSlot> CarriedSlots(IPlayer player) =>
        new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName }
            .Select(name => player.InventoryManager.GetOwnInventory(name))
            .Where(inv => inv != null)
            .SelectMany(inv => inv);

    static int CarriedCount(IPlayer player, string code)
    {
        var match = ColonyManager.Matcher(code);
        return CarriedSlots(player).Where(s => !s.Empty && match(s.Itemstack)).Sum(s => s.StackSize);
    }

    /// <summary>Text "code|count": moves up to count of the item from the player into the hut's storage.</summary>
    string? GiveResource(IServerPlayer player, Colony colony, ColonyRole role, ColonyActionPacket packet)
    {
        if (role < ColonyRole.Member) return "createacolony:denied-member";
        var hut = colony.Buildings.Find(b => b.BuildingId == packet.BuildingId);
        if (hut == null || !ColonyManager.HasOwnStorage(hut.Type)) return "createacolony:bad-building";
        int bar = packet.Text.LastIndexOf('|');
        if (bar <= 0 || !int.TryParse(packet.Text[(bar + 1)..], out int want) || want <= 0) return "createacolony:bad-building";
        var match = ColonyManager.Matcher(packet.Text[..bar]);
        var storage = Manager.HutStorage(colony, hut).ToList();
        if (storage.Count == 0) return "createacolony:give-no-storage";
        want = Math.Min(want, 999);
        int given = 0;
        foreach (var slot in CarriedSlots(player))
        {
            if (given >= want) break;
            if (slot.Empty || !match(slot.Itemstack)) continue;
            var part = slot.TakeOut(Math.Min(slot.StackSize, want - given));
            int before = part.StackSize;
            foreach (var chest in storage)
            {
                if (part.StackSize <= 0) break;
                Manager.PutItems(chest, part);
            }
            given += before - part.StackSize;
            // What did not fit goes back where it came from.
            if (part.StackSize > 0)
            {
                if (slot.Empty) slot.Itemstack = part;
                else slot.Itemstack.StackSize += part.StackSize;
            }
            slot.MarkDirty();
            if (part.StackSize > 0) break;
        }
        if (given == 0) return "createacolony:give-none";
        player.SendMessage(0, Lang.GetL(player.LanguageCode, "createacolony:give-done", given), EnumChatType.Notification);
        return null;
    }

    /// <summary>Fills in a Miner's Hut's Levels, Settings, Guards and Required resources tabs.</summary>
    void AddMineTabs(HutSnapshot snapshot, Colony colony, Building mine, string viewerUid)
    {
        snapshot.IsMiner = true;
        snapshot.MineLevel = mine.MineLevel;
        snapshot.RepairLevel = mine.RepairLevel;
        snapshot.MaxMineLevels = mine.MaxMineLevels;
        snapshot.MineLevelsAllowed = Manager.MineshaftLevelsAllowed(mine);
        snapshot.FillBlock = mine.FillBlock;
        if (Manager.MineLevels(colony, mine) is { } levels)
        {
            snapshot.MineLevels = levels.Select(l => new MineLevelView { Level = l.Level, Y = l.Y, Sections = l.Sections, Left = l.Left, Working = l.Working }).ToList();
        }

        Manager.CleanMineGuards(colony, mine);
        snapshot.MineGuardSlots = ColonyManager.MineGuardSlots(mine);
        foreach (var guard in Manager.MinePatrolGuards(colony).OrderBy(c => c.Name))
        {
            var with = Manager.MineGuardedBy(colony, guard);
            var view = new CitizenView { CitizenId = guard.CitizenId, Name = guard.Name, Job = with == null || with == mine ? "" : ColonyManager.BuildingName(with) };
            if (with == mine) snapshot.MineGuards.Add(view);
            else snapshot.MineGuardChoices.Add(view);
        }

        // Required resources: the mineshaft piece the miner works on now.
        if (Manager.MineshaftResources(colony, mine) is not { } work) return;
        snapshot.ResourcesFor = work.Project;
        snapshot.UsedPercent = work.Total == 0 ? 100 : work.Done * 100 / work.Total;
        snapshot.ResourcesStep = Lang.Get("createacolony:resources-step-mine", work.Done, work.Total);
        var miner = Manager.WorkerAt(colony, mine, Jobs.Miner);
        snapshot.BuilderName = miner?.Name ?? "";
        var storage = Manager.HutStorage(colony, mine).Cast<Vintagestory.GameContent.BlockEntityContainer>().ToList();
        foreach (var need in work.Needs)
        {
            var match = ColonyManager.Matcher(need.Code);
            int have = Manager.CountItems(storage, match) + (miner == null ? 0 : Manager.PackCount(miner, match));
            snapshot.Resources.Add(new ResourceView { Stack = need.Sample.ToBytes(), Needed = need.Count, Have = have, Code = need.Code, AnyOf = need.Code.Contains('*') ? "rock" : "" });
        }
        AddPlayerHas(snapshot, viewerUid);
    }

    /// <summary>How many of each required resource the viewer carries, for the green rows and the give button.</summary>
    void AddPlayerHas(HutSnapshot snapshot, string viewerUid)
    {
        if (sapi!.World.PlayerByUid(viewerUid) is not { } viewer) return;
        foreach (var row in snapshot.Resources.Where(r => !r.IsTool && r.Code.Length > 0)) row.PlayerHas = CarriedCount(viewer, row.Code);
    }
}
