namespace CreateAColony.Colonies;

/// <summary>The job codes settlers can be given.</summary>
public static class Jobs
{
    public const string Builder = "builder";
    public const string Courier = "courier";
    public const string Forester = "forester";
    public const string Farmer = "farmer";
    public const string Guard = "guard";

    /// <summary>All assignable jobs; an empty code means unemployed.</summary>
    public static readonly string[] All = { "", Builder, Courier, Forester, Farmer, Guard };

    public static bool IsValid(string code) => System.Array.IndexOf(All, code) >= 0;

    /// <summary>Jobs that keep their own tools, weapons and supplies in their inventory, so couriers leave it alone.</summary>
    public static bool CarriesOwnLoad(string? code) => code is Builder or Forester or Farmer or Guard;
}
