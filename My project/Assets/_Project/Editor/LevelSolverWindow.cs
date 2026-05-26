using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort > Verify All Levels — runs BFS solvability check on every level in
    /// Assets/Resources/levels.json and reports minimum-move count vs par.
    /// </summary>
    public class LevelSolverWindow : EditorWindow
    {
        private struct LevelResult
        {
            public int    LevelId;
            public string DisplayName;
            public int    Par;
            public bool   Ran;
            public LevelSolver.SolverResult Result;
        }

        private readonly List<LevelResult> _results = new();
        private Vector2 _scroll;
        private bool    _running;
        private string  _summary;

        [MenuItem("BoltSort/Verify All Levels")]
        private static void Open() => GetWindow<LevelSolverWindow>("Verify All Levels");

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("BFS Level Solvability Checker", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Verifies every level in Resources/levels.json using exact game move rules.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            GUI.enabled = !_running;
            if (GUILayout.Button("Run Verification", GUILayout.Height(30)))
                RunAll();
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(_summary))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_summary, MessageType.Info);
            }

            if (_results.Count == 0) return;

            EditorGUILayout.Space(4);

            // Header
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("ID",      GUILayout.Width(28));
            EditorGUILayout.LabelField("Name",    GUILayout.Width(90));
            EditorGUILayout.LabelField("Par",     GUILayout.Width(36));
            EditorGUILayout.LabelField("BFSMin",  GUILayout.Width(52));
            EditorGUILayout.LabelField("Result",  GUILayout.Width(80));
            EditorGUILayout.LabelField("States",  GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _results)
                DrawRow(r);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(LevelResult r)
        {
            if (!r.Ran) return;

            Color orig = GUI.color;
            if (r.Result.TimedOut)
                GUI.color = new Color(1f, 0.85f, 0f);
            else if (!r.Result.IsSolvable)
                GUI.color = new Color(1f, 0.4f, 0.4f);
            else if (r.Result.MinMoves > r.Par)
                GUI.color = new Color(1f, 0.7f, 0.4f); // par too low — 3-star unreachable
            else
                GUI.color = new Color(0.7f, 1f, 0.7f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(r.LevelId.ToString(),           GUILayout.Width(28));
            EditorGUILayout.LabelField(r.DisplayName,                  GUILayout.Width(90));
            EditorGUILayout.LabelField(r.Par.ToString(),               GUILayout.Width(36));
            EditorGUILayout.LabelField(r.Result.TimedOut ? "—" : r.Result.MinMoves.ToString(),
                                                                        GUILayout.Width(52));

            string status = r.Result.TimedOut   ? "TIMEOUT"    :
                            !r.Result.IsSolvable ? "UNSOLVABLE" :
                            r.Result.MinMoves > r.Par ? "PAR LOW"   : "OK";
            EditorGUILayout.LabelField(status,                         GUILayout.Width(80));
            EditorGUILayout.LabelField(r.Result.StatesExplored.ToString(), GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            GUI.color = orig;
        }

        private void RunAll()
        {
            _results.Clear();
            _summary = string.Empty;
            _running = true;
            Repaint();

            string path = Application.dataPath + "/Resources/levels.json";
            string json;
            try   { json = System.IO.File.ReadAllText(path); }
            catch (Exception ex)
            {
                _summary = $"Failed to read levels.json: {ex.Message}";
                _running = false;
                return;
            }

            JObject root;
            JArray  levels;
            try
            {
                root   = JObject.Parse(json);
                levels = (JArray)root["levels"];
            }
            catch (Exception ex)
            {
                _summary = $"JSON parse error: {ex.Message}";
                _running = false;
                return;
            }

            int ok = 0, badPar = 0, unsolvable = 0, timeout = 0;

            foreach (JObject lvl in levels)
            {
                int levelId     = (int)lvl["level_id"];
                string name     = (string)lvl["display_name"] ?? $"Level {levelId}";
                int par         = (int)lvl["par_moves"];
                int colorCount  = (int)lvl["color_count"];
                int stackDepth  = (int)lvl["stack_depth"];
                int tempCount   = (int)lvl["temp_slot_count"];
                int tempDepth   = (int)lvl["temp_slot_depth"];

                var colorStacksToken = (JArray)lvl["color_stacks"];
                var colorStacks = new int[colorStacksToken.Count][];
                for (int i = 0; i < colorStacks.Length; i++)
                {
                    var col = (JArray)colorStacksToken[i];
                    colorStacks[i] = new int[col.Count];
                    for (int j = 0; j < col.Count; j++)
                        colorStacks[i][j] = (int)col[j];
                }

                var spec = new LevelSolver.LevelSpec
                {
                    ColorCount    = colorCount,
                    StackDepth    = stackDepth,
                    TempSlotCount = tempCount,
                    TempSlotDepth = tempDepth,
                    ColorStacks   = colorStacks,
                };

                var result = LevelSolver.Solve(spec);

                if (result.TimedOut)          timeout++;
                else if (!result.IsSolvable)  unsolvable++;
                else if (result.MinMoves > par) badPar++;
                else                           ok++;

                _results.Add(new LevelResult
                {
                    LevelId     = levelId,
                    DisplayName = name,
                    Par         = par,
                    Ran         = true,
                    Result      = result,
                });
            }

            _summary = $"Done. OK: {ok}  PAR-TOO-LOW: {badPar}  UNSOLVABLE: {unsolvable}  TIMEOUT: {timeout}";
            _running = false;
            Repaint();
        }
    }
}
