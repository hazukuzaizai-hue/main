using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TikTokLiveGame
{
    public sealed class MusicPlaylistPlayer : MonoBehaviour
    {
        private const float DefaultVolume = 0.35f;
        private const float FadeSeconds = 1.2f;
        private AudioSource output;
        public AudioSource Output => output;
        private AudioClip currentClip;
        private string[] tracks = Array.Empty<string>();
        private int trackIndex;
        private float configuredVolume = DefaultVolume;
        private float trackStartedAt;
        private float watchdogAt;
        private float advanceAt;
        private bool loading;
        private bool advancePending;
        private bool wasPlaying;
        private bool musicEnabled = true;
        private bool paused;
        private float refreshAt;
        private bool manuallyPaused;
        private string lastCommandId;
        private string musicFolder;
        private float statusAt;

        [Serializable]
        private class MusicCommand { public string id; public string action; public string track; }
        [Serializable]
        private class MusicStatus { public string state; public string track; }

        private void ReadCommand()
        {
            string file = Path.Combine(musicFolder, "MUSIC_COMMAND.json");
            if (!File.Exists(file)) return;
            MusicCommand command = JsonUtility.FromJson<MusicCommand>(File.ReadAllText(file));
            if (command == null || string.IsNullOrEmpty(command.id) || command.id == lastCommandId) return;
            lastCommandId = command.id;
            if (command.action == "pause") { manuallyPaused = true; return; }
            manuallyPaused = false;
            musicEnabled = true;
            if (command.action == "resume") return;
            int index = command.action == "next" ? (trackIndex + 1) % Math.Max(1, tracks.Length)
                : Array.FindIndex(tracks, track => Path.GetFileName(track) == command.track);
            if (index < 0 || tracks.Length == 0) return;
            StopAllCoroutines();
            PlayTrack(index);
            Debug.Log("Music command: " + command.action + " " + Path.GetFileName(tracks[index]));
        }

        private void WriteStatus()
        {
            if (string.IsNullOrEmpty(musicFolder)) return;
            try
            {
                Directory.CreateDirectory(musicFolder);
                MusicStatus status = new MusicStatus
                {
                    state = !musicEnabled || manuallyPaused ? "paused" : loading ? "loading" : output.isPlaying ? "playing" : "idle",
                    track = tracks.Length > 0 ? Path.GetFileName(tracks[trackIndex]) : ""
                };
                string file = Path.Combine(musicFolder, "MUSIC_STATUS.json");
                string temp = file + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(status));
                if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
            }
            catch (IOException) { }
        }

        [Serializable]
        private class MusicSettings
        {
            public bool enabled = true;
            public float volume = DefaultVolume;
        }

        private void RefreshSettings()
        {
            try
            {
                string[] nextTracks = FindTracks(out string selectedFolder);
                configuredVolume = ReadVolume(selectedFolder);
                musicFolder = GamePaths.Music;
                string settingsPath = Path.Combine(musicFolder, "MUSIC_SETTINGS.json");
                if (File.Exists(settingsPath))
                {
                    MusicSettings settings = JsonUtility.FromJson<MusicSettings>(File.ReadAllText(settingsPath));
                    if (settings != null)
                    {
                        configuredVolume = Mathf.Clamp01(settings.volume);
                        musicEnabled = settings.enabled;
                    }
                }
                if (!tracks.SequenceEqual(nextTracks))
                {
                    string currentPath = tracks.Length > 0 ? tracks[trackIndex] : null;
                    tracks = nextTracks;
                    int currentIndex = Array.IndexOf(tracks, currentPath);
                    if (currentIndex >= 0) trackIndex = currentIndex;
                    else
                    {
                        StopAllCoroutines();
                        StopCurrentClip();
                        loading = false;
                        advancePending = false;
                        if (tracks.Length > 0) PlayTrack(0);
                    }
                }
                ReadCommand();
            }
            catch (Exception error)
            {
                Debug.LogWarning("Music settings: " + error.Message);
            }
        }

        private void Awake()
        {
            output = gameObject.AddComponent<AudioSource>();
            output.playOnAwake = false;
            output.loop = false;
            output.spatialBlend = 0f;
            output.volume = 0f;
            output.priority = 128;
            RefreshSettings();
        }

        private void Update()
        {
            if (output == null) return;
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + 2f;
                RefreshSettings();
            }
            if (Time.unscaledTime >= statusAt) { statusAt = Time.unscaledTime + 1f; WriteStatus(); }
            if (!musicEnabled || manuallyPaused)
            {
                if (output.isPlaying) { output.Pause(); paused = true; }
                return;
            }
            if (paused)
            {
                output.UnPause();
                paused = false;
                watchdogAt = Time.unscaledTime + 3f;
            }
            if (tracks.Length == 0) return;
            if (advancePending && Time.unscaledTime >= advanceAt)
            {
                advancePending = false;
                PlayTrack((trackIndex + 1) % tracks.Length);
                return;
            }

            if (output.isPlaying)
            {
                wasPlaying = true;
                float fadeIn = Mathf.Clamp01((Time.unscaledTime - trackStartedAt) / FadeSeconds);
                float fadeOut = currentClip == null
                    ? 1f
                    : Mathf.Clamp01((currentClip.length - output.time) / FadeSeconds);
                output.volume = configuredVolume * Mathf.Min(fadeIn, fadeOut);
                watchdogAt = Time.unscaledTime + 3f;
            }
            else if (wasPlaying && !loading)
            {
                wasPlaying = false;
                output.volume = 0f;
                ScheduleAdvance(0.05f);
            }
            else if (!loading && !advancePending && Time.unscaledTime >= watchdogAt)
            {
                Debug.LogWarning("Music watchdog detected a stopped track; advancing the playlist.");
                ScheduleAdvance(0.5f);
            }
        }

        private void PlayTrack(int index)
        {
            if (tracks.Length == 0) return;
            trackIndex = Mathf.Clamp(index, 0, tracks.Length - 1);
            StopCurrentClip();
            loading = true;
            advancePending = false;
            watchdogAt = Time.unscaledTime + 20f;
            StartCoroutine(LoadAndPlay(tracks[trackIndex]));
        }

        private IEnumerator LoadAndPlay(string path)
        {
            AudioType type = AudioTypeFor(path);
            string uri = new Uri(path).AbsoluteUri;
            Debug.Log($"Preparing music {trackIndex + 1}/{tracks.Length}: {path}");
            using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, type);
            if (request.downloadHandler is DownloadHandlerAudioClip audioDownload)
                audioDownload.streamAudio = true;
            yield return request.SendWebRequest();

            loading = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Music could not play '{path}': {request.error}");
                ScheduleAdvance(tracks.Length == 1 ? 2f : 0.75f);
                yield break;
            }

            currentClip = DownloadHandlerAudioClip.GetContent(request);
            if (currentClip == null)
            {
                Debug.LogWarning($"Music decoder returned no audio for '{path}'.");
                ScheduleAdvance(tracks.Length == 1 ? 2f : 0.75f);
                yield break;
            }

            currentClip.name = $"Music {Path.GetFileName(path)}";
            output.clip = currentClip;
            output.volume = 0f;
            trackStartedAt = Time.unscaledTime;
            watchdogAt = Time.unscaledTime + 5f;
            output.Play();
            if (!musicEnabled || manuallyPaused) { output.Pause(); paused = true; }
            Debug.Log($"Music playing {trackIndex + 1}/{tracks.Length}: {path}");
        }

        private void StopCurrentClip()
        {
            if (output != null)
            {
                output.Stop();
                output.clip = null;
                output.volume = 0f;
            }
            if (currentClip != null) Destroy(currentClip);
            currentClip = null;
            wasPlaying = false;
            paused = false;
        }

        private void ScheduleAdvance(float delay)
        {
            if (advancePending) return;
            advancePending = true;
            advanceAt = Time.unscaledTime + Mathf.Max(0.05f, delay);
        }

        private static AudioType AudioTypeFor(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                _ => AudioType.MPEG
            };
        }

        private static string[] FindTracks(out string selectedFolder)
        {
            string[] folders = { GamePaths.Music };
            string[] extensions = { ".mp3", ".wav", ".ogg" };
            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                string[] files = Directory.EnumerateFiles(folder)
                    .Where(candidate => extensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(candidate => Path.GetFileName(candidate), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (files.Length == 0) continue;
                selectedFolder = folder;
                return files;
            }
            selectedFolder = string.Empty;
            return Array.Empty<string>();
        }

        private static float ReadVolume(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return DefaultVolume;
            string path = Path.Combine(folder, "VOLUME.txt");
            if (!File.Exists(path)) return DefaultVolume;
            string value = File.ReadAllText(path).Trim().Replace(',', '.');
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return DefaultVolume;
            if (parsed > 1f) parsed /= 100f;
            return Mathf.Clamp01(parsed);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            StopCurrentClip();
        }
    }
}
