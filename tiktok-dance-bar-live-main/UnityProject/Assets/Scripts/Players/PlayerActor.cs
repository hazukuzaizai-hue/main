using System.Globalization;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class PlayerActor : MonoBehaviour
    {
        private Transform body;
        private SpriteRenderer characterRenderer;
        private static Material giftFloorMaterial;
        private LineRenderer giftFloorRing;
        private float giftFloorStarted;
        private const float GiftFloorDuration = 1.65f;
        private bool usesMezzanineDepth;
        private string titleStyle;
        private float customTitleUntil;
        private SpriteFlipbook flipbook;
        private SpriteRenderer avatarRenderer;
        private SpriteRenderer avatarFrameRenderer;
        private string avatarFrameId;
        private Sprite[] avatarFrameSprites;
        private string testAvatarFrame;
        private int avatarFrameRank;
        private float refreshAvatarFrameAt;
        private SpriteRenderer wingsRenderer;
        private SpriteRenderer bannerRenderer;
        private Transform wingAnchor;
        private TextMesh nameLabel;
        private long personalHearts;
        private int heartsPerLevel;
        private long personalLevel;
        private TextMesh titleLabel;
        private TextMesh titleShadowLabel;
        private Vector3 home;
        private Vector3 normalHome;
        private float phase;
        private float actionUntil;
        private bool vipDanceQueued;
        private float vipDanceDuration;
        private float vipDanceStarted;
        private DjAudioReactive danceAudio;
        private float walkStartedAt;
        private float walkDuration;
        private Vector3 walkOrigin;
        private float jumpStartedAt;
        private float jumpDuration;
        private ActionState action;
        private string characterName;
        private string wingId;
        private Sprite[] wingFrames;
        private float refreshWingAt;
        private Vector3 characterBaseScale = Vector3.one;
        private float lineupScale = 1f;
        private float crowdScale = 1f;
        private int topRank;
        private bool onPodium;
        private float djSpotUntil;
        private static PlayerActor guestDj;
        private Renderer defaultDjRenderer;
        private bool defaultDjWasVisible;
        private Vector3 PerformancePosition => Time.time < djSpotUntil
            ? new Vector3(0f, 2.55f, -8.35f)
            : home;
        private float effectScale = 1f;
        private float growUntil;
        private float visualAlpha = 1f;
        private float targetAlpha = 1f;
        private bool giftFocused;
        private Vector3 avatarBaseScale = Vector3.one;
        private float giftFocusScale = 1f;
        private float decorationScale = 1f;
        private float decorationBadgeY = 2.3f;
        private float bannerTextOffsetY;
        private string avatarUrl;
        private bool spawnDropPending;
        private bool spawnDropping;
        private float spawnDropStartedAt;

        private const float SpawnDropHeight = 6f;
        private const float SpawnDropDuration = 1.8f;
        private const float JumpHeight = 2f;

        public string UserId { get; private set; }
        public string Nickname { get; private set; }
        public int GiftPower { get; private set; }
        public float LastActiveTime { get; private set; }
        public bool IsTopRanked => topRank > 0;
        internal Sprite AvatarSprite => avatarRenderer != null ? avatarRenderer.sprite : null;
        internal Texture2D AvatarTexture => AvatarSprite?.texture;
        internal Bounds ComboBounds
        {
            get
            {
                Bounds bounds = characterRenderer.bounds;
                if (wingsRenderer.enabled) bounds.Encapsulate(wingsRenderer.bounds);
                if (bannerRenderer.enabled) bounds.Encapsulate(bannerRenderer.bounds);
                bounds.Encapsulate(avatarRenderer.bounds);
                bounds.Encapsulate(nameLabel.GetComponent<Renderer>().bounds);
                return bounds;
            }
        }
        internal int TopRank => topRank;
        private bool flying;

        internal void ApplyFlightPose(Vector3 position, Quaternion rotation, float height)
        {
            flying = true;
            transform.SetPositionAndRotation(position, rotation);
            float spriteHeight = VisibleCharacterHeight(characterBaseScale.y);
            transform.localScale = Vector3.one * Mathf.Clamp(height / Mathf.Max(0.1f, spriteHeight), 0.2f, 4f);
            if (giftFloorRing != null) giftFloorRing.enabled = false;
        }

        internal void EndFlight()
        {
            if (!flying) return;
            flying = false;
            transform.SetPositionAndRotation(home, Quaternion.identity);
            transform.localScale = Vector3.one;
        }
        public bool IsNpc => !string.IsNullOrEmpty(UserId) && UserId.StartsWith("npc-");
        private bool UsesSyntheticAvatar => !string.IsNullOrEmpty(UserId) &&
            (UserId.StartsWith("npc-") || UserId.StartsWith("demo-") || UserId == "master-test");

        public void Initialize(TikTokPlayerData data, Vector3 position, Color color)
        {
            UserId = data.userId;
            Nickname = string.IsNullOrWhiteSpace(data.nickname) ? data.uniqueId : data.nickname;
            GiftPower = Mathf.Max(0, data.giftPower);
            home = normalHome = position;
            transform.position = position;
            spawnDropPending = true;
            phase = Random.value * Mathf.PI * 2f;

            GameObject character = new("Dancer");
            character.transform.SetParent(transform, false);
            body = character.transform;
            characterRenderer = character.AddComponent<SpriteRenderer>();
            characterRenderer.sharedMaterial = GameMaterials.Sprite();
            characterRenderer.sortingOrder = Mathf.RoundToInt((12f - position.z) * 100f);
            flipbook = character.AddComponent<SpriteFlipbook>();
            ChangeCharacter();

            GameObject wingAnchorObject = new("Wing Anchor");
            wingAnchorObject.transform.SetParent(transform, false);
            wingAnchor = wingAnchorObject.transform;
            GameObject wingsObject = new("Wings");
            wingsObject.transform.SetParent(wingAnchor, false);
            wingsRenderer = wingsObject.AddComponent<SpriteRenderer>();
            wingsRenderer.sharedMaterial = GameMaterials.Sprite();
            wingsRenderer.sortingOrder = characterRenderer.sortingOrder - 2;
            wingsRenderer.enabled = false;

            GameObject bannerObject = new("Status Banner");
            bannerObject.transform.SetParent(transform, false);
            bannerRenderer = bannerObject.AddComponent<SpriteRenderer>();
            bannerRenderer.sharedMaterial = GameMaterials.Sprite();
            bannerRenderer.sortingOrder = characterRenderer.sortingOrder + 8;
            bannerRenderer.enabled = false;

            GameObject avatarObject = new("Avatar");
            avatarObject.transform.SetParent(transform, false);
            avatarObject.transform.localPosition = new Vector3(-0.75f, 2.1f, 0f);
            avatarRenderer = avatarObject.AddComponent<SpriteRenderer>();
            avatarRenderer.sharedMaterial = GameMaterials.CircularAvatar();
            avatarRenderer.sortingOrder = characterRenderer.sortingOrder + 12;
            avatarRenderer.enabled = false;

            GameObject labelObject = new("Name");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0.18f, 2.1f, 0f);
            nameLabel = labelObject.AddComponent<TextMesh>();
            RefreshNameLabel();
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.fontSize = 40;
            nameLabel.characterSize = 0.032f;
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = characterRenderer.sortingOrder + 10;

            titleLabel = CreateLabel("Title", new Vector3(0f, -0.25f, 0f), 36, new Color(0.3f, 0.95f, 1f), characterRenderer.sortingOrder + 14);
            titleLabel.characterSize = 0.04f;
            titleLabel.fontStyle = FontStyle.Bold;
            titleLabel.color = Color.white;
            titleLabel.gameObject.SetActive(false);
            titleShadowLabel = CreateLabel("Title Shadow", new Vector3(0f, -0.25f, 0f), 36, new Color(0f, 0f, 0f, 0.92f), characterRenderer.sortingOrder + 13);
            titleShadowLabel.characterSize = 0.04f;
            titleShadowLabel.fontStyle = FontStyle.Bold;
            titleShadowLabel.gameObject.SetActive(false);
            if (UsesSyntheticAvatar) LoadSampleAvatar();
            else LoadAvatar(data.avatar);
            ApplyEvolution();
            Touch();
        }

        private void LoadSampleAvatar()
        {
            const int avatarCount = 16;
            uint hash = 2166136261;
            foreach (char character in UserId)
                hash = unchecked((hash ^ character) * 16777619);
            int index = (int)(hash % avatarCount);
            ApplyAvatar(Resources.Load<Sprite>($"NpcAvatars/npc-avatar-{index:00}"));
        }

        private void LoadAvatar(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == avatarUrl) return;
            avatarUrl = url;
            string requestedUrl = url;
            AvatarService.Instance?.Load(url, sprite =>
            {
                if (this != null && avatarUrl == requestedUrl && sprite != null) ApplyAvatar(sprite);
            });
        }

        public void UpdateIdentity(TikTokEvent data)
        {
            if (data == null) return;
            string nextNickname = !string.IsNullOrWhiteSpace(data.nickname)
                ? data.nickname
                : !string.IsNullOrWhiteSpace(data.uniqueId) ? data.uniqueId : Nickname;
            if (!string.IsNullOrWhiteSpace(nextNickname) && nextNickname != Nickname)
            {
                Nickname = nextNickname;
                RefreshNameLabel();
            }
            if (!UsesSyntheticAvatar) LoadAvatar(data.avatar);
        }

        internal void SetPersonalHearts(long hearts, int threshold)
        {
            personalHearts = hearts;
            long level = threshold > 0 ? hearts / threshold : 0;
            bool labelChanged = personalLevel != level || (heartsPerLevel > 0) != (threshold > 0);
            heartsPerLevel = threshold;
            personalLevel = level;
            if (labelChanged) RefreshNameLabel();
        }

        internal void SetLevelThreshold(int threshold) => SetPersonalHearts(personalHearts, threshold);

        internal void PreviewLevelUp(int threshold) => SetPersonalHearts(personalHearts + threshold, threshold);

        private void RefreshNameLabel()
        {
            if (nameLabel != null)
                nameLabel.text = FormatDisplayName(Nickname) + (heartsPerLevel > 0 ? $" · Lv.{personalLevel}" : "");
        }

        private static string FormatDisplayName(string value)
        {
            const int maxCharacters = 16;
            string displayName = string.IsNullOrWhiteSpace(value) ? "Dancer" : value.Trim();
            int[] characters = StringInfo.ParseCombiningCharacters(displayName);
            return characters.Length <= maxCharacters
                ? displayName
                : displayName[..characters[maxCharacters - 1]].TrimEnd() + "…";
        }

        private TextMesh CreateLabel(string objectName, Vector3 position, int fontSize, Color color, int order)
        {
            GameObject labelObject = new(objectName);
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = fontSize;
            label.characterSize = 0.032f;
            label.color = color;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = order;
            return label;
        }

        private void ApplyAvatar(Sprite sprite)
        {
            if (this == null || avatarRenderer == null || sprite == null) return;
            avatarRenderer.sprite = sprite;
            nameLabel.anchor = TextAnchor.MiddleLeft;
            nameLabel.alignment = TextAlignment.Left;
            float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            avatarBaseScale = Vector3.one * (size > 0f ? 0.32f / size : 1f);
            avatarRenderer.transform.localScale = avatarBaseScale * Mathf.Clamp(decorationScale, 0.72f, 0.88f);
            avatarRenderer.enabled = true;
            RefreshBadgeVisibility();
            ApplyAlpha();
        }

        public void Touch() => LastActiveTime = Time.unscaledTime;

        public void AddGiftPower(int diamonds)
        {
            GiftPower += Mathf.Max(0, diamonds);
            characterBaseScale = Vector3.one * CharacterScale() * GiftPowerScale() * lineupScale;
            ApplyEvolution();
            Touch();
        }

        private float GiftPowerScale()
        {
            // Ranking controls the size of Top 1-3. Gift totals may only make a
            // regular dancer slightly larger so nobody suddenly fills the frame.
            return topRank > 0 ? 1f : 1f + Mathf.Min(0.18f, GiftPower / 3500f);
        }

        private void ApplyEvolution()
        {
            if (titleLabel == null) return;
            if (Time.time < customTitleUntil)
            {
                RefreshDecorations();
                return;
            }
            titleStyle = null;
            SetTitleText(string.Empty);
            SetTitleVisible(false);
            RefreshDecorations();
        }

        public void ShowTitle(string title, float seconds, string style = null)
        {
            if (titleLabel == null || (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(style))) return;
            titleStyle = style is "aura" or "neon" or "fire" or "royal" ? style : null;
            float lifetime = seconds <= 0f ? float.PositiveInfinity : Mathf.Max(2f, seconds);
            customTitleUntil = Time.time + lifetime;
            if (!string.IsNullOrWhiteSpace(title))
            {
                SetTitleText(FormatBannerTitle(title));
                SetTitleVisible(true);
            }
            RefreshDecorations();
            CancelInvoke(nameof(ExpireCustomTitle));
            if (!float.IsPositiveInfinity(lifetime)) Invoke(nameof(ExpireCustomTitle), lifetime);
        }

        private void ExpireCustomTitle()
        {
            customTitleUntil = 0f;
            ApplyEvolution();
        }

        private static string FormatBannerTitle(string value)
        {
            string title = value?.Trim() ?? string.Empty;
            return title.Length <= 16 ? title : title[..15] + "…";
        }

        private void SetTitleText(string value)
        {
            titleLabel.text = value;
            if (titleShadowLabel != null) titleShadowLabel.text = value;
        }

        private void SetTitleVisible(bool visible)
        {
            titleLabel.gameObject.SetActive(visible);
            if (titleShadowLabel != null) titleShadowLabel.gameObject.SetActive(visible);
        }

        public void SetTopRank(int rank, bool raisedPodium)
        {
            int previousRank = topRank;
            bool previouslyOnPodium = onPodium;
            topRank = Mathf.Clamp(rank, 0, 3);
            onPodium = topRank > 0 && raisedPodium;
            lineupScale = LineupScaleForRank();
            home = topRank > 0 ? RankPodiums.StandingPosition(topRank, raisedPodium) : normalHome;
            if (action == ActionState.Walk && (previouslyOnPodium != onPodium || (onPodium && previousRank != topRank)))
                action = ActionState.Idle;
            UpdateMezzanineDepth();
            characterBaseScale = Vector3.one * CharacterScale() * GiftPowerScale() * lineupScale;
            RefreshBadgeVisibility();
            if (topRank > 0 && topRank != previousRank && action != ActionState.Walk) Celebrate(3f);
        }

        private float LineupScaleForRank()
        {
            return topRank switch
            {
                1 => Mathf.Clamp(crowdScale * 1.28f, 0.68f, 0.78f),
                2 => Mathf.Clamp(crowdScale * 1.18f, 0.64f, 0.72f),
                3 => Mathf.Clamp(crowdScale * 1.1f, 0.6f, 0.68f),
                _ => crowdScale
            };
        }

        public void SetCrowdSlot(Vector3 position, float scale)
        {
            if (topRank == 0 && !Mathf.Approximately(normalHome.y, position.y) && !spawnDropPending && !spawnDropping)
            {
                if (action == ActionState.Walk || action == ActionState.Jump) action = ActionState.Idle;
                transform.position = position;
            }
            normalHome = position;
            crowdScale = Mathf.Clamp(scale, 0.26f, 1f);
            if (topRank == 0)
            {
                home = normalHome;
            }
            UpdateMezzanineDepth();
            if (spawnDropPending)
            {
                spawnDropPending = false;
                spawnDropping = true;
                spawnDropStartedAt = Time.time;
                transform.position = home + Vector3.up * SpawnDropHeight;
            }
            lineupScale = LineupScaleForRank();
            characterBaseScale = Vector3.one * CharacterScale() * GiftPowerScale() * lineupScale;
            RefreshBadgeVisibility();
        }

        private void UpdateMezzanineDepth()
        {
            bool onMezzanine = home.y == ClubSceneBuilder.MezzanineHeight || Time.time < djSpotUntil;
            if (usesMezzanineDepth == onMezzanine) return;
            usesMezzanineDepth = onMezzanine;
            float depthTest = (float)(onMezzanine
                ? UnityEngine.Rendering.CompareFunction.LessEqual
                : UnityEngine.Rendering.CompareFunction.Always);
            characterRenderer.sharedMaterial.SetFloat("_ZTest", depthTest);
            wingsRenderer.sharedMaterial.SetFloat("_ZTest", depthTest);
            bannerRenderer.sharedMaterial.SetFloat("_ZTest", depthTest);
            avatarRenderer.sharedMaterial.SetFloat("_ZTest", depthTest);
        }

        private void RefreshBadgeVisibility()
        {
            // Keep every dancer identifiable in every row. Perspective scaling
            // already makes badges smaller toward the back, so hiding them based
            // on crowdScale made rear-row players look anonymous.
            nameLabel.gameObject.SetActive(true);
            if (avatarRenderer.sprite != null) avatarRenderer.enabled = true;
            if (titleLabel != null && Time.time >= customTitleUntil && !giftFocused && topRank == 0 && GiftPower < 10 && crowdScale < 0.56f) SetTitleVisible(false);
            else ApplyEvolution();
            RefreshDecorations();
        }

        private void RefreshDecorations()
        {
            if (wingsRenderer == null || bannerRenderer == null) return;
            bool decorated = Time.time < customTitleUntil;
            string variant = topRank == 1 || GiftPower >= 1000 ? "royal"
                : topRank == 2 || GiftPower >= 200 ? "fire"
                : topRank == 3 || GiftPower >= 50 ? "neon"
                : "ice";
            bool customStyle = !string.IsNullOrEmpty(titleStyle);
            if (customStyle) variant = titleStyle == "aura" ? "ice" : titleStyle;
            bannerTextOffsetY = variant switch
            {
                "royal" => -0.075f,
                "fire" => -0.025f,
                _ => -0.01f
            };
            bannerRenderer.sprite = (customStyle ? null : RuntimeAssetLibrary.Selected("banner")) ?? Resources.Load<Sprite>($"Banners/title-{variant}");
            decorationScale = giftFocused ? Mathf.Max(0.55f, lineupScale) : lineupScale;
            float identityScale = Mathf.Clamp(decorationScale, 0.72f, 0.88f);
            if (bannerRenderer.sprite != null)
            {
                float bannerWidth = bannerRenderer.sprite.bounds.size.x;
                bannerRenderer.transform.localScale = Vector3.one * (bannerWidth > 0f ? 1.58f * identityScale / bannerWidth : 1f);
            }
            float displayedCharacterHeight = Mathf.Max(0.1f, VisibleCharacterHeight(characterBaseScale.y * giftFocusScale));
            decorationBadgeY = displayedCharacterHeight + 0.22f;
            avatarRenderer.transform.localScale = avatarBaseScale * identityScale;
            nameLabel.transform.localScale = Vector3.one * identityScale;
            float titleScale = titleLabel.text.Length <= 9 ? 0.68f : titleLabel.text.Length <= 14 ? 0.59f : 0.5f;
            titleLabel.transform.localScale = Vector3.one * identityScale * titleScale;
            if (titleShadowLabel != null) titleShadowLabel.transform.localScale = titleLabel.transform.localScale;
            UpdateDecorationPositions(0f);
            wingsRenderer.enabled = Time.unscaledTime < giftWingUntil;
            UpdateAssignedWing();
            bannerRenderer.enabled = decorated && bannerRenderer.sprite != null && !string.IsNullOrWhiteSpace(titleLabel.text);
        }

        private void UpdateAssignedWing()
        {
            if (assetTest || !wingsRenderer.enabled) return;
            float now = Time.unscaledTime;
            if (wingFrames == null || now >= refreshWingAt)
            {
                refreshWingAt = now + 2f;
                string assigned = RuntimeAssetLibrary.AssignWing(wingId);
                if (assigned != wingId || wingFrames == null)
                {
                    wingId = assigned;
                    wingFrames = RuntimeAssetLibrary.Load(wingId);
                }
            }
            wingsRenderer.sprite = wingFrames.Length > 0
                ? wingFrames[Mathf.FloorToInt((now + phase) * 12f) % wingFrames.Length]
                : null;
        }

        private float giftWingUntil;
        private float giftAvatarFrameUntil;
        private string giftAvatarFrame;

        internal void ChangeWing(float duration)
        {
            wingId = RuntimeAssetLibrary.RotateWing(wingId);
            wingFrames = RuntimeAssetLibrary.Load(wingId);
            giftWingUntil = duration <= 0f ? float.PositiveInfinity : Time.unscaledTime + Mathf.Max(2f, duration);
            RefreshDecorations();
        }

        internal void ShowGiftAvatarFrame(float duration)
        {
            giftAvatarFrame = RuntimeAssetLibrary.PickAvatarFrame(giftAvatarFrame);
            giftAvatarFrameUntil = duration <= 0f ? float.PositiveInfinity : Time.unscaledTime + Mathf.Max(2f, duration);
            refreshAvatarFrameAt = 0f;
        }

        private void UpdateAvatarFrame()
        {
            string activeGiftFrame = Time.unscaledTime < giftAvatarFrameUntil ? giftAvatarFrame : null;
            if (giftAvatarFrame != null && activeGiftFrame == null)
            {
                giftAvatarFrame = null;
                refreshAvatarFrameAt = 0f;
            }
            bool canShow = avatarRenderer.sprite != null &&
                (!string.IsNullOrEmpty(testAvatarFrame ?? activeGiftFrame) || (!UsesSyntheticAvatar && topRank > 0));
            if (!canShow)
            {
                if (avatarFrameRenderer != null) avatarFrameRenderer.enabled = false;
                return;
            }

            float now = Time.unscaledTime;
            if (avatarFrameSprites == null || avatarFrameRank != topRank || now >= refreshAvatarFrameAt)
            {
                avatarFrameRank = topRank;
                refreshAvatarFrameAt = now + 2f;
                string id = testAvatarFrame ?? activeGiftFrame ?? RuntimeAssetLibrary.AvatarFrameId(topRank);
                if (avatarFrameSprites == null || id != avatarFrameId)
                {
                    avatarFrameId = id;
                    avatarFrameSprites = RuntimeAssetLibrary.Load(id);
                }
            }

            if (avatarFrameSprites.Length == 0)
            {
                if (avatarFrameRenderer != null) avatarFrameRenderer.enabled = false;
                return;
            }
            if (avatarFrameRenderer == null)
            {
                GameObject frame = new("Avatar Frame");
                frame.transform.SetParent(avatarRenderer.transform, false);
                avatarFrameRenderer = frame.AddComponent<SpriteRenderer>();
                avatarFrameRenderer.sharedMaterial = GameMaterials.Sprite();
                avatarFrameRenderer.sortingOrder = avatarRenderer.sortingOrder + 1;
            }

            Sprite sprite = avatarFrameSprites[Mathf.FloorToInt(now * 12f) % avatarFrameSprites.Length];
            if (avatarFrameRenderer.sprite != sprite)
            {
                avatarFrameRenderer.sprite = sprite;
                float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                avatarFrameRenderer.transform.localScale = Vector3.one *
                    (avatarRenderer.sprite.bounds.size.x * 1.3f / Mathf.Max(0.01f, size));
            }
            avatarFrameRenderer.enabled = true;
            avatarFrameRenderer.color = avatarRenderer.color;
        }

        private void UpdateDecorationPositions(float bounce)
        {
            if (wingsRenderer == null || bannerRenderer == null || wingAnchor == null || body == null || avatarRenderer == null || nameLabel == null || titleLabel == null || titleShadowLabel == null) return;
            float attachedBounce = bounce * 0.72f;
            float characterHeight = Mathf.Max(0.1f, VisibleCharacterHeight(characterBaseScale.y * giftFocusScale));
            wingAnchor.localPosition = new Vector3(0f, body.localPosition.y + characterHeight * 0.25f, 0.12f);
            if (wingsRenderer.enabled && wingsRenderer.sprite != null)
            {
                float characterWidth = characterRenderer.sprite != null
                    ? characterRenderer.sprite.bounds.size.x * body.localScale.x
                    : characterHeight;
                float spread = Mathf.Max(characterHeight * 1.68f, characterWidth * 1.52f);
                float limit = usesMezzanineDepth ? 3.0f : 3.75f;
                float scale = Mathf.Clamp(spread, 0.95f, limit) / Mathf.Max(0.01f, wingsRenderer.sprite.bounds.size.x);
                float flutter = Mathf.Sin((Time.time + phase) * 6.2f);
                wingsRenderer.transform.localScale = new Vector3(scale * (1f + flutter * 0.045f),
                    scale * (1f - flutter * 0.022f), scale);
            }
            wingAnchor.localRotation = body.localRotation;
            float identityScale = Mathf.Clamp(decorationScale, 0.72f, 0.88f);
            // The main camera faces toward negative Z, so positive world X appears
            // on the left side of the portrait frame.
            float labelDirection = flying ? -1f : 1f;
            avatarRenderer.transform.localPosition = new Vector3(0.43f * identityScale * labelDirection, decorationBadgeY + attachedBounce, -0.01f);
            float nameX = avatarRenderer.sprite != null ? 0.22f * identityScale * labelDirection : 0f;
            nameLabel.transform.localPosition = new Vector3(nameX, decorationBadgeY + attachedBounce, -0.02f);
            float bannerY = decorationBadgeY + 0.53f * identityScale + attachedBounce;
            bannerRenderer.transform.localPosition = new Vector3(0f, bannerY, -0.01f);
            float titleY = bannerY + bannerTextOffsetY * identityScale;
            titleLabel.transform.localPosition = new Vector3(0f, titleY, -0.02f);
            titleShadowLabel.transform.localPosition = new Vector3(-0.014f * identityScale, titleY - 0.018f * identityScale, -0.015f);
        }

        private bool assetTest;
        private string testWing;
        private string testBanner;
        public void SetTestCharacter(string id)
        {
            Sprite[] frames = RuntimeAssetLibrary.Load(id);
            if (frames.Length == 0) return;
            assetTest = true;
            characterName = id;
            flipbook.SetFrames(frames, 12f);
            characterBaseScale = Vector3.one * CharacterScale() * GiftPowerScale() * lineupScale;
            body.localScale = characterBaseScale;
            Dance(60f);
        }
        public void SetTestDecoration(string kind, string id, string text)
        {
            assetTest = true;
            if (kind == "wing") testWing = id;
            if (kind == "banner") testBanner = id;
            if (kind == "avatarFrame")
            {
                testAvatarFrame = id;
                refreshAvatarFrameAt = 0f;
            }
            SetGiftFocus(true);
            ShowTitle(text, 60f);
            Dance(60f);
        }
        private void UpdateTestDecoration()
        {
            if (!assetTest) return;
            if (!string.IsNullOrEmpty(testWing))
            {
                Sprite[] frames = RuntimeAssetLibrary.Load(testWing);
                if (frames.Length > 0) { wingsRenderer.sprite = frames[Mathf.FloorToInt(Time.time * 12f) % frames.Length]; wingsRenderer.enabled = true; }
            }
            if (!string.IsNullOrEmpty(testBanner))
            {
                Sprite[] frames = RuntimeAssetLibrary.Load(testBanner);
                if (frames.Length > 0) { bannerRenderer.sprite = frames[Mathf.FloorToInt(Time.time * 12f) % frames.Length]; bannerRenderer.enabled = true; bannerRenderer.transform.localScale = Vector3.one * (1.4f / Mathf.Max(0.01f, bannerRenderer.sprite.bounds.size.x)); }
            }
        }

        public void ChangeCharacter()
        {
            (string nextName, Sprite[] frames) = CharacterLibrary.RandomCharacter(characterName);
            characterName = nextName;
            flipbook.SetFrames(frames, nextName == "hanhan_video_dance" ? 15f : Random.Range(10f, 14f));
            characterBaseScale = Vector3.one * CharacterScale() * GiftPowerScale() * lineupScale;
            body.localScale = characterBaseScale;
            Celebrate(1.2f);
        }

        private float CharacterScale()
        {
            Sprite sprite = characterRenderer.sprite;
            return sprite == null || sprite.bounds.size.y <= 0f ? 1f : 2.15f / sprite.bounds.size.y;
        }

        public void Dance(float seconds = 5f)
        {
            if (danceAudio == null) danceAudio = FindFirstObjectByType<DjAudioReactive>();
            SetAction(ActionState.Dance, seconds);
        }

        internal void QueueVipDance(float seconds)
        {
            vipDanceQueued = true;
            vipDanceDuration = Mathf.Clamp(seconds, 3f, 10f);
        }

        internal void BeginQueuedVipDance()
        {
            if (!vipDanceQueued) return;
            vipDanceQueued = false;
            spawnDropPending = spawnDropping = false;
            vipDanceStarted = Time.time;
            SetAction(ActionState.VipDance, vipDanceDuration);
        }

        public void Jump(float seconds = 0.95f)
        {
            spawnDropPending = false;
            spawnDropping = false;
            transform.position = home;
            jumpStartedAt = Time.time;
            jumpDuration = Mathf.Clamp(seconds, 0.75f, 1.5f);
            SetAction(ActionState.Jump, jumpDuration);
        }
        public void Walk(float seconds = 5f)
        {
            spawnDropPending = false;
            spawnDropping = false;
            walkOrigin = onPodium || home.y == ClubSceneBuilder.MezzanineHeight
                ? home
                : new Vector3(transform.position.x, 0f, transform.position.z);
            transform.position = walkOrigin;
            walkStartedAt = Time.time;
            walkDuration = Mathf.Max(1f, seconds);
            SetAction(ActionState.Walk, walkDuration);
        }
        public void Celebrate(float seconds = 4f) => SetAction(ActionState.Celebrate, seconds);

        internal void ShowAtDj(float seconds)
        {
            if (guestDj != null && guestDj != this) guestDj.EndDjSpot();
            if (guestDj != this)
            {
                defaultDjRenderer = GameObject.Find("Main Character GIF")?.GetComponent<Renderer>();
                defaultDjWasVisible = defaultDjRenderer != null && defaultDjRenderer.enabled;
            }
            guestDj = this;
            if (defaultDjRenderer != null) defaultDjRenderer.enabled = false;
            djSpotUntil = Time.time + Mathf.Max(2f, seconds);
            spawnDropPending = false;
            spawnDropping = false;
            transform.position = PerformancePosition;
            UpdateMezzanineDepth();
            Celebrate(Mathf.Max(2f, seconds));
        }

        private void EndDjSpot()
        {
            djSpotUntil = 0f;
            if (guestDj == this)
            {
                if (defaultDjRenderer != null) defaultDjRenderer.enabled = defaultDjWasVisible;
                guestDj = null;
            }
            UpdateMezzanineDepth();
        }

        private void OnDisable()
        {
            if (guestDj == this) EndDjSpot();
        }

        public void ReactToLike()
        {
            if (action == ActionState.Idle) Celebrate(1.25f);
        }
        public void Grow(float seconds = 8f)
        {
            effectScale = 1.85f;
            growUntil = Time.time + Mathf.Max(2f, seconds);
            Celebrate(seconds);
        }

        public void SetDimmed(bool dimmed)
        {
            targetAlpha = dimmed ? 0.1f : 1f;
        }

        public void SetVisibility(float alpha)
        {
            targetAlpha = Mathf.Clamp01(alpha);
        }

        public void PulseGiftFloor()
        {
            if (flying) return;
            if (giftFloorRing == null)
            {
                if (giftFloorMaterial == null)
                {
                    giftFloorMaterial = GameMaterials.BackgroundSprite();
                    giftFloorMaterial.mainTexture = Texture2D.whiteTexture;
                    giftFloorMaterial.renderQueue = 3070;
                }
                GameObject ring = new("VIP Gift Floor Ring");
                ring.transform.SetParent(transform, false);
                giftFloorRing = ring.AddComponent<LineRenderer>();
                giftFloorRing.useWorldSpace = false;
                giftFloorRing.loop = true;
                giftFloorRing.alignment = LineAlignment.TransformZ;
                giftFloorRing.positionCount = 64;
                giftFloorRing.sharedMaterial = giftFloorMaterial;
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int point = 0; point < giftFloorRing.positionCount; point++)
                {
                    float angle = point * 2f * Mathf.PI / giftFloorRing.positionCount;
                    giftFloorRing.SetPosition(point, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                }
            }
            giftFloorStarted = Time.time;
            giftFloorRing.enabled = true;
            UpdateGiftFloorRing();
        }

        private void UpdateGiftFloorRing()
        {
            if (giftFloorRing == null || !giftFloorRing.enabled) return;
            float progress = (Time.time - giftFloorStarted) / GiftFloorDuration;
            if (progress >= 1f)
            {
                giftFloorRing.enabled = false;
                return;
            }

            bool raisedPlatform = onPodium || home.y == ClubSceneBuilder.MezzanineHeight;
            float outerRadius = onPodium ? 0.77f : raisedPlatform ? 0.83f : 1.68f;
            float radius = Mathf.Lerp(0.32f, outerRadius, 1f - Mathf.Pow(1f - progress, 2f));
            giftFloorRing.transform.position = new Vector3(transform.position.x, home.y + (raisedPlatform ? 0.035f : 0.16f), transform.position.z);
            giftFloorRing.transform.localScale = new Vector3(radius, radius, 1f);
            giftFloorRing.widthMultiplier = Mathf.Lerp(0.095f, 0.025f, progress);
            Color tint = Color.Lerp(new Color(1f, 0.77f, 0.28f), new Color(1f, 0.42f, 0.76f), progress);
            tint.a = (1f - progress) * 0.88f;
            giftFloorRing.startColor = giftFloorRing.endColor = tint;
        }

        public void SetGiftFocus(bool focused)
        {
            giftFocused = focused;
            giftFocusScale = focused ? 1.08f : 1f;
            RefreshBadgeVisibility();
            if (focused && action is not (ActionState.Walk or ActionState.Dance or ActionState.VipDance)) Celebrate(3f);
        }

        public void ReturnToAssignedSlot()
        {
            if (!flying) transform.position = PerformancePosition;
        }

        private void SetAction(ActionState next, float seconds)
        {
            action = next;
            actionUntil = Time.time + seconds;
            Touch();
        }

        private void Update()
        {
            if (guestDj == this && Time.time >= djSpotUntil) EndDjSpot();
            if (giftWingUntil > 0f && Time.unscaledTime >= giftWingUntil)
            {
                giftWingUntil = 0f;
                RefreshDecorations();
            }
            UpdateTestDecoration();
            UpdateAvatarFrame();
            if (wingsRenderer != null) UpdateAssignedWing();
            if (!assetTest && bannerRenderer != null && bannerRenderer.enabled) { Sprite customBanner = RuntimeAssetLibrary.Selected("banner"); if (customBanner != null) bannerRenderer.sprite = customBanner; }
            if (Time.time >= actionUntil && action != ActionState.Idle) action = ActionState.Idle;
            if (!flying) UpdateBarPosition();
            if (effectScale > 1f && Time.time >= growUntil) effectScale = Mathf.Lerp(effectScale, 1f, 5f * Time.deltaTime);
            visualAlpha = Mathf.Lerp(visualAlpha, targetAlpha, 1f - Mathf.Exp(-7f * Time.deltaTime));
            ApplyAlpha();

            float time = Time.time + phase;
            bool isPerforming = !flying && action is (ActionState.Dance or ActionState.Jump or ActionState.Celebrate or ActionState.VipDance);
            bool dancing = !flying && action == ActionState.Dance;
            float bass = dancing && danceAudio != null ? danceAudio.Bass : 0f;
            flipbook.SetPlaybackSpeed(isPerforming ? 2.7f : 1.5f);
            float bounce = isPerforming ? Mathf.Abs(Mathf.Sin(time * 13.125f)) * 0.16f : 0f;
            if (dancing) bounce = Mathf.Abs(Mathf.Sin(Time.time * 6.5625f)) * (0.4f + bass * 0.24f);
            body.localRotation = isPerforming
                ? Quaternion.Euler(0f, Mathf.Sin(time * 3.75f) * 5f, Mathf.Sin(time * 9.375f) * 2.5f)
                : Quaternion.identity;
            if (dancing) body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 6.5625f) * (12f + bass * 5f));
            float pulse = action == ActionState.Celebrate ? 1f + Mathf.Sin(time * 16.875f) * 0.08f : 1f;
            if (dancing) pulse = 1f + bass * 0.08f;
            float djScale = Time.time < djSpotUntil ? 1.6f / Mathf.Max(0.1f, lineupScale) : 1f;
            Vector3 displayedScale = characterBaseScale * pulse * effectScale * giftFocusScale * djScale;
            body.localScale = displayedScale;
            float rawHeight = characterRenderer.sprite == null ? 0f : characterRenderer.sprite.bounds.size.y * displayedScale.y;
            float groundOffset = characterRenderer.sprite == null
                ? 0f
                : -characterRenderer.sprite.bounds.min.y * displayedScale.y - rawHeight * BottomPaddingFraction();
            body.localPosition = Vector3.up * (groundOffset + bounce);
            if (!flying && action == ActionState.VipDance)
            {
                float progress = Mathf.Clamp01((Time.time - vipDanceStarted) / vipDanceDuration);
                float flip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.65f, progress));
                body.localRotation = Quaternion.Euler(0f, 0f, -720f * flip);
                Vector3 pivot = characterRenderer.sprite == null ? Vector3.up
                    : characterRenderer.sprite.bounds.center * displayedScale.y;
                body.localPosition += pivot - body.localRotation * pivot;
            }
            UpdateDecorationPositions(bounce);
            UpdateGiftFloorRing();

            Camera camera = Camera.main;
            if (camera != null)
            {
                nameLabel.transform.rotation = camera.transform.rotation;
                titleLabel.transform.rotation = camera.transform.rotation;
                titleShadowLabel.transform.rotation = camera.transform.rotation;
                avatarRenderer.transform.rotation = camera.transform.rotation;
                bannerRenderer.transform.rotation = camera.transform.rotation;
            }
        }

        private void UpdateBarPosition()
        {
            if (action == ActionState.VipDance)
            {
                float progress = Mathf.Clamp01((Time.time - vipDanceStarted) / vipDanceDuration);
                float lift = Mathf.SmoothStep(0f, 1f, progress / 0.2f) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, progress)));
                float orbit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.82f, progress));
                float angle = orbit * Mathf.PI * 2f;
                transform.position = PerformancePosition + new Vector3(Mathf.Sin(angle) * 0.9f,
                    lift * 2.4f, (1f - Mathf.Cos(angle)) * 0.4f);
                return;
            }
            if (Time.time < djSpotUntil)
            {
                transform.position = PerformancePosition;
                return;
            }
            if (spawnDropping)
            {
                float progress = Mathf.Clamp01((Time.time - spawnDropStartedAt) / SpawnDropDuration);
                float heightOffset;
                if (progress < 0.72f)
                {
                    float fallProgress = progress / 0.72f;
                    heightOffset = SpawnDropHeight * (1f - fallProgress * fallProgress);
                }
                else
                {
                    float bounceProgress = (progress - 0.72f) / 0.28f;
                    heightOffset = Mathf.Sin(bounceProgress * Mathf.PI) * 0.28f * (1f - bounceProgress);
                }
                transform.position = home + Vector3.up * heightOffset;
                if (progress >= 1f)
                {
                    spawnDropping = false;
                    transform.position = home;
                }
            }
            else if (action == ActionState.Jump)
            {
                float progress = Mathf.Clamp01((Time.time - jumpStartedAt) / jumpDuration);
                float heightOffset;
                if (progress < 0.38f)
                {
                    float rise = progress / 0.38f;
                    float easedRise = rise * rise * (3f - 2f * rise);
                    heightOffset = JumpHeight * easedRise;
                }
                else if (progress < 0.82f)
                {
                    float fall = (progress - 0.38f) / 0.44f;
                    heightOffset = JumpHeight * (1f - fall * fall);
                }
                else
                {
                    float landing = (progress - 0.82f) / 0.18f;
                    heightOffset = Mathf.Sin(landing * Mathf.PI) * 0.16f * (1f - landing);
                }
                transform.position = home + Vector3.up * Mathf.Max(0f, heightOffset);
            }
            else if (action == ActionState.Walk)
            {
                float progress = Mathf.Clamp01((Time.time - walkStartedAt) / walkDuration);
                float smoothProgress = progress * progress * (3f - 2f * progress);
                float angle = smoothProgress * Mathf.PI * 2f;
                Vector3 circuitOffset = new(
                    Mathf.Sin(angle) * 4.6f,
                    0f,
                    Mathf.Sin(smoothProgress * Mathf.PI) * 3.6f
                );
                if (onPodium)
                {
                    circuitOffset.x *= 0.095f;
                    circuitOffset.z *= 0.12f;
                }
                else if (home.y == ClubSceneBuilder.MezzanineHeight)
                {
                    circuitOffset.x *= 0.075f;
                    circuitOffset.z *= 0.18f;
                }
                transform.position = walkOrigin + circuitOffset;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, PerformancePosition, 1f - Mathf.Exp(-5f * Time.deltaTime));
            }
        }

        private void ApplyAlpha()
        {
            if (characterRenderer != null)
            {
                Color color = characterRenderer.color;
                color.a = visualAlpha;
                characterRenderer.color = color;
            }
            if (avatarRenderer != null)
            {
                Color color = avatarRenderer.color;
                color.a = visualAlpha;
                avatarRenderer.color = color;
            }
            if (wingsRenderer != null)
            {
                Color color = wingsRenderer.color;
                color.a = visualAlpha;
                wingsRenderer.color = color;
            }
            if (bannerRenderer != null)
            {
                Color color = bannerRenderer.color;
                color.a = visualAlpha;
                bannerRenderer.color = color;
            }
            SetTextAlpha(nameLabel, visualAlpha);
            SetTextAlpha(titleLabel, visualAlpha);
            SetTextAlpha(titleShadowLabel, visualAlpha * 0.92f);
        }

        private static void SetTextAlpha(TextMesh label, float alpha)
        {
            if (label == null) return;
            Color color = label.color;
            color.a = alpha;
            label.color = color;
        }

        private float VisibleCharacterHeight(float scale)
        {
            if (characterRenderer.sprite == null) return 0f;
            return characterRenderer.sprite.bounds.size.y * scale * (1f - BottomPaddingFraction());
        }

        private float BottomPaddingFraction()
        {
            return characterName switch
            {
                "a" => 0.004f,
                "b" => 0.036f,
                "c" => 0f,
                "d" => 0f,
                "e" => 0.029f,
                "g" => 0f,
                "h" => 0.188f,
                "hanhan_video_dance" => 0.151f,
                "j" => 0.071f,
                "k" => 0.035f,
                "mushroom_dance_01" => 0.038f,
                "mushroom_dance_15" => 0.046f,
                "mushroom_magic_02" => 0.021f,
                _ => 0f
            };
        }

        private enum ActionState { Idle, Dance, Jump, Walk, Celebrate, VipDance }
    }
}
