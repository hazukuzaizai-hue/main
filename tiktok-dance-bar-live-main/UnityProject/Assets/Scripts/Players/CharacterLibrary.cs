using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TikTokLiveGame
{
    public static class CharacterLibrary
    {
        private static readonly string[] Names =
        {
            "a", "b", "c", "d", "e", "g", "h", "j", "k",
            "mushroom_dance_01", "mushroom_dance_15", "mushroom_magic_02",
            "hanhan_video_dance"
        };

        private static readonly Dictionary<string, Sprite[]> Cache = new();
        private static readonly AssetShuffle shuffle = new();

        public static (string name, Sprite[] frames) RandomCharacter(string except = null)
        {
            var items = RuntimeAssetLibrary.Read().items;
            var choices = items.Where(item => item.kind == "character" && item.enabled)
                .Select(item => item.id.StartsWith("builtin:character:", StringComparison.Ordinal)
                    ? item.id.Substring("builtin:character:".Length)
                    : "custom:" + item.id)
                .Distinct()
                .ToArray();
            if (items.Length == 0) choices = Names;
            for (int attempt = 0; attempt < choices.Length; attempt++)
            {
                string name = shuffle.Draw(choices, except);
                Sprite[] frames = Load(name);
                if (frames.Length > 0) return (name, frames);
            }
            return (except ?? "a", Load(except ?? "a"));
        }

        private static Sprite[] Load(string name)
        {
            if (name.StartsWith("builtin:character:")) return RuntimeAssetLibrary.Load(name);
            if (name.StartsWith("custom:")) return RuntimeAssetLibrary.Load(name.Substring(7));
            if (Cache.TryGetValue(name, out Sprite[] cached)) return cached;
            Sprite[] frames = Resources.LoadAll<Sprite>($"Characters/{name}")
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
            Cache[name] = frames;
            return frames;
        }
    }
}
