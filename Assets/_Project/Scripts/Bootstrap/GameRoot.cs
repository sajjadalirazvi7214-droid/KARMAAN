using System;
using Suraksha.Cert;
using Suraksha.Content;
using Suraksha.Core;
using Suraksha.Sync;
using UnityEngine;

namespace Suraksha.Bootstrap
{
    /// <summary>
    /// The one persistent object in the app. Owns every service and survives scene loads,
    /// so a scene reload can never lose an attempt that has already been scored.
    ///
    /// Nothing in here touches AR. That is deliberate: the 3D fallback runs through the
    /// exact same GameRoot, the same LessonRunner and the same certificates.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        private const string SecretResourcePath = "Settings/cert_secret";
        private const string DevFallbackSecret = "DEV-ONLY-REPLACE-BEFORE-SUBMISSION";

        public LocalizationService Localization { get; private set; }
        public AttemptStore Attempts { get; private set; }
        public CertService Certificates { get; private set; }

        public bool Ready { get; private set; }
        public string InitError { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                Init();
                Ready = true;
            }
            catch (Exception e)
            {
                InitError = e.Message;
                Ready = false;
                Debug.LogError("[GameRoot] init failed: " + e);
            }
        }

        private void Init()
        {
            Localization = new LocalizationService();
            LoadLocale("hi");
            LoadLocale("sat-olchiki");

            // Start in Hindi/Devanagari; the language picker changes this before any lesson.
            Localization.SetLocale(AppLanguage.Hindi, ScriptMode.Devanagari);

            Attempts = AttemptStore.CreateDefault();
            Certificates = new CertService(ResolveCertSecret());
        }

        private void LoadLocale(string localeId)
        {
            var asset = Resources.Load<TextAsset>("Localization/" + localeId);
            if (asset == null)
            {
                Debug.LogWarning("[GameRoot] locale table missing: Localization/" + localeId);
                return;
            }

            try
            {
                Localization.LoadTable(localeId, asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameRoot] locale '" + localeId + "' failed: " + e.Message);
            }
        }

        private static string ResolveCertSecret()
        {
            var asset = Resources.Load<TextAsset>(SecretResourcePath);
            if (asset != null && !string.IsNullOrEmpty(asset.text.Trim()))
            {
                return asset.text.Trim();
            }

            Debug.LogWarning(
                "[GameRoot] No cert secret at Resources/" + SecretResourcePath +
                ". Falling back to the dev secret. Replace before submission.");
            return DevFallbackSecret;
        }

        public LessonDef LoadLesson(string lessonId)
        {
            return LessonRepository.Load(lessonId);
        }

        /// <summary>
        /// Closes out a lesson: logs the attempt, and issues a certificate only on a pass.
        /// Returns null when the worker failed — a failed attempt is logged, never certified.
        /// </summary>
        public string CompleteLesson(LessonDef lesson, ScoreReport report, string workerId, string centreCode)
        {
            if (lesson == null) throw new ArgumentNullException(nameof(lesson));
            if (report == null) throw new ArgumentNullException(nameof(report));

            var record = new AttemptRecord
            {
                workerId = workerId,
                lessonId = lesson.lessonId,
                score = report.score,
                passed = report.passed,
                issuedAt = NowUnix(),
                elapsedSec = report.elapsedSec,
                tries = report.missCount,
                centre = centreCode,
                weakSteps = report.weakSteps
            };

            record = Attempts.Append(record);

            if (!report.passed) return null;

            return Certificates.Issue(new CertPayload
            {
                wid = workerId,
                lid = lesson.lessonId,
                score = report.score,
                issuedAt = record.issuedAt,
                centre = centreCode,
                attempt = record.uuid
            });
        }

        public static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
