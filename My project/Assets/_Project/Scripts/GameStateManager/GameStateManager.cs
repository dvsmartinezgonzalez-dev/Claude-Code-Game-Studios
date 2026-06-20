using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BoltSort.LevelData;

namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Core singleton at SEO −50. Sole owner and writer of board state.
    /// Implements the Level Lifecycle FSM and the atomic 5-step board mutation defined in ADR-0006.
    /// Implements <see cref="IGameStateManager"/>.
    /// </summary>
    /// <remarks>
    /// Design documents implemented by this class:
    ///   <list type="bullet">
    ///     <item><c>design/gdd/game-state-manager.md</c> — TR-GSM-001 through TR-GSM-008</item>
    ///     <item>ADR-0001 — Singleton Boot and Subscribe-Then-Check</item>
    ///     <item>ADR-0006 — Board State Representation and GSM Design</item>
    ///   </list>
    ///
    /// Story 001 scope: board state data structures, atomic 5-step mutation, test seams.
    /// Story 005 scope: LoadLevel L-01–L-07 pipeline, OnLevelLoaded event, LDS wiring.
    /// Out-of-scope stubs are marked with TODO comments referencing the implementing story.
    ///
    /// TR-GSM-001: GSM is the sole owner and writer of _stackContents / _tempSlotContents.
    /// TR-GSM-005: Atomic board mutation — all 5 steps execute synchronously on main thread
    ///             within a single HandleMoveCommitted() callback.
    /// TR-GSM-008: Level load pipeline — L-01 through L-07 execute synchronously on main thread.
    /// </remarks>
    public class GameStateManager : MonoBehaviour, IGameStateManager
    {
        // ── Singleton ─────────────────────────────────────────────────────────────

        /// <summary>Singleton instance. Set in <c>Awake</c>; registered at SEO −50.</summary>
        public static GameStateManager Instance { get; private set; }

        // ── Lifecycle State ───────────────────────────────────────────────────────

        private GSMLifecycleState _lifecycleState = GSMLifecycleState.Unloaded;

        /// <inheritdoc/>
        public GSMLifecycleState LifecycleState => _lifecycleState;

        // ── Board State — private backing fields (TR-GSM-001: sole writer = GSM) ─

        /// <summary>
        /// Combined flat-namespace array. Indices 0..(colorCount-1) = color stacks.
        /// Indices colorCount..(colorCount+tempSlotCount-1) = temp slots.
        /// Inner List index 0 = bottom bolt; last = top bolt. Empty = empty List.
        /// </summary>
        private List<int>[] _stackContents;

        /// <summary>
        /// Separate backing array for temp-slot read-only access.
        /// Each element is a reference to the corresponding sub-list inside _stackContents
        /// so they stay in sync without duplication.
        /// Populated by <see cref="SeedBoardForTesting"/> and <see cref="LoadLevel"/>.
        /// </summary>
        private List<int>[] _tempSlotBacking;

        private long _currentSequenceId;
        private int  _moveCount;
        private int  _stackDepth;
        private int  _tempSlotDepth;
        private int  _tempSlotCount;
        private int  _colorCount;

        // ── Phase-2 mechanics (schema_version 2; null/zero ⇒ classic level) ───────

        /// <summary>Per-column capacity (flat order). Null ⇒ uniform <see cref="_stackDepth"/>/<see cref="_tempSlotDepth"/>.</summary>
        private int[] _columnCapacities;

        /// <summary>
        /// Initial freeze turns per flat column (0 = never frozen). Remaining freeze is derived as
        /// <c>max(0, _freezeInit[i] - _moveCount)</c> — a pure function of move count, so it is
        /// automatically restored by undo (which decrements move count) and needs no per-move mutation.
        /// </summary>
        private int[] _freezeInit;

        // ── Extra (helper) tubes — runtime-added columns (header Extra-Tube mechanic) ─

        /// <summary>
        /// Count of helper tubes appended at runtime via <see cref="ApplyExtraTube"/>. Helper
        /// tubes are extra temp-slot columns added to the END of the flat namespace, so no
        /// existing flat index shifts (undo entries stay valid) and all movement / win /
        /// completion logic reuses them unchanged. Reset to 0 on every level load/teardown,
        /// so retry removes them for free.
        /// </summary>
        private int _helperTubeCount;

        // ── Watchdog Timer (TR-GSM-004, WDG-01–WDG-03) ───────────────────────────

        private Coroutine _watchdogCoroutine;
        private float _watchdogTimeoutSec = 1.5f; // 1500ms — overridable via SetWatchdogTimeoutForTesting

        // ── Undo Stack ────────────────────────────────────────────────────────────

        private readonly List<UndoEntry> _undoStack = new List<UndoEntry>();
        private bool _pendingUndo = false;         // EC-17: deferred undo queue — capacity 1; second request silently dropped
        private bool _isAnimationInFlight = false;  // true between BSM-01 and MOVE_EXECUTING exit (normal or watchdog)

        // ── App Lifecycle / Serialization (SER-01/02/03, EC-06) ──────────────────

        private IBoardSnapshotSystem _boardSnapshotSystem; // injected in Awake (production) or via seam (tests)
        private bool _sortMechanicWasInBoltSelected = false; // EC-06: set by Sort Mechanic before OnApplicationPause
        private int _currentLevelId;
        private ILevelDataSystem _levelDataSystem; // wired in Awake from LDS.Instance; injectable for tests

        // ── Public Read-Only Properties (IGameStateManager) ───────────────────────

        /// <inheritdoc/>
        public IReadOnlyList<int>[] StackContents { get; private set; }

        /// <inheritdoc/>
        public int StackDepth    => _stackDepth;

        /// <inheritdoc/>
        public IReadOnlyList<int>[] TempSlotContents { get; private set; }

        /// <inheritdoc/>
        public int TempSlotDepth => _tempSlotDepth;

        /// <inheritdoc/>
        public int TempSlotCount => _tempSlotCount;

        /// <inheritdoc/>
        public int ColorCount    => _colorCount;

        /// <inheritdoc/>
        public int GetColumnCapacity(int flatIndex)
        {
            if (_columnCapacities != null && flatIndex >= 0 && flatIndex < _columnCapacities.Length)
                return _columnCapacities[flatIndex];
            return flatIndex < _colorCount ? _stackDepth : _tempSlotDepth; // uniform fallback
        }

        /// <inheritdoc/>
        public bool IsTubeFrozen(int flatIndex) => GetFreezeRemaining(flatIndex) > 0;

        /// <summary>
        /// Remaining freeze turns for a column: <c>max(0, freezeInit - moveCount)</c>.
        /// Decrements implicitly on every committed move and is restored by undo.
        /// </summary>
        public int GetFreezeRemaining(int flatIndex)
        {
            if (_freezeInit == null || flatIndex < 0 || flatIndex >= _freezeInit.Length) return 0;
            int rem = _freezeInit[flatIndex] - _moveCount;
            return rem > 0 ? rem : 0;
        }

        /// <summary>
        /// Fired when a mystery ball (stored as a negative color id) becomes a column top and is
        /// permanently revealed: the stored value flips to its positive color. Phase-2 (schema_version 2).
        /// Parameters: <c>(flatIndex, revealedColor)</c>. BoardView subscribes to play the reveal animation.
        /// Concrete-only (not on <see cref="IGameStateManager"/>) — visual systems use the concrete GSM.
        /// </summary>
        public event Action<int, int> OnMysteryRevealed;

        /// <summary>
        /// Fired when the board's COLUMN COUNT changes at runtime: a helper (extra) tube was
        /// added or grown via <see cref="ApplyExtraTube"/>. BoardView subscribes to relayout
        /// the columns. NOT fired for normal moves (only the shape changed, not the contents).
        /// Concrete-only — visual systems use the concrete GSM.
        /// </summary>
        public event Action OnBoardShapeChanged;

        /// <summary>Number of helper (extra) tubes currently on the board (appended after the
        /// level's own temp slots). 0 for a freshly loaded level.</summary>
        public int HelperTubeCount => _helperTubeCount;

        /// <inheritdoc/>
        public int MoveCount { get; private set; }

        /// <summary>
        /// True when an undo can currently be performed: GSM is ACTIVE and the undo stack is
        /// non-empty. Additive read-only helper used by the HUD to gate its per-level undo
        /// budget (a tap is only spent when an undo will actually occur). Does not alter undo
        /// behaviour. Concrete-only — not on <see cref="IGameStateManager"/>.
        /// </summary>
        public bool CanUndo => _lifecycleState == GSMLifecycleState.Active && _undoStack.Count > 0;

        /// <inheritdoc/>
        public long CurrentSequenceId { get; private set; }

        // ── Events ────────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// NOTE: This event is NOT fired after a regular move_committed mutation (AC-GSM-03).
        /// It is fired after undo operations (Story 002) and foreground restore (Story 008).
        /// </remarks>
        public event Action<long, int> OnBoardStateChanged;

        /// <inheritdoc/>
        /// <remarks>
        /// Fires when any load step (L-01–L-05) or foreground-restore deserialization (SER-03) fails.
        /// GSM transitions to UNLOADED before firing. Level Progression subscribes for error recovery.
        /// </remarks>
        public event Action<GsmSessionLoadFailReason, int> OnSessionLoadFailed;

        /// <inheritdoc/>
        /// <remarks>
        /// Canonical 4-arg payload per ADR-0012: (levelId, moveCount, parMoves, sequenceId).
        /// GSM reads parMoves from LDS at transition time so downstream never re-queries LDS.
        /// sequenceId is the value at WIN transition — NOT incremented (WIN-01).
        /// </remarks>
        public event Action<int, int, int, long> OnLevelComplete;

        /// <inheritdoc/>
        /// <remarks>
        /// Fired at L-06 after board arrays are allocated (L-05) and after GSM transitions to
        /// ACTIVE (L-07). Subscribers will always see <c>LifecycleState == Active</c>.
        /// ADR-0001 subscribe-then-check: new subscribers must check LifecycleState after subscribing.
        /// </remarks>
        public event Action<int, int, int, int, int, long> OnLevelLoaded;

        /// <inheritdoc/>
        /// <remarks>
        /// Fired by <see cref="WatchdogCoroutine"/> after 1500ms if no <c>OnMoveExecutingExited</c>
        /// arrives. <c>CurrentSequenceId</c> is incremented before firing (WDG-01). The watchdog
        /// coroutine clears <c>_watchdogCoroutine</c> to null before invoking so subscribers
        /// see <c>IsWatchdogRunning == false</c> during their callback.
        /// </remarks>
        public event Action<long> OnBoardRefreshForced;

        /// <inheritdoc/>
        /// <remarks>
        /// Fired by <see cref="ExitLevel"/> after TEARDOWN completes (L-08) or LOADING is cancelled.
        /// <c>levelId</c> is null when called during LOADING (cancellation before L-02 confirms the ID).
        /// Level Progression must receive this event before issuing the next <see cref="LoadLevel"/> call.
        /// </remarks>
        public event Action<int?> OnLevelUnloaded;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Wire LDS reference. No Script Execution Order is configured for these systems —
            // GameBootstrap.CreateSystems() instantiates LevelDataSystem before GameStateManager
            // (AddComponent runs Awake synchronously), which is what guarantees LDS.Instance is
            // non-null here. If that creation order ever changes, this breaks silently.
            _levelDataSystem = LevelDataSystem.Instance;

            // SER-01/02/03: production board-snapshot store (Story 008). Test fixtures override
            // via InjectBoardSnapshotSystemForTesting after construction.
            _boardSnapshotSystem = new BoardSnapshotSystem();
        }

        private void OnDestroy()
        {
            CancelWatchdog(); // WDG-03: prevent coroutine leak on DDOL MonoBehaviour destroy
            if (Instance == this)
                Instance = null;
        }

        // ── IGameStateManager Commands ────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Implements TR-GSM-008 and ADR-0006 lifecycle steps L-01 through L-07.
        /// All steps execute synchronously on the main thread.
        ///
        /// EC-09: Silent reject when not in UNLOADED — no event fired; state unchanged.
        /// L-04: Pre-won board detection logs a warning but never triggers COMPLETE;
        ///       win detection during gameplay is exclusively SortMechanic's responsibility.
        /// L-07/L-06 ordering: ACTIVE transition fires BEFORE OnLevelLoaded so every
        ///           subscriber observing OnLevelLoaded sees LifecycleState == Active.
        ///
        /// After allocating new backing arrays, StackContents and TempSlotContents are
        /// re-assigned — consumers hold references to these properties; stale references
        /// cause silent display bugs.
        /// </remarks>
        public void LoadLevel(int levelId)
        {
            // EC-09: reject in all non-UNLOADED states (LOADING, ACTIVE, COMPLETE)
            if (_lifecycleState != GSMLifecycleState.Unloaded) return;

            _lifecycleState    = GSMLifecycleState.Loading;
            _currentLevelId    = levelId;

            // L-01: Boot guard — query LDS readiness. Never cache this result (AC-GSM-17).
            if (_levelDataSystem == null)
            {
                EmitSessionLoadFailed(GsmSessionLoadFailReason.LevelDataUnavailable, levelId);
                return;
            }
            SystemReadiness readiness = _levelDataSystem.GetReadiness();
            if (!readiness.Ready)
            {
                EmitSessionLoadFailed(GsmSessionLoadFailReason.LevelDataUnavailable, levelId);
                return;
            }

            // L-02: Fetch the level record from LDS.
            LevelRecord record;
            try
            {
                record = _levelDataSystem.GetLevel(levelId);
            }
            catch (LevelDataException)
            {
                EmitSessionLoadFailed(GsmSessionLoadFailReason.LevelRecordError, levelId);
                return;
            }

            // L-03: Bolt-count invariant checks (implemented in Story 004).
            // EmitSessionLoadFailed and UNLOADED transition are handled inside RunInvariantChecks.
            if (!RunInvariantChecks(record, levelId)) return;

            // L-04: Pre-won board detection — log warning only; do NOT auto-win (AC-GSM-11).
            // Win detection during gameplay is exclusively SortMechanic's responsibility.
            if (CheckWinCondition(record.ColorStacks, record.StackDepth))
                Debug.LogWarning($"[GameStateManager] Pre-won board detected: level_id={levelId}");

            // L-05: Instantiate board arrays; reset sequence ID, move count, undo stack.
            _colorCount    = record.ColorCount;
            _stackDepth    = record.StackDepth;
            _tempSlotCount = record.TempSlotCount;
            _tempSlotDepth = record.TempSlotDepth;

            // Allocate color stacks — copy from record so record arrays remain immutable
            var colorStacks = new List<int>[record.ColorCount];
            for (int i = 0; i < record.ColorCount; i++)
                colorStacks[i] = new List<int>(record.ColorStacks[i]);

            // Allocate temp slot lists and build flat combined array
            var tempSlots = new List<int>[record.TempSlotCount];
            for (int i = 0; i < record.TempSlotCount; i++)
                tempSlots[i] = new List<int>(record.TempSlotDepth);

            // Combine into single flat-namespace array: [colorStacks... | tempSlots...]
            _stackContents = new List<int>[record.ColorCount + record.TempSlotCount];
            for (int i = 0; i < record.ColorCount; i++)
                _stackContents[i] = colorStacks[i];
            for (int i = 0; i < record.TempSlotCount; i++)
                _stackContents[record.ColorCount + i] = tempSlots[i];

            // Build _tempSlotBacking as views into _stackContents
            _tempSlotBacking = new List<int>[record.TempSlotCount];
            for (int i = 0; i < record.TempSlotCount; i++)
                _tempSlotBacking[i] = _stackContents[record.ColorCount + i];

            // Re-assign public properties — consumers hold references to these (AC-GSM-12)
            StackContents    = _stackContents;
            TempSlotContents = _tempSlotBacking;

            // Phase-2: per-column capacities, freeze counters, and load-time mystery reveal.
            InitMechanicState(record);

            _currentSequenceId = 0;
            _moveCount         = 0;
            _helperTubeCount   = 0; // retry/reload drops any runtime-added extra tubes
            _undoStack.Clear();
            SyncPublicCounters();

            // L-07: Transition to ACTIVE before emitting — subscribers must see Active state
            _lifecycleState = GSMLifecycleState.Active;

            // L-06: Emit level_loaded
            OnLevelLoaded?.Invoke(levelId, record.ColorCount, record.StackDepth,
                                  record.TempSlotCount, record.TempSlotDepth, 0L);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Implements UND-01/UND-02/UND-03/UND-05 from <c>design/gdd/game-state-manager.md</c>.
        /// UND-03: if animation is in flight (<c>_isAnimationInFlight</c>), defers the request —
        /// stores it in <c>_pendingUndo</c> and returns. Second deferred request is silently dropped
        /// (EC-17: queue capacity = 1). The deferred undo is flushed by <see cref="FlushDeferredUndo"/>
        /// when MOVE_EXECUTING exits (normal or watchdog path).
        /// Synchronous path: calls <see cref="ExecuteUndo"/> immediately when no animation is in flight.
        /// No-op when stack is empty (UND-02) or GSM is not ACTIVE (UND-05).
        /// </remarks>
        public void UndoRequested()
        {
            if (_lifecycleState != GSMLifecycleState.Active) return; // frozen in COMPLETE etc. (UND-05)
            if (_undoStack.Count == 0) return;                       // no-op on empty stack (UND-02)

            // UND-03: defer if animation is in flight — do not silently drop
            if (_isAnimationInFlight)
            {
                _pendingUndo = true; // EC-17: second tap while already deferred is silently dropped
                return;
            }

            ExecuteUndo();
        }

        /// <summary>
        /// Executes a single undo step: pops the top <see cref="UndoEntry"/>, reverts the bolt move,
        /// increments <c>CurrentSequenceId</c> (UND-06 — monotonic), decrements <c>MoveCount</c>,
        /// and fires <see cref="OnBoardStateChanged"/>.
        /// Caller must verify the undo stack is non-empty before calling.
        /// </summary>
        private void ExecuteUndo()
        {
            var entry = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            // Revert: remove top bolt from destination, append back to source
            _stackContents[entry.To].RemoveAt(_stackContents[entry.To].Count - 1);
            _stackContents[entry.From].Add(entry.ColorId);

            // Phase-2: undoing onto/over a column can expose a covered mystery top → reveal it.
            RevealMysteryIfExposed(entry.To);

            // Increment — monotonic; never decrement (UND-06: stale-signal safety for AnimationSystem)
            _currentSequenceId++;
            _moveCount--;

            SyncPublicCounters();
            OnBoardStateChanged?.Invoke(_currentSequenceId, _moveCount);
        }

        /// <summary>
        /// Flushes the deferred undo request if one is pending (UND-03, EC-10, EC-11).
        /// Clears <c>_pendingUndo</c> and calls <see cref="ExecuteUndo"/> when the undo stack
        /// is non-empty. No-op when <c>_pendingUndo</c> is false.
        /// Called from <see cref="HandleMoveExecutingExited"/> (IDLE exit) and
        /// <see cref="WatchdogCoroutine"/> (watchdog exit) to ensure identical flush behaviour.
        /// </summary>
        private void FlushDeferredUndo()
        {
            if (!_pendingUndo) return;
            _pendingUndo = false;
            if (_undoStack.Count > 0)
                ExecuteUndo();
        }

        // ── Board Mutation ────────────────────────────────────────────────────────

        /// <summary>
        /// Handles a committed move from SortMechanic. Executes the atomic 5-step board mutation
        /// synchronously on the main thread in a single frame.
        /// </summary>
        /// <param name="src">
        /// Flat-namespace index of the source stack or temp slot (0..colorCount+tempSlotCount-1).
        /// </param>
        /// <param name="dst">
        /// Flat-namespace index of the destination stack or temp slot.
        /// </param>
        /// <param name="colorId">Color identifier of the bolt being moved.</param>
        /// <param name="seqId">
        /// Sequence ID that was current when the move was initiated (used for stale-signal guard).
        /// </param>
        /// <remarks>
        /// Implements TR-GSM-005: Atomic 5-step mutation — all steps synchronous, no await between
        /// steps. Subscribed to <c>SortMechanic.OnMoveCommitted</c> event.
        ///
        /// OnBoardStateChanged is NOT fired here — only on undo and foreground restore (AC-GSM-03).
        ///
        /// This is an internal method exposed for test seams via <see cref="SimulateMoveCommitted"/>.
        /// </remarks>
        public void HandleMoveCommitted(int src, int dst, int colorId, long seqId)
        {
            if (_lifecycleState != GSMLifecycleState.Active) return;

            // Step 1: Remove top bolt from source
            _stackContents[src].RemoveAt(_stackContents[src].Count - 1);

            // Phase-2: removing the source top may expose a covered mystery ball → reveal it.
            RevealMysteryIfExposed(src);

            // Step 2: Append bolt to destination
            _stackContents[dst].Add(colorId);

            // Step 3: Push undo entry (captures seqId BEFORE the post-commit increment)
            _undoStack.Add(new UndoEntry
            {
                From    = src,
                To      = dst,
                ColorId = colorId,
                SeqId   = _currentSequenceId,
            });

            // Step 4: Increment sequence ID (monotonic — TR-GSM-002)
            _currentSequenceId++;

            // Step 5: Increment move count
            _moveCount++;

            // Sync public read-only properties with updated private fields
            SyncPublicCounters();

            // NOTE: OnBoardStateChanged is NOT emitted here — only on undo and foreground
            // restore (AC-GSM-03). See Story 002 and Story 008 for emission sites.

            // UND-03: mark animation as in-flight so subsequent UndoRequested calls are deferred
            _isAnimationInFlight = true;

            // WDG-03: cancel any prior watchdog defensively, then start fresh
            if (_watchdogCoroutine != null) StopCoroutine(_watchdogCoroutine);
            _watchdogCoroutine = StartCoroutine(WatchdogCoroutine());
        }

        /// <summary>
        /// Handles a cancelled move (player tapped the same stack they already selected).
        /// No-op — board state is not mutated and no events are emitted.
        /// </summary>
        /// <param name="src">Index of the stack whose bolt was released.</param>
        /// <param name="colorId">Color identifier of the released bolt.</param>
        private void HandleMoveCancelled(int src, int colorId)
        {
            // No-op per AC-GSM-02: move_cancelled produces zero mutations and emits no events.
        }

        /// <summary>
        /// Handles a rejected move (player tapped an invalid destination).
        /// No-op — board state is not mutated and no events are emitted.
        /// </summary>
        /// <param name="src">Index of the source stack.</param>
        /// <param name="dst">Index of the rejected destination stack.</param>
        /// <param name="colorId">Color identifier of the bolt that attempted to move.</param>
        /// <param name="reason">Human-readable rejection reason (e.g. "COLOR_MISMATCH").</param>
        private void HandleMoveRejected(int src, int dst, int colorId, string reason)
        {
            // No-op per AC-GSM-02: move_rejected produces zero mutations and emits no events.
        }

        /// <summary>
        /// Handles the puzzle-solved signal from SortMechanic.
        /// Transitions to COMPLETE, freezes undo stack, and emits <see cref="OnLevelComplete"/>.
        /// </summary>
        /// <remarks>
        /// Implements WIN-01 from <c>design/gdd/game-state-manager.md</c>.
        /// Subscribed to <c>SortMechanic.OnPuzzleSolved</c> (wired in Story 004/005).
        /// </remarks>
        public void HandlePuzzleSolved()
        {
            if (_lifecycleState != GSMLifecycleState.Active) return; // EC-15: no-op in COMPLETE / non-ACTIVE

            CancelWatchdog();          // WDG-03: WIN is a MOVE_EXECUTING exit — must cancel
            _pendingUndo = false;      // EC-05: discard deferred undo — COMPLETE does not process undo
            _isAnimationInFlight = false;
            _lifecycleState = GSMLifecycleState.Complete;
            // Undo stack is now frozen: UndoRequested() guards _lifecycleState != Active

            // Read par_moves from LDS synchronously — ADR-0012: GSM embeds it so downstream never re-queries
            int parMoves = 0;
            if (_levelDataSystem != null)
                parMoves = _levelDataSystem.GetLevel(_currentLevelId).ParMoves;
            else
                Debug.LogWarning("[GameStateManager] HandlePuzzleSolved: _levelDataSystem is null — parMoves=0 (Story 005 wiring missing?).");

            // sequenceId NOT incremented (WIN-01) — the winning move_committed already incremented it in BSM-01
            OnLevelComplete?.Invoke(_currentLevelId, _moveCount, parMoves, _currentSequenceId);
        }

        // ── ExitLevel / TEARDOWN (L-08) ──────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Implements L-08: TEARDOWN state transition and board cleanup.
        /// Captures <c>levelId</c> before clearing state so the event payload is accurate.
        /// LOADING cancellation emits <c>OnLevelUnloaded(null)</c> — level was never confirmed.
        /// No-op in UNLOADED state (guard prevents double-teardown).
        /// </remarks>
        public void ExitLevel()
        {
            if (_lifecycleState == GSMLifecycleState.Unloaded) return;

            // Capture levelId before clearing — LOADING cancellation emits null
            int? levelId = (_lifecycleState == GSMLifecycleState.Loading) ? (int?)null : _currentLevelId;

            CancelWatchdog();
            ClearAllState();
            _lifecycleState = GSMLifecycleState.Unloaded;
            OnLevelUnloaded?.Invoke(levelId);
        }

        /// <summary>
        /// Clears all board state for TEARDOWN (L-08) and SER-03 recovery.
        /// Does not modify <c>_lifecycleState</c> or <c>_currentLevelId</c> — callers set those.
        /// </summary>
        private void ClearAllState()
        {
            _stackContents   = null;
            _tempSlotBacking = null;
            StackContents    = null;
            TempSlotContents = null;
            _columnCapacities = null;
            _freezeInit       = null;
            _helperTubeCount  = 0;

            _undoStack.Clear();
            _pendingUndo          = false;
            _isAnimationInFlight  = false;
            _moveCount            = 0;
            _currentSequenceId    = 0;
            _colorCount           = 0;
            _stackDepth           = 0;
            _tempSlotCount        = 0;
            _tempSlotDepth        = 0;
            _sortMechanicWasInBoltSelected = false;
            SyncPublicCounters();
        }

        // ── App Lifecycle — Serialization / Restore (SER-01/02/03, EC-06) ────────

        /// <summary>
        /// Unity lifecycle message. On pause: serializes board state to <see cref="_boardSnapshotSystem"/>
        /// (SER-01). On resume: deserializes and restores board state (SER-02/SER-03).
        /// <para><b>FORBIDDEN</b>: this method must never be <c>async void</c> — iOS returns control
        /// to the OS at the first <c>await</c>, cancelling any pending write (ADR-0003).</para>
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                // SER-01: synchronous write — W-2 path (no await; must complete within iOS 5s deadline)
                if (_lifecycleState == GSMLifecycleState.Active || _lifecycleState == GSMLifecycleState.Complete)
                {
                    var snapshot = BuildBoardSnapshot();
                    _boardSnapshotSystem?.WriteBoardSnapshotSync(snapshot);
                }
            }
            else
            {
                // Foreground restore — skip if no save system wired (e.g. cold-start before SP ready)
                if (_boardSnapshotSystem == null) return;

                var snapshot = _boardSnapshotSystem.ReadBoardSnapshot();
                if (snapshot == null || !snapshot.IsValid)
                {
                    // SER-03: corrupt or missing save — emit failure and return to UNLOADED
                    int levelId = snapshot?.LevelId ?? 0;
                    ClearAllState();
                    _lifecycleState = GSMLifecycleState.Unloaded;
                    OnSessionLoadFailed?.Invoke(GsmSessionLoadFailReason.SaveCorrupt, levelId);
                    return;
                }

                // SER-02: restore board state
                RestoreFromSnapshot(snapshot);

                // EC-06: bolt was held when app backgrounded — increment seqId to invalidate held state
                if (snapshot.WasInBoltSelected)
                    _currentSequenceId++;

                _sortMechanicWasInBoltSelected = false; // consumed; reset for next pause cycle
                SyncPublicCounters();
                OnBoardStateChanged?.Invoke(_currentSequenceId, _moveCount);
            }
        }

        /// <summary>
        /// Builds a deep-copy snapshot of the current board state for serialization (SER-01).
        /// Undo stack is excluded — session-only per GDD SER-01.
        /// </summary>
        private BoardSnapshot BuildBoardSnapshot()
        {
            var stackContents = new int[_colorCount][];
            for (int i = 0; i < _colorCount; i++)
                stackContents[i] = _stackContents[i].ToArray();

            var tempSlotContents = new int[_tempSlotCount][];
            for (int i = 0; i < _tempSlotCount; i++)
                tempSlotContents[i] = _stackContents[_colorCount + i].ToArray();

            return new BoardSnapshot
            {
                StackContents     = stackContents,
                TempSlotContents  = tempSlotContents,
                StackDepth        = _stackDepth,
                TempSlotDepth     = _tempSlotDepth,
                TempSlotCount     = _tempSlotCount,
                ColorCount        = _colorCount,
                TubeCapacities    = _columnCapacities == null ? null : (int[])_columnCapacities.Clone(),
                FreezeInit        = _freezeInit == null ? null : (int[])_freezeInit.Clone(),
                MoveCount         = _moveCount,
                SequenceId        = _currentSequenceId,
                LevelId           = _currentLevelId,
                GsmState          = _lifecycleState == GSMLifecycleState.Complete ? "Complete" : "Active",
                WasInBoltSelected = _sortMechanicWasInBoltSelected,
                IsValid           = true,
            };
        }

        /// <summary>
        /// Restores board state from a valid snapshot (SER-02).
        /// Undo stack is cleared — it is session-only and not serialized.
        /// </summary>
        private void RestoreFromSnapshot(BoardSnapshot snapshot)
        {
            _colorCount        = snapshot.ColorCount;
            _stackDepth        = snapshot.StackDepth;
            _tempSlotCount     = snapshot.TempSlotCount;
            _tempSlotDepth     = snapshot.TempSlotDepth;
            _moveCount         = snapshot.MoveCount;
            _currentSequenceId = snapshot.SequenceId;
            _currentLevelId    = snapshot.LevelId;
            _columnCapacities  = snapshot.TubeCapacities == null ? null : (int[])snapshot.TubeCapacities.Clone();
            _freezeInit        = snapshot.FreezeInit == null ? null : (int[])snapshot.FreezeInit.Clone();

            // Reconstruct flat-namespace array: [color stacks... | temp slots...]
            _stackContents = new List<int>[snapshot.ColorCount + snapshot.TempSlotCount];
            for (int i = 0; i < snapshot.ColorCount; i++)
                _stackContents[i] = new List<int>(snapshot.StackContents[i]);
            for (int i = 0; i < snapshot.TempSlotCount; i++)
                _stackContents[snapshot.ColorCount + i] = new List<int>(snapshot.TempSlotContents[i]);

            _tempSlotBacking = new List<int>[snapshot.TempSlotCount];
            for (int i = 0; i < snapshot.TempSlotCount; i++)
                _tempSlotBacking[i] = _stackContents[snapshot.ColorCount + i];

            StackContents    = _stackContents;
            TempSlotContents = _tempSlotBacking;
            _undoStack.Clear(); // undo stack is session-only — not restored

            _pendingUndo         = false;
            _isAnimationInFlight = false;
            _lifecycleState      = snapshot.GsmState == "Complete"
                ? GSMLifecycleState.Complete
                : GSMLifecycleState.Active;
        }

        // ── Invariant Checks (L-03) ───────────────────────────────────────────────

        /// <summary>
        /// Runs both bolt-count invariant checks on a level record (ADR-0006 L-03).
        /// Check 1: total bolt count == <c>colorCount × stackDepth</c>.
        /// Check 2: each color ID in {1..colorCount} appears exactly <c>stackDepth</c> times;
        ///          also rejects phantom color IDs outside the domain.
        /// On failure, calls <see cref="EmitSessionLoadFailed"/> (transitions to UNLOADED and
        /// fires <see cref="OnSessionLoadFailed"/>) and returns <c>false</c>.
        /// Called by <see cref="LoadLevel"/> at L-03; also callable via test seam.
        /// </summary>
        private bool RunInvariantChecks(LevelRecord record, int levelId)
        {
            // Phase-2 (schema_version 2) levels use generalized invariants (asymmetric
            // capacities, mystery negatives, wildcard 0). Offline solving remains the
            // authoritative solvability gate; this is structural defence-in-depth.
            if (record.SchemaVersion >= 2)
                return RunInvariantChecksV2(record, levelId);

            // Check 1: total bolt count
            int total = 0;
            foreach (var stack in record.ColorStacks)
                total += stack.Length;
            if (total != record.ColorCount * record.StackDepth)
            {
                EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                return false;
            }

            // Check 2: per-color frequency + phantom color ID detection
            var colorCounts = new int[record.ColorCount + 1]; // 1-indexed; slot 0 unused
            foreach (var stack in record.ColorStacks)
            {
                foreach (var colorId in stack)
                {
                    if (colorId < 1 || colorId > record.ColorCount)
                    {
                        EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                        return false;
                    }
                    colorCounts[colorId]++;
                }
            }
            for (int c = 1; c <= record.ColorCount; c++)
            {
                if (colorCounts[c] != record.StackDepth)
                {
                    EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Generalized invariant checks for schema_version 2 levels.
        /// Check 1: every token is valid — wildcard (0) or a normal/mystery color whose abs is in
        ///          {1..colorCount}. Check 2: the color stacks start full — total bolts equals the
        ///          sum of the color-stack capacities (uniform or per-tube via tube_capacities).
        /// Per-color frequency is intentionally NOT enforced here (asymmetric/wildcard make it
        /// non-trivial); the offline solver guarantees solvability before a level ships.
        /// </summary>
        private bool RunInvariantChecksV2(LevelRecord record, int levelId)
        {
            int colorCount = record.ColorCount;
            int totalCols  = colorCount + record.TempSlotCount;

            // Per-column capacities (tube_capacities override, else uniform depths).
            int colorStackCapacitySum = 0;
            for (int i = 0; i < colorCount; i++)
            {
                int cap = (record.TubeCapacities != null && record.TubeCapacities.Length == totalCols)
                    ? record.TubeCapacities[i]
                    : record.StackDepth;
                colorStackCapacitySum += cap;
            }

            int total = 0;
            foreach (var stack in record.ColorStacks)
            {
                if (stack == null)
                {
                    EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                    return false;
                }
                total += stack.Length;
                foreach (int token in stack)
                {
                    int c = token < 0 ? -token : token;      // mystery hides abs; wildcard is 0
                    if (token != 0 && (c < 1 || c > colorCount))
                    {
                        EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                        return false;
                    }
                }
            }

            if (total != colorStackCapacitySum)
            {
                EmitSessionLoadFailed(GsmSessionLoadFailReason.InvariantViolation, levelId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the given color stacks represent a pre-solved board (L-04).
        /// A board is pre-won when every color stack has exactly <c>stackDepth</c> bolts
        /// and all bolt IDs within each stack are equal.
        /// Returns <c>true</c> on a pre-won board — this triggers a warning log only.
        /// Win detection during gameplay is exclusively SortMechanic's responsibility.
        /// </summary>
        private static bool CheckWinCondition(int[][] colorStacks, int stackDepth)
        {
            if (stackDepth == 0) return false; // guard: empty stacks would pass length check but throw on stack[0]
            foreach (var stack in colorStacks)
            {
                if (stack.Length != stackDepth) return false;
                int first = stack[0];
                for (int i = 1; i < stack.Length; i++)
                    if (stack[i] != first) return false;
            }
            return true;
        }

        // ── Watchdog Timer ────────────────────────────────────────────────────────

        /// <summary>
        /// Coroutine body for the watchdog timer (TR-GSM-004, WDG-01).
        /// Fires after <see cref="_watchdogTimeoutSec"/> if not cancelled by
        /// <see cref="CancelWatchdog"/>. Increments <see cref="CurrentSequenceId"/>
        /// and emits <see cref="OnBoardRefreshForced"/>.
        /// <c>WaitForSecondsRealtime</c> is required — fires even at <c>Time.timeScale = 0</c>.
        /// </summary>
        private IEnumerator WatchdogCoroutine()
        {
            yield return new WaitForSecondsRealtime(_watchdogTimeoutSec);
            _currentSequenceId++;
            SyncPublicCounters();
            _watchdogCoroutine = null;     // clear before invoke so IsWatchdogRunning==false in callbacks
            _isAnimationInFlight = false;
            OnBoardRefreshForced?.Invoke(_currentSequenceId);
            FlushDeferredUndo();           // EC-11: watchdog exit also flushes deferred undo
        }

        /// <summary>
        /// Cancels the running watchdog coroutine and clears its reference.
        /// Safe to call when no watchdog is running (null guard).
        /// Must be called on every MOVE_EXECUTING exit: IDLE, WIN, TEARDOWN, and OnDestroy (WDG-03).
        /// </summary>
        private void CancelWatchdog()
        {
            if (_watchdogCoroutine != null)
            {
                StopCoroutine(_watchdogCoroutine);
                _watchdogCoroutine = null;
            }
        }

        /// <summary>
        /// Handles the MOVE_EXECUTING → IDLE exit signal from Sort Mechanic (normal animation
        /// completion path). Cancels the watchdog timer (WDG-03).
        /// Subscribed to <c>SortMechanic.OnMoveExecutingExited</c> (wired when Sort Mechanic exists).
        /// </summary>
        public void HandleMoveExecutingExited(long seqId)
        {
            CancelWatchdog();         // WDG-03: valid exit — timer no longer needed
            _isAnimationInFlight = false;
            FlushDeferredUndo();      // EC-10: deferred undo fires before any further processing
        }

        // ── Session Load Failure ──────────────────────────────────────────────────

        /// <summary>
        /// Transitions GSM to UNLOADED and fires <see cref="OnSessionLoadFailed"/>.
        /// Called on any load failure path (L-01 through L-05, SER-03).
        /// </summary>
        private void EmitSessionLoadFailed(GsmSessionLoadFailReason reason, int levelId)
        {
            _lifecycleState = GSMLifecycleState.Unloaded;
            OnSessionLoadFailed?.Invoke(reason, levelId);
        }

        // ── Private Helpers ───────────────────────────────────────────────────────

        /// <summary>
        /// Synchronises the public read-only counter properties (<c>MoveCount</c>,
        /// <c>CurrentSequenceId</c>) with their private backing fields after each mutation.
        /// Called at the end of <see cref="HandleMoveCommitted"/> and at the end of L-05.
        /// </summary>
        private void SyncPublicCounters()
        {
            MoveCount         = _moveCount;
            CurrentSequenceId = _currentSequenceId;
        }

        // ── Phase-2 mechanic state ────────────────────────────────────────────────

        /// <summary>
        /// Builds per-column capacity and freeze state from the record, and performs the
        /// load-time mystery reveal (any column already showing a mystery top is revealed so
        /// the player never starts with a selectable negative). Classic levels leave the
        /// capacity/freeze arrays null (uniform fallback, never frozen).
        /// </summary>
        private void InitMechanicState(LevelRecord record)
        {
            int totalCols = record.ColorCount + record.TempSlotCount;

            if (record.TubeCapacities != null && record.TubeCapacities.Length == totalCols)
            {
                _columnCapacities = new int[totalCols];
                Array.Copy(record.TubeCapacities, _columnCapacities, totalCols);
            }
            else
            {
                _columnCapacities = null; // uniform fallback via GetColumnCapacity
            }

            if (record.FrozenTubes != null && record.FrozenTubes.Length > 0)
            {
                _freezeInit = new int[totalCols];
                foreach (var ft in record.FrozenTubes)
                    if (ft != null && ft.TubeIndex >= 0 && ft.TubeIndex < totalCols)
                        _freezeInit[ft.TubeIndex] = ft.Turns;
            }
            else
            {
                _freezeInit = null;
            }

            // Load-time reveal happens before OnLevelLoaded, so BoardView reads the revealed
            // color directly (no animation for a board that starts with an exposed mystery).
            for (int i = 0; i < _stackContents.Length; i++)
                RevealMysteryIfExposed(i);
        }

        /// <summary>
        /// If the top of the given column is a mystery ball (negative color id), permanently
        /// reveals it by flipping the stored value to its positive color and firing
        /// <see cref="OnMysteryRevealed"/>. No-op for empty columns or non-mystery tops.
        /// </summary>
        private void RevealMysteryIfExposed(int flatIndex)
        {
            if (_stackContents == null || flatIndex < 0 || flatIndex >= _stackContents.Length) return;
            var col = _stackContents[flatIndex];
            if (col == null || col.Count == 0) return;
            int top = col[col.Count - 1];
            if (top < 0)
            {
                int revealed = -top;
                col[col.Count - 1] = revealed;
                OnMysteryRevealed?.Invoke(flatIndex, revealed);
            }
        }

        // ── Extra (helper) tube mechanic ──────────────────────────────────────────

        /// <summary>
        /// Applies one Extra-Tube use. Grows the last helper tube by one slot, or starts a new
        /// helper tube (capacity 1) when there is no helper yet or the last one is already at
        /// MAX capacity (= <see cref="StackDepth"/>, the level's standard tube depth). Helper
        /// tubes are appended to the END of the flat namespace as extra temp slots, so no
        /// existing index shifts — undo entries stay valid and movement/win/completion logic is
        /// reused unchanged. Fires <see cref="OnBoardShapeChanged"/> so the view relayouts.
        /// </summary>
        /// <returns>Flat index of the affected helper tube, or -1 when GSM is not ACTIVE.</returns>
        public int ApplyExtraTube()
        {
            if (_lifecycleState != GSMLifecycleState.Active) return -1;

            int maxCap = Mathf.Max(1, _stackDepth);
            EnsureColumnCapacitiesMaterialized();

            int lastFlat = _colorCount + _tempSlotCount - 1;
            bool startNew = _helperTubeCount == 0 || _columnCapacities[lastFlat] >= maxCap;

            int affected;
            if (startNew)
            {
                AppendHelperColumn(capacity: 1);
                _helperTubeCount++;
                affected = _colorCount + _tempSlotCount - 1;

                if (_colorCount + _tempSlotCount > 18)
                    Debug.LogWarning($"[GameStateManager] Extra tube added beyond the 18-column " +
                        $"layout budget (now {_colorCount + _tempSlotCount}); tubes may render small.");
            }
            else
            {
                _columnCapacities[lastFlat] = Mathf.Min(maxCap, _columnCapacities[lastFlat] + 1);
                affected = lastFlat;
            }

            OnBoardShapeChanged?.Invoke();
            return affected;
        }

        /// <summary>
        /// Materializes <see cref="_columnCapacities"/> from the uniform fallback when the level
        /// did not ship per-tube capacities, so a helper tube can carry a capacity different from
        /// the rest without affecting any other column.
        /// </summary>
        private void EnsureColumnCapacitiesMaterialized()
        {
            int totalCols = _colorCount + _tempSlotCount;
            if (_columnCapacities != null && _columnCapacities.Length == totalCols) return;

            var caps = new int[totalCols];
            for (int i = 0; i < totalCols; i++)
                caps[i] = GetColumnCapacity(i); // existing array if present, else uniform fallback
            _columnCapacities = caps;
        }

        /// <summary>
        /// Appends one empty helper column (a temp slot) of the given capacity to the END of the
        /// flat namespace and re-publishes the read-only views. Assumes
        /// <see cref="EnsureColumnCapacitiesMaterialized"/> has already run.
        /// </summary>
        private void AppendHelperColumn(int capacity)
        {
            int newTotal = _colorCount + _tempSlotCount + 1;
            var helperList = new List<int>(Mathf.Max(1, capacity));

            var newStack = new List<int>[newTotal];
            Array.Copy(_stackContents, newStack, _stackContents.Length);
            newStack[newTotal - 1] = helperList;
            _stackContents = newStack;

            int newTempCount = _tempSlotCount + 1;
            var newTempBacking = new List<int>[newTempCount];
            if (_tempSlotBacking != null)
                Array.Copy(_tempSlotBacking, newTempBacking, _tempSlotBacking.Length);
            newTempBacking[newTempCount - 1] = helperList;
            _tempSlotBacking = newTempBacking;

            var newCaps = new int[newTotal];
            Array.Copy(_columnCapacities, newCaps, _columnCapacities.Length);
            newCaps[newTotal - 1] = capacity;
            _columnCapacities = newCaps;

            if (_freezeInit != null) // helper tubes are never frozen — pad with 0
            {
                var newFreeze = new int[newTotal];
                Array.Copy(_freezeInit, newFreeze, _freezeInit.Length);
                _freezeInit = newFreeze;
            }

            _tempSlotCount = newTempCount;

            // Consumers hold these references — re-publish after growing the backing arrays.
            StackContents    = _stackContents;
            TempSlotContents = _tempSlotBacking;
        }

        // ── Test Seams (internal — never call from production code) ──────────────

        /// <summary>
        /// Seeds the board state directly for unit testing. Bypasses the Addressables / LDS
        /// pipeline to allow synchronous test setup.
        /// Call only from <c>[SetUp]</c> methods in test fixtures.
        /// </summary>
        /// <param name="combinedContents">
        /// Flat-namespace array: indices 0..(colorCount-1) = color stacks,
        /// indices colorCount..(colorCount+tempSlotCount-1) = temp slots.
        /// Each inner list is used by reference — do not share across tests.
        /// </param>
        /// <param name="stackDepth">Max bolts per color stack column.</param>
        /// <param name="tempSlotDepth">Max bolts per temp slot.</param>
        /// <param name="tempSlotCount">Number of temp slot columns.</param>
        /// <param name="colorCount">Number of distinct bolt colors.</param>
        /// <param name="initialSeqId">Initial <c>CurrentSequenceId</c> value (default 0).</param>
        /// <param name="initialMoveCount">Initial <c>MoveCount</c> value (default 0).</param>
        /// <param name="initialUndoStack">
        /// Initial undo stack contents. Null is treated as an empty stack.
        /// </param>
        internal void SeedBoardForTesting(
            List<int>[] combinedContents,
            int stackDepth,
            int tempSlotDepth,
            int tempSlotCount,
            int colorCount,
            long initialSeqId       = 0,
            int  initialMoveCount   = 0,
            List<UndoEntry> initialUndoStack = null)
        {
            _stackContents    = combinedContents;
            _stackDepth       = stackDepth;
            _tempSlotDepth    = tempSlotDepth;
            _tempSlotCount    = tempSlotCount;
            _colorCount       = colorCount;
            _currentSequenceId = initialSeqId;
            _moveCount        = initialMoveCount;
            _undoStack.Clear();
            if (initialUndoStack != null) _undoStack.AddRange(initialUndoStack);

            // Build the _tempSlotBacking array as a view into _stackContents
            _tempSlotBacking = new List<int>[tempSlotCount];
            for (int i = 0; i < tempSlotCount; i++)
                _tempSlotBacking[i] = _stackContents[colorCount + i];

            // Expose public read-only wrappers
            StackContents    = _stackContents;
            TempSlotContents = _tempSlotBacking;
            SyncPublicCounters();
        }

        /// <summary>
        /// Sets the lifecycle state directly for unit testing.
        /// Call only from test fixtures.
        /// </summary>
        internal void SetStateForTesting(GSMLifecycleState state)
        {
            _lifecycleState = state;
        }

        /// <summary>
        /// Injects a stub <see cref="ILevelDataSystem"/> for unit testing.
        /// In production, <see cref="Awake"/> wires the real <c>LevelDataSystem.Instance</c>.
        /// Call only from test fixtures.
        /// </summary>
        internal void InjectLevelDataSystemForTesting(ILevelDataSystem lds)
        {
            _levelDataSystem = lds;
        }

        /// <summary>
        /// Directly invokes <see cref="HandleMoveCommitted"/> for unit testing.
        /// Simulates a <c>SortMechanic.OnMoveCommitted</c> event without requiring
        /// a running SortMechanic instance.
        /// </summary>
        internal void SimulateMoveCommitted(int src, int dst, int colorId, long seqId)
        {
            HandleMoveCommitted(src, dst, colorId, seqId);
        }

        /// <summary>
        /// Directly invokes <see cref="HandleMoveCancelled"/> for unit testing.
        /// </summary>
        internal void SimulateMoveCancelled(int src, int colorId)
        {
            HandleMoveCancelled(src, colorId);
        }

        /// <summary>
        /// Directly invokes <see cref="HandleMoveRejected"/> for unit testing.
        /// </summary>
        internal void SimulateMoveRejected(int src, int dst, int colorId, string reason)
        {
            HandleMoveRejected(src, dst, colorId, reason);
        }

        /// <summary>
        /// Seeds per-column initial freeze turns for unit testing. In production
        /// <see cref="InitMechanicState"/> sets <c>_freezeInit</c> from the level record's
        /// frozen_tubes; this seam injects it directly for synchronous tests.
        /// Remaining freeze stays a pure function of <see cref="MoveCount"/>
        /// (<c>freezeInit − moveCount</c>), so undo restores it for free. Call only from tests.
        /// </summary>
        internal void SeedFreezeForTesting(int[] freezeInit) => _freezeInit = freezeInit;

        /// <summary>
        /// Sets the current level ID and LDS reference for unit testing.
        /// In production, <see cref="LoadLevel"/> sets these; inject here for synchronous tests.
        /// Call only from test fixtures.
        /// </summary>
        internal void SeedLevelForTesting(int levelId, ILevelDataSystem levelDataSystem = null)
        {
            _currentLevelId   = levelId;
            _levelDataSystem  = levelDataSystem;
        }

        /// <summary>
        /// Directly invokes <see cref="HandlePuzzleSolved"/> for unit testing.
        /// </summary>
        internal void SimulatePuzzleSolved() => HandlePuzzleSolved();

        /// <summary>
        /// Directly invokes <see cref="RunInvariantChecks"/> for unit testing.
        /// Story 005 will call this via <see cref="LoadLevel"/>; test directly here to isolate L-03.
        /// </summary>
        internal bool SimulateRunInvariantChecks(LevelRecord record, int levelId)
            => RunInvariantChecks(record, levelId);

        /// <summary>
        /// Clears the static <see cref="Instance"/> reference.
        /// Call only from <c>[TearDown]</c> methods in test fixtures.
        /// </summary>
        internal static void ClearInstanceForTesting()
        {
            Instance = null;
        }

        /// <summary>
        /// Directly executes the watchdog expiry logic for unit testing.
        /// Bypasses the coroutine timer — simulates the watchdog having fired after its timeout.
        /// Increments <c>CurrentSequenceId</c>, clears <c>_watchdogCoroutine</c>, and fires
        /// <c>OnBoardRefreshForced</c> exactly as the real coroutine would.
        /// Call only from test fixtures.
        /// </summary>
        internal void SimulateWatchdogFire()
        {
            _currentSequenceId++;
            SyncPublicCounters();
            _watchdogCoroutine = null;
            _isAnimationInFlight = false;
            OnBoardRefreshForced?.Invoke(_currentSequenceId);
            FlushDeferredUndo(); // EC-11: matches WatchdogCoroutine exactly
        }

        /// <summary>
        /// Directly invokes <see cref="HandleMoveExecutingExited"/> for unit testing.
        /// Simulates Sort Mechanic's MOVE_EXECUTING → IDLE exit signal arriving.
        /// Call only from test fixtures.
        /// </summary>
        internal void SimulateMoveExecutingExited(long seqId) => HandleMoveExecutingExited(seqId);

        /// <summary>
        /// Whether the watchdog coroutine is currently running.
        /// <c>true</c> between a move_committed and either valid exit or watchdog fire.
        /// Call only from test fixtures.
        /// </summary>
        internal bool IsWatchdogRunning => _watchdogCoroutine != null;

        /// <summary>Injects a stub <see cref="IBoardSnapshotSystem"/> for integration testing. Call only from test fixtures.</summary>
        internal void InjectBoardSnapshotSystemForTesting(IBoardSnapshotSystem stub) => _boardSnapshotSystem = stub;

        /// <summary>Directly invokes <see cref="OnApplicationPause"/> for integration testing. Call only from test fixtures.</summary>
        internal void SimulateApplicationPause(bool paused) => OnApplicationPause(paused);

        /// <summary>Sets the EC-06 bolt-selected flag for integration testing (normally set by Sort Mechanic). Call only from test fixtures.</summary>
        internal void SetSortMechanicWasInBoltSelectedForTesting(bool value) => _sortMechanicWasInBoltSelected = value;

        /// <summary>Whether a deferred undo is pending (set during MOVE_EXECUTING). Call only from test fixtures.</summary>
        internal bool IsPendingUndo => _pendingUndo;

        /// <summary>Whether an animation is currently in flight (true between BSM-01 and MOVE_EXECUTING exit). Call only from test fixtures.</summary>
        internal bool IsAnimationInFlight => _isAnimationInFlight;

        /// <summary>
        /// Overrides the watchdog timeout duration for unit testing.
        /// Use with <c>[UnityTest]</c> (async path) to shorten the wait.
        /// Not needed for synchronous tests that use <see cref="SimulateWatchdogFire"/>.
        /// Call only from test fixtures.
        /// </summary>
        internal void SetWatchdogTimeoutForTesting(float timeoutSec) => _watchdogTimeoutSec = timeoutSec;

        /// <summary>Current undo stack depth. Call only from test fixtures.</summary>
        internal int UndoStackDepth => _undoStack.Count;

        /// <summary>
        /// Returns the top <see cref="UndoEntry"/> without popping.
        /// Returns <see langword="false"/> (and <c>default</c>) if the stack is empty.
        /// Call only from test fixtures.
        /// </summary>
        internal bool TryPeekUndoStack(out UndoEntry entry)
        {
            if (_undoStack.Count == 0) { entry = default; return false; }
            entry = _undoStack[_undoStack.Count - 1];
            return true;
        }
    }
}
