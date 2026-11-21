using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class AssetTestController : MonoBehaviour
    {
        [Serializable] private class Command { public string token; public string id; public string text; }
        private string token; private float refreshAt; private PlayerActor actor; private float until;
        private void Awake()
        {
            // Existing commands belong to the previous game session.
            // Capture the token now; only a new button click may spawn a test dancer.
            try
            {
                string file = Path.Combine(RuntimeAssetLibrary.Folder, "test.json");
                if (File.Exists(file))
                    token = JsonUtility.FromJson<Command>(File.ReadAllText(file))?.token;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not read previous asset test token: " + error.Message);
            }
        }

        private void Update()
        {
            if (actor != null && Time.unscaledTime > until) Destroy(actor.gameObject);
            if (Time.unscaledTime < refreshAt) return; refreshAt = Time.unscaledTime + 1f;
            try
            {
                string file = Path.Combine(RuntimeAssetLibrary.Folder, "test.json"); if (!File.Exists(file)) return;
                Command command = JsonUtility.FromJson<Command>(File.ReadAllText(file)); if (command == null || command.token == token) return; token = command.token;
                var item = RuntimeAssetLibrary.Read().items.FirstOrDefault(item => item.id == command.id);
                if (item != null && item.kind == "character" && actor != null) { Destroy(actor.gameObject); actor = null; }
                if (actor == null)
                {
                    GameObject sample = new GameObject("Asset Test Dancer"); actor = sample.AddComponent<PlayerActor>();
                    string name = item != null && item.kind == "character" ? item.name : "TEST NHÂN VẬT";
                    actor.Initialize(new TikTokPlayerData { userId = "master-test", nickname = name, uniqueId = "asset-test" }, new Vector3(0f, 0f, 1.8f), Color.white);
                    actor.SetCrowdSlot(new Vector3(0f, 0f, 1.8f), 0.65f);
                }
                until = Time.unscaledTime + 60f;
                if (item != null && item.kind == "character") actor.SetTestCharacter(item.id);
                else if (item != null) actor.SetTestDecoration(item.kind, item.id, command.text);
                else actor.ShowTitle(command.text, 60f);
                actor.Dance(60f);
                Debug.Log("Asset dancer test: " + (item != null ? item.name : command.text));
            }
            catch (Exception error) { Debug.LogWarning("Asset test: " + error.Message); }
        }
    }
}
