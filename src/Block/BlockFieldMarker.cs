using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Blocks;

/// <summary>
/// Marks out a field for farmers, like MineColonies' scarecrow: the square of ground around it is
/// tilled, sown and harvested. Right-click it with seeds to choose the crop; sneak and right-click with
/// an empty hand to let the farmer sow whatever seeds it has.
/// </summary>
public class BlockFieldMarker : Block
{
    public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player)
        {
            return base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);
        }

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        if (colony == null || colony.RoleOf(player.PlayerUID) < ColonyRole.Member)
        {
            player.SendIngameError("createacolony-outside", Lang.Get("createacolony:marker-outside"));
            return false;
        }
        // The scarecrow is two blocks tall, so the block above has to be free.
        if (world.BlockAccessor.GetBlock(blockSel.Position.UpCopy()).Replaceable < 6000)
        {
            player.SendIngameError("createacolony-scarecrow-room", Lang.Get("createacolony:marker-no-room"));
            return false;
        }
        if (!base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)) return false;

        manager.AddField(colony, blockSel.Position);
        int size = manager.Config.FieldRadius * 2 + 1;
        player.SendMessage(0, Lang.Get("createacolony:field-placed", size), EnumChatType.Notification);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server || byPlayer is not IServerPlayer player) return true;

        var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
        var colony = manager.GetAt(blockSel.Position);
        var field = colony == null ? null : manager.FieldAt(colony, blockSel.Position);
        if (colony == null || field == null) return true;

        var held = player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        bool member = colony.RoleOf(player.PlayerUID) >= ColonyRole.Member;
        if (member && held != null && manager.IsSowable(held))
        {
            field.Seed = held.Collectible.Code.ToString();
            manager.MarkDirty(colony);
            player.SendMessage(0, Lang.Get("createacolony:field-crop-set", held.GetName()), EnumChatType.Notification);
            return true;
        }
        if (member && held == null && byPlayer.Entity.Controls.Sneak)
        {
            field.Seed = "";
            manager.MarkDirty(colony);
            player.SendMessage(0, Lang.Get("createacolony:field-crop-any"), EnumChatType.Notification);
            return true;
        }

        string crop = field.Seed.Length == 0 ? Lang.Get("createacolony:field-any-crop")
            : world.GetItem(new AssetLocation(field.Seed)) is { } seed ? new ItemStack(seed).GetName() : field.Seed;
        var farmer = colony.Citizens.Find(c => c.CitizenId == field.FarmerCitizenId && c.Alive && c.JobCode == Jobs.Farmer);
        string who = farmer?.Name ?? Lang.Get("createacolony:field-no-farmer");
        player.SendMessage(0, Lang.Get("createacolony:field-info", crop, who), EnumChatType.Notification);
        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side == EnumAppSide.Server)
        {
            var manager = world.Api.ModLoader.GetModSystem<ColonyManager>();
            var colony = manager.GetAt(pos);
            if (colony != null) manager.RemoveField(colony, pos);
        }
        base.OnBlockRemoved(world, pos);
    }
}
