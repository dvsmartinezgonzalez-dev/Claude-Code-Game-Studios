using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Foundation singleton at SEO −95. Loads and validates levels.json via Addressables.
    /// <c>GameBootstrap</c> calls <see cref="InitializeAsync"/> in Start() and <see cref="ReloadAsync"/>
    /// for live-ops hot-swap; all other systems use getters only after initialization completes.
    /// </summary>
    public class LevelDataSystem : MonoBehaviour, ILevelDataSystem
    {
        public static LevelDataSystem Instance { get; private set; }

        /// <summary>True after both READY and DEGRADED transitions — callers must not hang.</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Fires synchronously when <see cref="InitializeAsync"/> completes on both READY and
        /// DEGRADED paths. ADR-0001 subscribe-then-check compatibility bridge for LevelProgression.
        /// </summary>
        public event Action OnLevelDataReady;

        private LdsState _state = LdsState.Uninitialized;
        private Dictionary<int, LevelRecord> _levelCache = new Dictionary<int, LevelRecord>();
        private TaskCompletionSource<bool> _initTcs;
        private TaskCompletionSource<bool> _reloadTcs;
        private SystemReadiness _readiness;

        /// <summary>
        /// Seam for integration testing. Set to the real Addressables loader in Awake();
        /// replace before calling <see cref="InitializeAsync"/> to inject a fake catalogue.
        /// </summary>
        public Func<Task<(bool Succeeded, string Text)>> LoadCatalogueTextAsync;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCatalogueTextAsync = DefaultLoadCatalogueTextAsync;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Begins loading levels.json and transitions to READY or DEGRADED.
        /// Idempotent: returns the in-flight Task while LOADING; returns a completed Task
        /// immediately when already READY or DEGRADED. Call from GameBootstrap.Start() only.
        /// </summary>
        public Task InitializeAsync()
        {
            if (_state == LdsState.Ready || _state == LdsState.Degraded)
                return Task.CompletedTask;
            if (_state == LdsState.Loading && _initTcs != null)
                return _initTcs.Task;

            _state = LdsState.Loading;
            _initTcs = new TaskCompletionSource<bool>();
            LoadCatalogueAsync();
            return _initTcs.Task;
        }

        /// <summary>
        /// Hot-swaps the catalogue atomically. Transitions READY/DEGRADED → LOADING → READY/DEGRADED.
        /// Idempotent while LOADING — returns the same in-flight Task without starting a second load.
        /// Fires <see cref="OnLevelDataReady"/> when the reload completes (on both READY and DEGRADED paths).
        /// Only <c>GameBootstrap</c> is the permitted caller.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called in UNINITIALIZED state.</exception>
        public Task ReloadAsync()
        {
            if (_state == LdsState.Uninitialized)
                throw new InvalidOperationException(
                    "ReloadAsync() cannot be called before InitializeAsync() completes.");
            if (_state == LdsState.Loading)
                return _reloadTcs?.Task ?? _initTcs?.Task ?? Task.CompletedTask;

            _state = LdsState.Loading;
            IsReady = false;
            _reloadTcs = new TaskCompletionSource<bool>();
            var reloadTask = _reloadTcs.Task; // capture before ReloadCatalogueAsync may null _reloadTcs
            ReloadCatalogueAsync();
            return reloadTask;
        }

        private async void LoadCatalogueAsync()
        {
            try
            {
                var (succeeded, text) = await LoadCatalogueTextAsync();

                if (!succeeded)
                {
                    EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0);
                    return;
                }

                var catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(text);

                int loaded = 0, skipped = 0;
                foreach (var record in catalogue?.Levels ?? Array.Empty<LevelRecord>())
                {
                    if (LevelRecordValidator.ValidateRecord(record, out _))
                    { _levelCache[record.LevelId] = record; loaded++; }
                    else
                    { skipped++; }
                }

                int total = loaded + skipped;
                float failureRatio = total > 0 ? (float)skipped / total : 1f;
                if (total == 0 || failureRatio > 0.20f)
                    EnterDegraded("CATALOGUE_PARTIAL_FAILURE", loaded, skipped);
                else
                    EnterReady(loaded, skipped, catalogue?.CatalogueVersion ?? 0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelDataSystem] LoadCatalogueAsync failed: {ex.Message}");
                EnterDegraded("CATALOGUE_LOAD_FAILED", 0, 0);
            }
        }

        private async void ReloadCatalogueAsync()
        {
            var newCache = new Dictionary<int, LevelRecord>();
            try
            {
                var (succeeded, text) = await LoadCatalogueTextAsync();

                if (!succeeded)
                { CompleteReload(null, "CATALOGUE_LOAD_FAILED", 0, 0); return; }

                var catalogue = JsonConvert.DeserializeObject<LevelCatalogue>(text);

                int loaded = 0, skipped = 0;
                foreach (var record in catalogue?.Levels ?? Array.Empty<LevelRecord>())
                {
                    if (LevelRecordValidator.ValidateRecord(record, out _))
                    { newCache[record.LevelId] = record; loaded++; }
                    else { skipped++; }
                }

                int total = loaded + skipped;
                float ratio = total > 0 ? (float)skipped / total : 1f;
                if (total == 0 || ratio > 0.20f)
                    CompleteReload(null, "CATALOGUE_PARTIAL_FAILURE", loaded, skipped);
                else
                    CompleteReload(newCache, null, loaded, skipped, catalogue?.CatalogueVersion ?? 0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelDataSystem] ReloadCatalogueAsync failed: {ex.Message}");
                CompleteReload(null, "CATALOGUE_LOAD_FAILED", 0, 0);
            }
        }

        private void CompleteReload(Dictionary<int, LevelRecord> newCache, string errorCode,
                                    int loaded, int skipped, int version = 0)
        {
            if (newCache != null)
            {
                _levelCache = newCache;
                _state = LdsState.Ready;
                IsReady = true;
                _readiness = new SystemReadiness(true, loaded, skipped, version, null);
            }
            else
            {
                _state = LdsState.Degraded;
                IsReady = true;
                _readiness = new SystemReadiness(false, loaded, skipped, 0, errorCode);
            }
            OnLevelDataReady?.Invoke();
            _reloadTcs?.TrySetResult(true);
            // Null out before any re-entrant ReloadAsync call from an OnLevelDataReady subscriber
            // can observe the old (completed) TCS and incorrectly coalesce to it.
            _reloadTcs = null;
        }

        private static async Task<(bool Succeeded, string Text)> DefaultLoadCatalogueTextAsync()
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>("levels.json");
            try
            {
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded)
                    return (false, null);
                return (true, handle.Result.text);
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        private void EnterReady(int loaded, int skipped, int version)
        {
            _state = LdsState.Ready;
            IsReady = true;
            _readiness = new SystemReadiness(true, loaded, skipped, version, null);
            OnLevelDataReady?.Invoke();
            _initTcs?.TrySetResult(true);
        }

        private void EnterDegraded(string code, int loaded, int skipped)
        {
            _state = LdsState.Degraded;
            IsReady = true;                // true even on DEGRADED — callers must not hang
            _readiness = new SystemReadiness(false, loaded, skipped, 0, code);
            OnLevelDataReady?.Invoke();    // compatibility bridge fires on DEGRADED too
            _initTcs?.TrySetResult(true);
        }

        /// <summary>
        /// Returns the current system readiness snapshot. Safe to call in any state.
        /// Returns <c>default(SystemReadiness)</c> (Ready=false, all counts 0, DiagnosticCode=null)
        /// before <see cref="InitializeAsync"/> completes — callers must check <see cref="IsReady"/> first.
        /// </summary>
        public SystemReadiness GetReadiness() => _readiness;

        /// <summary>Returns the level with the given ID.</summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        /// <exception cref="LevelDataException">When the level ID is not present in the catalogue.</exception>
        public LevelRecord GetLevel(int levelId)
        {
            GuardReady();
            if (!_levelCache.TryGetValue(levelId, out var record))
                throw new LevelDataException($"Level {levelId} not found.", LdsErrorCode.NotFound);
            return record;
        }

        /// <summary>
        /// Returns all records with IDs in [fromId, toId], ordered by ID ascending.
        /// Returns an empty array if fromId &gt; toId or no IDs in the range are in the catalogue.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        public LevelRecord[] GetRange(int fromId, int toId)
        {
            GuardReady();
            if (fromId > toId) return Array.Empty<LevelRecord>();
            long count = (long)toId - fromId + 1;
            if (count > int.MaxValue) return Array.Empty<LevelRecord>();
            return Enumerable.Range(fromId, (int)count)
                .Where(id => _levelCache.ContainsKey(id))
                .Select(id => _levelCache[id])
                .ToArray();
        }

        /// <summary>
        /// Returns all records matching the given filter, in unspecified order.
        /// Returns an empty array if no records match — never returns null.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        public LevelRecord[] GetByFilter(LevelFilter filter)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            GuardReady();
            return _levelCache.Values.Where(filter.Matches).ToArray();
        }

        private void GuardReady()
        {
            if (_state == LdsState.Uninitialized || _state == LdsState.Loading)
                throw new InvalidOperationException(
                    $"LevelDataSystem not ready (state: {_state}). Await InitializeAsync() first.");
        }

        /// <summary>Clears the static Instance reference. Call in test TearDown only.</summary>
        internal static void ClearInstanceForTesting() => Instance = null;

        /// <summary>
        /// Seeds the system with a known READY state directly. Call from test SetUp only —
        /// bypasses the Addressables load pipeline to enable synchronous unit tests of getter logic.
        /// </summary>
        internal void SeedCacheForTesting(IEnumerable<LevelRecord> records, SystemReadiness readiness)
        {
            _levelCache.Clear();
            foreach (var r in records) _levelCache[r.LevelId] = r;
            _state = LdsState.Ready;
            IsReady = true;
            _readiness = readiness;
        }
    }
}
