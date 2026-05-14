using Newtonsoft.Json;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Root container for the levels.json catalogue file.
    /// Deserialized by LevelDataSystem via JsonConvert.DeserializeObject&lt;LevelCatalogue&gt;(json).
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class LevelCatalogue
    {
        /// <summary>
        /// Monotonically increasing integer, incremented with each catalogue publication.
        /// Defaults to 0 when the key is absent from JSON.
        /// </summary>
        [JsonProperty("catalogue_version")]
        public int CatalogueVersion { get; private set; }

        /// <summary>All level records in this catalogue.</summary>
        [JsonProperty("levels")]
        public LevelRecord[] Levels { get; private set; }
    }
}
