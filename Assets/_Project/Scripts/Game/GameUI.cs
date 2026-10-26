using System;
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

            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);

            BuildHud();
        }

        // ------------------------------------------------------------------ menu

        public void ShowMenu(Action<string> onLesson, Action<AppLanguage> onLanguage,
            Action onToggleAr, bool arSupported, bool arEnabled)
        {
            _hudRoot.gameObject.SetActive(false);
            _resultRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
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
            _resultRoot.gameObject.SetActive(true);
            UiKit.Clear(_resultRoot);

            var bg = UiKit.Panel(_resultRoot, "Bg", new Color(0.04f, 0.05f, 0.07f, 0.97f));
            UiKit.Stretch((RectTransform)bg.transform);

            var card = UiKit.Panel(_resultRoot, "Card", UiKit.Card);
            UiKit.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1240f, 820f));

            var verdict = report.passed ? _loc.Get("ui.result.pass") : _loc.Get("ui.result.fail");
            var verdictColor = report.passed ? UiKit.Go : UiKit.Stop;

            var verdictLabel = UiKit.Label(card.transform, "Verdict", verdict, 64, verdictColor);
            UiKit.Place((RectTransform)verdictLabel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -50f), new Vector2(1000f, 80f));

            var score = UiKit.Label(card.transform, "Score", report.score.ToString(), 150, UiKit.Ink);
            UiKit.Place((RectTransform)score.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -190f), new Vector2(600f, 170f));

            var scoreCaption = UiKit.Label(card.transform, "ScoreCaption", _loc.Get("ui.result.score"), 30, UiKit.Muted);
            UiKit.Place((RectTransform)scoreCaption.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -292f), new Vector2(600f, 36f));

            if (report.HasWeakSteps)
            {
                var weakCaption = UiKit.Label(card.transform, "WeakCaption",
                    _loc.Get("ui.result.weak"), 30, UiKit.Muted);
                UiKit.Place((RectTransform)weakCaption.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -352f), new Vector2(600f, 36f));

                var weakList = UiKit.Label(card.transform, "WeakList",
                    string.Join(" · ", report.weakSteps.ToArray()), 28, UiKit.Warn);
                UiKit.Place((RectTransform)weakList.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -396f), new Vector2(1100f, 40f));
            }

            // Certificate block. The token is real and signed; the QR matrix needs ZXing (Phase 5).
            var certBox = UiKit.Panel(card.transform, "CertBox", new Color(0.07f, 0.09f, 0.12f, 1f));
            UiKit.Place((RectTransform)certBox.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -500f), new Vector2(1120f, 140f));

            var certTitle = UiKit.Label(certBox.transform, "CertTitle",
                report.passed ? _loc.Get("ui.cert.title") : "—", 28, UiKit.Muted, TextAnchor.UpperLeft);
            UiKit.Place((RectTransform)certTitle.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -14f), new Vector2(600f, 34f));

            var tokenText = string.IsNullOrEmpty(certToken) ? "—" : Shorten(certToken, 64);
            var certBody = UiKit.Label(certBox.transform, "CertBody", tokenText, 22,
                report.passed ? UiKit.Ink : UiKit.Muted, TextAnchor.MiddleLeft);
            UiKit.Place((RectTransform)certBody.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 20f), new Vector2(820f, 70f));

            if (!string.IsNullOrEmpty(verifyResult))
            {
                var verifyLabel = UiKit.Label(certBox.transform, "VerifyResult", verifyResult, 24, UiKit.Go,
                    TextAnchor.LowerRight);
                UiKit.Place((RectTransform)verifyLabel.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-20f, -12f), new Vector2(280f, 40f));
            }

            if (report.passed)
            {
                UiKit.Button(certBox.transform, "VerifyBtn", _loc.Get("ui.cert.verify"), new Vector2(1f, 0f),
                    new Vector2(-24f, 30f), new Vector2(260f, 64f), onVerify);
            }

            UiKit.Button(card.transform, "RetryBtn", _loc.Get("ui.result.retry"), new Vector2(0.5f, 0f),
                new Vector2(-200f, 60f), new Vector2(340f, 96f), onRetry);

            UiKit.Button(card.transform, "MenuBtn", _loc.Get("ui.menu"), new Vector2(0.5f, 0f),
                new Vector2(200f, 60f), new Vector2(340f, 96f), onMenu);
        }

        public void ShowQuiz(string prompt, string[] options, Action<int> onAnswer)
        {
            _hudRoot.gameObject.SetActive(false);
            _menuRoot.gameObject.SetActive(false);
            _arRoot.gameObject.SetActive(false);
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

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "…";
        }
    }
}
