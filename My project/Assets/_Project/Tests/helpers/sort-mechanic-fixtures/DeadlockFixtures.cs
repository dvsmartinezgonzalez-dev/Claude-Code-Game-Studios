namespace BoltSort.Tests.Helpers.SortMechanic
{
    /// <summary>
    /// Canonical board state constants for Sort Mechanic deadlock detection tests (TR-SORT-005).
    /// All fixtures represent board states where no legal depth-1 first move exists.
    ///
    /// Apply to MockGsm via:
    ///   gsm.SetColorStacks(
    ///       DeadlockFixtures.CanonicalStackA,
    ///       DeadlockFixtures.CanonicalStackB);
    ///   gsm.StackDepth = DeadlockFixtures.CanonicalStackDepth;
    ///   gsm.TempSlotCount = 0;
    ///   gsm.TempSlotContents = null;
    ///   gsm.TempSlotDepth = DeadlockFixtures.CanonicalStackDepth;
    ///
    /// Deadlock proof:
    ///   Source=A, hold color 2 (top of A=[1,2]), dest=B=[2,1] full → DestinationFull
    ///   Source=B, hold color 1 (top of B=[2,1]), dest=A=[1,2] full → DestinationFull
    ///   → No legal (source, destination) pair → HasLegalMove() returns false.
    /// </summary>
    public static class DeadlockFixtures
    {
        /// <summary>Stack A of the canonical interlocked deadlock: [bottom=1, top=2].</summary>
        public static readonly int[] CanonicalStackA = { 1, 2 };

        /// <summary>Stack B of the canonical interlocked deadlock: [bottom=2, top=1].</summary>
        public static readonly int[] CanonicalStackB = { 2, 1 };

        /// <summary>Stack depth for the canonical fixture. Both stacks are full at this depth.</summary>
        public const int CanonicalStackDepth = 2;

        /// <summary>Color count for the canonical fixture.</summary>
        public const int CanonicalColorCount = 2;

        /// <summary>Temp slot count for the canonical fixture (none).</summary>
        public const int CanonicalTempSlotCount = 0;
    }
}
