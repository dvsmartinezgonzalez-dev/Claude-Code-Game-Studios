using System.Collections.Generic;
using UnityEngine;

namespace BoltSort.Gameplay
{
    /// <summary>
    /// Renders the bolt-sorting board as coloured world-space sprites.
    /// Each column has a BoxCollider2D on the "BoltStacks" layer so
    /// SortMechanic can detect taps via Physics2D.OverlapPoint.
    /// Calls OnAnimationComplete() immediately after each move to simulate
    /// instant animation (no animation system wired yet).
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        private float _colStep;
        private float _colWidth;
        private float _boltHeight;
        private float _boltStep;
        private float _boardCenterY;

        private static readonly Color[] BoltColors =
        {
            new Color(0.18f, 0.18f, 0.22f), // 0 – empty
            new Color(0.95f, 0.22f, 0.18f), // 1 – red
            new Color(0.20f, 0.55f, 0.98f), // 2 – blue
            new Color(0.18f, 0.82f, 0.28f), // 3 – green
            new Color(0.98f, 0.86f, 0.10f), // 4 – yellow
            new Color(0.98f, 0.52f, 0.10f), // 5 – orange
            new Color(0.68f, 0.22f, 0.98f), // 6 – purple
            new Color(0.10f, 0.88f, 0.88f), // 7 – cyan
            new Color(0.98f, 0.22f, 0.78f), // 8 – magenta
        };

        private static readonly Color ColBgColor  = new Color(0.15f, 0.16f, 0.20f);
        private static readonly Color TempBgColor = new Color(0.22f, 0.20f, 0.16f);

        private BoltSort.GameStateManager.GameStateManager _gsm;
        private BoltSort.SortMechanic.SortMechanic         _sortMechanic;

        private int _colorCount;
        private int _stackDepth;
        private int _tempSlotCount;
        private int _tempSlotDepth;

        private SpriteRenderer[][] _boltRenderers; // [col][slot], slot 0 = bottom

        private static Sprite _sharedSprite;

        public void Initialize(
            BoltSort.GameStateManager.GameStateManager gsm,
            BoltSort.SortMechanic.SortMechanic sm)
        {
            _gsm          = gsm;
            _sortMechanic = sm;
            _sharedSprite = _sharedSprite != null ? _sharedSprite : CreateWhiteSprite();

            gsm.OnLevelLoaded  += OnLevelLoaded;
            sm.OnMoveCommitted += OnMoveCommitted;
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
            // GSM has already mutated the board (it subscribed first in GameBootstrap.Awake).
            // Complete the FSM animation handshake so SortMechanic returns to Idle immediately
            // instead of waiting for the 1.5 s watchdog.
            _sortMechanic.OnAnimationComplete(seqId);
        }

        private void RebuildColumns()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            int totalCols = _colorCount + _tempSlotCount;

            // Fit all columns into the visible camera frustum at runtime
            Camera cam          = Camera.main;
            float camHalfWidth  = cam != null ? cam.orthographicSize * cam.aspect : 6f;
            float camHalfHeight = cam != null ? cam.orthographicSize : 6f;

            _colStep  = (camHalfWidth - 0.25f) * 2f / totalCols;
            _colWidth = _colStep * 0.85f;

            // Reserve top 32% and bottom 22% of camera height for HUD elements
            float hudTop    = camHalfHeight * 0.32f;
            float hudBottom = camHalfHeight * 0.22f;
            float usableH   = camHalfHeight * 2f - hudTop - hudBottom;

            int maxDepth = Mathf.Max(_stackDepth, _tempSlotDepth);
            _boltStep   = usableH / (maxDepth + 0.5f);
            _boltHeight = _boltStep * 0.82f;

            _boardCenterY = (-camHalfHeight + hudBottom + camHalfHeight - hudTop) * 0.5f;

            float startX         = -(totalCols - 1) * 0.5f * _colStep;
            int   boltStackLayer = LayerMask.NameToLayer("BoltStacks");
            if (boltStackLayer < 0)
            {
                Debug.LogWarning("[BoardView] Layer 'BoltStacks' not found — using layer 0 fallback. " +
                                 "Add 'BoltStacks' at User Layer 8 in Edit → Project Settings → Tags and Layers.");
                boltStackLayer = 0;
            }

            _boltRenderers = new SpriteRenderer[totalCols][];

            for (int col = 0; col < totalCols; col++)
            {
                bool  isTemp = col >= _colorCount;
                int   depth  = isTemp ? _tempSlotDepth : _stackDepth;
                float colX   = startX + col * _colStep;

                var colGO = new GameObject($"Column_{col}");
                colGO.transform.SetParent(transform, false);
                colGO.transform.localPosition = new Vector3(colX, _boardCenterY, 0f);
                colGO.layer = boltStackLayer;

                // Background
                var bgGO = new GameObject("Background");
                bgGO.transform.SetParent(colGO.transform, false);
                bgGO.transform.localPosition = new Vector3(0f, (depth - 1) * 0.5f * _boltStep, 0f);
                bgGO.transform.localScale    = new Vector3(_colWidth, depth * _boltStep + 0.12f, 1f);
                var bgSr = bgGO.AddComponent<SpriteRenderer>();
                bgSr.sprite       = _sharedSprite;
                bgSr.color        = isTemp ? TempBgColor : ColBgColor;
                bgSr.sortingOrder = 0;

                // Collider for tap detection — sized to match background
                var col2d    = colGO.AddComponent<BoxCollider2D>();
                col2d.size   = new Vector2(_colWidth, depth * _boltStep + 0.12f);
                col2d.offset = new Vector2(0f, (depth - 1) * 0.5f * _boltStep);

                var bsi = colGO.AddComponent<BoltSort.SortMechanic.BoltStackIndex>();
                bsi.Initialize(col);

                // Per-slot renderers
                _boltRenderers[col] = new SpriteRenderer[depth];
                for (int slot = 0; slot < depth; slot++)
                {
                    var boltGO = new GameObject($"Slot_{slot}");
                    boltGO.transform.SetParent(colGO.transform, false);
                    boltGO.transform.localPosition = new Vector3(0f, slot * _boltStep, 0f);
                    boltGO.transform.localScale    = new Vector3(_colWidth - 0.08f, _boltHeight, 1f);

                    var sr = boltGO.AddComponent<SpriteRenderer>();
                    sr.sprite       = _sharedSprite;
                    sr.sortingOrder = 1;
                    _boltRenderers[col][slot] = sr;
                }
            }
        }

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
                    : (temps  != null && (col - _colorCount) < temps.Length ? temps[col - _colorCount] : null);

                int depth = col < _colorCount ? _stackDepth : _tempSlotDepth;
                for (int slot = 0; slot < depth; slot++)
                {
                    var sr = _boltRenderers[col][slot];
                    if (sr == null) continue;

                    // Reset to base position every frame so the lift is always relative to ground truth
                    sr.transform.localPosition = new Vector3(0f, slot * _boltStep, 0f);

                    int colorId = (column != null && slot < column.Count) ? column[slot] : 0;
                    sr.color = BoltColors[Mathf.Clamp(colorId, 0, BoltColors.Length - 1)];
                }
            }

            // Selection lift: move the held bolt upward and brighten it
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
                            // Lift bolt upward by 70% of a slot step
                            sr.transform.localPosition = new Vector3(0f,
                                topSlot * _boltStep + _boltStep * 0.7f, 0f);

                            // Brighten to make the held bolt obvious
                            Color c = sr.color;
                            sr.color = new Color(
                                Mathf.Min(c.r * 1.5f + 0.15f, 1f),
                                Mathf.Min(c.g * 1.5f + 0.15f, 1f),
                                Mathf.Min(c.b * 1.5f + 0.15f, 1f));
                        }
                    }
                }
            }
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2f);
        }
    }
}
