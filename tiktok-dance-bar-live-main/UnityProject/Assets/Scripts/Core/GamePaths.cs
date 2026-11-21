using System.IO;
using UnityEngine;

namespace TikTokLiveGame
{
    /// <summary>All mutable game data lives outside the replaceable Build folder.</summary>
    public static class GamePaths
    {
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string Data => Path.Combine(Root, "Data");
        public static string Music => Path.Combine(Data, "Music");
        public static string Videos => Path.Combine(Data, "Videos");
        public static string Backgrounds => Path.Combine(Data, "Backgrounds");
        public static string Library => Path.Combine(Data, "Assets");
        public static string DjFrames => Path.Combine(Data, "Dj", "frames");
    }
}
