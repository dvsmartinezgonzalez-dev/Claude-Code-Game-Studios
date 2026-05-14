using System;

namespace BoltSort.LevelData
{
    /// <summary>
    /// Thrown by <see cref="LevelDataSystem"/> getter methods when a level cannot be found
    /// or the system cannot fulfil the request.
    /// </summary>
    public class LevelDataException : Exception
    {
        /// <summary>Typed error code indicating why the operation failed.</summary>
        public LdsErrorCode ErrorCode { get; }

        public LevelDataException(string message, LdsErrorCode errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
