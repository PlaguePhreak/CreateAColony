using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Colonies;

/// <summary>One trait from config/traits.json.</summary>
public class TraitDef
{
    public string Code = "";
    /// <summary>Plus for a good trait, minus for a bad one, 0 for a quirk.</summary>
    public int Points;
    /// <summary>How often it rolls against the others; 0 never rolls.</summary>
    public int Weight = 5;
    /// <summary>The job the trait works in; empty for one that always works.</summary>
    public string Job = "";
    /// <summary>Children can get it from a parent.</summary>
    public bool Inherited = true;
    /// <summary>Given by the game (Veteran, Shaken), never rolled or inherited.</summary>
    public bool Earned;
    public string[] Conflicts = Array.Empty<string>();
    /// <summary>Effect key to value; see traits.json for which keys multiply and which add.</summary>
    public Dictionary<string, float> Effects = new();

    public bool IsGood => Points > 0;
    public bool IsBad => Points < 0;
    public bool IsJobTrait => Job.Length > 0;
    public bool ConflictsWith(TraitDef other) => Conflicts.Contains(other.Code) || other.Conflicts.Contains(Code) || (IsJobTrait && other.IsJobTrait);
}

/// <summary>
/// Traits, Project Zomboid style: what a settler is born with. Every settler has at least one good and one
/// bad trait, 2 to 10 in all (children can inherit more). Each trait has a level 0 to 20 that grows a
/// little every day: a good trait gets stronger (5% a level), a bad one milder (4% a level). Every effect
/// is a multiplier or an adjustment on a number the mod already uses; the hooks ask TraitMult / TraitAdd.
/// Guards earn Veteran after enough kills; anyone knocked out too often is Shaken for a while.
/// </summary>
public partial class ColonyManager
{
    const string TraitsAsset = "config/traits.json";
    public const string Veteran = "veteran";
    public const string Shaken = "shaken";
    public const string Brave = "brave";
    /// <summary>Keys whose values multiply the game's number; every other key adds to it.</summary>
    static readonly HashSet<string> MultiplierKeys = new()
    {
        "workSpeed", "walkSpeed", "hunger", "downedDays", "regen", "toolWear", "variety", "willpowerWait", "swingSpeed", "nightSwing", "pairAfter", "packSlots"
    };
    /// <summary>Weights for rolling 2, 3, 4 ... 10 traits: most settlers have three to five.</summary>
    static readonly int[] TraitCountWeights = { 12, 22, 24, 18, 10, 6, 4, 2, 2 };
    /// <summary>Weights for a rolled trait's starting level 0 to 5.</summary>
    static readonly int[] StartLevelWeights = { 30, 25, 20, 12, 8, 5 };

    List<TraitDef>? traits;

    public List<TraitDef> TraitDefs()
    {
        if (traits != null) return traits;
        var loaded = sapi.Assets.TryGet(new AssetLocation(CreateAColonyModSystem.ModId, TraitsAsset))?.ToObject<TraitsFile>()?.Traits;
        traits = loaded?.Where(t => t.Code.Length > 0).ToList() ?? new List<TraitDef>();
        if (traits.Count == 0) Mod.Logger.Warning("No traits loaded from {0}; settlers have no traits", TraitsAsset);
        return traits;
    }

    class TraitsFile { public List<TraitDef> Traits = new(); }

    public TraitDef? Trait(string code) => TraitDefs().Find(t => t.Code == code);

    public int TraitLevelMax => Math.Max(0, Config.TraitLevelMax);

    // ---- A settler's traits ---------------------------------------------------------------------

    public TraitRecord? TraitOf(CitizenRecord c, string code) => c.Traits.Find(t => t.Code == code);
    public bool HasTrait(CitizenRecord c, string code) => TraitOf(c, code) != null;

    /// <summary>Sum of the points of a settler's traits: how lucky they were born.</summary>
    public int TraitScore(CitizenRecord c) => c.Traits.Sum(t => Trait(t.Code)?.Points ?? 0);

    /// <summary>
    /// True while a trait does something: a job trait only in its job, and Brave not while Shaken.
    /// Children's traits wait until they grow up.
    /// </summary>
    public bool TraitActive(CitizenRecord c, TraitRecord record)
    {
        var def = Trait(record.Code);
        if (def == null || c.IsChild) return false;
        if (def.IsJobTrait && def.Job != c.JobCode) return false;
        if (def.Code == Brave && HasTrait(c, Shaken)) return false;
        return true;
    }

    /// <summary>How much of a trait's base effect counts at a level: good ones grow, bad ones fade, never below a fifth.</summary>
    public static float LevelScale(TraitDef def, int level) =>
        def.IsBad ? Math.Max(0.2f, 1f - 0.04f * level) : 1f + 0.05f * level;

    /// <summary>A trait's value for one effect key at a level: a multiplier scaled about 1, or an adjustment scaled from 0.</summary>
    public static float EffectAt(TraitDef def, string key, int level)
    {
        if (!def.Effects.TryGetValue(key, out float value)) return MultiplierKeys.Contains(key) ? 1f : 0f;
        float scale = LevelScale(def, level);
        return MultiplierKeys.Contains(key) ? 1f + (value - 1f) * scale : value * scale;
    }

    IEnumerable<(TraitDef Def, TraitRecord Record)> ActiveTraits(CitizenRecord c)
    {
        if (!Config.Traits) yield break;
        foreach (var record in c.Traits)
        {
            if (Trait(record.Code) is { } def && TraitActive(c, record)) yield return (def, record);
        }
    }

    /// <summary>The product of every active trait's multiplier for a key, kept between half and double.</summary>
    public float TraitMult(CitizenRecord c, string key)
    {
        float f = 1f;
        foreach (var (def, record) in ActiveTraits(c))
        {
            if (def.Effects.ContainsKey(key)) f *= EffectAt(def, key, record.Level);
        }
        return ClampEffect(f);
    }

    /// <summary>The sum of every active trait's adjustment for a key.</summary>
    public float TraitAdd(CitizenRecord c, string key)
    {
        float sum = 0f;
        foreach (var (def, record) in ActiveTraits(c))
        {
            if (def.Effects.ContainsKey(key)) sum += EffectAt(def, key, record.Level);
        }
        return sum;
    }

    /// <summary>A yes-or-no trait effect: Loner's noPair, Cowardly's flees.</summary>
    public bool TraitFlag(CitizenRecord c, string key) => TraitAdd(c, key) > 0;

    // ---- Rolling --------------------------------------------------------------------------------

    static T Weighted<T>(Random rand, IList<T> items, Func<T, int> weight)
    {
        int total = items.Sum(i => Math.Max(0, weight(i)));
        int pick = rand.Next(Math.Max(1, total));
        foreach (var item in items)
        {
            pick -= Math.Max(0, weight(item));
            if (pick < 0) return item;
        }
        return items[^1];
    }

    int RollStartLevel()
    {
        int i = Weighted(sapi.World.Rand, Enumerable.Range(0, StartLevelWeights.Length).ToList(), i => StartLevelWeights[i]);
        return Math.Min(i, TraitLevelMax);
    }

    bool FitsWith(TraitDef candidate, List<TraitRecord> have) =>
        have.All(h => h.Code != candidate.Code && (Trait(h.Code) is not { } other || !candidate.ConflictsWith(other)));

    /// <summary>
    /// Traits for someone from outside: TraitsMin to TraitsMax of them, at least TraitsMinGood good and
    /// TraitsMinBad bad, no two that clash, at most one job trait, each at a starting level of 0 to 5.
    /// </summary>
    public List<TraitRecord> RollTraits()
    {
        var result = new List<TraitRecord>();
        if (!Config.Traits) return result;
        var pool = TraitDefs().Where(t => !t.Earned && t.Weight > 0).ToList();
        if (pool.Count == 0) return result;
        var rand = sapi.World.Rand;

        int min = Math.Max(1, Config.TraitsMin);
        int max = Math.Max(min, Config.TraitsMax);
        var counts = Enumerable.Range(min, max - min + 1).ToList();
        int count = Weighted(rand, counts, n => n - 2 >= 0 && n - 2 < TraitCountWeights.Length ? TraitCountWeights[n - 2] : 1);

        void Add(Func<TraitDef, bool> filter)
        {
            var fits = pool.Where(t => filter(t) && FitsWith(t, result)).ToList();
            if (fits.Count == 0) return;
            result.Add(new TraitRecord { Code = Weighted(rand, fits, t => t.Weight).Code, Level = RollStartLevel() });
        }
        for (int i = 0; i < Math.Max(0, Config.TraitsMinGood); i++) Add(t => t.IsGood);
        for (int i = 0; i < Math.Max(0, Config.TraitsMinBad); i++) Add(t => t.IsBad);
        while (result.Count < count)
        {
            int before = result.Count;
            Add(_ => true);
            if (result.Count == before) break;
        }
        return result;
    }

    /// <summary>A job trait's skill starts higher: two levels up (three at trait level 20) on top of the rolled skills.</summary>
    public void ApplyTraitSkillStart(Dictionary<string, int> skillXp, List<TraitRecord> rolled)
    {
        foreach (var record in rolled)
        {
            if (Trait(record.Code) is not { IsJobTrait: true } def || SkillForJob(def.Job) is not { } skill) continue;
            int bonus = record.Level >= TraitLevelMax && TraitLevelMax > 0 ? 3 : 2;
            int level = Math.Min(MaxLevel, LevelOf(skillXp.GetValueOrDefault(skill.Code)) + bonus);
            skillXp[skill.Code] = XpForLevel(level);
        }
    }

    /// <summary>Job traits make the matching skill grow half again as fast (twice as fast at trait level 20).</summary>
    public float TraitSkillXpFactor(CitizenRecord c, string? job)
    {
        if (job == null) return 1f;
        foreach (var (def, record) in ActiveTraits(c))
        {
            if (def.IsJobTrait && def.Job == job) return TraitLevelMax > 0 && record.Level >= TraitLevelMax ? 2f : 1.5f;
        }
        return 1f;
    }

    // ---- Inheritance ----------------------------------------------------------------------------

    /// <summary>
    /// A child's traits from its parents: a trait both have is passed on TraitInheritShared of the time
    /// (at the average of their levels), one parent's TraitInheritSingle of the time (at half that level),
    /// plus TraitNewPerChild new ones at level 0. Clashes go to the shared or higher-level one. A child
    /// always ends up with at least one good and one bad trait, and up to TraitsChildMax in all.
    /// </summary>
    public void InheritTraits(Colony colony, CitizenRecord child)
    {
        if (!Config.Traits) return;
        var parents = ParentsOf(colony, child).ToList();
        var rand = sapi.World.Rand;
        var result = new List<TraitRecord>();
        int cap = Math.Max(2, Config.TraitsChildMax);

        var candidates = parents.SelectMany(p => p.Traits).Where(t => Trait(t.Code) is { Inherited: true, Earned: false })
            .GroupBy(t => t.Code)
            .Select(g => (Code: g.Key, Shared: g.Count() >= 2 && parents.Count >= 2, Level: g.Count() >= 2 ? (int)Math.Round(g.Average(t => t.Level)) : g.First().Level / 2))
            // Shared traits first, then the higher levels, so they win a clash.
            .OrderByDescending(x => x.Shared).ThenByDescending(x => x.Level).ThenBy(_ => rand.Next())
            .ToList();
        foreach (var (code, shared, level) in candidates)
        {
            if (result.Count >= cap) break;
            if (rand.NextDouble() >= (shared ? Config.TraitInheritShared : Config.TraitInheritSingle)) continue;
            var def = Trait(code)!;
            if (!FitsWith(def, result)) continue;
            result.Add(new TraitRecord { Code = code, Level = Math.Clamp(level, 0, TraitLevelMax) });
        }

        var pool = TraitDefs().Where(t => !t.Earned && t.Weight > 0).ToList();
        void AddNew(Func<TraitDef, bool> filter)
        {
            if (result.Count >= cap) return;
            var fits = pool.Where(t => filter(t) && FitsWith(t, result)).ToList();
            if (fits.Count > 0) result.Add(new TraitRecord { Code = Weighted(rand, fits, t => t.Weight).Code });
        }
        for (int i = 0; i < Math.Max(0, Config.TraitNewPerChild); i++) AddNew(_ => true);
        if (!result.Any(t => Trait(t.Code)?.IsGood == true)) AddNew(t => t.IsGood);
        if (!result.Any(t => Trait(t.Code)?.IsBad == true)) AddNew(t => t.IsBad);

        child.Traits = result;
        child.TraitsRolled = true;
    }

    // ---- Gaining and losing traits --------------------------------------------------------------

    /// <summary>Gives a settler a trait (replacing one they already have) and tells the colony when asked.</summary>
    public bool AddTrait(Colony colony, CitizenRecord c, string code, int level = 0, bool announce = true)
    {
        var def = Trait(code);
        if (def == null) return false;
        c.Traits.RemoveAll(t => t.Code == code);
        c.Traits.Add(new TraitRecord { Code = code, Level = Math.Clamp(level, 0, TraitLevelMax) });
        MarkDirty(colony);
        if (announce)
        {
            TellMembers(colony, "createacolony:trait-gained", c.Name, Lang.Get("createacolony:trait-" + code));
            LogEvent(colony, "event-trait-gained", c.Name, "@trait-" + code);
        }
        return true;
    }

    public bool RemoveTrait(Colony colony, CitizenRecord c, string code, bool announce = true)
    {
        if (c.Traits.RemoveAll(t => t.Code == code) == 0) return false;
        MarkDirty(colony);
        if (announce) LogEvent(colony, "event-trait-lost", c.Name, "@trait-" + code);
        return true;
    }

    /// <summary>A guard killed something: after TraitVeteranKills kills they are a Veteran for life.</summary>
    public void CountKill(Colony colony, CitizenRecord c)
    {
        c.Kills++;
        if (Config.Traits && c.Kills >= Math.Max(1, Config.TraitVeteranKills) && !HasTrait(c, Veteran) && Trait(Veteran) != null) AddTrait(colony, c, Veteran);
        MarkDirty(colony);
    }

    /// <summary>A settler was knocked out: TraitShakenCount times within TraitShakenDays leaves them Shaken for as long again.</summary>
    public void CountKnockOut(Colony colony, CitizenRecord c, double now)
    {
        double window = Math.Max(0.1, Config.TraitShakenDays);
        c.DownedDays.Add(now);
        c.DownedDays.RemoveAll(d => now - d > window);
        if (!Config.Traits || Trait(Shaken) == null) return;
        if (c.DownedDays.Count >= Math.Max(1, Config.TraitShakenCount))
        {
            c.ShakenUntilTotalDays = now + window;
            if (!HasTrait(c, Shaken)) AddTrait(colony, c, Shaken);
        }
    }

    /// <summary>
    /// Part of the colony tick, once per in-game day: every grown-up's traits earn 1 XP, 2 more when the
    /// trait was in use, and level up when they have enough. Shaken fades after its quiet days.
    /// </summary>
    void TickTraits(Colony colony, double now)
    {
        if (!Config.Traits) return;
        int today = (int)now;
        if (colony.LastTraitDay == today) return;
        bool first = colony.LastTraitDay < 0;
        colony.LastTraitDay = today;
        MarkDirty(colony);
        if (first) return;

        foreach (var c in colony.Citizens)
        {
            if (!c.Alive || c.Visitor || c.IsChild) continue;
            if (HasTrait(c, Shaken) && now >= c.ShakenUntilTotalDays) RemoveTrait(colony, c, Shaken);
            if (c.IsDowned) continue;
            foreach (var record in c.Traits)
            {
                if (Trait(record.Code) is not { } def || record.Level >= TraitLevelMax) continue;
                record.Xp += TraitActive(c, record) ? 3 : 1;
                int cost = (record.Level + 1) * Math.Max(1, Config.TraitXpPerLevel);
                while (record.Level < TraitLevelMax && record.Xp >= cost)
                {
                    record.Xp -= cost;
                    record.Level++;
                    cost = (record.Level + 1) * Math.Max(1, Config.TraitXpPerLevel);
                    if (record.Level == TraitLevelMax) LogEvent(colony, "event-trait-max", c.Name, "@trait-" + def.Code);
                }
            }
        }
    }

    /// <summary>Settlers from saves before traits existed roll theirs once; children wait for their grow-up roll.</summary>
    void EnsureTraits(Colony colony)
    {
        if (!Config.Traits) return;
        bool changed = false;
        foreach (var c in colony.Citizens)
        {
            if (c.TraitsRolled) continue;
            c.TraitsRolled = true;
            if (c.Alive && !c.IsChild && c.Traits.Count == 0) c.Traits = RollTraits();
            changed = true;
        }
        if (changed) MarkDirty(colony);
    }

    // ---- Recruiting -----------------------------------------------------------------------------

    /// <summary>A Tavern recruit's price: the rolled base, plus TraitGearsPerPoint for each trait point and one more for every 10 good-trait levels, never under 2.</summary>
    public int RecruitPrice(int baseCost, List<TraitRecord> traits)
    {
        if (!Config.Traits) return baseCost;
        int points = traits.Sum(t => Trait(t.Code)?.Points ?? 0);
        int goodLevels = traits.Where(t => Trait(t.Code)?.IsGood == true).Sum(t => t.Level);
        return Math.Max(2, baseCost + points * Config.TraitGearsPerPoint + goodLevels / 10);
    }

    // ---- For the windows ------------------------------------------------------------------------

    /// <summary>"+15%" for a multiplier, "+6" or "-2" for an adjustment, formatted for the hover text.</summary>
    static string FormatEffect(string key, float value)
    {
        if (MultiplierKeys.Contains(key))
        {
            int percent = (int)Math.Round((value - 1f) * 100);
            return (percent >= 0 ? "+" : "") + percent + "%";
        }
        if (key is "reckless" or "retreatHealth" or "childChance" or "freeBlock")
        {
            int percent = (int)Math.Round(value * 100);
            return (percent >= 0 ? "+" : "") + percent + "%";
        }
        string number = Math.Abs(value - (float)Math.Round(value)) < 0.01f ? ((int)Math.Round(value)).ToString() : value.ToString("0.#", CultureInfo.InvariantCulture);
        return (value >= 0 ? "+" : "") + number;
    }

    /// <summary>One line per effect of a trait at a level, in words from the lang file.</summary>
    public string TraitEffectText(TraitDef def, int level)
    {
        var lines = new List<string>();
        foreach (var key in def.Effects.Keys)
        {
            float value = EffectAt(def, key, level);
            if (key is "noPair" or "pairFirst" or "flees") lines.Add(Lang.Get("createacolony:trait-effect-" + key));
            else lines.Add(Lang.Get("createacolony:trait-effect-" + key, FormatEffect(key, value)));
        }
        return string.Join("\n", lines);
    }

    /// <summary>"Hard Worker 3, Lazy" for a short list, good traits first.</summary>
    public string TraitsText(CitizenRecord c)
    {
        if (!Config.Traits || c.Traits.Count == 0) return "";
        var ordered = c.Traits.OrderByDescending(t => Trait(t.Code)?.Points ?? 0).ThenBy(t => t.Code);
        return string.Join(", ", ordered.Select(t => t.Level > 0 ? Lang.Get("createacolony:trait-level", Lang.Get("createacolony:trait-" + t.Code), t.Level) : Lang.Get("createacolony:trait-" + t.Code)));
    }

    /// <summary>A settler's job trait, when it matches the job given (for the worker lists), or empty.</summary>
    public string JobTraitFor(CitizenRecord c, string job)
    {
        if (!Config.Traits) return "";
        foreach (var record in c.Traits)
        {
            if (Trait(record.Code) is { IsJobTrait: true } def && def.Job == job) return def.Code;
        }
        return "";
    }
}
