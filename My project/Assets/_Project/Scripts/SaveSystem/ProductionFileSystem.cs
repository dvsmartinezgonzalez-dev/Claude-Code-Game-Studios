using System.IO;
using System.Text;

namespace BoltSort.SaveSystem
{
    /// <summary>
    /// Production implementation of <see cref="IFileSystem"/>. Thin wrapper over
    /// <c>System.IO.File</c> — no logic, no error handling (exceptions propagate to callers).
    /// </summary>
    /// <remarks>
    /// Internal: only <see cref="SaveSystem"/> and test helpers reference this type.
    /// Do not add error handling here — callers own their catch blocks (ADR-0003).
    /// </remarks>
    internal sealed class ProductionFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public bool FileExists(string path) => File.Exists(path);

        /// <inheritdoc/>
        public string ReadAllText(string path) => File.ReadAllText(path);

        /// <inheritdoc/>
        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

        /// <inheritdoc/>
        public void WriteAndFlush(string path, byte[] bytes)
        {
            // FileStream + Flush(flushToDisk:true) guarantees the OS write buffer is pushed to
            // physical storage before the rename step (ADR-0003 write-then-swap requirement).
            // File.WriteAllBytes does NOT provide this guarantee.
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(flushToDisk: true);
        }

        /// <inheritdoc/>
        public void Replace(string sourcePath, string destPath, string backupPath)
            => File.Replace(sourcePath, destPath, backupPath);

        /// <inheritdoc/>
        public void Move(string sourcePath, string destPath)
            => File.Move(sourcePath, destPath);

        /// <inheritdoc/>
        public void Delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
