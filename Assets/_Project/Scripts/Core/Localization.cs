using System;
using System.Collections.Generic;

namespace Suraksha.Core
{
    public enum AppLanguage
    {
        Hindi = 0,
        Santali = 1
    }

    /// <summary>Santali is written in Ol Chiki (1925) or Devanagari. The worker picks.</summary>
    public enum ScriptMode
    {
        Devanagari = 0,
        OlChiki = 1
    }

    public sealed class MissingLocalizationException : Exception
    {
        public MissingLocalizationException(string key, string locale)
            : base("Missing localisation key '" + key + "' in locale '" + locale + "'.")
        {
        }
    }

    /// <summary>
    /// Key -> string lookup with a hard failure mode. A missing key must never silently
    /// fall back to English: a worker who cannot read English would hit a dead screen and
    /// blame themselves. Loud in editor/dev builds, bracketed in release.
    /// </summary>
    public sealed class LocalizationService
    {
        /// <summary>
        /// Prefix that marks an entry as awaiting native translation. Stripped at load so it never
        /// reaches the screen, but it makes "this locale is not really translated yet" a fact the
        /// app knows about instead of a guess.
        /// </summary>
        public const string PendingPrefix = "TODO-SAT: ";

        private readonly Dictionary<string, Dictionary<string, string>> _tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private readonly Dictionary<string, bool> _pending =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public AppLanguage Language { get; private set; } = AppLanguage.Hindi;
        public ScriptMode Script { get; private set; } = ScriptMode.Devanagari;
        public string LocaleId { get; private set; } = "hi";

        /// <summary>True when the active locale still contains untranslated stubs. UI shows a chip.</summary>
        public bool LocaleIsPending { get; private set; }

        public static string LocaleIdFor(AppLanguage language, ScriptMode script)
        {
            if (language == AppLanguage.Hindi) return "hi";
            return script == ScriptMode.OlChiki ? "sat-olchiki" : "sat-deva";
        }

        public void LoadTable(string localeId, string json)
        {
            if (string.IsNullOrEmpty(localeId)) throw new ArgumentException("localeId is required", nameof(localeId));
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("json is empty", nameof(json));

            var flat = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (flat == null) throw new FormatException("Locale '" + localeId + "' did not parse to a key/value map.");

            var pending = false;
            foreach (var key in new List<string>(flat.Keys))
            {
                var value = flat[key];
                if (value != null && value.StartsWith(PendingPrefix, StringComparison.Ordinal))
                {
                    flat[key] = value.Substring(PendingPrefix.Length);
                    pending = true;
                }
            }

            _tables[localeId] = flat;
            _pending[localeId] = pending;
        }

        public void SetLocale(AppLanguage language, ScriptMode script)
        {
            var id = LocaleIdFor(language, script);
            if (!_tables.ContainsKey(id))
            {
                // Santali Devanagari is optional; fall back to Ol Chiki if that is what we have.
                if (language == AppLanguage.Santali && _tables.ContainsKey("sat-olchiki")) id = "sat-olchiki";
                else throw new MissingLocalizationException("<locale table>", id);
            }

            Language = language;
            Script = script;
            LocaleId = id;

            bool pending;
            LocaleIsPending = _pending.TryGetValue(id, out pending) && pending;
        }

        public bool TryGet(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;

            Dictionary<string, string> table;
            if (!_tables.TryGetValue(LocaleId, out table)) return false;
            return table.TryGetValue(key, out value);
        }

        public string Get(string key)
        {
            string value;
            if (TryGet(key, out value)) return value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            throw new MissingLocalizationException(key, LocaleId);
#else
            return "[" + key + "]";
#endif
        }

        /// <summary>Safe variant for optional keys (voice files, hints). Never throws.</summary>
        public string GetOrEmpty(string key)
        {
            string value;
            return TryGet(key, out value) ? value : string.Empty;
        }

        public bool HasLocale(string localeId)
        {
            return _tables.ContainsKey(localeId);
        }
    }
}
