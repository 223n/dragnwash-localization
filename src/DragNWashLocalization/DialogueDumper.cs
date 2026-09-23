using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace DragNWashLocalization
{
    // Dumps every line Yarn Spinner knows about, straight from whichever
    // YarnProject assets happen to be loaded, so translators get the whole
    // script without playing through every branch.
    //
    // The first version listed lines by ID. IDs are content hashes, so the file
    // came out in an order with no relationship to the order the game plays
    // them in, which makes the text nearly untranslatable: you cannot see who
    // is answering whom, which replies belong to which question, or where a
    // conversation begins.
    //
    // The compiled program has the real order. Each node is one conversation
    // (or a chunk of one) and its instruction list runs top to bottom, so
    // walking the instructions and picking out RunLine and AddOption recovers
    // the script as written - and tells lines apart from player choices, which
    // the ID listing also lost.
    internal static class DialogueDumper
    {
        private static readonly FieldInfo EntriesField = typeof(Localization).GetField(
            "entries", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo LocalizedStringField =
            typeof(Localization).GetNestedType("LocalizationTableEntry", BindingFlags.Public)
                ?.GetField("localizedString");

        public static void DumpAll(string pluginDirectory)
        {
            YarnProject[] projects = Resources.FindObjectsOfTypeAll<YarnProject>();
            Plugin.Log($"[dump] Found {projects.Length} loaded YarnProject asset(s).");

            if (projects.Length == 0)
            {
                Plugin.Log("[dump] No YarnProject is loaded yet. Get into a scene where dialogue can run (even without triggering a conversation) and try again.", LogKind.Warning);
                return;
            }

            if (EntriesField == null || LocalizedStringField == null)
            {
                Plugin.Log("[dump] ERROR: the Localization layout has changed and DialogueDumper needs updating.", LogKind.Error);
                return;
            }

            var rows = new List<string>();
            int ordered = 0;
            int unreferenced = 0;
            int nodeCount = 0;

            foreach (YarnProject project in projects)
            {
                Dictionary<string, string> textById = ReadBaseLocalization(project);
                if (textById.Count == 0)
                {
                    continue;
                }

                var emitted = new HashSet<string>(StringComparer.Ordinal);
                string[] nodeNames = GetNodeNames(project);
                Array.Sort(nodeNames, StringComparer.Ordinal);

                foreach (string nodeName in nodeNames)
                {
                    Node node = GetNode(project, nodeName);
                    if (node == null)
                    {
                        continue;
                    }

                    nodeCount++;
                    int order = 0;
                    foreach (Instruction instruction in node.Instructions)
                    {
                        string lineId;
                        string kind;
                        switch (instruction.InstructionTypeCase)
                        {
                            case Instruction.InstructionTypeOneofCase.RunLine:
                                lineId = instruction.RunLine.LineID;
                                kind = "line";
                                break;
                            case Instruction.InstructionTypeOneofCase.AddOption:
                                lineId = instruction.AddOption.LineID;
                                // What the player picks, not what a character says.
                                kind = "option";
                                break;
                            default:
                                continue;
                        }

                        order++;
                        emitted.Add(lineId);
                        ordered++;
                        rows.Add(FormatRow(project, nodeName, order, kind, lineId, textById));
                    }
                }

                // Lines the compiled program never references from a node still
                // belong in the file; a translator should not have to wonder
                // whether something was dropped.
                foreach (KeyValuePair<string, string> kv in textById)
                {
                    if (emitted.Contains(kv.Key))
                    {
                        continue;
                    }
                    unreferenced++;
                    rows.Add(FormatRow(project, "(not reached from any node)", 0, "line", kv.Key, textById));
                }
            }

            string dir = Path.Combine(pluginDirectory, "Translations", "_discovered");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "dialogue_lines.csv");

            using (var writer = new StreamWriter(path, append: false, new UTF8Encoding(false)))
            {
                writer.WriteLine("yarn_project,node,order,kind,speaker,line_id,key,source_en,translation,tags");
                foreach (string row in rows)
                {
                    writer.WriteLine(row);
                }
            }

            Plugin.Log($"[dump] Wrote {rows.Count} line(s): {ordered} in script order across {nodeCount} node(s), {unreferenced} not reached from any node -> {path}", LogKind.Result);
        }

        private static string FormatRow(YarnProject project, string nodeName, int order, string kind,
            string lineId, Dictionary<string, string> textById)
        {
            textById.TryGetValue(lineId, out string text);
            text = text ?? string.Empty;

            // Carry any existing translation through so re-dumping never costs
            // a translator work they have already done.
            TranslationStore.TryGetTranslation(text, out string translation);

            // The key column is what the published strings.csv is keyed by, so
            // a translator can see which repository row a line corresponds to.
            return string.Concat(
                CsvReader.Escape(project.name), ",",
                CsvReader.Escape(nodeName), ",",
                order.ToString(), ",",
                kind, ",",
                CsvReader.Escape(SpeakerFor(nodeName, kind, text)), ",",
                CsvReader.Escape(lineId), ",",
                (text.Length == 0 ? string.Empty : TranslationStore.KeyFor(text)), ",",
                CsvReader.Escape(text), ",",
                CsvReader.Escape(translation ?? string.Empty), ",",
                CsvReader.Escape(GetTags(project, lineId)));
        }

        private static string GetTags(YarnProject project, string lineId)
        {
            try
            {
                string[] metadata = project.lineMetadata?.GetMetadata(lineId);
                return metadata == null ? string.Empty : string.Join(" ", metadata);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string[] GetNodeNames(YarnProject project)
        {
            try
            {
                return project.NodeNames ?? Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Plugin.Log($"[dump] Could not read node names from {project.name}: {ex.Message}", LogKind.Warning);
                return Array.Empty<string>();
            }
        }

        private static Node GetNode(YarnProject project, string nodeName)
        {
            try
            {
                return project.Program.Nodes.TryGetValue(nodeName, out Node node) ? node : null;
            }
            catch
            {
                return null;
            }
        }


        // Who says a line, derived from the script's structure: options are
        // the player (the kobold); otherwise the node name carries the
        // character, and a "Name: text" line names its speaker outright.
        // Written into the exports so a translator can hear the voice without
        // opening the game.
        public static string SpeakerFor(string nodeName, string kind, string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                int colon = text.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0 && colon <= 12 && !text.Contains("<") && char.IsUpper(text[0]))
                {
                    return text.Substring(0, colon);
                }
            }
            if (kind == "option")
            {
                return "Kobold";
            }
            if (string.IsNullOrEmpty(nodeName))
            {
                return string.Empty;
            }
            if (nodeName.IndexOf("PhoneTutorial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Phone";
            }
            string head = nodeName.Split('_')[0];
            foreach (string name in new[] { "Alexander", "Conrad", "Ryan" })
            {
                if (head.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
            if (head.StartsWith("Mount", StringComparison.OrdinalIgnoreCase))
            {
                return "Alexander";
            }
            if (head.StartsWith("Dragon", StringComparison.OrdinalIgnoreCase))
            {
                return "Dragon";
            }
            return head;
        }

        // Every line's English with its speaker, in script order.
        public static IEnumerable<KeyValuePair<string, string>> EnumerateOrderedLines()
        {
            if (EntriesField == null || LocalizedStringField == null)
            {
                yield break;
            }

            foreach (YarnProject project in Resources.FindObjectsOfTypeAll<YarnProject>())
            {
                Dictionary<string, string> textById = ReadBaseLocalization(project);
                var emitted = new HashSet<string>(StringComparer.Ordinal);
                string[] nodeNames = GetNodeNames(project);
                Array.Sort(nodeNames, StringComparer.Ordinal);
                foreach (string nodeName in nodeNames)
                {
                    Node node = GetNode(project, nodeName);
                    if (node == null)
                    {
                        continue;
                    }
                    foreach (Instruction instruction in node.Instructions)
                    {
                        string lineId;
                        string kind;
                        switch (instruction.InstructionTypeCase)
                        {
                            case Instruction.InstructionTypeOneofCase.RunLine:
                                lineId = instruction.RunLine.LineID; kind = "line";
                                break;
                            case Instruction.InstructionTypeOneofCase.AddOption:
                                lineId = instruction.AddOption.LineID; kind = "option";
                                break;
                            default:
                                continue;
                        }
                        if (emitted.Add(lineId) && textById.TryGetValue(lineId, out string text) && text.Length > 0)
                        {
                            yield return new KeyValuePair<string, string>(text, SpeakerFor(nodeName, kind, text));
                        }
                    }
                }
                foreach (KeyValuePair<string, string> kv in textById)
                {
                    if (emitted.Add(kv.Key))
                    {
                        yield return new KeyValuePair<string, string>(kv.Value, string.Empty);
                    }
                }
            }
        }

        // Every line's English, in script order, from every loaded project.
        // WorkingCopy uses this to put the text back beside its hash.
        public static IEnumerable<string> EnumerateOrderedSources()
        {
            if (EntriesField == null || LocalizedStringField == null)
            {
                yield break;
            }

            foreach (YarnProject project in Resources.FindObjectsOfTypeAll<YarnProject>())
            {
                Dictionary<string, string> textById = ReadBaseLocalization(project);
                var emitted = new HashSet<string>(StringComparer.Ordinal);
                string[] nodeNames = GetNodeNames(project);
                Array.Sort(nodeNames, StringComparer.Ordinal);
                foreach (string nodeName in nodeNames)
                {
                    Node node = GetNode(project, nodeName);
                    if (node == null)
                    {
                        continue;
                    }
                    foreach (Instruction instruction in node.Instructions)
                    {
                        string lineId;
                        switch (instruction.InstructionTypeCase)
                        {
                            case Instruction.InstructionTypeOneofCase.RunLine:
                                lineId = instruction.RunLine.LineID;
                                break;
                            case Instruction.InstructionTypeOneofCase.AddOption:
                                lineId = instruction.AddOption.LineID;
                                break;
                            default:
                                continue;
                        }
                        if (emitted.Add(lineId) && textById.TryGetValue(lineId, out string text) && text.Length > 0)
                        {
                            yield return text;
                        }
                    }
                }
                foreach (KeyValuePair<string, string> kv in textById)
                {
                    if (emitted.Add(kv.Key))
                    {
                        yield return kv.Value;
                    }
                }
            }
        }

        internal static Dictionary<string, string> ReadBaseLocalization(YarnProject project)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            Localization baseLocalization = project.baseLocalization;
            if (baseLocalization == null)
            {
                return result;
            }

            // The table is internal, so read it rather than reimplementing the
            // Yarn line lookup.
            if (!(EntriesField.GetValue(baseLocalization) is IDictionary entries))
            {
                return result;
            }

            foreach (DictionaryEntry kv in entries)
            {
                string text = LocalizedStringField.GetValue(kv.Value) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    result[(string)kv.Key] = text;
                }
            }
            return result;
        }
    }
}
