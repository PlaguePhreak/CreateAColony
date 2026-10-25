using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CreateAColony.Colonies;

/// <summary>
/// The Bailiff, the lord's manager of the estate while the players are away. One settler with the Bailiff job
/// works from the Town Hall. While on duty (not knocked out or sulking) they:
/// - answer wanderers at the Town Hall as the Bailiff tab says: welcome them, welcome them while a bed is free,
///   turn them away, or leave it to the players;
/// - give jobless settlers a job at a hut with a free place, the one they are best at;
/// - oversee the fields: farmers work faster (BailiffFarmBoost), everyone else a little faster (BailiffWorkBoost);
/// - collect rent each morning from settlers with a job and a bed, in rusty gears, into the Tavern's storage
///   (to hire with), else the Town Hall's chests, else the warehouse, and keep a ledger of it;
/// - are not liked: every other settler has a small mood penalty (BailiffMood) while one watches over them.
/// The Bailiff tab also shows an estate report: every hut, who works there and what they are waiting on.
/// </summary>
public partial class ColonyManager
{
    /// <summary>How many days the bailiff's ledger keeps.</summary>
    public const int LedgerDays = 30;
    /// <summary>How often the bailiff looks for jobless settlers to put to work, in real milliseconds.</summary>
    const long BailiffJobCheckMs = 10_000;

    readonly Dictionary<string, long> lastBailiffJobCheck = new();

    /// <summary>The colony's bailiff, or null.</summary>
    public CitizenRecord? BailiffOf(Colony colony) =>
        colony.Citizens.Find(c => c.Alive && !c.Visitor && !c.IsChild && c.JobCode == Jobs.Bailiff);

    /// <summary>The bailiff when they are fit to work: not knocked out and not sulking.</summary>
    public CitizenRecord? BailiffOnDuty(Colony colony) =>
        BailiffOf(colony) is { IsDowned: false } b && !IsBrokenDown(b) ? b : null;

    /// <summary>Multiply work speed by this: farmers work faster with a bailiff overseeing the fields, the others a little.</summary>
    public float BailiffWorkFactor(Colony colony, CitizenRecord c)
    {
        if (c.JobCode == null || c.JobCode == Jobs.Bailiff || BailiffOnDuty(colony) == null) return 1f;
        return Math.Max(0.1f, c.JobCode == Jobs.Farmer ? Config.BailiffFarmBoost : Config.BailiffWorkBoost);
    }

    /// <summary>The mood points a settler has from the bailiff watching over them; 0 for the bailiff and when there is none.</summary>
    int BailiffMoodFor(Colony colony, CitizenRecord c) =>
        c.JobCode != Jobs.Bailiff && BailiffOnDuty(colony) != null ? Config.BailiffMood : 0;

    /// <summary>Part of the colony tick: the bailiff answers wanderers, hands out jobs and collects the day's rent.</summary>
    void TickBailiff(Colony colony, double now)
    {
        var bailiff = BailiffOnDuty(colony);
        if (bailiff == null) return;
        AnswerWanderers(colony, bailiff, now);

        long ms = sapi.World.ElapsedMilliseconds;
        if (colony.BailiffGivesJobs && (!lastBailiffJobCheck.TryGetValue(colony.ColonyId, out long last) || ms - last >= BailiffJobCheckMs))
        {
            lastBailiffJobCheck[colony.ColonyId] = ms;
            GiveOutJobs(colony, bailiff);
        }
        CollectRent(colony, bailiff, now);
    }

    /// <summary>Wanderers who have waited BailiffAnswerHours get the bailiff's answer, as the Bailiff tab says.</summary>
    void AnswerWanderers(Colony colony, CitizenRecord bailiff, double now)
    {
        if (colony.WandererRule == BailiffWanderers.Ask) return;
        double wait = Math.Max(0, Config.BailiffAnswerHours) / 24.0;
        foreach (var visitor in colony.Citizens.Where(c => c.Visitor && c.Alive).ToList())
        {
            double arrived = visitor.VisitorUntilTotalDays - Config.WandererWaitDays;
            if (now < arrived + wait) continue;
            bool welcome = colony.WandererRule switch
            {
                BailiffWanderers.Welcome => true,
                BailiffWanderers.IfBed => FreeBeds(colony) > 0,
                _ => false
            };
            // A full colony has no room for them whatever the rule says.
            if (welcome && Welcome(colony, visitor) == null)
            {
                Today(colony).Welcomed++;
                LogEvent(colony, "event-bailiff-welcomed", bailiff.Name, visitor.Name);
            }
            else
            {
                TurnAway(colony, visitor);
                Today(colony).TurnedAway++;
                LogEvent(colony, "event-bailiff-turned-away", bailiff.Name, visitor.Name);
            }
        }
    }

    /// <summary>Settlers the bailiff may put to work: grown, at home in the colony and without a job.</summary>
    IEnumerable<CitizenRecord> Jobless(Colony colony) =>
        colony.Citizens.Where(c => c.Alive && !c.Visitor && !c.IsChild && !c.WaitingForRoom && !c.IsDowned && c.JobCode == null);

    /// <summary>
    /// Gives each hut with a free place the jobless settler best at its job: the highest skill level, passions
    /// counting extra. Settlers whose traits rule the job out are passed over. A settler given a job on the
    /// Citizens tab who has not picked their hut yet still counts as taking a place of that job.
    /// </summary>
    void GiveOutJobs(Colony colony, CitizenRecord bailiff)
    {
        var free = Jobless(colony).ToList();
        if (free.Count == 0) return;
        var huts = colony.Buildings.Where(b => b.Level >= 1 && !b.Removing && JobOf(b.Type) != null).OrderBy(b => b.BuildingId).ToList();
        foreach (var job in huts.Select(b => JobOf(b.Type)!).Distinct().ToList())
        {
            if (free.Count == 0) break;
            var ofJob = huts.Where(b => JobOf(b.Type) == job).ToList();
            int open = ofJob.Sum(WorkerSlots) - colony.Citizens.Count(c => c.Alive && c.JobCode == job);
            while (open > 0 && free.Count > 0)
            {
                var hut = ofJob.FirstOrDefault(b => WorkersAt(colony, b).Count < WorkerSlots(b));
                if (hut == null) break;
                var pick = free.Where(c => IncapableOfJob(c, job) == null).OrderByDescending(c => JobScore(c, job)).ThenBy(c => c.CitizenId).FirstOrDefault();
                if (pick == null) break;
                free.Remove(pick);
                open--;
                pick.JobCode = job;
                pick.WorkplacePos = hut.HutPos.Copy();
                Today(colony).JobsGiven++;
                LogEvent(colony, "event-bailiff-job", bailiff.Name, pick.Name, "@job-" + job, BuildingName(hut));
                TellMembers(colony, "createacolony:bailiff-gave-job", bailiff.Name, pick.Name, Lang.Get("createacolony:job-" + job));
                MarkDirty(colony);
            }
        }
    }

    /// <summary>How good a settler would be at a job: its skill level, and a passion for it counts as more.</summary>
    int JobScore(CitizenRecord c, string job)
    {
        if (SkillForJob(job) is not { } skill) return 0;
        return SkillLevel(c, skill.Code) * 10 + c.Passions.GetValueOrDefault(skill.Code) * 15;
    }

    /// <summary>
    /// Once a day: every settler with a job and a bed pays BailiffRentPerSettler of the recruit currency. It goes to
    /// the Tavern's storage, so it can pay for hiring, else the Town Hall's chests, else the warehouse. What finds no
    /// room is owed and handed over the next day.
    /// </summary>
    void CollectRent(Colony colony, CitizenRecord bailiff, double now)
    {
        int today = (int)now;
        if (colony.LastRentDay < 0 || colony.LastRentDay > today)
        {
            colony.LastRentDay = today;
            MarkDirty(colony);
            return;
        }
        if (today <= colony.LastRentDay) return;
        colony.LastRentDay = today;
        MarkDirty(colony);
        if (!colony.BailiffCollectsRent || Config.BailiffRentPerSettler <= 0) return;

        int payers = colony.Citizens.Count(c => c.Alive && !c.Visitor && !c.IsChild && !c.IsDowned
            && c.JobCode != null && c.JobCode != Jobs.Bailiff && c.HomeBedPos != null);
        int rent = payers * Config.BailiffRentPerSettler;
        var day = Today(colony);
        day.Rent += rent;
        day.Payers = payers;
        int total = rent + colony.RentOwed;
        if (total <= 0) return;
        colony.RentOwed = total - StoreRent(colony, total);
        if (rent > 0) LogEvent(colony, "event-bailiff-rent", bailiff.Name, rent.ToString(), payers.ToString());
    }

    /// <summary>Puts this many of the recruit currency away; returns how many found room.</summary>
    int StoreRent(Colony colony, int count)
    {
        var item = sapi.World.GetItem(new AssetLocation(Config.RecruitCurrency));
        if (item == null) return 0;
        int stored = 0;
        var places = colony.Buildings.Where(b => b.Type == BuildingTypes.Tavern && b.Level >= 1 && !b.Removing)
            .Concat(colony.Buildings.Where(b => b.Type == BuildingTypes.TownHall))
            .SelectMany(b => HutStorage(colony, b)).ToList();
        while (stored < count)
        {
            var stack = new ItemStack(item, Math.Min(count - stored, Math.Max(1, item.MaxStackSize)));
            int want = stack.StackSize;
            foreach (var chest in places)
            {
                if (stack.StackSize <= 0) break;
                PutItems(chest, stack);
            }
            if (stack.StackSize > 0) PutInWarehouse(colony, stack);
            int moved = want - stack.StackSize;
            stored += moved;
            if (moved < want) break; // everything is full
        }
        return stored;
    }

    /// <summary>Today's line of the ledger, added if needed; old days drop off.</summary>
    LedgerDay Today(Colony colony)
    {
        int today = (int)sapi.World.Calendar.TotalDays;
        var day = colony.Ledger.Count > 0 && colony.Ledger[^1].Day == today ? colony.Ledger[^1] : null;
        if (day == null)
        {
            day = new LedgerDay { Day = today };
            colony.Ledger.Add(day);
            if (colony.Ledger.Count > LedgerDays) colony.Ledger.RemoveRange(0, colony.Ledger.Count - LedgerDays);
        }
        MarkDirty(colony);
        return day;
    }

    /// <summary>
    /// The estate report for the Bailiff tab: one line per hut with who works there and what they are doing or
    /// waiting on, huts nobody works at, then the fields and the jobless.
    /// </summary>
    public List<string> EstateReport(Colony colony)
    {
        var lines = new List<string>();
        foreach (var hut in colony.Buildings.Where(b => JobOf(b.Type) != null && !b.Removing).OrderBy(b => b.Level < 1).ThenBy(b => b.BuildingId))
        {
            string name = BuildingName(hut);
            if (hut.Level < 1)
            {
                lines.Add(Lang.Get("createacolony:estate-not-built", name));
                continue;
            }
            var workers = WorkersAt(colony, hut);
            if (workers.Count == 0)
            {
                lines.Add(Lang.Get("createacolony:estate-no-worker", name));
                continue;
            }
            foreach (var w in workers)
            {
                string status = WorkerStatus.TryGetValue(w.CitizenId, out var ws) ? ws : WorkStatus.TryGetValue(hut.BuildingId, out var s) ? s : "";
                string what = IsBrokenDown(w) ? Lang.Get("createacolony:estate-sulking")
                    : w.IsDowned ? Lang.Get("createacolony:estate-downed")
                    : status.Length > 0 ? Lang.Get("createacolony:workstatus-" + status) : Lang.Get("createacolony:estate-working");
                lines.Add(Lang.Get("createacolony:estate-worker", name, w.Name, what));
            }
        }
        int farmers = colony.Citizens.Count(c => c.Alive && c.JobCode == Jobs.Farmer);
        lines.Add(Lang.Get("createacolony:estate-fields", colony.Fields.Count, farmers));
        var idle = Jobless(colony).Select(c => c.Name).ToList();
        if (idle.Count > 0) lines.Add(Lang.Get("createacolony:estate-jobless", string.Join(", ", idle)));
        return lines;
    }
}
