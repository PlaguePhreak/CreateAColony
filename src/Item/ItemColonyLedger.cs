using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Items;

/// <summary>
/// The Colony Ledger: the colony's book. The first one comes free with the supply camp; another is
/// expensive to craft. Right-clicking it opens the quest book from anywhere, with or without a colony;
/// sneaking while right-clicking opens your colony's Town Hall window instead.
/// </summary>
public class ItemColonyLedger : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!firstEvent || byEntity is not EntityPlayer { Player: { } player })
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        handling = EnumHandHandling.PreventDefault;
        if (api.Side != EnumAppSide.Server || player is not IServerPlayer serverPlayer) return;

        var network = api.ModLoader.GetModSystem<Network.ColonyNetwork>();
        if (!byEntity.Controls.Sneak)
        {
            network.OpenQuestBook(serverPlayer);
            return;
        }
        var manager = api.ModLoader.GetModSystem<ColonyManager>();
        // The colony you own first, else the one you rank highest in.
        var colony = manager.HomeColony(player.PlayerUID);
        if (colony == null)
        {
            serverPlayer.SendMessage(0, Lang.GetL(serverPlayer.LanguageCode, "createacolony:ledger-no-colony"), EnumChatType.Notification);
            return;
        }
        network.OpenTownHall(serverPlayer, colony);
    }
}
