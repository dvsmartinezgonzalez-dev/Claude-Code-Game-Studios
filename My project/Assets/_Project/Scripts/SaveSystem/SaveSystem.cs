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
        /// Synchronous cold-start file read. Dispatches to R-1/R-3/R-5 based on file
        /// existence, parse success, and schema_version. Cases R-2/R-4 are stubs pending
        /// Stories 004 and 005.
        /// </summary>
        private void PerformColdStartRead()
        {
            bool tmpExists = _fileSystem.FileExists(_tmpPath);

            string json = null;
            try
            {
                json = _fileSystem.ReadAllText(_savePath);
            }
            catch (FileNotFoundException) { /* R-3: fresh install */ }
            catch (UnauthorizedAccessException)
            {
                // iOS cold-start file protection. Story 004 adds retry loop.
                // Sibling of IOException — caught separately (ADR-0003).
                Debug.LogWarning("[SaveSystem] UnauthorizedAccessException on cold-start read — defaults. Story 004 adds retry.");
            }
            catch (IOException ex)
            {
                // R-4 stub. Story 004 adds recovery.
                Debug.LogWarning($"[SaveSystem] IOException on cold-start read — defaults. {ex.Message}");
            }

            if (json == null)
            {
                _saveData = CreateDefaults();
                SafeDeleteTmp(tmpExists);
                return;
            }

            SaveData parsed = null;
            try { parsed = JsonUtility.FromJson<SaveData>(json); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] JSON parse failed — defaults. {ex.Message}");
            }

            if (parsed == null)
            {
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

            if (parsed.schema_version < MaxKnownVersion && parsed.schema_version > 0)
                _isDirty = true; // R-2 stub: Story 005 adds migration

            _saveData = parsed;
            _saveData.level_progress ??= new LevelProgress();
            _saveData.economy        ??= new Economy();
            _saveData.skins          ??= new Skins();
            _saveData.level_progress.completion_record ??= new List<CompletionRecord>();
            _saveData.level_progress.undo_stack        ??= new List<UndoMove>();

            if (_saveData.economy.coin_balance < 0)
            {
                Debug.LogWarning("[SaveSystem] Negative coin_balance clamped to 0 (AC-27).");
                _saveData.economy.coin_balance = 0;
            }

            SafeDeleteTmp(tmpExists);
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
            // Any mutations after this line will NOT be reflected in this write (AC-39).
            SaveData snapshot = CaptureSnapshot();

            // Step 2: switch to background thread.
            await Awaitable.BackgroundThreadAsync();

            // Step 3: acquire write lock — prevents concurrent File.Replace calls (AC-30).
            await _writeLock.WaitAsync(destroyCancellationToken);

            // Step 4: defensive re-assertion — guarantees I/O on thread-pool thread
            // even if WaitAsync continuation runs on main thread. Do NOT remove (ADR-0003 C.1).
            await Awaitable.BackgroundThreadAsync();

            // Steps 5-6: file I/O — NO Unity API calls (Application.*, Debug.*) here.
            WriteAtomicCore(snapshot);

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
        /// Called from <c>WriteCompletionAtomic</c> on a background thread.
        /// Internal visibility enables direct testing without the async wrapper (AC-03 etc.).
        /// NO Unity API calls permitted here — only <c>System.IO</c> via <c>_fileSystem</c>.
        /// </summary>
        internal void WriteAtomicCore(SaveData snapshot)
        {
            try
            {
                string json  = JsonUtility.ToJson(snapshot);          // JsonUtility, NOT JsonConvert
                byte[] bytes = Encoding.UTF8.GetBytes(json);
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
        /// W-2 synchronous write path. Story 003 implements this.
        /// FORBIDDEN: async void OnApplicationPause — iOS returns control to OS at first await.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            // Story 003: _writeLock.Wait(destroyCancellationToken); if (_isDirty) WriteAtomicCore(CaptureSnapshot());
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
