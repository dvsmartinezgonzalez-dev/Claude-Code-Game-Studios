using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BoltSort.LevelData;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace BoltSort.Editor
{
    /// <summary>
    /// BoltSort > Verify All Levels — runs BFS solvability on every level in
    /// Assets/Resources/levels.json and reports minimum-move count vs par.
    /// </summary>
    public class LevelSolverWindow : EditorWindow
    {
        private struct Row
        {
            public int    LevelId;
            public int    Tier;
            public int    Colors;
            public int    Depth;
            public int    Par;
            public bool   Ran;
            public LevelSolver.SolverResult Result;
        }

        private readonly List<Row> _rows = new();
        private Vector2 _scroll;
        private string  _statusMsg;

        [MenuItem("BoltSort/Verify All Levels")]
        private static void Open() => GetWindow<LevelSolverWindow>("Verify All Levels");

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("BFS Level Solvability Checker", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Loads Resources/levels.json and runs BFS on each level using exact game rules.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run Verification", GUILayout.Height(28)))
                RunAll();
            GUI.enabled = _rows.Count > 0;
            if (GUILayout.Button("Export Report", GUILayout.Height(28)))
                ExportReport();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusMsg))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_statusMsg, MessageType.Info);
            }

            if (_rows.Count == 0) return;

            EditorGUILayout.Space(6);
            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _rows)
                DrawRow(row);
            DrawSummary();
            EditorGUILayout.EndScrollView();
        }

        // ── Table drawing ─────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            HeaderLabel("ID",      36);
            HeaderLabel("Tier",    36);
            HeaderLabel("Colors",  52);
            HeaderLabel("Depth",   48);
            HeaderLabel("Par",     36);
            HeaderLabel("BFSMin",  52);
            HeaderLabel("Status",  120);
            EditorGUILayout.EndHorizontal();
        }

        private static void HeaderLabel(string text, float w)
            => EditorGUILayout.LabelField(text, EditorStyles.toolbarButton, GUILayout.Width(w));

        private void DrawRow(Row r)
        {
            if (!r.Ran) return;

            string status;
            Color  bg;
            if (r.Result.FailReason == "timeout")
            {
                status = "⏱️ TIMEOUT";
                bg     = new Color(1f, 0.85f, 0.2f, 0.35f);
            }
            else if (!r.Result.IsSolvable)
            {
                status = "❌ UNSOLVABLE";
                bg     = new Color(1f, 0.3f, 0.3f, 0.35f);
            }
            else if (r.Result.MinMoves > r.Par * 2)
            {
                status = "⚠️ HARD";
                bg     = new Color(1f, 0.65f, 0.1f, 0.35f);
            }
            else
            {
                status = "✅ SOLVABLE";
                bg     = new Color(0.25f, 0.85f, 0.35f, 0.25f);
            }

            var rect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rect, bg);
            CellLabel(r.LevelId.ToString(),         36);
            CellLabel(r.Tier.ToString(),             36);
            CellLabel(r.Colors.ToString(),           52);
            CellLabel(r.Depth.ToString(),            48);
            CellLabel(r.Par.ToString(),              36);
            CellLabel(r.Result.FailReason == "timeout" ? "—" : r.Result.MinMoves.ToString(), 52);
            CellLabel(status,                       120);
            EditorGUILayout.EndHorizontal();
        }

        private static void CellLabel(string text, float w)
            => EditorGUILayout.LabelField(text, GUILayout.Width(w));

        private void DrawSummary()
        {
            int solvable = 0, hard = 0, unsolvable = 0, timeout = 0;
            foreach (var r in _rows)
            {
                if (!r.Ran) continue;
                if (r.Result.FailReason == "timeout")        timeout++;
                else if (!r.Result.IsSolvable)               unsolvable++;
                else if (r.Result.MinMoves > r.Par * 2)      hard++;
                else                                         solvable++;
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Summary — ✅ {solvable}  ⚠️ {hard}  ❌ {unsolvable}  ⏱️ {timeout}  / {_rows.Count} total",
                EditorStyles.boldLabel);
        }

        // ── Solver run ────────────────────────────────────────────────────────────

        private void RunAll()
        {
            _rows.Clear();
            _statusMsg = string.Empty;

            string jsonPath = Application.dataPath + "/Resources/levels.json";
            string json;
            try   { json = File.ReadAllText(jsonPath); }
            catch (Exception ex) { _statusMsg = $"Cannot read levels.json: {ex.Message}"; return; }

            LevelCatalogue catalogue;
            try   { catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(json); }
            catch (Exception ex) { _statusMsg = $"JSON parse error: {ex.Message}"; return; }

            if (catalogue?.Levels == null || catalogue.Levels.Length == 0)
            {
                _statusMsg = "levels.json is empty or malformed.";
                return;
            }

            foreach (LevelRecord lvl in catalogue.Levels)
            {
                var result = LevelSolver.Solve(lvl);
                _rows.Add(new Row
                {
                    LevelId = lvl.LevelId,
                    Tier    = lvl.DifficultyTier,
                    Colors  = lvl.ColorCount,
                    Depth   = lvl.StackDepth,
                    Par     = lvl.ParMoves,
                    Ran     = true,
                    Result  = result,
                });
            }

            int ok = 0;
            foreach (var r in _rows)
                if (r.Result.IsSolvable && r.Result.FailReason != "timeout") ok++;
            _statusMsg = $"Done. {ok}/{_rows.Count} solvable. Open 'Export Report' to save.";
            Repaint();
        }

        // ── Report export ─────────────────────────────────────────────────────────

        private void ExportReport()
        {
            string dir  = Path.Combine(Application.dataPath, "../production/qa/evidence");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "solvability-2026-05-26.md");

            var sb = new StringBuilder();
            sb.AppendLine("# BoltSort — Level Solvability Report");
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine($"Solver: BFS, state limit {LevelSolver.DefaultStateLimit:N0}");
            sb.AppendLine();
            sb.AppendLine("| ID | Name | Tier | Colors | Depth | Par | BFSMin | Status |");
            sb.AppendLine("|----|------|------|--------|-------|-----|--------|--------|");

            foreach (var r in _rows)
            {
                string status = r.Result.FailReason == "timeout"   ? "⏱️ TIMEOUT"    :
                                !r.Result.IsSolvable               ? "❌ UNSOLVABLE"  :
                                r.Result.MinMoves > r.Par * 2      ? "⚠️ HARD"        :
                                                                     "✅ SOLVABLE";
                string bfsMin = r.Result.FailReason == "timeout"   ? "—" : r.Result.MinMoves.ToString();
                sb.AppendLine(
                    $"| {r.LevelId} | Level {r.LevelId} | {r.Tier} | {r.Colors} | {r.Depth} | {r.Par} | {bfsMin} | {status} |");
            }

            int solvable = 0, hard = 0, unsolvable = 0, timeout = 0;
            foreach (var r in _rows)
            {
                if (r.Result.FailReason == "timeout")       timeout++;
                else if (!r.Result.IsSolvable)              unsolvable++;
                else if (r.Result.MinMoves > r.Par * 2)     hard++;
                else                                        solvable++;
            }

            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- ✅ SOLVABLE: {solvable}");
            sb.AppendLine($"- ⚠️ HARD (MinMoves > Par×2): {hard}");
            sb.AppendLine($"- ❌ UNSOLVABLE: {unsolvable}");
            sb.AppendLine($"- ⏱️ TIMEOUT: {timeout}");
            sb.AppendLine($"- Total: {_rows.Count}");
            sb.AppendLine();
            sb.AppendLine("## Verdict");
            sb.AppendLine(unsolvable == 0 && timeout == 0
                ? "All levels are solvable. No data fixes required."
                : $"ACTION REQUIRED: {unsolvable} unsolvable, {timeout} timed out.");

            File.WriteAllText(path, sb.ToString());
            _statusMsg = $"Report saved → {path}";
            AssetDatabase.Refresh();
        }
    }
}
