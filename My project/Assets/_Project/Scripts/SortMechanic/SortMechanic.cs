using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using BoltSort.GameStateManager;

namespace BoltSort.SortMechanic
{
    /// <summary>
    /// Core rule engine for BoltSort's sort puzzle. Validates player moves against board state
    /// owned by <see cref="IGameStateManager"/> and emits outcomes via C# events.
    /// </summary>
    /// <remarks>
    /// Implements: <c>design/gdd/sort-mechanic.md</c> — States and Transitions, Move Validation,
    /// Win Condition, and Initialization Assertions.
    ///
    /// Story 001 scope: FSM core (Idle / BoltSelected / Cancellation), initialization assertions
    /// (AC-18b, AC-18c, AC-27), source-tap cancellation (AC-07, AC-21), empty-space cancellation
    /// (AC-17), committed vs. cancelled signal split (AC-15a), empty-source tap (AC-09),
    /// and the <c>level_load_failed</c> soft-block path.
    ///
    /// Stories 002–006 extend this class: input buffering, INVALID_MOVE / MOVE_EXECUTING
    /// full paths, win condition, deadlock detection, and app-pause cancellation.
    ///
    /// Script Execution Order: SEO −55 (more negative than GSM at −50; ensures
    /// OnApplicationPause fires Sort Mechanic before GSM per EC-14 and ADR-0001 SEO contract).
    ///
    /// ADR-0006: GSM is sole board owner; SortMechanic reads synchronously via IGameStateManager.
    /// ADR-0007: EnhancedTouchSupport for all tap input; Keyboard.current null guard for Android back.
    /// </remarks>
    public class SortMechanic : MonoBehaviour
    {
        // ── Constants ─────────────────────────────────────────────────────────────

        private const string LogCategory = "SortMechanic";

        /// <summary>
        /// Maximum number of columns (color stacks + temp slots) per ADR-0013.
        /// Derived from iPhone SE 375pt viewport and the 44pt/48dp tap-target mandate (ADR-0008).
        /// </summary>
        private const int MaxColumnCount = 8;

        // ── Dependencies (injected or assigned in Awake) ──────────────────────────

        /// <summary>
        /// Game State Manager reference. Assign via Inspector or inject via
        /// <see cref="InjectForTesting"/> before tests call <see cref="InitializeBoard"/>.
        /// </summary>
        [SerializeField]
        private global::BoltSort.GameStateManager.GameStateManager _gsmComponent;

        private IGameStateManager _gsm;
        private IDiagnosticLogger _logger;

        // ── Runtime State ─────────────────────────────────────────────────────────

        private SortMechState _currentState = SortMechState.Idle;

        /// <summary>
        /// When TRUE, all tap events are silently discarded. Set after any
        /// initialization assertion failure (AC-18c).
        /// </summary>
        private bool _inputBlocked;

        // ── Held-bolt State ───────────────────────────────────────────────────────

        /// <summary>Color ID of the bolt currently held. Valid only in BoltSelected.</summary>
        private int _heldColorId;

        /// <summary>
        /// Flat stack index of the source column the held bolt was lifted from.
        /// Valid only in BoltSelected. Indices 0..(ColorCount-1) = color stacks;
        /// ColorCount..(ColorCount+TempSlotCount-1) = temp slots.
        /// </summary>
        private int _heldSourceIndex;

        // ── Sequence ID ───────────────────────────────────────────────────────────

        /// <summary>
        /// Monotonically increasing session-global sequence counter. Never resets within
        /// one app session. Typed as <c>long</c> (int64) per GDD EC-12 stale-signal safety
        /// requirement — int32 wrapping to negative causes permanent MOVE_EXECUTING softlock.
        /// </summary>
        private long _sequenceId;

        // ── Input Layer ───────────────────────────────────────────────────────────

        private Camera _mainCamera;
        private int _boltStacksLayerMask;

        // ── One-Tap Buffers (ADR-0007) ────────────────────────────────────────────

        /// <summary>True when a tap was buffered during MOVE_EXECUTING.</summary>
        private bool _pendingTap;
        private int _pendingTapStackIndex;

        /// <summary>True when a tap was buffered during INVALID_MOVE rejection animation.</summary>
        private bool _invalidMovePendingTap;
        private int _invalidMovePendingTapIndex;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired immediately on entering MOVE_EXECUTING — before the bolt travel animation.
        /// GSM subscribes to update board state (authoritative state-change moment, BSM-01).
        /// Parameters: (sourceIndex, destinationIndex, colorId, sequenceId).
        /// </summary>
        public event Action<int, int, int, long> OnMoveCommitted;

        /// <summary>
        /// Fired on entering CANCELLATION. No board state change — bolt returns to source.
        /// Parameters: (sourceIndex, colorId).
        /// </summary>
        public event Action<int, int> OnMoveCancelled;

        /// <summary>
        /// Fired on entering INVALID_MOVE.
        /// Parameters: (sourceIndex, destinationIndex, colorId, reason).
        /// </summary>
        public event Action<int, int, int, MoveRejectedReason> OnMoveRejected;

        /// <summary>
        /// Fired on entering WIN after the win condition evaluates TRUE.
        /// Parameters: (moveCount) — read from <c>GSM.MoveCount</c> at WIN entry.
        /// </summary>
        public event Action<int> OnPuzzleSolved;

        /// <summary>
        /// Fired on MOVE_EXECUTING → IDLE exit (win condition failed, normal path only).
        /// NOT fired on watchdog exit or WIN exit.
        /// Parameters: (sequenceId) matching the completed move.
        /// </summary>
        public event Action<long> OnMoveExecutingExited;

        /// <summary>
        /// Fired when a shallow first-move scan finds no legal move from the current board.
        /// (a) After each legal placement during MOVE_EXECUTING exit.
        /// (b) On level_loaded when the initial board is deadlocked.
        /// Story 005 implements this event.
        /// </summary>
        public event Action OnDeadlockDetected;

        /// <summary>
        /// Fired when any initialization assertion fails.
        /// Sort Mechanic refuses all further input after emitting this event.
        /// HUD / session controller must subscribe and provide a recovery path.
        /// Parameters: (reason).
        /// </summary>
        public event Action<SortMechLoadFailReason> OnLevelLoadFailed;

        // ── Public State (read-only, for BoardView selection feedback) ────────────

        /// <summary>Current FSM state. BoardView polls this to drive selection highlight.</summary>
        public SortMechState CurrentState    => _currentState;

        /// <summary>Flat column index of the held bolt. Valid only when CurrentState == BoltSelected.</summary>
        public int           HeldSourceIndex => _heldSourceIndex;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            // Resolve GSM dependency. If not injected by tests, use component reference.
            if (_gsm == null && _gsmComponent != null)
                _gsm = _gsmComponent;
            // Fall back to singleton when wired programmatically (GameBootstrap path).
            if (_gsm == null)
                _gsm = global::BoltSort.GameStateManager.GameStateManager.Instance;

            // Input system setup (ADR-0007).
            EnhancedTouchSupport.Enable();
            _mainCamera = Camera.main;
            int boltStacksLayer = LayerMask.NameToLayer("BoltStacks");
            _boltStacksLayerMask = boltStacksLayer >= 0
                ? LayerMask.GetMask("BoltStacks")
                : ~0; // fall back to "everything" when layer isn't registered yet

            // Subscribe to GSM events using named methods (control manifest: no lambdas).
            if (_gsm != null)
                SubscribeToGsm();
        }

        private void OnDestroy()
        {
            EnhancedTouchSupport.Disable();

            if (_gsm != null)
                UnsubscribeFromGsm();
        }

        /// <summary>
        /// Cancels any in-progress bolt hold synchronously when the app is backgrounded (EC-14, AC-28).
        /// Must complete before GameStateManager serialises board state — enforced by SEO −55 being
        /// more negative (higher priority) than GSM's −50 (ADR-0001 OnApplicationPause ordering).
        /// Synchronous only — async void is forbidden here (control manifest, ADR-0007).
        /// No-op in all states other than BoltSelected; held state is never persisted across sessions.
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) return;
            if (_currentState != SortMechState.BoltSelected) return;
            ExecuteCancellation();
        }

        // ── GSM Event Subscriptions ───────────────────────────────────────────────

        private void SubscribeToGsm()
        {
            _gsm.OnLevelLoaded   += HandleLevelLoaded;
            _gsm.OnBoardRefreshForced += HandleBoardRefreshForced;
        }

        private void UnsubscribeFromGsm()
        {
            _gsm.OnLevelLoaded   -= HandleLevelLoaded;
            _gsm.OnBoardRefreshForced -= HandleBoardRefreshForced;
        }

        // ── GSM Event Handlers ────────────────────────────────────────────────────

        /// <summary>
        /// Runs initialization assertions and the pre-won / initial-deadlock checks
        /// on level load. Story 004 wires win detection here; Story 005 wires deadlock.
        /// </summary>
        private void HandleLevelLoaded(int levelId, int colorCount, int stackDepth,
                                       int tempSlotCount, int tempSlotDepth, long sequenceId)
        {
            InitializeBoard();
            // AC-25: emit deadlock_detected if initial board has no legal first move.
            // Guard: skip if init assertions failed (_inputBlocked) — board is corrupt, not deadlocked.
            if (!_inputBlocked && !HasLegalMove())
                OnDeadlockDetected?.Invoke();
        }

        /// <summary>
        /// Watchdog recovery path (EC-08). Story 004 implements the full win-check and
        /// buffered-tap firing on this path. Story 001 stub: transition to Idle or Win
        /// as appropriate — Win path deferred.
        /// </summary>
        private void HandleBoardRefreshForced(long sequenceId)
        {
            if (_currentState != SortMechState.MoveExecuting)
                return; // Frame-gap discard rule.

            // Watchdog path: run win check; OnMoveExecutingExited NOT emitted (AC-29a, TR-SORT-007).
            if (IsWon())
            {
                EnterWin(); // WIN: discard buffer, emit puzzle_solved
                return;
            }

            _currentState = SortMechState.Idle;
            DiscardPendingTap(); // Watchdog IDLE: discard buffer without firing
        }

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>
        /// Runs all three initialization assertions against current GSM board state.
        /// On any failure: emits <c>level_load_failed</c>, logs via IDiagnosticLogger,
        /// and sets <c>_inputBlocked = true</c>. Does not throw.
        /// Called from <see cref="HandleLevelLoaded"/> and from tests via
        /// <see cref="InjectForTesting"/>.
        /// </summary>
        internal void InitializeBoard()
        {
            _inputBlocked          = false;
            _currentState          = SortMechState.Idle;
            _pendingTap            = false;
            _invalidMovePendingTap = false;

            if (_gsm == null)
            {
                BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                    "GSM dependency not resolved before InitializeBoard.");
                return;
            }

            if (!AssertStackCountMatchesColorCount()) return;
            if (!AssertTempSlotDepthValid())          return;
            if (!AssertNoPhantomColorIds())           return;
            if (!AssertColumnCapValid())               return;
        }

        /// <summary>
        /// Assertion 1 — <c>StackContents.Length == ColorCount</c>.
        /// A mismatch silently excludes stacks from the win check.
        /// Returns false (and calls BlockInput) on failure.
        /// </summary>
        private bool AssertStackCountMatchesColorCount()
        {
            IReadOnlyList<int>[] stacks = _gsm.StackContents;
            int colorCount = _gsm.ColorCount;
            if (stacks != null && stacks.Length >= colorCount) return true;

            BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                $"Assertion 1 failed: StackContents.Length ({stacks?.Length}) < ColorCount ({colorCount}).");
            return false;
        }

        /// <summary>
        /// Assertion 2 — <c>TempSlotDepth &lt;= StackDepth</c>.
        /// Violation allows hoarding more bolts in temp than any color stack can hold.
        /// Returns false (and calls BlockInput) on failure.
        /// </summary>
        private bool AssertTempSlotDepthValid()
        {
            int tempSlotDepth = _gsm.TempSlotDepth;
            int stackDepth    = _gsm.StackDepth;
            if (tempSlotDepth <= stackDepth) return true;

            BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                $"Assertion 2 failed: TempSlotDepth ({tempSlotDepth}) > StackDepth ({stackDepth}).");
            return false;
        }

        /// <summary>
        /// Assertion 3 — all <c>colorId</c> values in color stacks and temp slots must
        /// belong to domain <c>{1..ColorCount}</c>. A phantom color ID passes the bolt-count
        /// invariant but makes the win condition structurally unreachable.
        /// Returns false (and calls BlockInput) on first violation found.
        /// </summary>
        private bool AssertNoPhantomColorIds()
        {
            int colorCount = _gsm.ColorCount;

            foreach (IReadOnlyList<int> col in _gsm.StackContents)
            {
                if (col == null) continue;
                foreach (int colorId in col)
                {
                    if (colorId < 1 || colorId > colorCount)
                    {
                        BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                            $"Assertion 3 failed: color_id {colorId} outside domain {{1..{colorCount}}}.");
                        return false;
                    }
                }
            }

            IReadOnlyList<int>[] tempSlots = _gsm.TempSlotContents;
            if (tempSlots == null) return true;
            foreach (IReadOnlyList<int> slot in tempSlots)
            {
                if (slot == null) continue;
                foreach (int colorId in slot)
                {
                    if (colorId < 1 || colorId > colorCount)
                    {
                        BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                            $"Assertion 3 failed: color_id {colorId} in temp slot outside domain {{1..{colorCount}}}.");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Assertion 4 — <c>ColorCount + TempSlotCount ≤ 8</c> (ADR-0013 column cap).
        /// Exceeding 8 columns makes tap-target compliance impossible on iPhone SE (375pt viewport).
        /// Returns false (and calls BlockInput) on violation.
        /// </summary>
        private bool AssertColumnCapValid()
        {
            int colorCount    = _gsm.ColorCount;
            int tempSlotCount = _gsm.TempSlotCount;
            if (colorCount + tempSlotCount <= MaxColumnCount) return true;

            BlockInput(SortMechLoadFailReason.CorruptedBoardState,
                $"Assertion 4 failed: color_count ({colorCount}) + temp_slot_count ({tempSlotCount}) = {colorCount + tempSlotCount} > {MaxColumnCount} (ADR-0013 column cap).");
            return false;
        }

        /// <summary>
        /// Emits <c>level_load_failed</c>, logs the error, and blocks all further input.
        /// </summary>
        private void BlockInput(SortMechLoadFailReason reason, string message)
        {
            _inputBlocked = true;
            _logger?.Error(LogCategory, message, new { reason });
            OnLevelLoadFailed?.Invoke(reason);
        }

        // ── Input Processing ──────────────────────────────────────────────────────

        private void Update()
        {
            if (_inputBlocked) return;

            // Android hardware back gesture — cancel held bolt (AC-12, ADR-0007).
            // Keyboard.current null guard is a REQUIRED implementation contract (AC-12):
            // on iOS there is no hardware keyboard device and Keyboard.current is null.
            if (_currentState == SortMechState.BoltSelected
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExecuteCancellation();
                return;
            }

            // Process touch input via EnhancedTouchSupport (ADR-0007).
            var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            bool touchHandled = false;
            foreach (var touch in activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    HandleTap(touch.screenPosition);
                    touchHandled = true;
                    break;
                }
            }

#if UNITY_EDITOR
            // Mouse fallback for Editor play mode.
            // Only fires when no real touch (or simulated touch) was processed this frame.
            if (!touchHandled)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    HandleTap(mouse.position.ReadValue());
            }
#endif
        }

        /// <summary>
        /// Resolves screen position to a flat stack index, then delegates to the
        /// index-based dispatch path shared with test injection.
        /// </summary>
        private void HandleTap(Vector2 screenPosition)
        {
            int flatIndex = ResolveStackIndex(screenPosition);
            DispatchIndexedTap(flatIndex);
        }

        /// <summary>
        /// Cancellation path: returns held bolt to source synchronously (AC-21).
        /// CANCELLATION → IDLE within the same call stack as move_cancelled emission.
        /// </summary>
        private void ExecuteCancellation()
        {
            _currentState = SortMechState.Cancellation;
            OnMoveCancelled?.Invoke(_heldSourceIndex, _heldColorId);
            // Synchronous transition — no animation handshake (GDD CANCELLATION state).
            _currentState = SortMechState.Idle;
        }

        /// <summary>
        /// Commits a move from <c>_heldSourceIndex</c> to <c>destinationIndex</c>.
        /// Emits <c>move_committed</c> and transitions to MOVE_EXECUTING.
        /// Story 003 wraps this in the is_legal_move guard.
        /// </summary>
        private void CommitMove(int destinationIndex)
        {
            _sequenceId++;
            _currentState = SortMechState.MoveExecuting;
            OnMoveCommitted?.Invoke(_heldSourceIndex, destinationIndex, _heldColorId, _sequenceId);
        }

        // ── Win Condition ─────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates the win condition against the current board state from GSM.
        /// All color stacks must be full (count == stackDepth) and monochromatic (all bolts same color).
        /// Win rule: every column is either EMPTY or COMPLETELY FULL with bolts of ONE color.
        /// Temp slots are included — a solved board may have bolts sorted into any column.
        /// O((colorCount + tempSlotCount) × maxDepth) ≤ 96 iterations; zero allocation.
        /// </summary>
        private bool IsWon()
        {
            IReadOnlyList<int>[] stacks = _gsm.StackContents;
            int colorCount    = _gsm.ColorCount;
            int tempSlotCount = _gsm.TempSlotCount;
            int stackDepth    = _gsm.StackDepth;
            int tempSlotDepth = _gsm.TempSlotDepth;

            if (stacks == null || stacks.Length < colorCount || stackDepth <= 0)
                return false;

            int totalCols = colorCount + tempSlotCount;
            for (int i = 0; i < totalCols && i < stacks.Length; i++)
            {
                IReadOnlyList<int> stack = stacks[i];
                if (stack == null || stack.Count == 0) continue; // empty column — ok
                int capacity = i < colorCount ? stackDepth : tempSlotDepth;
                if (stack.Count != capacity) return false;        // partial fill — not won
                if (!AllSameColor(stack))   return false;         // mixed colors — not won
            }
            return true;
        }

        /// <summary>Returns true if all elements in <paramref name="stack"/> are equal.</summary>
        private static bool AllSameColor(IReadOnlyList<int> stack)
        {
            int first = stack[0];
            for (int i = 1; i < stack.Count; i++)
                if (stack[i] != first) return false;
            return true;
        }

        /// <summary>
        /// Transitions to WIN state, discards the pending tap buffer, and emits
        /// <see cref="OnPuzzleSolved"/>. Called from both the normal animation-complete
        /// path and the watchdog path (AC-29a). Never emits <see cref="OnMoveExecutingExited"/>.
        /// </summary>
        /// <remarks>
        /// Input lock: <c>_inputBlocked</c> is intentionally NOT set here. The WIN FSM state
        /// itself acts as the input gate — <see cref="DispatchIndexedTap"/> breaks on
        /// <c>SortMechState.Win</c> with no action. This differs from init-failure locking
        /// (which uses <c>_inputBlocked</c>); the asymmetry is deliberate: WIN is a normal
        /// game end-state, not an error condition.
        /// </remarks>
        private void EnterWin()
        {
            _currentState = SortMechState.Win;
            DiscardPendingTap(); // AC-24: buffered tap silently discarded on WIN
            OnPuzzleSolved?.Invoke(_gsm.MoveCount);
        }

        // ── Deadlock Detection ────────────────────────────────────────────────────

        /// <summary>
        /// Shallow depth-1 deadlock check (TR-SORT-005). For each non-empty source column,
        /// evaluates every other column as a potential destination via <see cref="IsLegalMove"/>.
        /// Returns <c>true</c> if at least one legal (source, destination) pair exists.
        /// Returns <c>false</c> if no legal first move exists — board is deadlocked.
        /// O(N(N-1)) where N = colorCount + tempSlotCount ≤ 11 → max ~110 iterations; no allocation.
        /// </summary>
        private bool HasLegalMove()
        {
            IReadOnlyList<int>[] stacks   = _gsm.StackContents;
            int colorCount                 = _gsm.ColorCount;
            int stackDepth                 = _gsm.StackDepth;
            IReadOnlyList<int>[] tempSlots = _gsm.TempSlotContents;
            int tempSlotCount              = _gsm.TempSlotCount;
            int tempSlotDepth              = _gsm.TempSlotDepth;
            int totalColumns               = colorCount + tempSlotCount;

            for (int i = 0; i < totalColumns; i++)
            {
                IReadOnlyList<int> sourceCol = GetColumnByFlatIndex(i, stacks, colorCount, tempSlots);
                if (sourceCol == null || sourceCol.Count == 0) continue; // empty — skip

                int heldColor = sourceCol[sourceCol.Count - 1]; // top bolt color

                for (int j = 0; j < totalColumns; j++)
                {
                    if (j == i) continue;
                    IReadOnlyList<int> destCol = GetColumnByFlatIndex(j, stacks, colorCount, tempSlots);
                    int destCap = j < colorCount ? stackDepth : tempSlotDepth;
                    var (legal, _) = IsLegalMove(heldColor, destCol, destCap);
                    if (legal) return true; // at least one legal move exists
                }
            }
            return false; // no legal move — board is deadlocked
        }

        // ── Tap Buffer Management ─────────────────────────────────────────────────

        /// <summary>
        /// Fires the MOVE_EXECUTING pending tap as an IDLE tap. Called on IDLE exit
        /// (normal path only — NOT on WIN, NOT on watchdog). AC-29b: if the buffered
        /// stack index is now empty, DispatchIdleIndexedTap returns early → stays IDLE.
        /// </summary>
        private void ProcessPendingTap()
        {
            if (!_pendingTap) return;
            _pendingTap = false;
            DispatchIndexedTap(_pendingTapStackIndex);
        }

        /// <summary>Clears MOVE_EXECUTING buffer without firing. Called on WIN and watchdog.</summary>
        private void DiscardPendingTap()
        {
            _pendingTap = false;
        }

        /// <summary>
        /// Fires the INVALID_MOVE pending tap as a BOLT_SELECTED destination evaluation.
        /// Called after BOLT_SELECTED is re-entered on rejection animation complete (AC-30).
        /// </summary>
        private void ProcessInvalidMovePendingTap()
        {
            if (!_invalidMovePendingTap) return;
            _invalidMovePendingTap = false;
            DispatchIndexedTap(_invalidMovePendingTapIndex);
        }

        // ── Animation Completion (Story 004) ──────────────────────────────────────

        /// <summary>
        /// Called by Animation System when a bolt placement animation completes (BM-04).
        /// Evaluates win condition, optionally emits deadlock, fires buffered tap,
        /// and exits MOVE_EXECUTING. Full implementation in Story 004.
        /// </summary>
        /// <param name="sequenceId">Must match current in-flight sequence ID.</param>
        public void OnAnimationComplete(long sequenceId)
        {
            if (_currentState != SortMechState.MoveExecuting)
                return;

            if (sequenceId != _sequenceId)
                return; // Stale signal guard (EC-11).

            if (IsWon())
            {
                EnterWin(); // WIN path: discard buffer, emit puzzle_solved; NOT OnMoveExecutingExited
                return;
            }

            // IDLE path: win check failed; run deadlock check, then fire buffered tap.
            _currentState = SortMechState.Idle;
            OnMoveExecutingExited?.Invoke(sequenceId);              // TR-SORT-007: IDLE path only
            if (!HasLegalMove()) OnDeadlockDetected?.Invoke();      // AC-22: before buffered tap
            ProcessPendingTap();                                     // AC-29b: after deadlock signal
        }

        /// <summary>
        /// Called by Animation System when the rejection animation for an invalid move completes.
        /// Re-enters BOLT_SELECTED (held bolt still in hand) and fires any buffered tap
        /// as a S-04 destination evaluation (AC-30). State guard prevents stale calls.
        /// </summary>
        public void OnRejectionAnimationComplete()
        {
            if (_currentState != SortMechState.InvalidMove)
                return;

            _currentState = SortMechState.BoltSelected;
            ProcessInvalidMovePendingTap();
        }

        // ── Input Resolution ──────────────────────────────────────────────────────

        /// <summary>
        /// Converts a screen position to a flat stack index using Physics2D.OverlapPoint
        /// against the BoltStacks layer mask (ADR-0007).
        /// Returns the flat index (0 = first color stack; ColorCount = first temp slot),
        /// or −1 if no stack was hit.
        /// </summary>
        private int ResolveStackIndex(Vector2 screenPosition)
        {
            if (_mainCamera == null)
                return -1;

            Vector3 worldPos = _mainCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, 0f)); // 2D orthographic: z irrelevant

            Collider2D hit = Physics2D.OverlapPoint(worldPos, _boltStacksLayerMask);
            if (hit == null)
                return -1;

            // BoltStack colliders must carry a BoltStackIndex component or equivalent
            // that exposes the flat column index. Story 002 wires the full scene setup.
            // For unit-test paths, ResolveStackIndex is overridden via InjectTapForTesting.
            BoltStackIndex bsi = hit.GetComponent<BoltStackIndex>();
            return bsi != null ? bsi.Index : -1;
        }

        /// <summary>
        /// Returns the column list for a given flat index, spanning both color stacks
        /// and temp slots as exposed by the GSM interface.
        /// </summary>
        private static IReadOnlyList<int> GetColumnByFlatIndex(
            int flatIndex,
            IReadOnlyList<int>[] stacks,
            int colorCount,
            IReadOnlyList<int>[] tempSlots)
        {
            if (flatIndex < colorCount)
                return stacks != null && flatIndex < stacks.Length ? stacks[flatIndex] : null;

            int tempIdx = flatIndex - colorCount;
            return tempSlots != null && tempIdx < tempSlots.Length ? tempSlots[tempIdx] : null;
        }

        // ── Test Seams ────────────────────────────────────────────────────────────

        /// <summary>
        /// Injects dependencies for unit tests. Call before <see cref="InitializeBoard"/>.
        /// Bypasses Unity lifecycle so tests run in EditMode without a real MonoBehaviour chain.
        /// </summary>
        internal void InjectForTesting(IGameStateManager gsm, IDiagnosticLogger logger = null)
        {
            _gsm    = gsm;
            _logger = logger;
        }

        /// <summary>
        /// Directly injects a tap event into the FSM, bypassing Physics2D.OverlapPoint.
        /// Used in unit tests that don't have a live 2D physics scene.
        /// </summary>
        /// <param name="flatStackIndex">
        /// Flat column index to inject: 0..(ColorCount-1) = color stacks;
        /// ColorCount..(ColorCount+TempSlotCount-1) = temp slots; −1 = empty board space.
        /// </param>
        internal void InjectTapForTesting(int flatStackIndex)
        {
            if (_inputBlocked) return;
            DispatchIndexedTap(flatStackIndex);
        }

        /// <summary>
        /// Routes an already-resolved stack index through the current FSM state.
        /// Shared by <see cref="InjectTapForTesting"/> and future input-layer callers.
        /// </summary>
        private void DispatchIndexedTap(int flatStackIndex)
        {
            switch (_currentState)
            {
                case SortMechState.Idle:
                    DispatchIdleIndexedTap(flatStackIndex);
                    break;

                case SortMechState.BoltSelected:
                    DispatchBoltSelectedIndexedTap(flatStackIndex);
                    break;

                case SortMechState.MoveExecuting:
                    // Buffer the first tap; silently discard all subsequent (AC-08a/b/c).
                    if (!_pendingTap)
                    {
                        _pendingTap = true;
                        _pendingTapStackIndex = flatStackIndex;
                    }
                    break;

                case SortMechState.InvalidMove:
                    // Buffer the first tap during rejection animation; discard extras (AC-30b).
                    if (!_invalidMovePendingTap)
                    {
                        _invalidMovePendingTap = true;
                        _invalidMovePendingTapIndex = flatStackIndex;
                    }
                    break;

                case SortMechState.Win:
                case SortMechState.Cancellation:
                    break;
            }
        }

        private void DispatchIdleIndexedTap(int flatStackIndex)
        {
            if (flatStackIndex < 0) return;

            IReadOnlyList<int>[] stacks   = _gsm.StackContents;
            int colorCount                 = _gsm.ColorCount;
            IReadOnlyList<int>[] tempSlots = _gsm.TempSlotContents;

            IReadOnlyList<int> column = GetColumnByFlatIndex(flatStackIndex, stacks, colorCount, tempSlots);
            if (column == null || column.Count == 0)
                return; // S-02: empty stack.

            _heldColorId     = column[column.Count - 1];
            _heldSourceIndex = flatStackIndex;
            _currentState    = SortMechState.BoltSelected;
        }

        private void DispatchBoltSelectedIndexedTap(int flatStackIndex)
        {
            if (flatStackIndex < 0)
            {
                ExecuteCancellation();
                return;
            }

            if (flatStackIndex == _heldSourceIndex)
            {
                ExecuteCancellation();
                return;
            }

            IReadOnlyList<int>[] stacks   = _gsm.StackContents;
            int colorCount                 = _gsm.ColorCount;
            IReadOnlyList<int>[] tempSlots = _gsm.TempSlotContents;

            IReadOnlyList<int> destColumn = GetColumnByFlatIndex(flatStackIndex, stacks, colorCount, tempSlots);
            int cap = flatStackIndex < colorCount ? _gsm.StackDepth : _gsm.TempSlotDepth;

            var (legal, reason) = IsLegalMove(_heldColorId, destColumn, cap);
            if (legal)
                CommitMove(flatStackIndex);
            else
                EnterInvalidMove(flatStackIndex, reason);
        }

        /// <summary>
        /// Validates whether the held bolt may be placed on <paramref name="destination"/>.
        /// Uses a guarded conditional: empty check BEFORE full check BEFORE color check —
        /// reading <c>destination[Count-1]</c> (top color) on an empty column is undefined.
        /// This ordering is a required pattern per the control manifest.
        /// </summary>
        /// <param name="heldColor">Color ID of the bolt currently held.</param>
        /// <param name="destination">Current column contents; null or empty = vacant slot.</param>
        /// <param name="cap">Maximum bolt capacity of the destination column.</param>
        private static (bool legal, MoveRejectedReason reason) IsLegalMove(
            int heldColor, IReadOnlyList<int> destination, int cap)
        {
            if (destination == null || destination.Count == 0)
                return (true, default);                             // empty — any color

            if (destination.Count >= cap)
                return (false, MoveRejectedReason.DestinationFull); // full

            if (destination[destination.Count - 1] == heldColor)
                return (true, default);                             // color match, not full

            return (false, MoveRejectedReason.ColorMismatch);       // mismatch
        }

        /// <summary>
        /// Enters INVALID_MOVE and emits <see cref="OnMoveRejected"/>.
        /// The held bolt remains in hand. <see cref="OnRejectionAnimationComplete"/> returns
        /// to BOLT_SELECTED after the rejection animation completes.
        /// </summary>
        private void EnterInvalidMove(int destinationIndex, MoveRejectedReason reason)
        {
            _currentState = SortMechState.InvalidMove;
            OnMoveRejected?.Invoke(_heldSourceIndex, destinationIndex, _heldColorId, reason);
        }

        /// <summary>Exposes the current FSM state for test assertions.</summary>
        internal SortMechState CurrentStateForTesting => _currentState;

        /// <summary>Exposes the input-blocked flag for test assertions.</summary>
        internal bool InputBlockedForTesting => _inputBlocked;

        /// <summary>Exposes the held bolt color for test assertions.</summary>
        internal int HeldColorIdForTesting => _heldColorId;

        /// <summary>Exposes the held source index for test assertions.</summary>
        internal int HeldSourceIndexForTesting => _heldSourceIndex;

        /// <summary>Exposes the MOVE_EXECUTING pending-tap flag for test assertions.</summary>
        internal bool PendingTapForTesting => _pendingTap;

        /// <summary>Exposes the MOVE_EXECUTING pending-tap stack index for test assertions.</summary>
        internal int PendingTapStackIndexForTesting => _pendingTapStackIndex;

        /// <summary>Exposes the INVALID_MOVE pending-tap flag for test assertions.</summary>
        internal bool InvalidMovePendingTapForTesting => _invalidMovePendingTap;

        /// <summary>
        /// Forces FSM into INVALID_MOVE with a held bolt. Used to test Story 002 buffer
        /// behavior without Story 003's is_legal_move gate in place.
        /// </summary>
        internal void ForceEnterInvalidMoveForTesting(int sourceIndex, int heldColorId)
        {
            _heldSourceIndex       = sourceIndex;
            _heldColorId           = heldColorId;
            _currentState          = SortMechState.InvalidMove;
            _invalidMovePendingTap = false;
        }

        /// <summary>
        /// Forces FSM into MOVE_EXECUTING. Used to test buffered-tap behaviour with a
        /// specific board state without requiring the full tap sequence to get there.
        /// </summary>
        internal void ForceEnterMoveExecutingForTesting(int sourceIndex, int heldColorId, long seqId)
        {
            _heldSourceIndex = sourceIndex;
            _heldColorId     = heldColorId;
            _sequenceId      = seqId;
            _currentState    = SortMechState.MoveExecuting;
            _pendingTap      = false;
        }

        /// <summary>
        /// Directly invokes the watchdog handler. Used to test pending-tap discard on
        /// watchdog without a live GSM event source.
        /// </summary>
        internal void TriggerBoardRefreshForcedForTesting(long sequenceId)
        {
            HandleBoardRefreshForced(sequenceId);
        }

        /// <summary>
        /// Subscribes to the injected GSM's events. Call after <see cref="InjectForTesting"/>
        /// when a test needs SortMechanic to respond to <c>OnLevelLoaded</c> or
        /// <c>OnBoardRefreshForced</c> events fired directly by a stub GSM (AC-25, AC-10).
        /// Bypasses Unity lifecycle — Awake normally calls this.
        /// </summary>
        internal void SubscribeToGsmForTesting()
        {
            if (_gsm != null)
                SubscribeToGsm();
        }

        /// <summary>
        /// Simulates the Android back gesture for unit tests (Keyboard.current is null
        /// in EditMode). Triggers HandleBackGesture logic without OS-level input.
        /// </summary>
        internal void TriggerBackGestureForTesting()
        {
            if (_inputBlocked) return;
            if (_currentState == SortMechState.BoltSelected)
                ExecuteCancellation();
        }

        /// <summary>
        /// Invokes <see cref="OnApplicationPause"/> directly from test code.
        /// Unity calls this automatically at runtime; this seam bypasses OS-level backgrounding
        /// for integration tests that cannot trigger real app suspension (AC-28).
        /// </summary>
        internal void TriggerApplicationPauseForTesting(bool pauseStatus)
        {
            OnApplicationPause(pauseStatus);
        }
    }
}
