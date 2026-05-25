using System.Collections.Generic;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Renders the bolt-sorting board as coloured world-space sprites.
    /// Layout zones: top 10% = HUD, middle 70% = board, bottom 20% = buttons.
    /// Portrait aspect (9:16) is enforced regardless of Game view orientation.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        // ── Layout state ──────────────────────────────────────────────────────────
        private float _colStep;
        private float _colWidth;
        private float _boltHeight;
        private float _boltStep;
        private float _boardCenterY;

        // ── Color palette (#spec-aligned) ─────────────────────────────────────────
        private static readonly Color ColBgColor      = new Color(0.102f, 0.102f, 0.180f); // #1A1A2E
        private static readonly Color TempBgColor     = new Color(0.114f, 0.102f, 0.165f); // #1D1A2A
        private static readonly Color ColBorderColor  = new Color(0.227f, 0.227f, 0.361f); // #3A3A5C
        private static readonly Color TempBorderColor = new Color(0.290f, 0.227f, 0.361f); // #4A3A5C

        private static readonly Color[] BoltColors =
        {
            new Color(0.165f, 0.165f, 0.290f, 0.80f), // 0 – empty slot ring (#2A2A4A)
            new Color(0.92f,  0.20f,  0.18f),          // 1 – red
            new Color(0.18f,  0.50f,  0.95f),          // 2 – blue
            new Color(0.16f,  0.80f,  0.25f),          // 3 – green
            new Color(0.96f,  0.84f,  0.08f),          // 4 – yellow
            new Color(0.96f,  0.50f,  0.08f),          // 5 – orange
            new Color(0.65f,  0.20f,  0.96f),          // 6 – purple
            new Color(0.08f,  0.86f,  0.86f),          // 7 – cyan
            new Color(0.96f,  0.20f,  0.76f),          // 8 – magenta
        };

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

        // ── Shared sprites (static — survive level reloads) ───────────────────────
        private static Sprite _sharedSprite; // 2×2 white rectangle
        private static Sprite _boltSprite;   // solid circle with radial shading
        private static Sprite _ringSprite;   // hollow ring for empty slots
        private static Sprite _shineSprite;  // soft white oval for 3-D highlight

        // ─────────────────────────────────────────────────────────────────────────

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm)
        {
            _gsm          = gsm;
            _sortMechanic = sm;

            _sharedSprite = _sharedSprite ?? CreateWhiteSprite();
            _boltSprite   = _boltSprite   ?? CreateCircleSprite();
            _ringSprite   = _ringSprite   ?? CreateRingSprite();
            _shineSprite  = _shineSprite  ?? CreateShineSprite();

            gsm.OnLevelLoaded  += OnLevelLoaded;
            sm.OnMoveCommitted += OnMoveCommitted;
            // No rejection animation — handshake immediately so FSM exits InvalidMove.
            sm.OnMoveRejected  += (_, _, _, _) => _sortMechanic.OnRejectionAnimationComplete();
        }

        private void OnLevelLoaded(int levelId, int colorCount, int stackDepth,
                                   int tempSlotCount, int tempSlotDepth, long seqId)
        {
            _colorCount    = colorCount;
            _stackDepth    = stackDepth;
            _tempSlotCount = tempSlotCount;
            _tempSlotDepth = tempSlotDepth;
            RebuildColumns();
        }

        private void OnMoveCommitted(int src, int dst, int colorId, long seqId)
        {
            // GSM already mutated the board. Complete FSM handshake so Idle is re-entered.
            _sortMechanic.OnAnimationComplete(seqId);
        }

        // ── Column construction ───────────────────────────────────────────────────

        private void RebuildColumns()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            int totalCols = _colorCount + _tempSlotCount;

            Camera cam          = Camera.main;
            float camHalfHeight = cam != null ? cam.orthographicSize : 9.6f;

            // Portrait enforcement: cap aspect at 9:16 so layout is correct in landscape Editor views.
            float rawAspect     = cam != null ? cam.aspect : 9f / 16f;
            float aspect        = Mathf.Min(rawAspect, 9f / 16f);
            float camHalfWidth  = camHalfHeight * aspect;

            // Layout zones
            float totalH  = camHalfHeight * 2f;
            float hudH    = totalH * 0.10f;
            float buttonH = totalH * 0.20f;
            float boardH  = totalH * 0.70f;

            float boardTop        = camHalfHeight - hudH;
            float boardBottom     = -camHalfHeight + buttonH;
            float boardAreaCenter = (boardTop + boardBottom) * 0.5f;

            // Column horizontal dimensions
            float usableW = camHalfWidth * 2f - 0.40f; // 0.2 margin each side
            _colStep  = usableW / totalCols;
            _colWidth = _colStep * 0.82f;

            // Bolt vertical dimensions
            int maxDepth = Mathf.Max(_stackDepth, _tempSlotDepth);
            _boltStep   = boardH / (maxDepth + 0.5f);
            _boltHeight = _boltStep * 0.78f;

            // Slot-0 position: center the tallest column inside the board area.
            _boardCenterY = boardAreaCenter - (maxDepth - 1) * _boltStep * 0.5f;

            float startX         = -(totalCols - 1) * 0.5f * _colStep;
            int   boltStackLayer = LayerMask.NameToLayer("BoltStacks");
            if (boltStackLayer < 0)
            {
                Debug.LogWarning("[BoardView] Layer 'BoltStacks' not found — using layer 0.");
                boltStackLayer = 0;
            }

            _boltRenderers  = new SpriteRenderer[totalCols][];
            _shineRenderers = new SpriteRenderer[totalCols][];

            for (int col = 0; col < totalCols; col++)
            {
                bool  isTemp        = col >= _colorCount;
                int   depth         = isTemp ? _tempSlotDepth : _stackDepth;
                float colX          = startX + col * _colStep;
                float colH          = depth * _boltStep;
                float colCenterLocal = (depth - 1) * 0.5f * _boltStep;

                var colGO = new GameObject($"Column_{col}");
                colGO.transform.SetParent(transform, false);
                colGO.transform.localPosition = new Vector3(colX, _boardCenterY, 0f);
                colGO.layer = boltStackLayer;

                // Border (outline behind background)
                PlaceRect(colGO, "Border",
                    new Vector3(0f, colCenterLocal, 0f),
                    new Vector3(_colWidth + 0.18f, colH + 0.36f, 1f),
                    isTemp ? TempBorderColor : ColBorderColor,
                    sortingOrder: -1);

                // Background fill
                PlaceRect(colGO, "Background",
                    new Vector3(0f, colCenterLocal, 0f),
                    new Vector3(_colWidth, colH + 0.14f, 1f),
                    isTemp ? TempBgColor : ColBgColor,
                    sortingOrder: 0);

                // Physics collider for tap detection
                var col2d    = colGO.AddComponent<BoxCollider2D>();
                col2d.size   = new Vector2(_colWidth, colH + 0.14f);
                col2d.offset = new Vector2(0f, colCenterLocal);

                colGO.AddComponent<BoltSort.SortMechanic.BoltStackIndex>().Initialize(col);

                // Per-slot renderers
                _boltRenderers[col]  = new SpriteRenderer[depth];
                _shineRenderers[col] = new SpriteRenderer[depth];

                for (int slot = 0; slot < depth; slot++)
                {
                    // Bolt / ring sprite (switches at runtime between filled and empty)
                    var boltGO = new GameObject($"Slot_{slot}");
                    boltGO.transform.SetParent(colGO.transform, false);
                    boltGO.transform.localPosition = new Vector3(0f, slot * _boltStep, 0f);
                    boltGO.transform.localScale    = new Vector3(_colWidth - 0.12f, _boltHeight, 1f);

                    var boltSr = boltGO.AddComponent<SpriteRenderer>();
                    boltSr.sprite       = _ringSprite;
                    boltSr.color        = BoltColors[0];
                    boltSr.sortingOrder = 1;
                    _boltRenderers[col][slot] = boltSr;

                    // Shine highlight (child of boltGO — moves automatically with lift).
                    var shineGO = new GameObject("Shine");
                    shineGO.transform.SetParent(boltGO.transform, false);
                    // Upper-left offset in bolt local space; scale is bolt-relative.
                    shineGO.transform.localPosition = new Vector3(-0.22f, 0.22f, 0f);
                    shineGO.transform.localScale    = new Vector3(0.40f, 0.34f, 1f);

                    var shineSr = shineGO.AddComponent<SpriteRenderer>();
                    shineSr.sprite       = _shineSprite;
                    shineSr.color        = new Color(1f, 1f, 1f, 0.55f);
                    shineSr.enabled      = false;
                    shineSr.sortingOrder = 2;
                    _shineRenderers[col][slot] = shineSr;
                }
            }
        }

        private void PlaceRect(GameObject parent, string name,
                               Vector3 localPos, Vector3 localScale,
                               Color color, int sortingOrder)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            var sr  = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _sharedSprite;
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

                    // Reset position every frame (lift is re-applied below for held bolt)
                    boltSr.transform.localPosition = new Vector3(0f, slot * _boltStep, 0f);

                    int  colorId = (column != null && slot < column.Count) ? column[slot] : 0;
                    bool filled  = colorId > 0;

                    boltSr.sprite = filled ? _boltSprite : _ringSprite;
                    boltSr.color  = BoltColors[Mathf.Clamp(colorId, 0, BoltColors.Length - 1)];

                    var shineSr = _shineRenderers[col]?[slot];
                    if (shineSr != null) shineSr.enabled = filled;
                }
            }

            // Selection lift: raise held bolt 0.3 world units and brighten it.
            if (_sortMechanic != null &&
                _sortMechanic.CurrentState == BoltSort.SortMechanic.SortMechState.BoltSelected)
            {
                int heldCol = _sortMechanic.HeldSourceIndex;
                if (heldCol >= 0 && heldCol < totalCols && _boltRenderers[heldCol] != null)
                {
                    IReadOnlyList<int> heldColData = heldCol < _colorCount
                        ? (stacks != null && heldCol < stacks.Length ? stacks[heldCol] : null)
                        : (temps  != null && (heldCol - _colorCount) < temps.Length
                               ? temps[heldCol - _colorCount] : null);

                    if (heldColData != null && heldColData.Count > 0)
                    {
                        int topSlot = heldColData.Count - 1;
                        var sr = _boltRenderers[heldCol][topSlot];
                        if (sr != null)
                        {
                            sr.transform.localPosition = new Vector3(0f, topSlot * _boltStep + 0.3f, 0f);
                            Color c = sr.color;
                            sr.color = new Color(
                                Mathf.Min(c.r * 1.5f + 0.15f, 1f),
                                Mathf.Min(c.g * 1.5f + 0.15f, 1f),
                                Mathf.Min(c.b * 1.5f + 0.15f, 1f),
                                1f);
                        }
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

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[size * size];
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx   = x + 0.5f - c, dy = y + 0.5f - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - dist) * 2f);
                    // Radial shading: centre bright, rim 68%
                    float shade = Mathf.Lerp(1f, 0.68f, Mathf.Clamp01(dist / r));
                    // Specular blob upper-left
                    float hlDx = dx + r * 0.28f, hlDy = dy + r * 0.28f;
                    shade = Mathf.Min(1f,
                        shade + Mathf.Clamp01(1f - Mathf.Sqrt(hlDx*hlDx+hlDy*hlDy)/(r*0.38f)) * 0.40f);
                    px[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        // Hollow ring — used for empty slot indicator ("glass tube" look).
        private static Sprite CreateRingSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[size * size];
            float c = size * 0.5f, rOuter = c - 1.5f, rInner = rOuter - 6f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist   = Mathf.Sqrt((x+.5f-c)*(x+.5f-c) + (y+.5f-c)*(y+.5f-c));
                    float outerA = Mathf.Clamp01((rOuter - dist) * 2f);
                    float innerA = Mathf.Clamp01((dist - rInner) * 2f);
                    px[y * size + x] = new Color(1f, 1f, 1f, outerA * innerA);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }

        // Soft white oval with quadratic falloff — layered over filled bolts as a 3-D shine.
        private static Sprite CreateShineSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color[] px = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f, rr = size * 0.44f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float dist  = Mathf.Sqrt(dx * dx * 1.4f + dy * dy); // wider than tall
                    float t     = 1f - dist / rr;
                    float alpha = Mathf.Clamp01(t * t);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        }
    }
}
