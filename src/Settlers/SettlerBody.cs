using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace CreateAColony.Settlers;

/// <summary>
/// The player's small animations, on a settler. They run on the server, so every player sees them.
/// - A torch or lantern is held up the way a player holds one. The item says which pose, as it does for a player.
///   In the right hand only lights get a pose: there the work swings take over.
/// - Standing or sitting about with nothing to do, a settler now and then looks around, scratches its head,
///   coughs or stretches, and late in the day it yawns, as an idle player does.
/// </summary>
public class SettlerBody
{
    const float CheckEvery = 0.25f;
    /// <summary>Seconds of standing still before a settler starts to fidget.</summary>
    const float StillBeforeFidget = 4f;
    /// <summary>Fidgets come about this many seconds apart, on average.</summary>
    const float FidgetEverySeconds = 18f;
    /// <summary>A fidget is stopped after this long; it has played out by then.</summary>
    const float FidgetSeconds = 4f;
    /// <summary>Moving faster than this (blocks a tick) is not standing still.</summary>
    const double StillMotion = 0.003;

    readonly EntityAgent entity;
    string? rightAnim, leftAnim, fidget;
    float accum, still, fidgetLeft;

    public SettlerBody(EntityAgent entity)
    {
        this.entity = entity;
    }

    public void Tick(float dt, ColonyConfig? config)
    {
        accum += dt;
        if (accum < CheckEvery) return;
        float step = accum;
        accum = 0;
        if (!entity.Alive || config == null) return;

        // Asleep the torch stays down; the game's own check looks for the player's "sleep", which settlers lack.
        bool lights = config.HoldLightsLikePlayers && !IsActive("sleep");
        UpdateHand(lights ? entity.RightHandItemSlot : null, EnumHand.Right, ref rightAnim);
        UpdateHand(lights ? entity.LeftHandItemSlot : null, EnumHand.Left, ref leftAnim);

        if (fidget != null)
        {
            fidgetLeft -= step;
            if (fidgetLeft <= 0) StopFidget();
        }

        var motion = entity.ServerPos.Motion;
        bool moving = motion.X * motion.X + motion.Z * motion.Z > StillMotion * StillMotion || entity.Controls.IsClimbing || entity.Swimming;
        still = moving ? 0 : still + step;
        if (moving || !config.IdleAnimations || fidget != null || still < StillBeforeFidget || !Restful()) return;
        if (entity.World.Rand.NextDouble() > step / FidgetEverySeconds) return;

        bool sitting = IsActive(SettlerAnimations.SitFloor) || IsActive(SettlerAnimations.SitSeat);
        double hour = entity.World.Calendar.HourOfDay;
        var choices = SettlerAnimations.FidgetAnimations
            .Where(code => code != "yawn" || hour >= 20 || hour < 7)
            .Where(code => code != "stretch" || !sitting)
            .Where(Known)
            .ToList();
        if (choices.Count == 0) return;
        fidget = choices[entity.World.Rand.Next(choices.Count)];
        fidgetLeft = FidgetSeconds;
        entity.AnimManager.StartAnimation(fidget);
    }

    /// <summary>Stops a fidget straight away, for a task that starts something else (work, a walk).</summary>
    public void StopFidget()
    {
        if (fidget != null) entity.AnimManager.StopAnimation(fidget);
        fidget = null;
    }

    void UpdateHand(ItemSlot? slot, EnumHand hand, ref string? running)
    {
        string? want = null;
        if (slot?.Itemstack?.Collectible is { } held)
        {
            try
            {
                want = held.GetHeldTpIdleAnimation(slot, entity, hand);
            }
            catch (System.Exception)
            {
                // Some items expect a player holding them; those get no pose.
                want = null;
            }
            if (hand == EnumHand.Right && want?.StartsWith("holdinglantern") != true) want = null;
            if (want != null && !Known(want)) want = null;
        }
        if (want == running && (want == null || IsActive(want))) return;
        if (running != null) entity.AnimManager.StopAnimation(running);
        running = want;
        if (want != null) entity.AnimManager.StartAnimation(want);
    }

    /// <summary>True when nothing but sitting, holding a light or idle fidgets is playing: the settler is doing nothing.</summary>
    bool Restful()
    {
        var calm = new HashSet<string>();
        foreach (string code in new[] { "idle", SettlerAnimations.SitFloor, SettlerAnimations.SitSeat, rightAnim ?? "", leftAnim ?? "" })
        {
            if (Meta(code) is { } meta) calm.Add(meta.Animation);
        }
        return entity.AnimManager.ActiveAnimationsByAnimCode.Keys.All(calm.Contains);
    }

    AnimationMetaData? Meta(string code) =>
        code.Length > 0 && entity.Properties.Client.AnimationsByMetaCode.TryGetValue(code, out var meta) ? meta : null;

    bool Known(string code) => Meta(code) != null;

    bool IsActive(string code) => Meta(code) is { } meta && entity.AnimManager.IsAnimationActive(meta.Animation);
}
