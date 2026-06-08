using Newtonsoft.Json;
using UnityEngine;

namespace BoltSort.GameStateManager
{
    /// <summary>
    /// Minimal synchronous <see cref="IBoardSnapshotSystem"/> backed by PlayerPrefs (SER-01/02/03).
    /// Uses Newtonsoft.Json — JsonUtility cannot serialize the jagged <c>int[][]</c> fields on
    /// <see cref="BoardSnapshot"/> (ADR-0004). Storage follows the "bs." PlayerPrefs namespace
    /// convention established in the Save &amp; Persistence epic (Story 006).
    /// Single-use: <see cref="ReadBoardSnapshot"/> consumes (deletes) the stored snapshot so a
    /// stale board never resurfaces after a successful restore.
    /// </summary>
    internal sealed class BoardSnapshotSystem : IBoardSnapshotSystem
    {
        private const string SnapshotKey = "bs.board_snapshot";

        /// <inheritdoc/>
        public void WriteBoardSnapshotSync(BoardSnapshot snapshot)
        {
            if (snapshot == null) return;

            string json = JsonConvert.SerializeObject(snapshot);
            PlayerPrefs.SetString(SnapshotKey, json);
            PlayerPrefs.Save(); // AC-10 pattern: explicit save — OnApplicationQuit not guaranteed (ADR-0003)
        }

        /// <inheritdoc/>
        public BoardSnapshot ReadBoardSnapshot()
        {
            if (!PlayerPrefs.HasKey(SnapshotKey))
                return null;

            string json = PlayerPrefs.GetString(SnapshotKey, string.Empty);
            PlayerPrefs.DeleteKey(SnapshotKey);
            PlayerPrefs.Save();

            if (string.IsNullOrEmpty(json))
                return null;

            BoardSnapshot snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<BoardSnapshot>(json);
            }
            catch (JsonException ex)
            {
                Debug.LogWarning($"[BoardSnapshotSystem] Corrupt board snapshot — {ex.Message}");
                return new BoardSnapshot { IsValid = false };
            }

            return snapshot ?? new BoardSnapshot { IsValid = false };
        }
    }
}
