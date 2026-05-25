using System;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Immediate-mode GUI overlay: level name, move counter, Reset button,
    /// and a Level-Complete panel with a Next Level button.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private string _levelName     = "";
        private bool   _levelComplete;
        private bool   _deadlock;
        private Action _onReset;
        private Action _onNextLevel;

        private GUIStyle _labelLarge;
        private GUIStyle _labelMed;
        private GUIStyle _labelWin;
        private GUIStyle _labelRed;
        private int      _cachedScreenH;

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm,
            Action onReset,
            Action onNextLevel)
        {
            _gsm         = gsm;
            _onReset     = onReset;
            _onNextLevel = onNextLevel;

            gsm.OnLevelLoaded += (id, cc, sd, tsc, tsd, seqId) =>
            {
                _levelName     = $"Level {id}";
                _levelComplete = false;
                _deadlock      = false;
            };

            gsm.OnLevelComplete   += (id, moves, par, seqId) => _levelComplete = true;
            sm.OnDeadlockDetected += () => _deadlock = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Screen.height / 720f;
            Rect  safe  = Screen.safeArea;
            float sL    = safe.xMin;
            float sT    = Screen.height - safe.yMax; // pixels from top
            float sB    = safe.yMin;                 // pixels from bottom (GUI y=0 is top)

            int w = Screen.width;
            int h = Screen.height;

            float pad  = 20f * scale;
            float labH = 50f * scale;
            int   moves = _gsm != null ? _gsm.MoveCount : 0;

            GUI.Label(new Rect(sL + pad, sT + pad, 350f * scale, labH), _levelName, _labelLarge);
            GUI.Label(new Rect(w - sL - 260f * scale - pad, sT + pad, 260f * scale, labH),
                      $"Moves: {moves}", _labelLarge);

            if (_deadlock && !_levelComplete)
            {
                float dW = 260f * scale;
                GUI.Label(new Rect((w - dW) * 0.5f, sT + pad + labH + 4f * scale, dW, 44f * scale),
                          "DEADLOCK — Reset!", _labelRed);
            }

            float btnH = 55f * scale;
            float btnW = 150f * scale;
            if (GUI.Button(new Rect(sL + pad, h - sB - btnH - pad, btnW, btnH), "Reset"))
                _onReset?.Invoke();

            if (_levelComplete)
            {
                float pw = 340f * scale, ph = 220f * scale;
                float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;
                GUI.Box(new Rect(px, py, pw, ph), "");
                GUI.Label(new Rect(px, py + 20f * scale, pw, 60f * scale), "Level Complete!", _labelWin);
                GUI.Label(new Rect(px, py + 90f * scale, pw, 44f * scale), $"Moves: {moves}", _labelMed);
                float nW = 180f * scale, nH = 55f * scale;
                if (GUI.Button(new Rect(px + (pw - nW) * 0.5f, py + ph - nH - 20f * scale, nW, nH), "Next Level"))
                    _onNextLevel?.Invoke();
            }
        }

        private void EnsureStyles()
        {
            if (_labelLarge != null && Screen.height == _cachedScreenH) return;
            _cachedScreenH = Screen.height;

            float scale = Screen.height / 720f;

            _labelLarge = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(26 * scale),
                fontStyle = FontStyle.Bold,
            };
            _labelLarge.normal.textColor = Color.white;

            _labelMed = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(22 * scale),
                alignment = TextAnchor.MiddleCenter,
            };
            _labelMed.normal.textColor = Color.white;

            _labelWin = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(34 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _labelWin.normal.textColor = Color.yellow;

            _labelRed = new GUIStyle(GUI.skin.label)
            {
                fontSize  = Mathf.RoundToInt(22 * scale),
                alignment = TextAnchor.MiddleCenter,
            };
            _labelRed.normal.textColor = new Color(1f, 0.3f, 0.3f);

            GUI.skin.button.fontSize = Mathf.RoundToInt(20 * scale);
        }
    }
}
