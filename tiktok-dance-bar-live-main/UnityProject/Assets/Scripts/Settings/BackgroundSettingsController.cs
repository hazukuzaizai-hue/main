using System;
using System.IO;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class BackgroundSettingsController : MonoBehaviour
    {
        [Serializable]
        private class Settings { public string selected; }
        private string applied;
        private float refreshAt;
        private Texture2D ownedTexture;
        private GameObject room;
        private GameObject architecture;
        private GameObject club;
        private Renderer floor;
        private void SetStageMode(bool stageMode)
        {
            if (room == null) room = GameObject.Find("AmPhuBackdrop_Room");
            if (architecture == null) architecture = GameObject.Find("StageArchitecture");
            if (club == null) club = GameObject.Find("ClubRoot");
            if (floor == null) floor = GameObject.Find("Invisible Dance Floor")?.GetComponent<Renderer>();
            if (room != null) foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>()) renderer.enabled = !stageMode;
            if (architecture != null) architecture.SetActive(stageMode);
            FindFirstObjectByType<PlayerManager>()?.SetMezzaninesEnabled(stageMode);
            if (floor != null) floor.enabled = false; // Keep collider, reveal the recessed LED wells.
            if (club != null) club.transform.position = stageMode ? Vector3.zero : new Vector3(0f, 0f, -12f);
        }

        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 2f;
            try
            {
                string root = GamePaths.Backgrounds;
                string config = Path.Combine(root, "settings.json");
                string selected = File.Exists(config) ? JsonUtility.FromJson<Settings>(File.ReadAllText(config))?.selected : "nenamphu.png";
                if (string.IsNullOrEmpty(selected) || selected == applied || Path.GetFileName(selected) != selected) return;
                if (selected == "__stage3d__")
                {
                    SetStageMode(true);
                    applied = selected;
                    Debug.Log("Background applied: 3D stage");
                    return;
                }
                string file = selected == "nenamphu.png" ? Path.Combine(root, "default.png") : Path.Combine(root, "images", selected);
                if (!File.Exists(file)) return;
                if (room == null) room = GameObject.Find("AmPhuBackdrop_Room");
                if (room == null) return;
                byte[] bytes = File.ReadAllBytes(file);
                if (bytes.Length > 20 * 1024 * 1024) return;
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes)) { Destroy(texture); return; }
                if (texture.width > 8192 || texture.height > 8192 || (long)texture.width * texture.height > 32000000)
                { Destroy(texture); return; }
                texture.filterMode = FilterMode.Trilinear;
                texture.wrapMode = TextureWrapMode.Mirror;
                texture.anisoLevel = 8;
                foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial.mainTexture = texture;
                if (ownedTexture != null) Destroy(ownedTexture);
                ownedTexture = texture;
                SetStageMode(false);
                applied = selected;
                Debug.Log("Background applied: " + selected);
            }
            catch (Exception error) { Debug.LogWarning("Background settings: " + error.Message); }
        }

        private void OnDestroy() { if (ownedTexture != null) Destroy(ownedTexture); }
    }
}
