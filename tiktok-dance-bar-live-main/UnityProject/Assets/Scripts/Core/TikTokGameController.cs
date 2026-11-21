using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class TikTokGameController : MonoBehaviour
    {
        private TikTokWebSocketClient client;
        private PlayerManager playerManager;
        private GiftEffectManager giftEffects;
        private ClubCameraController clubCamera;
        private OrbitStage orbitStage;
        private readonly Dictionary<string, Donor> donors = new();
        private readonly List<FeedEntry> feed = new();
        private readonly Queue<TikTokEvent> waitingSocialActions = new();
        private float nextSocialActionAt;
        private float partyEnergy;
        private bool liveConnected;
        private bool demoMode;
        private bool stylesReady;
        private GUIStyle panelStyle;
        private GUIStyle smallStyle;
        private GUIStyle interactionHintStyle;
        private readonly GiftNotificationHud giftHud = new();
        private readonly ComboDragonHud comboDragon = new();
        private readonly GiftGuideHud giftGuide = new();
        private string[] interactionHints = System.Array.Empty<string>();
        private HeartMilestone[] heartRules = System.Array.Empty<HeartMilestone>();
        private readonly HashSet<(string Id, int Goal, string Action)> reachedHeartRules = new();
        private long roomHearts;
        private int personalLevelThreshold;


        public void Initialize(TikTokWebSocketClient socketClient, PlayerManager manager, GiftEffectManager effects)
        {
            client = socketClient;
            playerManager = manager;
            giftEffects = effects;
            clubCamera = Camera.main?.GetComponent<ClubCameraController>();
            orbitStage = gameObject.AddComponent<OrbitStage>();
            orbitStage.Initialize(Camera.main, playerManager);
            if (clubCamera != null) clubCamera.OrbitStage = orbitStage;
            client.EventReceived += HandleEvent;
        }

        private void Update()
        {
            if (waitingSocialActions.Count > 0 && Time.unscaledTime >= nextSocialActionAt &&
                !(clubCamera != null && clubCamera.HasGiftFocus) && !(giftEffects != null && giftEffects.HasGiftPresentation))
            {
                HandleEvent(waitingSocialActions.Dequeue());
                nextSocialActionAt = Time.unscaledTime + 1.2f;
            }
            for (int index = feed.Count - 1; index >= 0; index--)
                if (Time.unscaledTime - feed[index].Time > 9f) feed.RemoveAt(index);
        }

        internal void HandleEvent(TikTokEvent liveEvent)
        {
            if (!liveEvent.preview && liveEvent.type is ("chat" or "share") &&
                !string.IsNullOrEmpty(liveEvent.action) && liveEvent.action != "join" &&
                ((clubCamera != null && clubCamera.HasGiftFocus) || (giftEffects != null && giftEffects.HasGiftPresentation)))
            {
                waitingSocialActions.Enqueue(liveEvent);
                return;
            }
            if (liveEvent.type == "viewer_guide")
            {
                giftGuide.SetItems(liveEvent.guideItems);
                interactionHints = liveEvent.interactionHints ?? System.Array.Empty<string>();
                heartRules = (liveEvent.heartRules ?? System.Array.Empty<HeartMilestone>())
                    .Where(rule => rule != null && rule.heartGoal > 0 && rule.action != "level_up").OrderBy(rule => rule.heartGoal).ToArray();
                personalLevelThreshold = liveEvent.heartRules?.FirstOrDefault(rule => rule?.action == "level_up")?.heartGoal ?? 0;
                playerManager.SetPersonalLevelThreshold(personalLevelThreshold);
                return;
            }
            if (liveEvent.preview && liveEvent.type == "like")
            {
                if (liveEvent.action == "level_up")
                {
                    PlayerActor actor = playerManager.Find(liveEvent.userId);
                    if (actor == null)
                    {
                        client.AcknowledgeTest(liveEvent.testCommandId, actorMissing: true);
                        return;
                    }
                    actor.PreviewLevelUp(Mathf.Max(1, liveEvent.heartGoal));
                }
                else RunHeartEffect(liveEvent.action, liveEvent.durationMs, liveEvent.fireworkBursts);
                client.AcknowledgeTest(liveEvent.testCommandId);
                return;
            }
            if (liveEvent.type == "master_test_spawn")
            {
                playerManager.SpawnPreviewActor();
                client.AcknowledgeTest(liveEvent.testCommandId);
                return;
            }
            if (liveEvent.preview && playerManager.Find(liveEvent.userId) == null)
            {
                client.AcknowledgeTest(liveEvent.testCommandId, actorMissing: true);
                return;
            }
            if (liveEvent.type == "status")
            {
                liveConnected = liveEvent.state == "connected";
                demoMode = liveEvent.state == "demo";
            }

            if (liveEvent.type == "snapshot")
            {
                roomHearts = liveEvent.roomHearts;
                donors.Clear();
                foreach (TikTokPlayerData donor in liveEvent.vipScores ?? System.Array.Empty<TikTokPlayerData>())
                {
                    donors[donor.userId] = new Donor(donor.userId, donor.nickname, donor.score);
                }
            }
            else if (!liveEvent.preview && liveEvent.type == "gift")
            {
                donors.TryGetValue(liveEvent.userId, out Donor current);
                donors[liveEvent.userId] = new Donor(liveEvent.userId, liveEvent.nickname, current.Score + liveEvent.diamondCount);
                AddEnergy(liveEvent.diamondCount * 2f);
                AddFeed($"{liveEvent.nickname} tặng {liveEvent.giftName} (+{liveEvent.diamondCount})", new Color(1f, 0.55f, 0.84f));
            }
            else if (!liveEvent.preview && liveEvent.type == "chat")
            {
                AddEnergy(2f);
                if (!string.IsNullOrWhiteSpace(liveEvent.comment))
                    AddFeed($"{liveEvent.nickname}: {liveEvent.comment}", Color.white);
            }
            else if (liveEvent.type == "like")
            {
                AddEnergy(liveEvent.likeCount * 0.08f);
                bool demoEvent = (liveEvent.eventId ?? "").StartsWith("demo-", System.StringComparison.Ordinal);
                if (!liveEvent.preview && ((liveConnected && !demoEvent) || (demoMode && demoEvent)))
                    CountRoomHearts(liveEvent.likeCount);
            }
            else if (liveEvent.type is "follow" or "share")
            {
                if (!liveEvent.preview)
                    AddFeed($"{liveEvent.nickname} đã {liveEvent.type}", new Color(1f, 0.85f, 0.3f));
            }
            else if (liveEvent.type == "reset")
            {
                partyEnergy = 0f;
                donors.Clear();
                feed.Clear();
                orbitStage?.ResetShow();
                clubCamera?.ResetFocus();
                waitingSocialActions.Clear();
                nextSocialActionAt = 0f;
                giftEffects?.ResetPresentations();
                roomHearts = 0;
                reachedHeartRules.Clear();
            }

            bool wasOnFloor = playerManager.Find(liveEvent.userId) != null;
            playerManager.Handle(liveEvent);
            bool enteredFloor = !wasOnFloor && playerManager.Find(liveEvent.userId) != null;
            if (!liveEvent.preview && liveEvent.joinedNow && enteredFloor)
            {
                AddEnergy(1f);
                AddFeed($"{liveEvent.nickname} vào sàn", new Color(0.35f, 0.95f, 1f));
            }
            if (!liveEvent.preview && (enteredFloor || liveEvent.type is "gift" or "snapshot")) UpdateTopPlayers();
            if (liveEvent.type == "gift" || ((liveEvent.type is "chat" or "share") && (liveEvent.action is "fireworks" or "topdj")))
            {
                PlayerActor actor = playerManager.Find(liveEvent.userId);
                giftEffects.Play(liveEvent, actor);
            }

            bool joinFocus = liveEvent.action == "join" && liveEvent.joinedNow;
            bool socialFocus = liveEvent.type == "follow" || (liveEvent.type == "share" && string.IsNullOrEmpty(liveEvent.action));
            bool requestsFocus = liveEvent.type == "gift" || joinFocus || socialFocus || liveEvent.action is "camera" or "walk" or "vip" or "vip_dance" or "topdj" or "fireworks" or "medal";
            if (requestsFocus && (liveEvent.type is "gift" or "chat" or "follow" or "share"))
            {
                PlayerActor actor = playerManager.Find(liveEvent.userId);
                if (actor == null) return;
                float focusSeconds = liveEvent.durationMs > 0 ? liveEvent.durationMs / 1000f : (socialFocus ? 2f : joinFocus ? 2.5f : 3f);
                if (liveEvent.type == "gift" || liveEvent.action == "topdj") focusSeconds = liveEvent.zoomDurationMs > 0 ? liveEvent.zoomDurationMs / 1000f : 3.5f;
                bool wideWalkFocus = liveEvent.action == "walk";
                if ((joinFocus || socialFocus) && liveEvent.type != "gift")
                {
                    clubCamera?.QueueWelcome(actor, focusSeconds);
                }
                else if (liveEvent.action == "fireworks")
                    clubCamera?.FocusFireworks(actor, Mathf.Max(6f, focusSeconds), liveEvent.type == "gift");
                else
                    clubCamera?.Focus(
                        actor,
                        focusSeconds,
                        liveEvent.action is "vip" or "vip_dance" or "topdj",
                        wideWalkFocus,
                        gift: liveEvent.type == "gift");

            }
            if (liveEvent.preview) client.AcknowledgeTest(liveEvent.testCommandId);
        }

        private void CountRoomHearts(int amount)
        {
            if (amount <= 0) return;
            long previous = roomHearts;
            roomHearts += amount;
            orbitStage?.AddHearts(amount, autoOpen: false);
            foreach (HeartMilestone rule in heartRules)
            {
                if (previous >= rule.heartGoal || roomHearts < rule.heartGoal ||
                    !reachedHeartRules.Add((rule.id, rule.heartGoal, rule.action))) continue;
                RunHeartEffect(rule.action, rule.durationMs, rule.fireworkBursts);
            }
        }

        private void RunHeartEffect(string action, int durationMs, int bursts)
        {
            if (action == "orbit") orbitStage?.OpenFromMilestone(durationMs / 1000f);
            else if (action == "fireworks") giftEffects.LaunchRuleFireworks(new TikTokEvent { fireworkBursts = bursts });
            else if (action == "party") playerManager.DanceAll(durationMs > 0 ? durationMs / 1000f : 6f);
        }

        private void UpdateTopPlayers()
        {
            playerManager.UpdateTopRanks(donors.Values.OrderByDescending(donor => donor.Score).Select(donor => donor.UserId));
        }

        private void AddEnergy(float amount)
        {
            partyEnergy += Mathf.Max(0f, amount);
            if (partyEnergy < 1000f) return;
            partyEnergy %= 1000f;
            playerManager.DanceAll(7f);
            giftEffects.PartyBurst();
        }

        private void AddFeed(string text, Color color)
        {
            feed.Add(new FeedEntry(text, color, Time.unscaledTime));
            if (feed.Count > 7) feed.RemoveAt(0);
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1080f, Screen.height / 1080f), 0.72f, 1.25f);
            Matrix4x4 originalMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            DrawFeed(height);
            orbitStage?.DrawHud(width, height, false, panelStyle, giftGuide.HasItems ? 225f : 128f);
            giftGuide.Draw(width);
            DrawInteractionHint(width);
            comboDragon.Draw(giftEffects, width, height);
            if (giftEffects != null && Time.unscaledTime < giftEffects.BannerUntil)
                giftHud.Draw(giftEffects, width, 57f);
            GUI.matrix = originalMatrix;
        }

        private void DrawInteractionHint(float width)
        {
            HeartMilestone next = heartRules.FirstOrDefault(rule => roomHearts < rule.heartGoal);
            bool hasLevelRule = personalLevelThreshold > 0;
            int count = interactionHints.Length + (hasLevelRule ? 1 : 0) + (next != null ? 1 : 0);
            if (count == 0) return;
            string levelHint = $"♥ {personalLevelThreshold:N0} tim → +1 cấp cá nhân";
            string heartHint = next == null ? null :
                $"♥ Phòng {roomHearts:N0}/{next.heartGoal:N0} → " +
                (next.action == "orbit" ? "Mở cổng" : next.action == "fireworks" ? "Pháo hoa" : "Cả sàn nhảy");
            float cardWidth = Mathf.Min(238f, width * 0.5f - 32f);
            if (cardWidth < 60f) return;
            float x = width - cardWidth - 12f;
            float y = GiftGuideHud.Top;
            interactionHintStyle ??= new GUIStyle(smallStyle)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white }
            };
            float textWidth = cardWidth - 20f;
            for (int index = 0; index < count; index++)
            {
                GUIContent content = new(index < interactionHints.Length ? interactionHints[index]
                    : hasLevelRule && index == interactionHints.Length ? levelHint : heartHint);
                interactionHintStyle.fontSize = 18;
                content.text = content.text.Replace("Zoom camera", "Zoom").Replace("Nhảy theo nhạc", "Nhảy").Replace("Đổi nhân vật", "Đổi NV");
                float naturalWidth = interactionHintStyle.CalcSize(content).x;
                if (naturalWidth > textWidth)
                    interactionHintStyle.fontSize = Mathf.Max(10, Mathf.FloorToInt(18f * textWidth / naturalWidth));
                float textHeight = 23f;
                GUI.DrawTexture(new Rect(x, y, cardWidth, textHeight + 10f), Texture2D.whiteTexture,
                    ScaleMode.StretchToFill, true, 0f, new Color(0.025f, 0.04f, 0.09f, 0.72f), 0f, 8f);
                GUI.Label(new Rect(x + 10f, y + 5f, textWidth, textHeight), content, interactionHintStyle);
                y += textHeight + 14f;
            }
        }

        private void DrawFeed(float height)
        {
            float y = height - 34f - feed.Count * 27f;
            foreach (FeedEntry item in feed)
            {
                Color old = GUI.color;
                GUI.color = item.Color;
                GUI.Label(new Rect(24, y, 520, 25), item.Text, smallStyle);
                GUI.color = old;
                y += 27f;
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            Texture2D panel = Solid(new Color(0.018f, 0.018f, 0.055f, 0.91f));
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panel }, border = new RectOffset(8, 8, 8, 8) };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = new Color(0.82f, 0.86f, 0.94f) } };
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private readonly struct Donor
        {
            public readonly string UserId;
            public readonly string Name;
            public readonly int Score;
            public Donor(string userId, string name, int score)
            {
                UserId = userId;
                Name = string.IsNullOrWhiteSpace(name) ? "TikTok user" : name;
                Score = score;
            }
        }

        private readonly struct FeedEntry
        {
            public readonly string Text;
            public readonly Color Color;
            public readonly float Time;
            public FeedEntry(string text, Color color, float time) { Text = text; Color = color; Time = time; }
        }

        private void OnDestroy()
        {
            if (client != null) client.EventReceived -= HandleEvent;
            giftHud.Dispose();
            comboDragon.Dispose();
            comboDragon.Dispose();
        }
    }
}
