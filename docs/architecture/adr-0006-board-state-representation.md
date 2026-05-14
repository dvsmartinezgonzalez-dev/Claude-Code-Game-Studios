# ADR-0006: Board State Representation and GSM Design

## Status
Accepted

## Date
2026-05-02

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Core / Scripting |
| **Knowledge Risk** | LOW — Pure C# game logic: arrays, dictionaries, FSM, Coroutines. No Unity 6.x breaking changes in this domain. |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None — `int[][]`, `List<T>`, `Dictionary<TEnum,T>`, `Coroutine`, `WaitForSecondsRealtime` are all stable |
| **Verification Required** | Confirm `StopCoroutine` called in TEARDOWN and `OnDestroy`; confirm no coroutine leaks across level loads via Unity Profiler |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 (GSM is a DDOL singleton at SEO −50); ADR-0002 (GSM events use typed C# events); ADR-0004 (GSM.LoadLevel calls LDS.GetLevel synchronously) |
| **Enables** | ADR-0007 (Input Handling — SortMechanic reads board state from GSM synchronously); ADR-0009 (Bolt Animation — consumes GSM events and board state) |
| **Blocks** | GameStateManager implementation sprint; Sort Mechanic implementation sprint; CoinEconomy implementation sprint |
| **Ordering Note** | ADR-0001 and ADR-0004 must be Accepted before this ADR can be implemented. |

## Context

### Problem Statement
BoltSort's Game State Manager must be the sole authoritative owner of board state, enforcing atomic mutations and an auditable sequence ID so that AnimationSystem and SortMechanic always work from a consistent snapshot. Without explicit decisions on data structures, ownership rules, and mutation patterns, other systems risk writing board state directly or reading stale state mid-animation.

### Constraints
- All board state mutations must be synchronous, on the main thread, in a single frame — no async board writes
- `IReadOnlyList<int>[]` wrapper: the outer array is technically mutable (callers could replace elements); convention that GSM is the sole writer must be documented and enforced at code review
- Coroutine watchdog must be cancelled in every exit path from MOVE_EXECUTING (including scene teardown) to prevent leaks on DDOL MonoBehaviour
- `Dictionary<EarnSource, int>` is safe under IL2CPP in Unity 6.x (non-boxing enum comparer)

### Requirements
- GSM is sole owner: no system other than GSM may write to board state arrays
- Monotonic sequence ID: `CurrentSequenceId` only ever increments, never decrements
- Atomic 5-step board mutation: all steps in the same frame, no await between steps
- Unlimited undo stack during ACTIVE state; frozen (no new entries) on COMPLETE
- Watchdog fires `OnBoardRefreshForced` if `OnMoveExecutingExited` does not arrive within 1500ms
- `GetLevel()` (via LDS) is called synchronously inside `LoadLevel()` — LDS must be ready

## Decision

### Board State Data Structure

```csharp
// Private — sole writer is GSM
private int[][] _stackContents;     // [stackIndex][slotIndex] = colorId (1..colorCount; 0 = empty gap)
private int[][] _tempSlotContents;  // [slotIndex][boltIndex] = colorId
private int _currentSequenceId;     // monotonic, increments on every committed move
private int _moveCount;
private int _stackDepth;
private int _tempSlotDepth;
private int _tempSlotCount;
private int _colorCount;

// Public read-only access (outer array is still replaceable by callers — documented GSM-sole-writer convention)
public IReadOnlyList<int>[] StackContents { get; private set; }
public IReadOnlyList<int>[] TempSlotContents { get; private set; }
public int StackDepth { get; private set; }
public int TempSlotDepth { get; private set; }
public int TempSlotCount { get; private set; }
public int ColorCount { get; private set; }
public int MoveCount { get; private set; }
public int CurrentSequenceId { get; private set; }
```

**Why `int[][]` (jagged arrays)?** Each stack has variable occupancy (0–stackDepth bolts). Jagged arrays allow each inner array to reflect actual occupancy as a `List<int>` or fixed array with a length counter. `int[,]` rectangular arrays require fixed max dimensions and waste memory on sparse stacks. `List<Stack<int>>` adds GC pressure from dynamic resize. `int[][]` gives direct indexed access with no allocation on read.

**Stack occupancy model**: Each stack's inner array (`_stackContents[i]`) has length = current bolt count in that stack. Bolts are indexed from bottom (index 0) to top (index `length-1`). An empty stack is `int[0]`. Adding a bolt appends to the array; removing pops the last element.

**Note for future hardening**: If external mutation of the outer array becomes a concern, wrap as `IReadOnlyList<IReadOnlyList<int>>`. At MVP, the documented convention is sufficient.

### Level Lifecycle FSM

```
UNLOADED ──LoadLevel()──▶ LOADING ──board ready──▶ ACTIVE ──OnPuzzleSolved──▶ COMPLETE ──next level──▶ TEARDOWN ──▶ UNLOADED
                                                     │                              │
                                               OnBoardStateChanged            OnLevelComplete
                                               (every mutation)               (once; undo frozen)
```

| State | Entry Condition | Exit Condition | Invariants |
|-------|----------------|----------------|-----------|
| UNLOADED | Startup / after TEARDOWN | `LoadLevel()` called | No board data exists |
| LOADING | `LoadLevel()` called | bolt_count_invariant check passes | Board arrays being allocated; `LDS.GetLevel()` called |
| ACTIVE | `LoadLevel()` complete | `OnPuzzleSolved` received | Board data valid; undo stack writable |
| COMPLETE | `OnPuzzleSolved` received | Next level load requested | Undo stack frozen; `OnLevelComplete(levelId, moveCount, parMoves, sequenceId)` fired |
| TEARDOWN | Next level initiated | Cleanup done | Board arrays nulled; watchdog stopped |

### Sort Mechanic FSM

```
IDLE ─── tap ──▶ BOLT_SELECTED ─── valid move ──▶ MOVE_EXECUTING ─── OnAnimationComplete ──▶ IDLE
                       │                                   │
                  tap same stack                    OnPuzzleSolved
                       │                                   │
                  CANCELLATION ──▶ IDLE                   WIN
                       │
                  tap wrong stack
                       │
                  INVALID_MOVE ──▶ IDLE
```

| State | Input | Transition | Side Effect |
|-------|-------|-----------|-----------|
| IDLE | Tap bolt | → BOLT_SELECTED | Hold bolt reference |
| BOLT_SELECTED | Tap same stack | → CANCELLATION → IDLE | `OnMoveCancelled` event |
| BOLT_SELECTED | Tap valid destination | → MOVE_EXECUTING | `OnMoveCommitted(src,dst,colorId,seqId)` |
| BOLT_SELECTED | Tap invalid destination | → INVALID_MOVE → IDLE | `OnMoveRejected(src,dst,colorId,reason)` |
| MOVE_EXECUTING | `OnAnimationComplete(seqId)` (matching) | → IDLE (if not win) or WIN | `OnMoveExecutingExited(seqId)` on IDLE path only |
| MOVE_EXECUTING | `OnPuzzleSolved(moveCount)` | → WIN | GSM transitions to COMPLETE |

**Move validation** (synchronous pull from GSM on BOLT_SELECTED tap):
1. **Empty destination**: accepts any color
2. **Full destination** (length == stackDepth): rejects — `MoveRejectReason.DestinationFull`
3. **Non-empty, non-full destination**: accepts only if top bolt (`destination[length-1]`) matches `colorId` — else `MoveRejectReason.ColorMismatch`

**Win condition** (checked after each MOVE_EXECUTING exit):
All color stacks are full (length == stackDepth) AND monochromatic (all elements equal). Temp slots may contain bolts at win — only color stacks are evaluated.

**Deadlock check** (depth-1, called on `OnMoveExecutingExited` if game is not won):
For each bolt on top of any stack or temp slot: check if it can move to any other valid destination. If no legal move exists across all held bolts → emit `OnDeadlockDetected`. This is advisory — the player can still undo.

### Atomic Board Mutation (5 Steps)

Triggered by `GSM.HandleMoveCommitted(src, dst, colorId, seqId)` (subscribed to `SortMechanic.OnMoveCommitted`). All 5 steps execute synchronously on the main thread within this single callback:

```
Step 1: Remove top bolt from _stackContents[src] (or _tempSlotContents[src])
Step 2: Add bolt to _stackContents[dst] (or _tempSlotContents[dst]) — colorId appended
Step 3: Push UndoEntry { From = src, To = dst, ColorId = colorId, SeqId = currentSequenceId }
Step 4: _currentSequenceId++
Step 5: _moveCount++
```

After step 5: fire `OnBoardStateChanged(currentSequenceId, moveCount)`.

**Invariant enforced after every mutation**: `sum(stackContents[i].Length) + sum(tempSlotContents[j].Length) == colorCount × stackDepth` (bolt count conserved).

### Undo Stack

```csharp
[Serializable]
public struct UndoEntry
{
    public int From;     // source stack/slot index
    public int To;       // destination stack/slot index
    public int ColorId;
    public int SeqId;    // sequence ID at time of commit
}

private readonly List<UndoEntry> _undoStack = new();
```

- Unlimited depth during ACTIVE state (practical max: ~200 entries per level, ~3.2 KB)
- Frozen on COMPLETE: no new entries added; existing entries readable for stats/replay
- Undo operation: pop last `UndoEntry`, reverse mutation (move bolt from `To` back to `From`), `currentSequenceId++`, fire `OnBoardStateChanged` (board snap signal to AnimationSystem)
- **Deferred undo** during MOVE_EXECUTING: if `UndoRequested()` called while in MOVE_EXECUTING state, store `_pendingUndo = true`; process on `OnMoveExecutingExited(seqId)` (IDLE path only — not WIN, not watchdog)

### Watchdog Coroutine

```csharp
private Coroutine _watchdogCoroutine;

// Called on entering MOVE_EXECUTING state
private void StartWatchdog(int seqId)
{
    _watchdogCoroutine = StartCoroutine(WatchdogRoutine(seqId));
}

private IEnumerator WatchdogRoutine(int seqId)
{
    yield return new WaitForSecondsRealtime(WatchdogTimeoutMs / 1000f);
    // Only fires if not cancelled first
    _pendingUndo = false;  // cancel any deferred undo on watchdog trigger
    OnBoardRefreshForced?.Invoke(seqId);
}

// Called on every exit from MOVE_EXECUTING (normal path, WIN path, AND TEARDOWN/OnDestroy)
private void CancelWatchdog()
{
    if (_watchdogCoroutine != null)
    {
        StopCoroutine(_watchdogCoroutine);
        _watchdogCoroutine = null;
    }
}

// OnDestroy MUST call CancelWatchdog() to prevent coroutine leak on DDOL MonoBehaviour
private void OnDestroy() => CancelWatchdog();
```

`WaitForSecondsRealtime` is used (not `WaitForSeconds`) — fires even if `Time.timeScale` is 0 (e.g., pause screen).

### Board State Serialization (TR-GSM-011: SER-01 / SER-02 / SER-03)

**SER-01 — Serialize on backgrounding (`OnApplicationPause(true)`):**

**SEO ordering prerequisite (Sort Mechanic EC-14):** `SortMechanic.OnApplicationPause` must execute before `GSM.OnApplicationPause`. When the app backgrounds during BOLT_SELECTED, SortMechanic cancels the held bolt and returns it to source before GSM serializes. This ensures GSM always serializes a complete board where `total_bolts = color_count × stack_depth`. ADR-0001's Script Execution Order must include an explicit entry: SortMechanic executes before GSM in `OnApplicationPause` (lower SEO number = higher priority).

**Fields serialized via `SP.SetBoardSnapshot()`:**

```csharp
// Called synchronously within GSM.OnApplicationPause(true)
// SP owns I/O atomicity (ADR-0003 W-2 synchronous path)
SP.SetBoardSnapshot(new BoardSnapshot
{
    LevelId          = _levelId,
    ColorCount       = _colorCount,
    StackDepth       = _stackDepth,
    TempSlotDepth    = _tempSlotDepth,
    TempSlotCount    = _tempSlotCount,
    StackContents    = _stackContents,      // int[][]
    TempSlotContents = _tempSlotContents,   // int[][]
    MoveCount        = _moveCount,
    SequenceId       = _currentSequenceId,
    UndoStack        = _undoStack.ToArray() // UndoEntry[]
});
```

- **Not serialized:** Sort Mechanic held state (cancelled before this path), Animation System in-flight coroutine state (discarded — board snaps to serialized state on restore).
- **Only serialized in ACTIVE state.** If GSM is in LOADING, COMPLETE, or TEARDOWN when the app backgrounds, `SetBoardSnapshot` is skipped — SP retains whatever state was last written.

**SER-02 — Deserialize on foreground restore:**

On `OnSaveReady`, if SP reports an active board snapshot (`boardSnapshot.LevelId > 0`), GSM reads the snapshot and populates board arrays synchronously before transitioning to ACTIVE:

```csharp
// Load board from snapshot — called in GSM.Start() after SP.IsReady
if (SP.HasBoardSnapshot && SP.BoardSnapshot.LevelId > 0)
{
    var snap = SP.BoardSnapshot;
    _levelId         = snap.LevelId;
    _colorCount      = snap.ColorCount;
    _stackDepth      = snap.StackDepth;
    _tempSlotDepth   = snap.TempSlotDepth;
    _tempSlotCount   = snap.TempSlotCount;
    _stackContents   = snap.StackContents;
    _tempSlotContents = snap.TempSlotContents;
    _moveCount       = snap.MoveCount;
    _undoStack       = new List<UndoEntry>(snap.UndoStack);

    // Increment seqId on restore: any animation_complete signal pending from before
    // backgrounding carries the old seqId and is silently discarded (ADR-0002 stale-signal guard)
    _currentSequenceId = snap.SequenceId + 1;

    PublicStackContents    = _stackContents;
    PublicTempSlotContents = _tempSlotContents;
    // Transition: LOADING → ACTIVE
    OnLevelLoaded?.Invoke(_levelId, _colorCount);
}
```

**SER-03 — Deserialization failure:**

If `SP.HasBoardSnapshot` is false or the snapshot fails validation (missing fields, mismatched bolt count), GSM remains in UNLOADED and emits `OnSessionLoadFailed`. HUD or session controller subscribes to present recovery UI. GSM does not self-heal — a corrupt board snapshot is treated as "no active level." Completed level records in the SP schema are unaffected (stored in a separate section); only the in-progress board is lost.

```csharp
if (!ValidateBoardSnapshot(snap))
{
    _currentLifecycleState = GSMLifecycleState.Unloaded;
    OnSessionLoadFailed?.Invoke();
    return;
}
```

Validation: `sum(snap.StackContents[i].Length) + sum(snap.TempSlotContents[j].Length) == snap.ColorCount × snap.StackDepth`.

### CoinEconomy Idempotency Guard

```csharp
// CoinEconomy internal state
private int _coinBalance;
private readonly Dictionary<EarnSource, int> _lastCreditedLevelId = new()
{
    { EarnSource.Base, 0 },
    { EarnSource.AdBonus, 0 },
    { EarnSource.PityGrant, 0 }
};

public bool AddCoins(int amount, int levelId = -1, EarnSource source = EarnSource.Base)
{
    if (levelId > 0)  // -1 = manual grant (pity, etc.) — skip guard
    {
        if (_lastCreditedLevelId.TryGetValue(source, out int last) && levelId <= last)
            return false;  // already credited — idempotent no-op
        _lastCreditedLevelId[source] = levelId;
    }
    _coinBalance = Math.Min(_coinBalance + amount, int.MaxValue);
    SaveSystem.Instance.SetCoinBalance(_coinBalance);
    OnCoinBalanceChanged?.Invoke(_coinBalance, amount);
    return true;
}

public bool SpendCoins(int amount)
{
    if (_coinBalance < amount) return false;
    _coinBalance = Math.Max(0, _coinBalance - amount);  // floor = 0 (hard)
    SaveSystem.Instance.SetCoinBalance(_coinBalance);
    OnCoinBalanceChanged?.Invoke(_coinBalance, -amount);
    return true;
}
```

**First install starter grant** (CE-11): On `OnSaveReady`, if `SaveSystem.GetCoinBalance() == 0` AND `SaveSystem.GetCurrentLevelId() == 1` (never played), call `AddCoins(150, -1, EarnSource.Base)` unconditionally (bypass idempotency — -1 levelId).

### Level Progression Formulas

These formulas are simple enough to live as properties/methods on LevelProgression with no further architectural complexity:

```csharp
public bool IsLocked(int levelId) => levelId > _currentLevelId;
public bool IsBreather(int levelId) => levelId % 10 == 0;
public int GetBestStars(int levelId) =>
    _completionRecords.TryGetValue(levelId, out var r) ? r.BestStars : 0;

// Called on GSM.OnLevelComplete (4-arg canonical signature per ADR-0012):
// parMoves is provided by GSM (read from LDS before emitting) — LP does not need LDS.GetLevel().
private void HandleLevelComplete(int levelId, int moveCount, int parMoves, int sequenceId)
{
    int stars = StarRatingCalculator.Compute(moveCount, parMoves, _threshold2Star);
    if (stars == 0) { _consecutiveZeroStar++; return; }  // don't advance on 0-star

    int newBest = Math.Max(GetBestStars(levelId), stars);
    SaveSystem.Instance.WriteCompletionAtomic(levelId, newBest, completionVersion, levelId + 1);
    _currentLevelId = levelId + 1;
    _consecutiveZeroStar = 0;
    OnLevelCompleted?.Invoke(stars, levelId, moveCount, parMoves);
}
```

**Star rating formula** uses the shared `StarRatingCalculator.Compute(int moveCount, int parMoves, float threshold2Star)` utility (ADR-0012). LP computes stars directly from the GSM `OnLevelComplete` payload — it does not wait for or receive stars from LevelCompleteUI. The `threshold2Star` tuning knob is owned by LevelCompleteUI's GDD and read by LP at initialization.

### Architecture Diagram

```
GSM.LoadLevel(levelId) [LOADING]
    ├── LDS.GetLevel(levelId) → LevelRecord
    ├── bolt_count_invariant check (throws on fail)
    ├── Allocate int[][] (colorCount arrays, each pre-filled from LevelRecord.colorStacks)
    ├── _currentSequenceId = 0; _moveCount = 0
    └── → ACTIVE; OnLevelLoaded?.Invoke(levelId, colorCount)

Player tap → SortMechanic reads StackContents[i] synchronously
    [valid move] → OnMoveCommitted(src, dst, colorId, seqId) →
        GSM.HandleMoveCommitted():
            Step 1-5 synchronous mutation
            OnBoardStateChanged(seqId, moveCount)
        AnimationSystem starts coroutine
        StartWatchdog(seqId) [MOVE_EXECUTING]
    
OnAnimationComplete(seqId) [matching] →
    SortMechanic checks win condition
    [not win] → OnMoveExecutingExited(seqId)
        CancelWatchdog()
        if (_pendingUndo) ExecuteUndo()
        Deadlock check → if deadlock: OnDeadlockDetected
    [win] → OnPuzzleSolved(moveCount)
        GSM → COMPLETE; OnLevelComplete(levelId, moveCount, parMoves, sequenceId)
        LevelProgression.HandleLevelComplete(levelId, moveCount, parMoves, sequenceId) → WriteCompletionAtomic
        LevelProgression.OnLevelCompleted → CoinEconomy.AddCoins(reward, levelId, Base)
```

### Key Interfaces

```csharp
public interface IGameStateManager
{
    // Board state (read-only, synchronous pull)
    IReadOnlyList<int>[] StackContents { get; }
    int StackDepth { get; }
    IReadOnlyList<int>[] TempSlotContents { get; }
    int TempSlotDepth { get; }
    int TempSlotCount { get; }
    int ColorCount { get; }
    int MoveCount { get; }
    int CurrentSequenceId { get; }
    GSMLifecycleState CurrentState { get; }

    // Events
    event Action<int, int> OnLevelLoaded;         // (levelId, colorCount)
    event Action<int, int> OnBoardStateChanged;   // (sequenceId, moveCount)
    event Action<int>      OnBoardRefreshForced;  // (sequenceId) — watchdog
    event Action<int, int, int, int> OnLevelComplete;  // (levelId, moveCount, parMoves, sequenceId) — canonical per ADR-0012
    event Action<int>      OnLevelUnloaded;       // (levelId) — emitted on TEARDOWN; consumers release level-scoped resources
    event Action           OnSessionLoadFailed;

    // Commands
    void LoadLevel(int levelId);
    void UndoRequested();
}

public enum GSMLifecycleState { Unloaded, Loading, Active, Complete, Teardown }

public struct UndoEntry { public int From; public int To; public int ColorId; public int SeqId; }

public interface ICoinEconomy
{
    int GetCoinBalance();
    bool AddCoins(int amount, int levelId = -1, EarnSource source = EarnSource.Base);
    bool SpendCoins(int amount);
    event Action<int, int> OnCoinBalanceChanged;  // (newBalance, delta)
}

public enum EarnSource { Base, AdBonus, PityGrant }

public interface ILevelProgression
{
    bool IsLocked(int levelId);
    bool IsBreather(int levelId);
    int GetBestStars(int levelId);
    event Action<int, int, int, int> OnLevelCompleted;  // (stars, levelId, moveCount, parMoves)
}
```

## Alternatives Considered

### Alternative A: `List<Stack<int>>` for Board State
- **Description**: Each color stack as a `Stack<int>` inside a `List`, providing push/pop semantics
- **Pros**: Semantically clearer; `Push`/`Pop` match bolt operations; can't accidentally write to the middle
- **Cons**: `Stack<int>` is not `IReadOnlyList<int>`-compatible (no index access); must convert to array for read-only exposure; `List<Stack>` has dynamic resize GC; iteration for win condition check requires `ToArray()` (allocation)
- **Rejection Reason**: Incompatible with the required `IReadOnlyList<int>[]` read interface; GC overhead on mobile

### Alternative B: `int[,]` Rectangular 2D Array
- **Description**: `int[maxStacks, maxStackDepth]` — single allocation, row = stack, column = slot
- **Pros**: Single heap allocation; cache-friendly layout
- **Cons**: All stacks fixed at `maxStackDepth` size; variable occupancy requires a separate `int[] stackLengths` array; "empty" cells waste space for sparse stacks; column iteration for win check requires explicit stride arithmetic
- **Rejection Reason**: Variable occupancy is fundamental to BoltSort gameplay; jagged arrays model it naturally

### Alternative C: Task.Delay-Based Watchdog
- **Description**: Replace `Coroutine + WaitForSecondsRealtime` with `Task.Delay(1500)` + `CancellationToken`
- **Pros**: `CancellationToken` is cleaner than `StopCoroutine` for cancellation; no MonoBehaviour dependency
- **Cons**: `Task.Delay` uses wall-clock time by default but requires careful token management; Unity 6 `Awaitable` is preferred over raw `Task`; `Coroutine` is simpler to read and has zero risk of marshaling errors
- **Rejection Reason**: `Coroutine + WaitForSecondsRealtime` is well-understood in Unity context; the project's current pattern for async timing is Coroutine for game logic and `Awaitable` only for background I/O (ADR-0003)

## Consequences

### Positive
- Sole-owner rule on board state eliminates race conditions — enforced by architecture convention + code review
- Monotonic sequence ID enables stale-signal detection across all consumers (ADR-0002 contract)
- Synchronous 5-step mutation means AnimationSystem always starts from a consistent board snapshot
- Deferred undo correctly handles the undo-during-animation timing edge case without requiring a separate FSM state
- CE idempotency guard prevents duplicate coin awards across sessions (covers network/save race conditions)

### Negative
- `IReadOnlyList<int>[]` outer array is technically writable — must be enforced by convention and code review; failing silently
- Unlimited undo stack has no cap — in a pathological case (infinite retry loop on a 200-move level), memory grows. Practical max ~200 entries per level (~3 KB) is not a real concern but lacks a hard guard
- Watchdog coroutine must be stopped in `OnDestroy` — forgetting breaks the DDOL lifetime model

### Risks
- **Risk**: `StopCoroutine` not called in TEARDOWN or `OnDestroy` → watchdog fires after level unload → `OnBoardRefreshForced` fires against a null or stale board. **Mitigation**: `CancelWatchdog()` called in both paths; `OnDestroy` guard is documented in this ADR; control manifest will enforce it.
- **Risk**: `CurrentSequenceId` wraps past `int.MaxValue` on an extremely long play session. **Mitigation**: At 1 move/second for 24h = ~86,400 moves; `int.MaxValue` = ~2.1 billion. Not a practical risk within any session.
- **Risk**: Outer `IReadOnlyList<int>[]` replaced by a bug (e.g., `StackContents[0] = someOtherList`) — stale display data without an obvious exception. **Mitigation**: Code review rule: no write to `StackContents[i]` outside GSM; future hardening with `IReadOnlyList<IReadOnlyList<int>>` wrapper if violations occur.
- **Risk**: CE `_lastCreditedLevelId` not persisted — if app restarts mid-session, the idempotency guard resets. On next cold start, a duplicate `AddCoins(Base, levelId=N)` for the just-completed level could fire again. **Mitigation**: Architecture doc shows `CoinEconomy` reads `coin_balance` from SaveSystem; if LP calls `AddCoins(reward, levelId, Base)` and then crashes before W-1 write completes, the balance is already updated in CE's working copy, but the save hasn't landed. On restart, CE reads the old balance from save.json and LP doesn't re-fire the reward (LP checks completion record, not balance). The idempotency guard is defense-in-depth for the ad-bonus path, not the primary consistency mechanism.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| game-state-manager.md | TR-GSM-001: Sole owner of board state arrays | Documents GSM as sole writer; `IReadOnlyList<int>[]` exposes read-only view; forbidden pattern `direct_cross_system_state_write` registered |
| game-state-manager.md | TR-GSM-002: Monotonic sequence ID, never decrements | `_currentSequenceId` incremented at step 4 of mutation; only GSM increments it |
| game-state-manager.md | TR-GSM-003: Unlimited undo stack; frozen on COMPLETE | `List<UndoEntry>` with no cap; frozen on `COMPLETE` FSM state transition |
| game-state-manager.md | TR-GSM-004: Watchdog 1500ms → `OnBoardRefreshForced` | Coroutine + `WaitForSecondsRealtime(1.5f)`; `StopCoroutine` on every exit |
| game-state-manager.md | TR-GSM-005: Atomic board mutation (5 steps synchronous) | Documented 5-step sequence in `HandleMoveCommitted()` callback |
| game-state-manager.md | TR-GSM-006: Deferred undo on MOVE_EXECUTING | `_pendingUndo` flag; processed in `OnMoveExecutingExited` (IDLE path only) |
| game-state-manager.md | TR-GSM-007: bolt_count_invariant check at level load | Sum check + per-color check in `LoadLevel()` [LOADING state] |
| game-state-manager.md | TR-GSM-008: Level lifecycle FSM | `GSMLifecycleState` enum; UNLOADED/LOADING/ACTIVE/COMPLETE/TEARDOWN transitions |
| game-state-manager.md | TR-GSM-009: Emit typed C# events | All GSM events are `event Action<T>` per ADR-0002 |
| game-state-manager.md | TR-GSM-010: `OnLevelComplete` 4-arg payload (levelId, moveCount, parMoves, sequenceId) | `IGameStateManager.OnLevelComplete` updated to `Action<int,int,int,int>`; GSM reads parMoves from LDS before emitting; aligned with ADR-0012 |
| game-state-manager.md | TR-GSM-011: Board state serialization on backgrounding (SER-01/02/03) | Serialization section above documents field list, seqId increment on restore, and session_load_failed failure path |
| sort-mechanic.md | TR-SORT-001: Sort Mechanic FSM states | Sort Mechanic FSM documented here; IDLE/BOLT_SELECTED/MOVE_EXECUTING/WIN/CANCELLATION/INVALID_MOVE |
| sort-mechanic.md | TR-SORT-002: Move validation rules | Empty/full/color-match validation documented |
| sort-mechanic.md | TR-SORT-003: Win condition | All stacks full + monochromatic; temp slots excluded |
| sort-mechanic.md | TR-SORT-005: Shallow deadlock check → `OnDeadlockDetected` | Depth-1 check documented; triggered on `OnMoveExecutingExited` (non-win) |
| sort-mechanic.md | TR-SORT-009: Synchronous pull of board state from GSM | `StackContents` / `TempSlotContents` are synchronous properties; no async required |
| coin-economy.md | TR-CE-002: `AddCoins` with idempotency guard | `Dictionary<EarnSource, int>` guard documents; skipped on `levelId == -1` |
| coin-economy.md | TR-CE-003: `SpendCoins`; floor = 0 | `Math.Max(0, _coinBalance - amount)` floor enforced |
| level-progression.md | TR-LP-001: `is_locked = (levelId > currentLevelId)` | Property documented; `_currentLevelId` advances on completion |
| level-progression.md | TR-LP-002: `best_stars = max(current, earned)` | `Math.Max(GetBestStars(levelId), stars)` before `WriteCompletionAtomic` |

## Performance Implications
- **CPU**: Atomic 5-step mutation: ~0.01ms (pure array operations). Win condition check after each move: O(colorCount × stackDepth) ≤ 8×8=64 iterations. Deadlock check: O(N²) where N ≤ 11 stacks — ~120 comparisons. All negligible.
- **Memory**: Board state arrays: colorCount × stackDepth × sizeof(int) ≈ 8×8×4 = 256 bytes. Undo stack: 200 entries × ~20 bytes = ~4 KB peak. CE dictionary: 3 entries = negligible.
- **Load Time**: `LoadLevel()` allocates and fills board arrays synchronously; ~0.1ms for max-size board.
- **Network**: N/A

## Migration Plan
No existing code to migrate — written before implementation begins.

## Validation Criteria
1. Unit test: 5-step mutation sequence — verify `StackContents[src].Length` decrements, `StackContents[dst].Length` increments, `CurrentSequenceId` increments, `OnBoardStateChanged` fires
2. Unit test: Win condition — load a pre-solved board; verify `IsWin()` returns true; verify partial board returns false
3. Unit test: Undo during MOVE_EXECUTING — call `UndoRequested()` in MOVE_EXECUTING; verify deferred flag set; verify undo executes on `OnMoveExecutingExited`
4. Unit test: CE idempotency — call `AddCoins(10, 5, EarnSource.Base)` twice; verify balance increased by 10 only once
5. Unit test: Watchdog fires — simulate 1.6s without `OnMoveExecutingExited`; verify `OnBoardRefreshForced` fires
6. Integration test: Full move cycle — tap → BOLT_SELECTED → valid tap → animation → `OnAnimationComplete` → IDLE

## Related Decisions
- ADR-0001: Singleton Architecture and Boot Sequence — GSM at SEO −50
- ADR-0002: Event and Signal Architecture — GSM events, `OnMoveExecutingExited` on IDLE path only
- ADR-0003: Save System Design — `WriteCompletionAtomic` called by LP on level completion
- ADR-0004: Level Data Loading Strategy — `GSM.LoadLevel()` calls `LDS.GetLevel()` synchronously
- ADR-0007: Input Handling Strategy — SortMechanic reads GSM board state synchronously
- `design/gdd/game-state-manager.md`, `design/gdd/sort-mechanic.md`, `design/gdd/coin-economy.md`, `design/gdd/level-progression.md`
