using System;
using Suraksha.Bootstrap;
using Suraksha.Cert;
using Suraksha.Core;
using UnityEngine;

namespace Suraksha.Game
{
    /// <summary>
    /// Top-level state machine: menu -> lesson -> quiz -> result -> menu.
    ///
    /// Everything is one scene with runtime-built screens. That is not laziness: fewer scenes
    /// means fewer chances for a missing reference to blank the app on a device we cannot debug
    /// on site.
    /// </summary>
    public sealed class AppFlow : MonoBehaviour
    {
        public static AppFlow Instance { get; private set; }

        private GameRoot _root;
        private GameUI _ui;
        private LessonPresenter _presenter;

        private ScoreReport _lastReport;
        private string _lastToken;
        private string _verifyResult;

        /// <summary>
        /// AR is only offered where ARCore can actually run. The Editor and a Windows standalone
        /// build have no AR loader, so the switch is shown greyed out rather than failing at
        /// lesson start — a control that does nothing is worse than no control.
        /// </summary>
        private static bool ArSupported
        {
            get { return Application.platform == RuntimePlatform.Android; }
        }

        /// <summary>Off in the Editor by default so pressing Play is instant.</summary>
        private bool _arEnabled = ArSupported;

        public static AppFlow Create(GameRoot root)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("AppFlow");
            var flow = go.AddComponent<AppFlow>();
            flow._root = root;
            Instance = flow;
            flow.Build();
            return flow;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            if (_root == null)
            {
                Debug.LogError("[AppFlow] GameRoot missing — nothing can start.");
                return;
            }

            if (!_root.Ready)
            {
                Debug.LogError("[AppFlow] GameRoot failed to initialise: " + _root.InitError);
            }

            // Audio: one listener, one 2D source. Tones are generated, not loaded.
            gameObject.AddComponent<AudioListener>();

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.8f;
            ToneBank.Init(source);

            // Key light for the rooms.
            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 0.95f;
            sun.shadows = LightShadows.Soft;

            // Mobile budget: two pixel lights maximum, no more.
            QualitySettings.pixelLightCount = 2;
            Application.targetFrameRate = 60;

            _ui = GameUI.Create(_root.Localization);
            _ui.transform.SetParent(transform, false);
        }

        private void Start()
        {
            ShowMenu();
        }

        private void Update()
        {
            if (InputBridge.CancelDown && _presenter != null)
            {
                ShowMenu();
            }
        }

        // ------------------------------------------------------------------ menu

        private void ShowMenu()
        {
            TeardownLesson();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _ui.ShowMenu(StartLesson, SetLanguage, ToggleAr, ArSupported, _arEnabled);
        }

        private void ToggleAr()
        {
            if (!ArSupported) return;
            _arEnabled = !_arEnabled;
            ShowMenu();
        }

        private void SetLanguage(AppLanguage language)
        {
            var script = language == AppLanguage.Santali ? ScriptMode.OlChiki : ScriptMode.Devanagari;

            try
            {
                _root.Localization.SetLocale(language, script);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AppFlow] locale switch failed: " + e.Message);
            }

            ShowMenu();
        }

        // ------------------------------------------------------------------ lesson

        private void StartLesson(string lessonId)
        {
            TeardownLesson();

            LessonDef lesson;
            try
            {
                lesson = _root.LoadLesson(lessonId);
            }
            catch (Exception e)
            {
                Debug.LogError("[AppFlow] could not load lesson '" + lessonId + "': " + e.Message);
                return;
            }

            _presenter = LessonPresenter.Create(_root, _ui, lesson, ArSupported && _arEnabled);
            _presenter.transform.SetParent(transform, false);
            _presenter.Completed += OnCompleted;
            _presenter.Abandoned += ShowMenu;
        }

        private void OnCompleted(ScoreReport report, string token)
        {
            _lastReport = report;
            _lastToken = token;
            _verifyResult = null;

            TeardownLesson();
            RenderResult();
        }

        private void RenderResult()
        {
            if (_lastReport == null) return;

            _ui.ShowResult(_lastReport, _lastToken, RetryLesson, ShowMenu, VerifyCertificate, _verifyResult);
        }

        private void RetryLesson()
        {
            if (_lastReport == null) return;
            StartLesson(_lastReport.lessonId);
        }

        /// <summary>
        /// Re-verifies the certificate in front of the judge. This is the whole trust story in one
        /// tap: signature, then expiry, then revoke. A forged or edited token turns red here.
        /// </summary>
        private void VerifyCertificate()
        {
            if (string.IsNullOrEmpty(_lastToken))
            {
                _verifyResult = "no token";
                RenderResult();
                return;
            }

            CertPayload payload;
            var status = _root.Certificates.Verify(_lastToken, GameRoot.NowUnix(), out payload);

            _verifyResult = CertService.ColourFor(status) + " · " + status;
            RenderResult();
        }

        private void TeardownLesson()
        {
            if (_presenter == null) return;

            _presenter.Completed -= OnCompleted;
            _presenter.Abandoned -= ShowMenu;
            _presenter.Dispose();

            Destroy(_presenter.gameObject);
            _presenter = null;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
