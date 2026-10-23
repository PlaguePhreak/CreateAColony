using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CreateAColony.Settlers;

/// <summary>
/// Makes settlers look like players. When the game's assets are loaded (on server and client alike, before
/// entity types are read), the player's entity type is read and copied into the settler entity types:
/// - the model: a settler shape made from the player's own model, with the settler animations added;
/// - the player's textures;
/// - the skin parts of the player character creator (skin, eyes, hair, beard...) and its "extraskinnable"
///   behavior, so each settler gets a random player look the way a new player does.
/// Clothes come from the "seraphinventory" behavior in the settler files (see ColonyManager.Gear.cs).
/// If the player's entity cannot be read, settlers keep the older villager model written in their files.
/// </summary>
public class SettlerLooks : ModSystem
{
    const string SettlerShape = "createacolony:shapes/entity/humanoid/settler-seraph.json";
    const string SettlerShapeBase = "createacolony:entity/humanoid/settler-seraph";
    static readonly string[] SettlerTypes = { "createacolony:entities/settler.json", "createacolony:entities/settlerchild.json" };
    /// <summary>Animation source: the settler model whose animations (balanced-idle, balanced-walk...) the settler files use.</summary>
    const string AnimationSource = "createacolony:shapes/entity/humanoid/settler-male.json";
    /// <summary>Player attributes the skin and clothing behaviors read.</summary>
    static readonly string[] PlayerAttributes = { "skinnableParts", "mainTextureCode", "skinBaseTextureKey", "disableElements" };
    /// <summary>Keys whose string values are asset paths, which need the game's domain written out once copied into this mod.</summary>
    static readonly HashSet<string> PathKeys = new() { "base", "texture", "textureTemplate", "sound", "overlays" };

    /// <summary>True once the settlers were given the player's look, on this side.</summary>
    public static bool PlayerLook { get; private set; }

    // After the game's JSON patches (0.05) and the settler animations (0.05), before entity types are read (0.2).
    public override double ExecuteOrder() => 0.15;

    public override void AssetsLoaded(ICoreAPI api)
    {
        try
        {
            PlayerLook = Apply(api);
        }
        catch (Exception e)
        {
            api.Logger.Error("[createacolony] Could not give settlers the player's look, they keep the villager model: {0}", e);
            PlayerLook = false;
        }
    }

    static bool Apply(ICoreAPI api)
    {
        var player = FindPlayerType(api);
        if (player == null)
        {
            api.Logger.Warning("[createacolony] The player entity type was not found; settlers keep the villager model.");
            return false;
        }
        var client = player["client"] as JObject;
        var attributes = player["attributes"] as JObject;
        string? shapeBase = (string?)client?["shape"]?["base"];
        var textures = client?["textures"] as JObject
            ?? (client?["texture"] is JObject single ? new JObject { [(string?)attributes?["mainTextureCode"] ?? "seraph"] = single } : null);
        if (shapeBase == null || textures == null || attributes?["skinnableParts"] == null)
        {
            api.Logger.Warning("[createacolony] The player entity type has no model, textures or skin parts where expected; settlers keep the villager model.");
            return false;
        }

        if (!BuildShape(api, shapeBase)) return false;

        var copiedTextures = TexturesWithDomain(textures);
        foreach (string path in SettlerTypes)
        {
            var asset = api.Assets.TryGet(new AssetLocation(path));
            if (asset == null) continue;
            var type = asset.ToObject<JObject>();
            if (type["client"] is not JObject settlerClient) continue;

            settlerClient["shape"] = new JObject { ["base"] = SettlerShapeBase };
            settlerClient["textures"] = copiedTextures.DeepClone();
            var settlerAttributes = type["attributes"] as JObject ?? new JObject();
            foreach (string key in PlayerAttributes)
            {
                if (attributes![key] is { } value) settlerAttributes[key] = GameDomain(value.DeepClone());
                else settlerAttributes.Remove(key);
            }
            type["attributes"] = settlerAttributes;
            AddSkinBehavior(settlerClient["behaviors"] as JArray);
            AddSkinBehavior(type["server"]?["behaviors"] as JArray);

            asset.Data = Encoding.UTF8.GetBytes(type.ToString(Newtonsoft.Json.Formatting.None));
        }
        api.Logger.Notification("[createacolony] Settlers use the player model {0} and the player character creator's skin parts.", shapeBase);
        return true;
    }

    /// <summary>The game's player entity type, as JSON.</summary>
    static JObject? FindPlayerType(ICoreAPI api)
    {
        var known = api.Assets.TryGet(new AssetLocation("game:entities/humanoid/player.json"));
        var candidates = known != null ? new[] { known } : api.Assets.GetMany("entities/", "game", loadAsset: true).Where(a => a.Location.Path.EndsWith(".json"));
        foreach (var asset in candidates)
        {
            JObject type;
            try
            {
                type = asset.ToObject<JObject>();
            }
            catch (Exception)
            {
                continue;
            }
            if ((string?)type["code"] == "player") return type;
        }
        return null;
    }

    /// <summary>
    /// Writes the settler shape: the player's model plus the settler animations it lacks, keeping only the
    /// bones the player model has. False if the player's model could not be read.
    /// </summary>
    static bool BuildShape(ICoreAPI api, string shapeBase)
    {
        var location = AssetLocation.Create(shapeBase, "game").WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        var playerShape = api.Assets.TryGet(location);
        var target = api.Assets.TryGet(new AssetLocation(SettlerShape));
        if (playerShape == null || target == null)
        {
            api.Logger.Warning("[createacolony] Player model {0} not found; settlers keep the villager model.", location);
            return false;
        }

        var shape = JObject.Parse(playerShape.ToText());
        if (shape["animations"] is JArray own)
        {
            foreach (var anim in own.OfType<JObject>())
            {
                if ((string?)anim["code"] is { } code) SettlerAnimations.PlayerAnimations.Add(code);
            }
        }
        if (api.Assets.TryGet(new AssetLocation(AnimationSource)) is { } source)
        {
            int added = SettlerAnimations.MergeAnimations(shape, JObject.Parse(source.ToText()));
            api.Logger.Notification("[createacolony] Added {0} settler animations to the player model.", added);
        }
        if (shape["textures"] is JObject shapeTextures) shape["textures"] = TexturesWithDomain(shapeTextures);
        target.Data = Encoding.UTF8.GetBytes(shape.ToString(Newtonsoft.Json.Formatting.None));
        return true;
    }

    /// <summary>Adds the player character creator's behavior after the clothing behavior it needs.</summary>
    static void AddSkinBehavior(JArray? behaviors)
    {
        if (behaviors == null || behaviors.Any(b => (string?)b["code"] == "extraskinnable")) return;
        int at = behaviors.ToList().FindIndex(b => (string?)b["code"] == "seraphinventory");
        var entry = new JObject { ["code"] = "extraskinnable" };
        if (at < 0) behaviors.Add(entry);
        else behaviors.Insert(at + 1, entry);
    }

    /// <summary>Gives asset paths in copied player JSON the game's domain, since in this mod's files a bare path means this mod.</summary>
    static JToken GameDomain(JToken token, string? key = null)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var prop in obj.Properties().ToList()) prop.Value = GameDomain(prop.Value, prop.Name);
                return obj;
            case JArray array:
                for (int i = 0; i < array.Count; i++) array[i] = GameDomain(array[i], key);
                return array;
            case JValue { Type: JTokenType.String } value when key != null && PathKeys.Contains(key):
                return Prefixed(value);
        }
        return token;
    }

    /// <summary>A texture dictionary, where a value is either a path or an object with a "base" path.</summary>
    static JObject TexturesWithDomain(JObject textures)
    {
        var copy = (JObject)textures.DeepClone();
        foreach (var prop in copy.Properties().ToList())
        {
            prop.Value = prop.Value is JValue { Type: JTokenType.String } path ? Prefixed(path) : GameDomain(prop.Value);
        }
        return copy;
    }

    static JValue Prefixed(JValue value)
    {
        string text = (string)value!;
        return text.Length > 0 && !text.Contains(':') ? new JValue("game:" + text) : value;
    }
}
