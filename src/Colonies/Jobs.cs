namespace CreateAColony.Colonies;

/// <summary>The job codes settlers can be given. The work itself arrives in later milestones.</summary>
public static class Jobs
{
    public const string Builder = "builder";
    public const string Courier = "courier";
    public const string Forester = "forester";
    public const string Farmer = "farmer";

    /// <summary>All assignable jobs; an empty code means unemployed.</summary>
    public static readonly string[] All = { "", Builder, Courier, Forester, Farmer };

    public static bool IsValid(string code) => System.Array.IndexOf(All, code) >= 0;
}
