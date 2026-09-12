using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Yarn;
using Yarn.Unity;

namespace DragNWashLocalization
{
    // Investigation exports for ordering the public CSV the way the game
    // actually plays. Two files under Translations/_discovered/:
    //
    //   level_flow.csv     one row per level: dragon, which Yarn nodes the
    //                      level runs (intro / progress / idle / nag / phone /
    //                      outro / cutscene hooks) and the flags it sets.
    //                      Read through reflection from the game's LevelFlow
    //                      ScriptableObject, so nothing here compiles against
    //                      Assembly-CSharp.
    //   dialogue_graph.csv one row per Yarn instruction that matters for flow:
    //                      lines, options, commands, jumps, conditions, and
    //                      variable reads/writes, in program order per node.
    internal static class FlowDumper
    {
        public static string Export(string pluginDirectory)
        {
            string dir = Path.Combine(pluginDirectory, "Translations", "_discovered");
            Directory.CreateDirectory(dir);
            var log = new StringBuilder();
            log.Append(ExportLevelFlow(Path.Combine(dir, "level_flow.csv")));
            log.Append("  ");
            log.Append(ExportGraph(Path.Combine(dir, "dialogue_graph.csv")));
            return log.ToString();
        }

        // ---------------------------------------------------------------- levels
        private static string ExportLevelFlow(string path)
        {
            Type flowType = AccessTools.TypeByName("LevelFlow");
            if (flowType == null)
            {
                return "[flow] LevelFlow type not found.";
            }

            UnityEngine.Object[] flows = Resources.FindObjectsOfTypeAll(flowType);
            if (flows.Length == 0)
            {
                return "[flow] No LevelFlow asset is loaded (load a save first).";
            }

            var rows = new List<string>();
            int total = 0;
            foreach (UnityEngine.Object flow in flows)
            {
                int count = (int)Call(flow, "GetLevelCount");
                for (int i = 0; i < count; i++)
                {
                    object dragon = Call(flow, "GetDragonDescriptor", i);
                    string dragonName = dragon == null ? "" : (ReadField(dragon, "name") as string ?? ((UnityEngine.Object)dragon).name);
                    rows.Add(string.Join(",", new[]
                    {
                        Csv(flow.name), i.ToString(), Csv(dragonName),
                        Csv(Str(Call(flow, "GetDialogIntro", i))),
                        Csv(Join(Call(flow, "GetProgressDialogs", i))),
                        Csv(Join(Call(flow, "GetIdleDialogs", i))),
                        Csv(Join(NagDialogs(flow, i))),
                        Csv(Str(Call(flow, "GetPhoneDialog", i))),
                        Csv(Str(Call(flow, "GetDialogOutro", i))),
                        Csv(Str(Call(flow, "GetDragonStartJerkingOffDialog", i))),
                        Csv(Str(Call(flow, "GetDragonCumDialog", i))),
                        Csv(Str(Call(flow, "GetDragonStartMountDialogue", i))),
                        Csv(Str(Call(flow, "GetDragonFinishMountDialogue", i))),
                        Csv(Str(Call(flow, "GetDragonSpawnFlag", i))),
                        Csv(Join(Call(flow, "GetSetFlags", i))),
                        Csv(Join(Call(flow, "GetEndFlags", i))),
                        Csv(Str(Call(flow, "GetWeatherState", i))),
                        Csv(Str(Call(flow, "GetPlayerSpawnTransformName", i))),
                    }));
                    total++;
                }
            }

            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                w.WriteLine("flow_asset,level,dragon,intro,progress_dialogs,idle_dialogs,nag_dialogs,phone,outro,jerkoff_dialog,cum_dialog,mount_start,mount_finish,spawn_flag,set_flags,end_flags,weather,player_spawn");
                foreach (string r in rows) w.WriteLine(r);
            }
            return $"[flow] Wrote {total} level(s) from {flows.Length} LevelFlow asset(s) -> {path}";
        }

        private static object NagDialogs(object flow, int level)
        {
            // LevelFlow exposes nag dialogs only through a random getter; the
            // LevelConfiguration itself keeps the array.
            try
            {
                object levels = ReadField(flow, "levels");
                if (levels is Array arr && level < arr.Length)
                {
                    object cfg = arr.GetValue(level);
                    return cfg == null ? null : ReadField(cfg, "nagDialogs");
                }
            }
            catch { }
            return null;
        }

        private static object Call(object target, string method, params object[] args)
        {
            try
            {
                MethodInfo m = AccessTools.Method(target.GetType(), method);
                return m?.Invoke(target, args);
            }
            catch (Exception ex)
            {
                Plugin.Log($"[flow] {method} failed: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }

        private static object ReadField(object target, string field)
        {
            FieldInfo f = AccessTools.Field(target.GetType(), field);
            return f?.GetValue(target);
        }

        private static string Str(object o) => o == null ? "" : o.ToString();

        private static string Join(object o)
        {
            if (o is IEnumerable e && !(o is string))
            {
                return string.Join(" | ", e.Cast<object>().Select(x => x?.ToString() ?? ""));
            }
            return Str(o);
        }

        // ----------------------------------------------------------------- graph
        private static string ExportGraph(string path)
        {
            YarnProject[] projects = Resources.FindObjectsOfTypeAll<YarnProject>();
            if (projects.Length == 0)
            {
                return "[graph] No YarnProject loaded.";
            }

            var rows = new List<string>();
            int nodes = 0;
            foreach (YarnProject project in projects)
            {
                Program program = project.Program;
                if (program == null) continue;
                foreach (KeyValuePair<string, Node> kv in program.Nodes.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    Node node = kv.Value;
                    nodes++;
                    string headers = string.Join(" | ", node.Headers.Select(h => h.Key + "=" + h.Value));
                    rows.Add(Row(project.name, kv.Key, 0, "node", "", headers));
                    int index = 0;
                    foreach (Instruction ins in node.Instructions)
                    {
                        index++;
                        string kind, detail = "";
                        switch (ins.InstructionTypeCase)
                        {
                            case Instruction.InstructionTypeOneofCase.RunLine:
                                kind = "line"; detail = ins.RunLine.LineID; break;
                            case Instruction.InstructionTypeOneofCase.AddOption:
                                kind = "option"; detail = ins.AddOption.LineID + (ins.AddOption.HasCondition ? " (conditional)" : ""); break;
                            case Instruction.InstructionTypeOneofCase.ShowOptions:
                                kind = "show_options"; break;
                            case Instruction.InstructionTypeOneofCase.RunCommand:
                                kind = "command"; detail = ins.RunCommand.CommandText; break;
                            case Instruction.InstructionTypeOneofCase.JumpTo:
                                kind = "jump"; detail = ins.JumpTo.Destination.ToString(); break;
                            case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                                kind = "jump_if_false"; detail = ins.JumpIfFalse.Destination.ToString(); break;
                            case Instruction.InstructionTypeOneofCase.PeekAndJump:
                                kind = "jump_peek"; break;
                            case Instruction.InstructionTypeOneofCase.PeekAndRunNode:
                                kind = "run_node_peek"; break;
                            case Instruction.InstructionTypeOneofCase.RunNode:
                                kind = "run_node"; detail = ins.RunNode.NodeName; break;
                            case Instruction.InstructionTypeOneofCase.DetourToNode:
                                kind = "detour"; detail = ins.DetourToNode.NodeName; break;
                            case Instruction.InstructionTypeOneofCase.PushVariable:
                                kind = "read_var"; detail = ins.PushVariable.VariableName; break;
                            case Instruction.InstructionTypeOneofCase.StoreVariable:
                                kind = "set_var"; detail = ins.StoreVariable.VariableName; break;
                            case Instruction.InstructionTypeOneofCase.PushString:
                                kind = "push_string"; detail = ins.PushString.Value; break;
                            case Instruction.InstructionTypeOneofCase.PushBool:
                                kind = "push_bool"; detail = ins.PushBool.Value.ToString(); break;
                            case Instruction.InstructionTypeOneofCase.PushFloat:
                                kind = "push_float"; detail = ins.PushFloat.Value.ToString(); break;
                            case Instruction.InstructionTypeOneofCase.CallFunc:
                                kind = "call"; detail = ins.CallFunc.FunctionName; break;
                            case Instruction.InstructionTypeOneofCase.Stop:
                                kind = "stop"; break;
                            case Instruction.InstructionTypeOneofCase.Return:
                                kind = "return"; break;
                            default:
                                kind = ins.InstructionTypeCase.ToString().ToLowerInvariant(); break;
                        }
                        rows.Add(Row(project.name, kv.Key, index, kind, detail, ""));
                    }
                }
                // Variable declarations with initial values, once per project.
                foreach (KeyValuePair<string, Operand> iv in program.InitialValues.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    rows.Add(Row(project.name, "(initial values)", 0, "declare", iv.Key + " = " + iv.Value, ""));
                }
            }

            using (var w = new StreamWriter(path, false, Encoding.UTF8))
            {
                w.WriteLine("yarn_project,node,index,kind,detail,headers");
                foreach (string r in rows) w.WriteLine(r);
            }
            return $"[graph] Wrote {rows.Count} row(s) across {nodes} node(s) -> {path}";
        }

        private static string Row(string project, string node, int index, string kind, string detail, string headers)
            => string.Join(",", Csv(project), Csv(node), index.ToString(), kind, Csv(detail), Csv(headers));

        private static string Csv(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
