using System;
using System.Collections.Generic;

namespace BoltSort.SaveSystem
{
    // ── Schema v1 Data Classes ────────────────────────────────────────────────────
    //
    // All classes must be [Serializable] for JsonUtility.
    // Fields use snake_case because JsonUtility serializes field names verbatim —
    // the JSON key is exactly the C# field name.
    //
    // PUBLIC FIELDS only — JsonUtility does not serialize properties.
    // [SerializeField] on properties is a compile error in Unity 6.3 (ADR-0001).
    //
    // JsonUtility null-string behaviour: absent string fields round-trip as "" (empty string).
    // "" is the absent-sentinel for completion_version — treat as "not yet written".
    //
    // Schema defined in ADR-0003 / design/gdd/save-persistence.md section C.2.

    /// <summary>Root save document. Serialized to Application.persistentDataPath/save.json.</summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>
        /// Integer schema version. Incremented when the save format changes.
        /// <c>MAX_KNOWN_VERSION = 1</c> — files with a higher version trigger R-5 (downgrade).
        /// </summary>
        public int schema_version;

        /// <summary>Level progress sub-document.</summary>
        public LevelProgress level_progress;

        /// <summary>Economy sub-document (coin balance).</summary>
        public Economy economy;

        /// <summary>Skins sub-document. Reserved for future use.</summary>
        public Skins skins;
    }

    /// <summary>Level progress sub-document. Contains current level and completion history.</summary>
    [Serializable]
    public class LevelProgress
    {
        /// <summary>
        /// The level the player should load next. Default: 1 (fresh install).
        /// Written atomically with <c>completion_record</c> by <c>WriteCompletionAtomic</c>.
        /// </summary>
        public int current_level_id = 1;

        /// <summary>
        /// Per-level completion records. Order is not guaranteed — look up by <c>level_id</c>.
        /// JsonUtility serializes null List as empty list on deserialization.
        /// </summary>
        public List<CompletionRecord> completion_record = new List<CompletionRecord>();

        /// <summary>
        /// In-progress undo stack. Session-only — cleared on fresh load.
        /// Stored for crash-recovery (SER-01/SER-02 in GSM). Empty list when no moves made.
        /// </summary>
        public List<UndoMove> undo_stack = new List<UndoMove>();
    }

    /// <summary>
    /// Single completion record for one level. Immutable after first write — see ADR-0003
    /// write-once contract for <c>completion_version</c>.
    /// </summary>
    [Serializable]
    public struct CompletionRecord
    {
        /// <summary>Level identifier this record belongs to.</summary>
        public int level_id;

        /// <summary>Best star rating achieved (1–3). Never decremented.</summary>
        public int best_stars;

        /// <summary>
        /// App version string at time of first completion. Write-once.
        /// JsonUtility serializes absent/null strings as "" — treat "" as the absent-sentinel.
        /// Migrators must never overwrite a previously empty field (ADR-0003 write-once contract).
        /// </summary>
        public string completion_version;
    }

    /// <summary>Economy sub-document.</summary>
    [Serializable]
    public class Economy
    {
        /// <summary>
        /// Current coin balance. Must be ≥ 0 — negative values are clamped to 0 at load time
        /// and an anomaly warning is logged (AC-27).
        /// </summary>
        public int coin_balance;
    }

    /// <summary>Skins sub-document. Reserved for a future cosmetics system.</summary>
    [Serializable]
    public class Skins
    {
        /// <summary>Placeholder field. Value "reserved" signals this section is not yet implemented.</summary>
        public string _status = "reserved";
    }

    /// <summary>
    /// A single undo move entry in the undo stack. Uses compact field names to minimise
    /// save file size — <c>f</c> = from stack index, <c>t</c> = to stack index.
    /// </summary>
    [Serializable]
    public struct UndoMove
    {
        /// <summary>Flat-namespace source stack index (from).</summary>
        public int f;

        /// <summary>Flat-namespace destination stack index (to).</summary>
        public int t;
    }
}
