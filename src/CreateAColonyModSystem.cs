using System;
using System.Collections.Generic;
using CreateAColony.Blocks;
using CreateAColony.Colonies;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

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
            .EndSubCommand();
    }

    TextCommandResult OnInfo(TextCommandCallingArgs args)
    {
        var manager = sapi!.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(args.Caller.Entity.Pos.AsBlockPos);
        if (colony == null) return TextCommandResult.Success(Lang.Get("createacolony:not-in-colony"));
        return TextCommandResult.Success(Lang.Get("createacolony:summary", colony.Name, colony.Level, colony.Citizens.Count, colony.Members.Count));
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
}
