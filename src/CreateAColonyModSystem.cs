using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Blocks;
using CreateAColony.Colonies;
using CreateAColony.Import;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace CreateAColony;

/// <summary>
/// Entry point: registers classes on both sides and the server-side debug commands.
/// Gameplay state lives in ColonyManager; this system only wires things up.
/// </summary>
public class CreateAColonyModSystem : ModSystem
{
    public const string ModId = "createacolony";

    /// <summary>Mods that replace the same gameplay loop and are not supported alongside this one.</summary>
    static readonly string[] IncompatibleModIds = { "vsvillage" };

    ICoreServerAPI? sapi;

    // Pending invites, invitee uid -> colony id. Kept in memory only: an invite lapses on restart.
    readonly Dictionary<string, string> pendingInvites = new();

    public override void StartPre(ICoreAPI api)
    {
        // modinfo.json has no incompatibility field, so refuse to start with a clear message instead.
        foreach (string modId in IncompatibleModIds)
        {
            if (api.ModLoader.IsModEnabled(modId))
            {
                throw new InvalidOperationException($"Create a Colony cannot run together with '{modId}'. Disable one of the two mods.");
            }
        }
    }

    public override void Start(ICoreAPI api)
    {
        api.RegisterEntity("EntitySettler", typeof(EntitySettler));
        api.RegisterEntityBehaviorClass("colonycitizen", typeof(EntityBehaviorColonyCitizen));
        api.RegisterBlockClass("BlockTownHall", typeof(BlockTownHall));
        api.RegisterBlockEntityClass("TownHall", typeof(BlockEntityTownHall));
        api.RegisterBlockClass("BlockSupplyCamp", typeof(BlockSupplyCamp));
        api.RegisterEntityBehaviorClass("settlerneeds", typeof(EntityBehaviorSettlerNeeds));
        api.RegisterBlockClass("BlockWarehouse", typeof(BlockWarehouse));
        api.RegisterBlockClass("BlockDropOff", typeof(BlockDropOff));
        api.RegisterBlockClass("BlockColonyHut", typeof(BlockColonyHut));
        api.RegisterBlockEntityClass("ColonyHut", typeof(BlockEntityColonyHut));
        api.RegisterBlockClass("BlockFieldMarker", typeof(BlockFieldMarker));

        AiTaskRegistry.Register<AiTaskSettlerShelter>("settlershelter");
        AiTaskRegistry.Register<AiTaskSettlerSleep>("settlersleep");
        AiTaskRegistry.Register<AiTaskSettlerEat>("settlereat");
        AiTaskRegistry.Register<AiTaskSettlerCourier>("settlercourier");
        AiTaskRegistry.Register<AiTaskSettlerBuilder>("settlerbuilder");
        AiTaskRegistry.Register<AiTaskSettlerForester>("settlerforester");
        AiTaskRegistry.Register<AiTaskSettlerFarmer>("settlerfarmer");
        AiTaskRegistry.Register<AiTaskSettlerGuard>("settlerguard");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        var parsers = api.ChatCommands.Parsers;

        api.ChatCommands.Create("colony")
            .WithDescription("Create a Colony commands")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .BeginSubCommand("info")
                .WithDescription("Show the colony you are standing in")
                .HandleWith(OnInfo)
            .EndSubCommand()
            .BeginSubCommand("citizens")
                .WithDescription("List the settlers of the colony you are standing in")
                .HandleWith(OnCitizens)
            .EndSubCommand()
            .BeginSubCommand("recruit")
                .WithDescription("Spawn a settler for your colony (MVP test command)")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(parsers.OptionalWord("name"))
                .HandleWith(OnRecruit)
            .EndSubCommand()
            .BeginSubCommand("invite")
                .WithDescription("Invite an online player to join your colony")
                .WithArgs(parsers.OnlinePlayer("player"))
                .HandleWith(OnInvite)
            .EndSubCommand()
            .BeginSubCommand("join")
                .WithDescription("Accept your pending colony invite")
                .HandleWith(OnJoin)
            .EndSubCommand()
            .BeginSubCommand("leave")
                .WithDescription("Leave the colony you are standing in")
                .HandleWith(OnLeave)
            .EndSubCommand()
            .BeginSubCommand("request")
                .WithDescription("Ask couriers to bring items from the warehouse to the chest you are looking at")
                .WithArgs(parsers.OptionalWord("item"), parsers.OptionalInt("amount", 1))
                .HandleWith(OnRequest)
            .EndSubCommand()
            .BeginSubCommand("requests")
                .WithDescription("List the open delivery requests of the colony you are standing in")
                .HandleWith(OnRequests)
            .EndSubCommand()
            .BeginSubCommand("reloadblueprints")
                .WithDescription("Re-read blueprint files after editing them, without restarting")
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(_ =>
                {
                    sapi!.ModLoader.GetModSystem<ColonyManager>().Blueprints.Reload();
                    return TextCommandResult.Success(Lang.Get("createacolony:blueprints-reloaded", BlueprintLibrary.UserFolder));
                })
            .EndSubCommand()
            .BeginSubCommand("importmc")
                .WithDescription("Convert a MineColonies .blueprint from ModConfig/createacolony/import into a building level")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(parsers.Word("file"), parsers.Word("buildingtype"), parsers.IntRange("level", 1, BlueprintLibrary.MaxLevels))
                .HandleWith(OnImportMc)
            .EndSubCommand()
            .BeginSubCommand("findblock")
                .WithDescription("List installed block codes matching a pattern, e.g. /colony findblock *door*oak*")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(parsers.Word("pattern"))
                .HandleWith(args =>
                {
                    string pattern = (string)args[0];
                    if (!pattern.Contains(':')) pattern = "game:" + pattern;
                    var codes = sapi!.World.SearchBlocks(new AssetLocation(pattern)).Select(b => b.Code.ToString()).OrderBy(c => c).ToList();
                    string list = codes.Count == 0 ? "-" : string.Join("\n", codes.Take(30)) + (codes.Count > 30 ? $"\n... {codes.Count - 30} more" : "");
                    sapi.Logger.Notification("[createacolony] findblock {0}: {1}", pattern, string.Join(", ", codes));
                    return TextCommandResult.Success(list);
                })
            .EndSubCommand()
            .BeginSubCommand("cancelrequest")
                .WithDescription("Cancel a delivery request by its number")
                .WithArgs(parsers.Int("number"))
                .HandleWith(OnCancelRequest)
            .EndSubCommand();
    }

    TextCommandResult OnInfo(TextCommandCallingArgs args)
    {
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(args.Caller.Entity.Pos.AsBlockPos);
        if (colony == null) return TextCommandResult.Success(Lang.Get("createacolony:not-in-colony"));
        return TextCommandResult.Success(Lang.Get("createacolony:summary", colony.Name, colony.Level, colony.Citizens.Count(c => c.Alive), colony.Members.Count));
    }

    TextCommandResult OnCitizens(TextCommandCallingArgs args)
    {
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(args.Caller.Entity.Pos.AsBlockPos);
        if (colony == null) return TextCommandResult.Success(Lang.Get("createacolony:not-in-colony"));

        var lines = new List<string>();
        foreach (var c in colony.Citizens)
        {
            string state = !c.Alive ? Lang.Get("createacolony:state-dead")
                : c.IsDowned ? Lang.Get("createacolony:state-downed")
                : c.Saturation >= 0 && c.Saturation < manager.Config.SaturationMax * manager.Config.EatBelowFraction ? Lang.Get("createacolony:state-hungry")
                : Lang.Get("createacolony:state-ok");
            int food = c.Saturation < 0 ? 100 : (int)(100 * c.Saturation / manager.Config.SaturationMax);
            string bed = c.HomeBedPos != null ? Lang.Get("createacolony:has-bed") : Lang.Get("createacolony:no-bed");
            lines.Add(Lang.Get("createacolony:citizen-line", c.Name, state, food, bed));
        }
        return TextCommandResult.Success(lines.Count == 0 ? Lang.Get("createacolony:no-citizens") : string.Join("\n", lines));
    }

    TextCommandResult OnRecruit(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetOwnedBy(player.PlayerUID);
        if (colony == null) return TextCommandResult.Error(Lang.Get("createacolony:no-colony"));

        var record = manager.SpawnSettler(colony, args[0] as string);
        if (record == null) return TextCommandResult.Error("Settler entity type not found, see server log");

        return TextCommandResult.Success(Lang.Get("createacolony:recruited", record.Name));
    }

    TextCommandResult OnInvite(TextCommandCallingArgs args)
    {
        var inviter = (IServerPlayer)args.Caller.Player;
        var invitee = (IServerPlayer)args[0];
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();

        // Invite into the colony you stand in if you manage it, else into the one you own.
        var colony = manager.GetAt(inviter.Entity.Pos.AsBlockPos) is { } here && here.CanManage(inviter.PlayerUID)
            ? here
            : manager.GetOwnedBy(inviter.PlayerUID);
        if (colony == null) return TextCommandResult.Error(Lang.Get("createacolony:no-colony"));
        if (colony.RoleOf(invitee.PlayerUID) >= ColonyRole.Member) return TextCommandResult.Error(Lang.Get("createacolony:already-member", invitee.PlayerName));

        pendingInvites[invitee.PlayerUID] = colony.ColonyId;
        invitee.SendMessage(0, Lang.Get("createacolony:invited", inviter.PlayerName, colony.Name), EnumChatType.Notification);
        return TextCommandResult.Success(Lang.Get("createacolony:invite-sent", invitee.PlayerName));
    }

    TextCommandResult OnJoin(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        if (!pendingInvites.Remove(player.PlayerUID, out var colonyId) || manager.GetById(colonyId) is not { } colony)
        {
            return TextCommandResult.Error(Lang.Get("createacolony:no-invite"));
        }

        manager.SetMemberRole(colony, player.PlayerUID, player.PlayerName, ColonyRole.Member);
        return TextCommandResult.Success(Lang.Get("createacolony:joined", colony.Name));
    }

    TextCommandResult OnLeave(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(player.Entity.Pos.AsBlockPos);
        if (colony == null || colony.RoleOf(player.PlayerUID) == ColonyRole.Visitor) return TextCommandResult.Error(Lang.Get("createacolony:not-member"));
        if (colony.OwnerUid == player.PlayerUID) return TextCommandResult.Error(Lang.Get("createacolony:owner-cannot-leave"));

        manager.SetMemberRole(colony, player.PlayerUID, player.PlayerName, ColonyRole.Visitor);
        return TextCommandResult.Success(Lang.Get("createacolony:left", colony.Name));
    }

    TextCommandResult OnRequest(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        if (args[0] is not string code || code.Length == 0) return TextCommandResult.Error(Lang.Get("createacolony:request-usage"));
        int amount = Math.Max(1, (int)args[1]);

        var sel = player.CurrentBlockSelection;
        var colony = sel == null ? null : manager.GetAt(sel.Position);
        if (sel == null || colony == null || sapi.World.BlockAccessor.GetBlockEntity(sel.Position) is not BlockEntityContainer)
        {
            return TextCommandResult.Error(Lang.Get("createacolony:request-not-container"));
        }
        if (!colony.CanManage(player.PlayerUID)) return TextCommandResult.Error(Lang.Get("createacolony:denied-officer"));

        if (!code.Contains(':')) code = "game:" + code;
        var request = manager.AddRequest(colony, code, amount, sel.Position, player.PlayerName);
        return TextCommandResult.Success(Lang.Get("createacolony:request-created", request.RequestId, amount, code));
    }

    TextCommandResult OnRequests(TextCommandCallingArgs args)
    {
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(args.Caller.Entity.Pos.AsBlockPos);
        if (colony == null) return TextCommandResult.Success(Lang.Get("createacolony:not-in-colony"));
        if (colony.Requests.Count == 0) return TextCommandResult.Success(Lang.Get("createacolony:request-none"));

        var lines = colony.Requests.Select(r => Lang.Get("createacolony:request-line", r.RequestId, r.ItemCode, r.Delivered, r.Quantity,
            r.LeasedByCitizenId != 0 ? Lang.Get("createacolony:request-leased") : ""));
        return TextCommandResult.Success(string.Join("\n", lines));
    }

    TextCommandResult OnImportMc(TextCommandCallingArgs args)
    {
        string file = (string)args[0];
        string type = ((string)args[1]).ToLowerInvariant();
        int level = (int)args[2];

        if (!file.EndsWith(".blueprint")) file += ".blueprint";
        string input = System.IO.Path.Combine(MineColoniesImporter.ImportFolder, file);
        if (!System.IO.File.Exists(input)) return TextCommandResult.Error(Lang.Get("createacolony:import-missing", input));
        string output = System.IO.Path.Combine(BlueprintLibrary.UserFolder, type, $"level{level}.json");

        MineColoniesImporter.Result result;
        try
        {
            result = new MineColoniesImporter(sapi!, MineColoniesImporter.LoadConfig(sapi!)).Convert(input, type, output);
        }
        catch (Exception e)
        {
            sapi!.Logger.Error("[createacolony] Import of {0} failed: {1}", input, e);
            return TextCommandResult.Error(Lang.Get("createacolony:import-failed", e.Message));
        }

        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        manager.Blueprints.Reload();
        string unmatched = result.Unmatched.Count == 0
            ? Lang.Get("createacolony:import-all-matched")
            : Lang.Get("createacolony:import-unmatched", string.Join(", ", result.Unmatched.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} x{kv.Value}")));
        sapi.Logger.Notification("[createacolony] Imported {0} as {1} level {2}: {3} blocks. {4}", file, type, level, result.Blocks, unmatched);
        return TextCommandResult.Success(Lang.Get("createacolony:import-done", file, Lang.Get("createacolony:building-" + type), level, result.Blocks) + "\n" + unmatched);
    }

    TextCommandResult OnCancelRequest(TextCommandCallingArgs args)
    {
        var player = (IServerPlayer)args.Caller.Player;
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(player.Entity.Pos.AsBlockPos);
        if (colony == null) return TextCommandResult.Error(Lang.Get("createacolony:not-in-colony"));
        if (!colony.CanManage(player.PlayerUID)) return TextCommandResult.Error(Lang.Get("createacolony:denied-officer"));

        int id = (int)args[0];
        var request = colony.Requests.Find(r => r.RequestId == id);
        if (request == null) return TextCommandResult.Error(Lang.Get("createacolony:request-bad-id"));
        manager.CancelRequest(colony, request);
        return TextCommandResult.Success(Lang.Get("createacolony:request-cancelled", id));
    }
}
