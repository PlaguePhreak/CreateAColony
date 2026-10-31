using System.Linq;
using CreateAColony.Colonies;

namespace CreateAColony.Network;

/// <summary>The Church tab of the Church's window (ColonyManager.Church.cs).</summary>
public partial class ColonyNetwork
{
    void AddChurch(HutSnapshot snapshot, Colony colony, Building church)
    {
        var m = Manager;
        snapshot.IsChurch = true;
        snapshot.TithePercent = colony.TithePercent;
        snapshot.ChurchCoins = m.ChurchCoins(colony, church);
        snapshot.AverageFaith = m.AverageFaith(colony);
        var people = colony.Citizens.Where(c => c.Alive && !c.Visitor).Select(c => m.FaithWord(ColonyManager.FaithOf(c))).ToList();
        snapshot.FaithCounts = new[] { "devout", "faithful", "doubting", "faithless" }.Select(w => people.Count(p => p == w)).ToList();
        int today = (int)sapi!.World.Calendar.TotalDays;
        snapshot.ServiceNow = m.ServiceUnderway(colony);
        snapshot.ServiceInDays = System.Math.Max(0, m.NextServiceDay() - today);
        snapshot.ChurchLedger = colony.ChurchLedger.AsEnumerable().Reverse().ToList();
        snapshot.Stations = m.StationsIn(colony, church).Select(ColonyManager.BuildingName).ToList();
        snapshot.Orphans = colony.Citizens.Count(c => m.IsOrphan(colony, c));
        snapshot.Sheltered = colony.Citizens.Count(c => m.InPoorhouse(colony, c));
        snapshot.AwaitingFuneral = m.FuneralsWaiting(colony);
        snapshot.ChronicleEvents = m.UnrecordedCount(colony);
        snapshot.ChronicleNeeds = m.Config.ChronicleMinEvents;
    }
}
