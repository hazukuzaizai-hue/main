using System.Collections.Generic;
using UnityEngine;

namespace TikTokLiveGame
{
    public sealed class OrbitStage : MonoBehaviour
    {
        private const int EntryGoal = 1000;
        private const int DropGoal = 350;
        private const float DefaultShowSeconds = 50f;
        private const float BreakSeconds = 20f;
        private const float TravelSeconds = 2.8f;

        private enum Phase { Bar, Countdown, Entering, Orbit, Returning, Break }
        private static readonly Color Cyan = new(0.12f, 0.9f, 1f);
        private static readonly Color Violet = new(0.72f, 0.22f, 1f);
        private static readonly Vector3 GateCenter = new(0f, 2.7f, -4.5f);
        private static readonly Vector3 GateApproach = new(0f, 4.5f, 9.5f);
        private readonly List<Renderer> hiddenBar = new();
        private Camera view;
        private PlayerManager players;
        private DjAudioReactive audio;
        private GameObject world;
        private GameObject tunnel;
        private OrbitFlightVisuals flight;
        private float flightStarted;
        private float flightDistance;
        private Phase phase;
        private int hearts;
        private float phaseStarted;
        private float showSeconds;
        private float dropUntil;
        private bool dropPlayed;
        private bool chroma;
        private Vector3 barPosition;
        private Quaternion barRotation;
        private float barFov;
        private GUIStyle heading;
        private GUIStyle subtitle;

        public bool OwnsCamera => phase is Phase.Countdown or Phase.Entering or Phase.Orbit or Phase.Returning;
        private float Elapsed => Time.unscaledTime - phaseStarted;

        public void Initialize(Camera camera, PlayerManager playerManager)
        {
            view = camera;
            players = playerManager;
            audio = GetComponent<DjAudioReactive>();
        }

        internal void OpenFromMilestone(float durationSeconds)
        {
            if (!chroma && (phase is Phase.Bar or Phase.Break)) StartShow(durationSeconds);
        }

        public void AddHearts(int amount, bool autoOpen = true)
        {
            if (chroma || amount <= 0) return;
            if (phase == Phase.Bar && autoOpen)
            {
                hearts += Mathf.Min(amount, EntryGoal - hearts);
                if (hearts == EntryGoal) StartShow();
            }
            else if (phase == Phase.Orbit && !dropPlayed)
            {
                hearts += Mathf.Min(amount, DropGoal - hearts);
                if (hearts == DropGoal)
                {
                    dropPlayed = true;
                    dropUntil = Time.unscaledTime + 6f;
                }
            }
        }

        public void TogglePreview()
        {
            if (chroma) return;
            if (phase is Phase.Bar or Phase.Break) StartShow();
            else if (phase == Phase.Orbit) ChangePhase(Phase.Returning);
            else ResetShow();
        }

        public void SetChroma(bool enabled)
        {
            chroma = enabled;
            if (enabled) ResetShow();
        }

        public void ResetShow()
        {
            if (OwnsCamera) RestoreCamera();
            SetOrbitVisible(false);
            if (tunnel != null) tunnel.SetActive(false);
            hearts = 0;
            dropUntil = 0f;
            dropPlayed = false;
            ChangePhase(Phase.Bar);
        }

        private void StartShow(float durationSeconds = DefaultShowSeconds)
        {
            if (view == null) return;
            if (world == null) Build();
            showSeconds = durationSeconds > 0f ? durationSeconds : DefaultShowSeconds;
            barPosition = view.transform.position;
            barRotation = view.transform.rotation;
            barFov = view.fieldOfView;
            ChangePhase(Phase.Countdown);
        }

        private void ChangePhase(Phase next)
        {
            phase = next;
            phaseStarted = Time.unscaledTime;
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Countdown when Elapsed >= 3f:
                    ChangePhase(Phase.Entering);
                    break;
                case Phase.Entering when Elapsed >= TravelSeconds:
                    hearts = 0;
                    dropPlayed = false;
                    dropUntil = 0f;
                    ChangePhase(Phase.Orbit);
                    break;
                case Phase.Orbit when Elapsed >= showSeconds:
                    ChangePhase(Phase.Returning);
                    break;
                case Phase.Returning when Elapsed >= TravelSeconds:
                    ResetShow();
                    ChangePhase(Phase.Break);
                    break;
                case Phase.Break when Elapsed >= BreakSeconds:
                    ChangePhase(Phase.Bar);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (!OwnsCamera) return;
            if (phase == Phase.Countdown)
            {
                float approach = Mathf.SmoothStep(0f, 1f, Elapsed / 3f);
                view.transform.position = Vector3.Lerp(barPosition, GateApproach, approach);
                view.transform.rotation = Quaternion.Slerp(barRotation,
                    Quaternion.LookRotation(GateCenter - view.transform.position), approach);
                view.fieldOfView = Mathf.Lerp(barFov, 47f, approach);
                tunnel.SetActive(true);
                tunnel.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 1f, approach);
                return;
            }

            if (phase == Phase.Orbit)
            {
                SetOrbitVisible(true);
                tunnel.SetActive(false);
                SetOrbitCamera(1f);
            }
            else
            {
                bool entering = phase == Phase.Entering;
                float progress = Mathf.Clamp01(Elapsed / TravelSeconds);
                bool showOrbit = entering ? progress >= 0.5f : progress < 0.5f;
                SetOrbitVisible(showOrbit);
                tunnel.SetActive(!showOrbit);
                tunnel.transform.localScale = Vector3.one;
                if (showOrbit) SetOrbitCamera(Mathf.SmoothStep(0f, 1f, Mathf.Abs(progress * 2f - 1f)));
                else if (entering)
                {
                    float travel = Mathf.SmoothStep(0f, 1f, progress * 2f);
                    view.transform.position = Vector3.Lerp(GateApproach, GateCenter + Vector3.back * 5.5f, travel);
                    view.transform.rotation = Quaternion.Slerp(
                        Quaternion.LookRotation(GateCenter - GateApproach),
                        Quaternion.LookRotation(Vector3.back), travel);
                    view.fieldOfView = Mathf.Lerp(47f, 66f, travel);
                }
                else
                {
                    float travel = Mathf.SmoothStep(0f, 1f, progress * 2f - 1f);
                    tunnel.transform.localScale = Vector3.one * (1f - travel * 0.98f);
                    view.transform.position = Vector3.Lerp(GateApproach, barPosition, travel);
                    view.transform.rotation = Quaternion.Slerp(Quaternion.LookRotation(GateCenter - GateApproach), barRotation, travel);
                    view.fieldOfView = Mathf.Lerp(47f, barFov, travel);
                }
            }

            if (world.activeSelf)
            {
                float time = Time.unscaledTime - flightStarted;
                float bass = audio != null ? Mathf.Clamp01(audio.Bass * 2.5f) : 0f;
                float boost = Mathf.Clamp01((dropUntil - Time.unscaledTime) / 2f);
                flightDistance += Time.unscaledDeltaTime * (13f + bass * 6f + boost * 19f);
                flight.Animate(view.transform.position, time, flightDistance, bass, boost);
                players.FlyFormation(view, time, boost);
            }
        }

        private void SetOrbitCamera(float reveal)
        {
            float time = Time.unscaledTime - flightStarted;
            float sweep = Mathf.Sin(time * 0.78f);
            float climb = Mathf.Sin(time * 0.59f);
            float cycle = Mathf.Repeat(time + 10f, 12f);
            float roll = Mathf.SmoothStep(0f, 360f, Mathf.InverseLerp(1.8f, 9f, cycle));
            float bank = Mathf.Sin(time * 0.95f) * 9f;
            view.transform.position = new Vector3(sweep * 2.55f, climb * 1.8f, -flightDistance);
            view.transform.rotation = Quaternion.Euler(climb * 6f, 180f + sweep * 10f, 0f)
                * Quaternion.AngleAxis(roll + bank, Vector3.forward);
            view.fieldOfView = Mathf.Lerp(64f, 51f + Mathf.Sin(time * 0.78f + 0.65f) * 3f, reveal);
        }

        private void RestoreCamera()
        {
            if (view == null) return;
            view.transform.SetPositionAndRotation(barPosition, barRotation);
            view.fieldOfView = barFov;
        }

        private void SetOrbitVisible(bool visible)
        {
            if (world == null || world.activeSelf == visible) return;
            if (visible)
            {
                flightStarted = Time.unscaledTime;
                flightDistance = 0f;
                foreach (string name in new[] { "ClubRoot", "AmPhuBackdrop_Room" })
                {
                    GameObject root = GameObject.Find(name);
                    if (root == null) continue;
                    foreach (Renderer item in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (item.forceRenderingOff) continue;
                        item.forceRenderingOff = true;
                        hiddenBar.Add(item);
                    }
                }
            }
            else
            {
                foreach (Renderer item in hiddenBar)
                    if (item != null) item.forceRenderingOff = false;
                hiddenBar.Clear();
                players.EndFlight();
            }
            world.SetActive(visible);
        }

        private void Build()
        {
            world = new GameObject("Orbit Stage");
            world.transform.SetParent(transform, false);
            flight = new OrbitFlightVisuals(world.transform, Cyan, Violet);

            tunnel = new GameObject("Neon Portal");
            tunnel.transform.SetParent(transform, false);
            tunnel.transform.position = GateCenter;
            for (int i = 0; i < 11; i++)
            {
                Color color = Color.Lerp(Cyan, Violet, i / 10f);
                float radius = 2.5f - i * 0.077f;
                Vector3 depth = Vector3.back * (i * 0.85f);
                Color glow = color;
                glow.a = 0.18f;
                Loop(tunnel.transform, "Portal glow", depth, 3, radius, 0.14f, glow);
                Loop(tunnel.transform, "Portal frame", depth, 3, radius, 0.043f, Color.Lerp(color, Color.white, 0.35f));
            }
            world.SetActive(false);
            tunnel.SetActive(false);
        }

        private static LineRenderer Loop(Transform parent, string name, Vector3 position, int sides, float radius,
            float width, Color color)
        {
            GameObject item = new(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.sharedMaterial = GameMaterials.BackgroundSprite();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = sides;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float x = radius * Mathf.Sin(angle);
                float y = radius * Mathf.Cos(angle);
                line.SetPosition(i, new Vector3(x, y, 0f));
            }
            return line;
        }

        public void DrawHud(float width, float height, bool fullHud, GUIStyle panel, float top = 16f)
        {
            if (chroma || phase is Phase.Bar or Phase.Break) return;
            if (phase is Phase.Entering or Phase.Returning)
            {
                float fade = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(Elapsed - TravelSeconds * 0.5f) / 0.43f);
                DrawRect(new Rect(0f, 0f, width, height), new Color(0.035f, 0.024f, 0.12f, fade));
            }
            heading ??= new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            subtitle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = new Color(0.78f, 0.86f, 1f) } };
            string title = phase switch
            {
                Phase.Countdown => $"MỞ CỔNG · {Mathf.Max(1, Mathf.CeilToInt(3f - Elapsed))}",
                Phase.Entering => "ĐANG XUYÊN CỔNG",
                Phase.Orbit => $"XUYÊN KHÔNG · {Mathf.Max(0, Mathf.CeilToInt(showSeconds - Elapsed))} GIÂY",
                Phase.Returning => "TRỞ VỀ BAR",
                _ => string.Empty
            };
            string detail = phase switch
            {
                Phase.Orbit when Time.unscaledTime < dropUntil => "TĂNG TỐC XUYÊN KHÔNG",
                Phase.Orbit when dropPlayed => "Đã kích hoạt tăng tốc",
                Phase.Orbit => $"Thả tim tăng tốc · {hearts:N0} / {DropGoal:N0} tim",
                _ => string.Empty
            };
            float cardWidth = Mathf.Min(330f, width - 28f);
            float x = (width - cardWidth) * 0.5f;
            float y = fullHud ? 242f : top;
            GUI.Box(new Rect(x, y, cardWidth, 74f), GUIContent.none, panel);
            GUI.Label(new Rect(x + 13f, y + 5f, cardWidth - 26f, 29f), title, heading);
            GUI.Label(new Rect(x + 13f, y + 32f, cardWidth - 26f, 24f), detail, subtitle);
            if (phase == Phase.Orbit)
            {
                float progress = hearts / (float)DropGoal;
                DrawRect(new Rect(x + 13f, y + 61f, cardWidth - 26f, 3f), new Color(0.18f, 0.2f, 0.33f));
                DrawRect(new Rect(x + 13f, y + 61f, (cardWidth - 26f) * progress, 3f), Violet);
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color original = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = original;
        }
    }
}
