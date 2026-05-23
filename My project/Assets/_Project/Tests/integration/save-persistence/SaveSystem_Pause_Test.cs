using System;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using BoltSort.Tests.Helpers.SavePersistence;
using SS = BoltSort.SaveSystem.SaveSystem;

// Test assembly: Tests.Integration.SaveSystem
// Covers: AC-06, AC-07, AC-08, AC-09, AC-42
//   from design/gdd/save-persistence.md (Story 003 scope)
// Note: Pause_W2AfterW1_DirtyCheckPostLock (concurrent W-1 + W-2) deferred to PlayMode — S3-08.
// Story: production/epics/save-persistence/story-003-w2-pause-write.md

namespace BoltSort.Tests.Integration.SaveSystem
{
    [TestFixture]
    public class SaveSystem_Pause_Test
    {
        private FakeFileSystem _fakeFs;
        private SS _saveSystem;

        [SetUp]
        public void SetUp()
        {
            _fakeFs = new FakeFileSystem();
            _fakeFs.ReadAllTextResult =
                @"{""schema_version"":1," +
                @"""level_progress"":{""current_level_id"":1,""completion_record"":[],""undo_stack"":[]}," +
                @"""economy"":{""coin_balance"":0}," +
                @"""skins"":{""_status"":""reserved""}}";
            SS.SetFileSystemForTesting(_fakeFs);
            var go = new GameObject("SaveSystem_Pause_Test");
            _saveSystem = go.AddComponent<SS>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_saveSystem != null)
                UnityEngine.Object.DestroyImmediate(_saveSystem.gameObject);
            SS.ClearInstanceForTesting();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void Pause(bool pauseStatus = true) =>
            _saveSystem.HandleApplicationPause(pauseStatus, CancellationToken.None);

        private int TotalWriteOps =>
            _fakeFs.WrittenFiles.Count + _fakeFs.MoveCalls.Count + _fakeFs.ReplaceCalls.Count;

        // ── AC-06: dirty=false → no I/O ──────────────────────────────────────────

        [Test]
        public void Pause_DirtyFalse_NoFileIO()
        {
            Pause();
            Assert.AreEqual(0, TotalWriteOps, "AC-06: no file I/O when dirty=false");
        }

        // ── AC-07: pauseStatus=false → no write ───────────────────────────────────

        [Test]
        public void Pause_PauseStatusFalse_NoWrite()
        {
            _saveSystem.PushUndoMove(0, 1);
            Pause(false);
            Assert.AreEqual(0, TotalWriteOps, "AC-07: no write when pauseStatus=false");
        }

        // ── AC-08: dirty=true + pause → synchronous write completes ──────────────

        [Test]
        public void Pause_DirtyTrue_SynchronousWriteCompletes()
        {
            _saveSystem.PushUndoMove(0, 1);
            Pause();
            Assert.Greater(TotalWriteOps, 0, "AC-08: write must complete synchronously");
        }

        // ── AC-09 success: dirty cleared after write ──────────────────────────────

        [Test]
        public void Pause_WriteSuccess_DirtyFlagCleared()
        {
            _saveSystem.PushUndoMove(0, 1);
            Pause();
            int afterFirst = TotalWriteOps;
            Assert.Greater(afterFirst, 0, "Setup: first pause must write");

            Pause();
            Assert.AreEqual(afterFirst, TotalWriteOps,
                "AC-09 success: dirty cleared; second pause is a no-op");
        }

        // ── AC-09 failure: IOException → dirty retained ───────────────────────────

        [Test]
        public void Pause_WriteIOException_DirtyFlagRetained()
        {
            _saveSystem.PushUndoMove(0, 1);
            _fakeFs.WriteAndFlushException = new IOException("Disk full");
            Pause();

            _fakeFs.WriteAndFlushException = null;
            int beforeRetry = TotalWriteOps;
            Pause();
            Assert.Greater(TotalWriteOps, beforeRetry,
                "AC-09 failure: dirty retained; retry write succeeds on next trigger");
        }

        // ── UnauthorizedAccessException → dirty retained ──────────────────────────

        [Test]
        public void Pause_UnauthorizedAccessException_DirtyFlagRetained()
        {
            _fakeFs.SetFileExists(Application.persistentDataPath + "/save.json", true);
            _saveSystem.PushUndoMove(0, 1);
            _fakeFs.ReplaceException = new UnauthorizedAccessException("Permission denied");
            Pause();

            _fakeFs.ReplaceException = null;
            int beforeRetry = TotalWriteOps;
            Pause();
            Assert.Greater(TotalWriteOps, beforeRetry,
                "UnauthorizedAccessException: dirty retained; retry succeeds");
        }

        // ── AC-42: cancellation → silent catch, no write ─────────────────────────

        [Test]
        public void Pause_CancellationToken_OperationCanceledSilently()
        {
            _saveSystem.PushUndoMove(0, 1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.DoesNotThrow(
                () => _saveSystem.HandleApplicationPause(true, cts.Token),
                "AC-42: OperationCanceledException must not propagate");
            Assert.AreEqual(0, TotalWriteOps, "AC-42: no I/O when cancelled before lock");
        }

        // ── Async-void compile guard ──────────────────────────────────────────────

        [Test]
        public void Pause_OnApplicationPause_IsNotAsyncVoid()
        {
            var method = typeof(SS).GetMethod(
                "OnApplicationPause",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null);

            Assert.IsNotNull(method, "OnApplicationPause(bool) must exist");
            Assert.AreEqual(typeof(void), method.ReturnType,
                "OnApplicationPause must return void");
            Assert.AreEqual(0,
                method.GetCustomAttributes(typeof(AsyncStateMachineAttribute), false).Length,
                "FORBIDDEN: OnApplicationPause must NOT be async (ADR-0003)");
        }

        // ── Post-lock dirty check proxy ───────────────────────────────────────────

        [Test]
        public void Pause_PostLockDirtyCheck_SkipsWriteWhenDirtyFalse()
        {
            // dirty starts false; verifies the post-lock early exit releases the lock correctly
            // (if the semaphore were not released, the second Pause() call would deadlock).
            Pause();
            Assert.DoesNotThrow(() => Pause(), "Lock must be released even when dirty=false (post-lock check path)");
            Assert.AreEqual(0, TotalWriteOps, "No I/O on either call when dirty=false");
        }
    }
}
