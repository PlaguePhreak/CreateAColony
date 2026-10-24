using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CreateAColony.Colonies;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Map;

/// <summary>
/// The "Colony prospecting" layer of the world map: the readings the colony's prospectors took, kept
/// apart from the player's own prospecting layer. It draws them just as the game draws a player's
/// readings (same dots, colours, hover text and ore filter), but they come from the colonies the player
/// belongs to and cannot be deleted from the map.
/// </summary>
public class ColonyOreMapLayer : OreMapLayer
{
    public const string Code = "colonyprospecting";

    public ColonyOreMapLayer(ICoreAPI api, IWorldMapManager mapSink) : base(api, mapSink) { }

    public override string Title => "Colony ore map readings";
    public override string LayerGroupCode => Code;

    public override void OnViewChangedServer(IServerPlayer fromPlayer, int x1, int z1, int x2, int z2) => Resend(fromPlayer);

    [Obsolete("Kept because the base layer still has it")]
    public override void OnViewChangedServer(IServerPlayer fromPlayer, List<FastVec2i> nowVisible, List<FastVec2i> nowHidden) => Resend(fromPlayer);

    /// <summary>Right-clicking a reading deletes it on the player's own layer; colony readings stay.</summary>
    public override void OnMouseUpClient(MouseEvent args, GuiElementMap mapElem) { }

    /// <summary>Sends the player the readings of every colony they belong to.</summary>
    public void Resend(IServerPlayer player)
    {
        var manager = api.ModLoader.GetModSystem<ColonyManager>();
        if (manager == null) return;
        var readings = manager.All
            .Where(c => c.Members.Any(m => m.PlayerUid == player.PlayerUID))
            .SelectMany(c => c.OreSites)
            .Select(ToReading)
            .ToList();
        mapSink.SendMapDataToClient(this, player, SerializerUtil.Serialize(readings));
    }

    /// <summary>A colony reading in the form the game's ore map draws.</summary>
    public static PropickReading ToReading(OreSite site) => new()
    {
        Position = new Vec3d(site.Pos.X + 0.5, site.Pos.Y, site.Pos.Z + 0.5),
        OreReadings = site.Ores.ToDictionary(o => o.Code, o => new OreReading { DepositCode = o.Code, TotalFactor = o.Factor, PartsPerThousand = o.Ppt }),
        Guid = site.Pos.ToString()
    };
}

/// <summary>
/// Registers the Colony prospecting map layer. It must come after the game's own prospecting layer: the
/// game hands a player's new readings to the first ore map layer it finds, which must stay the player's.
/// The map lists it after the game's Owned creatures (position 2), with Colony markers below it.
/// </summary>
public class ColonyOreMapSystem : ModSystem
{
    public override double ExecuteOrder() => 1.1;

    public override void Start(ICoreAPI api)
    {
        api.ModLoader.GetModSystem<WorldMapManager>()?.RegisterMapLayer<ColonyOreMapLayer>(ColonyOreMapLayer.Code, 2.1);
    }
}
