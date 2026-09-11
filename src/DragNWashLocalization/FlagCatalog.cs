using System;
using System.Collections.Generic;
using System.IO;

namespace DragNWashLocalization
{
    // Every event flag the game is known to use, with a group and a short
    // description, read from FlagCatalog.csv next to the plugin DLL. The
    // game's own registry only lists flags that have been set at least once,
    // so the catalog is what lets the Saves tab show "unset" flags too.
    // Users can add rows; unknown flags found in a save are shown regardless.
    internal static class FlagCatalog
    {
        internal sealed class Entry
        {
            public string Id;
            public string Group;
            public string SetBy;
            public string Description;
        }

        private static List<Entry> _entries;
        private static Dictionary<string, Entry> _byId;

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                if (_entries == null)
                {
                    Load();
                }
                return _entries;
            }
        }

        public static Entry Find(string id)
        {
            if (_byId == null)
            {
                Load();
            }
            return _byId.TryGetValue(id, out Entry e) ? e : null;
        }

        public static void Reload()
        {
            _entries = null;
            _byId = null;
        }

        private static void Load()
        {
            _entries = new List<Entry>();
            _byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
            try
            {
                string path = Path.Combine(Plugin.PluginDirectory, "FlagCatalog.csv");
                if (!File.Exists(path))
                {
                    Plugin.Log("[flags] FlagCatalog.csv not found next to the plugin; only flags present in the save will be listed.");
                    return;
                }

                foreach (Dictionary<string, string> row in CsvReader.ReadRows(path))
                {
                    if (!row.TryGetValue("id", out string id) || string.IsNullOrEmpty(id) || _byId.ContainsKey(id))
                    {
                        continue;
                    }

                    var e = new Entry
                    {
                        Id = id,
                        Group = row.TryGetValue("group", out string g) && g.Length > 0 ? g : "Other",
                        SetBy = row.TryGetValue("set_by", out string s) ? s : "",
                        Description = row.TryGetValue("description", out string d) ? d : "",
                    };
                    _entries.Add(e);
                    _byId[id] = e;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[flags] Could not read FlagCatalog.csv: {ex.Message}");
            }
        }
    }
}
