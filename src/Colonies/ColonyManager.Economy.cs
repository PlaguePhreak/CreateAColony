using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using ItemMatch = System.Func<Vintagestory.API.Common.ItemStack, bool>;

namespace CreateAColony.Colonies;

/// <summary>
/// The colony's money. The Royal Mint strikes copper coins (AiTaskSettlerMinter, recipes/grid/coin.json); they go
/// to the treasury: the Town Hall's chests, the Mint's storage and the warehouse. Once a month, on payday, the
/// bailiff collects TaxPercent of every settler's purse into the treasury, then pays each settler the wage of
/// their job (config Wages) from it. Settlers keep their coins in their purse; what they spend them on comes later.
/// Taxes make settlers a little unhappy, more the higher they are, and so does a wage that was not paid.
/// </summary>
public partial class ColonyManager
{
    /// <summary>True for a coin the Royal Mint strikes.</summary>
    public static bool IsCoin(ItemStack s) => s.Collectible.Code.Domain == "createacolony" && FirstPart(s.Collectible.Code) == "coin";

    /// <summary>True once the colony has a built Royal Mint: until then there are no taxes or wages.</summary>
    public bool HasMint(Colony colony) => colony.Buildings.Any(b => b.Type == BuildingTypes.Mint && b.Level >= 1 && !b.Removing);

    /// <summary>Where the colony's coins are kept: the Town Hall's chests, the Royal Mint's storage, then the warehouse.</summary>
    List<BlockEntityContainer> TreasuryContainers(Colony colony) =>
        colony.Buildings.Where(b => b.Type == BuildingTypes.TownHall)
            .Concat(colony.Buildings.Where(b => b.Type == BuildingTypes.Mint && b.Level >= 1 && !b.Removing))
            .SelectMany(b => HutStorage(colony, b))
            .Concat(WarehouseContainers(colony))
            .Distinct().ToList();

    /// <summary>How many coins the treasury holds.</summary>
    public int TreasuryCoins(Colony colony) => CountItems(TreasuryContainers(colony), IsCoin);

    /// <summary>A job's monthly wage in coins; 0 for no job.</summary>
    public int WageOf(string? job) => job == null ? 0 : Config.Wages.TryGetValue(job, out int w) ? w : Config.WageDefault;

    /// <summary>The in-game month a moment falls in, counted from the start of the world.</summary>
    int MonthOf(double totalDays) => (int)(totalDays / Math.Max(1, sapi.World.Calendar.DaysPerMonth));

    /// <summary>The in-game day of the next payday: the first day of next month.</summary>
    public int NextPayday() => (MonthOf(sapi.World.Calendar.TotalDays) + 1) * Math.Max(1, sapi.World.Calendar.DaysPerMonth);

    /// <summary>The standing thoughts about money, for ThoughtsOf: taxes, and a wage that was not paid.</summary>
    void MoneyThoughts(Colony colony, CitizenRecord c, Action<string, int> standing)
    {
        if (c.Unpaid) standing("unpaid", Config.UnpaidMood);
        if (colony.BailiffCollectsRent && colony.TaxPercent > 0 && HasMint(colony) && BailiffOnDuty(colony) != null)
            standing("taxes", -(int)Math.Round(colony.TaxPercent * Config.TaxMoodPerPercent));
    }

    /// <summary>Settlers who pay taxes and earn a wage: grown, living members of the colony.</summary>
    static IEnumerable<CitizenRecord> Earners(Colony colony) => colony.Citizens.Where(c => c.Alive && !c.Visitor && !c.IsChild);

    /// <summary>
    /// Part of the bailiff's tick: on the first day of each month, taxes then wages. Nothing happens until the
    /// colony has a Royal Mint or coins in its treasury.
    /// </summary>
    void Payday(Colony colony, CitizenRecord bailiff, double now)
    {
        int month = MonthOf(now);
        if (colony.LastPayMonth < 0 || colony.LastPayMonth > month)
        {
            colony.LastPayMonth = month;
            MarkDirty(colony);
            return;
        }
        if (month <= colony.LastPayMonth) return;
        colony.LastPayMonth = month;
        MarkDirty(colony);
        if (!HasMint(colony) && TreasuryCoins(colony) == 0) return;

        var earners = Earners(colony).ToList();
        var day = Today(colony);
        if (colony.BailiffCollectsRent && colony.TaxPercent > 0)
        {
            int taxed = 0, payers = 0;
            foreach (var c in earners)
            {
                int tax = c.Purse * colony.TaxPercent / 100;
                if (tax <= 0) continue;
                int stored = StoreCoins(colony, tax);
                c.Purse -= stored;
                taxed += stored;
                payers++;
                if (stored < tax) break; // the treasury is full
            }
            day.Rent += taxed;
            day.Payers += payers;
            if (taxed > 0) LogEvent(colony, "event-bailiff-taxes", bailiff.Name, taxed.ToString(), payers.ToString());
        }

        if (!colony.BailiffPaysWages)
        {
            foreach (var c in earners) c.Unpaid = false;
            return;
        }
        var treasury = TreasuryContainers(colony);
        int paid = 0, unpaid = 0, paidCount = 0;
        // The bailiff pays themselves last.
        foreach (var c in earners.OrderBy(c => c.JobCode == Jobs.Bailiff))
        {
            int wage = WageOf(c.JobCode);
            if (wage <= 0)
            {
                c.Unpaid = false;
                continue;
            }
            int got = TakeCoins(treasury, wage);
            c.Purse += got;
            paid += got;
            if (got > 0) OnWagePaid(colony, c, got);
            c.Unpaid = got < wage;
            if (c.Unpaid) unpaid++;
            else
            {
                paidCount++;
                AddThought(c, "payday");
            }
        }
        day.Wages += paid;
        day.Unpaid += unpaid;
        if (paid > 0) LogEvent(colony, "event-bailiff-wages", bailiff.Name, paid.ToString(), paidCount.ToString());
        if (unpaid > 0) LogEvent(colony, "event-bailiff-unpaid", unpaid.ToString());
    }

    /// <summary>Called after a settler is paid a wage (already in their purse), for other systems to take a share of it.
    /// Left unimplemented here; a partial method with no body compiles away.</summary>
    partial void OnWagePaid(Colony colony, CitizenRecord c, int wage);

    /// <summary>Takes up to this many coins out of the treasury; returns how many it found.</summary>
    int TakeCoins(List<BlockEntityContainer> treasury, int count)
    {
        int got = 0;
        while (got < count)
        {
            var stack = TakeItems(treasury, IsCoin, count - got);
            if (stack == null || stack.StackSize <= 0) break;
            got += stack.StackSize;
        }
        return got;
    }

    /// <summary>Puts this many coins in the treasury, the Town Hall's chests first; returns how many found room.</summary>
    int StoreCoins(Colony colony, int count)
    {
        var item = sapi.World.GetItem(new AssetLocation("createacolony:coin-copper"));
        if (item == null) return 0;
        var chests = colony.Buildings.Where(b => b.Type == BuildingTypes.TownHall).SelectMany(b => HutStorage(colony, b)).ToList();
        int stored = 0;
        while (stored < count)
        {
            var stack = new ItemStack(item, Math.Min(count - stored, Math.Max(1, item.MaxStackSize)));
            int want = stack.StackSize;
            foreach (var chest in chests)
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

    // ---- The Royal Mint -----------------------------------------------------------------------

    /// <summary>A new Royal Mint keeps MintStartKeep coins on hand from the start, so the minter has work.</summary>
    void KeepCoinsAtStart(Building hut)
    {
        if (Config.MintStartKeep <= 0) return;
        foreach (var kind in HutRecipeKinds(BuildingTypes.Mint).Where(k => IsCoin(k.Sample))) hut.RecipeKeep[kind.Key] = Config.MintStartKeep;
    }

    /// <summary>What the Royal Mint does not keep: everything but its hammer, coins and what its recipes take.</summary>
    ItemMatch MintHutJunk(Colony colony, Building hut)
    {
        var recipeInputs = HutRecipeInputs(colony, hut);
        var tools = Config.HutRecipeTools.GetValueOrDefault(BuildingTypes.Mint) ?? Array.Empty<string>();
        return s => !IsCoin(s) && !tools.Contains(FirstPart(s.Collectible.Code)) && !recipeInputs(s);
    }

    /// <summary>A line about the treasury, for the hut window.</summary>
    public string MintInfo(Colony colony) => Lang.Get("createacolony:mint-treasury", TreasuryCoins(colony));
}
