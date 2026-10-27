using System;
using Karmaan.Cert;
using Karmaan.Core;
using UnityEngine;
using UIImage = UnityEngine.UI.Image;
using UIText = UnityEngine.UI.Text;

namespace Karmaan.Game
{
    /// <summary>
    /// All screens, built at runtime. No prefabs, no scene YAML.
    ///
    /// Design rules taken from the PS, not invented here:
    ///  - touch targets never below 2 cm (≈150 px at our 1080p reference)
    ///  - colour carries meaning: green = go, red = stop, amber = wait
    ///  - every screen readable by voice-first users: icons, colour and one short line
    ///  - no menu tree. Language, two lesson cards, done.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        private LocalizationService _loc;

        private Canvas _canvas;
        private RectTransform _menuRoot;
        private RectTransform _hudRoot;
        private RectTransform _resultRoot;
        private RectTransform _arRoot;
        private RectTransform _certRoot;

        private UIText _lessonTitle;
        private UIText _counter;
        private UIText _prompt;
        private UIText _hint;
        private UIText _arStatus;
        private UIImage _reticle;
        private UIImage _flash;
        private readonly System.Collections.Generic.List<UIImage> _stepDots =
            new System.Collections.Generic.List<UIImage>();

        private float _flashAlpha;

        public bool IsOverUi
        {
            get { return UiButton.IsPointerOverButton(InputBridge.PointerPosition); }
        }

        public static GameUI Create(LocalizationService localization)
        {
            var go = new GameObject("GameUI");
            var ui = go.AddComponent<GameUI>();
            ui._loc = localization;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            _canvas = UiKit.CreateCanvas("Canvas", 10);
            _canvas.transform.SetParent(transform, false);

            _menuRoot = UiKit.Stretch(UiKit.Node(_canvas.transform, "MenuRoot"));
            _hudRoot = UiKit.Stretch(UiKit.Node(_canvas.transform, "HudRoot"));
            _resultRoot = UiKit.Stretch(UiKit.Node(_canvas.transform, "ResultRoot"));
            _arRoot = UiKit.Stretch(UiKit.Node(_canvas.transform, "ArRoot"));
            // Created last so it draws over every other screen.
            _certRoot = UiKit.Stretch(UiKit.Node(_canvas.transform, "CertRoot"));

            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
            _certRoot.gameObject.SetActive(false);

            BuildHud();
        }

        // ------------------------------------------------------------------ menu

        public void ShowMenu(Action<string> onLesson, Action<AppLanguage> onLanguage,
            Action onToggleAr, bool arSupported, bool arEnabled)
        {
            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
            HideZoom();
            _menuRoot.gameObject.SetActive(true);
            UiKit.Clear(_menuRoot);

            var bg = UiKit.Panel(_menuRoot, "Bg", new Color(0.04f, 0.05f, 0.07f, 0.97f));
            UiKit.Stretch((RectTransform)bg.transform);

            var card = UiKit.Panel(_menuRoot, "Card", UiKit.Card);
            UiKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1180f, 760f));

            var title = UiKit.Label(card.transform, "Title", _loc.Get("ui.app.name"), 76, UiKit.Ink);
            UiKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(1000f, 90f));

            var subtitle = UiKit.Label(card.transform, "Subtitle",
                "सुरक्षित सीखो · सुरक्षित रहो", 34, UiKit.Muted);
            UiKit.Place((RectTransform)subtitle.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(1000f, 50f));

            var langLabel = UiKit.Label(card.transform, "LangLabel", _loc.Get("ui.lang.title"), 30, UiKit.Muted);
            UiKit.Place((RectTransform)langLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -222f), new Vector2(600f, 40f));

            BuildLanguageButton(card.transform, "BtnHindi", "ui.lang.hindi", AppLanguage.Hindi, -180f, onLanguage);
            BuildLanguageButton(card.transform, "BtnSantali", "ui.lang.santali", AppLanguage.Santali, 180f, onLanguage);

            if (_loc.LocaleIsPending)
            {
                var chip = UiKit.Label(card.transform, "PendingChip",
                    "⚠ " + _loc.Get("ui.lang.santali") + " — translation pending", 24, UiKit.Warn);
                UiKit.Place((RectTransform)chip.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -300f), new Vector2(900f, 34f));
            }

            var fire = BuildLessonCard(card.transform, "LessonFire", "lesson.fire.title",
                new Color(0.85f, 0.30f, 0.10f), -260f, "fire", onLesson);
            fire.background.color = new Color(0.26f, 0.13f, 0.08f);

            var gas = BuildLessonCard(card.transform, "LessonGas", "lesson.gas.title",
                new Color(0.15f, 0.55f, 0.85f), 260f, "gas", onLesson);
            gas.background.color = new Color(0.08f, 0.17f, 0.26f);

            BuildArToggle(card.transform, onToggleAr, arSupported, arEnabled);

            var footer = UiKit.Label(card.transform, "Footer",
                "SIH 26041 · Team JKSS · " + _loc.LocaleId, 22, new Color(0.45f, 0.49f, 0.55f));
            UiKit.Place((RectTransform)footer.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), new Vector2(900f, 30f));
        }

        private void BuildLanguageButton(Transform parent, string name, string key,
            AppLanguage language, float x, Action<AppLanguage> onLanguage)
        {
            var active = _loc.Language == language;
            var button = UiKit.Button(parent, name, _loc.Get(key), new Vector2(0.5f, 1f),
                new Vector2(x, -262f), new Vector2(320f, 92f), () => onLanguage(language));

            button.background.color = active ? UiKit.Go : new Color(0.17f, 0.20f, 0.25f);
            button.idleColor = button.background.color;
            button.hoverColor = active ? new Color(0.14f, 0.74f, 0.42f) : new Color(0.25f, 0.30f, 0.38f);
        }

        private UiButton BuildLessonCard(Transform parent, string name, string titleKey,
            Color accent, float x, string lessonId, Action<string> onLesson)
        {
            var button = UiKit.Button(parent, name, _loc.Get(titleKey), new Vector2(0.5f, 0.5f),
                new Vector2(x, -40f), new Vector2(480f, 190f), () => onLesson(lessonId));

            button.label.fontSize = 44;
            button.hoverColor = new Color(accent.r * 0.55f, accent.g * 0.55f, accent.b * 0.55f);

            var bar = UiKit.Panel(button.transform, "Accent", accent);
            UiKit.Place((RectTransform)bar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(12f, 190f));

            return button;
        }

        /// <summary>
        /// AR on/off. Deliberately a single visible switch rather than a settings screen: a worker
        /// at a training centre needs to know which mode they are in before they start, not to
        /// discover it halfway through a fire.
        /// </summary>
        private void BuildArToggle(Transform parent, Action onToggle, bool supported, bool enabled)
        {
            var text = supported
                ? _loc.Get(enabled ? "ui.ar.on" : "ui.ar.off")
                : _loc.Get("ui.ar.unavailable");

            var button = UiKit.Button(parent, "BtnAr", text, new Vector2(0f, 0f),
                new Vector2(240f, 96f), new Vector2(300f, 92f), supported ? onToggle : null,
                _loc.Script == ScriptMode.OlChiki);

            button.label.fontSize = 30;

            Color colour;
            if (!supported) colour = new Color(0.16f, 0.17f, 0.19f);
            else if (enabled) colour = new Color(0.10f, 0.50f, 0.33f);
            else colour = new Color(0.22f, 0.24f, 0.28f);

            button.background.color = colour;
            button.idleColor = colour;
            button.hoverColor = supported
                ? new Color(colour.r * 1.4f, colour.g * 1.4f, colour.b * 1.4f)
                : colour;
        }

        // ------------------------------------------------------------------ ar scan

        /// <summary>
        /// The screen between pressing a lesson and the room existing: point the phone at the
        /// floor. Shown before the runner starts, so nothing is timed while the worker is still
        /// finding a patch of ground.
        /// </summary>
        public void ShowArScan(string title, string hint, Action onCancel)
        {
            _menuRoot.gameObject.SetActive(false);
            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(true);
            HideZoom();
            UiKit.Clear(_arRoot);

            var bg = UiKit.Panel(_arRoot, "Bg", new Color(0.02f, 0.03f, 0.05f, 0.92f));
            UiKit.Stretch((RectTransform)bg.transform);

            var titleLabel = UiKit.Label(_arRoot, "ArTitle", title, 56, UiKit.Ink);
            UiKit.Place((RectTransform)titleLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -140f), new Vector2(1200f, 90f));

            // Filled on the first Update by SetArScanStatus. Starting empty rather than with the
            // static hint keeps the two lines from saying the same thing on frame one.
            _arStatus = UiKit.Label(_arRoot, "ArStatus", string.Empty, 40, UiKit.Warn);
            UiKit.Place((RectTransform)_arStatus.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(1280f, 160f));

            var hintLabel = UiKit.Label(_arRoot, "ArHint", hint, 30, UiKit.Muted);
            UiKit.Place((RectTransform)hintLabel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f), new Vector2(1200f, 120f));

            var cancel = UiKit.Button(_arRoot, "BtnArCancel", _loc.Get("ui.menu"), new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(420f, 96f), onCancel, _loc.Script == ScriptMode.OlChiki);
            cancel.label.fontSize = 34;
            cancel.background.color = new Color(0.24f, 0.26f, 0.30f);
            cancel.idleColor = cancel.background.color;
        }

        public void SetArScanStatus(string text)
        {
            if (_arStatus == null || _arStatus.text == text) return;
            _arStatus.text = text;
        }

        public void HideArScan()
        {
            if (_arRoot != null) _arRoot.gameObject.SetActive(false);
            _arStatus = null;
        }

        // ------------------------------------------------------------------ hud

        private void BuildHud()
        {
            _lessonTitle = UiKit.Label(_hudRoot, "LessonTitle", "", 34, UiKit.Ink, TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)_lessonTitle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -28f), new Vector2(800f, 46f));

            _counter = UiKit.Label(_hudRoot, "Counter", "", 32, UiKit.Muted, TextAnchor.MiddleRight);
            UiKit.Place((RectTransform)_counter.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -28f), new Vector2(400f, 46f));

            var promptCard = UiKit.Panel(_hudRoot, "PromptCard", new Color(0.05f, 0.07f, 0.10f, 0.88f));
            UiKit.Place((RectTransform)promptCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(1360f, 160f));

            _prompt = UiKit.Label(promptCard.transform, "Prompt", "", 52, UiKit.Ink);
            UiKit.Stretch((RectTransform)_prompt.transform, 30f, 16f, 30f, 16f);

            _hint = UiKit.Label(_hudRoot, "Hint", "", 34, UiKit.Warn);
            UiKit.Place((RectTransform)_hint.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -276f), new Vector2(1360f, 50f));

            _reticle = UiKit.Image(_hudRoot, "Reticle", new Color(1f, 1f, 1f, 0.85f), UiKit.Circle,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

            _flash = UiKit.Image(_hudRoot, "Flash", new Color(0.9f, 0.1f, 0.1f, 0f), null,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UiKit.Stretch((RectTransform)_flash.transform);
        }

        public void ShowHud(string lessonTitleKey)
        {
            _menuRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
            HideZoom();
            _hudRoot.gameObject.SetActive(true);

            _lessonTitle.text = _loc.Get(lessonTitleKey);
            _hint.text = string.Empty;
            _flashAlpha = 0f;
        }

        public void SetProgress(int stepIndex, int stepTotal)
        {
            _counter.text = (stepIndex + 1) + " / " + stepTotal;

            if (_stepDots.Count != stepTotal)
            {
                for (var i = 0; i < _stepDots.Count; i++)
                {
                    if (_stepDots[i] != null) Destroy(_stepDots[i].gameObject);
                }
                _stepDots.Clear();

                var totalWidth = stepTotal * 34f;
                for (var i = 0; i < stepTotal; i++)
                {
                    var dot = UiKit.Image(_hudRoot, "Dot" + i, new Color(0.3f, 0.33f, 0.38f), UiKit.Circle,
                        new Vector2(0.5f, 0f),
                        new Vector2(-totalWidth * 0.5f + 17f + i * 34f, 56f), new Vector2(18f, 18f));
                    _stepDots.Add(dot);
                }
            }

            for (var i = 0; i < _stepDots.Count; i++)
            {
                _stepDots[i].color = i < stepIndex
                    ? new Color(0.25f, 0.75f, 0.45f)
                    : (i == stepIndex ? UiKit.Ink : new Color(0.3f, 0.33f, 0.38f));
            }
        }

        public void SetPrompt(string text)
        {
            _prompt.text = text ?? string.Empty;
            _hint.text = string.Empty;
        }

        public void SetHint(string text, bool bad)
        {
            var changed = _hint.text != (text ?? string.Empty);
            _hint.text = text ?? string.Empty;
            _hint.color = bad ? UiKit.Warn : UiKit.Go;

            // Only flash on a new message, otherwise a live countdown strobes the whole screen.
            if (bad && changed) _flashAlpha = 0.32f;
        }

        public void SetReticle(bool onTarget)
        {
            _reticle.color = onTarget
                ? new Color(0.35f, 1f, 0.55f, 1f)
                : new Color(1f, 1f, 1f, 0.85f);
            _reticle.rectTransform.sizeDelta = onTarget ? new Vector2(26f, 26f) : new Vector2(16f, 16f);
        }

        private void Update()
        {
            if (_flashAlpha <= 0f) return;

            _flashAlpha = Mathf.Max(0f, _flashAlpha - Time.deltaTime * 1.6f);
            _flash.color = new Color(0.9f, 0.1f, 0.1f, _flashAlpha);
        }

        // ------------------------------------------------------------------ result

        public void ShowResult(ScoreReport report, string certToken,
            Action onRetry, Action onMenu, Action onVerify, string verifyResult)
        {
            _hudRoot.gameObject.SetActive(false);
            _menuRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
            HideZoom();
            _resultRoot.gameObject.SetActive(true);
            UiKit.Clear(_resultRoot);

            var bg = UiKit.Panel(_resultRoot, "Bg", new Color(0.04f, 0.05f, 0.07f, 0.97f));
            UiKit.Stretch((RectTransform)bg.transform);

            var card = UiKit.Panel(_resultRoot, "Card", UiKit.Card);
            UiKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1400f, 900f));

            var verdict = report.passed ? _loc.Get("ui.result.pass") : _loc.Get("ui.result.fail");
            var verdictColor = report.passed ? UiKit.Go : UiKit.Stop;

            var verdictLabel = UiKit.Label(card.transform, "Verdict", verdict, 64, verdictColor);
            UiKit.Place((RectTransform)verdictLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -46f), new Vector2(1100f, 76f));

            var score = UiKit.Label(card.transform, "Score", report.score.ToString(), 150, UiKit.Ink);
            UiKit.Place((RectTransform)score.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -176f), new Vector2(600f, 160f));

            var scoreCaption = UiKit.Label(card.transform, "ScoreCaption", _loc.Get("ui.result.score"), 30, UiKit.Muted);
            UiKit.Place((RectTransform)scoreCaption.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -272f), new Vector2(600f, 36f));

            if (report.HasWeakSteps)
            {
                var weakCaption = UiKit.Label(card.transform, "WeakCaption",
                    _loc.Get("ui.result.weak"), 30, UiKit.Muted);
                UiKit.Place((RectTransform)weakCaption.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -330f), new Vector2(600f, 36f));

                var weakList = UiKit.Label(card.transform, "WeakList",
                    string.Join(" · ", report.weakSteps.ToArray()), 28, UiKit.Warn);
                UiKit.Place((RectTransform)weakList.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -372f), new Vector2(1200f, 40f));
            }

            // Certificate block. The token is real and HMAC-signed; the QR beside it is rendered
            // from the token itself, so a scanner reads back exactly what Verify() checks.
            var certBox = UiKit.Panel(card.transform, "CertBox", new Color(0.07f, 0.09f, 0.12f, 1f));
            UiKit.Place((RectTransform)certBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -424f), new Vector2(1300f, 300f));

            var certTitle = UiKit.Label(certBox.transform, "CertTitle",
                report.passed ? _loc.Get("ui.cert.title") : "—", 28, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place((RectTransform)certTitle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -16f), new Vector2(900f, 34f));

            var hasToken = report.passed && !string.IsNullOrEmpty(certToken);
            var tokenText = hasToken ? certToken : "—";
            var certBody = UiKit.Label(certBox.transform, "CertBody", tokenText, 20,
                hasToken ? UiKit.Ink : UiKit.Muted, TextAnchor.UpperLeft);
            certBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Place((RectTransform)certBody.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -56f), new Vector2(940f, 132f));

            // QR. Encoded here rather than in CertService so the token stays the single source of
            // truth: whatever we draw is exactly the string Verify() will parse.
            Sprite qrSprite = null;
            if (hasToken)
            {
                string qrError;
                qrSprite = QrSprite.ForToken(certToken, out qrError, 4, 4);
                if (qrSprite == null)
                {
                    Debug.LogError("[Cert] QR encode failed for token: " + qrError);
                }
            }

            if (qrSprite != null)
            {
                // Tap the QR to get a full-screen copy. At 268 px on a 1920-wide canvas the
                // symbol is about 1.3 cm on a phone — fine to look at, too small for a guard to
                // scan. The zoom view is what makes it actually usable at a gate.
                var qrButton = UiKit.Button(certBox.transform, "QrBtn", "", new Vector2(1f, 1f),
                    new Vector2(-146f, -138f), new Vector2(260f, 260f),
                    delegate { ShowCertificateZoom(certToken); });

                qrButton.background.sprite = qrSprite;
                qrButton.background.color = Color.white;
                qrButton.background.preserveAspect = true;
                qrButton.idleColor = Color.white;
                qrButton.hoverColor = new Color(0.82f, 0.82f, 0.82f);
                if (qrButton.label != null) qrButton.label.gameObject.SetActive(false);

                var scanCaption = UiKit.Label(certBox.transform, "QrCaption", _loc.Get("ui.cert.scan"), 22,
                    UiKit.Muted);
                UiKit.Place((RectTransform)scanCaption.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(-16f, 2f), new Vector2(300f, 26f));
            }
            else
            {
                var placeholder = UiKit.Label(certBox.transform, "QrMissing", "—", 60, UiKit.Muted);
                UiKit.Place((RectTransform)placeholder.transform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(-146f, -138f), new Vector2(260f, 260f));
            }

            if (!string.IsNullOrEmpty(verifyResult))
            {
                var verifyLabel = UiKit.Label(certBox.transform, "VerifyResult", verifyResult, 24, UiKit.Go,
                    TextAnchor.MiddleLeft);
                UiKit.Place((RectTransform)verifyLabel.transform, new Vector2(0f, 0f), new Vector2(0f, 0.5f),
                    new Vector2(360f, 48f), new Vector2(560f, 40f));
            }

            if (report.passed)
            {
                UiKit.Button(certBox.transform, "VerifyBtn", _loc.Get("ui.cert.verify"), new Vector2(0f, 0f),
                    new Vector2(180f, 48f), new Vector2(300f, 64f), onVerify);
            }

            UiKit.Button(card.transform, "RetryBtn", _loc.Get("ui.result.retry"), new Vector2(0.5f, 0f),
                new Vector2(-200f, 60f), new Vector2(340f, 96f), onRetry);

            UiKit.Button(card.transform, "MenuBtn", _loc.Get("ui.menu"), new Vector2(0.5f, 0f),
                new Vector2(200f, 60f), new Vector2(340f, 96f), onMenu);
        }

        /// <summary>
        /// Full-screen QR, opened by tapping the one on the result card.
        ///
        /// The small copy exists to show that a certificate was issued; this one exists to be
        /// scanned. At 715 px on a 1920-wide canvas it is roughly a third of the screen, which is
        /// what a guard's phone needs at arm's length. The token is printed underneath as well, so
        /// a damaged screen or a dead scanner does not make the certificate useless.
        /// </summary>
        public void ShowCertificateZoom(string token)
        {
            if (_certRoot == null || string.IsNullOrEmpty(token)) return;

            // UiButton hit-tests rectangles and ignores draw order, so leaving the result card
            // visible underneath would let a tap land on Retry as well as on Close.
            _resultRoot.gameObject.SetActive(false);
            _certRoot.gameObject.SetActive(true);
            UiKit.Clear(_certRoot);

            var bg = UiKit.Panel(_certRoot, "Bg", new Color(0.02f, 0.02f, 0.03f, 0.99f));
            UiKit.Stretch((RectTransform)bg.transform);

            string error;
            var sprite = QrSprite.ForToken(token, out error, 11, 4);
            if (sprite == null)
            {
                Debug.LogError("[Cert] Zoom QR encode failed: " + error);
                HideZoom();
                return;
            }

            var qr = UiKit.Image(_certRoot, "QrLarge", Color.white, sprite,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(715f, 715f));
            qr.preserveAspect = true;

            var tokenLabel = UiKit.Label(_certRoot, "Token", token, 20, UiKit.Muted, TextAnchor.UpperCenter);
            UiKit.Place((RectTransform)tokenLabel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(1500f, 110f));

            UiKit.Button(_certRoot, "CloseBtn", _loc.Get("ui.cert.close"), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(360f, 96f), CloseZoom);
        }

        private void CloseZoom()
        {
            HideZoom();
            if (_resultRoot != null && _resultRoot.childCount > 0) _resultRoot.gameObject.SetActive(true);
        }

        public void HideZoom()
        {
            if (_certRoot != null) _certRoot.gameObject.SetActive(false);
        }

        public void ShowQuiz(string prompt, string[] options, Action<int> onAnswer)
        {
            _hudRoot.gameObject.SetActive(false);
            _menuRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
            HideZoom();
            _resultRoot.gameObject.SetActive(true);
            UiKit.Clear(_resultRoot);

            var bg = UiKit.Panel(_resultRoot, "Bg", new Color(0.04f, 0.05f, 0.07f, 0.97f));
            UiKit.Stretch((RectTransform)bg.transform);

            var card = UiKit.Panel(_resultRoot, "Card", UiKit.Card);
            UiKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1280f, 760f));

            var question = UiKit.Label(card.transform, "Question", prompt, 44, UiKit.Ink);
            UiKit.Place((RectTransform)question.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(1140f, 140f));

            for (var i = 0; i < options.Length; i++)
            {
                var index = i;
                var y = -320f - i * 130f;

                var button = UiKit.Button(card.transform, "Answer" + i, options[i],
                    new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1080f, 108f),
                    () => onAnswer(index));

                button.label.fontSize = 34;
                button.label.alignment = TextAnchor.MiddleLeft;
                StretchLabel(button.label, 40f, 0f);
            }
        }

        private static void StretchLabel(UIText label, float left, float right)
        {
            var rect = (RectTransform)label.transform;
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        }

        public void HideAll()
        {
            _menuRoot.gameObject.SetActive(false);
            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
        }
    }
}
