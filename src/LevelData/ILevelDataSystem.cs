using System;
using System.Threading.Tasks;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Public contract for the Level Data System. Depend on this interface, not the concrete class.
    /// </summary>
    public interface ILevelDataSystem
    {
        /// <summary>True after both READY and DEGRADED transitions.</summary>
        bool IsReady { get; }

        /// <summary>
        /// Fires synchronously when <see cref="InitializeAsync"/> or <see cref="ReloadAsync"/>
        /// completes — on both READY and DEGRADED paths.
        /// </summary>
        event Action OnLevelDataReady;

        /// <summary>Loads levels.json and transitions to READY or DEGRADED. Idempotent.</summary>
        Task InitializeAsync();

        /// <summary>
        /// Hot-swaps the catalogue. Transitions READY/DEGRADED → LOADING → READY/DEGRADED.
        /// Idempotent while LOADING — returns the in-flight Task.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called in UNINITIALIZED state.</exception>
        Task ReloadAsync();

        /// <summary>Returns the current system readiness snapshot. Safe to call in any state.</summary>
        SystemReadiness GetReadiness();

        /// <summary>Returns the level with the given ID.</summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        /// <exception cref="LevelDataException">When the level ID is not present in the catalogue.</exception>
        LevelRecord GetLevel(int levelId);

        /// <summary>
        /// Returns all records with IDs in [fromId, toId], ordered by ID ascending.
        /// Returns an empty array if fromId &gt; toId or no IDs in the range are in the catalogue.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        LevelRecord[] GetRange(int fromId, int toId);

        /// <summary>
        /// Returns all records matching the given filter, in unspecified order.
        /// Returns an empty array if no records match — never returns null.
        /// </summary>
        /// <exception cref="InvalidOperationException">When called before initialization completes.</exception>
        /// <exception cref="ArgumentNullException">When filter is null.</exception>
        LevelRecord[] GetByFilter(LevelFilter filter);
    }
}
