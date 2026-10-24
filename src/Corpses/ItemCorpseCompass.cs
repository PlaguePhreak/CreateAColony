using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace CreateAColony.Corpses;

/// <summary>
/// Right-click (or hold in the off hand) to send sparks towards the nearest corpse: your own, or one of your
/// colony's settlers. In creative it finds any corpse. Searches 7x7 chunks around you.
/// Ported from ItemCorpseCompass in Player Corpse by DArkHekRoMaNT (MIT, see THIRD_PARTY_NOTICES.md).
/// </summary>
public class ItemCorpseCompass : Item
{
    const long SearchCooldown = 5000;
    const long OffHandSearchCooldown = 10000;
    const long OffHandParticleEmitCooldown = 250;
    const int SearchRadius = 3;

    readonly SimpleParticleProperties particles = new()
    {
        MinPos = Vec3d.Zero,
        AddPos = new Vec3d(.2, .2, .2),
        MinVelocity = Vec3f.Zero,
        AddVelocity = Vec3f.Zero,
        RandomVelocityChange = true,
        Bounciness = 0.1f,
        GravityEffect = 0,
        WindAffected = false,
        WithTerrainCollision = true,
        MinSize = 0.3f,
        MaxSize = 0.8f,
        MinQuantity = 1,
        AddQuantity = 5,
        LifeLength = 1f,
        VertexFlags = 100 & VertexFlags.GlowLevelBitMask,
        ParticleModel = EnumParticleModel.Quad
    };

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        if (handling != EnumHandHandling.NotHandled) return;

        long lastSearch = slot.Itemstack.TempAttributes.GetLong("lastCorpseSearch", 0);
        if (lastSearch + SearchCooldown < api.World.ElapsedMilliseconds)
        {
            UpdateNearestCorpse(byEntity, slot);
            slot.Itemstack.TempAttributes.SetLong("lastCorpseSearch", api.World.ElapsedMilliseconds);
            handling = EnumHandHandling.PreventDefault;
        }
        EmitParticles(slot, byEntity);
    }

    public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
    {
        if (byEntity.LeftHandItemSlot == slot)
        {
            long lastSearch = slot.Itemstack.TempAttributes.GetLong("lastCorpseSearch", 0);
            if (lastSearch + OffHandSearchCooldown < api.World.ElapsedMilliseconds)
            {
                UpdateNearestCorpse(byEntity, slot);
                slot.Itemstack.TempAttributes.SetLong("lastCorpseSearch", api.World.ElapsedMilliseconds);
            }
            long lastEmit = slot.Itemstack.TempAttributes.GetLong("lastEmitParticlesOffHand", 0);
            if (lastEmit + OffHandParticleEmitCooldown < byEntity.World.ElapsedMilliseconds)
            {
                EmitParticles(slot, byEntity);
                slot.Itemstack.TempAttributes.SetLong("lastEmitParticlesOffHand", byEntity.World.ElapsedMilliseconds);
            }
        }
        base.OnHeldIdle(slot, byEntity);
    }

    void EmitParticles(ItemSlot slot, EntityAgent byEntity)
    {
        var target = slot.Itemstack.Attributes.GetVec3i("nearestCorpsePos");
        if (api.Side != EnumAppSide.Client || target == null) return;

        var targetPos = new Vec3d(target.X + .5, target.Y, target.Z + .5);
        var startPos = byEntity.SidedPos.AheadCopy(1).XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
        var toTarget = targetPos - startPos;

        particles.MinVelocity = toTarget.ToVec3f() / (particles.LifeLength * 3);
        particles.MinPos = startPos;
        particles.AddPos = particles.MinVelocity.ToVec3d() * 0.1;
        particles.MinSize = GameMath.Clamp(particles.MinVelocity.Length() * 0.01f, 0.05f, 3f);
        particles.MaxSize = particles.MinSize * 2;
        var rand = api.World.Rand;
        particles.Color = ColorUtil.ToRgba(255, rand.Next(200, 256), rand.Next(100, 156), rand.Next(0, 56));
        api.World.SpawnParticles(particles);
    }

    void UpdateNearestCorpse(EntityAgent byEntity, ItemSlot slot)
    {
        if (api is not ICoreServerAPI sapi || byEntity is not EntityPlayer playerEntity) return;

        bool creative = playerEntity.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;
        string uid = playerEntity.PlayerUID;
        var manager = sapi.ModLoader.GetModSystem<ColonyManager>();
        bool IsMine(EntityColonyCorpse corpse)
        {
            if (creative) return true;
            if (!corpse.IsSettler) return corpse.OwnerUID == uid;
            return manager?.GetById(corpse.ColonyId) is { } colony && colony.RoleOf(uid) >= ColonyRole.Member;
        }

        var nearest = CorpsesAround(sapi, byEntity.ServerPos.XYZInt)
            .Where(IsMine)
            .OrderBy(c => byEntity.Pos.SquareDistanceTo(c.Pos))
            .FirstOrDefault();

        if (nearest != null)
        {
            slot.Itemstack.Attributes.SetVec3i("nearestCorpsePos", nearest.Pos.XYZInt);
            slot.MarkDirty();
            sapi.ModLoader.GetModSystem<CorpseSystem>()?.Log("{0} found at {1}", nearest.GetName(), nearest.RelativePos());
        }
        else
        {
            slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosX");
            slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosY");
            slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosZ");
            slot.MarkDirty();
            (playerEntity.Player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("createacolony:corpsecompass-corpses-not-found"), EnumChatType.Notification);
        }
    }

    static IEnumerable<EntityColonyCorpse> CorpsesAround(ICoreServerAPI sapi, Vec3i pos)
    {
        int chunkSize = sapi.WorldManager.ChunkSize;
        int chunksInColumn = sapi.WorldManager.MapSizeY / chunkSize;
        int chunkX = pos.X / chunkSize;
        int chunkZ = pos.Z / chunkSize;

        for (int x = chunkX - SearchRadius; x <= chunkX + SearchRadius; x++)
        for (int z = chunkZ - SearchRadius; z <= chunkZ + SearchRadius; z++)
        for (int y = 0; y < chunksInColumn; y++)
        {
            var entities = sapi.WorldManager.GetChunk(x, y, z)?.Entities;
            if (entities == null) continue;
            foreach (var entity in entities)
            {
                if (entity is EntityColonyCorpse corpse) yield return corpse;
            }
        }
    }
}
