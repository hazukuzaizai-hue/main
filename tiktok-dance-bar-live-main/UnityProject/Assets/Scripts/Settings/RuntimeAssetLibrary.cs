using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TikTokLiveGame
{
    internal sealed class AssetShuffle
    {
        private string[] choices = Array.Empty<string>();
        private readonly List<string> remaining = new();

        public string Draw(string[] available, string except)
        {
            if (!choices.SequenceEqual(available))
            {
                choices = available;
                remaining.Clear();
            }
            if (remaining.Count == 0) remaining.AddRange(choices);
            if (remaining.Count == 0) return null;

            int index = UnityEngine.Random.Range(0, remaining.Count);
            if (remaining[index] == except && remaining.Count > 1)
                index = (index + 1) % remaining.Count;
            string result = remaining[index];
            remaining.RemoveAt(index);
            return result;
        }
    }

    public static class RuntimeAssetLibrary
    {
        [Serializable] public class Item { public string id; public string name; public string kind; public bool enabled; public int avatarRank; public float fps = 12f; }
        [Serializable] public class Catalog { public Item[] items = Array.Empty<Item>(); public string wing; public string banner; }
        public static string Folder => GamePaths.Library;
        private static Catalog catalog = new Catalog(); private static float refreshAt;
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
        private static readonly AssetShuffle wingShuffle = new();
        private static readonly AssetShuffle avatarFrameShuffle = new();
        public static string RotateWing(string current) => wingShuffle.Draw(
            Read().items.Where(item => item.kind == "wing" && item.enabled && item.id != current).Select(item => item.id).ToArray(), current)
            ?? AssignWing(current);
        public static string PickAvatarFrame(string current) => avatarFrameShuffle.Draw(
            Read().items.Where(item => item.kind == "avatarFrame" && item.enabled).Select(item => item.id).ToArray(), current);
        public static string AssignWing(string current)
        {
            var wings = Read().items.Where(item => item.kind == "wing" && item.enabled).Select(item => item.id).ToArray();
            return wings.Contains(current) ? current : wingShuffle.Draw(wings, current);
        }
        public static Catalog Read() { if (Time.unscaledTime < refreshAt) return catalog; refreshAt = Time.unscaledTime + 2f; try { string file = Path.Combine(Folder, "library.json"); if (File.Exists(file)) catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(file)) ?? new Catalog(); } catch (Exception error) { Debug.LogWarning("Asset catalog: " + error.Message); } return catalog; }
        public static Sprite[] Load(string id)
        {
            if (!string.IsNullOrEmpty(id) && id.StartsWith("builtin:"))
            {
                string[] parts = id.Split(':'); if (parts.Length != 3) return Array.Empty<Sprite>();
                if (parts[1] == "character") return Resources.LoadAll<Sprite>("Characters/" + parts[2]).OrderBy(sprite => sprite.name).ToArray();
                Sprite sprite = Resources.Load<Sprite>("Banners/" + parts[2]); return sprite != null ? new[] { sprite } : Array.Empty<Sprite>();
            }
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out _)) return Array.Empty<Sprite>();
            if (cache.TryGetValue(id, out Sprite[] sprites)) return sprites;
            Item item = Read().items.FirstOrDefault(candidate => candidate.id == id);
            if (item == null) return Array.Empty<Sprite>();
            string category = item.kind switch
            {
                "character" => "characters",
                "wing" => "wings",
                "avatarFrame" => "avatar-frames",
                _ => "banners"
            };
            string dir = Path.Combine(Folder, category, id); if (!Directory.Exists(dir)) return Array.Empty<Sprite>();
            var result = new List<Sprite>();
            foreach (string file in Directory.GetFiles(dir, "frame_*.png").OrderBy(file => file).Take(120))
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); if (!texture.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.Destroy(texture); continue; }
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f); result.Add(sprite);
            }
            sprites = result.ToArray(); cache[id] = sprites; return sprites;
        }
        public static Sprite Selected(string kind) { Catalog data = Read(); string id = kind == "wing" ? data.wing : data.banner; if (!data.items.Any(item => item.id == id && item.enabled)) return null; Sprite[] frames = Load(id); return frames.Length > 0 ? frames[Mathf.FloorToInt(Time.unscaledTime * 12f) % frames.Length] : null; }

        public static string AvatarFrameId(int rank)
        {
            return Read().items.FirstOrDefault(item =>
                item.kind == "avatarFrame" && item.enabled && item.avatarRank == rank)?.id;
        }
    }
}
