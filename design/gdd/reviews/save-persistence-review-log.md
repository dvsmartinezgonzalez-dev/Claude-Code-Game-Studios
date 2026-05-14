# Review Log: Save & Persistence GDD

---

## Review — 2026-04-24 — Verdict: APPROVED

Scope signal: XL
Specialists: lean mode — single-session analysis
Blocking items: 0 | Recommended: 3
Prior verdict resolved: Yes — all 8 third-pass blockers confirmed resolved.

Summary: Fourth pass. No new blocking issues found. Three recommended revisions noted: (1) iOS post-reboot retry path creates an ambiguity between the synchronous-Awake constraint and the yield-between-retries requirement in Tuning Knobs — the intended resolution (IsReady=false + polling fallback during retry window) should be stated explicitly in the iOS edge case; (2) AC-43's `Thread.IsBackground` assertion is an imprecise proxy for "not the Unity main thread" — should use `ManagedThreadId` comparison instead; (3) no AC covers the `WriteCompletionAtomic` skip-ahead warning log. GDD is approved for implementation; OQ-05 (IsReady awaitable type) must be resolved before the implementation sprint begins.

---

## Review — 2026-04-23 — Verdict: NEEDS REVISION (third pass) → Revised (pending re-review)

Scope signal: XL
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director (synthesis)
Blocking items: 8 | Recommended: 10
Prior verdict resolved: Yes — all 19 second-pass blockers confirmed resolved.

Summary: Third pass surfaced two hard implementation bugs (`File.Move` with overwrite parameter not available in .NET Standard 2.1/Unity 6; `async void Awake()` ambiguity breaking IsReady-before-Start guarantee for migration write-back), two AC gate-level errors (AC-08 cannot be automated in CI as BLOCKING; AC-31 must be BLOCKING not ADVISORY), three missing BLOCKING ACs (W-2 `destroyCancellationToken` cancellation, thread re-assertion verification, dual-corruption fallback), and a Formula 2 per-record byte underestimate (80 bytes → 105 bytes), shifting the content ceiling trigger from 375 to 300 levels. All 8 blockers addressed in third revision: `File.Replace` substituted for `File.Move` overload; migration write-back explicitly specced as synchronous in `Awake()`; AC-08 split into automated BLOCKING + ADVISORY device sign-off; AC-31 promoted to BLOCKING; AC-42/43/44 added for missing coverage; Formula 2 recalculated with corrected byte estimate; content ceiling and AC-26/AC-40 thresholds updated.

---

## Review — 2026-04-23 — Verdict: MAJOR REVISION NEEDED (second pass) → Revised (pending re-review)

Scope signal: XL
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, creative-director (synthesis)
Blocking items: 19 | Recommended: 8
Prior verdict resolved: Partially — first-round mechanical blockers closed; revision introduced new concurrency defects

Summary: The first revision correctly closed the mechanical blockers from round one (backup mechanism, undo_stack schema addition, SemaphoreSlim concurrency primitive, migration termination, IFileSystem abstraction) but introduced three new concurrency correctness bugs — thread-context escape after WaitAsync (file I/O could run on main thread under contention), W-1/W-2 deadlock risk if W-1 accesses any main-thread-only Unity API inside the locked section, and an unsynchronized background read of GSM-owned undo_stack causing data races. The undo_stack addition also cascaded incompletely across the document (AC-26 threshold stale, W-1 write contract omitting undo_stack, missing GSM Interactions row, v0 migration unspecified, missing max-depth AC). Additionally, has_completion_record vs AC-19 contradicted each other, GetCompletionRecord had a nullable type mismatch, "cold start" was never mapped to a Unity lifecycle event, the Player Fantasy didn't acknowledge the backup-exclusion tradeoff against "Respect the Session," and Tuning Knobs still said backup was enabled. All 19 blocking items addressed in second revision: concurrency model rewritten with 6-step W-1 sequence (snapshot → BackgroundThreadAsync → WaitAsync → re-assert background → I/O only with System.IO → try/finally release), W-2 cancellation defined, undo_stack consistency swept across all eight document locations, 6 ACs corrected (AC-08 device named, AC-26/35/38/39/40 fixed, AC-41 added), Player Fantasy reconciliation paragraph added, Unity lifecycle anchored to Awake() on DontDestroyOnLoad singleton.

---

## Review — 2026-04-22 — Verdict: MAJOR REVISION NEEDED → Revised (pending re-review)

Scope signal: XL
Specialists: game-designer, systems-designer, qa-lead, unity-specialist, performance-analyst, creative-director (synthesis)
Blocking items: 7 themes (17 sub-items) | Recommended: 8
Prior verdict resolved: No — first review

Summary: The GDD had the right structural skeleton (8/8 sections) but failed on five critical axes: (1) the concurrency model was entirely unspecified — `async void OnApplicationPause` would silently abandon iOS writes; (2) `File.Move` atomicity claims were factually incorrect for Android 11+ FUSE and APFS power-loss scenarios; (3) the 32 KB ceiling silently breaks at 375 levels with no content-milestone gate; (4) migration chain lacked a v0 schema definition and had a loop-termination defect (`==` instead of `>=`); (5) the `IsReady` defensive return (silent `0`) contradicted the "stall" edge-case rule and enabled silent level-0 load attempts. Two design decisions were made during revision: backup excluded entirely (`NSURLIsExcludedFromBackupKey`), and undo history persisted (last 20 moves, ~600 bytes). Post-revision: OQ-01 closed, 9 ACs added (AC-32–AC-40), 8 ACs revised, `IFileSystem` abstraction required, v0 schema documented, concurrency model mandated in C.1. Status set to In Review pending re-review in a clean session.
