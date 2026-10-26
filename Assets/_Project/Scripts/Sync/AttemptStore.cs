using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Suraksha.Sync
{
    /// <summary>One completed attempt. This is the only thing that ever leaves the phone.</summary>
    [Serializable]
    public class AttemptRecord
    {
        public string uuid;
        public string workerId;
        public string lessonId;
        public int score;
        public bool passed;
        public long issuedAt;
        public float elapsedSec;
        public int tries;
        public string centre;
        public List<string> weakSteps = new List<string>();
    }

    /// <summary>
    /// Append-only attempt log (JSONL) plus a separate set of synced UUIDs.
    ///
    /// Why not SQLite yet: a native plugin means an Android ABI per architecture, a
    /// plugin-loading failure mode on cheap phones, and nothing in the demo needs SQL.
    /// The log is append-only so a crash mid-write loses at most the last line, and
    /// dedupe is by UUID so a replayed sync cannot double-count a worker.
    /// </summary>
    public sealed class AttemptStore
    {
        private readonly string _logPath;
        private readonly string _syncedPath;
        private readonly HashSet<string> _synced = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<AttemptRecord> _cache = new List<AttemptRecord>();

        public AttemptStore(string rootDirectory)
        {
            if (string.IsNullOrEmpty(rootDirectory)) throw new ArgumentException("rootDirectory is required");

            Directory.CreateDirectory(rootDirectory);
            _logPath = Path.Combine(rootDirectory, "attempts.jsonl");
            _syncedPath = Path.Combine(rootDirectory, "synced.json");

            Load();
        }

        public static AttemptStore CreateDefault()
        {
            return new AttemptStore(Path.Combine(Application.persistentDataPath, "suraksha"));
        }

        public int Count => _cache.Count;

        public IReadOnlyList<AttemptRecord> All => _cache;

        public AttemptRecord Append(AttemptRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));

            if (string.IsNullOrEmpty(record.uuid)) record.uuid = Guid.NewGuid().ToString("N");
            if (record.issuedAt <= 0) record.issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Never double-append the same attempt.
            for (var i = 0; i < _cache.Count; i++)
            {
                if (_cache[i].uuid == record.uuid) return _cache[i];
            }

            var line = JsonConvert.SerializeObject(record, Formatting.None);
            try
            {
                using (var writer = new StreamWriter(_logPath, true))
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AttemptStore] append failed: " + e.Message);
            }

            _cache.Add(record);
            return record;
        }

        public List<AttemptRecord> Pending()
        {
            var pending = new List<AttemptRecord>();
            for (var i = 0; i < _cache.Count; i++)
            {
                if (!_synced.Contains(_cache[i].uuid)) pending.Add(_cache[i]);
            }
            return pending;
        }

        public void MarkSynced(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return;
            if (!_synced.Add(uuid)) return;
            FlushSynced();
        }

        public void MarkSynced(IEnumerable<string> uuids)
        {
            var changed = false;
            foreach (var id in uuids)
            {
                if (!string.IsNullOrEmpty(id) && _synced.Add(id)) changed = true;
            }
            if (changed) FlushSynced();
        }

        public bool IsSynced(string uuid)
        {
            return !string.IsNullOrEmpty(uuid) && _synced.Contains(uuid);
        }

        private void Load()
        {
            if (File.Exists(_logPath))
            {
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(_logPath);
                }
                catch (Exception e)
                {
                    Debug.LogError("[AttemptStore] read failed: " + e.Message);
                    lines = new string[0];
                }

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var rec = JsonConvert.DeserializeObject<AttemptRecord>(line);
                        if (rec != null && !string.IsNullOrEmpty(rec.uuid)) _cache.Add(rec);
                    }
                    catch (Exception)
                    {
                        // A torn last line after a crash is expected. Skip it, keep going.
                    }
                }
            }

            if (File.Exists(_syncedPath))
            {
                try
                {
                    var ids = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(_syncedPath));
                    if (ids != null)
                    {
                        foreach (var id in ids)
                        {
                            if (!string.IsNullOrEmpty(id)) _synced.Add(id);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[AttemptStore] synced set unreadable, starting empty: " + e.Message);
                }
            }
        }

        private void FlushSynced()
        {
            try
            {
                File.WriteAllText(_syncedPath, JsonConvert.SerializeObject(new List<string>(_synced), Formatting.None));
            }
            catch (Exception e)
            {
                Debug.LogError("[AttemptStore] synced flush failed: " + e.Message);
            }
        }
    }
}
