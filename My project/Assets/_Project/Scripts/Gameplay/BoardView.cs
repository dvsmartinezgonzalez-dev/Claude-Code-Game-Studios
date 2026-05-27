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
        private float _colStep;
        private float _colWidth;
        private float _boltHeight;
        private float _boltStep;
        private float _boardCenterY;

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
        private Vector3[]        _columnSlot0World; // world-space origin of slot 0 per column

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

        // ── Shared sprites ────────────────────────────────────────────────────────
        private static Sprite _whiteSprite;
        private static Sprite _boltSprite;
        private static Sprite _ringSprite;
        private static Sprite _shineSprite;
        private static Sprite _shadowSprite;
        private static Sprite _glowSprite;

        // ─────────────────────────────────────────────────────────────────────────

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm)
        {
            _gsm          = gsm;
            _sortMechanic = sm;

            _whiteSprite  = _whiteSprite  ?? CreateWhiteSprite();
            _boltSprite   = _boltSprite   ?? CreateMarbleSprite();
            _ringSprite   = _ringSprite   ?? CreateRingSprite();
            _shineSprite  = _shineSprite  ?? CreateShineSprite();
            _shadowSprite = _shadowSprite ?? CreateShadowSprite();
            _glowSprite   = _glowSprite   ?? CreateGlowSprite();

            gsm.OnLevelLoaded    += OnLevelLoaded;
            gsm.OnLevelComplete  += OnLevelComplete;
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

            // Bolt size in world units (same as bolt local scale x)
            float boltSize = _colWidth - 0.12f;

            Vector3 srcWorld = _columnSlot0World[src];
            // src slot: GSM already removed the bolt — slot count now is correct
            IReadOnlyList<int> srcColAfter = src < _colorCount
                ? (stacks != null && src < stacks.Length ? stacks[src] : null)
                : (temps  != null && (src - _colorCount) < temps.Length
                       ? temps[src - _colorCount] : null);
            int srcSlot = srcColAfter != null ? srcColAfter.Count : 0;
            srcWorld = _columnSlot0World[src] + Vector3.up * (srcSlot * _boltStep);

            Vector3 dstWorld = _columnSlot0World[dst] + Vector3.up * (dstSlot * _boltStep);

            // Hide the destination slot while ghost is in the air
            _hideDstCol  = dst;
            _hideDstSlot = dstSlot;

            // Create ghost bolt
            _moveGhost = new GameObject("MoveGhost");
            _moveGhost.transform.position   = srcWorld;
            _moveGhost.transform.localScale = new Vector3(boltSize, _boltHeight, 1f);

            var shadowGO = new GameObject("GhostShadow");
            shadowGO.transform.SetParent(_moveGhost.transform, false);
            shadowGO.transform.localPosition = new Vector3(0.04f, -0.05f, 0f);
            shadowGO.transform.localScale    = new Vector3(1.1f, 0.6f, 1f);
            var shadowSr = shadowGO.AddComponent<SpriteRenderer>();
            shadowSr.sprite       = _shadowSprite;
            shadowSr.color        = BoltSortTheme.BoltShadow;
            shadowSr.sortingOrder = 8;

            var ghostSr = _moveGhost.AddComponent<SpriteRenderer>();
            ghostSr.sprite       = _boltSprite;
            ghostSr.color        = BoltSortTheme.BoltColorForId(colorId);
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

            float squishDur = 0.08f;
            elapsed = 0f;
            Vector3 normalScale = new Vector3(boltSize, _boltHeight, 1f);
            while (elapsed < squishDur)
            {
                if (_moveGhost == null) yield break;
                elapsed += Time.deltaTime;
                float t = TweenUtility.EaseOutElastic(Mathf.Clamp01(elapsed / squishDur));
                // squish is the "going in" motion (scale toward squished) then spring
                float sx = Mathf.LerpUnclamped(boltSize * 1.20f, boltSize, t);
                float sy = Mathf.LerpUnclamped(_boltHeight * 0.85f, _boltHeight, t);
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
            sr.sprite       = _glowSprite;
            sr.color        = new Color(color.r, color.g, color.b, 0.6f);
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
                sr.color = new Color(color.r, color.g, color.b, 0.6f * (1f - t));
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
            sr.sprite       = _boltSprite;
            sr.color        = color;
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
                sr.color = new Color(color.r, color.g, color.b, 1f - t);
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

        // ── Column construction ───────────────────────────────────────────────────

        private void RebuildColumns()
        {
            foreach (Transform child in transform) Destroy(child.gameObject);

            int totalCols = _colorCount + _tempSlotCount;

            Camera cam          = Camera.main;
            float  camHalfH     = cam != null ? cam.orthographicSize  : 9.6f;
            float  rawAspect    = cam != null ? cam.aspect            : 9f / 16f;
            float  aspect       = Mathf.Min(rawAspect, 9f / 16f);
            float  camHalfW     = camHalfH * aspect;

            float totalH   = camHalfH * 2f;
            float hudH     = totalH * 0.10f;
            float buttonH  = totalH * 0.20f;
            float boardH   = totalH * 0.70f;

            float boardTop    = camHalfH - hudH;
            float boardBot    = -camHalfH + buttonH;
            float boardCenter = (boardTop + boardBot) * 0.5f;

            float usableW = camHalfW * 2f - 0.40f;
            _colStep    = usableW / totalCols;
            _colWidth   = _colStep * 0.82f;

            int   maxDepth = Mathf.Max(_stackDepth, _tempSlotDepth);
            _boltStep      = boardH / (maxDepth + 0.5f);
            _boltHeight    = _boltStep * 0.78f;
            _boardCenterY  = boardCenter - (maxDepth - 1) * _boltStep * 0.5f;

            float startX       = -(totalCols - 1) * 0.5f * _colStep;
            int   boltLayer    = LayerMask.NameToLayer("BoltStacks");
            if (boltLayer < 0) boltLayer = 0;

            _boltRenderers           = new SpriteRenderer[totalCols][];
            _shineRenderers          = new SpriteRenderer[totalCols][];
            _columnTransforms        = new Transform[totalCols];
            _columnBgRenderers       = new SpriteRenderer[totalCols];
            _columnGlowRenderers     = new SpriteRenderer[totalCols];
            _columnSlot0World        = new Vector3[totalCols];

            for (int col = 0; col < totalCols; col++)
            {
                bool  isTemp     = col >= _colorCount;
                int   depth      = isTemp ? _tempSlotDepth : _stackDepth;
                float colX       = startX + col * _colStep;
                float colH       = depth * _boltStep;
                float colCtrLoc  = (depth - 1) * 0.5f * _boltStep;

                var colGO = new GameObject($"Column_{col}");
                colGO.transform.SetParent(transform, false);
                colGO.transform.localPosition = new Vector3(colX, _boardCenterY, 0f);
                colGO.layer = boltLayer;

                _columnTransforms[col]  = colGO.transform;
                _columnSlot0World[col]  = transform.position +
                                          new Vector3(colX, _boardCenterY, 0f);

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

                // Border
                PlaceRect(colGO, "Border",
                    new Vector3(0f, colCtrLoc, 0f),
                    new Vector3(_colWidth + 0.18f, colH + 0.36f, 1f),
                    isTemp ? BoltSortTheme.HexColor("4A3A5C") : BoltSortTheme.TubeRim,
                    sortingOrder: -2);

                // Background fill (tube body)
                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(colGO.transform, false);
                bgGO.transform.localPosition = new Vector3(0f, colCtrLoc, 0f);
                bgGO.transform.localScale    = new Vector3(_colWidth, colH + 0.14f, 1f);
                var bgSr = bgGO.AddComponent<SpriteRenderer>();
                bgSr.sprite       = _whiteSprite;
                bgSr.color        = isTemp
                    ? new Color(0.114f, 0.102f, 0.165f, 1f)
                    : BoltSortTheme.TubeBody;
                bgSr.sortingOrder = -1;
                _columnBgRenderers[col] = bgSr;

                // Glass reflection — thin white line on left edge
                var glassGO = new GameObject("GlassReflect");
                glassGO.transform.SetParent(colGO.transform, false);
                glassGO.transform.localPosition = new Vector3(-_colWidth * 0.44f, colCtrLoc, 0f);
                glassGO.transform.localScale    = new Vector3(0.04f, colH + 0.10f, 1f);
                var glassSr = glassGO.AddComponent<SpriteRenderer>();
                glassSr.sprite       = _whiteSprite;
                glassSr.color        = new Color(1f, 1f, 1f, 0.12f);
                glassSr.sortingOrder = 0;

                // Top rim highlight
                var rimGO = new GameObject("TopRim");
                rimGO.transform.SetParent(colGO.transform, false);
                rimGO.transform.localPosition = new Vector3(0f, colCtrLoc + (colH + 0.14f) * 0.5f - 0.04f, 0f);
                rimGO.transform.localScale    = new Vector3(_colWidth, 0.08f, 1f);
                var rimSr = rimGO.AddComponent<SpriteRenderer>();
                rimSr.sprite       = _whiteSprite;
                rimSr.color        = new Color(1f, 1f, 1f, 0.20f);
                rimSr.sortingOrder = 0;

                // Physics collider
                var col2d    = colGO.AddComponent<BoxCollider2D>();
                col2d.size   = new Vector2(_colWidth, colH + 0.14f);
                col2d.offset = new Vector2(0f, colCtrLoc);

                colGO.AddComponent<BoltSort.SortMechanic.BoltStackIndex>().Initialize(col);

                // Per-slot renderers
                _boltRenderers[col]  = new SpriteRenderer[depth];
                _shineRenderers[col] = new SpriteRenderer[depth];

                for (int slot = 0; slot < depth; slot++)
                {
                    // Shadow layer
                    var shadowGO = new GameObject($"Shadow_{slot}");
                    shadowGO.transform.SetParent(colGO.transform, false);
                    shadowGO.transform.localPosition = new Vector3(0.03f, slot * _boltStep - 0.05f, 0f);
                    shadowGO.transform.localScale    = new Vector3(_colWidth * 0.90f, _boltHeight * 0.55f, 1f);
                    var shadowSr = shadowGO.AddComponent<SpriteRenderer>();
                    shadowSr.sprite       = _shadowSprite;
                    shadowSr.color        = new Color(0f, 0f, 0f, 0.35f);
                    shadowSr.sortingOrder = 0;
                    shadowSr.enabled      = false; // enabled only for filled bolts

                    // Base bolt
                    var boltGO = new GameObject($"Slot_{slot}");
                    boltGO.transform.SetParent(colGO.transform, false);
                    boltGO.transform.localPosition = new Vector3(0f, slot * _boltStep, 0f);
                    boltGO.transform.localScale    = new Vector3(_colWidth - 0.12f, _boltHeight, 1f);

                    var boltSr = boltGO.AddComponent<SpriteRenderer>();
                    boltSr.sprite       = _ringSprite;
                    boltSr.color        = new Color(0.165f, 0.165f, 0.290f, 0.80f);
                    boltSr.sortingOrder = 1;
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
            float     colX     = -((_colorCount + _tempSlotCount - 1) * 0.5f * _colStep)
                                 + col * _colStep;
            Vector3   target   = new Vector3(colX, _boardCenterY, 0f);
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

        private void PlaceRect(GameObject parent, string name,
                               Vector3 localPos, Vector3 localScale,
                               Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _whiteSprite;
            sr.color        = color;
            sr.sortingOrder = sortingOrder;
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

                int depth = col < _colorCount ? _stackDepth : _tempSlotDepth;
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
                    boltSr.enabled = true;

                    // Base position
                    float slotY = slot * _boltStep;

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

                    // Scale — apply selection scale
                    float sx = _colWidth - 0.12f;
                    float sy = _boltHeight;
                    if (isHeldSlot)
                    {
                        sx *= _selScale;
                        sy *= _selScale;
                    }
                    boltSr.transform.localScale = new Vector3(sx, sy, 1f);

                    // Color & sprite
                    int  colorId = (column != null && slot < column.Count) ? column[slot] : 0;
                    bool filled  = colorId > 0;

                    boltSr.sprite = filled ? _boltSprite : _ringSprite;

                    Color baseColor = BoltSortTheme.BoltColorForId(colorId);
                    if (isHeldSlot && filled)
                        baseColor = BoltSortTheme.BrightnessMult(baseColor, 1.25f);
                    boltSr.color = baseColor;

                    var shineSr = _shineRenderers[col]?[slot];
                    if (shineSr != null) shineSr.enabled = filled;
                }
            }

            // Update column glow for selected column
            if (_columnGlowRenderers != null)
            {
                for (int col = 0; col < _columnGlowRenderers.Length; col++)
                {
                    var gsr = _columnGlowRenderers[col];
                    if (gsr == null) continue;
                    if (col == heldCol)
                    {
                        Color gc = BoltSortTheme.TubeSelected;
                        gc.a = 0.30f + Mathf.Sin(Time.time * 2f) * 0.05f;
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
        }

        // ── Procedural sprite factories ───────────────────────────────────────────

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
        }

        // Premium marble-look sphere sprite: radial shading + upper-left specular
        private static Sprite CreateMarbleSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float c = size * 0.5f, r = c - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx   = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - dist) * 2.5f);

                    // Base radial shading — bright centre, shadowed rim
                    float shade = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dist / r));

                    // Primary specular blob — upper left, sharp
                    float hlDx  = dx + r * 0.30f, hlDy = dy + r * 0.30f;
                    float hlDist = Mathf.Sqrt(hlDx * hlDx * 0.7f + hlDy * hlDy);
                    float spec1  = Mathf.Clamp01(1f - hlDist / (r * 0.35f));
                    shade        = Mathf.Min(1f, shade + spec1 * spec1 * 0.55f);

                    // Secondary soft specular — larger, lower intensity
                    float hlDx2  = dx + r * 0.15f, hlDy2 = dy + r * 0.15f;
                    float hlDist2 = Mathf.Sqrt(hlDx2 * hlDx2 + hlDy2 * hlDy2);
                    float spec2   = Mathf.Clamp01(1f - hlDist2 / (r * 0.55f));
                    shade         = Mathf.Min(1f, shade + spec2 * 0.18f);

                    // Rim darkening (subsurface scatter fake)
                    float rimT = Mathf.Clamp01((dist - r * 0.75f) / (r * 0.25f));
                    shade      = Mathf.Lerp(shade, shade * 0.70f, rimT * rimT);

                    px[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        private static Sprite CreateRingSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px  = new Color[size * size];
            float c = size * 0.5f, rOuter = c - 2f, rInner = rOuter - 7f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist   = Mathf.Sqrt((x + .5f - c) * (x + .5f - c) + (y + .5f - c) * (y + .5f - c));
                    float outerA = Mathf.Clamp01((rOuter - dist) * 2f);
                    float innerA = Mathf.Clamp01((dist - rInner) * 2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, outerA * innerA * 0.7f);
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
    }
}
