using System.Collections.Generic;

namespace CreateAColony.Colonies;

public partial class ColonyManager
{
    const string PacksKey = "createacolony:assetpacks";

    Dictionary<string, string>? packs;

    Dictionary<string, string> Packs =>
        packs ??= sapi.WorldManager.SaveGame.GetData<Dictionary<string, string>>(PacksKey) ?? new Dictionary<string, string>();

    /// <summary>
    /// The asset pack (blueprint style) the player builds with, or "" when they have not picked one yet (or
    /// theirs was removed). Kept on the world, like the starter kits, so it holds before a colony exists.
    /// </summary>
    public string PackOf(string playerUid) =>
        Packs.TryGetValue(playerUid, out var pack) && BlueprintStyles.Exists(pack) ? pack : "";

    public void SetPack(string playerUid, string pack)
    {
        if (!BlueprintStyles.Exists(pack)) return;
        Packs[playerUid] = pack;
        sapi.WorldManager.SaveGame.StoreData(PacksKey, Packs);
    }
}
