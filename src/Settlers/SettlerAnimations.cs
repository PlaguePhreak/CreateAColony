using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CreateAColony.Settlers;

/// <summary>
/// Gives settlers the player's own tool animations (chopping, digging, mining, placing). Settlers use a
/// villager shape with the same skeleton as the player, so when the game's assets are loaded the
/// player's animations are copied into the settler shapes, on both server and client. Animations the
/// settler shape already has are kept as they are.
/// </summary>
public class SettlerAnimations : ModSystem
{
    static readonly string[] SettlerShapes =
    {
        "createacolony:shapes/entity/humanoid/settler-male.json",
        "createacolony:shapes/entity/humanoid/settler-female.json"
    };

    /// <summary>Codes of the animations copied from the player, so tasks know they can use them.</summary>
    public static readonly HashSet<string> PlayerAnimations = new();

    public override double ExecuteOrder() => 0.05;

    public override void AssetsLoaded(ICoreAPI api)
    {
        var player = FindPlayerShape(api);
        if (player == null)
        {
            api.Logger.Warning("[createacolony] Player shape not found; settlers keep their villager animations.");
            return;
        }

        foreach (string path in SettlerShapes)
        {
            var asset = api.Assets.TryGet(new AssetLocation(path));
            if (asset == null) continue;
            var shape = JObject.Parse(asset.ToText());
            int added = MergeAnimations(shape, player.Value.Shape);
            asset.Data = Encoding.UTF8.GetBytes(shape.ToString(Newtonsoft.Json.Formatting.None));
            api.Logger.Notification("[createacolony] Copied {0} player animations from {1} into {2}", added, player.Value.Path, path);
        }
    }

    /// <summary>The game's player (seraph) shape: the one with the bare-hand mining animation.</summary>
    internal static (string Path, JObject Shape)? FindPlayerShape(ICoreAPI api)
    {
        var candidates = api.Assets.GetMany("shapes/entity/humanoid/", "game", loadAsset: true)
            .Where(a => a.Location.Path.Contains("seraph") && a.Location.Path.EndsWith(".json"))
            .OrderBy(a => a.Location.Path.Contains("faceless") ? 0 : 1)
            .ThenBy(a => a.Location.Path.Length);
        foreach (var asset in candidates)
        {
            string text = asset.ToText();
            if (!text.Contains("\"breakhand\"")) continue;
            try
            {
                return (asset.Location.ToString(), JObject.Parse(text));
            }
            catch (Newtonsoft.Json.JsonException)
            {
                // Not a shape we can read; try the next one.
            }
        }
        return null;
    }

    internal static int MergeAnimations(JObject shape, JObject player)
    {
        if (shape["animations"] is not JArray animations || player["animations"] is not JArray source) return 0;

        var bones = new HashSet<string>();
        CollectNames(shape["elements"], bones);
        var have = new HashSet<string>(animations.Select(a => (string?)a["code"] ?? "").Where(c => c.Length > 0));

        int added = 0;
        foreach (var anim in source.OfType<JObject>())
        {
            string? code = (string?)anim["code"];
            if (code == null || have.Contains(code) || anim["keyframes"] is not JArray keyframes) continue;

            // Keep only the bones the settler has, so the copy never points at missing parts.
            var copy = (JObject)anim.DeepClone();
            bool any = false;
            foreach (var frame in ((JArray)copy["keyframes"]!).OfType<JObject>())
            {
                if (frame["elements"] is not JObject elements) continue;
                foreach (var prop in elements.Properties().ToList())
                {
                    if (bones.Contains(prop.Name)) any = true;
                    else prop.Remove();
                }
            }
            if (!any) continue;

            animations.Add(copy);
            have.Add(code);
            PlayerAnimations.Add(code);
            added++;
        }
        return added;
    }

    static void CollectNames(JToken? elements, HashSet<string> names)
    {
        if (elements is not JArray array) return;
        foreach (var e in array.OfType<JObject>())
        {
            if ((string?)e["name"] is { } name) names.Add(name);
            CollectNames(e["children"], names);
        }
    }
}
