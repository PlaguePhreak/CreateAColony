using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CreateAColony.Settlers;

/// <summary>
/// The player's small animations, on a settler. They run on the server, so every player sees them.
/// - A torch or lantern is held up the way a player holds one. The item says which pose, as it does for a player.
///   In the right hand only lights get a pose: there the work swings take over.
/// - Standing or sitting about with nothing to do, a settler now and then looks around, scratches its head,
///   coughs or stretches, and late in the day it yawns, as an idle player does. A happy settler sometimes cheers or
///   laughs; an unhappy one cries, shrugs or facepalms, and one having a breakdown rages.
/// - In water a settler swims as a player does, and on a ladder it climbs as a player does (config SwimAndClimbLikePlayers).
/// - A settler with nothing to do waves, nods or bows to a player who comes near (config GreetPlayers).
/// - Out in the cold with nothing to do, a settler hugs itself to keep warm (config ShiverWhenCold).
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
    /// <summary>Climbing slower than this (blocks a tick) is holding on to the ladder.</summary>
    const double ClimbMotion = 0.002;
    /// <summary>A player this close to a settler with nothing to do gets a greeting.</summary>
    const double GreetRange = 6;
    /// <summary>The same settler greets the same player again only after this long.</summary>
    const long GreetAgainMs = 5 * 60_000;
    /// <summary>Colder than this (°C), outdoors, a settler with nothing to do hugs itself.</summary>
    const float ColdBelow = 2f;
    /// <summary>Seconds between looking at the weather.</summary>
    const float ColdCheckEvery = 5f;
    /// <summary>Mood (0-100) from which a settler sometimes cheers or laughs, and below which it sometimes cries or shrugs.</summary>
    const float HappyFrom = 75f, UnhappyBelow = 30f;

    public static readonly string[] Greetings = { "wave", "nod", "bow" };
    static readonly string[] HappyGestures = { "cheer", "laugh" };
    static readonly string[] SadGestures = { "cry", "shrug", "facepalm" };
    static readonly string[] BreakdownGestures = { "rage", "cry" };
    /// <summary>The animations walking tasks start (the "animation" in settler.json), which swimming and climbing take over from.</summary>
    static readonly string[] WalkAnimations = { "walk", "sprint" };
    const string Shiver = "coldidle";

    readonly EntityAgent entity;
    readonly ColonyManager? manager;
    string? rightAnim, leftAnim, fidget, moveAnim;
    float accum, still, fidgetLeft, coldCheck;
    bool cold, shivering;
    readonly Dictionary<long, long> greeted = new();

    /// <summary>Set by a walking task while it takes the settler up or down a ladder (SettlerTaskBase).</summary>
    public bool OnLadder;

    /// <summary>True while swimming or climbing: the walk animation is left off then (SettlerTaskBase.KeepWalking).</summary>
    public bool MovesOwnWay => moveAnim != null;

    public SettlerBody(EntityAgent entity, ColonyManager? manager = null)
    {
        this.entity = entity;
        this.manager = manager;
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

        var motion = entity.Pos.Motion;
        bool walking = motion.X * motion.X + motion.Z * motion.Z > StillMotion * StillMotion;
        UpdateMovement(config, walking, motion);
        bool moving = walking || entity.Controls.IsClimbing || OnLadder || entity.Swimming;
        still = moving ? 0 : still + step;
        UpdateShiver(config, moving, step);
        if (moving || fidget != null || !Restful()) return;
        if (config.GreetPlayers && Greet()) return;
        if (!config.IdleAnimations || still < StillBeforeFidget) return;
        if (entity.World.Rand.NextDouble() > step / FidgetEverySeconds) return;

        bool sitting = IsActive(SettlerAnimations.SitFloor) || IsActive(SettlerAnimations.SitSeat);
        double hour = entity.World.Calendar.HourOfDay;
        var choices = SettlerAnimations.FidgetAnimations
            .Where(code => code != "yawn" || hour >= 20 || hour < 7)
            .Where(code => code != "stretch" || !sitting)
            .Concat(MoodGestures(sitting))
            .Where(Known)
            .ToList();
        if (choices.Count == 0) return;
        StartFidget(choices[entity.World.Rand.Next(choices.Count)]);
    }

    void StartFidget(string code)
    {
        fidget = code;
        fidgetLeft = FidgetSeconds;
        entity.AnimManager.StartAnimation(code);
    }

    /// <summary>
    /// Swimming and climbing, the way a player does: "swim" while moving in water and "swimidle" while treading water,
    /// "climbup" on a ladder and "climbidle" while holding on. The walk animation stops meanwhile and comes back after.
    /// </summary>
    void UpdateMovement(ColonyConfig config, bool walking, Vec3d motion)
    {
        string? want = null;
        if (config.SwimAndClimbLikePlayers)
        {
            if (OnLadder || entity.Controls.IsClimbing) want = System.Math.Abs(motion.Y) > ClimbMotion || walking ? "climbup" : "climbidle";
            else if (entity.Swimming) want = walking ? "swim" : "swimidle";
            if (want != null && !Known(want)) want = null;
        }
        if (want != null)
        {
            foreach (string code in WalkAnimations)
            {
                if (IsActive(code)) entity.AnimManager.StopAnimation(code);
            }
            StopFidget();
        }
        if (want == moveAnim && (want == null || IsActive(want))) return;
        if (moveAnim != null) entity.AnimManager.StopAnimation(moveAnim);
        moveAnim = want;
        if (want != null) entity.AnimManager.StartAnimation(want);
    }

    /// <summary>Gestures that show how the settler feels, mixed in with the idle fidgets.</summary>
    IEnumerable<string> MoodGestures(bool sitting)
    {
        if (manager == null || entity is not EntitySettler settler || manager.FindCitizen(settler.ColonyId, settler.CitizenId) is not { } citizen) yield break;
        if (!manager.Config.Happiness || citizen.Happiness < 0) yield break;
        string[] gestures = manager.IsBrokenDown(citizen) ? BreakdownGestures
            : citizen.Happiness >= HappyFrom ? HappyGestures
            : citizen.Happiness < UnhappyBelow ? SadGestures
            : System.Array.Empty<string>();
        // Twice over, so they come up about as often as the other fidgets put together.
        foreach (string code in gestures.Where(c => c != "cheer" || !sitting))
        {
            yield return code;
            yield return code;
        }
    }

    /// <summary>Waves, nods or bows to a player who has come near, once in a while for each player.</summary>
    bool Greet()
    {
        var player = entity.World.GetNearestEntity(entity.Pos.XYZ, (float)GreetRange, (float)GreetRange, e => e is EntityPlayer { Alive: true }) as EntityPlayer;
        if (player == null) return false;
        long now = entity.World.ElapsedMilliseconds;
        if (greeted.TryGetValue(player.EntityId, out long at) && now - at < GreetAgainMs) return false;
        greeted[player.EntityId] = now;
        if (greeted.Count > 16)
        {
            foreach (long id in greeted.Where(kv => now - kv.Value >= GreetAgainMs).Select(kv => kv.Key).ToList()) greeted.Remove(id);
        }
        var choices = Greetings.Where(Known).ToList();
        if (choices.Count == 0) return false;
        // Turn to face them first; a seated settler stays facing the way it sat.
        if (!IsActive(SettlerAnimations.SitSeat))
        {
            float yaw = (float)System.Math.Atan2(player.Pos.X - entity.Pos.X, player.Pos.Z - entity.Pos.Z);
            entity.Pos.Yaw = yaw;
            entity.BodyYaw = yaw;
        }
        StartFidget(choices[entity.World.Rand.Next(choices.Count)]);
        return true;
    }

    /// <summary>Hugs itself in the cold while doing nothing outdoors, as a cold player does.</summary>
    void UpdateShiver(ColonyConfig config, bool moving, float step)
    {
        coldCheck -= step;
        if (coldCheck <= 0)
        {
            coldCheck = ColdCheckEvery;
            cold = config.ShiverWhenCold && Known(Shiver) && IsColdHere();
        }
        bool want = cold && !moving && fidget == null && still >= StillBeforeFidget && !IsActive("sleep") && Restful();
        if (want == shivering && (!want || IsActive(Shiver))) return;
        shivering = want;
        if (want) entity.AnimManager.StartAnimation(Shiver);
        else entity.AnimManager.StopAnimation(Shiver);
    }

    bool IsColdHere()
    {
        var pos = entity.Pos.AsBlockPos;
        var climate = entity.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
        if (climate == null || climate.Temperature >= ColdBelow) return false;
        // Indoors (a room with no way out to the open) is out of the wind.
        var room = entity.Api.ModLoader.GetModSystem<RoomRegistry>()?.GetRoomForPosition(pos);
        return room == null || room.ExitCount > 0;
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
        foreach (string code in new[] { "idle", SettlerAnimations.SitFloor, SettlerAnimations.SitSeat, Shiver, rightAnim ?? "", leftAnim ?? "" })
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
