using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace CreateAColony.Items;

/// <summary>
/// The Colony Ledger: the colony's book. The first one comes free with the supply camp; another is
/// expensive to craft. For now right-clicking it opens your colony's Town Hall window from anywhere;
/// the quest book and the colony's history pages will live in it later.
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

        var manager = api.ModLoader.GetModSystem<ColonyManager>();
        // The colony you own first, else the one you rank highest in.
        var colony = manager.All
            .Where(c => c.RoleOf(player.PlayerUID) >= ColonyRole.Member)
            .OrderByDescending(c => c.RoleOf(player.PlayerUID))
            .FirstOrDefault();
        if (colony == null)
        {
            serverPlayer.SendMessage(0, Lang.GetL(serverPlayer.LanguageCode, "createacolony:ledger-no-colony"), EnumChatType.Notification);
            return;
        }
        api.ModLoader.GetModSystem<Network.ColonyNetwork>().OpenTownHall(serverPlayer, colony);
    }
}
