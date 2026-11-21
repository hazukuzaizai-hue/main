using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace TikTokLiveGame
{
    public sealed class StageSettingsController : MonoBehaviour
    {
        [Serializable] private class Settings { public string mode = "equalizer"; public string video = ""; }
        [Serializable] private class Status { public string state; public string message; }
        private DjLedBackdrop backdrop;
        private VideoPlayer video;
        private RenderTexture videoTexture;
        private string folder;
        private string appliedMode;
        private string appliedVideo;
        private string state = "equalizer";
        private string message = "Màn DJ đang chạy sóng nhạc LED";
        private float refreshAt;
        private float videoStartedAt;
        private float lastFrameAt;
        private long lastFrame;
        private const float StallTimeout = 5f;

        private void Start()
        {
            folder = GamePaths.Videos;
            backdrop = FindFirstObjectByType<DjLedBackdrop>();
            video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false; video.isLooping = true; video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.RenderTexture; video.audioOutputMode = VideoAudioOutputMode.None;
            video.skipOnDrop = true;
            video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            video.prepareCompleted += OnVideoPrepared;
            video.loopPointReached += _ => Debug.Log("DJ video loop: " + appliedVideo);
            video.errorReceived += (_, error) => VideoError(error);
        }

        private void OnVideoPrepared(VideoPlayer player)
        {
            if (appliedMode != "video" || state != "loading") return;
            lastFrame = -1;
            lastFrameAt = Time.unscaledTime;
            player.Play();
            backdrop?.SetDisplay(videoTexture, true);
            state = "video";
            message = "Đang phát video: " + appliedVideo;
            Debug.Log(message);
        }

        private void PrepareVideo()
        {
            state = "loading";
            message = "Đang tải video: " + appliedVideo;
            videoStartedAt = Time.unscaledTime;
            video.Prepare();
        }

        private void KeepVideoPlaying()
        {
            if (state != "video") return;
            long frame = video.frame;
            if (frame >= 0 && frame != lastFrame)
            {
                lastFrame = frame;
                lastFrameAt = Time.unscaledTime;
            }
            if (Time.unscaledTime - lastFrameAt < StallTimeout) return;

            // Reopen the decoder if the native loop stops presenting new frames.
            Debug.LogWarning("DJ video stalled. Restarting " + appliedVideo);
            video.Stop();
            PrepareVideo();
        }

        private void VideoError(string error)
        {
            video.Stop(); backdrop?.SetDisplay(null, true);
            state = "error"; message = "Video không phát được; đã trở về sóng nhạc LED. Dùng MP4 H.264.";
            Debug.LogWarning("DJ video: " + error);
        }

        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 2f;
            try
            {
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "STAGE_SETTINGS.json");
                Settings settings = File.Exists(file) ? JsonUtility.FromJson<Settings>(File.ReadAllText(file)) : new Settings();
                if (settings == null) return;
                if (settings.mode != appliedMode || settings.video != appliedVideo)
                {
                    appliedMode = settings.mode; appliedVideo = settings.video; video.Stop(); backdrop?.SetDisplay(null, settings.mode != "off");
                    state = settings.mode; message = settings.mode == "off" ? "Màn DJ đã tắt" : "Màn DJ đang chạy sóng nhạc LED";
                    if (settings.mode == "video")
                    {
                        if (string.IsNullOrEmpty(settings.video) || Path.GetFileName(settings.video) != settings.video || !settings.video.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) { VideoError("Invalid video path"); }
                        else
                        {
                            string path = Path.Combine(folder, settings.video);
                            if (!File.Exists(path)) VideoError("Video file not found");
                            else
                            {
                                if (videoTexture == null) { videoTexture = new RenderTexture(1280, 720, 0); videoTexture.Create(); }
                                video.targetTexture = videoTexture; video.url = new Uri(path).AbsoluteUri;
                                PrepareVideo();
                            }
                        }
                    }
                    Debug.Log("Stage settings: " + appliedMode);
                }
                KeepVideoPlaying();
                if (state == "loading" && Time.unscaledTime - videoStartedAt > 25f) VideoError("Video preparation timeout");
                string statusFile = Path.Combine(folder, "STAGE_STATUS.json"), temp = statusFile + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(new Status { state = state, message = message }));
                if (File.Exists(statusFile)) File.Replace(temp, statusFile, null); else File.Move(temp, statusFile);
            }
            catch (Exception error) { Debug.LogWarning("Stage settings: " + error.Message); }
        }

        private void OnDestroy()
        {
            if (video != null) video.Stop();
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        }
    }
}
