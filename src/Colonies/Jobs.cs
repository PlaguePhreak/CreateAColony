namespace CreateAColony.Colonies;

/// <summary>The job codes settlers can be given.</summary>
public static class Jobs
{
    public const string Builder = "builder";
    public const string Courier = "courier";
    public const string Forester = "forester";
    public const string Farmer = "farmer";
    public const string Guard = "guard";
    public const string Potter = "potter";
    public const string Cook = "cook";
    public const string Prospector = "prospector";
    public const string Miner = "miner";
    public const string Quarrier = "quarrier";
    public const string Smelter = "smelter";
    public const string Blacksmith = "blacksmith";

    /// <summary>All assignable jobs; an empty code means unemployed.</summary>
    public static readonly string[] All = { "", Builder, Courier, Forester, Farmer, Guard, Potter, Cook, Prospector, Miner, Quarrier, Smelter, Blacksmith };

    public static bool IsValid(string code) => System.Array.IndexOf(All, code) >= 0;

    /// <summary>Jobs that keep their own tools, weapons and supplies in their inventory, so couriers leave it alone.</summary>
    public static bool CarriesOwnLoad(string? code) => code is Builder or Forester or Farmer or Guard or Potter or Cook or Prospector or Miner or Quarrier or Smelter or Blacksmith;
}
