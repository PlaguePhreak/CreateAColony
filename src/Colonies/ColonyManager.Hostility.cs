using System;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace CreateAColony.Colonies;

/// <summary>
/// Makes the monsters guards fight (GuardTargets) go after settlers the way they go after players.
/// Their hunting tasks only look for the entity codes and tags in the game's own files, which do not
/// include settlers, so when such a creature spawns or loads, "settler" is added to the codes its
/// attack and chase tasks look for.
/// </summary>
public partial class ColonyManager
{
    const string HuntedCode = "settler";

    static readonly FieldInfo? OldBeginsWith = typeof(AiTaskBaseTargetable).GetField("targetEntityCodesBeginsWith", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo? OldFirstLetters = typeof(AiTaskBaseTargetable).GetField("targetEntityFirstLetters", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly PropertyInfo? NewConfig = typeof(AiTaskBaseTargetableR).GetProperty("Config", BindingFlags.NonPublic | BindingFlags.Instance);

    void OnCreatureReady(Entity entity)
    {
        if (!Config.HostilesAttackSettlers || entity is EntityPlayer || !IsThreat(entity)) return;
        var tasks = entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager?.AllTasks;
        if (tasks == null) return;
        foreach (var task in tasks)
        {
            string name = task.GetType().Name;
            if (!name.Contains("MeleeAttack") && !name.Contains("SeekEntity") && !name.Contains("ShootAtEntity")) continue;
            try
            {
                if (task is AiTaskBaseTargetable old) AddSettlerTarget(old);
                else if (task is AiTaskBaseTargetableR && NewConfig?.GetValue(task) is AiTaskBaseTargetableConfig config) AddSettlerTarget(config);
            }
            catch (Exception e)
            {
                Mod.Logger.Warning("Could not make {0} hunt settlers: {1}", entity.Code, e.Message);
            }
        }
    }

    static void AddSettlerTarget(AiTaskBaseTargetable task)
    {
        if (OldBeginsWith == null || OldFirstLetters == null) return;
        var codes = (string[]?)OldBeginsWith.GetValue(task) ?? Array.Empty<string>();
        var letters = (string?)OldFirstLetters.GetValue(task) ?? "";
        if (letters.Length == 0 || codes.Contains(HuntedCode)) return; // empty letters already means "anything"
        OldBeginsWith.SetValue(task, codes.Append(HuntedCode).ToArray());
        if (letters.IndexOf(HuntedCode[0]) < 0) OldFirstLetters.SetValue(task, letters + HuntedCode[0]);
    }

    /// <summary>Task configs are shared by every creature of a type, so this runs once per type in effect.</summary>
    static void AddSettlerTarget(AiTaskBaseTargetableConfig config)
    {
        if (config.TargetEverything || config.TargetEntityCodesBeginsWith.Contains(HuntedCode)) return;
        config.TargetEntityCodesBeginsWith = config.TargetEntityCodesBeginsWith.Append(HuntedCode).ToArray();
        if (config.TargetEntityFirstLetters.IndexOf(HuntedCode[0]) < 0) config.TargetEntityFirstLetters += HuntedCode[0];
    }
}
