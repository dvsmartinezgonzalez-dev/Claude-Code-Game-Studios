# Save & Persistence

> **Status**: Approved
> **Author**: Design session + agents
> **Last Updated**: 2026-04-23
> **Implements Pillar**: Flow Over Friction, Respect the Session

## Overview

Save & Persistence is BoltSort's data layer: it serializes, stores, and retrieves all player-facing state that must survive between sessions. It owns four data categories — level progress (`current_level_id`, `completion_record[]`), economy state (`coin_balance`, provisionally until Coin Economy GDD is authored), settings preferences (`audio.*` and `qts.*` namespaces), and skin ownership (deferred to Skin System GDD). It defines the schema version contract that governs all save data, and the atomic write primitive that Level Progression requires to advance `current_level_id` and `best_stars[N]` as a single operation. The player never interacts with this system directly. What they experience is its guarantee: returning to BoltSort always means returning exactly to where they left off — level, coins, and skins intact.

## Player Fantasy

Save & Persistence has no direct fantasy of its own. What it delivers is a guarantee: whatever state the game held when you last closed it — level, coins, settings, skins — is the state it holds when you open it again. This is what allows short, interrupted play sessions to stitch together without friction. The feeling of returning to your place belongs to Level Progression; this system is what makes that feeling reliable.

**Tradeoff — backup exclusion vs. cross-device continuity:** The "Respect the Session" pillar is upheld for single-device play: progress is never lost from a crash, OOM kill, or force-quit. However, save data is intentionally excluded from iCloud and Android Auto-Backup. A player who switches devices, reinstalls the app, or restores from backup starts over at level 1. This is a deliberate product decision: stale backup overwrites — where an older cloud save silently replaces current progress — are a worse experience for the BoltSort player base than the one-time loss on device switch. This tradeoff holds as long as BoltSort has no account system. If cross-device sync is added in a future milestone, backup exclusion must be revisited.

*Primary pillars: Respect the Session, Flow Over Friction*
*MDA target: (infrastructure — enables Sensation and Submission/Flow via Level Progression)*

## Detailed Design

### Core Rules

**C.1 — Storage Architecture**

Save & Persistence uses a split storage model:

- **JSON file** (`Application.persistentDataPath/save.json`) — structured data requiring atomicity, versioning, or arrays: level progress, economy state, skin ownership. This is the authoritative store for all cross-session progress.
- **PlayerPrefs** — scalar settings only (audio volumes, QTS tier). Simple, low-frequency writes. Not suitable for structured data or atomic writes.

No system reads `save.json` directly. All access goes through the Save & Persistence interface. PlayerPrefs keys are read directly by their owning systems (Audio System reads `audio.*` on Awake; QTS reads `qts.*` on startup).

**Backup exclusion:** `save.json` is excluded from iCloud and Android Auto-Backup. iOS: `NSURLIsExcludedFromBackupKey = true` applied to `Application.persistentDataPath/save.json` immediately after first file creation. Android: `<cloud-backup-rules>` XML excludes `save.json` and `save.tmp`. Player data is device-local only. Players who switch devices or restore from backup start from defaults (level 1, 0 coins). This guarantees the Player Fantasy is never violated by a stale backup overwriting current progress.

**Concurrency model:** All write paths use a single `SemaphoreSlim(1, 1)` named `_writeLock`. Acquisition uses `destroyCancellationToken` (Unity 6 MonoBehaviour token) to prevent leaked operations if the MonoBehaviour is destroyed mid-write:

- **W-1** acquires `_writeLock` on a background thread. Mandatory sequence:
  1. On the **main thread**: pre-cache `Application.persistentDataPath` and capture an immutable snapshot of all mutable state to be written — `current_level_id`, `best_stars`, `completion_version`, and `undo_stack[]`. The snapshot is a value copy, not a reference; this prevents data races with GSM mutations that occur concurrently on the main thread.
  2. Call `await Awaitable.BackgroundThreadAsync()` to switch to a background thread.
  3. Call `await _writeLock.WaitAsync(destroyCancellationToken)` to acquire the lock.
  4. Immediately call `await Awaitable.BackgroundThreadAsync()` **again** — defensive re-assertion to guarantee file I/O executes on a thread-pool thread regardless of how `WaitAsync` schedules its continuation. `SemaphoreSlim.WaitAsync` does not capture or restore a `SynchronizationContext`; the re-assertion is forward-correctness insurance, not a workaround for a known current bug. **Do not remove this call** — a future refactoring that changes the surrounding async context could silently route I/O back to the main thread without it.
  5. Serialize the snapshot and perform all file I/O using only `System.IO` APIs. No Unity API calls (`Application.*`, `Debug.*`, MonoBehaviour fields) inside the locked section — these are main-thread-only and will deadlock W-2 (see W-2-during-W-1 below).
  6. Release the lock inside a `try/finally` block: `finally { _writeLock.Release(); }`. This guarantees release even if serialization throws mid-way; a leaked semaphore permanently deadlocks all subsequent write attempts.

- **W-2** acquires `_writeLock` using `_writeLock.Wait(destroyCancellationToken)` (synchronous overload with CancellationToken — not `WaitAsync`) on the main thread within `OnApplicationPause(true)`. No `await` expression may appear in the `OnApplicationPause` callback body — `async void` must not be used, as Unity returns control to the OS at the first `await`, abandoning the write under iOS suspension. At ~17 KB, a synchronous write-then-swap completes in 2–8 ms on target devices — well within the iOS 5-second suspension budget. W-2 checks the dirty flag **after** acquiring `_writeLock` (post-lock, not pre-lock) to prevent a race where W-1 clears dirty between the check and the lock acquisition. If `destroyCancellationToken` cancels W-2's lock-wait (MonoBehaviour destroyed while W-2 is blocked), catch `OperationCanceledException` silently — preserve the dirty flag as `true`, perform no write, and allow OS suspension to proceed normally.

- **W-2-during-W-1 (no-deadlock precondition):** W-2 blocks the main thread in `_writeLock.Wait()` while W-1 holds the lock on a background thread. This is safe only because W-1's locked section contains no Unity API calls (enforced by step 5 above). If W-1 were to call any main-thread-only Unity API after acquiring the lock, neither thread could proceed. This constraint is non-negotiable.

- **Queue**: If W-1 fires while a write is in progress (semaphore held), a pending-write token is stored. When the lock releases, the token triggers a fresh serialization of the latest in-memory state. Queue depth is capped at 1 (a second pending trigger is discarded — the token always captures current state at flush time).

- **W-2-during-W-1 (no-duplicate-write):** W-2 checks the dirty flag after acquiring the lock — if W-1 cleared it, W-2 performs no I/O.

---

**C.2 — JSON Schema (`save.json`)**

```json
{
  "schema_version": 1,
  "level_progress": {
    "current_level_id": 1,
    "completion_record": [
      {
        "level_id": 1,
        "best_stars": 3,
        "completion_version": "2026.04"
      }
    ],
    "undo_stack": [
      {"f": 2, "t": 0}
    ]
  },
  "economy": {
    "coin_balance": 0
  },
  "skins": {
    "_status": "reserved"
  }
}
```

| Field | Type | Range / Format | Owner | Notes |
|---|---|---|---|---|
| `schema_version` | int | 1 – unbounded | Save & Persistence | Increment once per breaking schema change. Current: 1. |
| `level_progress.current_level_id` | int | 1–9999 | Level Progression | Next level the player is authorized to load. Starts at 1. |
| `level_progress.completion_record[].level_id` | int | 1–9999 | Level Progression | One entry per completed level. No duplicates. |
| `level_progress.completion_record[].best_stars` | int | 0–3 | Level Progression | 0 = never completed (sentinel). Absent record implies 0. |
| `level_progress.completion_record[].completion_version` | string | `"YYYY.MM"` | Level Progression | Written once on first completion; never overwritten. (LP OQ-03) |
| `economy.coin_balance` | int | 0–INT_MAX | **Coin Economy** | Ownership transferred from Level Progression per Coin Economy GDD CE-01. CE reads this field on `OnSaveReady`; writes via `SP.SetCoinBalance(int)`. |
| `skins` | object | reserved | Skin System (future) | Written as `{"_status":"reserved"}` until Skin System GDD is authored. No game system reads this block. |
| `level_progress.undo_stack[]` | array | 0–20 entries | Game State Manager | Last N committed moves: `{"f": from_stack_index, "t": to_stack_index}`. Written on W-1 and W-2. Loaded on cold start to restore undo history. Oldest entry discarded when count exceeds 20. |

**Undo persistence (conscious decision):** The last 20 moves are persisted across sessions. A player who is interrupted mid-puzzle returns with both their board position and their undo history intact — consistent with "Flow Over Friction." The GSM reads `undo_stack[]` on cold start and reconstructs its internal undo queue. If the stack is empty or absent, undo is simply unavailable (not an error).

**Schema v0 (pre-versioning legacy):** Files written before `schema_version` was introduced have no `schema_version` key. They are treated as v0. The v0 schema is the minimal early-development structure: `{"current_level_id": int, "completion_record": [...], "coin_balance": int}` — a flat layout without nesting under `level_progress` or `economy`. `migrate_v0_to_v1` restructures these into the C.2 v1 schema, filling absent fields with defaults and setting `schema_version = 1`. Additionally: (a) `migrate_v0_to_v1` initializes `level_progress.undo_stack = []` (empty array — v0 files have no undo history to restore); (b) v0 `completion_record[]` entries have no `completion_version` field — `migrate_v0_to_v1` leaves `completion_version` absent on those entries rather than backfilling it (`completion_version` is write-once at the persistence layer; migration must not synthesise a value it does not know).

---

**C.3 — PlayerPrefs Namespace Table**

| Key | Type | Default | Owner GDD | Written by |
|---|---|---|---|---|
| `audio.sfx_volume` | float | 1.0 | Audio System | Settings UI |
| `audio.ambient_volume` | float | 1.0 | Audio System | Settings UI |
| `audio.ui_volume` | float | 1.0 | Audio System | Settings UI |
| `qts.tier` | int | -1 (auto-detect sentinel) | Quality Tier System | QTS (auto-detect) + Settings UI |

**`sp.*` keys (Save & Persistence internal):**

| Key | Type | Default | Purpose |
|---|---|---|---|
| `sp.downgrade_notice_shown` | int | 0 | Set to 1 after the Case R-5 downgrade notice is shown. Prevents the notice from repeating on subsequent cold starts. Cleared only if the player clears app data. |

**Reserved namespaces:**

| Prefix | Reserved for | Status |
|---|---|---|
| `audio.*` | Audio System | Active |
| `qts.*` | Quality Tier System | Active |
| `sp.*` | Save & Persistence | Active |

**Namespace claim rule:** Any future system requiring PlayerPrefs keys must (1) choose a unique prefix not in this table, (2) define all keys in its own GDD, (3) add the prefix to this table via a Save & Persistence GDD update before the first implementation sprint for that system. Writing a key in an unregistered namespace is a defect.

**PlayerPrefs write rule:** Call `PlayerPrefs.Save()` immediately after every write. Do not rely on `OnApplicationQuit` — it is not guaranteed to fire on Android process kills.

---

**C.4 — Write Contract**

Three triggers initiate a save operation:

**W-1 — Level completion.** When Level Progression signals level completion, Save & Persistence performs one atomic write containing all of: `current_level_id` advanced to N+1, `completion_record[N].best_stars` updated (if improved), `completion_record[N].completion_version` written (if first completion), and the current `undo_stack[]` snapshot. These four data points are never written individually — they are bundled into a single write-then-swap operation using the snapshot captured on the main thread before the background thread switch (see C.1). This satisfies LP EC-16 / AC-18.

**W-2 — App pause.** On `OnApplicationPause(true)` only (not `OnApplicationFocus(false)`). Writes the full JSON if and only if a dirty flag indicates in-memory state has changed since the last successful write — checked **after** acquiring `_writeLock` (see C.1 concurrency model). If nothing changed, no I/O is performed. The write must be synchronous or fully awaited before the callback returns — do not fire-and-forget on iOS (5-second suspension budget).

**W-3 — Settings change.** Handled directly by each owning system via `PlayerPrefs.Set*()` + `PlayerPrefs.Save()`. Save & Persistence does not mediate PlayerPrefs writes.

**Write-then-swap procedure (W-1 and W-2):**

1. Serialize full in-memory save state to UTF-8 JSON string.
2. Write string to `Application.persistentDataPath/save.tmp` using `FileStream` with `FileMode.Create`.
3. Call `FileStream.Flush(flushToDisk: true)` to push OS write buffers to storage, then close the file handle. This step is required on both iOS and Android — `File.WriteAllText` does not guarantee durable flush before the rename.
4. Replace `save.json` with `save.tmp`. **Implementation note: `File.Move(source, dest, overwrite: true)` (three-argument overload) does not exist in .NET Standard 2.1 (Unity 6) — it was added in .NET 5+. Using it causes a compilation error.** Use the correct API for each case:
   - **Typical case (`save.json` already exists):** `File.Replace(save.tmp, save.json, destinationBackupFileName: null)` — available in .NET Standard 2.1. Atomically replaces the destination with the source. No backup file is created.
   - **First-ever write (`save.json` does not exist, e.g., Case R-3's first W-1 or W-2 trigger):** `File.Move(save.tmp, save.json)` (two-argument overload) — no existing destination to replace.

Step 4 is atomic on iOS (APFS) and Windows (NTFS) — `File.Replace` maps to `renameat2`/`ReplaceFileW`. On Android ext4/F2FS **internal** storage it is atomic. On Android 11+ devices where `persistentDataPath` is backed by a FUSE overlay, the rename may be emulated as a copy-then-delete by the FUSE layer — atomicity is best-effort. Additionally, `FileStream.Flush(flushToDisk: true)` on Android 11+ FUSE paths may succeed without guaranteeing physical durable write (weaker than a direct `ext4` `fsync`). This is a known platform limitation. Power loss between steps 3 and 4 may leave `save.tmp` on disk; this is covered by cold-start cleanup.

**Write failure handling:**
- If `File.Replace` or `File.Move` throws `IOException` or `UnauthorizedAccessException`: (a) leave `save.json` intact, (b) delete `save.tmp`, (c) log to analytics, (d) retry on the next W-1 or W-2 trigger.
- If step 2 fails (disk full): catch `IOException`, surface non-blocking message ("Could not save progress — please free up storage space"), do not crash.
- All catch blocks must catch both `IOException` and `UnauthorizedAccessException`. These are siblings in .NET (`SystemException` subclasses); `catch(IOException)` will **not** catch `UnauthorizedAccessException`.

**`save.tmp` cleanup:** `save.tmp` must never be left on disk beyond the session. Every write path — success, failure, or crash recovery — must either promote `save.tmp` to `save.json` or delete it before the next session ends.

---

**C.5 — Read Contract**

**Unity lifecycle anchor:** "Cold start" is the first `Awake()` call on the `SaveSystem` MonoBehaviour. `SaveSystem` must be a `DontDestroyOnLoad` singleton. Because `DontDestroyOnLoad` prevents destruction across scene loads, `Awake()` fires exactly once per process lifetime. The cold-start read sequence executes inside `Awake()`, completing — including `IsReady = true` — before any other system's `Start()` runs. `Application.persistentDataPath` must be pre-read in `Awake()` and cached; it may not be accessed on background threads.

**Singleton guard (required):** `SaveSystem.Awake()` must begin with: `if (instance != null && instance != this) { Destroy(gameObject); return; }`. Without this guard, if the SaveSystem prefab is present in multiple scenes, a second `Awake()` fires and begins a second cold-start read before the duplicate-destroy executes. The guard must be the first statement in `Awake()`, before any initialization code.

**`async void Awake()` is forbidden.** Unity does not await `Awake()` — any `await` expression in `Awake()` causes `Start()` on other MonoBehaviours to fire while `Awake()` is suspended. This would allow systems to call read methods before `IsReady = true`, violating the initialization sequencing rule. The entire cold-start sequence — including migration write-back (Case R-2) and corruption recovery write-back (Case R-4) — must execute synchronously and blocking within `Awake()`.

**Cold-start execution order:** (1) Pre-cache `Application.persistentDataPath`. (2) Check if `save.tmp` exists (record result). (3) Attempt to read and parse `save.json`. (4) Dispatch to the appropriate case (R-1 through R-5) using the combination of file existence, parse result, and `schema_version`. (5) Handle `save.tmp` as part of case dispatch — not as a separate post-dispatch step. The `save.tmp` existence result from step 2 is an input to R-4 and the cold-start cleanup rule.

Read occurs once at cold start, before any gameplay system initializes. The result is the authoritative in-memory save state. All systems query it via the Save & Persistence interface.

**Case R-1 — File valid, `schema_version == current`:** Load into memory. Any absent field defaults per C.2. Proceed.

**Case R-2 — File valid, `schema_version < current` (migration):** Apply migration functions in sequence (`migrate_v0_to_v1`, `migrate_v1_to_v2`, etc.) until `schema_version >= current`. Migrations are cumulative, ordered, and append-only — they add fields with defaults and transform existing fields; they never delete without mapping to a new location. Migration functions must be idempotent (running the same migration twice produces the same result as running it once). The idempotency guarantee relies on two mechanisms together: (1) the migration function's own logic, and (2) case-selection — once the write-back succeeds and `schema_version` is updated on disk, Case R-2 will not re-run that migration on future cold starts. Future migration authors must not assume idempotency comes from case-selection alone; each migration function must be safe to run twice on the same in-memory state. The `completion_version` field on any completion record must never be mutated by a migration function — it is write-once at the persistence layer as well as the write path. If a required migration function does not exist (gap in the chain), treat as Case R-5: reject the file, start from defaults, do not overwrite, emit analytics warning. After migration, immediately write the migrated state back to disk via write-then-swap to prevent re-migration on next cold start. **This write-back must be synchronous and blocking — it executes within `Awake()` and must complete before `IsReady = true` is set. Use the synchronous W-2-style approach (no `await`). During `Awake()`, no other threads compete for `_writeLock`; the semaphore is not required for this write.** Proceed.

**Case R-3 — File does not exist (fresh install or data cleared):** Initialize from all defaults. Do not write a file — defer first write to the first W-1 or W-2 trigger. Proceed.

**Case R-4 — File exists but fails JSON parse (corrupted):** Log the error to analytics (file size, first 256 bytes, exception message). Attempt to read `save.tmp` — if it exists and parses successfully, use it as the recovered state. If `save.tmp` also fails to parse or does not exist, fall back to defaults (same as R-3). Write the recovered or default state to `save.json` via write-then-swap immediately. **This write-back must be synchronous and blocking — identical to the R-2 write-back constraint (see above).** Show no error UI. **UI dependency note:** Silent recovery is indistinguishable from intentional progress deletion for a casual player. The UI layer (In-Game HUD or Main Menu UI GDD) must display a brief, non-blocking, non-modal system notice after R-4 fallback (e.g., "We couldn't load your last save. Starting fresh."). This GDD does not own the presentation; the UI GDD must spec it. Proceed.

**Case R-5 — `schema_version > MAX_KNOWN_VERSION` (future schema, downgrade scenario):** Reject the file. Do not attempt forward migration. Start from defaults (same as R-3). Do not overwrite the file (preserve for potential recovery). Flag a warning to analytics. Show a one-time notice only if `PlayerPrefs.GetInt("sp.downgrade_notice_shown", 0) == 0`: display "This device has a save file from a newer version of the game. Update the app to recover your progress." Then call `PlayerPrefs.SetInt("sp.downgrade_notice_shown", 1)` + `PlayerPrefs.Save()` so the notice is not repeated on subsequent cold starts. Proceed.

**`save.tmp` on cold start:** If `save.tmp` exists and `save.json` is valid, delete `save.tmp` silently. If `save.json` is also absent or corrupted, follow Case R-4.

**Initialization sequencing rule:** Save & Persistence initialization — including migration — must complete and `SaveSystem.IsReady` must be `true` before any other system reads saved values. No system may request data before this flag is set. The correct integration pattern is a callback or `await` on an `IsReady` awaitable — polling is acceptable as a fallback with a 2-second timeout. In debug builds, any read method called before `IsReady` throws `InvalidOperationException` to surface the integration error immediately. In release builds, read methods called before `IsReady` stall (await `IsReady` with a 2-second timeout), then return an explicit nullable result if the timeout elapses — callers must handle null. Returning a plausible default (e.g., `0` for `GetCurrentLevelId()`) is not acceptable: it silently allows Level Progression to attempt loading level 0, which is out of range.

---

### States and Transitions

Save & Persistence has no gameplay states. It is a stateless service plus a dirty flag.

| Event | Dirty flag |
|---|---|
| Cold start read completes | `false` |
| Any in-memory value mutated | `true` |
| Successful write-then-swap | `false` |
| Failed write (IO error) | remains `true` — retry on next trigger |

---

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| Level Progression | Bidirectional | LP reads `current_level_id` and `completion_record[]` on cold start. LP triggers W-1 writes on level completion. LP no longer holds provisional `coin_balance` ownership — transferred to Coin Economy per CE-01. SP must expose: `GetCurrentLevelId(): int`, `GetCompletionRecord(level_id): CompletionRecord?` (nullable — returns null if no record for that level or if called before IsReady with timeout elapsed), `WriteCompletionAtomic(level_id, best_stars, completion_version, new_current_level_id)`. The provisional `AddCoins(amount: int): void` method is retired — replaced by `SetCoinBalance(int)` per Cross-GDD SP-01. |
| Game State Manager | Bidirectional | GSM provides `undo_stack[]` to Save & Persistence as part of the W-1 and W-2 main-thread snapshot (captured before BackgroundThreadAsync — see C.1). On cold start, SaveSystem exposes `GetUndoStack(): List<UndoMove>` — GSM calls this after `IsReady` to restore its internal undo queue. Returns an empty list if `undo_stack` is absent or empty (not an error). |
| Audio System | Write only (PlayerPrefs) | Audio System reads `audio.*` PlayerPrefs keys directly on Awake. Settings UI writes them. Save & Persistence owns the namespace declaration only — no mediation. |
| Quality Tier System | Write only (PlayerPrefs) | QTS reads `qts.tier` directly on startup. Save & Persistence owns namespace declaration only. |
| Coin Economy | Bidirectional | CE owns `economy.coin_balance` (transferred per CE-01). CE reads the field on `OnSaveReady`. CE writes via `SP.SetCoinBalance(value: int)` — this method must atomically set `economy.coin_balance` in the in-memory save state and mark SP's internal dirty flag. CE must not call SP before `IsReady`. SP must expose: `SetCoinBalance(value: int): void` (sets in-memory `economy.coin_balance` = value; marks dirty; does not trigger a write directly — next W-1 or W-2 will flush), `SaveSystem.IsReady: bool` (synchronous read for CE's subscribe-then-check pattern per Cross-GDD SP-02), `OnSaveReady` event (fired when SP transitions to ready — exact name must be confirmed in the implementation sprint per SP OQ-05). |
| Skin System (future) | Bidirectional | `skins` block reserved. Skin System GDD will define its structure. |
| Settings UI (future) | Write only (PlayerPrefs) | Settings UI calls `PlayerPrefs.Set*()` + `PlayerPrefs.Save()` for audio and QTS keys. No Save & Persistence mediation. |
| All systems | Read | All systems query in-memory save state through the Save & Persistence interface. No system reads `save.json` directly. |

## Formulas

### Formula 1: Completion Record Lookup

The `has_completion_record` formula is defined as:

`has_completion_record(level_id) = (∃ entry in completion_record[] where entry.level_id == level_id)`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Query level | `level_id` | int | 1–9999 | The level whose record is being queried |
| Completion array | `completion_record[]` | array | 0–9999 entries | In-memory array of completion entries, each with a `level_id` key |
| Result | — | bool | {false, true} | Whether a record exists for this level |

**Output Range:** Boolean (`bool`); `null` (`bool?`) if called before `IsReady` in a release build and the 2-second timeout elapses (see Edge Cases — before-IsReady rule).

**Corollary — implicit `best_stars` default:** When `has_completion_record(level_id) == false`, `best_stars(level_id)` returns 0. No entry is created; the 0 is a read-time default only.

**Example:**
- `completion_record[] = [{level_id:1, best_stars:3}, {level_id:2, best_stars:2}]`
- `has_completion_record(3)` → `false` → `best_stars(3)` = 0
- `has_completion_record(1)` → `true` → `best_stars(1)` = 3

---

### Formula 2: Save File Size Upper Bound

The `save_file_size_upper_bound` formula is defined as:

`save_file_size_upper_bound = base_bytes + (max_levels × bytes_per_record) + undo_stack_max_bytes`

**Variables:**

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Fixed overhead | `base_bytes` | int | ≥ 0 bytes | Schema wrapper, `schema_version`, `economy`, `skins` block, JSON structural characters |
| Level count | `max_levels` | int | 1–9999 | Total level count; currently 200 at launch |
| Per-record size | `bytes_per_record` | int | ≥ 0 bytes | Bytes per `completion_record[]` entry (keys + values + JSON punctuation + indentation); conservative unminified upper bound = 105 bytes (measured at 4-space indentation with max-length field values: `level_id`=9999, `completion_version`="YYYY.MM") |
| Undo stack | `undo_stack_max_bytes` | int | ≥ 0 bytes | Max 20 entries × 30 bytes per entry (unminified `{"f":N,"t":N}` + punctuation) = 600 bytes |
| Result | — | int | bytes | Estimated maximum file size when all levels are completed |

**Output Range:** Upper bound. Worked values: `base_bytes` ≈ 180, `bytes_per_record` ≈ 105 (conservative unminified upper bound at 4-space indentation with max-length field values), `max_levels = 200`, `undo_stack_max_bytes` = 600.

`save_file_size_upper_bound = 180 + (200 × 105) + 600 = 21,780 bytes ≈ 22 KB`

**Example:** Fresh install: ~180 bytes. 50 levels completed: ~6,030 bytes. All 200 levels completed: ~22 KB. Ceiling headroom: 32 KB − 22 KB = 10 KB.

**Content-update ceiling trigger:** The 32 KB ceiling is reached at approximately 300 levels (`180 + (300 × 105) + 600 = 32,280 bytes` — 488-byte headroom). A content update that pushes total level count past 300 must re-run this formula and obtain explicit sign-off — this is a content milestone gate, not a schema-change gate.

**Skins block forward-dependency:** The `skins` object currently contributes 0 bytes to this formula (`{"_status":"reserved"}`). When Skin System GDD is authored, it must contribute a `skins_max_bytes` estimate to this formula and this formula must be re-run. Any schema change to the `skins` block is a trigger for formula re-evaluation.

Any schema change that adds a per-level field must re-run this estimate.

---

### Named Constant: MAX_KNOWN_VERSION

`MAX_KNOWN_VERSION = 1` (at launch)

| Attribute | Value |
|---|---|
| Type | int |
| Range | 1–unbounded; increments on each breaking schema change |
| Owner | Save & Persistence (GDD-local — not cross-system) |
| Used in | Case R-5 (downgrade rejection), Case R-2 (migration termination condition) |

Must be updated in-source whenever a new schema version is introduced. A mismatch between this GDD value and the implementation value is a defect.

---

> `is_save_stale` (dirty flag) — not a formula. Defined as a state machine variable in Detailed Design → States and Transitions.

## Edge Cases

- **If W-1 (level completion) and W-2 (app pause) fire in the same frame:** W-2 acquires `_writeLock` after W-1 releases it. W-2 then checks the dirty flag — if W-1 succeeded and cleared it, W-2 performs no I/O. Both triggers serialize through the single `SemaphoreSlim`; concurrent `File.Move` operations are structurally impossible.

- **If `OnApplicationPause(false)` fires while W-2 is executing (user immediately re-opens app mid-write):** The W-2 write continues to completion — it holds `_writeLock` and cannot be interrupted mid-operation. The game resumes normally once the lock is released. At ~17 KB synchronous write, player-perceived delay is 2–8 ms on target devices.

- **If a second W-1 fires before the first has completed:** Queue the second write behind the first. The in-memory state accumulates both mutations. When the lock releases, the queued write flushes the latest state incorporating both completions. No write is lost.

- **If `completion_record[]` is present but empty in a valid file:** Normal state for a player who has never completed a level. Do not error. `current_level_id` is the authoritative progress marker.

- **If `schema_version` is entirely absent from the file (not null — absent):** Treat as `schema_version = 0`. Apply all migrations from v0 upward. A file written before the schema system was introduced will not have this key.

- **If a migration write-back fails (the post-migration write-then-swap throws `IOException`):** Do not abort startup. Retain migrated state in memory, set dirty flag `true`. Next W-1 or W-2 will retry the write. Log to analytics with the schema version being migrated from. On next cold start, migration will run again — migrations must therefore be idempotent.

- **If `WriteCompletionAtomic` is called with a `level_id` that already has an entry:** Update `best_stars` only if the new value is higher. Never overwrite `completion_version` (write-once). One entry per `level_id` — no duplicates.

- **If `WriteCompletionAtomic` receives `best_stars = 0`:** No-op. Log a caller bug warning. Zero-star completions are not completions (mirrors LP AC-34).

- **If `WriteCompletionAtomic` is called with `level_id < 1` or `level_id > 9999`:** No-op. Log a caller bug warning. No I/O, no in-memory mutation. Level IDs outside 1–9999 are invalid.

- **If `WriteCompletionAtomic` is called with `new_current_level_id != level_id + 1`:** Log a caller bug warning and proceed with the write — `new_current_level_id` is stored as given. This allows future skip-ahead features. However, implementations that always pass `level_id + 1` are correct for the standard unlock flow; any deviation should be intentional.

- **If `has_completion_record` is called with `level_id = 0`:** Return `false` without querying. Level IDs are 1–9999; 0 is out of range. Do not throw.

- **If `has_completion_record` is called before `SaveSystem.IsReady`:** Follows AC-19 — same rule as all read methods. In debug builds, throws `InvalidOperationException` immediately. In release builds, stalls with a 2-second timeout and returns `null` (`bool?`) if the timeout elapses. A silent `false` default is not acceptable — it would allow callers to treat an unready system as "no records exist," which is functionally indistinguishable from a fresh install.

- **If `coin_balance` would overflow `INT_MAX` after an add:** Clamp to `INT_MAX`. Mark the dirty flag `true` and allow normal write on the next W-1 or W-2 trigger. Prevents sign-flip to a large negative balance.

- **If `coin_balance` is negative in the file on read:** Clamp to 0 at load time. Log the anomaly to analytics. Do not reject the file — level progress is intact.

- **On iOS, if cold start begins immediately after a device reboot before first unlock:** With Unity 6.3's default data protection class (`NSFileProtectionCompleteUntilFirstUserAuthentication`), files are inaccessible only between reboot and first unlock — not on every screen lock. File access will fail with `UnauthorizedAccessException` (not `IOException`, not a parse error). Treat as transient: retry read at 250 ms intervals, timeout at 5 seconds. On timeout, fall back to defaults and emit `first_unlock_read_failure` analytics event. Do not write a default file during the retry window.

- **On Android, if OOM kill occurs mid-write (between `save.tmp` write and `File.Replace`/`File.Move`):** On next cold start, `save.json` is the previous valid state and `save.tmp` is a leftover. Covered by the cold-start `save.tmp` cleanup rule — no additional handling required.

- **On iOS, if `OnApplicationFocus(false)` fires without a subsequent `OnApplicationPause(true)` (brief interruption — incoming call, Siri overlay, Control Center):** W-2 is not triggered. In-memory dirty state remains unwritten during the interruption window. If the device is force-terminated during the interruption (e.g., OOM during a call), the last W-1 write is the recovery point. This is a known best-effort limitation — iOS does not provide a guaranteed write trigger for brief focus-loss interruptions. Consider logging a `focus_without_pause_dirty_loss` analytics event when `OnApplicationFocus(false)` fires while the dirty flag is `true`, to track exposure. No additional write is triggered.

- **If a future schema addition causes `save_file_size_upper_bound` to approach 32 KB:** Re-run the size formula before incrementing `schema_version`. 32 KB is the design ceiling (2× headroom over the 16 KB launch estimate). Any schema change exceeding it requires explicit sign-off.

- **If two systems both read saved values before `IsReady` resolves:** Both must stall (await `IsReady`). The correct integration pattern is a callback or awaitable on `IsReady`, not a poll. In debug builds, reading before `IsReady` throws immediately to surface the integration error. In release builds, the call stalls with a 2-second timeout and returns a nullable result if the timeout elapses. Returning a plausible default without signalling is not acceptable.

## Dependencies

**Systems this GDD depends on (upstream):**

None. Save & Persistence is a Foundation-layer system with no upstream dependencies.

**IFileSystem abstraction (testing requirement):**

All file I/O operations (`FileStream` write, `File.Move`, `File.Delete`, `File.Exists`) must be accessed through an `IFileSystem` interface injected into `SaveSystem` at construction. The production implementation wraps `System.IO`. Test implementations inject faults — `IOException` on write, `IOException` on `File.Move`, disk-full simulation, configurable write delay — enabling AC-04, AC-05, AC-11, and AC-30 to be verified in automated tests without process kill or real disk I/O.

---

**Systems that depend on this GDD (downstream):**

| System | Nature | Hard/Soft | Interface |
|---|---|---|---|
| Level Progression | Bidirectional — reads on cold start, triggers W-1 writes | Hard (cross-session) | `GetCurrentLevelId()`, `GetCompletionRecord(level_id): CompletionRecord?`, `WriteCompletionAtomic(level_id, best_stars, completion_version, new_current_level_id)` |
| Game State Manager | Bidirectional — provides undo_stack snapshot on writes; reads undo_stack on cold start | Hard (cross-session) | `GetUndoStack(): List<UndoMove>` (SaveSystem exposes; GSM calls after IsReady to restore undo queue). GSM provides snapshot to SaveSystem at write time — not a hard compile-time dependency. |
| Coin Economy | Bidirectional — owns `economy.coin_balance` | Hard | CE reads on `OnSaveReady`; writes via `SP.SetCoinBalance(int)`. SP must expose `SetCoinBalance(int)` and `SaveSystem.IsReady: bool`. |
| Skin System (future) | Bidirectional — will define `skins` block structure | Hard (when authored) | `skins` block reserved in schema. Skin System GDD will define keys and read/write interface. |
| Level Select UI (future) | Read-only | Soft | Reads `completion_record[].best_stars` and `current_level_id` via Level Progression interface — not directly from Save & Persistence. |
| Settings UI (future) | Write only (PlayerPrefs) | Soft | Writes `audio.*` and `qts.*` PlayerPrefs keys via direct `PlayerPrefs.Set*()` calls. Save & Persistence owns namespace declaration only. |
| Daily Challenge System (future) | Read-only | Soft | Reads completion state via Level Progression. Does not read Save & Persistence directly. |

**Bidirectional consistency note:** Level Progression GDD already lists Save & Persistence as a hard upstream dependency with the atomic write requirement (EC-16). Coin Economy GDD and Skin System GDD must add themselves to this section's downstream table when authored.

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect if Too High | Effect if Too Low |
|---|---|---|---|---|
| `ios_read_retry_interval_ms` | 250ms | 100–1000ms | Slower response to unlock — player sees a longer load delay after unlocking device | Hammers file I/O; catches should be `UnauthorizedAccessException` specifically to avoid stack-trace overhead on each retry |
| `ios_read_retry_timeout_ms` | 5000ms | 2000–15000ms | Player waits longer before fresh-start fallback on a post-reboot cold launch | Falls back to defaults too quickly — only triggers post-reboot (not every screen lock), so 5s is reasonable |
| `save_file_size_ceiling_bytes` | 32,768 (32 KB) | — (design ceiling, not a runtime constant) | N/A — exceeding this triggers a schema review requirement, not a runtime error | N/A |
| `write_retry_on_pause` | enabled (dirty flag gates the write) | N/A | N/A | If disabled: W-2 never fires; in-session mutations not caught by app-kill between W-1 events |

**Knob interactions:**
- `floor(ios_read_retry_timeout_ms / ios_read_retry_interval_ms)` = max attempts. At defaults: `floor(5000 / 250) = 20`. At safe-range extremes (interval=1000, timeout=2000): `floor(2000 / 1000) = 2` attempts — functional but degraded. The retry loop implementation must yield between attempts (coroutine or `Awaitable.WaitForSecondsAsync`) to avoid blocking the main thread and triggering the iOS watchdog.

**Non-tunable design decisions (conscious locks):**
- `schema_version` starts at 1 and increments by 1 per breaking change — no skipping.
- `save.tmp` is always deleted or promoted — no configurable cleanup delay.
- iCloud/Auto-Backup is **excluded** for all save data (per C.1 backup exclusion decision) — not configurable at runtime (requires app manifest change).

## Visual/Audio Requirements

Not applicable. Save & Persistence is a pure data layer with no visual or audio output.

## UI Requirements

Not applicable. Save & Persistence owns no UI screens. Player-visible messages it triggers ("Could not save progress", "Save data from newer version") are non-blocking system notices — their visual presentation is owned by the UI layer.

## Acceptance Criteria

| ID | Level | Criterion |
|---|---|---|
| AC-01 | BLOCKING | GIVEN any game system other than Save & Persistence, WHEN a codebase search is run for direct file reads targeting `save.json` outside the SaveSystem class, THEN zero results are returned. |
| AC-02 | BLOCKING | GIVEN any PlayerPrefs write in the codebase, WHEN its key and type are inspected, THEN it is a scalar (float, int, or string) in a registered namespace (`audio.*`, `qts.*`, `sp.*`); no key stores a JSON string, array, or compound value. |
| AC-03 | BLOCKING | GIVEN a player completes level N with 2 stars for the first time, WHEN `WriteCompletionAtomic(N, 2, "YYYY.MM", N+1)` executes, THEN `save.json` contains `current_level_id = N+1`, `completion_record[N].best_stars = 2`, and `completion_record[N].completion_version = "YYYY.MM"` — all three written in a single `File.Move` operation. Verified by injecting a fault after `save.tmp` write and confirming that only one `File.Move` occurs per `WriteCompletionAtomic` call. |
| AC-04 | BLOCKING | GIVEN a W-1 write is in progress, WHEN an injected `IFileSystem` fault throws `IOException` on `File.Move` (simulating a crash between write and rename), THEN on next cold start `save.json` contains the previous valid state, `save.tmp` is cleaned up, and `IsReady = true`. Requires `IFileSystem` fault injection seam. |
| AC-05 | BLOCKING | GIVEN W-1 is executing for sequential levels N then N+1, WHEN a second W-1 fires before the first's `File.Move` completes (simulated via `IFileSystem` write delay), THEN both completion records are present in `save.json` after all writes resolve, `current_level_id = N+2`, and the file is valid JSON. Requires `IFileSystem` configurable-delay injection. |
| AC-06 | BLOCKING | GIVEN the dirty flag is `false`, WHEN `OnApplicationPause(true)` fires, THEN no file I/O is performed and `save.json`'s modification timestamp is unchanged. |
| AC-07 | BLOCKING | GIVEN the in-memory state is dirty, WHEN `OnApplicationFocus(false)` is raised, THEN no write is initiated — write is deferred to the next `OnApplicationPause(true)`. |
| AC-08 | BLOCKING | GIVEN the dirty flag is `true`, WHEN `OnApplicationPause(true)` fires, THEN `_writeLock.Wait()` acquires the lock, the write-then-swap completes or throws a caught exception synchronously before the callback returns, and no background I/O continues after `OnApplicationPause` returns. Verified via `IFileSystem` configurable-delay injection confirming synchronous blocking behavior. |
| AC-08b | ADVISORY | GIVEN the dirty flag is `true`, WHEN `OnApplicationPause(true)` fires on a physical iPhone SE 2nd gen (A13, iOS 16 minimum) and Samsung Galaxy A13 (Exynos 850, Android 12), THEN elapsed time from callback entry to return is under 4 seconds. Requires device-lab validation with timing instrumentation; cannot be automated in CI. Evidence file: `production/qa/evidence/ac-08b-device-timing.md`. |
| AC-09 | BLOCKING | GIVEN a W-2 write completes successfully, THEN the dirty flag is `false`. GIVEN a W-2 write throws `IOException`, THEN the dirty flag remains `true` and the next trigger will retry. |
| AC-10 | BLOCKING | GIVEN any system writes a key in a registered PlayerPrefs namespace, WHEN the write executes, THEN `PlayerPrefs.Save()` is called in the same method before returning — no write path omits the flush call. |
| AC-11 | BLOCKING | GIVEN any write path executes (success, failure, or crash recovery), WHEN the session ends or the next cold start completes, THEN no `save.tmp` file exists at `Application.persistentDataPath`. |
| AC-12 | BLOCKING | GIVEN the device has insufficient free storage to write `save.tmp`, WHEN W-1 or W-2 fires, THEN the `IOException` is caught, a non-blocking message is shown ("Could not save progress — please free up storage space"), the error is logged to analytics, and the game remains playable. |
| AC-13 | BLOCKING | GIVEN `save.json` exists with `schema_version = 1` and valid JSON, WHEN cold start read executes, THEN all in-memory fields match file values, `IsReady = true`, and no migration runs. |
| AC-14 | BLOCKING | GIVEN `save.json` exists with `schema_version = 0` (or absent key treated as v0), WHEN cold start read executes, THEN migration runs to `MAX_KNOWN_VERSION`, the migrated state is written back via write-then-swap, and the resulting file contains `schema_version = 1`. |
| AC-15 | BLOCKING | GIVEN no `save.json` exists, WHEN cold start read executes, THEN `current_level_id = 1`, `completion_record[] = []`, `coin_balance = 0`, `IsReady = true`, and no file is written until the first W-1 or W-2 fires. |
| AC-16 | BLOCKING | GIVEN `save.json` fails JSON parsing, WHEN cold start read executes, THEN the error is logged (file size, first 256 bytes, exception), `save.tmp` is tried if present, the recovered or default state is written via write-then-swap, no error UI is shown, and `IsReady = true`. |
| AC-17 | BLOCKING | GIVEN `save.json` contains `schema_version = 99` (greater than `MAX_KNOWN_VERSION`), WHEN cold start read executes with `sp.downgrade_notice_shown = 0`, THEN the file is not overwritten, the system starts from defaults, an analytics warning is emitted, the notice "This device has a save file from a newer version of the game. Update the app to recover your progress." is shown exactly once, `sp.downgrade_notice_shown` is set to 1 with `PlayerPrefs.Save()`, and `IsReady = true`. GIVEN a second cold start with the same rejected file, WHEN cold start executes, THEN the notice is NOT shown again. |
| AC-18 | BLOCKING | GIVEN `save.json` is valid JSON with no `schema_version` key, WHEN cold start read executes, THEN it is treated as `schema_version = 0` and all migrations from v0 upward run — identical to R-2 for a file that explicitly declares v0. |
| AC-19 | BLOCKING | GIVEN `SaveSystem.IsReady` is `false`, WHEN any external system calls a read method (`GetCurrentLevelId()`, `GetCompletionRecord()`, etc.) in a DEBUG build, THEN `InvalidOperationException` is thrown immediately. GIVEN a RELEASE build, WHEN the same read is made before `IsReady`, THEN the call stalls (awaits `IsReady`) with a 2-second timeout; if the timeout elapses the method returns a nullable/Optional result that the caller must handle. A plain `0` or `false` default must not be returned silently. |
| AC-20 | BLOCKING | GIVEN `WriteCompletionAtomic` is called with `best_stars = 0`, WHEN the call executes, THEN no I/O is performed, no in-memory mutation occurs, a caller bug warning is logged, and `save.json` and the dirty flag are unchanged. |
| AC-21 | BLOCKING | GIVEN `completion_record[N].best_stars = 3` exists, WHEN `WriteCompletionAtomic(N, 2, ...)` is called, THEN `best_stars` remains 3 in memory and after the write — the lower value is not written. |
| AC-22 | BLOCKING | GIVEN `completion_record[N].completion_version = "2026.04"` exists, WHEN `WriteCompletionAtomic(N, 3, "2026.09", ...)` is called, THEN `completion_version` in `save.json` remains `"2026.04"` AND the in-memory `completion_record[N].completion_version` also remains `"2026.04"` — the new value is discarded at both layers. |
| AC-23 | BLOCKING | GIVEN `completion_record[]` already has one entry for `level_id = 5`, WHEN `WriteCompletionAtomic(5, 3, ...)` is called, THEN `completion_record[]` still contains exactly one entry for `level_id = 5` after the write. |
| AC-24 | BLOCKING | GIVEN `completion_record[]` has entries for levels 1 and 2 only, WHEN `has_completion_record(3)` is called, THEN the result is `false`, `best_stars(3)` returns 0, and no entry is created as a side effect. |
| AC-25 | BLOCKING | GIVEN any save state with `IsReady = true`, WHEN `has_completion_record(0)` is called, THEN the result is `false` and no exception is raised. |
| AC-26 | ADVISORY | GIVEN a test save file with `schema_version = 1`, 200 completion records with max-length field values (`level_id`=9999, `best_stars`=3, `completion_version`="9999.12"), and a 20-entry `undo_stack` with two-digit indices (`{"f":99,"t":99}`), WHEN serialized to unminified UTF-8 JSON using the production serializer and measured, THEN the file size is at most 21,780 bytes; if exceeded, update `bytes_per_record` in Formula 2 and recalculate the content ceiling trigger and all affected AC thresholds. |
| AC-27 | BLOCKING | GIVEN `save.json` contains `"coin_balance": -50`, WHEN cold start read executes, THEN in-memory `coin_balance` is clamped to 0, the anomaly is logged to analytics, and all other fields load normally with `IsReady = true`. |
| AC-28 | ADVISORY | GIVEN an iOS device cold-started immediately after a reboot (before first unlock), WHEN cold start read encounters `UnauthorizedAccessException` (not a parse error), THEN the system polls at 250ms intervals; if the file becomes accessible within 5 seconds it loads with all fields matching the file (same as AC-13 success state); if the timeout elapses it falls back to defaults (same as AC-15 state), emits `first_unlock_read_failure` to analytics, and writes no file during the retry window. Verification requires a physical iOS device in post-reboot state. |
| AC-29 | BLOCKING | GIVEN cold start performs a migration and the post-migration write-then-swap throws `IOException`, WHEN the exception is caught, THEN the migrated in-memory state is retained (not reverted), the dirty flag is `true`, the failure is logged with the migrated-from schema version, `IsReady = true`, and the migration re-runs on the next cold start. |
| AC-30 | BLOCKING | GIVEN W-1 and W-2 both fire in the same frame, WHEN both triggers execute (tested via `IFileSystem` write delay to enforce overlap), THEN only one `File.Move` runs at a time — `_writeLock` prevents concurrent rename operations. After both writes complete, `save.json` reflects the in-memory state at the time the second write began (W-2 serializes after W-1 releases the lock) and is valid parseable JSON. Verification requires `IFileSystem` configurable-delay injection. |
| AC-31 | BLOCKING | GIVEN a new schema version is introduced (`MAX_KNOWN_VERSION` incremented), WHEN the constant in the implementation is inspected via codebase grep, THEN it matches the value declared in this GDD's Formulas section — a mismatch causes Case R-5 to incorrectly accept or reject save files, causing silent data corruption. Automate as a CI lint check or unit assertion. |

| AC-32 | BLOCKING | GIVEN a valid `save.json` with `schema_version = 1` and `completion_record[] = []` (empty array), WHEN cold start read executes, THEN `current_level_id` loads correctly, `completion_record[]` is an empty array (not an error), `IsReady = true`, and no migration runs. |
| AC-33 | BLOCKING | GIVEN `save.json` is valid and `save.tmp` exists at cold start, WHEN cold start read executes (no corruption), THEN `save.tmp` is deleted silently before `IsReady = true` is set, and `save.json` is loaded normally. |
| AC-34 | BLOCKING | GIVEN a migration has been applied (in-memory state is migrated, dirty flag `true`), WHEN the same migration runs again on the next cold start (because write-back failed), THEN the resulting in-memory state is identical to the first migration pass — idempotency is verified by comparing field-for-field output. |
| AC-35 | BLOCKING | GIVEN `coin_balance` in memory is `INT_MAX - 1`, WHEN the coin-add mutation method is called with a value that would set `coin_balance` above `INT_MAX`, THEN `coin_balance` is clamped to `INT_MAX` at the mutation site (not at write time — SaveSystem write paths do not add coins), the dirty flag is set `true`, and the clamped value is written to `save.json` on the next W-1 or W-2 trigger. |
| AC-36 | BLOCKING | GIVEN `WriteCompletionAtomic` is called with `level_id = 0` or `level_id = 10000`, WHEN the call executes, THEN no I/O is performed, no in-memory mutation occurs, a caller bug warning is logged, and `save.json` and the dirty flag are unchanged. |
| AC-37 | BLOCKING | GIVEN a W-1 `File.Replace` or `File.Move` call throws `IOException` (simulated via `IFileSystem` injection), WHEN the exception is caught, THEN `save.json` is intact (not overwritten), `save.tmp` is deleted, the error is logged to analytics, and the dirty flag remains `true` for retry on next trigger. |
| AC-38 | ADVISORY | GIVEN `save.json` does not exist, WHEN `NSURLIsExcludedFromBackupKey` is queried on the path after first file creation, THEN the attribute is `true`. Requires physical iOS device; cannot execute on Android or CI without iOS entitlements. |
| AC-39 | BLOCKING | GIVEN a player completes level N and the undo stack has 5 moves recorded (snapshot captured on main thread before BackgroundThreadAsync), WHEN W-1 executes, THEN `save.json` contains `level_progress.undo_stack` with exactly those 5 entries in the correct order. The cross-system round-trip (cold start → GSM restores 5 undos from `GetUndoStack()`) is an integration test owned by the GSM GDD — it is not independently testable from Save & Persistence in isolation. |
| AC-40 | BLOCKING | GIVEN a CI test fixture with `schema_version = 1`, all 300 completion records (max level count before 32 KB ceiling, same max-length field values as AC-26 fixture), and a 20-entry `undo_stack`, WHEN serialized to unminified UTF-8 JSON using the production serializer and measured, THEN the file size is at most 32,500 bytes (Formula 2 predicts 32,280 at 300 levels; 220-byte buffer for serializer whitespace variance). If exceeded, update `bytes_per_record` in Formula 2 and recalculate the content-update ceiling trigger (currently 300 levels). |

| AC-41 | BLOCKING | GIVEN `level_progress.undo_stack[]` has exactly 20 entries, WHEN a 21st move is committed (mutation via the undo-stack update method), THEN `undo_stack[]` still contains exactly 20 entries, the oldest entry (index 0) has been discarded, and the 21st move is the new last entry. An off-by-one error or wrong-end discard is a defect. |
| AC-42 | BLOCKING | GIVEN W-2 is blocked in `_writeLock.Wait(destroyCancellationToken)` (simulated via `IFileSystem` delay holding the lock), WHEN `destroyCancellationToken` is cancelled (MonoBehaviour destroyed), THEN `OperationCanceledException` is caught silently, the dirty flag remains `true`, no write occurs, no exception propagates outside `OnApplicationPause`, and `save.json` is unchanged. |
| AC-43 | BLOCKING | GIVEN a W-1 write is executing and `_writeLock` was previously released by W-2 (running on the main thread), WHEN `_writeLock.WaitAsync` completes and the second `Awaitable.BackgroundThreadAsync()` is called, THEN all subsequent file I/O in the locked section (step 5) executes off the main thread — verified by asserting `Thread.IsBackground == true` and the current thread is not the Unity main thread at the point `FileStream` is constructed. Requires `IFileSystem` delay injection to create a controlled W-2-then-W-1 ordering. |
| AC-44 | BLOCKING | GIVEN `save.json` fails JSON parsing AND `save.tmp` also exists but fails JSON parsing (or is absent), WHEN cold start read executes, THEN the system falls back to all defaults (`current_level_id = 1`, `completion_record[] = []`, `coin_balance = 0`), both parse errors are logged to analytics, the default state is written to `save.json` via write-then-swap, no error UI is shown, and `IsReady = true`. |
| AC-45 | ADVISORY | GIVEN `schema_version = 1` in a valid save file AND `MAX_KNOWN_VERSION = 2` BUT the `migrate_v1_to_v2` function does not exist in the codebase, WHEN cold start read executes, THEN the system treats the migration gap as Case R-5 (missing handler = unknown future schema): defaults are loaded, an analytics warning is emitted, the file is not overwritten, and `IsReady = true`. |
| AC-46 | ADVISORY | GIVEN a W-1 or W-2 write is attempted and `File.Replace` or `File.Move` throws `UnauthorizedAccessException` (not `IOException` — e.g., mid-session file permission change on Android external storage), WHEN the exception is caught, THEN the error is logged to analytics with the exception type, `save.tmp` is deleted, the dirty flag remains `true`, and the game remains playable. Catch block must catch `UnauthorizedAccessException` explicitly — it is not a subclass of `IOException`. |

**Summary: 41 BLOCKING / 6 ADVISORY**

## Open Questions

**OQ-01 — RESOLVED: Write-lock implementation pattern**
Resolved in design-review. Primitive: `SemaphoreSlim(1, 1)` with `destroyCancellationToken`. W-2 is synchronous. W-1 uses `Awaitable.BackgroundThreadAsync()`. Queue depth 1 with coalescing. See C.1 Concurrency Model.

**OQ-02 — Analytics event contract**
Four events are implied by this GDD: `migration_write_failure(schema_version_from)`, `first_unlock_read_failure`, `coin_balance_clamped(value)`, `file_corruption_detected(size, first_256_bytes)`. The Analytics GDD (system 22) has not been authored. Resolve when Analytics GDD is authored. *Priority: before Beta.*

**OQ-03 — RESOLVED: Coin Economy ownership transfer complete**
Coin Economy GDD authored. `economy.coin_balance` ownership transferred from Level Progression to Coin Economy per CE-01. C.2 schema table and Interactions table updated. SP now exposes `SetCoinBalance(int)` (replacing the provisional `AddCoins` method). Cross-GDD SP-01 satisfied.

**OQ-04 — Android Auto-Backup rule file** *(elevated to required)*
The Android manifest must exclude both `save.json` and `save.tmp` via `<cloud-backup-rules>` XML. The backup policy decision (exclude entirely) was locked in design-review. Assign rule file authoring to Unity specialist before Beta build. *Priority: before Beta build.*

**OQ-05 — `IsReady` integration pattern** *(partially resolved)*
Debug throws `InvalidOperationException`; release stalls with 2-second timeout + nullable return (locked in design-review, see C.5 and AC-19). Remaining ADR question: expose `IsReady` as a `UniTask`, a Unity `Awaitable`, or a C# `Task`? Resolve before implementation sprint. *Priority: before implementation.*
