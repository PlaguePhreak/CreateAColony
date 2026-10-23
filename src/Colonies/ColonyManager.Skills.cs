using System;
using System.Collections.Generic;
using System.Linq;
using CreateAColony.Settlers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>One skill from config/skills.json.</summary>
public class SkillDef
{
    public string Code = "";
    /// <summary>The job whose work trains this skill and goes faster with it; empty for a skill everyone trains (strength, fitness).</summary>
    public string Job = "";
    /// <summary>Other jobs this skill counts for (the quarrier shares Mining with the miner).</summary>
    public string[] AlsoJobs = Array.Empty<string>();
    /// <summary>XP for one piece of work: a block built, a tree felled, a hit landed.</summary>
    public int XpPerWork = 1;
    /// <summary>Strength XP that the same piece of work gives, for heavy work.</summary>
    public int StrengthPerWork;
    /// <summary>Each usable level makes the job's work this much faster (0.04 = 4%).</summary>
    public float WorkSpeedPerLevel = 0.04f;

    public bool IsPassive => Job.Length == 0;
    public bool Covers(string? job) => job != null && job.Length > 0 && (job == Job || AlsoJobs.Contains(job));
}

/// <summary>What the settler window and the hiring lists show about one skill.</summary>
public readonly record struct SkillInfo(string Code, int Level, int Usable, int Xp, int LevelXp, int NextXp, string CapNote);

/// <summary>
/// Skills, Project Zomboid style: settlers learn by doing and get better at their job, from level 0 to 10.
/// Strength and Fitness grow for everyone. How much of a skill a settler can use is limited by the level
/// of the building they work at (or sleep in, for Strength and Fitness): a master at a level 1 hut works
/// like a level 4 one until the hut is upgraded, because skill is no use without the tools. XP keeps
/// coming past the cap, so nothing is lost. Numbers live in config/skills.json and ColonyConfig.
/// </summary>
public partial class ColonyManager
{
    const string SkillsAsset = "config/skills.json";
    public const string Strength = "strength";
    public const string Fitness = "fitness";
    /// <summary>Strength XP for carrying a full load somewhere.</summary>
    const int StrengthPerFullLoad = 1;
    /// <summary>Fitness XP per this many blocks walked.</summary>
    public const float FitnessBlocksPerXp = 50f;

    List<SkillDef>? skills;

    /// <summary>All skills, in the order of skills.json.</summary>
    public List<SkillDef> Skills()
    {
        if (skills != null) return skills;
        var loaded = sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, SkillsAsset))?.ToObject<SkillsFile>()?.Skills;
        skills = loaded?.Where(s => s.Code.Length > 0).ToList() ?? new List<SkillDef>();
        if (skills.Count == 0) Mod.Logger.Warning("No skills loaded from {0}; settlers have no skills", SkillsAsset);
        return skills;
    }

    class SkillsFile { public List<SkillDef> Skills = new(); }

    public SkillDef? Skill(string code) => Skills().Find(s => s.Code == code);

    /// <summary>The skill a job trains, or null for a job without one.</summary>
    public SkillDef? SkillForJob(string? job) => job == null ? null : Skills().Find(s => s.Covers(job));

    // ---- Levels and XP --------------------------------------------------------------------------

    public int MaxLevel => Math.Max(1, Config.SkillLevelMax);

    /// <summary>Total XP needed to reach a level: level n costs SkillXpPerLevel x n on top of the one below.</summary>
    public int XpForLevel(int level) => Math.Max(0, Config.SkillXpPerLevel) * level * (level + 1) / 2;

    /// <summary>The level this much XP has earned, at most MaxLevel.</summary>
    public int LevelOf(int xp)
    {
        int level = 0;
        while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
        return level;
    }

    public int SkillXp(CitizenRecord c, string code) => c.SkillXp.GetValueOrDefault(code);

    /// <summary>The settler's real level in a skill, whatever their building allows.</summary>
    public int SkillLevel(CitizenRecord c, string code) => LevelOf(SkillXp(c, code));

    /// <summary>Gives a settler XP in a skill. Children and the dead learn nothing. Logs level 5 and the top level.</summary>
    public void AddSkillXp(Colony colony, CitizenRecord c, string code, int amount)
    {
        if (!Config.Skills || amount <= 0 || !c.Alive || c.IsChild || Skill(code) == null) return;
        int scaled = (int)Math.Round(amount * Math.Max(0, Config.SkillXpMultiplier));
        if (scaled <= 0) return;
        int before = SkillLevel(c, code);
        c.SkillXp[code] = Math.Min(SkillXp(c, code) + scaled, XpForLevel(MaxLevel));
        int after = SkillLevel(c, code);
        if (after > before && (after == 5 || after == MaxLevel)) LogEvent(colony, "event-skill-up", c.Name, "@skill-" + code, after.ToString());
        MarkDirty(colony);
    }

    /// <summary>One piece of a job's work done: XP for its skill, plus Strength for heavy work.</summary>
    public void GainWorkXp(Colony colony, CitizenRecord c, string? job, int times = 1)
    {
        var skill = SkillForJob(job);
        if (skill == null || times <= 0) return;
        // A matching job trait (Green Thumb for a farmer) makes the skill grow faster.
        AddSkillXp(colony, c, skill.Code, (int)Math.Ceiling(skill.XpPerWork * times * TraitSkillXpFactor(c, job)));
        if (skill.StrengthPerWork > 0) AddSkillXp(colony, c, Strength, skill.StrengthPerWork * times);
    }

    /// <summary>A courier set down a load: Hauling for each stack, Strength when the pack was full.</summary>
    public void GainHaulXp(Colony colony, CitizenRecord c, int stacks, bool wasFull)
    {
        GainWorkXp(colony, c, Jobs.Courier, stacks);
        if (wasFull) AddSkillXp(colony, c, Strength, StrengthPerFullLoad);
    }

    // ---- Caps from building level ---------------------------------------------------------------

    /// <summary>The highest level a building of this level lets a settler use, from SkillCapByBuildingLevel (level 0 counts as 1).</summary>
    public int CapForBuildingLevel(int buildingLevel)
    {
        var caps = Config.SkillCapByBuildingLevel;
        if (caps == null || caps.Length == 0) return MaxLevel;
        int i = Math.Clamp(buildingLevel - 1, 0, caps.Length - 1);
        return Math.Clamp(caps[i], 0, MaxLevel);
    }

    /// <summary>The building that limits a settler's skill: their hut for a job skill, their Residence for Strength and Fitness.</summary>
    public Building? CapBuilding(Colony colony, CitizenRecord c, SkillDef skill)
    {
        if (skill.IsPassive) return c.HomeBedPos == null ? null : ResidenceAt(colony, c.HomeBedPos);
        if (skill.Covers(c.JobCode) && c.WorkplacePos != null) return GetBuilding(colony, c.WorkplacePos);
        // Not working at it: the best hut of that kind in the colony says what they could use there.
        return colony.Buildings.Where(b => skill.Covers(JobOf(b.Type))).OrderByDescending(b => b.Level).FirstOrDefault();
    }

    /// <summary>How much of a skill the settler can use where they work or live now.</summary>
    public int SkillCap(Colony colony, CitizenRecord c, SkillDef skill) => CapForBuildingLevel(CapBuilding(colony, c, skill)?.Level ?? 0);

    /// <summary>The level that counts: the lower of the real level and what the building allows.</summary>
    public int UsableLevel(Colony colony, CitizenRecord c, string code)
    {
        if (!Config.Skills || c.IsChild) return 0;
        var skill = Skill(code);
        if (skill == null) return 0;
        return Math.Min(SkillLevel(c, code), SkillCap(colony, c, skill));
    }

    // ---- Effects --------------------------------------------------------------------------------

    /// <summary>Clamps a multiplier so stacked skills can't go past SkillEffectMax (or below its inverse).</summary>
    float ClampEffect(float f) => Math.Clamp(f, 1f / Math.Max(1.01f, Config.SkillEffectMax), Math.Max(1.01f, Config.SkillEffectMax));

    /// <summary>Multiply a job's work time by this: 1 at level 0, smaller as the job's skill grows.</summary>
    public float WorkTimeFactor(Colony colony, CitizenRecord c)
    {
        var skill = SkillForJob(c.JobCode);
        float speed = Config.Skills && skill != null ? 1f + skill.WorkSpeedPerLevel * UsableLevel(colony, c, skill.Code) : 1f;
        speed *= TraitMult(c, "workSpeed"); // Hard Worker, Lazy, the job traits, Shaken
        return ClampEffect(1f / speed);
    }

    /// <summary>Divide a guard's time between swings by this: Veteran swings faster, Night Owl at night.</summary>
    public float SwingFactor(Colony colony, CitizenRecord c)
    {
        float f = TraitMult(c, "swingSpeed");
        float hour = sapi.World.Calendar.HourOfDay;
        if (!InHours(hour, Config.SleepEndHour, Config.SleepStartHour)) f *= TraitMult(c, "nightSwing");
        return ClampEffect(f);
    }

    /// <summary>Multiply a guard's damage by this: Fighting and Strength each add 3% a level.</summary>
    public float DamageFactor(Colony colony, CitizenRecord c) =>
        ClampEffect(1f + 0.03f * UsableLevel(colony, c, "fighting") + 0.03f * UsableLevel(colony, c, Strength));

    /// <summary>Multiply a settler's walking speed by this: Fitness adds 1.5% a level, Hauling 4% a level while a courier carries a load.</summary>
    public float WalkFactor(Colony colony, CitizenRecord c, bool carrying)
    {
        float f = 1f + 0.015f * UsableLevel(colony, c, Fitness);
        if (carrying && c.JobCode == Jobs.Courier) f += 0.04f * UsableLevel(colony, c, "hauling");
        f *= TraitMult(c, "walkSpeed"); // Fleet-footed, Sluggish
        return ClampEffect(f);
    }

    /// <summary>Extra max health from Fitness: one point for every two usable levels.</summary>
    public float HealthBonus(Colony colony, CitizenRecord c) => UsableLevel(colony, c, Fitness) / 2 + (float)Math.Round(TraitAdd(c, "maxHealth")); // plus Tough, minus Frail

    /// <summary>Sets a settler's max health from their Fitness; the game's health behavior adds it to the base.</summary>
    public void ApplyHealthBonus(Colony colony, CitizenRecord c, Entity entity) =>
        entity.GetBehavior<EntityBehaviorHealth>()?.SetMaxHealthModifiers("createacolony-fitness", HealthBonus(colony, c));

    // ---- Starting skills and inheritance --------------------------------------------------------

    /// <summary>
    /// Starting skills for someone from outside: 0 to 2 in everything and one background skill at 2 to 4
    /// ("was a farmer before"). The dictionary goes on a CitizenRecord or a Tavern offer.
    /// </summary>
    public Dictionary<string, int> RollStartingSkills()
    {
        var result = new Dictionary<string, int>();
        var all = Skills();
        if (all.Count == 0) return result;
        var rand = sapi.World.Rand;
        foreach (var s in all)
        {
            int level = rand.Next(3);
            if (level > 0) result[s.Code] = XpForLevel(level);
        }
        var background = all[rand.Next(all.Count)];
        result[background.Code] = XpForLevel(rand.Next(2, 5));
        return result;
    }

    /// <summary>A grown-up child starts each skill at a third of their parents' average level.</summary>
    public void InheritSkills(Colony colony, CitizenRecord child)
    {
        var parents = ParentsOf(colony, child).ToList();
        if (parents.Count == 0) return;
        foreach (var s in Skills())
        {
            int average = (int)Math.Round(parents.Average(p => SkillLevel(p, s.Code)));
            int level = average / 3;
            if (level > 0) child.SkillXp[s.Code] = Math.Max(SkillXp(child, s.Code), XpForLevel(level));
        }
    }

    /// <summary>Settlers from saves before skills existed roll their starting skills once.</summary>
    void EnsureSkills(Colony colony)
    {
        if (!Config.Skills) return;
        bool changed = false;
        foreach (var c in colony.Citizens)
        {
            if (c.SkillsRolled) continue;
            c.SkillsRolled = true;
            if (c.Alive && !c.IsChild && c.SkillXp.Count == 0) c.SkillXp = RollStartingSkills();
            changed = true;
        }
        if (changed) MarkDirty(colony);
    }

    // ---- For the windows ------------------------------------------------------------------------

    /// <summary>Every skill of a settler with level, usable level and what limits it, the job's own skill first.</summary>
    public List<SkillInfo> SkillInfos(Colony colony, CitizenRecord c)
    {
        var list = new List<SkillInfo>();
        foreach (var s in Skills())
        {
            int xp = SkillXp(c, s.Code);
            int level = LevelOf(xp);
            int cap = SkillCap(colony, c, s);
            int usable = c.IsChild ? 0 : Math.Min(level, cap);
            string note = "";
            if (level > usable && CapBuilding(colony, c, s) is { } limit) note = BuildingName(limit);
            else if (level > usable) note = s.IsPassive ? "@skill-cap-no-home" : "@skill-cap-no-hut";
            int next = level >= MaxLevel ? 0 : XpForLevel(level + 1);
            list.Add(new SkillInfo(s.Code, level, usable, xp, XpForLevel(level), next, note));
        }
        var own = SkillForJob(c.JobCode);
        return own == null ? list : list.OrderByDescending(i => i.Code == own.Code).ToList();
    }

    /// <summary>The two best skills someone has, for the hiring lists: "Farming 3, Strength 2".</summary>
    public List<(string Code, int Level)> BestSkills(Dictionary<string, int> xp, int count = 2) =>
        xp.Select(kv => (kv.Key, LevelOf(kv.Value))).Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).ThenBy(x => x.Key).Take(count).ToList();
}
