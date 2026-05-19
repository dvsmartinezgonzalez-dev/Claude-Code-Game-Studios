namespace BoltSort.SortMechanic
{
    /// <summary>
    /// Logging contract for diagnostic output from <see cref="SortMechanic"/>.
    /// Injected via constructor / test seam so unit tests can assert on structured
    /// log calls without coupling to <c>UnityEngine.Debug</c>.
    /// </summary>
    /// <remarks>
    /// AC-18b and AC-27 require that <c>Error</c> is called with category <c>"SortMechanic"</c>
    /// and a structured payload carrying the failure reason when an initialization assertion fails.
    /// Tests assert on the call itself — not on the formatted message text.
    ///
    /// Production implementation should forward to <c>Debug.LogError</c> or a
    /// centralised logging service; the interface is deliberately minimal.
    /// </remarks>
    public interface IDiagnosticLogger
    {
        /// <summary>
        /// Logs a structured error message.
        /// </summary>
        /// <param name="category">Subsystem name (e.g. <c>"SortMechanic"</c>).</param>
        /// <param name="message">Human-readable summary of the error.</param>
        /// <param name="payload">
        /// Optional structured payload for machine-readable diagnostics.
        /// Convention used by Sort Mechanic: an anonymous object such as
        /// <c>new { reason = SortMechLoadFailReason.CorruptedBoardState }</c>.
        /// </param>
        void Error(string category, string message, object payload = null);

        /// <summary>
        /// Logs a structured warning message.
        /// </summary>
        /// <param name="category">Subsystem name.</param>
        /// <param name="message">Human-readable summary.</param>
        /// <param name="payload">Optional structured payload.</param>
        void Warning(string category, string message, object payload = null);
    }
}
