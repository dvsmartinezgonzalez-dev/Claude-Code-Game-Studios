using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BoltSort.Visual;
using BoltSort.SortMechanic;
using AudioMgr = BoltSort.Audio.AudioManager;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Renders the bolt-sorting board as coloured world-space sprites with full
    /// premium animations: marble-look bolts, arc movement, selection lift+bob,
    /// invalid-move shake, and win celebration.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        // ── Layout constants ──────────────────────────────────────────────────────
        private float _colWidth;
        private float _boltStep;
        private float _boardCenterY;
        /// <summary>Vertical distance between ball centers, per column — derived from each
        /// tube sprite's glass-region geometry (see <see cref="_tubeMetricsByTier"/>).</summary>
        private float[] _ballStep;
        /// <summary>Ball diameter (world units) per column — fits both tube interior width and slot height.</summary>
        private float[] _ballSize;
        /// <summary>Final visible tube width (world units) per column — used to re-normalize the
        /// selected/unselected sprite swap (the two PNGs have different native widths).</summary>
        private float[] _tubeWidth;
        /// <summary>Local Y of slot 0's ball CENTER, per column.</summary>
        private float[] _slot0CenterLocal;

        /// <summary>Per-column capacity (asymmetric-aware; mirrors GSM.GetColumnCapacity).</summary>
        private int[] _colCap;

        // ── Phase-2 frozen-tube overlay (per column) ──────────────────────────────
        private SpriteRenderer[] _frozenIcon;   // snowflake glyph, shown while frozen
        private TextMesh[]       _frozenLabel;  // remaining-turn counter
        private static Sprite    _snowSprite;

        // ── System references ─────────────────────────────────────────────────────
        private BoltSort.GameStateManager.GameStateManager _gsm;
        private BoltSort.SortMechanic.SortMechanic         _sortMechanic;

        // ── Level state ───────────────────────────────────────────────────────────
        private int _colorCount;
        private int _stackDepth;
        private int _tempSlotCount;
        private int _tempSlotDepth;

        // ── Renderer arrays [col][slot] ───────────────────────────────────────────
        private SpriteRenderer[][] _boltRenderers;
        private SpriteRenderer[][] _shineRenderers;

        // ── Column transforms and glow refs ──────────────────────────────────────
        private Transform[]      _columnTransforms;
        private SpriteRenderer[] _columnBgRenderers;
        private SpriteRenderer[] _columnGlowRenderers;
        private SpriteRenderer[] _tubeRenderers;      // tube body sprite per column (swaps selected/unselected)
        private Vector3[]        _columnSlot0World;   // world-space CENTER of slot 0's ball per column
        private Vector3[]        _columnTargetLocal;  // settled local position per column (multi-row layout)

        // ── Selection animation state ─────────────────────────────────────────────
        private float        _selYOffset   = 0f;
        private float        _selScale     = 1f;
        private float        _bobTimer     = 0f;
        private bool         _selLifted    = false;
        private Coroutine    _selCoroutine;
        private SortMechState _prevFsmState = SortMechState.Idle;

        // ── Move animation state ──────────────────────────────────────────────────
        private GameObject   _moveGhost;
        private int          _hideDstCol  = -1;
        private int          _hideDstSlot = -1;

        // ── Win state ─────────────────────────────────────────────────────────────
        private bool     _winPlaying;
        private Coroutine _winCoroutine;

        // ── Multicolor ball animation ──────────────────────────────────────────────
        private const float MulticolorFps = 8f; // frames per second

        // ── Shared sprites ────────────────────────────────────────────────────────
        private static Sprite _shineSprite;
        private static Sprite _shadowSprite;
        private static Sprite _glowSprite;
        private static Sprite _particleCircle;

        // ─────────────────────────────────────────────────────────────────────────

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm)
        {
            _gsm          = gsm;
            _sortMechanic = sm;

            _shineSprite    = _shineSprite    ?? CreateShineSprite();
            _shadowSprite   = _shadowSprite   ?? CreateShadowSprite();
            _glowSprite     = _glowSprite     ?? CreateGlowSprite();
            _snowSprite     = _snowSprite     ?? CreateSnowSprite();
            _particleCircle = _particleCircle ?? CreateParticleCircleSprite();

            gsm.OnLevelLoaded    += OnLevelLoaded;
            gsm.OnLevelComplete  += OnLevelComplete;
            gsm.OnMysteryRevealed += OnMysteryRevealed;
            sm.OnMoveCommitted   += OnMoveCommitted;
            sm.OnMoveRejected    += OnMoveRejected;

            SceneTransitionManager.OnTransitionOut += OnTransitionOut;
        }

        private void OnDestroy()
        {
            if (_gsm != null)
            {
                _gsm.OnLevelLoaded   -= OnLevelLoaded;
                _gsm.OnLevelComplete -= OnLevelComplete;
                _gsm.OnMysteryRevealed -= OnMysteryRevealed;
            }
            SceneTransitionManager.OnTransitionOut -= OnTransitionOut;
        }

        private void OnLevelLoaded(int levelId, int colorCount, int stackDepth,
                                   int tempSlotCount, int tempSlotDepth, long seqId)
        {
            _colorCount    = colorCount;
            _stackDepth    = stackDepth;
            _tempSlotCount = tempSlotCount;
            _tempSlotDepth = tempSlotDepth;

            _selYOffset  = 0f;
            _selScale    = 1f;
            _selLifted   = false;
            _hideDstCol  = -1;
            _hideDstSlot = -1;
            _winPlaying  = false;

            if (_moveGhost != null) { Destroy(_moveGhost); _moveGhost = null; }

            RebuildColumns();
        }

        private void OnMoveCommitted(int src, int dst, int colorId, long seqId)
        {
            _selYOffset = 0f;
            _selScale   = 1f;
            _selLifted  = false;
            if (_selCoroutine != null) { StopCoroutine(_selCoroutine); _selCoroutine = null; }
            StartCoroutine(AnimateBoltMove(src, dst, colorId, seqId));
        }

        private void OnMoveRejected(int src, int dst, int colorId, MoveRejectedReason reason)
        {
            _selYOffset = 0f;
            _selScale   = 1f;
            _selLifted  = false;
            if (_selCoroutine != null) { StopCoroutine(_selCoroutine); _selCoroutine = null; }
            StartCoroutine(AnimateInvalidShake(src));
        }

        private void OnLevelComplete(int levelId, int moves, int par, long seqId)
        {
            if (_winPlaying) return;
            _winPlaying  = true;
            if (_winCoroutine != null) StopCoroutine(_winCoroutine);
            _winCoroutine = StartCoroutine(PlayWinCelebration());
        }

        private void OnTransitionOut()
        {
            StartCoroutine(AnimateColumnsOut());
        }

        // ── FSM state polling ─────────────────────────────────────────────────────

        private void Update()
        {
            if (_sortMechanic == null) return;

            SortMechState cur = _sortMechanic.CurrentState;
            if (cur != _prevFsmState)
            {
                if (cur == SortMechState.BoltSelected)
                {
                    AudioMgr.Instance?.PlaySFX("bolt_pick");
                    if (_selCoroutine != null) StopCoroutine(_selCoroutine);
                    _selCoroutine = StartCoroutine(AnimateLift());
                    // D.2-E: column squish pop on pickup
                    int src = _sortMechanic.HeldSourceIndex;
                    if (_columnTransforms != null && src >= 0 && src < _columnTransforms.Length)
                        StartCoroutine(ColumnPickupPop(_columnTransforms[src]));
                }
                else if (_prevFsmState == SortMechState.BoltSelected &&
                         cur != SortMechState.MoveExecuting)
                {
                    // Cancelled or invalid — return bolt
                    if (_selCoroutine != null) StopCoroutine(_selCoroutine);
                    _selCoroutine = StartCoroutine(AnimateReturn());
                }
                _prevFsmState = cur;
            }

            if (_selLifted) _bobTimer += Time.deltaTime;
        }

        private IEnumerator AnimateLift()
        {
            float startY = _selYOffset, startS = _selScale;
            float dur = 0.08f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t  = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                _selYOffset = Mathf.LerpUnclamped(startY, 0.4f, t);
                _selScale   = Mathf.LerpUnclamped(startS, 1.15f, t);
                yield return null;
            }
            _selYOffset = 0.4f;
            _selScale   = 1.15f;
            _selLifted  = true;
            _bobTimer   = 0f;
        }

        private IEnumerator AnimateReturn()
        {
            float startY = _selYOffset, startS = _selScale;
            float dur = 0.12f, elapsed = 0f;
            _selLifted = false;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t  = TweenUtility.EaseInOutQuad(Mathf.Clamp01(elapsed / dur));
                _selYOffset = Mathf.LerpUnclamped(startY, 0f, t);
                _selScale   = Mathf.LerpUnclamped(startS, 1f, t);
                yield return null;
            }
            _selYOffset = 0f;
            _selScale   = 1f;
        }

        // ── Column scale pop animations ───────────────────────────────────────────

        /// <summary>Squish-and-spring when a bolt is picked up from a column. D.2-E.</summary>
        private IEnumerator ColumnPickupPop(Transform colTr)
        {
            if (colTr == null) yield break;
            Vector3 orig   = colTr.localScale;
            Vector3 squish = new Vector3(orig.x * 1.08f, orig.y * 0.94f, orig.z);
            float dur = 0.06f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                colTr.localScale = Vector3.LerpUnclamped(orig, squish, elapsed / dur);
                yield return null;
            }
            elapsed = 0f; dur = 0.10f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / dur));
                colTr.localScale = Vector3.LerpUnclamped(squish, orig, t);
                yield return null;
            }
            if (colTr != null) colTr.localScale = orig;
        }

        /// <summary>Squish-and-bounce when a bolt lands in a column. D.2-F.</summary>
        private IEnumerator ColumnLandPop(Transform colTr)
        {
            if (colTr == null) yield break;
            Vector3 orig   = colTr.localScale;
            Vector3 squish = new Vector3(orig.x * 1.06f, orig.y * 0.95f, orig.z);
            float dur = 0.05f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                colTr.localScale = Vector3.LerpUnclamped(orig, squish, elapsed / dur);
                yield return null;
            }
            elapsed = 0f; dur = 0.12f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutBounce(Mathf.Clamp01(elapsed / dur));
                colTr.localScale = Vector3.LerpUnclamped(squish, orig, t);
                yield return null;
            }
            if (colTr != null) colTr.localScale = orig;
        }

        // ── Arc movement animation ────────────────────────────────────────────────

        private IEnumerator AnimateBoltMove(int src, int dst, int colorId, long seqId)
        {
            if (_columnSlot0World == null || src >= _columnSlot0World.Length ||
                dst >= _columnSlot0World.Length)
            {
                _sortMechanic.OnAnimationComplete(seqId);
                yield break;
            }

            // Compute positions AFTER GSM mutation
            var stacks = _gsm.StackContents;
            var temps  = _gsm.TempSlotContents;
            int totalCols = _colorCount + _tempSlotCount;

            IReadOnlyList<int> dstCol = dst < _colorCount
                ? (stacks != null && dst < stacks.Length ? stacks[dst] : null)
                : (temps  != null && (dst - _colorCount) < temps.Length
                       ? temps[dst - _colorCount] : null);

            int dstSlot = dstCol != null ? dstCol.Count - 1 : 0;

            // Ball diameter in world units (matches a settled ball in the source column).
            float boltSize = (_ballSize != null && src < _ballSize.Length) ? _ballSize[src] : _ballStep[src];

            // src slot: GSM already removed the bolt — slot count now is correct
            IReadOnlyList<int> srcColAfter = src < _colorCount
                ? (stacks != null && src < stacks.Length ? stacks[src] : null)
                : (temps  != null && (src - _colorCount) < temps.Length
                       ? temps[src - _colorCount] : null);
            int srcSlot = srcColAfter != null ? srcColAfter.Count : 0;
            Vector3 srcWorld = _columnSlot0World[src] + Vector3.up * (srcSlot * _ballStep[src]);

            Vector3 dstWorld = _columnSlot0World[dst] + Vector3.up * (dstSlot * _ballStep[dst]);

            // Hide the destination slot while ghost is in the air
            _hideDstCol  = dst;
            _hideDstSlot = dstSlot;

            // Create ghost bolt — convert the target world diameter to a local scale using
            // the ball sprite's native size (independent of import PPU).
            Sprite ghostSpr   = GameAssets.BallSpriteForToken(colorId);
            float  ghostNative = ghostSpr != null ? ghostSpr.bounds.size.x : 1f;
            float  ghostScale  = boltSize / Mathf.Max(0.0001f, ghostNative);

            _moveGhost = new GameObject("MoveGhost");
            _moveGhost.transform.position   = srcWorld;
            _moveGhost.transform.localScale = new Vector3(ghostScale, ghostScale, 1f);

            var shadowGO = new GameObject("GhostShadow");
            shadowGO.transform.SetParent(_moveGhost.transform, false);
            shadowGO.transform.localPosition = new Vector3(0.04f, -0.05f, 0f);
            shadowGO.transform.localScale    = new Vector3(1.1f, 0.6f, 1f);
            var shadowSr = shadowGO.AddComponent<SpriteRenderer>();
            shadowSr.sprite       = _shadowSprite;
            shadowSr.color        = BoltSortTheme.BoltShadow;
            shadowSr.sortingOrder = 8;

            var ghostSr = _moveGhost.AddComponent<SpriteRenderer>();
            ghostSr.sprite       = ghostSpr;
            ghostSr.color        = Color.white;
            ghostSr.sortingOrder = 9;

            var shineGO = new GameObject("GhostShine");
            shineGO.transform.SetParent(_moveGhost.transform, false);
            shineGO.transform.localPosition = new Vector3(-0.20f, 0.22f, 0f);
            shineGO.transform.localScale    = new Vector3(0.38f, 0.32f, 1f);
            var shineSr = shineGO.AddComponent<SpriteRenderer>();
            shineSr.sprite       = _shineSprite;
            shineSr.color        = new Color(1f, 1f, 1f, 0.60f);
            shineSr.sortingOrder = 10;

            // Arc trajectory — Phase 1: rise (0–100ms), Phase 2: fall (100–220ms)
            float peakY  = Mathf.Max(srcWorld.y, dstWorld.y) + 1.5f;
            float arc1   = 0.10f; // rise duration
            float arc2   = 0.12f; // fall duration

            // Phase 1: rise
            float elapsed = 0f;
            while (elapsed < arc1)
            {
                if (_moveGhost == null) yield break;
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / arc1);
                float x  = Mathf.Lerp(srcWorld.x, dstWorld.x, t * 0.5f);
                float y  = Mathf.Lerp(srcWorld.y, peakY, TweenUtility.EaseOutQuad(t));
                _moveGhost.transform.position = new Vector3(x, y, srcWorld.z);
                yield return null;
            }

            // Phase 2: fall
            elapsed = 0f;
            while (elapsed < arc2)
            {
                if (_moveGhost == null) yield break;
                elapsed += Time.deltaTime;
                float t  = Mathf.Clamp01(elapsed / arc2);
                float x  = Mathf.Lerp(srcWorld.x, dstWorld.x, 0.5f + t * 0.5f);
                float y  = Mathf.Lerp(peakY, dstWorld.y, TweenUtility.EaseInQuad(t));
                _moveGhost.transform.position = new Vector3(x, y, srcWorld.z);
                yield return null;
            }

            // Play landing sound
            AudioMgr.Instance?.PlaySFX("bolt_place");

            // Landing squish: scale X*1.2 Y*0.85 then spring back (EaseOutElastic, 80ms)
            SpawnLandingDust(dstWorld, BoltSortTheme.BoltColorForId(colorId));
            if (dst >= 0 && dst < _columnTransforms.Length && _columnTransforms[dst] != null)
                StartCoroutine(ColumnLandPop(_columnTransforms[dst]));

            float squishDur = 0.08f;
            elapsed = 0f;
            while (elapsed < squishDur)
            {
                if (_moveGhost == null) yield break;
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutElastic(Mathf.Clamp01(elapsed / squishDur));
                // squish is the "going in" motion (scale toward squished) then spring
                float sx = Mathf.LerpUnclamped(ghostScale * 1.20f, ghostScale, t);
                float sy = Mathf.LerpUnclamped(ghostScale * 0.85f, ghostScale, t);
                _moveGhost.transform.localScale = new Vector3(sx, sy, 1f);
                _moveGhost.transform.position   = dstWorld;
                yield return null;
            }

            // Cleanup
            if (_moveGhost != null) { Destroy(_moveGhost); _moveGhost = null; }
            _hideDstCol  = -1;
            _hideDstSlot = -1;

            _sortMechanic.OnAnimationComplete(seqId);
        }

        private void SpawnLandingDust(Vector3 worldPos, Color boltColor)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                StartCoroutine(DustParticle(worldPos, angle, boltColor));
            }
        }

        private IEnumerator DustParticle(Vector3 origin, float angle, Color color)
        {
            var go = new GameObject("Dust");
            go.transform.position   = origin;
            go.transform.localScale = Vector3.one * 0.12f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _particleCircle;
            sr.color        = new Color(1f, 1f, 1f, 0.6f);
            sr.sortingOrder = 11;

            float   rad = Mathf.Deg2Rad * angle;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * 0.6f;
            float   dur = 0.20f, elapsed = 0f;

            while (elapsed < dur)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                go.transform.position = origin + dir * TweenUtility.EaseOutQuad(t);
                sr.color = new Color(1f, 1f, 1f, 0.6f * (1f - t));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ── Invalid move shake ────────────────────────────────────────────────────

        private IEnumerator AnimateInvalidShake(int col)
        {
            if (_columnTransforms == null || col < 0 || col >= _columnTransforms.Length)
            {
                _sortMechanic.OnRejectionAnimationComplete();
                yield break;
            }

            Transform colTr    = _columnTransforms[col];
            Vector3   basePos  = colTr.localPosition;
            float     px       = 0.20f; // world-unit shake amplitude

            // Flash held bolt red
            IReadOnlyList<int> colData = col < _colorCount
                ? (_gsm.StackContents != null && col < _gsm.StackContents.Length
                       ? _gsm.StackContents[col] : null)
                : (_gsm.TempSlotContents != null && (col - _colorCount) < _gsm.TempSlotContents.Length
                       ? _gsm.TempSlotContents[col - _colorCount] : null);
            SpriteRenderer heldSr = null;
            Color          origColor = Color.white;
            if (colData != null && colData.Count > 0 && _boltRenderers != null &&
                col < _boltRenderers.Length)
            {
                int topSlot = colData.Count - 1;
                heldSr   = _boltRenderers[col][topSlot];
                origColor = heldSr?.color ?? Color.white;
                if (heldSr != null) heldSr.color = new Color(1f, 0.15f, 0.15f, 1f);
            }

            // Shake frames
            (float dx, float dur)[] frames =
            {
                ( px,  0.04f),
                (-px,  0.04f),
                ( px * 0.5f, 0.04f),
                (0f,   0.04f),
            };

            foreach (var (dx, dur) in frames)
            {
                float elapsed = 0f;
                float startX  = colTr.localPosition.x;
                float targetX = basePos.x + dx;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = TweenUtility.EaseInOutQuad(Mathf.Clamp01(elapsed / dur));
                    colTr.localPosition = new Vector3(
                        Mathf.Lerp(startX, targetX, t),
                        colTr.localPosition.y, 0f);
                    yield return null;
                }
            }
            colTr.localPosition = basePos;

            // Return bolt color over 80ms
            if (heldSr != null)
            {
                float elapsed = 0f;
                Color redColor = heldSr.color;
                while (elapsed < 0.08f)
                {
                    elapsed += Time.deltaTime;
                    if (heldSr == null) break;
                    heldSr.color = Color.Lerp(redColor, origColor, elapsed / 0.08f);
                    yield return null;
                }
                if (heldSr != null) heldSr.color = origColor;
            }

            _sortMechanic.OnRejectionAnimationComplete();
        }

        // ── Win celebration ───────────────────────────────────────────────────────

        private IEnumerator PlayWinCelebration()
        {
            if (_columnBgRenderers == null) yield break;

            // Step 1: Column completion flash — staggered white flash
            AudioMgr.Instance?.PlaySFX("level_win");
            int colCount = _columnBgRenderers.Length;
            for (int i = 0; i < colCount; i++)
            {
                int captured = i;
                if (_columnBgRenderers[captured] != null)
                    StartCoroutine(FlashColumn(captured));
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.35f);

            // Step 2: Board zoom 1.0 → 1.05 → 1.0 (300ms)
            Vector3 baseScale = transform.localScale;
            float zoomDur = 0.15f;
            float elapsed = 0f;
            while (elapsed < zoomDur)
            {
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutBack(Mathf.Clamp01(elapsed / zoomDur));
                transform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * 1.05f, t);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < zoomDur)
            {
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseInOutQuad(Mathf.Clamp01(elapsed / zoomDur));
                transform.localScale = Vector3.LerpUnclamped(baseScale * 1.05f, baseScale, t);
                yield return null;
            }
            transform.localScale = baseScale;

            // Step 3: Particle burst (1500ms) from board center
            StartCoroutine(BurstWinParticles());
        }

        private IEnumerator FlashColumn(int col)
        {
            var sr = _columnBgRenderers[col];
            if (sr == null) yield break;
            Color orig  = sr.color;
            Color white = Color.white;
            float dur   = 0.10f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                sr.color = Color.LerpUnclamped(orig, white, TweenUtility.EaseOutQuad(elapsed / dur));
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                sr.color = Color.LerpUnclamped(white, orig, elapsed / dur);
                yield return null;
            }
            if (sr != null) sr.color = orig;
        }

        private IEnumerator BurstWinParticles()
        {
            int   count    = 40;
            float duration = 1.5f;
            var   colors   = BoltSortTheme.BoltColors;
            Vector3 center = new Vector3(0f, _boardCenterY, 0f);

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 360f);
                float speed = Random.Range(3f, 8f);
                float size  = Random.Range(0.08f, 0.15f);
                float life  = Random.Range(1.0f, 1.5f);
                Color c     = colors[i % colors.Length];
                StartCoroutine(WinParticle(center, angle, speed, size, life, c));
            }
            yield return new WaitForSeconds(duration);
        }

        private IEnumerator WinParticle(Vector3 origin, float angle, float speed,
                                         float size, float lifetime, Color color)
        {
            var go = new GameObject("WinParticle");
            go.transform.position   = origin;
            go.transform.localScale = Vector3.one * size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _particleCircle;
            sr.color        = Color.white;
            sr.sortingOrder = 15;

            float rad     = Mathf.Deg2Rad * angle;
            Vector3 vel   = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * speed;
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                vel.y          -= 2f * Time.deltaTime; // gravity
                go.transform.position += vel * Time.deltaTime;
                go.transform.Rotate(0f, 0f, speed * 90f * Time.deltaTime);
                sr.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ── Scene transition fly-out ──────────────────────────────────────────────

        private IEnumerator AnimateColumnsOut()
        {
            if (_columnTransforms == null) yield break;

            Camera cam      = Camera.main;
            float  flyDist  = cam != null ? cam.orthographicSize * 2f + 2f : 22f;

            for (int i = 0; i < _columnTransforms.Length; i++)
            {
                if (_columnTransforms[i] == null) continue;
                int      captured = i;
                Transform colTr   = _columnTransforms[captured];
                Vector3   baseLoc = colTr.localPosition;
                StartCoroutine(FlyColumnOut(colTr, baseLoc, flyDist));
                yield return new WaitForSeconds(0.06f);
            }
        }

        private IEnumerator FlyColumnOut(Transform colTr, Vector3 baseLoc, float flyDist)
        {
            float dur = 0.22f, elapsed = 0f;
            Vector3 target = baseLoc + Vector3.up * flyDist;
            while (elapsed < dur)
            {
                if (colTr == null) yield break;
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseInBack(Mathf.Clamp01(elapsed / dur));
                colTr.localPosition = Vector3.LerpUnclamped(baseLoc, target, t);
                yield return null;
            }
        }

        // ── Tube sprite geometry ──────────────────────────────────────────────────

        /// <summary>Measured proportions of a Tube_*.png sprite at a given capacity tier:
        /// overall bbox aspect (height/width), the glass-interior region as a fraction of
        /// bbox height, and the bottom-cap region as a fraction of bbox height.</summary>
        private struct TubeMetrics
        {
            public float Aspect;        // bbox height / width — fallback only; real value read from sprite
            public float GlassFrac;     // glass interior height as a fraction of bbox height
            public float BotCapFrac;    // bottom purple cap height as a fraction of bbox height
            public float InteriorWFrac; // glass interior width as a fraction of bbox width
        }

        private static readonly TubeMetrics[] _tubeMetricsByTier =
        {
            new TubeMetrics { Aspect = 2.440f, GlassFrac = 0.834f, BotCapFrac = 0.073f, InteriorWFrac = 0.838f }, // short   (depth <= 3)
            new TubeMetrics { Aspect = 2.844f, GlassFrac = 0.859f, BotCapFrac = 0.056f, InteriorWFrac = 0.844f }, // normal  (depth == 4)
            new TubeMetrics { Aspect = 3.252f, GlassFrac = 0.870f, BotCapFrac = 0.058f, InteriorWFrac = 0.850f }, // large   (depth == 5)
            new TubeMetrics { Aspect = 3.944f, GlassFrac = 0.893f, BotCapFrac = 0.048f, InteriorWFrac = 0.850f }, // x-large (depth == 6)
            new TubeMetrics { Aspect = 5.127f, GlassFrac = 0.918f, BotCapFrac = 0.037f, InteriorWFrac = 0.882f }, // xxl     (depth >= 7)
        };

        private static TubeMetrics TubeMetricsForCapacity(int capacity)
        {
            int tier = capacity <= 3 ? 0
                     : capacity == 4 ? 1
                     : capacity == 5 ? 2
                     : capacity == 6 ? 3
                     : 4;
            return _tubeMetricsByTier[tier];
        }

        // ── Column construction ───────────────────────────────────────────────────

        private void RebuildColumns()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            int totalCols = _colorCount + _tempSlotCount;

            // Per-column capacities (asymmetric-aware). For uniform levels this equals the
            // scalar depths; for tube_capacities levels it is the per-tube capacity.
            _colCap = new int[totalCols];
            int maxColorCap = _stackDepth, maxTempCap = _tempSlotDepth;
            for (int c = 0; c < totalCols; c++)
            {
                _colCap[c] = _gsm != null ? _gsm.GetColumnCapacity(c)
                                          : (c < _colorCount ? _stackDepth : _tempSlotDepth);
                if (c < _colorCount) maxColorCap = Mathf.Max(maxColorCap, _colCap[c]);
                else                 maxTempCap  = Mathf.Max(maxTempCap,  _colCap[c]);
            }

            Camera cam          = Camera.main;
            float  camHalfH     = cam != null ? cam.orthographicSize  : 9.6f;
            float  rawAspect    = cam != null ? cam.aspect            : 9f / 16f;
            float  aspect       = Mathf.Min(rawAspect, 9f / 16f);
            float  camHalfW     = camHalfH * aspect;

            float totalH   = camHalfH * 2f;
            float hudH     = totalH * 0.10f;
            float buttonH  = totalH * 0.20f;

            float boardTop = camHalfH - hudH;
            float boardBot = -camHalfH + buttonH;

            // Responsive multi-row layout — positions/sizes only, no game rules.
            // Pass the MAX capacities so ball sizing fits the tallest tube on asymmetric boards.
            BoardLayout layout = GameplayBoardLayout.Compute(
                _colorCount, _tempSlotCount, maxColorCap, maxTempCap,
                camHalfW, boardTop, boardBot);

            _colWidth     = layout.ColWidth;
            _boltStep     = layout.BoltStep;
            _boardCenterY = layout.BoardCenterY;

            // Vertical budget per tube row — used to clamp tube height to the screen.
            float boardH    = boardTop - boardBot;
            int   rowCount  = Mathf.Max(1, layout.RowCount);
            float rowHeight = (boardH - (rowCount - 1) * GameplayBoardLayout.RowGap) / rowCount;

            int boltLayer = LayerMask.NameToLayer("BoltStacks");
            if (boltLayer < 0) boltLayer = 0;

            _boltRenderers       = new SpriteRenderer[totalCols][];
            _shineRenderers      = new SpriteRenderer[totalCols][];
            _columnTransforms    = new Transform[totalCols];
            _columnBgRenderers   = new SpriteRenderer[totalCols];
            _columnGlowRenderers = new SpriteRenderer[totalCols];
            _tubeRenderers       = new SpriteRenderer[totalCols];
            _columnSlot0World    = new Vector3[totalCols];
            _columnTargetLocal   = new Vector3[totalCols];
            _ballStep            = new float[totalCols];
            _ballSize            = new float[totalCols];
            _tubeWidth           = new float[totalCols];
            _slot0CenterLocal    = new float[totalCols];
            _frozenIcon          = new SpriteRenderer[totalCols];
            _frozenLabel         = new TextMesh[totalCols];

            for (int col = 0; col < totalCols; col++)
            {
                bool    isTemp    = col >= _colorCount;
                int     depth     = _colCap[col]; // per-tube (asymmetric-aware)
                Vector3 colLocal  = layout.Columns[col]; // (x, slot0-Y, 0)
                float   colH      = depth * _boltStep;
                float   colCtrLoc = (depth - 1) * 0.5f * _boltStep;

                var colGO = new GameObject($"Column_{col}");
                colGO.transform.SetParent(transform, false);
                colGO.transform.localPosition = colLocal;
                colGO.layer = boltLayer;

                _columnTransforms[col]  = colGO.transform;
                _columnTargetLocal[col] = colLocal;

                // Ambient glow beneath column (very subtle, column color at 10% alpha)
                var glowGO = new GameObject("ColumnGlow");
                glowGO.transform.SetParent(colGO.transform, false);
                glowGO.transform.localPosition = new Vector3(0f, colCtrLoc, 0.1f);
                glowGO.transform.localScale    = new Vector3(_colWidth * 1.5f, colH * 1.2f, 1f);
                var glowSr = glowGO.AddComponent<SpriteRenderer>();
                glowSr.sprite       = _glowSprite;
                glowSr.color        = isTemp
                    ? new Color(0.29f, 0.22f, 0.36f, 0.08f)
                    : new Color(0.10f, 0.10f, 0.25f, 0.08f);
                glowSr.sortingOrder = -3;
                _columnGlowRenderers[col] = glowSr;

                // Tube body sprite — real art, bottom-anchored. The PNG is a tall portrait
                // image; we scale it UNIFORMLY so the visible tube is exactly _colWidth wide
                // while keeping the sprite's own aspect (caps never distort). Sizing is driven
                // by the available screen space, NOT by the sprite's native pixel size.
                TubeMetrics metrics = TubeMetricsForCapacity(depth);
                Sprite tubeSpr      = GameAssets.TubeSprite(depth, selected: false);
                float spriteNativeW = (tubeSpr != null) ? tubeSpr.bounds.size.x : 1f;
                float spriteNativeH = (tubeSpr != null) ? tubeSpr.bounds.size.y : metrics.Aspect;

                // Width-driven scale, then clamp so a tube never exceeds the per-row height
                // (and never more than 75% of the whole board for a single row).
                float tubeScale  = _colWidth / Mathf.Max(0.0001f, spriteNativeW);
                float tubeHeight = spriteNativeH * tubeScale;
                float maxTubeH   = Mathf.Min(rowHeight * 0.95f, boardH * 0.75f);
                if (tubeHeight > maxTubeH)
                {
                    tubeScale *= maxTubeH / tubeHeight;
                    tubeHeight = maxTubeH;
                }
                float tubeWidth = spriteNativeW * tubeScale; // == _colWidth unless height-clamped

                float tubeBottomLoc  = colCtrLoc - tubeHeight * 0.5f;
                float glassBottomLoc = tubeBottomLoc + tubeHeight * metrics.BotCapFrac;
                float glassHeight    = tubeHeight * metrics.GlassFrac;
                float ballStep       = glassHeight / depth;

                // Ball diameter: 88% of the glass interior width, but never taller than its
                // slot (so balls never overlap the caps or each other). Vertical spacing is
                // ballStep, i.e. ~ballDiameter × 1.05 when height-limited.
                float interiorWidth  = tubeWidth * metrics.InteriorWFrac;
                float ballSize       = Mathf.Min(interiorWidth * 0.88f, ballStep * 0.95f);

                _ballStep[col]         = ballStep;
                _ballSize[col]         = ballSize;
                _tubeWidth[col]        = tubeWidth;
                _slot0CenterLocal[col] = glassBottomLoc + 0.5f * ballStep;
                _columnSlot0World[col] = transform.position + colLocal + Vector3.up * _slot0CenterLocal[col];

                // Position so the sprite's BOTTOM edge lands exactly at tubeBottomLoc,
                // regardless of the sprite's pivot (bottom-center once imported; center if
                // the import hasn't refreshed yet). sprite.bounds is centered on the pivot,
                // so bounds.min.y is the pivot→bottom offset in native units.
                float spriteBottomOffset = (tubeSpr != null ? tubeSpr.bounds.min.y : 0f) * tubeScale;

                var tubeGO = new GameObject("Tube");
                tubeGO.transform.SetParent(colGO.transform, false);
                tubeGO.transform.localPosition = new Vector3(0f, tubeBottomLoc - spriteBottomOffset, 0f);
                tubeGO.transform.localScale    = new Vector3(tubeScale, tubeScale, 1f);
                var tubeSr = tubeGO.AddComponent<SpriteRenderer>();
                tubeSr.sprite       = tubeSpr;
                tubeSr.color        = isTemp ? new Color(0.85f, 0.80f, 0.95f, 1f) : Color.white;
                tubeSr.sortingOrder = -1;
                _tubeRenderers[col]     = tubeSr;
                _columnBgRenderers[col] = tubeSr; // reused by PlayWinCelebration's FlashColumn

                // Phase-2 frozen-tube overlay — snowflake + remaining-turn counter near the tube
                // top. Created hidden; shown/updated each frame in LateUpdate from GSM freeze state.
                float overlayY = colCtrLoc + colH * 0.5f + ballSize * 0.25f;

                var snowGO = new GameObject("FrozenIcon");
                snowGO.transform.SetParent(colGO.transform, false);
                snowGO.transform.localPosition = new Vector3(0f, overlayY, -0.2f);
                snowGO.transform.localScale    = Vector3.one * (ballSize * 0.85f);
                var snowSr = snowGO.AddComponent<SpriteRenderer>();
                snowSr.sprite       = _snowSprite;
                snowSr.color        = new Color(0.72f, 0.90f, 1f, 0.95f);
                snowSr.sortingOrder = 5;
                snowSr.enabled      = false;
                _frozenIcon[col]    = snowSr;

                var lblGO = new GameObject("FrozenCounter");
                lblGO.transform.SetParent(colGO.transform, false);
                lblGO.transform.localPosition = new Vector3(0f, overlayY, -0.3f);
                var tm = lblGO.AddComponent<TextMesh>();
                tm.text          = "";
                tm.anchor        = TextAnchor.MiddleCenter;
                tm.alignment     = TextAlignment.Center;
                tm.fontSize      = 48;
                tm.characterSize = Mathf.Max(0.02f, ballSize * 0.18f);
                tm.color         = new Color(0.08f, 0.22f, 0.5f, 1f);
                var tmr = lblGO.GetComponent<MeshRenderer>();
                if (tmr != null) { tmr.sortingOrder = 6; tmr.enabled = false; }
                _frozenLabel[col] = tm;

                // Physics collider — matches the visible tube so taps register accurately.
                var col2d    = colGO.AddComponent<BoxCollider2D>();
                col2d.size   = new Vector2(tubeWidth, tubeHeight);
                col2d.offset = new Vector2(0f, colCtrLoc);

                colGO.AddComponent<BoltSort.SortMechanic.BoltStackIndex>().Initialize(col);

                // Per-slot renderers
                _boltRenderers[col]  = new SpriteRenderer[depth];
                _shineRenderers[col] = new SpriteRenderer[depth];

                for (int slot = 0; slot < depth; slot++)
                {
                    float slotCenterLoc = _slot0CenterLocal[col] + slot * ballStep;

                    // Shadow layer
                    var shadowGO = new GameObject($"Shadow_{slot}");
                    shadowGO.transform.SetParent(colGO.transform, false);
                    shadowGO.transform.localPosition = new Vector3(0.03f, slotCenterLoc - ballSize * 0.4f, 0f);
                    shadowGO.transform.localScale    = new Vector3(ballSize * 0.95f, ballSize * 0.40f, 1f);
                    var shadowSr = shadowGO.AddComponent<SpriteRenderer>();
                    shadowSr.sprite       = _shadowSprite;
                    shadowSr.color        = new Color(0f, 0f, 0f, 0.35f);
                    shadowSr.sortingOrder = 0;
                    shadowSr.enabled      = false; // enabled only for filled bolts

                    // Ball — final scale is set per-frame in LateUpdate (normalized to the
                    // actual ball sprite), so placeholder scale here is just the diameter.
                    var boltGO = new GameObject($"Slot_{slot}");
                    boltGO.transform.SetParent(colGO.transform, false);
                    boltGO.transform.localPosition = new Vector3(0f, slotCenterLoc, 0f);
                    boltGO.transform.localScale    = new Vector3(ballSize, ballSize, 1f);

                    var boltSr = boltGO.AddComponent<SpriteRenderer>();
                    boltSr.sortingOrder = 1;
                    boltSr.enabled      = false; // shown by LateUpdate when filled
                    _boltRenderers[col][slot] = boltSr;

                    // Specular highlight
                    var shineGO = new GameObject("Shine");
                    shineGO.transform.SetParent(boltGO.transform, false);
                    shineGO.transform.localPosition = new Vector3(-0.20f, 0.22f, 0f);
                    shineGO.transform.localScale    = new Vector3(0.40f, 0.34f, 1f);
                    var shineSr = shineGO.AddComponent<SpriteRenderer>();
                    shineSr.sprite       = _shineSprite;
                    shineSr.color        = new Color(1f, 1f, 1f, 0.60f);
                    shineSr.enabled      = false;
                    shineSr.sortingOrder = 2;
                    _shineRenderers[col][slot] = shineSr;
                }
            }

            // Drop-in stagger animation
            StartCoroutine(AnimateColumnsIn());
        }

        private IEnumerator AnimateColumnsIn()
        {
            Camera cam    = Camera.main;
            float flyDist = cam != null ? cam.orthographicSize * 2f + 2f : 22f;

            int totalCols = _columnTransforms?.Length ?? 0;
            // Move all columns off-screen top first
            for (int i = 0; i < totalCols; i++)
            {
                if (_columnTransforms[i] == null) continue;
                var pos = _columnTransforms[i].localPosition;
                _columnTransforms[i].localPosition = pos + Vector3.up * flyDist;
            }

            // Stagger drop-in with EaseOutBounce
            for (int i = 0; i < totalCols; i++)
            {
                if (_columnTransforms[i] == null) continue;
                int captured = i;
                StartCoroutine(DropColumnIn(captured, flyDist));
                yield return new WaitForSeconds(0.08f);
            }
        }

        private IEnumerator DropColumnIn(int col, float flyDist)
        {
            if (_columnTransforms[col] == null) yield break;
            Transform colTr    = _columnTransforms[col];
            Vector3   startPos = colTr.localPosition;
            Vector3   target   = _columnTargetLocal[col];
            float     dur      = 0.30f, elapsed = 0f;

            while (elapsed < dur)
            {
                if (colTr == null) yield break;
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutBounce(Mathf.Clamp01(elapsed / dur));
                colTr.localPosition = Vector3.LerpUnclamped(startPos, target, t);
                yield return null;
            }
            if (colTr != null) colTr.localPosition = target;
        }

        // ── Per-frame rendering ───────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_gsm == null || _boltRenderers == null) return;
            if (_gsm.LifecycleState == BoltSort.GameStateManager.GSMLifecycleState.Unloaded) return;

            IReadOnlyList<int>[] stacks = _gsm.StackContents;
            IReadOnlyList<int>[] temps  = _gsm.TempSlotContents;
            int totalCols = _colorCount + _tempSlotCount;

            bool isSelected = _sortMechanic != null &&
                              _sortMechanic.CurrentState == SortMechState.BoltSelected;
            int  heldCol    = isSelected ? _sortMechanic.HeldSourceIndex : -1;

            IReadOnlyList<int> heldColData = null;
            int heldTopSlot = -1;
            if (isSelected && heldCol >= 0 && heldCol < totalCols)
            {
                heldColData = heldCol < _colorCount
                    ? (stacks != null && heldCol < stacks.Length ? stacks[heldCol] : null)
                    : (temps  != null && (heldCol - _colorCount) < temps.Length
                           ? temps[heldCol - _colorCount] : null);
                if (heldColData != null && heldColData.Count > 0)
                    heldTopSlot = heldColData.Count - 1;
            }

            for (int col = 0; col < totalCols; col++)
            {
                if (_boltRenderers[col] == null) continue;

                IReadOnlyList<int> column = col < _colorCount
                    ? (stacks != null && col < stacks.Length ? stacks[col] : null)
                    : (temps  != null && (col - _colorCount) < temps.Length
                           ? temps[col - _colorCount] : null);

                int depth = _colCap[col];
                for (int slot = 0; slot < depth; slot++)
                {
                    var boltSr = _boltRenderers[col][slot];
                    if (boltSr == null) continue;

                    // Hide destination slot during move animation
                    if (col == _hideDstCol && slot == _hideDstSlot)
                    {
                        boltSr.enabled = false;
                        var shine = _shineRenderers[col]?[slot];
                        if (shine != null) shine.enabled = false;
                        continue;
                    }
                    // Occupancy & sprite. A slot is filled when occupied — the token may be a
                    // normal color (>0), the multicolor wildcard (0), or a covered mystery (<0),
                    // so occupancy is by COUNT, not by color sign.
                    bool occupied = column != null && slot < column.Count;
                    int  token    = occupied ? column[slot] : 0;
                    bool filled   = occupied;

                    boltSr.enabled = filled;
                    var shineSr = _shineRenderers[col]?[slot];
                    if (!filled)
                    {
                        if (shineSr != null) shineSr.enabled = false;
                        continue;
                    }

                    if (token == 0)
                    {
                        var frames = GameAssets.BallMulticolorFrames;
                        if (frames != null && frames.Length > 1)
                        {
                            int frame = (int)(Time.time * MulticolorFps) % frames.Length;
                            boltSr.sprite = frames[frame];
                        }
                        else
                        {
                            boltSr.sprite = GameAssets.BallMulticolor;
                        }
                    }
                    else
                    {
                        boltSr.sprite = GameAssets.BallSpriteForToken(token);
                    }
                    boltSr.color  = Color.white;

                    // Base position (slot center)
                    float slotY = _slot0CenterLocal[col] + slot * _ballStep[col];

                    // Apply selection offset + bob for held bolt
                    bool isHeldSlot = (col == heldCol && slot == heldTopSlot);
                    if (isHeldSlot)
                    {
                        float bob = _selLifted
                            ? Mathf.Sin(_bobTimer * Mathf.PI * 2f * 1.5f) * 0.03f
                            : 0f;
                        slotY += _selYOffset + bob;
                    }

                    boltSr.transform.localPosition = new Vector3(0f, slotY, 0f);

                    // Scale to the target world diameter, normalized by the sprite's native
                    // size so the result is independent of the PNG's import PPU.
                    float diameter   = _ballSize[col] * (isHeldSlot ? _selScale : 1f);
                    float nativeBallW = boltSr.sprite != null ? boltSr.sprite.bounds.size.x : 1f;
                    float ballScale  = diameter / Mathf.Max(0.0001f, nativeBallW);
                    boltSr.transform.localScale = new Vector3(ballScale, ballScale, 1f);

                    if (shineSr != null) shineSr.enabled = true;
                }
            }

            // Update column glow + tube sprite for selected column
            for (int col = 0; col < totalCols; col++)
            {
                var tsr = _tubeRenderers != null && col < _tubeRenderers.Length ? _tubeRenderers[col] : null;
                if (tsr != null)
                {
                    int    depth   = _colCap[col];
                    Sprite wantSpr = GameAssets.TubeSprite(depth, selected: col == heldCol);
                    if (tsr.sprite != wantSpr && wantSpr != null)
                    {
                        tsr.sprite = wantSpr;
                        // Keep the visible tube the same width across the swap — selected/
                        // unselected PNGs have different native widths, so re-normalize.
                        float targetW = (_tubeWidth != null && col < _tubeWidth.Length) ? _tubeWidth[col] : _colWidth;
                        float s = targetW / Mathf.Max(0.0001f, wantSpr.bounds.size.x);
                        tsr.transform.localScale = new Vector3(s, s, 1f);
                    }

                    // Phase-2 frozen tube: blue tint + snowflake/counter overlay (visible from move 0).
                    int   frozenRem = _gsm != null ? _gsm.GetFreezeRemaining(col) : 0;
                    bool  isTempCol = col >= _colorCount;
                    Color baseTint  = isTempCol ? new Color(0.85f, 0.80f, 0.95f, 1f) : Color.white;
                    tsr.color = frozenRem > 0 ? new Color(0.50f, 0.72f, 1f, 1f) : baseTint;

                    if (_frozenIcon != null && col < _frozenIcon.Length && _frozenIcon[col] != null)
                    {
                        _frozenIcon[col].enabled = frozenRem > 0;
                        float pulse = frozenRem == 1 ? 1f + 0.12f * Mathf.Sin(Time.time * 8f) : 1f; // thawing pulse
                        _frozenIcon[col].transform.localScale = Vector3.one * (_ballSize[col] * 0.85f * pulse);
                    }
                    if (_frozenLabel != null && col < _frozenLabel.Length && _frozenLabel[col] != null)
                    {
                        var tmr = _frozenLabel[col].GetComponent<MeshRenderer>();
                        if (frozenRem > 0) { _frozenLabel[col].text = frozenRem.ToString(); if (tmr != null) tmr.enabled = true; }
                        else if (tmr != null) tmr.enabled = false;
                    }
                }

                var gsr = _columnGlowRenderers != null && col < _columnGlowRenderers.Length ? _columnGlowRenderers[col] : null;
                if (gsr == null) continue;
                if (col == heldCol)
                {
                    Color gc = BoltSortTheme.TubeSelected;
                    gc.a = 0.50f + Mathf.Sin(Time.time * 2f) * 0.05f; // D.2-E: raised from 0.30
                    gsr.color = gc;
                }
                else
                {
                    var c = gsr.color;
                    c.a = 0.08f;
                    gsr.color = c;
                }
            }
        }

        // ── Mystery reveal ────────────────────────────────────────────────────────

        /// <summary>
        /// GSM fired a mystery-ball reveal (the negative token already flipped positive, so
        /// LateUpdate now renders the real color). Adds a colored particle burst flourish at the
        /// revealed ball's position. Phase-2 (schema_version 2).
        /// </summary>
        private void OnMysteryRevealed(int flatIndex, int revealedColor)
        {
            if (_columnSlot0World == null || _ballStep == null ||
                flatIndex < 0 || flatIndex >= _columnSlot0World.Length) return;

            IReadOnlyList<int>[] stacks = _gsm.StackContents;
            IReadOnlyList<int>[] temps  = _gsm.TempSlotContents;
            IReadOnlyList<int> col = flatIndex < _colorCount
                ? (stacks != null && flatIndex < stacks.Length ? stacks[flatIndex] : null)
                : (temps  != null && (flatIndex - _colorCount) < temps.Length
                       ? temps[flatIndex - _colorCount] : null);
            int topSlot = (col != null && col.Count > 0) ? col.Count - 1 : 0;
            Vector3 world = _columnSlot0World[flatIndex] + Vector3.up * (topSlot * _ballStep[flatIndex]);

            AudioMgr.Instance?.PlaySFX("mystery_reveal");
            Color c = BoltSortTheme.BoltColorForId(revealedColor);
            for (int i = 0; i < 12; i++)
                StartCoroutine(WinParticle(world, i * 30f, Random.Range(2f, 4f),
                                           Random.Range(0.06f, 0.12f), Random.Range(0.30f, 0.50f), c));
        }

        // ── Procedural sprite factories ───────────────────────────────────────────

        /// <summary>Six-spoke snowflake glyph for the frozen-tube overlay.</summary>
        private static Sprite CreateSnowSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, R = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Atan2(dy, dx);
                    float fold = Mathf.Repeat(a, Mathf.PI / 3f);
                    float dist = Mathf.Min(fold, Mathf.PI / 3f - fold); // angular distance to nearest spoke
                    float alpha = 0f;
                    if (r < R)
                    {
                        float spoke = Mathf.Clamp01(1f - dist / 0.18f) * Mathf.Clamp01(1f - r / R);
                        float core  = Mathf.Clamp01(1f - r / (R * 0.30f));
                        alpha = Mathf.Clamp01(Mathf.Max(spoke, core));
                    }
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateShineSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, rr = size * 0.44f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d  = Mathf.Sqrt(dx * dx * 1.4f + dy * dy);
                    float t  = 1f - d / rr;
                    float a  = Mathf.Clamp01(t * t * t); // cubic falloff for sharper specular
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateShadowSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, rx = size * 0.46f, ry = size * 0.28f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01((1f - d) * 1.5f);
                    a        = a * a * 0.6f;
                    px[y * size + x] = new Color(0f, 0f, 0f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateGlowSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - cx) / cx, dy = (y + 0.5f - cy) / cy;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    float a  = Mathf.Clamp01((1f - d) * (1f - d));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        /// <summary>Sharp anti-aliased white filled circle for dust/win particles.</summary>
        private static Sprite CreateParticleCircleSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, r = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    // AA edge: 1px feather
                    float a  = Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }
    }
}
