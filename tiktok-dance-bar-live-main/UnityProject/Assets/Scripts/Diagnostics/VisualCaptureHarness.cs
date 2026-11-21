using System;
using System.Collections;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class VisualCaptureHarness : MonoBehaviour
    {
        public static void InstallIfRequested(GameObject root)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-capturePath");
            int gift = Array.IndexOf(arguments, "-captureGiftHud");
            if (gift >= 0 && gift + 1 < arguments.Length)
            {
                root.GetComponent<TikTokWebSocketClient>().enabled = false;
                VisualCaptureHarness capture = root.AddComponent<VisualCaptureHarness>();
                capture.StartCoroutine(capture.CaptureGiftHud(arguments[gift + 1]));
                return;
            }
            int orbit = Array.IndexOf(arguments, "-captureOrbit");
            if (orbit >= 0 && orbit + 1 < arguments.Length)
            {
                root.GetComponent<TikTokWebSocketClient>().enabled = false;
                VisualCaptureHarness capture = root.AddComponent<VisualCaptureHarness>();
                capture.StartCoroutine(capture.CaptureOrbit(arguments[orbit + 1]));
                return;
            }
            if (index < 0 || index + 1 >= arguments.Length) return;
            VisualCaptureHarness harness = root.AddComponent<VisualCaptureHarness>();
            harness.StartCoroutine(harness.Capture(arguments[index + 1]));
        }

        private IEnumerator CaptureGiftHud(string path)
        {
            yield return new WaitForSecondsRealtime(2f);
            GiftEffectManager effects = GetComponent<GiftEffectManager>();
            TikTokEvent gift = new()
            {
                type = "gift",
                userId = "demo-gift-hud",
                nickname = "Người xem có tên rất dài cùng quẩy với mọi người",
                giftName = "Món quà ánh sáng từ dải ngân hà gửi tới cả sàn nhảy",
                diamondCount = 240,
                repeatCount = 12,
                durationMs = 4000
            };
            effects.Play(gift, null);
            effects.Play(gift, null);
            yield return new WaitForSecondsRealtime(0.7f);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit();
        }

        private IEnumerator CaptureOrbit(string path)
        {
            Camera.main.aspect = 540f / 960f;
            yield return new WaitForSecondsRealtime(2f);
            PlayerManager players = FindFirstObjectByType<PlayerManager>();
            TikTokPlayerData[] crowd = new TikTokPlayerData[18];
            for (int i = 1; i <= 18; i++)
                crowd[i - 1] = new TikTokPlayerData { userId = $"demo-{i}", nickname = $"Dancer {i}" };
            GetComponent<TikTokGameController>().HandleEvent(new TikTokEvent
            {
                type = "snapshot",
                players = crowd,
                vipScores = new[]
                {
                    new TikTokPlayerData { userId = "demo-1", score = 300 },
                    new TikTokPlayerData { userId = "demo-2", score = 200 },
                    new TikTokPlayerData { userId = "demo-off-floor", score = 150 },
                    new TikTokPlayerData { userId = "demo-3", score = 100 }
                }
            });
            if (players.Find("demo-3")?.TopRank != 3)
                throw new InvalidOperationException("The third eligible dancer did not receive podium rank 3.");
            OrbitStage stage = GetComponent<OrbitStage>();
            stage.AddHearts(999);
            CaptureCamera(path + ".goal.png");
            yield return new WaitForSecondsRealtime(0.5f);
            stage.AddHearts(1);
            yield return new WaitForSecondsRealtime(2.5f);
            CaptureCamera(path + ".portal.png");
            yield return new WaitForSecondsRealtime(4f);
            CaptureCamera(path + ".orbit.png");
            yield return new WaitForSecondsRealtime(0.5f);
            stage.AddHearts(350);
            yield return new WaitForSecondsRealtime(0.6f);
            CaptureCamera(path + ".drop.png");
            yield return new WaitForSecondsRealtime(4.8f);
            CaptureCamera(path + ".roll.png");
            stage.TogglePreview();
            yield return new WaitForSecondsRealtime(3.3f);
            CaptureCamera(path + ".return.png");
            yield return new WaitForSecondsRealtime(0.7f);
            Application.Quit();
        }

        private static void CaptureCamera(string path)
        {
            Camera camera = Camera.main;
            RenderTexture original = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(540, 960, 24);
            Texture2D image = new(540, 960, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = original;
            RenderTexture.ReleaseTemporary(target);
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            Destroy(image);
        }

        private IEnumerator Capture(string path)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            TikTokWebSocketClient client = FindFirstObjectByType<TikTokWebSocketClient>();
            client?.StartDemo(40);
            yield return new WaitForSecondsRealtime(5f);
            client?.StopDemo();
            client?.DemoGift(300, 7);
            client?.DemoGift(200, 8);
            client?.DemoGift(100, 9);
            yield return new WaitForSecondsRealtime(9f);
            FindFirstObjectByType<GiftEffectManager>()?.PartyBurst();
            yield return new WaitForSecondsRealtime(0.8f);
            ScreenCapture.CaptureScreenshot(path + ".stage.png");
            yield return new WaitForSecondsRealtime(0.5f);
            client?.DemoGift(100, 7);
            yield return new WaitForSecondsRealtime(4f);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"Visual capture saved to {path}");
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }
    }
}
