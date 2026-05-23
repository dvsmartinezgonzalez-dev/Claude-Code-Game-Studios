// REQUIRED: Register SaveSystem in Project Settings > Script Execution Order at -90
// Without this, boot ordering is non-deterministic. See ADR-0001 SEO table.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace BoltSort.SaveSystem
{
    /// <summary>
    /// Foundation singleton at SEO −90. Reads save.json synchronously in <c>Awake()</c>.
    /// <c>IsReady = true</c> and <c>OnSaveReady?.Invoke()</c> fire before any lower-SEO
    /// system's <c>Start()</c> runs.
    /// </summary>
    /// <remarks>
    /// ADR-0001: Singleton Architecture — DDOL pattern; subscribe-then-check mandatory.
    /// ADR-0003: Save System Design — cold-start cases R-1/R-3/R-5; write contract W-1/W-2.
    ///
    /// Story 001: cold-start read (R-1, R-3, R-5), IsReady contract, IFileSystem seam.
    /// Story 002: WriteCompletionAtomic full implementation, PushUndoMove, GetUndoStack.
    /// Story 003: OnApplicationPause W-2 write path.
    /// Story 004: iOS UnauthorizedAccessException retry loop; R-4 corruption recovery.
    /// Story 005: R-2 migration dispatch.
    /// Story 006: SetCoinBalance PlayerPrefs.Save() call.
    ///
    /// FORBIDDEN: async void Awake() — Unity does not await Awake(); Start() on other
    /// MonoBehaviours fires before IsReady = true, breaking initialization contract (ADR-0003).
    /// </remarks>
    [DefaultExecutionOrder(-90)]  // fallback if Project Settings SEO registration is lost; see ADR-0001
    public class SaveSystem : MonoBehaviour, ISaveSystem
    {
        // ── Schema Version ────────────────────────────────────────────────────────

        /// <summary>
        /// Maximum schema version this build understands. Files with schema_version &gt; this
        /// trigger Case R-5 (downgrade): defaults loaded, file not overwritten, notice shown.
        /// </summary>
        public const int MaxKnownVersion = 1;

        // ── Singleton ─────────────────────────────────────────────────────────────

        /// <summary>Singleton instance. Set in <c>Awake</c>; registered at SEO −90.</summary>
        public static SaveSystem Instance { get; private set; }

        // ── ISaveSystem ───────────────────────────────────────────────────────────

        /// <summary>
        /// True after cold-start read completes. Set synchronously before any Start() runs.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Fires at the end of Awake() after IsReady is set.
        /// Consumers must use subscribe-then-check (ADR-0001).
        /// </summary>
        public event Action OnSaveReady;

        // ── Private State ─────────────────────────────────────────────────────────

        private IFileSystem _fileSystem = new ProductionFileSystem();
        private SaveData _saveData;
        private volatile bool _isDirty;

        // W-1 concurrency primitives (Story 002).
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        // Deferred error message: set on background thread, logged on main thread.
        private volatile string _lastWriteError;

        // Cached paths — Application.persistentDataPath must only be accessed on main thread.
        // Cache in Awake() and never re-read on background threads (ADR-0003).
        private string _savePath;
        private string _tmpPath;

        // R-5 downgrade notice flag key (PlayerPrefs).
        private const string DowngradeNoticeShownKey = "sp.downgrade_notice_shown";

        // ── iOS Retry Seam ────────────────────────────────────────────────────────

        // Retry constants — extracted so tests can override via internal seam fields
        // without needing reflection on the live constants (AC-28 retry interval/timeout).
        // Default values match the spec: 250ms interval, 5000ms timeout (Story 004).
        internal int RetryIntervalMs = 250;
        internal int RetryTimeoutMs  = 5000;

        // Analytics sink seam — tests verify emission without calling a real analytics SDK.
        // Production: replace null check with actual analytics call (e.g. FirebaseAnalytics.LogEvent).
        internal Action<string> EmitAnalyticsEvent;

        /// <summary>
        /// True after <c>first_unlock_read_failure</c> analytics event was emitted.
        /// Readable by tests via internal access to verify AC-28 timeout fallback.
        /// </summary>
        internal bool FirstUnlockReadFailureEmitted { get; private set; }

        /// <summary>
        /// Exposes <c>_isDirty</c> for test assertions (e.g. AC-29 write-back failure).
        /// Not intended for production callers.
        /// </summary>
        internal bool IsDirty => _isDirty;

        // ── Test Injection Seam ───────────────────────────────────────────────────

        // Static override: tests set this before AddComponent<SaveSystem>() so the
        // value is available when Awake() fires. Cleared immediately after use so it
        // does not leak across tests.
        private static IFileSystem s_testFileSystemOverride;

        /// <summary>
        /// Sets a test-only IFileSystem override. Call BEFORE <c>AddComponent&lt;SaveSystem&gt;()</c>
        /// because Unity fires Awake() synchronously inside AddComponent.
        /// The override is consumed and cleared by the first SaveSystem Awake() that runs.
        /// Call from [TearDown] AND [OneTimeTearDown] to guard against setup failures.
        /// </summary>
        internal static void SetFileSystemForTesting(IFileSystem fs)
        {
            s_testFileSystemOverride = fs;
        }

        /// <summary>
        /// Clears the static Instance reference and the test override.
        /// Call from [TearDown] AND [OneTimeTearDown] to ensure isolation between tests.
        /// </summary>
        internal static void ClearInstanceForTesting()
        {
            if (Instance != null)
                Instance.FirstUnlockReadFailureEmitted = false;
            Instance = null;
            s_testFileSystemOverride = null;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            // 1. Singleton guard — MUST be the absolute first statement (ADR-0001, ADR-0003).
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 2. Apply test injection seam — consume and clear so it does not leak across tests.
            if (s_testFileSystemOverride != null)
            {
                _fileSystem = s_testFileSystemOverride;
                s_testFileSystemOverride = null;
            }

            // 3. Pre-cache persistent path — Application.persistentDataPath is main-thread-only.
            //    Never access this property from a background thread (ADR-0003).
            _savePath = Application.persistentDataPath + "/save.json";
            _tmpPath  = Application.persistentDataPath + "/save.tmp";

            // 4. Cold-start read (synchronous — async void Awake() is forbidden).
            PerformColdStartRead();

            // 5. Signal ready — fires before any lower-SEO system's Start() runs (ADR-0001).
            IsReady = true;
            OnSaveReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Cold-Start Read ───────────────────────────────────────────────────────

        /// <summary>
        /// Synchronous cold-start file read. Dispatches to R-1/R-2/R-3/R-4/R-5 based on
        /// file existence, iOS retry result, parse success, and schema_version.
        /// </summary>
        /// <remarks>
        /// iOS retry (AC-28, Story 004): <c>UnauthorizedAccessException</c> triggers a
        /// background-thread retry loop (250ms × 20 attempts = 5s). Thread is joined before
        /// <c>IsReady = true</c>. <c>Thread.Sleep</c> on the background thread is intentional
        /// and safe — it MUST NOT occur on the main thread (ADR-0003).
        ///
        /// R-2 migration (Story 005): rawJson is captured on the background thread; parsing and
        /// migration run on the main thread after the thread join so that JsonUtility and Debug
        /// calls are safely on the main thread.
        /// </remarks>
        private void PerformColdStartRead()
        {
            bool tmpExists = _fileSystem.FileExists(_tmpPath);

            // --- iOS retry: run ReadWithIosRetry on a background thread, join before IsReady.
            // The retry method contains Thread.Sleep — it must NOT run on the main thread.
            // rawJson == null means: file missing (R-3), iOS timeout (AC-28 fail), or IOException (R-4).
            string rawJson = null;
            var retryThread = new Thread(() => { rawJson = ReadWithIosRetry(_savePath); })
            {
                IsBackground = true,
                Name = "SaveSystem_ColdStartRead",
            };
            retryThread.Start();
            retryThread.Join();   // blocks main thread until read+retry finishes (ADR-0003 thread-join)

            // rawJson == null → missing file, iOS timeout, or IOException.
            if (rawJson == null)
            {
                // R-4: attempt save.tmp recovery before falling back to defaults (AC-16, AC-44).
                if (tmpExists)
                {
                    SaveData recovered = AttemptTmpRecovery(_tmpPath);
                    if (recovered != null)
                    {
                        // Use recovered state; write back to save.json synchronously (AC-16).
                        ApplySubObjectDefaults(recovered);
                        _saveData = recovered;
                        WriteSaveJsonSync(_saveData);
                        // Do NOT delete tmp here — WriteSaveJsonSync will Replace(tmp → save.json).
                        return;
                    }
                    // AC-44: both files corrupt — log already done inside AttemptTmpRecovery.
                }

                _saveData = CreateDefaults();
                SafeDeleteTmp(tmpExists);
                return;
            }

            // Parse on main thread (JsonUtility is main-thread safe; Debug.LogWarning requires it).
            SaveData parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(rawJson);
                if (parsed == null)
                {
                    Debug.LogWarning("[SaveSystem] JsonUtility.FromJson returned null — R-4 corruption path.");
                    EmitAnalyticsEvent?.Invoke("cold_start_parse_failure");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] JSON parse failed — R-4. {ex.Message}");
                EmitAnalyticsEvent?.Invoke("cold_start_parse_failure");
            }

            if (parsed == null)
            {
                if (tmpExists)
                {
                    SaveData recovered = AttemptTmpRecovery(_tmpPath);
                    if (recovered != null)
                    {
                        ApplySubObjectDefaults(recovered);
                        _saveData = recovered;
                        WriteSaveJsonSync(_saveData);
                        return;
                    }
                }
                _saveData = CreateDefaults();
                SafeDeleteTmp(tmpExists);
                return;
            }

            if (parsed.schema_version > MaxKnownVersion)
            {
                HandleDowngrade(parsed.schema_version);
                _saveData = CreateDefaults();
                SafeDeleteTmp(tmpExists);
                return;
            }

            // R-2: schema version is older — run migration chain.
            if (parsed.schema_version < MaxKnownVersion)
            {
                parsed = RunMigrations(rawJson, parsed.schema_version);
            }

            ApplySubObjectDefaults(parsed);
            _saveData = parsed;

            if (_saveData.economy.coin_balance < 0)
            {
                Debug.LogWarning("[SaveSystem] Negative coin_balance clamped to 0 (AC-27).");
                _saveData.economy.coin_balance = 0;
            }

            SafeDeleteTmp(tmpExists);
        }

        /// <summary>
        /// Reads <paramref name="savePath"/> with iOS <c>UnauthorizedAccessException</c> retry
        /// (AC-28). Runs on a background thread — <c>Thread.Sleep</c> is intentional here.
        /// Returns the raw JSON string on success, or <c>null</c> on:
        ///   - file not found (R-3)
        ///   - iOS timeout after <see cref="RetryTimeoutMs"/> (AC-28 fail path)
        ///   - <see cref="IOException"/> (R-4 corruption)
        /// No Unity API calls — only <c>System.IO</c> via <c>_fileSystem</c> and <c>Debug</c>.
        /// Parsing is intentionally deferred to the main thread (caller) so that JsonUtility
        /// and Debug run on the correct thread.
        /// </summary>
        private string ReadWithIosRetry(string savePath)
        {
            int elapsed = 0;

            while (elapsed < RetryTimeoutMs)
            {
                try
                {
                    return _fileSystem.ReadAllText(savePath);
                }
                catch (FileNotFoundException)
                {
                    return null;  // R-3: fresh install — no retry, fall through to defaults
                }
                catch (UnauthorizedAccessException)
                {
                    // iOS file protection before first unlock — retry (ADR-0003).
                    Thread.Sleep(RetryIntervalMs);   // safe: background thread only (ADR-0003)
                    elapsed += RetryIntervalMs;
                    continue;
                }
                catch (IOException ex)
                {
                    // Sibling of UnauthorizedAccessException — NOT the iOS retry case (ADR-0003).
                    Debug.LogWarning($"[SaveSystem] IOException on cold-start read — R-4 recovery. {ex.Message}");
                    EmitAnalyticsEvent?.Invoke("cold_start_io_error");
                    return null;
                }
            }

            // iOS timeout elapsed — fall back to defaults, no file written during window (AC-28).
            Debug.LogWarning($"[SaveSystem] iOS retry timeout ({RetryTimeoutMs}ms) — loading defaults. (AC-28)");
            EmitAnalyticsEvent?.Invoke("first_unlock_read_failure");
            FirstUnlockReadFailureEmitted = true;
            return null;
        }

        // ── R-2 Migration ─────────────────────────────────────────────────────────

        /// <summary>
        /// Applies sequential schema migrators to bring <paramref name="rawJson"/> up to
        /// <see cref="MaxKnownVersion"/>. Called from <c>PerformColdStartRead</c> when
        /// <c>schema_version &lt; MaxKnownVersion</c> (Case R-2).
        ///
        /// Write-back is attempted synchronously via <see cref="WriteSaveJsonSyncCore"/>.
        /// On <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>,
        /// <c>_isDirty</c> is set to <c>true</c> so W-1/W-2 retries on next opportunity.
        /// In-memory migrated state is always retained regardless of write-back outcome (AC-29).
        /// </summary>
        /// <param name="rawJson">Original raw JSON string from disk (pre-migration).</param>
        /// <param name="fromVersion">Schema version read from the file (the starting version).</param>
        /// <returns>The fully migrated <see cref="SaveData"/> (always non-null).</returns>
        private SaveData RunMigrations(string rawJson, int fromVersion)
        {
            SaveData data;

            // v0 → v1: deserialize via the legacy flat schema class, then restructure.
            if (fromVersion < 1)
            {
                data = MigrateV0ToV1(rawJson);
            }
            else
            {
                // Already at a known intermediate version — parse as current schema.
                data = JsonUtility.FromJson<SaveData>(rawJson) ?? CreateDefaults();
            }

            // Future: if (data.schema_version < 2) { data = MigrateV1ToV2(data); }

            // Write-back synchronously (no await — inside Awake).
            // On failure, retain in-memory state and mark dirty for next W-1/W-2 (AC-29).
            try
            {
                WriteSaveJsonSyncCore(data);
                _isDirty = false;
            }
            catch (IOException ex)
            {
                _isDirty = true;
                Debug.LogWarning($"[SaveSystem] R-2 write-back failed (from v{fromVersion}): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                _isDirty = true;
                Debug.LogWarning($"[SaveSystem] R-2 write-back failed (from v{fromVersion}): {ex.Message}");
            }

            return data;
        }

        /// <summary>
        /// Migrates a v0 flat-schema save to the v1 nested schema.
        ///
        /// v0 layout: <c>{ current_level_id, completion_record, coin_balance }</c> (flat root).
        /// v1 layout: <c>{ level_progress: { current_level_id, completion_record, undo_stack }, economy: { coin_balance } }</c>.
        ///
        /// Rules enforced:
        /// <list type="bullet">
        ///   <item><c>completion_version</c> on existing records is NEVER synthesised (write-once, AC-22).</item>
        ///   <item><c>undo_stack</c> is initialised to an empty list (v0 has no undo history).</item>
        ///   <item>Idempotent: running twice on v1 data returns identical output (AC-34).</item>
        /// </list>
        ///
        /// Internal visibility so unit tests can call it directly without going through Awake (AC-34).
        /// </summary>
        /// <param name="rawJson">Raw JSON string to migrate. Parsed via <see cref="SaveDataLegacyV0"/>.</param>
        /// <returns>A fully populated v1 <see cref="SaveData"/>.</returns>
        internal SaveData MigrateV0ToV1(string rawJson)
        {
            // Deserialize using the v0 flat schema to get field values faithfully.
            // If the JSON is already v1 (idempotency case), the v0 class will still parse safely:
            // absent flat fields (current_level_id, coin_balance) get their defaults (1, 0).
            SaveDataLegacyV0 v0 = JsonUtility.FromJson<SaveDataLegacyV0>(rawJson)
                                   ?? new SaveDataLegacyV0();

            // Idempotency: if the raw JSON already has schema_version = 1 and nested structure,
            // parse it as v1 directly to preserve any fields the v0 struct cannot represent.
            if (v0.schema_version >= 1)
            {
                SaveData existing = JsonUtility.FromJson<SaveData>(rawJson);
                if (existing != null)
                    return existing;
            }

            // Build v1 nested structure from v0 flat fields.
            var migrated = new SaveData
            {
                schema_version = 1,
                level_progress = new LevelProgress
                {
                    current_level_id  = v0.current_level_id,
                    undo_stack        = new List<UndoMove>(),   // v0 had no undo history
                    completion_record = new List<CompletionRecord>(),
                },
                economy = new Economy
                {
                    coin_balance = v0.coin_balance,
                },
                skins = new Skins(),
            };

            // Copy completion records — NEVER synthesise completion_version (write-once, AC-22).
            if (v0.completion_record != null)
            {
                foreach (var rec in v0.completion_record)
                {
                    migrated.level_progress.completion_record.Add(new CompletionRecord
                    {
                        level_id           = rec.level_id,
                        best_stars         = rec.best_stars,
                        // completion_version: preserve existing value only — never synthesise.
                        // JsonUtility serializes absent/null strings as ""; treat "" as absent.
                        completion_version = string.IsNullOrEmpty(rec.completion_version)
                                             ? ""
                                             : rec.completion_version,
                    });
                }
            }

            return migrated;
        }

        /// <summary>
        /// Attempts to recover save state from <paramref name="tmpPath"/> when <c>save.json</c>
        /// is absent or corrupt (AC-16). Returns parsed <see cref="SaveData"/> or <c>null</c>
        /// if <c>save.tmp</c> is also absent or corrupt (AC-44).
        /// Called on the main thread during cold-start (after background retry joins).
        /// Uses <c>JsonUtility</c> and <c>Debug</c> — main-thread Unity APIs are safe here.
        /// </summary>
        private SaveData AttemptTmpRecovery(string tmpPath)
        {
            string json = null;
            try
            {
                json = _fileSystem.ReadAllText(tmpPath);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[SaveSystem] R-4: save.tmp read failed (IO) — {ex.Message} (AC-44).");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[SaveSystem] R-4: save.tmp read failed (unauthorized) — {ex.Message} (AC-44).");
                return null;
            }

            try
            {
                var result = JsonUtility.FromJson<SaveData>(json);
                if (result == null)
                    Debug.LogWarning("[SaveSystem] R-4: save.tmp parse returned null (AC-44).");
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] R-4: save.tmp parse failed — {ex.Message} (AC-44).");
                return null;
            }
        }

        /// <summary>
        /// Core synchronous write-then-swap. Writes <paramref name="data"/> to <c>save.tmp</c>
        /// then atomically promotes it to <c>save.json</c>. Throws <see cref="IOException"/> or
        /// <see cref="UnauthorizedAccessException"/> on failure — callers are responsible for catch.
        /// Does NOT acquire <c>_writeLock</c> — must only be called from single-threaded Awake() context.
        /// </summary>
        private void WriteSaveJsonSyncCore(SaveData data)
        {
            string json  = JsonUtility.ToJson(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            _fileSystem.WriteAndFlush(_tmpPath, bytes);
            if (_fileSystem.FileExists(_savePath))
                _fileSystem.Replace(_tmpPath, _savePath, null);
            else
                _fileSystem.Move(_tmpPath, _savePath);
        }

        /// <summary>
        /// Synchronous write of <paramref name="data"/> to <c>save.json</c> via write-then-swap.
        /// Used during cold-start R-4 recovery (inside <c>PerformColdStartRead</c>) where
        /// the async W-1 path is not yet available. Runs on the main thread.
        /// Does NOT acquire <c>_writeLock</c> — Awake() is single-threaded at this point.
        /// Wraps <see cref="WriteSaveJsonSyncCore"/> with R-4-prefixed log messages.
        /// </summary>
        private void WriteSaveJsonSync(SaveData data)
        {
            try
            {
                WriteSaveJsonSyncCore(data);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[SaveSystem] R-4 write-back failed (IO): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[SaveSystem] R-4 write-back failed (unauthorized): {ex.Message}");
            }
        }

        /// <summary>
        /// Applies <c>??=</c> null-coalescing defaults to all sub-objects of <paramref name="data"/>.
        /// Extracted to avoid duplication between the R-1 success path and the R-4 recovery path.
        /// </summary>
        private static void ApplySubObjectDefaults(SaveData data)
        {
            data.level_progress ??= new LevelProgress();
            data.economy        ??= new Economy();
            data.skins          ??= new Skins();
            data.level_progress.completion_record ??= new List<CompletionRecord>();
            data.level_progress.undo_stack        ??= new List<UndoMove>();
        }

        private void HandleDowngrade(int fileSchemaVersion)
        {
            Debug.LogWarning($"[SaveSystem] R-5: schema_version={fileSchemaVersion} > MaxKnownVersion={MaxKnownVersion}. Loading defaults.");
            if (PlayerPrefs.GetInt(DowngradeNoticeShownKey, 0) == 0)
                PlayerPrefs.SetInt(DowngradeNoticeShownKey, 1);
            // PlayerPrefs.Save() deferred to Story 006.
        }

        /// <summary>Silently deletes save.tmp if it existed. Non-fatal on failure.</summary>
        private void SafeDeleteTmp(bool tmpExisted)
        {
            if (!tmpExisted) return;
            try { _fileSystem.Delete(_tmpPath); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Failed to delete save.tmp: {ex.Message}");
            }
        }

        private static SaveData CreateDefaults() => new SaveData
        {
            schema_version = MaxKnownVersion,
            level_progress = new LevelProgress
            {
                current_level_id  = 1,
                completion_record = new List<CompletionRecord>(),
                undo_stack        = new List<UndoMove>(),
            },
            economy = new Economy { coin_balance = 0 },
            skins   = new Skins(),
        };

        // ── ISaveSystem Read Methods ──────────────────────────────────────────────

        /// <inheritdoc/>
        public int GetCurrentLevelId()
        {
            GuardIsReady();
            return _saveData.level_progress.current_level_id;
        }

        /// <inheritdoc/>
        public CompletionRecord? GetCompletionRecord(int levelId)
        {
            GuardIsReady();
            var records = _saveData.level_progress.completion_record;
            for (int i = 0; i < records.Count; i++)
                if (records[i].level_id == levelId) return records[i];
            return null;
        }

        /// <inheritdoc/>
        public int GetCoinBalance()
        {
            GuardIsReady();
            return _saveData.economy.coin_balance;
        }

        /// <inheritdoc/>
        public IReadOnlyList<UndoMove> GetUndoStack()
        {
            GuardIsReady();
            return _saveData.level_progress.undo_stack;
        }

        // ── ISaveSystem Write Methods ─────────────────────────────────────────────

        /// <inheritdoc/>
        public async Awaitable WriteCompletionAtomic(int levelId, int bestStars,
            string version, int newCurrentLevelId)
        {
            // Early-exit guards — run synchronously on main thread before any await.
            if (bestStars == 0)
            {
                Debug.LogWarning("[SaveSystem] WriteCompletionAtomic: best_stars=0 is a no-op (AC-20).");
                return;
            }
            if (levelId < 1 || levelId > 9999)
            {
                Debug.LogWarning($"[SaveSystem] WriteCompletionAtomic: levelId={levelId} out of range 1–9999 (AC-36).");
                return;
            }

            // Apply in-memory mutations on main thread before snapshot (AC-21, AC-22, AC-23).
            ApplyCompletionToMemory(levelId, bestStars, version, newCurrentLevelId);

            // Capture immutable deep-copy snapshot on main thread BEFORE BackgroundThreadAsync.
            // Serialize to bytes here — JsonUtility is main-thread-only (ADR-0003).
            // Any mutations after this line will NOT be reflected in this write (AC-39).
            SaveData snapshot    = CaptureSnapshot();
            byte[]   snapshotBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));

            // Step 2: switch to background thread.
            await Awaitable.BackgroundThreadAsync();

            // Step 3: acquire write lock — prevents concurrent File.Replace calls (AC-30).
            await _writeLock.WaitAsync(destroyCancellationToken);

            // Step 4: defensive re-assertion — guarantees I/O on thread-pool thread
            // even if WaitAsync continuation runs on main thread. Do NOT remove (ADR-0003 C.1).
            await Awaitable.BackgroundThreadAsync();

            // Steps 5-6: file I/O — NO Unity API calls (Application.*, Debug.*) here.
            WriteAtomicCore(snapshotBytes);

            // Step 7: back to main thread — safe to call Unity APIs and mutate _isDirty.
            await Awaitable.MainThreadAsync();
            if (_lastWriteError != null)
            {
                Debug.LogError($"[SaveSystem] W-1 write failed: {_lastWriteError}");
                _lastWriteError = null;
                // _isDirty remains true — retry on next W-1 or W-2 trigger.
            }
            else
            {
                _isDirty = false;
            }
        }

        /// <summary>
        /// Executes the synchronous write-then-swap inside the acquired <c>_writeLock</c>.
        /// Called from <c>WriteCompletionAtomic</c> (W-1, background thread) and
        /// <c>HandleApplicationPause</c> (W-2, main thread). Caller must hold
        /// <c>_writeLock</c> before calling; this method releases it in its <c>finally</c>.
        /// Internal visibility enables direct testing without the async wrapper (AC-03 etc.).
        /// NO Unity API calls permitted here — only <c>System.IO</c> via <c>_fileSystem</c>.
        /// <paramref name="bytes"/> must be serialized on the main thread before any thread hop
        /// (JsonUtility is main-thread-only per ADR-0003).
        /// </summary>
        internal void WriteAtomicCore(byte[] bytes)
        {
            try
            {
                _fileSystem.WriteAndFlush(_tmpPath, bytes);           // FileStream + Flush(flushToDisk:true)
                if (_fileSystem.FileExists(_savePath))
                    _fileSystem.Replace(_tmpPath, _savePath, null);   // atomic swap — NOT File.Move(3-arg)
                else
                    _fileSystem.Move(_tmpPath, _savePath);            // first-ever write (2-arg overload)
            }
            catch (IOException ex)
            {
                SafeDeleteTmpBackground();
                _isDirty        = true;
                _lastWriteError = ex.Message;
            }
            catch (UnauthorizedAccessException ex)  // sibling of IOException — separate catch (ADR-0003)
            {
                SafeDeleteTmpBackground();
                _isDirty        = true;
                _lastWriteError = ex.Message;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Applies level completion mutations to the in-memory save state on the main thread,
        /// before the snapshot is captured. Enforces write-once rules (AC-21, AC-22, AC-23).
        /// Internal visibility for direct testing.
        /// </summary>
        internal void ApplyCompletionToMemory(int levelId, int bestStars,
            string version, int newCurrentLevelId)
        {
            _saveData.level_progress.current_level_id = newCurrentLevelId;
            _isDirty = true;

            var records = _saveData.level_progress.completion_record;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].level_id != levelId) continue;

                var rec = records[i];
                if (bestStars > rec.best_stars)
                    rec.best_stars = bestStars;                                // AC-21: never downgrade
                if (string.IsNullOrEmpty(rec.completion_version) && !string.IsNullOrEmpty(version))
                    rec.completion_version = version;                          // AC-22: write-once
                records[i] = rec;
                return; // AC-23: no duplicates
            }

            records.Add(new CompletionRecord
            {
                level_id           = levelId,
                best_stars         = bestStars,
                completion_version = version,
            });
        }

        /// <summary>
        /// Returns a deep copy of the current in-memory save state via JsonUtility round-trip.
        /// Any mutations to <c>_saveData</c> after this call will NOT be reflected in the snapshot.
        /// Internal visibility for direct testing (AC-39).
        /// </summary>
        internal SaveData CaptureSnapshot() =>
            JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_saveData));

        /// <summary>
        /// Silently deletes save.tmp from within the write locked section.
        /// No Unity API calls — only <c>System.IO</c> via <c>_fileSystem</c>.
        /// </summary>
        private void SafeDeleteTmpBackground()
        {
            try { _fileSystem.Delete(_tmpPath); }
            catch { /* best-effort cleanup; error already captured in _lastWriteError */ }
        }

        /// <inheritdoc/>
        public void PushUndoMove(int from, int to)
        {
            var stack = _saveData.level_progress.undo_stack;
            if (stack.Count >= 20)
                stack.RemoveAt(0);   // discard oldest entry (FIFO cap — AC-41)
            stack.Add(new UndoMove { f = from, t = to });
            _isDirty = true;
        }

        /// <inheritdoc/>
        public void SetCoinBalance(int balance)
        {
            int clamped = Math.Clamp(balance, 0, int.MaxValue);
            if (clamped != balance)
                Debug.LogWarning($"[SaveSystem] SetCoinBalance: balance={balance} clamped to {clamped} (AC-35).");
            _saveData.economy.coin_balance = clamped;
            _isDirty = true;
            // PlayerPrefs.Save() deferred to Story 006.
        }

        // ── App Pause (W-2) — Story 003 ──────────────────────────────────────────

        /// <summary>
        /// W-2 synchronous write path. Fires when app is suspended.
        /// FORBIDDEN: async void — iOS returns control to OS at first await, abandoning the write.
        /// </summary>
        private void OnApplicationPause(bool pauseStatus) =>
            HandleApplicationPause(pauseStatus, destroyCancellationToken);

        /// <summary>
        /// W-2 implementation, extracted for testability. Called by <c>OnApplicationPause</c>
        /// with <c>destroyCancellationToken</c>. No <c>await</c> permitted.
        /// </summary>
        /// <remarks>
        /// Dirty flag is checked POST-lock (not pre-lock) to prevent the race where W-1
        /// clears dirty between a pre-lock check and lock acquisition (ADR-0003 C.1).
        /// Lock release is handled by <c>WriteAtomicCore</c>'s own <c>finally</c> block when
        /// I/O is attempted; or manually here when the post-lock dirty check short-circuits.
        /// </remarks>
        internal void HandleApplicationPause(bool pauseStatus, CancellationToken cancellationToken)
        {
            if (!pauseStatus) return;

            bool acquired;
            try
            {
                // 3 000 ms budget — iOS suspends after ≈5 s; Android is similar (ADR-0003).
                acquired = _writeLock.Wait(TimeSpan.FromMilliseconds(3000), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // DDOL MonoBehaviour destroyed mid-wait — preserve _isDirty; do not write.
                return;
            }

            if (!acquired)
            {
                Debug.LogWarning("[SaveSystem] W-2 lock timeout — write skipped; _isDirty retained for retry.");
                return;
            }

            // Post-lock dirty check: W-1 may have cleared dirty during lock contention.
            if (!_isDirty)
            {
                _writeLock.Release();  // acquired but nothing to write
                return;
            }

            // Serialize on main thread — JsonUtility must not be called off main thread (ADR-0003).
            // WriteAtomicCore releases the lock in its own finally — do NOT double-release here.
            byte[] snapshotBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(CaptureSnapshot()));
            WriteAtomicCore(snapshotBytes);

            // Lock is now released. Check deferred error (WriteAtomicCore → _lastWriteError).
            if (_lastWriteError != null)
            {
                Debug.LogError($"[SaveSystem] W-2 write failed: {_lastWriteError}");
                _lastWriteError = null;
                // _isDirty remains true — retry on next W-1 or W-2 trigger.
            }
            else
            {
                _isDirty = false;
            }
        }

        // ── IsReady Guard ─────────────────────────────────────────────────────────

        private void GuardIsReady()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsReady)
                throw new InvalidOperationException(
                    "[SaveSystem] Read method called before IsReady. Use subscribe-then-check pattern. (AC-19)");
#else
            if (!IsReady)
                Debug.LogError("[SaveSystem] Read method called before IsReady. " +
                               "Caller violated the subscribe-then-check contract (ADR-0001). " +
                               "Returning default value. (AC-19)");
#endif
        }
    }
}
