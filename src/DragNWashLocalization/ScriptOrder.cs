using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace DragNWashLocalization
{
    // The order the game actually plays its lines in, so the published CSV can
    // be read top to bottom like a script.
    //
    // Generate() runs in the game with a level loaded: it reads the LevelFlow
    // asset (every level, regardless of the player's progress) and walks the
    // Yarn program from each level's nodes, descending into run_node targets
    // where they occur. The result is data/script_order.csv, which carries
    // node names, line ids, hashes and speakers - never the English text - so
    // it can be committed and used anywhere, including outside the game.
    //
    // Load() reads that file (shipped under the plugin's data/ folder, or the
    // freshly generated copy under _discovered/) and gives the writers the
    // sequence plus the section headers to print above each group.
    internal static class ScriptOrder
    {
        internal sealed class Entry
        {
            public string Section;   // "L01 Ryan", "Cutscene", "Reaction", "Unused"
            public string Phase;     // intro, phone, progress, idle, nag, jerkoff, cum, mount_start, mount_finish, outro, picnic, ""
            public string Node;
            public int Order;
            public string LineId;
            public string Key;
            public string Speaker;
            public string Condition; // variables the parent read before jumping here
        }

        internal sealed class LevelMeta
        {
            public int Index;
            public string Dragon;
            public string Weather;
            public string SetFlags;
            public string EndFlags;
            public string Section => $"L{Index + 1:00} {Dragon}";
            public string Header => $"Level {Index + 1}: {Dragon}" + (string.IsNullOrEmpty(Weather) ? "" : $" ({Weather})")
                                    + (string.IsNullOrEmpty(SetFlags) ? "" : $" | sets {SetFlags}")
                                    + (string.IsNullOrEmpty(EndFlags) ? "" : $" | ends {EndFlags}");
        }

        internal sealed class Data
        {
            public List<Entry> Entries = new List<Entry>();
            public Dictionary<string, LevelMeta> Levels = new Dictionary<string, LevelMeta>(StringComparer.Ordinal);
            public string Source;

            // key -> every speaker of that English, in play order ("Ryan/Alexander").
            private Dictionary<string, List<string>> _speakers;

            private void BuildSpeakers()
            {
                _speakers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (Entry e in Entries)
                {
                    if (string.IsNullOrEmpty(e.Speaker)) continue;
                    if (!_speakers.TryGetValue(e.Key, out List<string> list))
                    {
                        _speakers[e.Key] = list = new List<string>();
                    }
                    if (!list.Contains(e.Speaker)) list.Add(e.Speaker);
                }
            }

            public string SpeakersFor(string key)
            {
                if (_speakers == null) BuildSpeakers();
                return _speakers.TryGetValue(key, out List<string> list) ? string.Join("/", list) : string.Empty;
            }

            // The same English is said by more than one character, so a
            // translator may want a line-ID row for each occurrence.
            public bool IsShared(string key)
            {
                if (_speakers == null) BuildSpeakers();
                return _speakers.TryGetValue(key, out List<string> list) && list.Count > 1;
            }
        }

        private static readonly string[] PhaseOrder =
        {
            "intro", "phone", "progress", "idle", "nag", "picnic", "jerkoff", "cum", "mount_start", "mount_finish", "outro"
        };

        private static readonly HashSet<string> InternalVars = new HashSet<string>(StringComparer.Ordinal)
        {
            "$dragonModel", "$dragonHead", "$dragonPrefab", "$dragonName", "$cleanEvaluation", "$SetDragonState", "$isEditor"
        };

        // ------------------------------------------------------------ generate
        public static string Generate(string pluginDirectory)
        {
            List<FlowDumper.LevelInfo> levels = FlowDumper.ReadLevels(out string error);
            if (levels == null)
            {
                return "[order] " + error;
            }
            YarnProject[] projects = Resources.FindObjectsOfTypeAll<YarnProject>();
            if (projects.Length == 0)
            {
                return "[order] No YarnProject loaded (load a save first).";
            }

            // Node name -> (project, node), plus every line's English by id.
            var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            var textById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (YarnProject project in projects)
            {
                if (project.Program == null) continue;
                foreach (KeyValuePair<string, Node> kv in project.Program.Nodes)
                {
                    if (!nodes.ContainsKey(kv.Key)) nodes[kv.Key] = kv.Value;
                }
                foreach (KeyValuePair<string, string> kv in DialogueDumper.ReadBaseLocalization(project))
                {
                    if (!textById.ContainsKey(kv.Key)) textById[kv.Key] = kv.Value;
                }
            }

            var entries = new List<Entry>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            // Every occurrence is recorded, not only the first per English
            // string: an English line said by several characters needs each
            // line ID for per-line translations. Writers still print one hash
            // row per key, at its first occurrence.
            var emittedLines = new HashSet<string>(StringComparer.Ordinal);

            void Walk(string section, string phase, string nodeName, string condition)
            {
                if (string.IsNullOrEmpty(nodeName) || !visited.Add(nodeName) || !nodes.TryGetValue(nodeName, out Node node))
                {
                    return;
                }
                int order = 0;
                var reads = new List<string>();
                foreach (Instruction ins in node.Instructions)
                {
                    switch (ins.InstructionTypeCase)
                    {
                        case Instruction.InstructionTypeOneofCase.PushVariable:
                            if (!InternalVars.Contains(ins.PushVariable.VariableName) && !ins.PushVariable.VariableName.StartsWith("$Yarn.Internal", StringComparison.Ordinal)
                                && !reads.Contains(ins.PushVariable.VariableName))
                            {
                                reads.Add(ins.PushVariable.VariableName);
                            }
                            break;
                        case Instruction.InstructionTypeOneofCase.RunLine:
                            Emit(ins.RunLine.LineID, "line");
                            break;
                        case Instruction.InstructionTypeOneofCase.AddOption:
                            Emit(ins.AddOption.LineID, "option");
                            break;
                        case Instruction.InstructionTypeOneofCase.RunNode:
                            Walk(section, phase, ins.RunNode.NodeName, string.Join(" ", reads));
                            break;
                        case Instruction.InstructionTypeOneofCase.DetourToNode:
                            Walk(section, phase, ins.DetourToNode.NodeName, string.Join(" ", reads));
                            break;
                    }
                }

                void Emit(string lineId, string kind)
                {
                    if (!textById.TryGetValue(lineId, out string text) || text.Length == 0) return;
                    if (!emittedLines.Add(lineId)) return;
                    string key = TranslationStore.KeyFor(text);
                    order++;
                    entries.Add(new Entry
                    {
                        Section = section, Phase = phase, Node = nodeName, Order = order, LineId = lineId, Key = key,
                        Speaker = DialogueDumper.SpeakerFor(nodeName, kind, text), Condition = condition ?? "",
                    });
                }
            }

            // 1. Levels, in play order, each phase in the order the game runs it.
            foreach (FlowDumper.LevelInfo lv in levels)
            {
                string section = $"L{lv.Index + 1:00} {lv.Dragon}";
                foreach (string phase in PhaseOrder)
                {
                    foreach (string n in lv.NodesFor(phase)) Walk(section, phase, n, "");
                    if (phase == "picnic")
                    {
                        // Food items in the picnic level start their own nodes from the scene.
                        foreach (string n in nodes.Keys.Where(k => k.StartsWith("Ryan_", StringComparison.Ordinal) && (k.Contains("_egg") || k.Contains("_grapes") || k.Contains("_ham") || k.Contains("_melon") || k.Contains("_grab_basket")) && lv.Dragon == "Ryan" && lv.Index == 9).OrderBy(k => k, StringComparer.Ordinal))
                        {
                            Walk(section, phase, n, "");
                        }
                    }
                }
            }

            // 2. Nodes the level flow never names: cutscenes and reactions the game
            //    code starts, then whatever is left (legacy content).
            foreach (string n in nodes.Keys.Where(k => k.IndexOf("SexScene", StringComparison.Ordinal) >= 0).OrderBy(k => k, StringComparer.Ordinal))
                Walk("Cutscene", "", n, "");
            foreach (string n in nodes.Keys.Where(k => k.StartsWith("Dragon", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal))
                Walk("Reaction", "", n, "");
            foreach (string n in nodes.Keys.OrderBy(k => k, StringComparer.Ordinal))
                Walk("Unused", "", n, "");

            string dir = Path.Combine(pluginDirectory, "Translations", "_discovered");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "script_order.csv");
            using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                // Copied into the repository as data/script_order.csv, which
                // is LF like every other CSV here.
                w.NewLine = "\n";
                w.WriteLine("section,phase,node,order,line_id,key,speaker,condition");
                foreach (Entry e in entries)
                {
                    w.WriteLine(string.Join(",", CsvReader.Escape(e.Section), e.Phase, CsvReader.Escape(e.Node), e.Order.ToString(),
                        CsvReader.Escape(e.LineId), e.Key, CsvReader.Escape(e.Speaker), CsvReader.Escape(e.Condition)));
                }
            }
            int lines = textById.Count;
            return $"[order] Wrote {entries.Count} line(s) in play order ({lines} in the project, {visited.Count} node(s) visited) -> {path}. Copy script_order.csv and level_flow.csv into the repository's data/ folder.";
        }

        // ---------------------------------------------------------------- load
        private static Data _cached;
        private static string _cachedFrom;

        public static Data Load(string pluginDirectory)
        {
            string shipped = Path.Combine(pluginDirectory, "data", "script_order.csv");
            string generated = Path.Combine(pluginDirectory, "Translations", "_discovered", "script_order.csv");
            // The freshly generated file wins so a maintainer sees the new order at once.
            string path = File.Exists(generated) ? generated : (File.Exists(shipped) ? shipped : null);
            if (path == null) return null;
            string stamp = path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
            if (_cached != null && _cachedFrom == stamp) return _cached;

            var data = new Data { Source = path };
            try
            {
                foreach (Dictionary<string, string> row in CsvReader.ReadRows(path))
                {
                    row.TryGetValue("key", out string key);
                    if (string.IsNullOrEmpty(key)) continue;
                    row.TryGetValue("order", out string o);
                    int.TryParse(o, out int order);
                    data.Entries.Add(new Entry
                    {
                        Section = Get(row, "section"), Phase = Get(row, "phase"), Node = Get(row, "node"), Order = order,
                        LineId = Get(row, "line_id"), Key = key.Trim().ToLowerInvariant(), Speaker = Get(row, "speaker"), Condition = Get(row, "condition"),
                    });
                }
                string flow = Path.Combine(Path.GetDirectoryName(path), "level_flow.csv");
                if (File.Exists(flow))
                {
                    foreach (Dictionary<string, string> row in CsvReader.ReadRows(flow))
                    {
                        if (!int.TryParse(Get(row, "level"), out int index)) continue;
                        var meta = new LevelMeta
                        {
                            Index = index, Dragon = Get(row, "dragon"), Weather = Get(row, "weather"),
                            SetFlags = Get(row, "set_flags").Replace(" | ", ", "), EndFlags = Get(row, "end_flags").Replace(" | ", ", "),
                        };
                        data.Levels[meta.Section] = meta;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"[order] Could not read {path}: {ex.Message}");
                return null;
            }
            _cached = data;
            _cachedFrom = stamp;
            return data;
        }

        private static string Get(Dictionary<string, string> row, string col) => row.TryGetValue(col, out string v) ? v ?? "" : "";

        // ---------------------------------------------------------------- write
        // Walks the order, printing section and phase headers as comments, and
        // calls emit(key, entry) for each key that has a row. Keys the order
        // does not know are handed back for the caller to append.
        public static void WriteOrdered(TextWriter w, Data data, ICollection<string> keysPresent, Action<string, Entry> emit, out List<string> leftovers)
        {
            WriteOrdered(w, data, keysPresent, emit, null, null, out leftovers);
        }

        // As above, and also calls emitLine(entry) at every occurrence for
        // which wantLine(entry) is true, under the same headers, right after
        // the hash row when both fall on the same occurrence.
        public static void WriteOrdered(TextWriter w, Data data, ICollection<string> keysPresent, Action<string, Entry> emit,
            Func<Entry, bool> wantLine, Action<Entry> emitLine, out List<string> leftovers)
        {
            var done = new HashSet<string>(StringComparer.Ordinal);
            string lastSection = null, lastNode = null;
            foreach (Entry e in data.Entries)
            {
                bool hashRow = keysPresent.Contains(e.Key) && !done.Contains(e.Key);
                bool lineRow = emitLine != null && !string.IsNullOrEmpty(e.LineId) && wantLine(e);
                if (!hashRow && !lineRow) continue;
                if (e.Section != lastSection)
                {
                    string header = data.Levels.TryGetValue(e.Section, out LevelMeta meta) ? meta.Header : SectionTitle(e.Section);
                    w.WriteLine();
                    w.WriteLine("# ===== " + header + " =====");
                    lastSection = e.Section;
                    lastNode = null;
                }
                if (e.Node != lastNode)
                {
                    string title = (string.IsNullOrEmpty(e.Phase) ? "" : e.Phase + ": ") + e.Node
                                   + (string.IsNullOrEmpty(e.Condition) ? "" : " | if " + e.Condition);
                    w.WriteLine("# --- " + title + " ---");
                    lastNode = e.Node;
                }
                if (hashRow)
                {
                    emit(e.Key, e);
                    done.Add(e.Key);
                }
                if (lineRow)
                {
                    emitLine(e);
                }
            }
            leftovers = keysPresent.Where(k => !done.Contains(k)).ToList();
        }

        private static string SectionTitle(string section)
        {
            switch (section)
            {
                case "Cutscene": return "Cutscenes (started by game code)";
                case "Reaction": return "Dragon reactions (started by game code)";
                case "Unused": return "Unused nodes (not reachable in the current game)";
                default: return section;
            }
        }
    }
}
