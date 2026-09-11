using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DragNWashLocalization
{
    // The game keeps one save per slot at
    //   <persistentDataPath>/<steamid>_slot<N>/savegame.dgn
    // and overwrites it on every save, keeping a single savegame_backup.dgn.
    // A translator checking a scene wants to step back to the previous state,
    // which that one backup does not reliably give them. So: snapshot the
    // file every time it changes, keep a rolling history per slot, and let the
    // debug menu put any snapshot back.
    //
    // Restoring only rewrites the file. The game reads it when a slot is
    // loaded, so the player returns to the title screen and loads the slot;
    // saving in game afterwards overwrites it again, as it should. Nothing
    // here touches flags or Yarn state - it is the game's own save, one step
    // earlier.
    internal static class SaveHistory
    {
        public const string SaveFileName = "savegame.dgn";

        private const float PollInterval = 2f;
        private static readonly Regex LevelIndex = new Regex("\"levelIndex\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        internal sealed class Snapshot
        {
            public string Path;
            public DateTime Taken;
            public string Level;
            public string Label => $"{Taken:yyyy-MM-dd HH:mm:ss}  level {Level}";
        }

        private static float _nextPoll;
        private static string _historyRoot;
        private static int _keep = 30;
        private static readonly Dictionary<string, DateTime> LastWrite = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> LastContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Configure(string pluginDirectory, int keep)
        {
            _historyRoot = Path.Combine(pluginDirectory, "SaveHistory");
            _keep = Mathf.Max(1, keep);
        }

        // Slot directories that currently hold a save, newest activity first.
        public static List<string> Slots()
        {
            var slots = new List<string>();
            try
            {
                foreach (string dir in Directory.GetDirectories(Application.persistentDataPath))
                {
                    if (File.Exists(Path.Combine(dir, SaveFileName)))
                    {
                        slots.Add(Path.GetFileName(dir));
                    }
                }
                slots.Sort((a, b) => File.GetLastWriteTimeUtc(Path.Combine(Application.persistentDataPath, b, SaveFileName))
                    .CompareTo(File.GetLastWriteTimeUtc(Path.Combine(Application.persistentDataPath, a, SaveFileName))));
            }
            catch (Exception ex)
            {
                Plugin.Log($"[saves] Could not list save slots: {ex.Message}");
            }
            return slots;
        }

        public static List<Snapshot> SnapshotsFor(string slot)
        {
            var list = new List<Snapshot>();
            try
            {
                string dir = Path.Combine(_historyRoot, slot);
                if (!Directory.Exists(dir))
                {
                    return list;
                }
                foreach (string file in Directory.GetFiles(dir, "*.dgn"))
                {
                    list.Add(new Snapshot
                    {
                        Path = file,
                        Taken = File.GetLastWriteTime(file),
                        Level = ReadLevel(file),
                    });
                }
                list.Sort((a, b) => b.Taken.CompareTo(a.Taken));
            }
            catch (Exception ex)
            {
                Plugin.Log($"[saves] Could not list snapshots for {slot}: {ex.Message}");
            }
            return list;
        }

        // Main thread only.
        public static void Tick()
        {
            if (_historyRoot == null || Time.unscaledTime < _nextPoll)
            {
                return;
            }
            _nextPoll = Time.unscaledTime + PollInterval;

            foreach (string slot in Slots())
            {
                string savePath = Path.Combine(Application.persistentDataPath, slot, SaveFileName);
                try
                {
                    DateTime now = File.GetLastWriteTimeUtc(savePath);
                    if (LastWrite.TryGetValue(slot, out DateTime seen) && seen == now)
                    {
                        continue;
                    }
                    LastWrite[slot] = now;

                    // The game writes on more occasions than the state changes;
                    // only a different file is worth a snapshot.
                    string content = File.ReadAllText(savePath);
                    if (LastContent.TryGetValue(slot, out string prev) && prev == content)
                    {
                        continue;
                    }
                    LastContent[slot] = content;

                    TakeSnapshot(slot, savePath, content);
                }
                catch (IOException)
                {
                    // Mid-write; next poll.
                    LastWrite.Remove(slot);
                }
                catch (Exception ex)
                {
                    Plugin.Log($"[saves] Snapshot failed for {slot}: {ex.Message}");
                }
            }
        }

        private static void TakeSnapshot(string slot, string savePath, string content)
        {
            string dir = Path.Combine(_historyRoot, slot);
            Directory.CreateDirectory(dir);

            // Skip if identical to the newest snapshot (covers restarts, where
            // LastContent is empty but nothing changed since the last run).
            List<Snapshot> existing = SnapshotsFor(slot);
            if (existing.Count > 0 && File.ReadAllText(existing[0].Path) == content)
            {
                return;
            }

            string target = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dgn");
            File.Copy(savePath, target, overwrite: true);
            Plugin.Log($"[saves] Snapshot of {slot} saved ({existing.Count + 1} kept, level {ReadLevel(target)}).");

            for (int i = _keep - 1; i < existing.Count; i++)
            {
                try { File.Delete(existing[i].Path); } catch { }
            }
        }

        // Put a snapshot back as the slot's save. The file being replaced is
        // snapshotted first so a restore is itself reversible.
        public static string Restore(string slot, Snapshot snapshot)
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, slot, SaveFileName);
                if (File.Exists(savePath))
                {
                    string current = File.ReadAllText(savePath);
                    LastContent[slot] = current;
                    TakeSnapshot(slot, savePath, current);
                }
                File.Copy(snapshot.Path, savePath, overwrite: true);
                LastWrite[slot] = File.GetLastWriteTimeUtc(savePath);
                LastContent[slot] = File.ReadAllText(savePath);
                return $"[saves] Restored {slot} to {snapshot.Label}. Return to the title screen and load the slot for it to take effect; saving in game will overwrite it again.";
            }
            catch (Exception ex)
            {
                return $"[saves] Restore failed: {ex.Message}";
            }
        }

        // ---- progress editing -------------------------------------------
        // The save is a JSON array: [{"levelIndex":N}, {"id":..,"type":"BOOL",
        // "boolValue":..,"stringValue":..}, ...]. Edits are done with regexes on
        // the text so the file keeps exactly the game's own layout.

        internal struct Flag
        {
            public string Id;
            public bool Value;
        }

        public static int ReadLevel(string slot, out string error)
        {
            error = null;
            try
            {
                string m = ReadLevel(Path.Combine(Application.persistentDataPath, slot, SaveFileName));
                return int.TryParse(m, out int level) ? level : -1;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return -1;
            }
        }

        public static List<Flag> ReadFlags(string slot)
        {
            var list = new List<Flag>();
            try
            {
                string text = File.ReadAllText(Path.Combine(Application.persistentDataPath, slot, SaveFileName));
                foreach (Match m in FlagEntry.Matches(text))
                {
                    list.Add(new Flag { Id = m.Groups[1].Value, Value = m.Groups[2].Value == "true" });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[saves] Could not read flags of {slot}: {ex.Message}");
            }
            return list;
        }

        public static string SetLevel(string slot, int level)
        {
            return Edit(slot, text => LevelIndex.Replace(text, "\"levelIndex\":" + level, 1),
                $"levelIndex set to {level}");
        }

        public static string SetFlag(string slot, string id, bool value)
        {
            return Edit(slot, text => ApplyFlag(text, id, value), $"{id} = {(value ? "true" : "false")}");
        }

        // Several flags in one edit (one snapshot, one write).
        public static string SetFlags(string slot, IEnumerable<KeyValuePair<string, bool>> values, string what)
        {
            return Edit(slot, text =>
            {
                foreach (KeyValuePair<string, bool> kv in values)
                {
                    text = ApplyFlag(text, kv.Key, kv.Value);
                }
                return text;
            }, what);
        }

        // Rewrites an existing entry, or appends one when the game has never
        // set that flag (the registry only stores flags that were set once).
        private static string ApplyFlag(string text, string id, bool value)
        {
            string v = value ? "true" : "false";
            var one = new Regex("(\"id\"\\s*:\\s*\"" + Regex.Escape(id) + "\"\\s*,\\s*\"type\"\\s*:\\s*\"BOOL\"\\s*,\\s*\"boolValue\"\\s*:\\s*)(true|false)(\\s*,\\s*\"stringValue\"\\s*:\\s*)(true|false)");
            if (one.IsMatch(text))
            {
                return one.Replace(text, "${1}" + v + "${3}" + v, 1);
            }

            int end = text.LastIndexOf(']');
            if (end < 0)
            {
                return text;
            }
            string entry = "{\"id\":\"" + id.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"type\":\"BOOL\",\"boolValue\":" + v + ",\"stringValue\":" + v + "}";
            string before = text.Substring(0, end).TrimEnd();
            string sep = before.EndsWith("[") ? "" : ",";
            return before + sep + entry + text.Substring(end);
        }

        private static readonly Regex FlagEntry = new Regex(
            "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"type\"\\s*:\\s*\"BOOL\"\\s*,\\s*\"boolValue\"\\s*:\\s*(true|false)", RegexOptions.Compiled);

        // Snapshot, rewrite, and re-arm change tracking so the edit itself is
        // not reported as a game write.
        private static string Edit(string slot, Func<string, string> change, string what)
        {
            try
            {
                string savePath = Path.Combine(Application.persistentDataPath, slot, SaveFileName);
                string current = File.ReadAllText(savePath);
                LastContent[slot] = current;
                TakeSnapshot(slot, savePath, current);
                string edited = change(current);
                if (edited == current)
                {
                    return $"[saves] Nothing changed ({what}).";
                }
                File.WriteAllText(savePath, edited);
                LastWrite[slot] = File.GetLastWriteTimeUtc(savePath);
                LastContent[slot] = edited;
                return $"[saves] {slot}: {what}. Return to the title screen and load the slot for it to take effect.";
            }
            catch (Exception ex)
            {
                return $"[saves] Edit failed: {ex.Message}";
            }
        }

        private static string ReadLevel(string file)
        {
            try
            {
                Match m = LevelIndex.Match(File.ReadAllText(file));
                return m.Success ? m.Groups[1].Value : "?";
            }
            catch
            {
                return "?";
            }
        }
    }
}
