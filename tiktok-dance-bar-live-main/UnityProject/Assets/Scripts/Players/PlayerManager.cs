using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class PlayerManager : MonoBehaviour
    {
        [SerializeField] private int maxPlayers = 400;
        [SerializeField] private float playerTtlSeconds = 600f;
        private readonly Dictionary<string, PlayerActor> players = new();
        private readonly List<string> playerOrder = new();
        private readonly Dictionary<string, int> crowdSlots = new();
        private int focusVersion;
        private bool mezzaninesEnabled = true;
        private PlayerActor previewActor;
        private readonly Dictionary<PlayerActor, Queue<TikTokEvent>> giftChanges = new();
        private int personalLevelThreshold;

        internal void SetPersonalLevelThreshold(int threshold)
        {
            previewActor?.SetLevelThreshold(threshold);
            if (personalLevelThreshold == threshold) return;
            personalLevelThreshold = threshold;
            foreach (PlayerActor actor in players.Values) actor.SetLevelThreshold(threshold);
        }
        private RankPodiums rankPodiums;
        private string focusedUserId;
        private const float NpcAmbientAlpha = 1f;
        private const float FocusedCrowdAlpha = 0.22f;
        private const float FocusedNpcAlpha = 0.14f;
        private const float FocusedTopAlpha = 0.4f;
        private const int CrowdColumns = 24;
        private const int CrowdRows = 17;
        private const int CrowdCapacity = CrowdColumns * CrowdRows;

        public int Count => players.Count;
        public PlayerActor Find(string userId) => userId == "master-test" ? previewActor : !string.IsNullOrWhiteSpace(userId) && players.TryGetValue(userId, out PlayerActor actor) ? actor : null;

        public bool TryGetCrowdBounds(out Bounds bounds)
        {
            PlayerActor first = players.Values.FirstOrDefault(actor => actor != null);
            if (first == null)
            {
                bounds = new Bounds(Vector3.zero, Vector3.zero);
                return false;
            }

            bounds = new Bounds(first.transform.position, Vector3.zero);
            foreach (PlayerActor actor in players.Values)
                if (actor != null) bounds.Encapsulate(actor.transform.position);
            return true;
        }

        public void DanceAll(float seconds = 6f)
        {
            foreach (PlayerActor actor in players.Values) actor.Dance(seconds);
        }

        internal void FlyFormation(Camera camera, float time, float boost)
        {
            int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(players.Count * 1.35f)), 4, 22);
            int rows = Mathf.Max(3, Mathf.CeilToInt((players.Count + 1) / (float)columns));
            int slot = 0;
            float heightAtUnitDepth = 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float formationSweep = Mathf.Sin(time * 0.78f + 0.7f) * 0.052f;
            foreach (string id in playerOrder)
            {
                if (!players.TryGetValue(id, out PlayerActor actor)) continue;
                PlaceFlyer(actor);
            }
            if (previewActor != null) PlaceFlyer(previewActor);

            void PlaceFlyer(PlayerActor actor)
            {
                int rank = actor.TopRank;
                float x, y, depth, height;
                float sway = time * 1.42f + rank * 1.9f;
                if (rank > 0)
                {
                    x = rank == 1 ? 0.5f : rank == 2 ? 0.27f : 0.73f;
                    y = rank == 1 ? 0.51f : 0.405f;
                    depth = rank == 1 ? 8.2f : 10.4f;
                    height = rank == 1 ? 0.105f : 0.083f;
                    x += Mathf.Sin(sway * 0.72f) * 0.028f;
                    y += Mathf.Cos(sway) * (0.014f + 0.012f * boost);
                }
                else
                {
                    int index = slot++;
                    float u = (index % columns + 0.5f) / columns;
                    float v = (index / columns + 0.5f) / rows;
                    sway = time * 1.55f + index * 2.39996f;
                    x = Mathf.Lerp(0.115f, 0.885f, u) + Mathf.Sin(sway * 0.77f) * (0.026f + boost * 0.012f);
                    y = Mathf.Lerp(0.12f, 0.34f, v) + Mathf.Sin(sway) * 0.027f - Mathf.Sin(u * Mathf.PI) * 0.042f;
                    depth = Mathf.Lerp(13.5f, 25f, v) + Mathf.Cos(sway * 0.65f) * 1.4f;
                    height = Mathf.Lerp(0.067f, 0.024f, Mathf.InverseLerp(12f, 125f, players.Count));
                }
                Quaternion bank = camera.transform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(sway) * (rank > 0 ? 6f : 13f));
                actor.ApplyFlightPose(camera.ViewportToWorldPoint(new Vector3(x + formationSweep, y, depth)), bank, height * depth * heightAtUnitDepth);
            }
        }

        internal void EndFlight()
        {
            foreach (PlayerActor actor in players.Values) actor.EndFlight();
            if (previewActor != null) previewActor.EndFlight();
        }


        public void FocusPlayer(string userId, float seconds)
        {
            int version = ++focusVersion;
            focusedUserId = userId;
            foreach (KeyValuePair<string, PlayerActor> pair in players)
            {
                bool focused = pair.Key == userId;
                if (focused)
                {
                    pair.Value.ReturnToAssignedSlot();
                    pair.Value.PulseGiftFloor();
                }
                pair.Value.SetVisibility(focused ? 1f : FocusBackgroundAlpha(pair.Value));
                pair.Value.SetGiftFocus(focused);
            }
            StartCoroutine(RestoreFocus(version, Mathf.Clamp(seconds, 2.5f, 12f)));
        }

        private System.Collections.IEnumerator RestoreFocus(int version, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (version != focusVersion) yield break;
            focusedUserId = null;
            foreach (PlayerActor actor in players.Values)
            {
                actor.SetGiftFocus(false);
                actor.ReturnToAssignedSlot();
            }
            ApplyAmbientVisibility();
        }

        public void UpdateTopRanks(IEnumerable<string> rankedUserIds)
        {
            Dictionary<string, int> ranks = rankedUserIds
                .Where(id =>
                    !string.IsNullOrWhiteSpace(id) &&
                    players.TryGetValue(id, out PlayerActor actor) &&
                    !actor.IsNpc)
                .Take(3)
                .Select((id, index) => new { id, rank = index + 1 })
                .ToDictionary(item => item.id, item => item.rank);
            if (rankPodiums == null) rankPodiums = FindFirstObjectByType<RankPodiums>(FindObjectsInactive.Include);
            rankPodiums?.ClearOccupants();
            foreach (KeyValuePair<string, PlayerActor> pair in players)
            {
                int rank = ranks.TryGetValue(pair.Key, out int position) ? position : 0;
                pair.Value.SetTopRank(rank, mezzaninesEnabled);
                if (rank > 0) rankPodiums?.SetOccupant(rank, pair.Value);
            }
            ApplyVisibilityState();
        }

        public PlayerActor GetOrCreate(TikTokEvent data)
        {
            if (string.IsNullOrWhiteSpace(data.userId)) return null;
            if (players.TryGetValue(data.userId, out PlayerActor existing))
            {
                existing.UpdateIdentity(data);
                existing.SetPersonalHearts(data.personalHearts, personalLevelThreshold);
                existing.Touch();
                return existing;
            }

            if (players.Count >= maxPlayers) RemoveOldest();
            TikTokPlayerData playerData = new()
            {
                userId = data.userId,
                uniqueId = data.uniqueId,
                nickname = data.nickname,
                avatar = data.avatar,
                personalHearts = data.personalHearts,
                giftPower = data.giftPower
            };
            return Create(playerData);
        }

        public void Handle(TikTokEvent data)
        {
            if (data.type == "reset")
            {
                Clear();
                return;
            }
            if (data.type == "snapshot")
            {
                Clear();
                foreach (TikTokPlayerData player in data.players ?? System.Array.Empty<TikTokPlayerData>())
                    Create(player);
                return;
            }
            if (data.type is not ("member" or "chat" or "gift" or "like" or "follow" or "share")) return;

            if (data.spectatorOnly && Find(data.userId) == null) return;

            PlayerActor actor = data.preview ? previewActor : GetOrCreate(data);
            if (actor == null) return;

            if (data.type == "chat")
            {
                if (!string.IsNullOrWhiteSpace(data.action))
                {
                    ApplyAction(actor, data, data.durationMs > 0 ? data.durationMs / 1000f : 5f);
                }
                else
                {
                    string command = Normalize(data.comment);
                    if (command.Contains("doi nv")) actor.ChangeCharacter();
                    else if (command.Contains("di vong") || command.Contains("walk")) actor.Walk();
                    else if (command is "jump" or "nhay") actor.Jump();
                    else if (command.Contains("dance")) actor.Dance();
                }
            }
            else if (data.type == "gift")
            {
                actor.AddGiftPower(data.diamondCount);
                if (data.action is "change" or "wing" or "avatar_frame" or "medal" or "vip_set")
                {
                    if (!giftChanges.TryGetValue(actor, out Queue<TikTokEvent> queue))
                    {
                        queue = new Queue<TikTokEvent>();
                        giftChanges[actor] = queue;
                        queue.Enqueue(data);
                        StartCoroutine(PlayGiftChanges(actor, queue));
                    }
                    else queue.Enqueue(data);
                    return;
                }
                float duration = data.durationMs > 0 ? data.durationMs / 1000f : Mathf.Clamp(data.diamondCount, 3f, 7f);
                ApplyAction(actor, data, duration);
            }
            else if (data.type == "like") actor.ReactToLike();
            else if (data.type == "share" && !string.IsNullOrEmpty(data.action))
                ApplyAction(actor, data, data.durationMs > 0 ? data.durationMs / 1000f : 5f);
            else if (data.type is "follow" or "share") actor.Celebrate();
        }

        public void SpawnPreviewActor()
        {
            if (previewActor != null) return;
            Vector3 position = new(0f, 0f, 1.8f);
            GameObject sample = new("Interaction Preview Dancer");
            sample.transform.SetParent(transform);
            previewActor = sample.AddComponent<PlayerActor>();
            previewActor.Initialize(new TikTokPlayerData { userId = "master-test", nickname = "Thử tương tác" }, position, Color.white);
            previewActor.SetLevelThreshold(personalLevelThreshold);
            previewActor.SetCrowdSlot(position, 0.65f);
        }

        private System.Collections.IEnumerator PlayGiftChanges(PlayerActor actor, Queue<TikTokEvent> queue)
        {
            ClubCameraController camera = Camera.main?.GetComponent<ClubCameraController>();
            while (actor != null && queue.Count > 0)
            {
                TikTokEvent gift = queue.Dequeue();
                for (int index = 0; actor != null && index < Mathf.Max(1, gift.repeatCount); index++)
                {
                    ApplyAction(actor, gift, 0f);
                    camera?.Focus(actor, gift.zoomDurationMs > 0 ? gift.zoomDurationMs / 1000f : 3.5f, false, gift: true);
                    yield return new WaitForSeconds(0.65f);
                }
            }
            giftChanges.Remove(actor);
        }

        private static void ApplyAction(PlayerActor actor, TikTokEvent data, float duration)
        {
            if (data.action == "join") actor.ReturnToAssignedSlot();
            else if (data.action == "change") actor.ChangeCharacter();
            else if (data.action == "wing") actor.ChangeWing(0f);
            else if (data.action == "avatar_frame") actor.ShowGiftAvatarFrame(0f);
            else if (data.action == "walk") actor.Walk(duration);
            else if (data.action == "jump") actor.Jump(duration);
            else if (data.action == "grow") actor.Grow(duration);
            else if (data.action == "topdj") actor.ShowAtDj(duration);
            else if (data.action is "camera" or "vip" or "fireworks" or "medal" or "vip_set")
                actor.Celebrate(Mathf.Clamp(duration, 2.5f, 15f));
            else if (data.action == "dance") actor.Dance(duration);
            else if (data.action == "vip_dance") actor.QueueVipDance(duration);

            if (data.action is "medal" or "vip_set") actor.ChangeWing(0f);
            if (data.action == "vip_set") actor.ShowGiftAvatarFrame(0f);
            if (data.action is "medal" or "vip_set")
                actor.ShowTitle(string.IsNullOrWhiteSpace(data.label)  ? "KHÁCH VIP" : data.label,
                    0f, data.variant);
        }

        private PlayerActor Create(TikTokPlayerData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.userId)) return null;
            bool isNpc = data.userId.StartsWith("npc-");
            if (!isNpc) RemoveOneNpc();
            int index = players.Count;

            GameObject playerObject = new($"Player_{data.userId}");
            playerObject.transform.SetParent(transform);
            PlayerActor actor = playerObject.AddComponent<PlayerActor>();
            Color color = Color.HSVToRGB(Mathf.Repeat(index * 0.173f, 1f), 0.72f, 1f);
            actor.Initialize(data, Vector3.zero, color);
            actor.SetPersonalHearts(data.personalHearts, personalLevelThreshold);
            if (data.hasGiftWing) actor.ChangeWing(0f);
            if (data.hasGiftAvatarFrame) actor.ShowGiftAvatarFrame(0f);
            if (!string.IsNullOrWhiteSpace(data.titleLabel)) actor.ShowTitle(data.titleLabel, 0f, data.titleVariant);
            players[data.userId] = actor;
            playerOrder.Add(data.userId);
            crowdSlots[data.userId] = AllocateCrowdSlot();
            ReflowPlayers();
            ApplyVisibilityState();
            return actor;
        }

        private void ApplyVisibilityState()
        {
            if (string.IsNullOrEmpty(focusedUserId))
            {
                ApplyAmbientVisibility();
                return;
            }
            foreach (KeyValuePair<string, PlayerActor> pair in players)
                pair.Value.SetVisibility(pair.Key == focusedUserId ? 1f : FocusBackgroundAlpha(pair.Value));
        }

        private void ApplyAmbientVisibility()
        {
            foreach (PlayerActor actor in players.Values)
                actor.SetVisibility(actor.IsNpc ? NpcAmbientAlpha : 1f);
        }

        private static float FocusBackgroundAlpha(PlayerActor actor)
        {
            if (actor.IsTopRanked) return FocusedTopAlpha;
            return actor.IsNpc ? FocusedNpcAlpha : FocusedCrowdAlpha;
        }

        public void SetMezzaninesEnabled(bool enabled)
        {
            if (mezzaninesEnabled == enabled) return;
            mezzaninesEnabled = enabled;
            ReflowPlayers();
            foreach (PlayerActor actor in players.Values)
                if (actor.IsTopRanked) actor.SetTopRank(actor.TopRank, enabled);
        }

        private void ReflowPlayers()
        {
            int count = playerOrder.Count;
            float scale = count <= 30 ? 0.65f : count <= 80 ? 0.56f : count <= 140 ? 0.46f : count <= 200 ? 0.38f : count <= 280 ? 0.32f : 0.27f;
            const float xSpacing = 0.5f;
            float expansion = mezzaninesEnabled ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(12f, 130f, count)) : 0f;
            float frontEdge = Mathf.Lerp(3.75f, 12.55f, expansion);
            float backEdge = mezzaninesEnabled ? -3.45f : -3.65f;
            float zSpacing = (frontEdge - backEdge) / (CrowdRows - 1);
            for (int index = 0; index < playerOrder.Count; index++)
            {
                string userId = playerOrder[index];
                if (!players.TryGetValue(userId, out PlayerActor actor)) continue;
                if (!crowdSlots.TryGetValue(userId, out int slot))
                {
                    slot = AllocateCrowdSlot();
                    crowdSlots[userId] = slot;
                }
                int row = slot / CrowdColumns;
                int column = slot % CrowdColumns;
                float stagger = row % 2 == 0 ? 0f : xSpacing * 0.5f;
                float x = (column - (CrowdColumns - 1) * 0.5f) * xSpacing + stagger;
                // Stable offsets break up the rows without reshuffling the crowd on joins.
                float offsetX = mezzaninesEnabled ? SlotOffset(slot, 0x68bc21ebu) * xSpacing * 0.70f : 0f;
                float offsetZ = mezzaninesEnabled ? SlotOffset(slot, 0x02e5be93u) * zSpacing * 0.92f : 0f;
                x += offsetX;
                float z = frontEdge - row * zSpacing + offsetZ;
                float depthRatio = row / (float)(CrowdRows - 1);
                float perspectiveScale = scale * Mathf.Lerp(1.06f, 0.86f, depthRatio);
                int mezzanineSeat = index switch { 2 => 0, 3 => 1, 6 => 2, 7 => 3, _ => -1 };
                Vector3 position = mezzaninesEnabled && mezzanineSeat >= 0
                    ? ClubSceneBuilder.MezzanineSpot(mezzanineSeat)
                    : new Vector3(x, 0f, z);
                if (position.y == ClubSceneBuilder.MezzanineHeight)
                    perspectiveScale = Mathf.Max(0.54f, perspectiveScale);
                actor.SetCrowdSlot(position, perspectiveScale);
            }
        }

        private static float SlotOffset(int slot, uint salt)
        {
            uint value = (uint)slot ^ salt;
            value ^= value >> 16;
            value = unchecked(value * 0x7feb352du);
            value ^= value >> 15;
            value = unchecked(value * 0x846ca68bu);
            value ^= value >> 16;
            return (value & 0xffffu) / 65535f - 0.5f;
        }

        private int AllocateCrowdSlot()
        {
            HashSet<int> occupied = crowdSlots.Values.ToHashSet();
            List<int> available = Enumerable.Range(0, CrowdCapacity).Where(slot => !occupied.Contains(slot)).ToList();
            if (available.Count == 0) return Random.Range(0, CrowdCapacity);
            int minimumCellDistanceSquared = occupied.Count < 30 ? 9 : occupied.Count < 80 ? 4 : occupied.Count < 140 ? 2 : 1;
            List<int> separated = available.Where(candidate => occupied.All(other =>
            {
                int rowDelta = candidate / CrowdColumns - other / CrowdColumns;
                int columnDelta = candidate % CrowdColumns - other % CrowdColumns;
                return rowDelta * rowDelta + columnDelta * columnDelta >= minimumCellDistanceSquared;
            })).ToList();
            List<int> choices = separated.Count > 0 ? separated : available;
            return choices[Random.Range(0, choices.Count)];
        }

        private void Update()
        {
            float cutoff = Time.unscaledTime - playerTtlSeconds;
            foreach (string id in players.Where(pair => !pair.Value.IsNpc && !pair.Value.IsTopRanked && pair.Value.LastActiveTime < cutoff).Select(pair => pair.Key).ToArray())
                Remove(id);
        }

        public void Clear()
        {
            giftChanges.Clear();
            if (previewActor != null) Destroy(previewActor.gameObject);
            previewActor = null;
            foreach (PlayerActor actor in players.Values) Destroy(actor.gameObject);
            focusVersion++;
            focusedUserId = null;
            players.Clear();
            playerOrder.Clear();
            crowdSlots.Clear();
            rankPodiums?.ClearOccupants();
        }

        private void RemoveOldest()
        {
            KeyValuePair<string, PlayerActor> oldest = players.OrderBy(pair => pair.Value.IsTopRanked)
                .ThenBy(pair => pair.Value.LastActiveTime).FirstOrDefault();
            if (!string.IsNullOrEmpty(oldest.Key)) Remove(oldest.Key);
        }

        private void Remove(string id)
        {
            if (!players.Remove(id, out PlayerActor actor)) return;
            playerOrder.Remove(id);
            crowdSlots.Remove(id);
            Destroy(actor.gameObject);
            ReflowPlayers();
        }

        private void RemoveOneNpc()
        {
            string npcId = playerOrder.LastOrDefault(id => players.TryGetValue(id, out PlayerActor actor) && actor.IsNpc);
            if (string.IsNullOrEmpty(npcId)) return;
            players.Remove(npcId, out PlayerActor actorToRemove);
            playerOrder.Remove(npcId);
            crowdSlots.Remove(npcId);
            if (actorToRemove != null) Destroy(actorToRemove.gameObject);
        }

        private static string Normalize(string value)
        {
            string decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
            StringBuilder builder = new();
            foreach (char character in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
            return builder.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
        }
    }
}
