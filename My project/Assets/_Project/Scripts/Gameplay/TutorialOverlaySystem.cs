using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BoltSort.Visual;
using BoltSort.LevelData;
using BoltSort.SortMechanic;
using GSM = BoltSort.GameStateManager.GameStateManager;
using SM  = BoltSort.SortMechanic.SortMechanic;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Full animated tutorial overlay for BoltSort. Shown automatically on any level
    /// whose <see cref="LevelRecord.IsTutorial"/> flag is true, as long as the player
    /// has not yet completed the tutorial (<c>bs.tutorial_complete</c> PlayerPrefs key).
    ///
    /// Rendered on a ScreenSpaceOverlay canvas at sortingOrder 155 (above board 0,
    /// below mechanic-tip overlay 150 which is re-ordered above this, below win 200).
    ///
    /// Tutorial flow (fully sequential):
    ///   Level 1: PickHint → PlaceHint → MismatchHint(A) → UndoHint(B) → FreePlay
    ///   Level 2: ExtraTubeHint → ResetHint(C) → CompleteTutorial
    /// </summary>
    public class TutorialOverlaySystem : MonoBehaviour
    {
        // ── Constants ──────────────────────────────────────────────────────────────
        private const string TutorialCompleteKey = "bs.tutorial_complete";
        private const float  HandBobAmplitude    = 6f;   // px bob in pick-hint mode
        private const float  HandBobPeriod       = 0.9f; // seconds per bob cycle
        private const float  FrameInterval       = 0.10f; // seconds per animation frame
        private const float  ExtraHintAutoDismiss = 3.5f; // auto-dismiss extra-tube hint
        private const float  MismatchMsgDuration  = 2.5f; // mismatch rule message auto-dismiss
        private const float  ExtraHandYOffset     = -120f; // 2.3: lower the extra-tube pulse hand

        // ── System refs ────────────────────────────────────────────────────────────
        private GSM         _gsm;
        private SM          _sm;
        private BoardView   _board;
        private HUDController _hud;

        // ── Tutorial state ─────────────────────────────────────────────────────────
        private enum Step { Inactive, PickHint, PlaceHint, MismatchHint, UndoHint, FreePlay, ExtraTubeHint, ResetHint }
        private Step _step = Step.Inactive;
        private int  _undoWatchMoveCount;   // MoveCount captured when UndoHint begins
        private bool _resetPending;         // ResetHint armed → next level load completes tutorial
        private Coroutine _mismatchMsgCo;

        // ── Sprites ────────────────────────────────────────────────────────────────
        private Sprite[] _framesIndicate;   // downward-pointing hand (Indicate_Down)
        private Sprite[] _framesPulse;
        private Sprite   _rightIcon;
        private Sprite   _wrongIcon;

        // ── Canvas & UI refs ───────────────────────────────────────────────────────
        private Canvas        _canvas;
        private RectTransform _canvasRT;
        private Image         _handImage;
        private RectTransform _handRT;
        private Text          _hintText;
        private readonly List<Image> _tubeIcons = new List<Image>();

        // ── Coroutines ─────────────────────────────────────────────────────────────
        private Coroutine _handFrameAnim;
        private Coroutine _handBobAnim;

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Wire up the tutorial overlay. Call after all other systems are initialized.
        /// If the tutorial is already complete this method returns immediately and the
        /// component stays dormant.
        /// </summary>
        public void Initialize(GSM gsm, SM sm, BoardView board, HUDController hud)
        {
            if (PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1)
                return; // tutorial already done — stay dormant

            _gsm   = gsm;
            _sm    = sm;
            _board = board;
            _hud   = hud;

            // 2.1: point with the downward-pointing hand. Fall back to the legacy
            // "Indicate" frames if the Indicate_Down folder is missing.
            _framesIndicate = GameAssets.TutorialHandFrames("Indicate_Down");
            if (_framesIndicate == null || _framesIndicate.Length == 0)
                _framesIndicate = GameAssets.TutorialHandFrames("Indicate");
            _framesPulse    = GameAssets.TutorialHandFrames("Pulse");
            _rightIcon      = GameAssets.TutorialRightIcon;
            _wrongIcon      = GameAssets.TutorialWrongIcon;

            BuildCanvas();

            gsm.OnLevelLoaded       += OnLevelLoaded;
            gsm.OnLevelComplete     += OnLevelComplete;
            gsm.OnBoardShapeChanged += OnBoardShapeChanged;
            sm.OnMoveCommitted      += OnMoveCommitted;
            sm.OnMoveRejected       += OnMoveRejected;
        }

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded       -= OnLevelLoaded;
                _gsm.OnLevelComplete     -= OnLevelComplete;
                _gsm.OnBoardShapeChanged -= OnBoardShapeChanged;
            }
            if (_sm != null)
            {
                _sm.OnMoveCommitted -= OnMoveCommitted;
                _sm.OnMoveRejected  -= OnMoveRejected;
            }
        }

        // ── GSM / SM event handlers ────────────────────────────────────────────────

        private void OnLevelLoaded(int levelId, int cc, int sd, int tsc, int tsd, long seqId)
        {
            // ResetHint armed: this load is the player restarting the level → finish tutorial.
            if (_resetPending)
            {
                _resetPending = false;
                CompleteTutorial(showFinal: true);
                return;
            }

            // Rebuild tube icon pool since column count may have changed.
            ClearTubeIcons();

            var lds = LevelDataSystem.Instance;
            if (lds == null || !lds.IsReady) { _step = Step.Inactive; HideAll(); return; }

            LevelRecord rec;
            try { rec = lds.GetLevel(levelId); }
            catch { _step = Step.Inactive; HideAll(); return; }

            if (!rec.IsTutorial) { _step = Step.Inactive; HideAll(); return; }

            if (levelId == 1)
            {
                TransitionTo(Step.PickHint);
            }
            else if (levelId == 2)
            {
                TransitionTo(Step.ExtraTubeHint);
            }
            else
            {
                _step = Step.Inactive;
                HideAll();
            }
        }

        private void OnMoveCommitted(int src, int dst, int colorId, long seqId)
        {
            // PlaceHint → MismatchHint(A) → UndoHint(B): each successful move advances.
            if (_step == Step.PlaceHint)
                TransitionTo(Step.MismatchHint);
            else if (_step == Step.MismatchHint)
                TransitionTo(Step.UndoHint);
        }

        private void OnMoveRejected(int src, int dst, int colorId, MoveRejectedReason reason)
        {
            // A: when the player tries to stack onto a different color, teach the rule.
            if (_step == Step.MismatchHint && reason == MoveRejectedReason.ColorMismatch)
                ShowMismatchMessage();
        }

        private void OnLevelComplete(int levelId, int moves, int par, long seqId)
        {
            // Level 2 finished by any means while the tutorial is still active → complete.
            if (levelId >= 2 && _step != Step.Inactive)
                CompleteTutorial(showFinal: true);
        }

        private void OnBoardShapeChanged()
        {
            if (_step == Step.ExtraTubeHint)
                TransitionTo(Step.ResetHint); // extra tube was used → advance to reset hint
        }

        // ── Update — detect BoltSelected (PlaceHint) and undo (UndoHint) ───────────

        private void Update()
        {
            if (_step == Step.PickHint && _sm != null
                && _sm.CurrentState == SortMechState.BoltSelected)
            {
                TransitionTo(Step.PlaceHint);
            }
            else if (_step == Step.UndoHint && _gsm != null
                     && _gsm.MoveCount < _undoWatchMoveCount)
            {
                // B: the player pressed undo → move count dropped. Praise then free play.
                StartCoroutine(PraiseThen(Step.FreePlay));
            }
        }

        // ── State transitions ──────────────────────────────────────────────────────

        private void TransitionTo(Step next)
        {
            StopHandAnims();
            StopMismatchMessage();
            HideAll();
            _step = next;

            switch (next)
            {
                case Step.PickHint:      StartCoroutine(ShowPickHint());      break;
                case Step.PlaceHint:     ShowPlaceHint();                     break;
                case Step.MismatchHint:  StartCoroutine(ShowMismatchHint());  break;
                case Step.UndoHint:      StartCoroutine(ShowUndoHint());      break;
                case Step.ExtraTubeHint: StartCoroutine(ShowExtraTubeHint()); break;
                case Step.ResetHint:     StartCoroutine(ShowResetHint());     break;
                case Step.FreePlay:
                    HideAll();
                    break;
            }
        }

        // ── Step implementations ───────────────────────────────────────────────────

        private IEnumerator ShowPickHint()
        {
            yield return null; // let BoardView settle its layout

            SetHintText("TAP A TUBE!");

            if (_board != null && _board.TubeCount > 0 && Camera.main != null)
            {
                Vector3 worldTop  = _board.GetTubeTopWorldPos(0);
                Vector2 screenPos = Camera.main.WorldToScreenPoint(worldTop);
                screenPos.y += 80f;                 // hover the hand above the tube top
                PlaceHand(screenPos, _framesIndicate);
                _handBobAnim = StartCoroutine(BobHand());
            }
        }

        private void ShowPlaceHint()
        {
            SetHintText("PLACE THE BALL!");
            RefreshPlaceIcons();
        }

        private IEnumerator ShowMismatchHint()
        {
            // A: prompt a second move; the rule message appears on a wrong-color tap.
            SetHintText("NOW TRY ANOTHER TUBE!");
            yield return null;
            RefreshPlaceIcons();
        }

        /// <summary>Shows the color-match rule message (auto-dismiss after a few seconds),
        /// then re-points the hand at a valid destination so the player can continue.</summary>
        private void ShowMismatchMessage()
        {
            StopMismatchMessage();
            _mismatchMsgCo = StartCoroutine(MismatchMessageRoutine());
        }

        private IEnumerator MismatchMessageRoutine()
        {
            SetHintText("You can only place a ball on top of the SAME COLOR!");

            // Point the hand at the first valid destination (Indicate_Down).
            int valid = FindValidDestination();
            if (valid >= 0 && _board != null && Camera.main != null)
            {
                Vector3 worldTop  = _board.GetTubeTopWorldPos(valid);
                Vector2 screenPos = Camera.main.WorldToScreenPoint(worldTop);
                screenPos.y += 80f;
                PlaceHand(screenPos, _framesIndicate);
                _handBobAnim = StartCoroutine(BobHand());
            }

            yield return new WaitForSeconds(MismatchMsgDuration);

            if (_step == Step.MismatchHint)
                SetHintText("NOW TRY ANOTHER TUBE!");
            _mismatchMsgCo = null;
        }

        private void StopMismatchMessage()
        {
            if (_mismatchMsgCo != null) { StopCoroutine(_mismatchMsgCo); _mismatchMsgCo = null; }
        }

        private IEnumerator ShowUndoHint()
        {
            SetHintText("Made a mistake? Use the lightning bolt to UNDO your last move!");
            _undoWatchMoveCount = _gsm != null ? _gsm.MoveCount : 0;
            yield return null; // let HUD finish building

            var rt = _hud != null ? _hud.UndoButtonRT : null;
            if (rt != null)
            {
                PlaceHand((Vector2)rt.position, _framesPulse);
                _handBobAnim = StartCoroutine(PulseHand());
            }
        }

        private IEnumerator ShowExtraTubeHint()
        {
            SetHintText("ADD A TUBE! +");
            yield return null; // wait for HUD to be built

            var rt = _hud != null ? _hud.ExtraTubeButtonRT : null;
            if (rt != null)
            {
                PlaceHand((Vector2)rt.position, _framesPulse, ExtraHandYOffset); // 2.3: lower the hand
                _handBobAnim = StartCoroutine(PulseHand());
            }

            // Auto-dismiss after a few seconds if the player doesn't use the button.
            yield return new WaitForSeconds(ExtraHintAutoDismiss);
            if (_step == Step.ExtraTubeHint)
                TransitionTo(Step.ResetHint);
        }

        private IEnumerator ShowResetHint()
        {
            SetHintText("Stuck? Tap here to restart the level anytime!");
            _resetPending = true;
            yield return null;

            var rt = _hud != null ? _hud.RetryButtonRT : null;
            if (rt != null)
            {
                PlaceHand((Vector2)rt.position, _framesPulse);
                _handBobAnim = StartCoroutine(PulseHand());
            }
        }

        /// <summary>Brief praise message, then advance to <paramref name="next"/>.</summary>
        private IEnumerator PraiseThen(Step next)
        {
            // Latch immediately so Update doesn't re-trigger this coroutine.
            _step = Step.FreePlay;
            StopHandAnims();
            if (_handImage != null) _handImage.enabled = false;
            SetHintText("GOOD!");
            yield return new WaitForSeconds(1.0f);
            if (next == Step.FreePlay) HideAll();
        }

        private void CompleteTutorial(bool showFinal = false)
        {
            PlayerPrefs.SetInt(TutorialCompleteKey, 1);
            PlayerPrefs.Save();
            _step = Step.Inactive;
            StopHandAnims();
            StopMismatchMessage();
            if (_handImage != null) _handImage.enabled = false;
            ClearTubeIcons();
            if (showFinal) StartCoroutine(FinalMessage());
            else           HideAll();
        }

        private IEnumerator FinalMessage()
        {
            SetHintText("GREAT!");
            yield return new WaitForSeconds(1.2f);
            HideAll();
        }

        // ── Place-hint icon helpers ────────────────────────────────────────────────

        private void RefreshPlaceIcons()
        {
            if (_gsm == null || _sm == null || _board == null) return;
            if (_sm.CurrentState != SortMechState.BoltSelected) return;

            int total = _board.TubeCount;
            EnsureTubeIcons(total);

            int   heldSrc   = _sm.HeldSourceIndex;
            int   heldColor = _sm.HeldColorId;

            for (int col = 0; col < total; col++)
            {
                bool valid = col != heldSrc && IsMoveValid(col, heldColor);
                Sprite icon = valid ? _rightIcon : _wrongIcon;
                var img = _tubeIcons[col];
                if (icon != null) { img.sprite = icon; img.color = Color.white; }
                else              { img.color = valid ? new Color(0.2f, 0.9f, 0.2f, 0.85f)
                                                       : new Color(0.9f, 0.2f, 0.2f, 0.85f); }
                img.enabled = true;

                Vector3 worldCenter = _board.GetTubeCenterWorldPos(col);
                Vector2 screenPos   = Camera.main.WorldToScreenPoint(worldCenter);
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRT, screenPos, null, out localPos);
                img.rectTransform.anchoredPosition = localPos;
            }
        }

        /// <summary>Index of the first tube the held ball can legally move into, or -1.</summary>
        private int FindValidDestination()
        {
            if (_gsm == null || _sm == null || _board == null) return -1;
            if (_sm.CurrentState != SortMechState.BoltSelected) return -1;
            int heldSrc = _sm.HeldSourceIndex, heldColor = _sm.HeldColorId;
            for (int col = 0; col < _board.TubeCount; col++)
                if (col != heldSrc && IsMoveValid(col, heldColor)) return col;
            return -1;
        }

        private bool IsMoveValid(int dst, int heldColor)
        {
            if (_gsm == null) return false;
            if (_gsm.IsTubeFrozen(dst)) return false;

            IReadOnlyList<int> dstStack;
            if (dst < _gsm.ColorCount)
            {
                var stacks = _gsm.StackContents;
                dstStack = (stacks != null && dst < stacks.Length) ? stacks[dst] : null;
            }
            else
            {
                int ti = dst - _gsm.ColorCount;
                var temps = _gsm.TempSlotContents;
                dstStack = (temps != null && ti < temps.Length) ? temps[ti] : null;
            }

            int cap = _gsm.GetColumnCapacity(dst);
            if (dstStack == null || dstStack.Count == 0) return true;   // empty → valid
            if (dstStack.Count >= cap)                   return false;  // full → invalid

            int top = dstStack[dstStack.Count - 1];
            // Color match: exact OR either is wildcard (multicolor = 0)
            return top == heldColor || top == 0 || heldColor == 0;
        }

        private void EnsureTubeIcons(int count)
        {
            while (_tubeIcons.Count < count)
            {
                var go  = new GameObject($"TubeIcon_{_tubeIcons.Count}");
                go.transform.SetParent(_canvasRT, false);
                var img = go.AddComponent<Image>();
                img.raycastTarget = false;
                img.enabled = false;
                var rt  = img.rectTransform;
                rt.sizeDelta = new Vector2(80f, 80f);
                _tubeIcons.Add(img);
            }
        }

        private void ClearTubeIcons()
        {
            foreach (var img in _tubeIcons)
                if (img != null) img.enabled = false;
        }

        // ── Hand placement & animation ─────────────────────────────────────────────

        private void PlaceHand(Vector2 screenPos, Sprite[] frames, float localYOffset = 0f)
        {
            if (_handRT == null) return;

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screenPos, null, out localPos);
            localPos.y += localYOffset;
            _handRT.anchoredPosition = localPos;
            _handImage.enabled = true;

            if (frames != null && frames.Length > 0)
            {
                _handImage.sprite = frames[0];
                _handImage.color  = Color.white;
                _handImage.preserveAspect = true;
                _handFrameAnim = StartCoroutine(AnimateFrames(frames));
            }
        }

        private IEnumerator AnimateFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) yield break;

            int idx = 0, dir = 1;
            while (true)
            {
                if (_handImage != null && idx < frames.Length && frames[idx] != null)
                    _handImage.sprite = frames[idx];
                yield return new WaitForSeconds(FrameInterval);

                idx += dir;
                if (idx >= frames.Length) { dir = -1; idx = Mathf.Max(0, frames.Length - 2); }
                else if (idx < 0)         { dir =  1; idx = Mathf.Min(1, frames.Length - 1); }
            }
        }

        // Gentle bob up/down for pointing hand (origin = current anchored position).
        private IEnumerator BobHand()
        {
            if (_handRT == null) yield break;
            Vector2 localOrigin = _handRT.anchoredPosition;
            float t = 0f;
            while (_handRT != null && _handImage != null && _handImage.enabled)
            {
                t += Time.deltaTime;
                float offset = Mathf.Sin(t / HandBobPeriod * Mathf.PI * 2f) * HandBobAmplitude;
                _handRT.anchoredPosition = localOrigin + Vector2.up * offset;
                yield return null;
            }
        }

        // Scale-pulse for undo/extra-tube/reset hint hand
        private IEnumerator PulseHand()
        {
            float t = 0f;
            while (_handRT != null && _handImage != null && _handImage.enabled)
            {
                t += Time.deltaTime;
                float s = 1f + 0.12f * Mathf.Sin(t / 0.7f * Mathf.PI * 2f);
                _handRT.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private void StopHandAnims()
        {
            if (_handFrameAnim != null) { StopCoroutine(_handFrameAnim); _handFrameAnim = null; }
            if (_handBobAnim   != null) { StopCoroutine(_handBobAnim);   _handBobAnim   = null; }
            if (_handRT != null) _handRT.localScale = Vector3.one;
        }

        // ── UI helpers ─────────────────────────────────────────────────────────────

        private void HideAll()
        {
            if (_handImage  != null) _handImage.enabled  = false;
            if (_hintText   != null) _hintText.enabled   = false;
            ClearTubeIcons();
        }

        private void SetHintText(string msg)
        {
            if (_hintText == null) return;
            _hintText.text    = msg;
            _hintText.enabled = true;
        }

        // ── Canvas construction ────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("TutorialOverlayCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 155;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.matchWidthOrHeight  = 1f;
            _canvasRT = canvasGO.GetComponent<RectTransform>();

            // 2.2: hint text — upper-middle of screen, clearly below the Level header.
            var textGO = new GameObject("HintText");
            textGO.transform.SetParent(canvasGO.transform, false);
            _hintText = textGO.AddComponent<Text>();
            _hintText.font      = GameAssets.MenuFont; // GummyPop
            _hintText.fontSize  = 44;
            _hintText.fontStyle = FontStyle.Bold;
            _hintText.alignment = TextAnchor.UpperCenter;
            _hintText.color     = Color.white;
            _hintText.supportRichText = false;
            _hintText.raycastTarget   = false;
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hintText.verticalOverflow   = VerticalWrapMode.Overflow;
            // White with black outline (per spec) + soft shadow.
            var ol = textGO.AddComponent<Outline>();
            ol.effectColor    = new Color(0f, 0f, 0f, 1f);
            ol.effectDistance = new Vector2(2f, -2f);
            var sh = textGO.AddComponent<Shadow>();
            sh.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            sh.effectDistance = new Vector2(1f, -1f);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.60f);
            textRT.anchorMax = new Vector2(1f, 0.70f);
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;
            _hintText.enabled = false;

            // Hand image — initially hidden, positioned dynamically
            var handGO = new GameObject("TutorialHand");
            handGO.transform.SetParent(canvasGO.transform, false);
            _handImage = handGO.AddComponent<Image>();
            _handImage.raycastTarget = false;
            _handImage.preserveAspect = true;
            _handImage.enabled = false;
            _handRT = handGO.GetComponent<RectTransform>();
            _handRT.sizeDelta  = new Vector2(120f, 120f);
            _handRT.anchorMin  = new Vector2(0.5f, 0.5f);
            _handRT.anchorMax  = new Vector2(0.5f, 0.5f);
            _handRT.pivot      = new Vector2(0.5f, 0f); // pivot at fingertip so hand points at target
        }
    }
}
