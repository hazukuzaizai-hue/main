using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;

namespace TikTokLiveGame
{
    public sealed class AvatarService : MonoBehaviour
    {
        private readonly Dictionary<string, Sprite> cache = new();
        private readonly Dictionary<string, List<Action<Sprite>>> waiting = new();
        private readonly Queue<string> queue = new();
        private int activeDownloads;
        private const int MaximumConcurrent = 4;
        private static readonly HttpClient LocalBridge = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        public static AvatarService Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Load(string url, Action<Sprite> callback)
        {
            if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("https://") && !url.StartsWith("http://"))) return;
            if (cache.TryGetValue(url, out Sprite sprite))
            {
                callback?.Invoke(sprite);
                return;
            }
            if (waiting.TryGetValue(url, out List<Action<Sprite>> callbacks))
            {
                callbacks.Add(callback);
                return;
            }
            waiting[url] = new List<Action<Sprite>> { callback };
            queue.Enqueue(url);
            Pump();
        }

        private void Pump()
        {
            while (activeDownloads < MaximumConcurrent && queue.Count > 0)
            {
                activeDownloads++;
                StartCoroutine(Download(queue.Dequeue()));
            }
        }

        private IEnumerator Download(string url)
        {
            Sprite result = null;
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true);
            request.timeout = 10;
            yield return request.SendWebRequest();
            Texture2D texture = request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(request) : null;
            if (texture == null)
            {
                string conversionUrl = "http://127.0.0.1:3000/api/avatar?url=" + UnityWebRequest.EscapeURL(url);
                var conversion = LocalBridge.GetByteArrayAsync(conversionUrl);
                yield return new WaitUntil(() => conversion.IsCompleted);
                if (conversion.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    texture = new Texture2D(2, 2);
                    if (!texture.LoadImage(conversion.Result, true))
                    {
                        Destroy(texture);
                        texture = null;
                    }
                }
                else if (conversion.IsFaulted)
                    _ = conversion.Exception;
            }
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                int side = Mathf.Min(texture.width, texture.height);
                Rect rect = new((texture.width - side) * 0.5f, (texture.height - side) * 0.5f, side, side);
                result = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), side);
                cache[url] = result;
            }
            if (waiting.Remove(url, out List<Action<Sprite>> callbacks))
                foreach (Action<Sprite> callback in callbacks) callback?.Invoke(result);
            activeDownloads--;
            Pump();
        }
    }
}
