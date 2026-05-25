using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BoltSort.SaveSystem
{
    /// <summary>
    /// Public contract for the Save &amp; Persistence system.
    /// All consumers must use the subscribe-then-check pattern:
    /// <code>
    /// SaveSystem.Instance.OnSaveReady += HandleSaveReady;
    /// if (SaveSystem.Instance.IsReady) HandleSaveReady();
    /// </code>
    /// </summary>
    /// <remarks>
    /// ADR-0003: Save System Design — defines schema v1, cold-start cases, and write contract.
    /// ADR-0001: Singleton Architecture — mandates subscribe-then-check for all OnSaveReady consumers.
    ///
    /// Method implementation schedule:
    ///   Story 001 — IsReady, OnSaveReady, GetCurrentLevelId, GetCompletionRecord, GetCoinBalance
    ///   Story 002 — WriteCompletionAtomic (background atomic write), PushUndoMove, GetUndoStack
    ///   Story 006 — SetCoinBalance (PlayerPrefs.Save() call)
    /// </remarks>
    public interface ISaveSystem
    {
        /// <summary>
        /// True after cold-start read completes successfully. Set synchronously inside
        /// <c>Awake()</c> before any lower-SEO system's <c>Start()</c> runs.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Fires at the end of <c>SaveSystem.Awake()</c> after <see cref="IsReady"/> is set.
        /// Subscribe with the subscribe-then-check pattern — the event may have already fired
        /// by the time a lower-SEO system subscribes.
        /// Must be <c>event Action</c> — never UnityEvent (ADR-0002: no UnityEvent on hot paths).
        /// </summary>
        event Action OnSaveReady;

        /// <summary>
        /// Returns the current level ID from in-memory save state.
        /// Default: 1 (fresh install).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// DEBUG build only: thrown if called before <see cref="IsReady"/> is true.
        /// </exception>
        int GetCurrentLevelId();

        /// <summary>
        /// Returns the completion record for the given level, or <c>null</c> if not yet completed.
        /// Empty <c>completion_version</c> field ("") is the absent-sentinel — treat as not-yet-written.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// DEBUG build only: thrown if called before <see cref="IsReady"/> is true.
        /// </exception>
        CompletionRecord? GetCompletionRecord(int levelId);

        /// <summary>
        /// Writes level completion data atomically to disk on a background thread.
        /// Captures a snapshot on the calling (main) thread before switching to background.
        /// Implements the W-1 write path: background thread via <c>Task.Run</c> + <c>SemaphoreSlim</c> lock.
        /// </summary>
        /// <remarks>
        /// The <c>async</c> keyword is an implementation detail — the interface declares
        /// <c>Task</c> as the return type so callers can <c>await</c> if needed.
        /// </remarks>
        Task WriteCompletionAtomic(int levelId, int bestStars, string version, int newCurrentLevelId);

        /// <summary>
        /// Returns the current coin balance from in-memory save state.
        /// Clamped to 0 at load time if the stored value is negative.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// DEBUG build only: thrown if called before <see cref="IsReady"/> is true.
        /// </exception>
        int GetCoinBalance();

        /// <summary>
        /// Updates the in-memory coin balance and marks the save as dirty.
        /// Clamps negative values to 0 (AC-07). The dirty flag causes W-2 to persist the change.
        /// Does not write PlayerPrefs — coin balance is persisted via the JSON file (W-1 or W-2).
        /// </summary>
        void SetCoinBalance(int balance);

        /// <summary>
        /// Appends an undo move to the in-memory undo stack. Capped at 20 entries —
        /// the oldest entry is discarded when the cap is exceeded (FIFO eviction).
        /// </summary>
        /// <param name="from">Source stack index (flat namespace).</param>
        /// <param name="to">Destination stack index (flat namespace).</param>
        void PushUndoMove(int from, int to);

        /// <summary>
        /// Returns a snapshot of the current undo stack (read-only copy).
        /// Returns an empty list when no moves have been pushed.
        /// </summary>
        IReadOnlyList<UndoMove> GetUndoStack();
    }
}
