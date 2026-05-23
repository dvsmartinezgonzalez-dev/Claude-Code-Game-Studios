using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BoltSort.SaveSystem;

namespace BoltSort.Tests.Helpers.SavePersistence
{
    /// <summary>
    /// Controllable <see cref="IFileSystem"/> for unit testing SaveSystem.
    /// Allows tests to:
    ///   1. Control file content returned by <see cref="ReadAllText"/>.
    ///   2. Simulate I/O exceptions thrown by <see cref="ReadAllText"/>.
    ///   3. Control which paths "exist" via <see cref="SetFileExists"/>.
    ///   4. Record whether <see cref="Delete"/> was called and for which paths.
    ///   5. Record all bytes written via <see cref="WriteAllBytes"/>.
    ///   6. Return per-path content or exceptions via <see cref="SetReadResultByPath"/>.
    ///   7. Count total read calls and calls that threw <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    /// <remarks>
    /// Shared helper for Stories 001–006 unit tests.
    /// Shared dependency: Tests.Helpers.SavePersistence assembly.
    /// </remarks>
    internal sealed class FakeFileSystem : IFileSystem
    {
        // ── ReadAllText control ───────────────────────────────────────────────────

        /// <summary>
        /// When set, <see cref="ReadAllText"/> throws this exception instead of returning content.
        /// Set to null (default) to return <see cref="ReadAllTextResult"/> normally.
        /// Takes precedence over <see cref="ReadAllTextByPath"/> for ALL paths.
        /// </summary>
        public Exception ReadException { get; set; }

        /// <summary>
        /// Content returned by <see cref="ReadAllText"/> when no exception is configured
        /// and no per-path override is registered for the requested path.
        /// Null simulates a <see cref="FileNotFoundException"/> (file not found).
        /// </summary>
        public string ReadAllTextResult { get; set; }

        // ── Per-path read control ─────────────────────────────────────────────────

        private readonly Dictionary<string, Queue<object>> _readQueueByPath
            = new Dictionary<string, Queue<object>>(StringComparer.Ordinal);

        /// <summary>
        /// Enqueues a response for the next <see cref="ReadAllText"/> call for <paramref name="path"/>.
        /// Each call to <see cref="ReadAllText"/> dequeues one entry:
        ///   • <c>string</c> — returned as file content.
        ///   • <c>Exception</c> — thrown.
        ///   • <c>null</c> — simulates <see cref="FileNotFoundException"/>.
        /// When the queue for a path is exhausted, falls back to <see cref="ReadAllTextResult"/>.
        /// Allows per-path sequencing (e.g. fail 2× then succeed) needed for iOS retry tests.
        /// </summary>
        public void SetReadResultByPath(string path, params object[] results)
        {
            if (!_readQueueByPath.TryGetValue(path, out var queue))
            {
                queue = new Queue<object>();
                _readQueueByPath[path] = queue;
            }
            foreach (var r in results)
                queue.Enqueue(r);
        }

        // ── Read-call counters ────────────────────────────────────────────────────

        /// <summary>Total number of <see cref="ReadAllText"/> calls made across all paths.</summary>
        public int ReadCallCount { get; private set; }

        /// <summary>
        /// Number of <see cref="ReadAllText"/> calls that threw <see cref="UnauthorizedAccessException"/>,
        /// whether from <see cref="ReadException"/>, a per-path queue entry, or any other source.
        /// </summary>
        public int UnauthorizedReadCount { get; private set; }

        // ── FileExists control ────────────────────────────────────────────────────

        private readonly HashSet<string> _existingPaths = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Marks <paramref name="path"/> as existing for <see cref="FileExists"/> calls.</summary>
        public void SetFileExists(string path, bool exists)
        {
            if (exists)
                _existingPaths.Add(path);
            else
                _existingPaths.Remove(path);
        }

        // ── Spy: Delete ───────────────────────────────────────────────────────────

        /// <summary>All paths passed to <see cref="Delete"/>.</summary>
        public List<string> DeletedPaths { get; } = new List<string>();

        /// <summary>True if <see cref="Delete"/> was called at least once.</summary>
        public bool WasDeleteCalled => DeletedPaths.Count > 0;

        // ── Spy: WriteAllBytes / WriteAndFlush ────────────────────────────────────

        /// <summary>All (path, bytes) pairs passed to <see cref="WriteAllBytes"/> or <see cref="WriteAndFlush"/>.</summary>
        public List<(string Path, byte[] Bytes)> WrittenFiles { get; } = new List<(string, byte[])>();

        // ── Write-path fault injection ────────────────────────────────────────────

        /// <summary>
        /// When set, <see cref="WriteAndFlush"/> throws this exception instead of recording bytes.
        /// Simulates disk-full, I/O error, or permission denied during the tmp-file write step.
        /// </summary>
        public Exception WriteAndFlushException { get; set; }

        /// <summary>
        /// When set, <see cref="Replace"/> throws this exception.
        /// Simulates atomic rename failure (IOException or UnauthorizedAccessException).
        /// </summary>
        public Exception ReplaceException { get; set; }

        /// <summary>
        /// When set, <see cref="Move"/> throws this exception.
        /// Simulates rename failure on first-ever write.
        /// </summary>
        public Exception MoveException { get; set; }

        // ── Concurrency simulation ────────────────────────────────────────────────

        /// <summary>
        /// Artificial delay applied inside <see cref="WriteAndFlush"/> before recording bytes.
        /// Set to a positive value to simulate slow I/O — enables concurrent W-1+W-1 and
        /// W-1+W-2 tests (AC-05, AC-30) that require writes to overlap in time.
        /// </summary>
        public TimeSpan WriteDelay { get; set; }

        // ── Thread capture for background-thread assertion (AC-43) ────────────────

        /// <summary>
        /// The thread that executed the most recent <see cref="WriteAndFlush"/> call.
        /// Assert <c>Thread.IsBackground == true</c> and not the Unity main thread to verify
        /// that W-1 file I/O runs off the main thread after <c>Awaitable.BackgroundThreadAsync()</c>.
        /// </summary>
        public Thread CapturedWritingThread { get; private set; }

        // ── Spy: Move / Replace ───────────────────────────────────────────────────

        /// <summary>All Move calls recorded as (source, dest) tuples.</summary>
        public List<(string Src, string Dst)> MoveCalls { get; } = new List<(string, string)>();

        /// <summary>All Replace calls recorded as (source, dest, backup) tuples.</summary>
        public List<(string Src, string Dst, string Backup)> ReplaceCalls { get; } = new List<(string, string, string)>();

        // ── IFileSystem implementation ────────────────────────────────────────────

        /// <inheritdoc/>
        public bool FileExists(string path) => _existingPaths.Contains(path);

        /// <inheritdoc/>
        public string ReadAllText(string path)
        {
            ReadCallCount++;

            // Global exception override takes priority over everything.
            if (ReadException != null)
            {
                if (ReadException is UnauthorizedAccessException)
                    UnauthorizedReadCount++;
                throw ReadException;
            }

            // Per-path queue: dequeue one entry if a queue exists and has items.
            if (_readQueueByPath.TryGetValue(path, out var queue) && queue.Count > 0)
            {
                object entry = queue.Dequeue();
                switch (entry)
                {
                    case Exception ex:
                        if (ex is UnauthorizedAccessException)
                            UnauthorizedReadCount++;
                        throw ex;
                    case string s:
                        return s;
                    case null:
                        throw new FileNotFoundException($"FakeFileSystem: file not found at '{path}'.", path);
                }
            }

            // Fall back to global ReadAllTextResult.
            if (ReadAllTextResult == null)
                throw new FileNotFoundException($"FakeFileSystem: file not found at '{path}'.", path);

            return ReadAllTextResult;
        }

        /// <inheritdoc/>
        public void WriteAllBytes(string path, byte[] bytes)
        {
            WrittenFiles.Add((path, bytes));
            _existingPaths.Add(path);
        }

        /// <inheritdoc/>
        public void WriteAndFlush(string path, byte[] bytes)
        {
            CapturedWritingThread = Thread.CurrentThread;  // captured for AC-43 thread assertion
            if (WriteDelay > TimeSpan.Zero)
                Thread.Sleep(WriteDelay);                  // simulates slow I/O for AC-05/AC-30
            if (WriteAndFlushException != null)
                throw WriteAndFlushException;              // simulates disk-full or I/O error (AC-04, AC-12)
            WrittenFiles.Add((path, bytes));
            _existingPaths.Add(path);
        }

        /// <inheritdoc/>
        public void Replace(string sourcePath, string destPath, string backupPath)
        {
            if (ReplaceException != null)
                throw ReplaceException;                    // simulates atomic rename failure (AC-04, AC-37, AC-46)
            ReplaceCalls.Add((sourcePath, destPath, backupPath));
            // Simulate the file-system effect: dest now exists, source is gone.
            _existingPaths.Add(destPath);
            _existingPaths.Remove(sourcePath);
        }

        /// <inheritdoc/>
        public void Move(string sourcePath, string destPath)
        {
            if (MoveException != null)
                throw MoveException;                       // simulates first-write rename failure
            MoveCalls.Add((sourcePath, destPath));
            _existingPaths.Add(destPath);
            _existingPaths.Remove(sourcePath);
        }

        /// <inheritdoc/>
        public void Delete(string path)
        {
            DeletedPaths.Add(path);
            _existingPaths.Remove(path);
        }
    }
}
