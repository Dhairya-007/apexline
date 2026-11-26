using UnityEngine;
using UnityEngine.UI;
using Apexline.Race;
using Apexline.Vehicle;

namespace Apexline.UI
{
    /// <summary>
    /// The race HUD: lap, position, lap times, speed, gear, countdown and a
    /// results panel.
    ///
    /// It BUILDS ITS OWN Canvas in Awake rather than referencing a prefab. That
    /// is deliberate: a hand-wired HUD prefab is a long list of serialized
    /// references, every one of which can silently break when a scene is
    /// regenerated, and none of which can be verified without opening the
    /// editor. Building it in code means the HUD either compiles and works or
    /// does not exist, and a headless test can assert every field.
    ///
    /// Add this component to any GameObject in a scene that has a RaceManager.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        [Header("Links (found automatically if left empty)")]
        public RaceManager race;
        public CarController playerCar;

        [Header("Style")]
        public int referenceWidth = 1920;
        public int referenceHeight = 1080;

        static readonly Color Panel   = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color Ink     = new Color(0.97f, 0.97f, 0.98f, 1f);
        static readonly Color Accent  = new Color(1f, 0.78f, 0.25f, 1f);
        static readonly Color Good    = new Color(0.45f, 0.95f, 0.55f, 1f);

        Text _lap, _position, _speed, _gear, _current, _last, _best, _countdown;
        GameObject _resultsPanel;
        Text _resultsText;
        Font _font;

        // Exposed so tests can assert the HUD is actually wired, not just present.
        public bool IsBuilt => _lap != null && _speed != null && _countdown != null;

        void Awake()
        {
            if (race == null) race = FindFirstObjectByType<RaceManager>();
            ResolvePlayerCar();
            _font = LoadFont();
            BuildCanvas();
        }

        void OnEnable()
        {
            if (race != null)
            {
                race.StateChanged += OnStateChanged;
                race.RacerFinished += OnRacerFinished;
            }
        }

        void OnDisable()
        {
            if (race != null)
            {
                race.StateChanged -= OnStateChanged;
                race.RacerFinished -= OnRacerFinished;
            }
        }

        void ResolvePlayerCar()
        {
            if (playerCar != null) return;

            // The player is the car without an AI driver.
            foreach (var c in FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                if (c.GetComponent<AI.AIDriver>() == null) { playerCar = c; return; }
            }
            playerCar = FindFirstObjectByType<CarController>();
        }

        static Font LoadFont()
        {
            // Unity's built-in runtime font. Falls back to an OS font so the HUD
            // never renders as invisible text if the builtin name ever changes.
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Helvetica", 24);
            return f;
        }

        // ------------------------------------------------------------ building
        void BuildCanvas()
        {
            var canvasGo = new GameObject("RaceHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            // Scale with screen size so the HUD is legible on a phone and on a
            // 4K monitor without two sets of numbers.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(referenceWidth, referenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // top-left: lap and position
            var tl = MakePanel(canvasGo.transform, new Vector2(0f, 1f), new Vector2(24f, -24f),
                               new Vector2(260f, 110f));
            _lap      = MakeText(tl.transform, "LAP 1/3",  44, Ink,    TextAnchor.UpperLeft,
                                 new Vector2(16f, -10f), new Vector2(240f, 52f));
            _position = MakeText(tl.transform, "P1/1",     34, Accent, TextAnchor.UpperLeft,
                                 new Vector2(16f, -62f), new Vector2(240f, 42f));

            // top-right: timing
            var tr = MakePanel(canvasGo.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f),
                               new Vector2(300f, 140f));
            _current = MakeText(tr.transform, "0:00.000", 38, Ink,  TextAnchor.UpperRight,
                                new Vector2(-16f, -10f), new Vector2(270f, 46f));
            _last    = MakeText(tr.transform, "last  --:--.---", 22, Ink, TextAnchor.UpperRight,
                                new Vector2(-16f, -60f), new Vector2(270f, 30f));
            _best    = MakeText(tr.transform, "best  --:--.---", 22, Good, TextAnchor.UpperRight,
                                new Vector2(-16f, -92f), new Vector2(270f, 30f));

            // bottom-right: speed and gear
            var br = MakePanel(canvasGo.transform, new Vector2(1f, 0f), new Vector2(-24f, 24f),
                               new Vector2(280f, 130f));
            _speed = MakeText(br.transform, "0", 76, Ink, TextAnchor.LowerRight,
                              new Vector2(-70f, 14f), new Vector2(190f, 84f));
            MakeText(br.transform, "km/h", 22, Ink, TextAnchor.LowerRight,
                     new Vector2(-16f, 22f), new Vector2(56f, 28f));
            _gear  = MakeText(br.transform, "1", 34, Accent, TextAnchor.UpperRight,
                              new Vector2(-16f, -12f), new Vector2(60f, 40f));

            // centre: countdown
            _countdown = MakeText(canvasGo.transform, "", 180, Accent, TextAnchor.MiddleCenter,
                                  Vector2.zero, new Vector2(700f, 240f));
            SetAnchor(_countdown.rectTransform, new Vector2(0.5f, 0.5f));
            _countdown.gameObject.SetActive(false);

            // centre: results
            _resultsPanel = MakePanel(canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                      new Vector2(620f, 420f));
            _resultsText = MakeText(_resultsPanel.transform, "", 30, Ink, TextAnchor.UpperCenter,
                                    new Vector2(0f, -24f), new Vector2(580f, 370f));
            SetAnchor(_resultsText.rectTransform, new Vector2(0.5f, 1f));
            _resultsPanel.SetActive(false);
        }

        static void SetAnchor(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
        }

        GameObject MakePanel(Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Panel;
            var rt = img.rectTransform;
            SetAnchor(rt, anchor);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return go;
        }

        Text MakeText(Transform parent, string value, int size, Color colour,
                      TextAnchor align, Vector2 offset, Vector2 box)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.text = value;
            t.fontSize = size;
            t.color = colour;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var rt = t.rectTransform;
            // Anchor to the same corner as the alignment, so the text grows away
            // from its corner instead of drifting off the panel.
            Vector2 a = align switch
            {
                TextAnchor.UpperLeft   => new Vector2(0f, 1f),
                TextAnchor.UpperRight  => new Vector2(1f, 1f),
                TextAnchor.LowerRight  => new Vector2(1f, 0f),
                TextAnchor.LowerLeft   => new Vector2(0f, 0f),
                TextAnchor.UpperCenter => new Vector2(0.5f, 1f),
                _                      => new Vector2(0.5f, 0.5f),
            };
            SetAnchor(rt, a);
            rt.sizeDelta = box;
            rt.anchoredPosition = offset;
            return t;
        }

        // ------------------------------------------------------------- updating
        void Update()
        {
            if (playerCar != null)
            {
                _speed.text = Mathf.RoundToInt(playerCar.SpeedKph).ToString();
                _gear.text = playerCar.Gear.ToString();
            }

            if (race == null) return;

            if (race.Current == RaceManager.State.Countdown && race.CountdownRemaining > 0)
            {
                _countdown.gameObject.SetActive(true);
                _countdown.text = race.CountdownRemaining.ToString();
            }
            else if (_countdown.gameObject.activeSelf && race.Current != RaceManager.State.Countdown)
            {
                _countdown.gameObject.SetActive(false);
            }

            RaceManager.Racer me = race.PlayerRacer;
            if (me == null) return;

            _lap.text = $"LAP {Mathf.Min(me.lap + 1, race.totalLaps)}/{race.totalLaps}";
            _position.text = $"P{me.position}/{race.Standings.Count}";

            float lapElapsed = Mathf.Max(0f, race.RaceTime - me.lapStartTime);
            _current.text = race.Current == RaceManager.State.Racing
                ? RaceManager.FormatTime(lapElapsed)
                : RaceManager.FormatTime(0f);
            _last.text = $"last  {RaceManager.FormatTime(me.lastLapTime)}";
            _best.text = $"best  {RaceManager.FormatTime(me.bestLapTime)}";
        }

        void OnStateChanged(RaceManager.State s)
        {
            if (s == RaceManager.State.Racing)
            {
                _countdown.text = "GO";
                _countdown.gameObject.SetActive(true);
                CancelInvoke(nameof(HideCountdown));
                Invoke(nameof(HideCountdown), 1.2f);
            }
            else if (s == RaceManager.State.Finished)
            {
                ShowResults();
            }
        }

        void HideCountdown()
        {
            if (_countdown != null) _countdown.gameObject.SetActive(false);
        }

        void OnRacerFinished(RaceManager.Racer r)
        {
            if (r != null && r.isPlayer) ShowResults();
        }

        void ShowResults()
        {
            if (race == null || _resultsPanel == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>RESULTS</b>\n");
            int i = 1;
            foreach (var r in race.Standings)
            {
                string time = r.finished ? RaceManager.FormatTime(r.finishTime) : "DNF";
                string best = RaceManager.FormatTime(r.bestLapTime);
                string mark = r.isPlayer ? ">" : " ";
                sb.AppendLine($"{mark} P{i}  {r.displayName,-10}  {time}   best {best}");
                i++;
            }

            _resultsText.supportRichText = true;
            _resultsText.text = sb.ToString();
            _resultsPanel.SetActive(true);
        }
    }
}
