using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using BoltSort.Visual;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Canvas-based HUD for portrait mobile. All sizes reference 720×1280 logical pixels
    /// (ScaleWithScreenSize, match-height). Includes animated move counter, button
    /// press feedback, and a slide-in win overlay.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────────
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private bool   _levelComplete;
        private bool   _deadlock;
        private Action _onReset;
        private Action _onNextLevel;
        private Action _onUndo;
        private Action _onMenu;
        private Action _onReplay;
        private Action _onLevels;
        private Action _onShop;
        private Action _onExtraTube;

        // ── Header counters (Phase 2/3) ───────────────────────────────────────────
        /// <summary>Undo uses granted per level. The undo system itself is unlimited; this
        /// caps it. Reset on every level load/retry. Configurable.</summary>
        private const int MaxUndosPerLevel = 3;
        /// <summary>Extra-tube uses granted per level. Reset on every level load/retry. Configurable.</summary>
        private const int MaxExtraTubeUses = 3;

        private Text  _undoCountText;
        private Image _undoButtonImg;
        private int   _undoRemaining = MaxUndosPerLevel;

        // ── Thunder Undo effect (additive visual wrapper around the existing undo) ──
        /// <summary>Plays a thunder flourish then runs the existing undo at the impact frame.
        /// Null only if construction failed — in which case Undo runs plainly (failsafe).</summary>
        private ThunderUndoEffect _thunderEffect;
        /// <summary>True while the thunder effect is mid-play; blocks re-presses (no duplicate undo).</summary>
        private bool _thunderPlaying;
        private Text  _extraTubeCountText;
        private Image _extraTubeButtonImg;
        private int   _extraTubeRemaining = MaxExtraTubeUses;

        // ── Availability pulse (Fix 2) — subtle 1.0↔1.06 scale breathing while a
        // gameplay button is usable; stops & resets to 1.0 the instant it is not. ──
        private RectTransform _undoPulseRT;
        private RectTransform _extraPulseRT;

        /// <summary>Header undo button RectTransform — used by TutorialOverlaySystem for hand placement.</summary>
        public RectTransform UndoButtonRT      => _undoPulseRT;
        /// <summary>Header extra-tube button RectTransform — used by TutorialOverlaySystem for hand placement.</summary>
        public RectTransform ExtraTubeButtonRT => _extraPulseRT;
        private RectTransform _retryHeaderRT;
        /// <summary>Header retry/restart button RectTransform — used by TutorialOverlaySystem for hand placement.</summary>
        public RectTransform RetryButtonRT => _retryHeaderRT;
        private float _undoPressSuppressUntil;   // pause pulse briefly so it never fights the press bounce
        private float _extraPressSuppressUntil;
        private const float PulsePeriod   = 1.35f; // seconds per cycle (within 1.2–1.5s)
        private const float PulseMaxScale = 1.06f;

        // ── Live UI refs ──────────────────────────────────────────────────────────
        private Text        _levelText;
        private Text        _movesText;
        private RectTransform _movesRT;
        private Text        _deadlockText;
        private GameObject  _winOverlay;
        private RectTransform _winCardRT;
        private Text        _winMovesText;
        private Text        _moreLevelsText;
        private Image[]     _winStarImages;
        private Text        _winCoinsText;     // WIN-04
        private int         _winStarCount = 3; // WIN-01: set by GameBootstrap before HUD handler fires
        private int         _winCoins     = 0; // WIN-04
        private RectTransform[] _confettiPieces;
        private Image[]     _confettiImages;
        private float[]     _confettiSpeeds;
        private float[]     _confettiRotSpeeds;
        private float[]     _confettiBaseAlphas;
        private Sprite[]    _confettiSprites;
        private Coroutine   _confettiLoop;
        private Image         _trophyImg;
        private RectTransform _trophyRT;
        private RectTransform _winTitleRT;
        private RectTransform[] _winButtonRects;
        private Coroutine   _winIdleAnim;
        private Coroutine   _nextJuiceAnim;
        private RectTransform _nextShineRt;
        private Image         _nextShineImg;
        private SettingsPanel _settingsPanel;

        // ── Currency counters (Economy) — own strip below the header band, no overlap ──
        // Show OWNED balances (coins, gems) and OWNED power-up inventory (Extra Tube / Lightning
        // Bolt, cap 3). Distinct from the header Extra-Tube button, which grants per-level free
        // USES and is intentionally left untouched.
        private EconomyManager _economy;
        private Action _onEconomyChangedHandler;
        private Text _coinsCounter;
        private Text _gemsCounter;
        private Text _tubesCounter;
        private Text _boltsCounter;

        // Confetti rain layout, in the 720×1280 logical-pixel space (see class doc).
        private const int   ConfettiPieceCount = 30;
        private const float ConfettiHalfWidth   = 360f;  // 720 / 2
        private const float ConfettiHalfHeight  = 640f;  // 1280 / 2
        private const float ConfettiMargin      = 80f;   // spawn/despawn buffer beyond visible area
        private const float ConfettiFadeBand    = 140f;  // distance over which pieces fade in/out
        private const float ConfettiTopSpawnY    =  ConfettiHalfHeight + ConfettiMargin;
        private const float ConfettiBottomDespawnY = -ConfettiHalfHeight - ConfettiMargin;

        // ── Move counter animation ────────────────────────────────────────────────
        private int       _lastMoveCount = -1;
        private int       _currentLevelId;
        private Coroutine _movesAnimCoroutine;

        // ── Stored event handlers ─────────────────────────────────────────────────
        private Action<int, int, int, int, int, long> _onLevelLoadedHandler;
        private Action<int, int, int, long>           _onLevelCompleteHandler;

        // ─────────────────────────────────────────────────────────────────────────

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm,
            Action onReset,
            Action onNextLevel,
            Action onUndo     = null,
            Action onMenu     = null,
            Action onReplay   = null,
            Action onLevels   = null,
            Action onShop     = null,
            Action onExtraTube = null)
        {
            _gsm         = gsm;
            _onReset     = onReset;
            _onNextLevel = onNextLevel;
            _onUndo      = onUndo;
            _onMenu      = onMenu;
            _onReplay    = onReplay;
            _onLevels    = onLevels;
            _onShop      = onShop;
            _onExtraTube = onExtraTube;

            _onLevelLoadedHandler = (id, cc, sd, tsc, tsd, seqId) =>
            {
                _currentLevelId = id;
                if (_levelText  != null) _levelText.text = TrFmt("key_level_fmt", id);
                _levelComplete = false;
                _deadlock      = false;
                _lastMoveCount = 0;
                // Reset per-level header budgets (undo + extra tube) on load/retry.
                _undoRemaining      = MaxUndosPerLevel;
                _extraTubeRemaining = MaxExtraTubeUses;
                _thunderPlaying     = false; // clear in case a reload interrupted the effect
                RefreshUndoBadge();
                RefreshExtraTubeBadge();
                RefreshDeadlock();
                if (_winOverlay != null) _winOverlay.SetActive(false);
                if (_confettiLoop != null) { StopCoroutine(_confettiLoop); _confettiLoop = null; }
                if (_winIdleAnim != null) { StopCoroutine(_winIdleAnim); _winIdleAnim = null; }
                if (_nextJuiceAnim != null) { StopCoroutine(_nextJuiceAnim); _nextJuiceAnim = null; }
            };
            gsm.OnLevelLoaded += _onLevelLoadedHandler;

            _onLevelCompleteHandler = (id, moves, par, seqId) =>
            {
                _levelComplete = true;
                if (_winMovesText != null) _winMovesText.text = TrFmt("key_moves_fmt", moves);
                if (_winOverlay   != null) StartCoroutine(ShowWinOverlay(moves));
            };
            gsm.OnLevelComplete += _onLevelCompleteHandler;

            sm.OnDeadlockDetected += () => { _deadlock = true; RefreshDeadlock(); };

            BuildUI();
        }

        private void Update()
        {
            if (_gsm == null || _movesText == null) return;
            int cur = _gsm.MoveCount;
            if (cur != _lastMoveCount)
            {
                _movesText.text = TrFmt("key_moves_fmt", cur);
                if (_lastMoveCount >= 0 && _movesRT != null)
                {
                    if (_movesAnimCoroutine != null) StopCoroutine(_movesAnimCoroutine);
                    _movesAnimCoroutine = StartCoroutine(AnimateMoveCounter());
                }
                _lastMoveCount = cur;
            }
        }

        private IEnumerator AnimateMoveCounter()
        {
            if (_movesText != null) _movesText.color = BoltSortTheme.HUDAccent;
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                _movesRT, new Vector3(1.30f, 1.30f, 1f), 0.08f, TweenUtility.EaseOutBack));
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                _movesRT, Vector3.one, 0.10f, TweenUtility.EaseInOutQuad));
            if (_movesText != null)
                StartCoroutine(FadeTextColor(_movesText, BoltSortTheme.HUDAccent, Color.white, 0.20f));
        }

        private IEnumerator FadeTextColor(Text t, Color from, Color to, float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (t == null) yield break;
                elapsed += Time.deltaTime;
                t.color = Color.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            if (t != null) t.color = to;
        }

        private static void AddOutline(Text t, Color color, float distance)
        {
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor    = color;
            ol.effectDistance = new Vector2(distance, -distance);
        }

        /// <summary>Drives the confetti rain: each piece falls and spins independently
        /// at its own speed, fading in as it enters from above and fading out as it
        /// nears the bottom edge, then recycling back above the top once fully faded.
        /// A continuous, gap-free stream of individual pieces rather than a looping
        /// background image.</summary>
        private IEnumerator ConfettiLoop()
        {
            while (_winOverlay != null && _winOverlay.activeSelf)
            {
                for (int i = 0; i < _confettiPieces.Length; i++)
                {
                    var rt = _confettiPieces[i];
                    if (rt == null) continue;

                    var pos = rt.anchoredPosition;
                    pos.y -= _confettiSpeeds[i] * Time.deltaTime;

                    if (pos.y < ConfettiBottomDespawnY)
                    {
                        ResetConfettiPiece(i, spreadAcrossScreen: false);
                        continue;
                    }

                    rt.anchoredPosition = pos;
                    rt.Rotate(0f, 0f, _confettiRotSpeeds[i] * Time.deltaTime);

                    float fadeIn  = Mathf.Clamp01((ConfettiTopSpawnY - pos.y) / ConfettiFadeBand);
                    float fadeOut = Mathf.Clamp01((pos.y - ConfettiBottomDespawnY) / ConfettiFadeBand);
                    var c = _confettiImages[i].color;
                    c.a = Mathf.Min(fadeIn, fadeOut) * _confettiBaseAlphas[i];
                    _confettiImages[i].color = c;
                }
                yield return null;
            }
        }

        /// <summary>Randomizes one confetti piece's position, fall speed, spin and
        /// color. With <paramref name="spreadAcrossScreen"/> true (initial setup),
        /// the piece is placed at a random height across the whole fall range so the
        /// rain starts staggered instead of as one synchronized wave. Otherwise
        /// (recycling after falling off the bottom) it is placed just above the
        /// top edge to fall back in.</summary>
        private void ResetConfettiPiece(int i, bool spreadAcrossScreen)
        {
            var rt  = _confettiPieces[i];
            var img = _confettiImages[i];
            if (rt == null || img == null) return;

            // Pick a random real confetti shape from the sliced sheet. Size the piece to
            // the sprite's native aspect, normalized so its longest side is ~26 logical px,
            // then apply the random scale below. Keep the image tint white so the sprite's
            // own colors show through (no procedural recoloring).
            var sprites = _confettiSprites;
            if (sprites != null && sprites.Length > 0)
            {
                var spr = sprites[UnityEngine.Random.Range(0, sprites.Length)];
                img.sprite = spr;
                var nat = spr.rect.size;
                float longest = Mathf.Max(nat.x, nat.y, 1f);
                float k = 26f / longest;
                rt.sizeDelta = new Vector2(nat.x * k, nat.y * k);
            }

            float scale = UnityEngine.Random.Range(0.5f, 1.2f);
            rt.localScale    = new Vector3(scale, scale, 1f);
            rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            _confettiSpeeds[i]     = UnityEngine.Random.Range(150f, 320f);
            _confettiRotSpeeds[i]  = UnityEngine.Random.Range(-220f, 220f);
            _confettiBaseAlphas[i] = UnityEngine.Random.Range(0.85f, 1f);

            img.color = new Color(1f, 1f, 1f, 0f);

            float posY = spreadAcrossScreen
                ? UnityEngine.Random.Range(ConfettiBottomDespawnY, ConfettiTopSpawnY)
                : ConfettiTopSpawnY;
            rt.anchoredPosition = new Vector2(
                UnityEngine.Random.Range(-ConfettiHalfWidth, ConfettiHalfWidth), posY);
        }

        /// <summary>Slow continuous scale breathing for the win title while the overlay is shown.</summary>
        private IEnumerator IdlePulse(RectTransform rt, float minScale, float maxScale, float period)
        {
            while (_winOverlay != null && _winOverlay.activeSelf && rt != null)
            {
                float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f;
                float s = Mathf.Lerp(minScale, maxScale, t);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        /// <summary>Primary-action "juice" for the Next Level button: a persistent gentle
        /// scale pulse (1.0→1.05) combined with a ±2° sine-wave rotation, plus a bright
        /// inner-shine streak that sweeps across the button periodically.</summary>
        private IEnumerator NextLevelJuice(RectTransform rt)
        {
            if (rt == null) yield break;
            const float pulsePeriod = 1.1f, rotPeriod = 1.6f, shinePeriod = 2.6f, shineSweep = 0.55f;
            float shineTimer = 0f;
            while (_winOverlay != null && _winOverlay.activeSelf && rt != null)
            {
                float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / pulsePeriod)) + 1f) * 0.5f;
                float s = Mathf.Lerp(1f, 1.05f, t);
                rt.localScale    = new Vector3(s, s, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(Time.time * (2f * Mathf.PI / rotPeriod)) * 2f);

                if (_nextShineRt != null && _nextShineImg != null)
                {
                    shineTimer += Time.deltaTime;
                    float ph = shineTimer % shinePeriod;
                    if (ph < shineSweep)
                    {
                        float u     = ph / shineSweep;
                        float halfW = rt.rect.width * 0.5f + 60f;
                        _nextShineRt.anchoredPosition = new Vector2(Mathf.Lerp(-halfW, halfW, u), 0f);
                        // Fade in then out across the sweep for a soft glint.
                        float a = Mathf.Sin(u * Mathf.PI) * 0.35f;
                        _nextShineImg.color = new Color(1f, 1f, 1f, a);
                    }
                    else _nextShineImg.color = new Color(1f, 1f, 1f, 0f);
                }
                yield return null;
            }
            if (rt != null) { rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; }
        }

        /// <summary>Gentle scale pulse idle animation for earned stars.</summary>
        private IEnumerator StarTwinkle(Image star)
        {
            if (star == null) yield break;
            var rt = star.GetComponent<RectTransform>();
            if (rt == null) yield break;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.5f));
            while (_winOverlay != null && _winOverlay.activeSelf && rt != null)
            {
                float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / 1.5f)) + 1f) * 0.5f;
                float s = Mathf.Lerp(0.97f, 1.03f, t);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
        }

        private IEnumerator ButtonEntrance(RectTransform rt, float delay)
        {
            if (rt == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, Vector3.one, 0.22f, TweenUtility.EaseOutBack));
        }

        /// <summary>Brief white flash at win moment. D.2-C.</summary>
        private IEnumerator WinFlash()
        {
            var flashGO  = new GameObject("WinFlash");
            flashGO.transform.SetParent(_winOverlay.transform, false);
            var flashImg = flashGO.AddComponent<UnityEngine.UI.Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            var flashRt  = flashGO.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero; flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            flashImg.raycastTarget = false;

            // Ramp up to 65% white in 80ms
            float dur = 0.08f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                flashImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.65f, elapsed / dur));
                yield return null;
            }
            // Fade out to transparent in 220ms
            elapsed = 0f; dur = 0.22f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                flashImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.65f, 0f, elapsed / dur));
                yield return null;
            }
            Destroy(flashGO);
        }

        private IEnumerator ShowWinOverlay(int moves)
        {
            _winOverlay.SetActive(true);

            // Reset entrance state for trophy/title/buttons — the card itself is
            // still off-screen (slides in below) so this causes no visible pop.
            if (_trophyRT != null) _trophyRT.localScale = new Vector3(0.4f, 0.4f, 1f);
            if (_winTitleRT != null) _winTitleRT.localScale = Vector3.zero;
            if (_winButtonRects != null)
                foreach (var brt in _winButtonRects)
                    if (brt != null) brt.localScale = Vector3.zero;

            // D.2-C: brief white flash before card arrives
            StartCoroutine(WinFlash());

            // Continuous looping confetti rain — many independently falling pieces.
            if (_confettiPieces != null)
            {
                if (_confettiLoop != null) StopCoroutine(_confettiLoop);
                _confettiLoop = StartCoroutine(ConfettiLoop());
            }

            yield return new WaitForSeconds(0.06f);

            // Dim all stars first
            if (_winStarImages != null)
                foreach (var si in _winStarImages)
                    if (si != null) si.color = new Color(1f, 1f, 1f, 0.18f);

            // Trophy punch-scale entrance
            if (_trophyRT != null)
                StartCoroutine(TweenUtility.LerpRectScale(_trophyRT, Vector3.one, 0.40f, TweenUtility.EaseOutBack));

            // Title pop-in
            if (_winTitleRT != null)
                StartCoroutine(TweenUtility.LerpRectScale(_winTitleRT, Vector3.one, 0.30f, TweenUtility.EaseOutBack));

            // Slide card in from bottom
            if (_winCardRT != null)
            {
                Vector2 hiddenPos = new Vector2(0f, -1300f);
                Vector2 shownPos  = Vector2.zero;
                _winCardRT.anchoredPosition = hiddenPos;

                float dur = 0.30f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                    _winCardRT.anchoredPosition = Vector2.LerpUnclamped(hiddenPos, shownPos, t);
                    yield return null;
                }
                _winCardRT.anchoredPosition = shownPos;
            }

            // Title idle pulse begins once the card has settled
            if (_winTitleRT != null)
                _winIdleAnim = StartCoroutine(IdlePulse(_winTitleRT, 1f, 1.05f, 1.6f));

            // Button entrance: staggered scale-in
            if (_winButtonRects != null)
                for (int i = 0; i < _winButtonRects.Length; i++)
                    StartCoroutine(ButtonEntrance(_winButtonRects[i], 0.05f * i));

            // Next Level "juice": persistent pulse + gentle sine rotation + periodic shine sweep,
            // marking it as the primary action.
            if (_winButtonRects != null && _winButtonRects[1] != null)
            {
                if (_nextJuiceAnim != null) StopCoroutine(_nextJuiceAnim);
                _nextJuiceAnim = StartCoroutine(NextLevelJuice(_winButtonRects[1]));
            }

            // Count up moves text
            if (_winMovesText != null)
            {
                float dur = 0.20f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    int displayed = Mathf.RoundToInt(Mathf.Lerp(0f, moves, elapsed / dur));
                    _winMovesText.text = TrFmt("key_moves_fmt", displayed);
                    yield return null;
                }
                _winMovesText.text = TrFmt("key_moves_fmt", moves);
            }

            // Coin count-up + bounce (WIN-04)
            if (_winCoinsText != null && _winCoins > 0)
            {
                float dur = 0.35f, elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    int displayed = Mathf.RoundToInt(Mathf.Lerp(0f, _winCoins, elapsed / dur));
                    _winCoinsText.text = TrFmt("key_coins_fmt", displayed);
                    yield return null;
                }
                _winCoinsText.text = TrFmt("key_coins_fmt", _winCoins);

                var coinRT = _winCoinsText.GetComponent<RectTransform>();
                yield return StartCoroutine(TweenUtility.LerpRectScale(
                    coinRT, new Vector3(1.18f, 1.18f, 1f), 0.10f, TweenUtility.EaseOutBack));
                yield return StartCoroutine(TweenUtility.LerpRectScale(
                    coinRT, Vector3.one, 0.12f, TweenUtility.EaseInOutQuad));
            }
            else if (_winCoinsText != null)
            {
                _winCoinsText.text = "";
            }

            // Light up only earned stars (WIN-01) and twinkle them; leave unearned stars dim
            if (_winStarImages != null)
            {
                for (int i = 0; i < _winStarImages.Length; i++)
                {
                    var si = _winStarImages[i];
                    if (si == null) continue;
                    yield return new WaitForSeconds(0.18f);
                    if (i < _winStarCount)
                    {
                        si.color = Color.white;
                        var rt = si.GetComponent<RectTransform>();
                        if (rt != null) StartCoroutine(TweenUtility.LerpRectScale(
                            rt, new Vector3(1.3f, 1.3f, 1f), 0.08f, TweenUtility.EaseOutBack));
                        StartCoroutine(StarTwinkle(si));
                    }
                }
            }
        }

        private void RefreshDeadlock()
        {
            if (_deadlockText != null)
                _deadlockText.gameObject.SetActive(_deadlock && !_levelComplete);
        }

        public void ShowMoreLevelsSoon()
        {
            if (_moreLevelsText != null) _moreLevelsText.gameObject.SetActive(true);
        }

        // Called by GameBootstrap.HandleLevelComplete (subscribed in Awake) before this
        // component's own OnLevelComplete handler (subscribed in Initialize/Start) fires.
        public void SetWinResult(int stars, int coins)
        {
            _winStarCount = Mathf.Clamp(stars, 0, 3);
            _winCoins     = Mathf.Max(0, coins);
        }

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded   -= _onLevelLoadedHandler;
                _gsm.OnLevelComplete -= _onLevelCompleteHandler;
            }
            if (_economy != null && _onEconomyChangedHandler != null)
                _economy.OnBalanceChanged -= _onEconomyChangedHandler;
        }

        // ── Currency counters (Economy) ───────────────────────────────────────────

        /// <summary>Builds the four-counter currency strip (Coins, Gems, Extra Tubes, Lightning
        /// Bolts) anchored below the header + deadlock band so it never overlaps existing HUD
        /// elements, and live-binds it to <see cref="EconomyManager.OnBalanceChanged"/>.</summary>
        private void BuildCurrencyBar(GameObject canvasGO, Font font, float topBarH, float safeTop)
        {
            _economy = EconomyManager.Instance;

            var bar = MakePanel(canvasGO, "CurrencyBar", new Color(0f, 0f, 0f, 0f));
            bar.GetComponent<Image>().raycastTarget = false;
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            float top = -(topBarH + safeTop + 54f);          // start below the deadlock banner band
            rt.offsetMax = new Vector2(-12f, top);
            rt.offsetMin = new Vector2(12f, top - 52f);

            _coinsCounter = AddCurrencyCell(bar, font, 0.125f, new Color(1f, 0.82f, 0.20f, 1f)); // Coins
            _gemsCounter  = AddCurrencyCell(bar, font, 0.375f, new Color(0.40f, 0.85f, 1f, 1f)); // Gems
            _tubesCounter = AddCurrencyCell(bar, font, 0.625f, new Color(0.20f, 0.62f, 0.86f, 1f)); // Extra Tubes (owned)
            _boltsCounter = AddCurrencyCell(bar, font, 0.875f, new Color(1f, 0.85f, 0.20f, 1f)); // Lightning Bolts (owned)

            RefreshCurrency();
            if (_economy != null)
            {
                _onEconomyChangedHandler = RefreshCurrency;
                _economy.OnBalanceChanged += _onEconomyChangedHandler;
            }
        }

        /// <summary>One currency cell: a colored icon swatch + a number label, centered at
        /// <paramref name="anchorX"/> (a fraction of the bar width). Returns the number label.</summary>
        private Text AddCurrencyCell(GameObject parent, Font font, float anchorX, Color iconColor)
        {
            var cell = new GameObject("CurrencyCell");
            cell.transform.SetParent(parent.transform, false);
            var crt = cell.AddComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(anchorX, 0.5f);
            crt.pivot     = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta        = new Vector2(150f, 48f);

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(cell.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = iconColor;
            iconImg.raycastTarget = false;
            var irt = iconGO.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot     = new Vector2(0f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta        = new Vector2(34f, 34f);

            var t = MakeLabel(cell, "Num", "0", font, 28, TextAnchor.MiddleLeft, bold: true, shadow: true);
            t.color = Color.white;
            t.raycastTarget = false;
            AddOutline(t, new Color(0f, 0f, 0f, 0.9f), 1.5f);
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(42f, 0f); trt.offsetMax = new Vector2(0f, 0f);
            return t;
        }

        /// <summary>Pushes the current economy balances into the four counters. Safe with no economy.</summary>
        private void RefreshCurrency()
        {
            if (_economy == null) _economy = EconomyManager.Instance;
            if (_coinsCounter != null) _coinsCounter.text = (_economy != null ? _economy.Coins : 0).ToString();
            if (_gemsCounter  != null) _gemsCounter.text  = (_economy != null ? _economy.Gems : 0).ToString();
            if (_tubesCounter != null) _tubesCounter.text = (_economy != null ? _economy.ExtraTubes : 0).ToString();
            if (_boltsCounter != null) _boltsCounter.text = (_economy != null ? _economy.LightningBolts : 0).ToString();
        }

        private void OnSettingsClicked() => _settingsPanel?.Toggle();

        /// <summary>True when the settings overlay is currently visible. GP-02: queried by GameBootstrap.</summary>
        public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.IsOpen;

        /// <summary>Closes the settings panel if it is open. GP-02: called by GameBootstrap on back gesture.</summary>
        public void CloseSettings() => _settingsPanel?.Toggle();

        // ── Header undo / extra-tube buttons ──────────────────────────────────────

        /// <summary>Header Undo tap. Gated by a per-level budget (<see cref="MaxUndosPerLevel"/>);
        /// a tap is only consumed when an undo can actually be performed.</summary>
        private void OnUndoButtonTapped()
        {
            _undoPressSuppressUntil = Time.unscaledTime + 0.22f; // let the press bounce play uninterrupted
            if (_thunderPlaying) return;               // mid-effect — block re-press (no duplicate undo)
            if (_undoRemaining <= 0) return;
            if (_gsm != null && !_gsm.CanUndo) return; // nothing to revert — don't spend a use (no thunder)
            _undoRemaining--;
            RefreshUndoBadge();

            // Failsafe: if the effect couldn't be built, run the existing undo directly.
            if (_thunderEffect == null)
            {
                _onUndo?.Invoke();
                return;
            }

            // Play the thunder flourish, then run the EXISTING undo at the impact frame.
            _thunderPlaying = true;
            _thunderEffect.Play(
                onImpact:   () => _onUndo?.Invoke(),
                onComplete: () => _thunderPlaying = false);
        }

        /// <summary>Header Extra-Tube tap. Gated by a per-level budget
        /// (<see cref="MaxExtraTubeUses"/>). The mechanic itself is supplied via the
        /// <c>onExtraTube</c> callback (Phase 3); without it the button is inert.</summary>
        private void OnExtraTubeButtonTapped()
        {
            _extraPressSuppressUntil = Time.unscaledTime + 0.22f; // let the press bounce play uninterrupted
            if (_extraTubeRemaining <= 0) return;
            if (_onExtraTube == null) return;
            _extraTubeRemaining--;
            RefreshExtraTubeBadge();
            _onExtraTube.Invoke();
        }

        // ── Availability pulse (Fix 2) ────────────────────────────────────────────

        /// <summary>True when an undo can actually be performed right now (a move exists to
        /// revert and budget remains), so the Undo/Thunder button should pulse.</summary>
        private bool UndoPulseAvailable() =>
            !_levelComplete && !_thunderPlaying && _undoRemaining > 0 &&
            _gsm != null && _gsm.CanUndo;

        /// <summary>True when the player still has extra-tube uses, so the Add-tube button pulses.</summary>
        private bool ExtraTubePulseAvailable() =>
            !_levelComplete && _onExtraTube != null && _extraTubeRemaining > 0;

        /// <summary>Drives both buttons' subtle scale breathing. A single sine source keeps them
        /// in sync; each button pulses only while available and snaps back to 1.0 otherwise.
        /// Skips writing during the brief post-press window so it never fights the press bounce.</summary>
        private IEnumerator PulseButtons()
        {
            while (true)
            {
                float s = (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / PulsePeriod)) + 1f) * 0.5f; // 0..1
                float scale = Mathf.Lerp(1f, PulseMaxScale, s);
                ApplyPulse(_undoPulseRT,  UndoPulseAvailable(),      _undoPressSuppressUntil,  scale);
                ApplyPulse(_extraPulseRT, ExtraTubePulseAvailable(), _extraPressSuppressUntil, scale);
                yield return null;
            }
        }

        private static void ApplyPulse(RectTransform rt, bool available, float suppressUntil, float scale)
        {
            if (rt == null) return;
            if (Time.unscaledTime < suppressUntil) return;     // press bounce owns the scale right now
            rt.localScale = available ? new Vector3(scale, scale, 1f) : Vector3.one;
        }

        private void RefreshUndoBadge()
        {
            if (_undoCountText != null) _undoCountText.text = _undoRemaining.ToString();
            SetButtonDimmed(_undoButtonImg, _undoRemaining <= 0);
        }

        private void RefreshExtraTubeBadge()
        {
            if (_extraTubeCountText != null) _extraTubeCountText.text = _extraTubeRemaining.ToString();
            SetButtonDimmed(_extraTubeButtonImg, _extraTubeRemaining <= 0);
        }

        /// <summary>Greys a button (alpha) and disables interaction when its budget is spent.</summary>
        private static void SetButtonDimmed(Image img, bool dimmed)
        {
            if (img == null) return;
            var c = img.color; c.a = dimmed ? 0.40f : 1f; img.color = c;
            var btn = img.GetComponent<Button>();
            if (btn != null) btn.interactable = !dimmed;
        }

        /// <summary>Small count badge pinned to the top-right corner of a header button.
        /// No background panel — uses text outline for readability over any background.</summary>
        private Text AddCountBadge(GameObject buttonGO, Font font, string initial)
        {
            var badge = new GameObject("CountBadge");
            badge.transform.SetParent(buttonGO.transform, false);
            var brt = badge.GetComponent<RectTransform>();
            if (brt == null) brt = badge.AddComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot     = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-8f, -8f);
            brt.sizeDelta        = new Vector2(40f, 40f);

            var t = MakeLabel(badge, "Count", initial, font, 26,
                              TextAnchor.MiddleCenter, bold: true, shadow: false);
            t.color = Color.white;
            t.raycastTarget = false;
            AddOutline(t, new Color(0f, 0f, 0f, 1f), 1.5f);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>Anchors a header button at a horizontal fraction of the bar, bottom-pivoted
        /// at <paramref name="yBottom"/> so it sits clear of the safe-area notch.</summary>
        private static void PlaceHeaderButton(RectTransform rt, float anchorX, float yBottom, float size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yBottom);
            rt.sizeDelta = new Vector2(size, size);
        }

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                // Defensive: module self-assigns default UI actions in OnEnable too.
                esGO.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Font font = GameAssets.MenuFont; // Gummy display font (falls back to built-in)

            // Uniform canvas scale = Screen.width/720 (match-width below), so 1 physical px
            // = 720/Screen.width canvas units. Keeps notch insets pixel-accurate.
            float lpu        = 720f / Mathf.Max(1, Screen.width);
            float safeTop    = (Screen.height - Screen.safeArea.yMax) * lpu;
            float safeBottom = Screen.safeArea.yMin * lpu;

            // ── Root Canvas ──────────────────────────────────────────────────────
            var canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            // Match WIDTH (0): pin logical width to 720 so the HUD never clips on phones
            // narrower than 9:16. No-op at the 9:16 reference ratio.
            scaler.matchWidthOrHeight  = 0f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Thunder Undo effect — its own overlay canvas (sortingOrder 150) above this HUD.
            // Created once; null-safe so a build failure never breaks Undo.
            if (_thunderEffect == null)
                _thunderEffect = ThunderUndoEffect.Create(transform);

            // ── Header (redesign) ────────────────────────────────────────────────
            // L→R: [Settings][Retry] | Level X (+moves) | [Undo+count][ExtraTube+count]
            // The old semi-transparent panel + border line are removed: the bar is now a
            // fully transparent layout anchor. Height bumped to fit four buttons.
            const float topBarH = 140f;
            var topBar  = MakePanel(canvasGO, "TopBar", new Color(0f, 0f, 0f, 0f));
            topBar.GetComponent<Image>().raycastTarget = false;
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot     = new Vector2(0.5f, 1f);
            topRect.offsetMin = new Vector2(0f, -(topBarH + safeTop));
            topRect.offsetMax = new Vector2(0f, 0f);

            const float hBtn  = 84f;                  // header button size
            float       hBtnY = (topBarH - hBtn) * 0.5f; // vertical-center in the visible band

            // ── Left group: Settings, Retry (symmetric with the right group) ──────
            var settingsBtn = MakeIconButton(topBar, "SettingsButton", "⚙", font, 36,
                                             GameAssets.BtnMenuSettings ?? GameAssets.NavSettings, OnSettingsClicked,
                                             juice: true);
            if (GameAssets.BtnMenuSettings == null && GameAssets.NavSettings == null)
                settingsBtn.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f, 0.85f);
            PlaceHeaderButton(settingsBtn.GetComponent<RectTransform>(), 0.075f, hBtnY, hBtn);

            var retryBtn = MakeIconButton(topBar, "RetryHeaderButton", "↺", font, 38,
                                          GameAssets.BtnRestartLevel ?? GameAssets.BtnRetryAction, _onReset);
            PlaceHeaderButton(retryBtn.GetComponent<RectTransform>(), 0.225f, hBtnY, hBtn);
            _retryHeaderRT = retryBtn.GetComponent<RectTransform>();

            // ── Center: Level title + small moves line beneath ────────────────────
            _levelText = MakeLabel(topBar, "LevelText", "Level —", font, 42, // +10%
                                   TextAnchor.MiddleCenter, bold: true, shadow: true);
            _levelText.color = BoltSortTheme.HUDText;
            // 42pt exceeds the 54px box height → default Truncate hides the line. Overflow
            // guarantees the text renders (still centered on the buttons' center-line).
            _levelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _levelText.verticalOverflow   = VerticalWrapMode.Overflow;
            var lrt = _levelText.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot     = new Vector2(0.5f, 0.5f);
            // Vertically centered on the side buttons' center-line (hBtnY + hBtn/2).
            lrt.anchoredPosition = new Vector2(0f, hBtnY + hBtn * 0.5f);
            lrt.sizeDelta        = new Vector2(320f, 54f);
            // Live-refresh on language change while the HUD is visible behind Settings.
            LocalizedText.BindFormat(_levelText, "key_level_fmt", () => new object[] { _currentLevelId });

            _movesText = MakeLabel(topBar, "MovesText", "Moves: 0", font, 24,
                                   TextAnchor.MiddleCenter, bold: true, shadow: true);
            _movesText.color = BoltSortTheme.HUDText;
            var mrt = _movesText.GetComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f);
            mrt.pivot     = new Vector2(0.5f, 0f);
            mrt.anchoredPosition = new Vector2(0f, hBtnY - 30f); // clear the lowered Level label
            mrt.sizeDelta        = new Vector2(280f, 32f);
            _movesRT = _movesText.GetComponent<RectTransform>();
            LocalizedText.BindFormat(_movesText, "key_moves_fmt", () => new object[] { _gsm != null ? _gsm.MoveCount : 0 });

            // ── Right group: Undo (+count), Extra Tube (+count) ───────────────────
            var undoBtn = MakeIconButton(topBar, "UndoHeaderButton", "↩", font, 38,
                                         GameAssets.BtnUndoNew ?? GameAssets.BtnUndoAction, OnUndoButtonTapped,
                                         silentClick: true);
            _undoButtonImg = undoBtn.GetComponent<Image>();
            _undoCountText = AddCountBadge(undoBtn, font, MaxUndosPerLevel.ToString());
            PlaceHeaderButton(undoBtn.GetComponent<RectTransform>(), 0.775f, hBtnY, hBtn);
            _undoPulseRT = undoBtn.GetComponent<RectTransform>();

            var extraBtn = MakeIconButton(topBar, "ExtraTubeButton", "+", font, 48,
                                          GameAssets.BtnExtraTube, OnExtraTubeButtonTapped);
            _extraTubeButtonImg = extraBtn.GetComponent<Image>();
            if (GameAssets.BtnExtraTube == null)
                _extraTubeButtonImg.color = new Color(0.20f, 0.62f, 0.86f, 1f);
            _extraTubeCountText = AddCountBadge(extraBtn, font, _extraTubeRemaining.ToString());
            PlaceHeaderButton(extraBtn.GetComponent<RectTransform>(), 0.925f, hBtnY, hBtn);
            _extraPulseRT = extraBtn.GetComponent<RectTransform>();

            RefreshUndoBadge();
            RefreshExtraTubeBadge();
            StartCoroutine(PulseButtons());

            // ── Deadlock banner ──────────────────────────────────────────────────
            _deadlockText = MakeLabel(canvasGO, "DeadlockBanner",
                                      "No more moves! Tap Reset", font, 34,
                                      TextAnchor.MiddleCenter, bold: false, shadow: false);
            _deadlockText.color = new Color(0.95f, 0.24f, 0.24f, 1f);
            LocalizedText.Bind(_deadlockText, "key_deadlock");
            var dlRect = _deadlockText.GetComponent<RectTransform>();
            dlRect.anchorMin = new Vector2(0.1f, 1f); dlRect.anchorMax = new Vector2(0.9f, 1f);
            dlRect.pivot     = new Vector2(0.5f, 1f);
            dlRect.offsetMin = new Vector2(0f, -(topBarH + safeTop + 54f));
            dlRect.offsetMax = new Vector2(0f, -(topBarH + safeTop));
            _deadlockText.gameObject.SetActive(false);

            // Currency counter strip intentionally hidden — placeholder colored squares
            // are not player-facing. Economy logic lives in EconomyManager independently.

            // ── Bottom bar ───────────────────────────────────────────────────────
            const float botBarH = 140f;
            var bottomBar  = MakePanel(canvasGO, "BottomBar", BoltSortTheme.HUDBackground);
            var botRect    = bottomBar.GetComponent<RectTransform>();
            botRect.anchorMin = new Vector2(0f, 0f); botRect.anchorMax = new Vector2(1f, 0f);
            botRect.pivot     = new Vector2(0.5f, 0f);
            botRect.offsetMin = new Vector2(0f, 0f);
            botRect.offsetMax = new Vector2(0f, botBarH + safeBottom);

            float btnY = safeBottom + 22f;

            // Retry / Reset button (far bottom-left) — retry_button.png
            var resetBtn  = MakeIconButton(bottomBar, "ResetButton", "Reset", font, 30,
                                           GameAssets.BtnRetryAction, _onReset);
            var resetRect = resetBtn.GetComponent<RectTransform>();
            resetRect.anchorMin = resetRect.anchorMax = new Vector2(0.06f, 0f);
            resetRect.pivot     = new Vector2(0f, 0f);
            resetRect.anchoredPosition = new Vector2(0f, btnY);
            resetRect.sizeDelta        = new Vector2(90f, 90f);

            // Undo button (center) — undo_button.png
            var undoBtnBottom = MakeIconButton(bottomBar, "UndoButton", "↩", font, 42,
                                          GameAssets.BtnUndoAction, _onUndo);
            var undoRect = undoBtnBottom.GetComponent<RectTransform>();
            undoRect.anchorMin = undoRect.anchorMax = new Vector2(0.5f, 0f);
            undoRect.pivot     = new Vector2(0.5f, 0f);
            undoRect.anchoredPosition = new Vector2(0f, btnY);
            undoRect.sizeDelta        = new Vector2(90f, 90f);

            // Menu / Home button (far bottom-right) — home_button.png
            var menuBtn  = MakeIconButton(bottomBar, "MenuButton", "Menu", font, 30,
                                          GameAssets.BtnHomeAction, _onMenu);
            var menuRect = menuBtn.GetComponent<RectTransform>();
            menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.94f, 0f);
            menuRect.pivot     = new Vector2(1f, 0f);
            menuRect.anchoredPosition = new Vector2(0f, btnY);
            menuRect.sizeDelta        = new Vector2(90f, 90f);

            // Header redesign: the bottom button bar is hidden (not deleted). Its buttons'
            // actions (_onReset/_onUndo/_onMenu) are relocated to the header; the GameObject
            // and all wiring stay intact so it can be re-enabled if needed. The board layout
            // reclaims this space via BoardView.BottomReserveFrac.
            bottomBar.SetActive(false);

            // Settings panel host (the Settings button itself now lives in the header
            // left group above). New options "Go to Shop" / "Go to Levels" are injected.
            var spHost = new GameObject("SettingsPanelHost");
            spHost.transform.SetParent(canvasGO.transform, false);
            _settingsPanel = spHost.AddComponent<SettingsPanel>();
            _settingsPanel.Initialize(font, canvasGO.transform, _onShop, _onLevels);

            // ── Win overlay ──────────────────────────────────────────────────────
            // Layering (back→front): dim background (WinOverlay's own Image, full
            // screen) → looping confetti rain → card content (trophy/stars/text,
            // background fully transparent so confetti shows through) → buttons.
            _winOverlay = MakePanel(canvasGO, "WinOverlay", new Color(0f, 0f, 0f, 0.78f));
            var winRect = _winOverlay.GetComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero; winRect.anchorMax = Vector2.one;
            winRect.offsetMin = winRect.offsetMax = Vector2.zero;

            // Confetti rain — many small individually-falling pieces. Each piece is an
            // Image showing a random real confetti shape sliced from confetti_sheet.png
            // (white tint so the sprite's own colors show) that falls, spins and fades at
            // its own randomized rate, and recycles back above the top once it fades out at
            // the bottom — a continuous, pooled rain with no per-piece allocation after setup.
            _confettiPieces     = new RectTransform[ConfettiPieceCount];
            _confettiImages     = new Image[ConfettiPieceCount];
            _confettiSpeeds     = new float[ConfettiPieceCount];
            _confettiRotSpeeds  = new float[ConfettiPieceCount];
            _confettiBaseAlphas = new float[ConfettiPieceCount];
            _confettiSprites    = GameAssets.ConfettiPieces;

            var confettiContainer = MakePanel(_winOverlay, "ConfettiContainer", new Color(0f, 0f, 0f, 0f));
            confettiContainer.GetComponent<Image>().raycastTarget = false;
            var confettiContainerRt = confettiContainer.GetComponent<RectTransform>();
            confettiContainerRt.anchorMin = Vector2.zero; confettiContainerRt.anchorMax = Vector2.one;
            confettiContainerRt.offsetMin = confettiContainerRt.offsetMax = Vector2.zero;

            for (int i = 0; i < ConfettiPieceCount; i++)
            {
                var pieceGO = new GameObject($"ConfettiPiece_{i}");
                pieceGO.transform.SetParent(confettiContainer.transform, false);
                var pieceImg = pieceGO.AddComponent<Image>();
                pieceImg.raycastTarget = false;
                var pieceRt = pieceGO.GetComponent<RectTransform>();
                pieceRt.anchorMin = pieceRt.anchorMax = pieceRt.pivot = new Vector2(0.5f, 0.5f);

                _confettiPieces[i] = pieceRt;
                _confettiImages[i] = pieceImg;
                ResetConfettiPiece(i, spreadAcrossScreen: true);
            }

            // Win card — transparent layout container only; no background image,
            // so the confetti rain stays visible behind the trophy and text.
            var winCard  = MakePanel(_winOverlay, "WinCard", new Color(0f, 0f, 0f, 0f));
            winCard.GetComponent<Image>().raycastTarget = false;
            _winCardRT   = winCard.GetComponent<RectTransform>();
            _winCardRT.anchorMin        = new Vector2(0.5f, 0.5f);
            _winCardRT.anchorMax        = new Vector2(0.5f, 0.5f);
            _winCardRT.pivot            = new Vector2(0.5f, 0.5f);
            _winCardRT.anchoredPosition = Vector2.zero;
            _winCardRT.sizeDelta        = new Vector2(620f, 880f);

            // Title — top of the card
            var winTitle = MakeLabel(winCard, "WinTitle", "LEVEL COMPLETE!",
                                     font, 52, TextAnchor.MiddleCenter, bold: true, shadow: true);
            winTitle.color = BoltSortTheme.WinGold;
            LocalizedText.Bind(winTitle, "key_level_complete");
            AddOutline(winTitle, new Color(0f, 0f, 0f, 0.9f), 3f);
            _winTitleRT = winTitle.GetComponent<RectTransform>();
            _winTitleRT.anchorMin = _winTitleRT.anchorMax = new Vector2(0.5f, 1f);
            _winTitleRT.pivot     = new Vector2(0.5f, 1f);
            _winTitleRT.anchoredPosition = new Vector2(0f, -20f);
            _winTitleRT.sizeDelta        = new Vector2(560f, 80f);

            // 3 star icons below the title, above the trophy (no overlap)
            _winStarImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var starGO  = new GameObject($"WinStar_{i + 1}");
                starGO.transform.SetParent(winCard.transform, false);
                var starImg = starGO.AddComponent<Image>();
                Sprite spr  = GameAssets.StarLarge;
                if (spr != null) { starImg.sprite = spr; starImg.color = new Color(1f, 1f, 1f, 0.25f); starImg.preserveAspect = true; }
                else              { starImg.color = new Color(0f, 0f, 0f, 0f); }
                starImg.raycastTarget = false;
                var stRT = starGO.GetComponent<RectTransform>();
                stRT.anchorMin = stRT.anchorMax = new Vector2(0.5f, 1f);
                stRT.pivot     = new Vector2(0.5f, 1f);
                float offsetX = (i - 1) * 120f;
                stRT.anchoredPosition = new Vector2(offsetX, -110f);
                stRT.sizeDelta        = new Vector2(90f, 90f);
                _winStarImages[i]     = starImg;
            }

            // Trophy — restored to original position (previous lowering reverted)
            var trophyGO = new GameObject("Trophy");
            trophyGO.transform.SetParent(winCard.transform, false);
            _trophyImg = trophyGO.AddComponent<Image>();
            GameAssets.Apply(_trophyImg, GameAssets.Trophy, preserveAspect: true);
            if (GameAssets.Trophy == null) _trophyImg.color = new Color(0f, 0f, 0f, 0f);
            _trophyRT = trophyGO.GetComponent<RectTransform>();
            _trophyRT.anchorMin = _trophyRT.anchorMax = new Vector2(0.5f, 0.5f);
            _trophyRT.pivot     = new Vector2(0.5f, 0.5f);
            _trophyRT.anchoredPosition = new Vector2(0f, 100f);
            _trophyRT.sizeDelta        = new Vector2(260f, 260f);
            _trophyImg.raycastTarget = false;

            // Moves text — below the trophy
            _winMovesText = MakeLabel(winCard, "WinMoves", "Moves: 0",
                                      font, 38, TextAnchor.MiddleCenter, bold: false, shadow: true);
            _winMovesText.color = Color.white;
            var wmRect = _winMovesText.GetComponent<RectTransform>();
            wmRect.anchorMin = wmRect.anchorMax = new Vector2(0.5f, 1f);
            wmRect.pivot     = new Vector2(0.5f, 1f);
            wmRect.anchoredPosition = new Vector2(0f, -490f);
            wmRect.sizeDelta        = new Vector2(560f, 50f);

            // Coins earned label (WIN-04) — below moves, with outline for legibility
            // over the confetti rain
            _winCoinsText = MakeLabel(winCard, "WinCoins", "",
                                      font, 36, TextAnchor.MiddleCenter, bold: true, shadow: true);
            _winCoinsText.color = new Color(1f, 0.85f, 0.2f, 1f);
            AddOutline(_winCoinsText, new Color(0.25f, 0.12f, 0f, 0.9f), 2.5f);
            var wcRect = _winCoinsText.GetComponent<RectTransform>();
            wcRect.anchorMin = wcRect.anchorMax = new Vector2(0.5f, 1f);
            wcRect.pivot     = new Vector2(0.5f, 1f);
            wcRect.anchoredPosition = new Vector2(0f, -510f);
            wcRect.sizeDelta        = new Vector2(320f, 70f);

            // More-levels fallback — between the coins label and the button row
            _moreLevelsText = MakeLabel(winCard, "MoreLevels", "More levels coming soon!",
                                        font, 26, TextAnchor.MiddleCenter, bold: false, shadow: true);
            _moreLevelsText.color = new Color(1f, 0.95f, 0.7f, 1f);
            LocalizedText.Bind(_moreLevelsText, "key_more_levels");
            var mlRect = _moreLevelsText.GetComponent<RectTransform>();
            mlRect.anchorMin = mlRect.anchorMax = new Vector2(0.5f, 1f);
            mlRect.pivot     = new Vector2(0.5f, 1f);
            mlRect.anchoredPosition = new Vector2(0f, -640f);
            mlRect.sizeDelta        = new Vector2(560f, 70f);
            _moreLevelsText.gameObject.SetActive(false);

            // Buttons — Next Level (large, top) + Retry & Home (smaller, side-by-side below)
            _winButtonRects = new RectTransform[3];

            // Next Level button — hero action; uses general_button.png
            const float nextW = 540f, nextH = 150f;
            var nextBtn = MakeIconButton(winCard, "NextLevelButton", "", font, 34,
                                         GameAssets.MenuButton ?? GameAssets.VictoryNext, _onNextLevel);
            if (GameAssets.MenuButton == null && GameAssets.VictoryNext == null)
                nextBtn.GetComponent<Image>().color = BoltSortTheme.HUDAccent;
            // Add "Next Level" text label over the button sprite
            var nextLabelGO = new GameObject("Label");
            nextLabelGO.transform.SetParent(nextBtn.transform, false);
            var nextLabelT = nextLabelGO.AddComponent<Text>();
            nextLabelT.text = "Next Level"; nextLabelT.font = font; nextLabelT.fontSize = 40;
            LocalizedText.Bind(nextLabelT, "key_next_level");
            nextLabelT.fontStyle = FontStyle.Bold; nextLabelT.alignment = TextAnchor.MiddleCenter;
            nextLabelT.color = Color.white; nextLabelT.supportRichText = false;
            nextLabelT.raycastTarget = false;
            AddOutline(nextLabelT, new Color(0f, 0f, 0f, 0.8f), 2f);
            var nextLabelRt = nextLabelGO.GetComponent<RectTransform>();
            nextLabelRt.anchorMin = Vector2.zero; nextLabelRt.anchorMax = Vector2.one;
            nextLabelRt.offsetMin = nextLabelRt.offsetMax = Vector2.zero;
            var nbRect = nextBtn.GetComponent<RectTransform>();
            nbRect.anchorMin = nbRect.anchorMax = new Vector2(0.5f, 0f);
            nbRect.pivot     = new Vector2(0.5f, 0f);
            nbRect.anchoredPosition = new Vector2(0f, 128f);
            nbRect.sizeDelta        = new Vector2(nextW, nextH);
            _winButtonRects[1] = nbRect;

            // Inner-shine sweep: a tilted bright streak clipped to the button bounds,
            // swept across periodically by NextLevelJuice. Mask keeps it inside the button.
            nextBtn.AddComponent<RectMask2D>();
            var shineGO = new GameObject("Shine");
            shineGO.transform.SetParent(nextBtn.transform, false);
            _nextShineImg = shineGO.AddComponent<Image>();
            _nextShineImg.color = new Color(1f, 1f, 1f, 0f);
            _nextShineImg.raycastTarget = false;
            _nextShineRt = shineGO.GetComponent<RectTransform>();
            _nextShineRt.anchorMin = new Vector2(0.5f, 0f);
            _nextShineRt.anchorMax = new Vector2(0.5f, 1f);
            _nextShineRt.pivot     = new Vector2(0.5f, 0.5f);
            _nextShineRt.sizeDelta = new Vector2(64f, 80f); // extra height so the tilt covers corners
            _nextShineRt.localRotation = Quaternion.Euler(0f, 0f, 18f);

            // Replay / Retry button (bottom-left) — general_button.png + "Retry" text
            var replayBtn = MakeIconButton(winCard, "ReplayButton", "", font, 28,
                                           GameAssets.MenuButton, _onReplay ?? _onReset);
            if (GameAssets.MenuButton == null)
                replayBtn.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.36f, 1f);
            LocalizedText.Bind(AddTextLabel(replayBtn, "Retry", font, 30), "key_retry");
            var rpRect = replayBtn.GetComponent<RectTransform>();
            rpRect.anchorMin = rpRect.anchorMax = new Vector2(0.5f, 0f);
            rpRect.pivot     = new Vector2(0.5f, 0f);
            rpRect.anchoredPosition = new Vector2(-130f, 24f);
            rpRect.sizeDelta        = new Vector2(240f, 96f);
            _winButtonRects[0] = rpRect;

            // Home button (bottom-right) — general_button.png + "Home" text
            var homeBtn = MakeIconButton(winCard, "HomeButton", "", font, 28,
                                         GameAssets.MenuButton, _onMenu);
            if (GameAssets.MenuButton == null)
                homeBtn.GetComponent<Image>().color = new Color(0.22f, 0.36f, 0.22f, 1f);
            LocalizedText.Bind(AddTextLabel(homeBtn, "Home", font, 30), "key_home");
            var hbRect = homeBtn.GetComponent<RectTransform>();
            hbRect.anchorMin = hbRect.anchorMax = new Vector2(0.5f, 0f);
            hbRect.pivot     = new Vector2(0.5f, 0f);
            hbRect.anchoredPosition = new Vector2(130f, 24f);
            hbRect.sizeDelta        = new Vector2(240f, 96f);
            _winButtonRects[2] = hbRect;

            _winOverlay.SetActive(false);
        }

        // ── UI helpers ────────────────────────────────────────────────────────────

        /// <summary>Adds a centered text label as a direct child of a button GameObject.</summary>
        private static Text AddTextLabel(GameObject btnGO, string text, Font font, int fontSize)
        {
            var lgo = new GameObject("Label");
            lgo.transform.SetParent(btnGO.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false; t.raycastTarget = false;
            AddOutline(t, new Color(0f, 0f, 0f, 0.8f), 1.5f);
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>Localizes a key via the active <see cref="LocalizationManager"/> (key fallback if absent).</summary>
        private static string Tr(string key)
            => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key) : key;

        /// <summary>Localized format string filled with <paramref name="args"/>.</summary>
        private static string TrFmt(string key, params object[] args)
            => LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Format(key, args)
                : string.Format(key, args);

        private static GameObject MakePanel(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text MakeLabel(GameObject parent, string name, string text,
                                      Font font, int fontSize, TextAnchor anchor,
                                      bool bold, bool shadow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.font = font; t.fontSize = fontSize;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = anchor; t.color = Color.white; t.supportRichText = false;
            if (shadow)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.80f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private GameObject MakeAnimatedButton(GameObject parent, string name, string label,
                                              Font font, int fontSize, Action onClick)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = BoltSortTheme.HUDAccent;

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = BoltSortTheme.BrightnessMult(BoltSortTheme.HUDAccent, 1.25f);
            cs.pressedColor     = BoltSortTheme.BrightnessMult(BoltSortTheme.HUDAccent, 0.75f);
            btn.colors = cs;

            var rt = go.GetComponent<RectTransform>();
            btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));
            btn.onClick.AddListener(() =>
            {
                StartCoroutine(BounceButton(rt));
                onClick?.Invoke();
            });

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(go.transform, false);
            var t = lgo.AddComponent<Text>();
            t.text = label; t.font = font; t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white; t.supportRichText = false;
            var lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            return go;
        }

        private IEnumerator BounceButton(RectTransform rt)
        {
            if (rt == null) yield break;
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, new Vector3(0.90f, 0.90f, 1f), 0.07f, TweenUtility.EaseInQuad));
            yield return StartCoroutine(TweenUtility.LerpRectScale(
                rt, Vector3.one, 0.10f, TweenUtility.EaseOutBack));
        }

        // Icon-first button: uses sprite if available, falls back to labeled colored rect.
        private GameObject MakeIconButton(GameObject parent, string name, string fallbackLabel,
                                          Font font, int fontSize,
                                          Sprite icon, Action onClick, bool silentClick = false,
                                          bool juice = false)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();

            if (icon != null)
            {
                img.sprite         = icon;
                img.color          = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.color = BoltSortTheme.HUDAccent;
                if (!string.IsNullOrEmpty(fallbackLabel))
                {
                    var lgo = new GameObject("Label");
                    lgo.transform.SetParent(go.transform, false);
                    var t = lgo.AddComponent<Text>();
                    t.text = fallbackLabel; t.font = font; t.fontSize = fontSize;
                    t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
                    t.color = Color.white; t.supportRichText = false;
                    var lr = lgo.GetComponent<RectTransform>();
                    lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                    lr.offsetMin = lr.offsetMax = Vector2.zero;
                }
            }

            var btn = go.AddComponent<Button>();
            var cs  = btn.colors;
            cs.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cs.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = cs;

            var rt = go.GetComponent<RectTransform>();
            // Undo/Thunder button suppresses the generic click SFX (its own thunder SFX plays instead).
            if (!silentClick)
                btn.onClick.AddListener(() => AudioMgr.Instance?.PlaySFX("button_tap"));

            if (juice)
            {
                // Pointer down/up press feel + haptic via the reusable component. Use ONLY on
                // buttons without their own scale animation (idle pulse) — see ButtonJuice docs.
                go.AddComponent<ButtonJuice>();
                btn.onClick.AddListener(() => onClick?.Invoke());
            }
            else
            {
                btn.onClick.AddListener(() =>
                {
                    HapticManager.Light();
                    StartCoroutine(BounceButton(rt));
                    onClick?.Invoke();
                });
            }
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt,
                                       Vector2 anchorMin, Vector2 anchorMax,
                                       Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
    }
}
