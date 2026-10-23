using System.Collections.Generic;
using System.Linq;
using CreateAColony.Colonies;
using Vintagestory.API.Config;

namespace CreateAColony.Network;

/// <summary>The family lists of the settler window and of a Residence's Residents tab.</summary>
public partial class ColonyNetwork
{
    FamilyMemberView FamilyView(CitizenRecord c, string relation) => new()
    {
        CitizenId = c.CitizenId,
        Name = c.Name,
        Relation = relation.Length == 0 ? "" : Lang.Get("createacolony:relation-" + relation),
        Job = c.JobCode ?? "",
        Child = c.IsChild,
        GrowsUpHours = Manager.HoursToGrowUp(c),
        Alive = c.Alive
    };

    /// <summary>A settler's partner, parents, children and brothers and sisters, in that order.</summary>
    List<FamilyMemberView> FamilyOf(Colony colony, CitizenRecord citizen)
    {
        var list = new List<FamilyMemberView>();
        if (Manager.PartnerOf(colony, citizen) is { } partner) list.Add(FamilyView(partner, "partner"));
        foreach (var p in Manager.ParentsOf(colony, citizen)) list.Add(FamilyView(p, p.Sex == "female" ? "mother" : "father"));
        foreach (var c in Manager.ChildrenOf(colony, citizen)) list.Add(FamilyView(c, c.Sex == "female" ? "daughter" : "son"));
        foreach (var s in Manager.SiblingsOf(colony, citizen)) list.Add(FamilyView(s, s.Sex == "female" ? "sister" : "brother"));
        return list;
    }

    /// <summary>For a couple, whether a child may come and if not why; empty for singles and children.</summary>
    string FamilyNote(Colony colony, CitizenRecord citizen)
    {
        if (citizen.IsChild || citizen.Visitor || Manager.PartnerOf(colony, citizen) is not { Alive: true } partner) return "";
        string? blocker = Manager.ChildBlocker(colony, citizen, partner, sapi!.World.Calendar.TotalDays);
        return Lang.Get(blocker ?? "createacolony:family-may-have-child");
    }

    void AddResidence(HutSnapshot snapshot, Colony colony, Building residence)
    {
        snapshot.IsResidence = true;
        var (residents, beds, free) = Manager.ResidenceInfo(colony, residence);
        snapshot.ResidenceBeds = beds;
        snapshot.ResidenceFreeBeds = free;
        foreach (var house in Manager.Households(colony, residents))
        {
            var head = house[0];
            var view = new HouseholdView { Surname = head.Surname, Note = FamilyNote(colony, head) };
            foreach (var c in house)
            {
                string relation = c == head ? "" : c.IsChild ? (c.Sex == "female" ? "daughter" : "son") : "partner";
                view.Members.Add(FamilyView(c, relation));
            }
            snapshot.Households.Add(view);
        }
    }
}
