using System.IO;

namespace BoltSort.SaveSystem
{
    /// <summary>
    /// Testability seam over <c>System.IO.File</c> operations.
    /// Production code uses <see cref="ProductionFileSystem"/>.
    /// Tests inject <c>FakeFileSystem</c> to control file content and simulate I/O failures.
    /// </summary>
    /// <remarks>
    /// This interface is a shared dependency for Stories 001–006.
    /// Without it, file-I/O paths cannot be tested in EditMode.
    ///
    /// All methods let exceptions propagate naturally — callers own catch blocks:
    ///   <see cref="FileNotFoundException"/> — file not found (R-3)
    ///   <see cref="UnauthorizedAccessException"/> — iOS file protection (R-4 / iOS retry)
    ///   <see cref="IOException"/> — corruption or I/O error (R-4)
    ///
    /// NOTE: <see cref="File.Move(string, string)"/> two-argument overload only — the
    /// three-argument <c>File.Move(src, dst, overwrite: true)</c> does NOT exist in
    /// .NET Standard 2.1 (Unity 6.3 BCL) and causes a compile error. (ADR-0003)
    /// </remarks>
    public interface IFileSystem
    {
        /// <summary>Returns true if the file at <paramref name="path"/> exists.</summary>
        bool FileExists(string path);

        /// <summary>
        /// Reads the entire contents of the file at <paramref name="path"/> as a UTF-8 string.
        /// Throws <see cref="FileNotFoundException"/> if missing,
        /// <see cref="UnauthorizedAccessException"/> if protected (iOS cold-start),
        /// or <see cref="IOException"/> on other I/O errors.
        /// </summary>
        string ReadAllText(string path);

        /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/>, creating or replacing the file.</summary>
        void WriteAllBytes(string path, byte[] bytes);

        /// <summary>
        /// Writes <paramref name="bytes"/> to <paramref name="path"/> via <c>FileStream</c> with
        /// <c>Flush(flushToDisk: true)</c>, guaranteeing the data is durable on storage before
        /// returning. Required for the save.tmp write step in W-1 and W-2 (ADR-0003 write-then-swap).
        /// </summary>
        /// <remarks>
        /// Production: <c>FileStream(path, FileMode.Create) + Write + Flush(flushToDisk:true)</c>.
        /// Test: records bytes; simulates delay and exceptions via <c>WriteDelay</c>/<c>WriteAndFlushException</c>.
        /// Use this method — not <see cref="WriteAllBytes"/> — whenever a durable fsync is required.
        /// </remarks>
        void WriteAndFlush(string path, byte[] bytes);

        /// <summary>
        /// Atomically replaces <paramref name="destPath"/> with <paramref name="sourcePath"/>,
        /// optionally backing up the replaced file to <paramref name="backupPath"/>.
        /// Pass <c>null</c> for <paramref name="backupPath"/> when no backup is needed.
        /// </summary>
        void Replace(string sourcePath, string destPath, string backupPath);

        /// <summary>
        /// Moves <paramref name="sourcePath"/> to <paramref name="destPath"/>.
        /// Two-argument overload only — three-argument overload is not available in .NET Standard 2.1.
        /// </summary>
        void Move(string sourcePath, string destPath);

        /// <summary>Deletes the file at <paramref name="path"/>. No-op if the file does not exist.</summary>
        void Delete(string path);
    }
}
