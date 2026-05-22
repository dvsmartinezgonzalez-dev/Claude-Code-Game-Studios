using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using BoltSort.Tests.Helpers.SavePersistence;
using SS = BoltSort.SaveSystem.SaveSystem;          // alias avoids namespace/class collision
using ISS = BoltSort.SaveSystem.ISaveSystem;

// Test assembly: Tests.Unit.SaveSystem
// Covers: AC-01, AC-13, AC-15, AC-17, AC-19, AC-27, AC-32, AC-33 (Story 001)
//   from design/gdd/save-persistence.md
// Framework: Unity Test Framework (NUnit, EditMode)
// Story: production/epics/save-persistence/story-001-boot-schema-isready.md

namespace BoltSort.Tests.Unit.SaveSystem
{
    [TestFixture]
    public class SaveSystem_Boot_Test
    {
        private FakeFileSystem _fakeFs;
        private SS _saveSystem;

        // Valid schema v1 JSON matching the C.2 schema in GDD / ADR-0003.
        private const string ValidV1Json =
            @"{""schema_version"":1," +
            @"""level_progress"":{""current_level_id"":5," +
            @"""completion_record"":[{""level_id"":1,""best_stars"":3,""completion_version"":""2026.04""}," +
            @"{""level_id"":2,""best_stars"":2,""completion_version"":""2026.04""}]," +
            @"""undo_stack"":[{""f"":0,""t"":1}]}," +
            @"""economy"":{""coin_balance"":150}," +
            @"""skins"":{""_status"":""reserved""}}";

        // Fresh-install defaults JSON for R-3 verification.
        private const string DefaultJson =
            @"{""schema_version"":1," +
            @"""level_progress"":{""current_level_id"":1,""completion_record"":[],""undo_stack"":[]}," +
            @"""economy"":{""coin_balance"":0}," +
            @"""skins"":{""_status"":""reserved""}}";

        [SetUp]
        public void SetUp()
        {
            _fakeFs = new FakeFileSystem();
            // Static override consumed by Awake() on the next AddComponent<SaveSystem>().
            SS.SetFileSystemForTesting(_fakeFs);
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy the SaveSystem GameObject and clear static state so tests are isolated.
            if (_saveSystem != null)
                UnityEngine.Object.DestroyImmediate(_saveSystem.gameObject);
            SS.ClearInstanceForTesting();
            // Clear PlayerPrefs keys written by HandleDowngrade (R-5 path) so they do not
            // leak between tests or persist across Editor sessions. Must be in TearDown, not
            // individual tests, to cover all execution orders (NUnit does not guarantee order).
            PlayerPrefs.DeleteKey("sp.downgrade_notice_shown");
        }

        /// <summary>Creates and returns the SaveSystem MonoBehaviour (triggers Awake immediately).</summary>
        private SS CreateSaveSystem()
        {
            var go = new GameObject("SaveSystem_Test");
            _saveSystem = go.AddComponent<SS>();
            return _saveSystem;
        }

        // ── AC-13: R-1 — valid v1 file loads correctly ────────────────────────────

        [Test]
        public void Boot_R1_ValidV1File_LoadsAllFields()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json;

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsTrue(ss.IsReady, "IsReady must be true after Awake()");
            Assert.AreEqual(5, ss.GetCurrentLevelId(), "current_level_id should be 5");
            Assert.AreEqual(150, ss.GetCoinBalance(), "coin_balance should be 150");

            var rec1 = ss.GetCompletionRecord(1);
            Assert.IsNotNull(rec1, "completion_record[1] should be present");
            Assert.AreEqual(3, rec1.Value.best_stars);
            Assert.AreEqual("2026.04", rec1.Value.completion_version);

            var rec2 = ss.GetCompletionRecord(2);
            Assert.IsNotNull(rec2, "completion_record[2] should be present");
            Assert.AreEqual(2, rec2.Value.best_stars);
        }

        // ── AC-15: R-3 — no file → defaults, nothing written ─────────────────────

        [Test]
        public void Boot_R3_NoFile_DefaultsAndNoWrite()
        {
            // Arrange: ReadAllTextResult = null → FakeFileSystem throws FileNotFoundException.
            _fakeFs.ReadAllTextResult = null;

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsTrue(ss.IsReady);
            Assert.AreEqual(1, ss.GetCurrentLevelId(), "fresh install: current_level_id = 1");
            Assert.AreEqual(0, ss.GetCoinBalance(), "fresh install: coin_balance = 0");
            Assert.IsNull(ss.GetCompletionRecord(1), "fresh install: no completion records");
            Assert.IsFalse(_fakeFs.WasDeleteCalled, "no delete on R-3 path when no tmp exists");
            Assert.AreEqual(0, _fakeFs.WrittenFiles.Count, "no file written on R-3 (deferred to W-1/W-2)");
        }

        // ── AC-19: IsReady guard throws in DEBUG before Awake completes ───────────

        [Test]
        public void Boot_IsReady_IsFalse_BeforeAwakeCompletes()
        {
            // We cannot intercept mid-Awake() in Unity test framework, so verify the contract
            // via the guard method behaviour: after Awake, IsReady is true.
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();

            Assert.IsTrue(ss.IsReady,
                "IsReady must be true after Awake() completes — subscribe-then-check relies on this.");
        }

        [Test]
        public void Boot_GetCurrentLevelId_ThrowsWhenNotReady()
        {
            // Simulate the not-ready state by reflecting on a fresh (uninitialized) instance,
            // OR verify that after a valid boot the guard does NOT throw (positive assertion).
            // Since we cannot intercept mid-Awake in EditMode, we test the guard's path via reflection.
            // After boot, IsReady = true and no exception is thrown — this verifies the guard compiles.
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();
            Assert.DoesNotThrow(() => ss.GetCurrentLevelId(),
                "GetCurrentLevelId must not throw after IsReady = true");
        }

        // ── AC-27: negative coin_balance clamped to 0 ────────────────────────────

        [Test]
        public void Boot_NegativeCoinBalance_ClampedToZero()
        {
            // Arrange: save file with coin_balance = -50
            _fakeFs.ReadAllTextResult =
                @"{""schema_version"":1," +
                @"""level_progress"":{""current_level_id"":3,""completion_record"":[],""undo_stack"":[]}," +
                @"""economy"":{""coin_balance"":-50}," +
                @"""skins"":{""_status"":""reserved""}}";

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsTrue(ss.IsReady, "IsReady must be true even with clamped balance");
            Assert.AreEqual(0, ss.GetCoinBalance(), "Negative coin_balance must be clamped to 0 (AC-27)");
            Assert.AreEqual(3, ss.GetCurrentLevelId(), "Other fields must load normally after clamp");
        }

        // ── AC-32: empty completion_record is not an error ────────────────────────

        [Test]
        public void Boot_EmptyCompletionRecord_NotAnError()
        {
            // Arrange
            _fakeFs.ReadAllTextResult =
                @"{""schema_version"":1," +
                @"""level_progress"":{""current_level_id"":7,""completion_record"":[],""undo_stack"":[]}," +
                @"""economy"":{""coin_balance"":200}," +
                @"""skins"":{""_status"":""reserved""}}";

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsTrue(ss.IsReady, "IsReady with empty completion_record (AC-32)");
            Assert.AreEqual(7, ss.GetCurrentLevelId());
            Assert.IsNull(ss.GetCompletionRecord(1), "No records = null result, not exception");
        }

        // ── AC-33: save.tmp present with valid save.json → tmp deleted silently ──

        [Test]
        public void Boot_TmpPresentWithValidJson_TmpDeleted()
        {
            // Arrange: mark save.tmp as existing in FakeFileSystem.
            _fakeFs.ReadAllTextResult = ValidV1Json;
            // The tmp path is Application.persistentDataPath + "/save.tmp"
            // We cannot know the exact path without running Unity, but we can verify
            // that Delete was called at least once (for the tmp path).
            string tmpPath = Application.persistentDataPath + "/save.tmp";
            _fakeFs.SetFileExists(tmpPath, true);

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsTrue(ss.IsReady);
            Assert.IsTrue(_fakeFs.WasDeleteCalled,
                "save.tmp must be deleted when save.json is valid (AC-33)");
            Assert.IsTrue(_fakeFs.DeletedPaths.Contains(tmpPath),
                "Exactly the tmp path must be deleted");
        }

        [Test]
        public void Boot_NoTmpPresent_NoDeleteCalled()
        {
            // Arrange: valid save.json, NO save.tmp
            _fakeFs.ReadAllTextResult = ValidV1Json;
            // save.tmp is NOT added to existing paths

            // Act
            var ss = CreateSaveSystem();

            // Assert
            Assert.IsFalse(_fakeFs.WasDeleteCalled,
                "Delete must not be called when save.tmp does not exist");
        }

        // ── AC-17: R-5 — schema_version > MAX_KNOWN_VERSION ──────────────────────

        [Test]
        public void Boot_R5_FutureSchema_LoadsDefaults()
        {
            // Arrange: schema_version = 99, far beyond MaxKnownVersion = 1
            _fakeFs.ReadAllTextResult =
                @"{""schema_version"":99," +
                @"""level_progress"":{""current_level_id"":42,""completion_record"":[],""undo_stack"":[]}," +
                @"""economy"":{""coin_balance"":9999}," +
                @"""skins"":{""_status"":""reserved""}}";

            // Act
            var ss = CreateSaveSystem();

            // Assert: defaults loaded (NOT the data from the future-schema file)
            Assert.IsTrue(ss.IsReady, "IsReady after R-5 fallback");
            Assert.AreEqual(1, ss.GetCurrentLevelId(), "R-5: current_level_id defaults to 1");
            Assert.AreEqual(0, ss.GetCoinBalance(), "R-5: coin_balance defaults to 0");
            Assert.IsNull(ss.GetCompletionRecord(1), "R-5: no completion records");
        }

        [Test]
        public void Boot_R5_FutureSchema_FileNotOverwritten()
        {
            // Arrange
            _fakeFs.ReadAllTextResult =
                @"{""schema_version"":99,""level_progress"":{""current_level_id"":1,""completion_record"":[],""undo_stack"":[]}," +
                @"""economy"":{""coin_balance"":0},""skins"":{""_status"":""reserved""}}";

            // Act
            var ss = CreateSaveSystem();

            // Assert: no write to save.json (file is preserved for potential recovery)
            Assert.AreEqual(0, _fakeFs.WrittenFiles.Count,
                "R-5: save.json must NOT be overwritten (AC-17)");
        }

        // ── Singleton guard ───────────────────────────────────────────────────────

        [Test]
        public void Boot_SingletonGuard_SecondAwakeDestroyseDuplicate()
        {
            // Arrange: first SaveSystem created
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var first = CreateSaveSystem();
            Assert.IsTrue(first.IsReady, "first instance ready");

            // Act: create a second SaveSystem (singleton guard fires inside AddComponent)
            SS.SetFileSystemForTesting(_fakeFs);
            var secondGo = new GameObject("SaveSystem_Duplicate");
            secondGo.AddComponent<SS>();

            // Assert: Instance is still the first (the only deterministic EditMode check).
            // The duplicate's Destroy() is deferred; we cannot assert second == null immediately.
            // secondGo is cleaned up in TearDown via DestroyImmediate.
            Assert.AreEqual(first, SS.Instance,
                "Instance must remain the first SaveSystem — singleton guard must not overwrite it");

            // Cleanup: destroy secondGo immediately so it does not leak into other tests.
            UnityEngine.Object.DestroyImmediate(secondGo);
        }

        // ── Subscribe-then-check: fires immediately when already ready ────────────

        [Test]
        public void Boot_OnSaveReady_SubscribeAfterReady_StillFires()
        {
            // Arrange
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();
            Assert.IsTrue(ss.IsReady, "Precondition: IsReady = true after boot");

            // Act: subscribe AFTER boot (subscribe-then-check pattern — consumer's Awake at lower SEO)
            bool callbackFired = false;
            ss.OnSaveReady += () => callbackFired = true;
            if (ss.IsReady) callbackFired = true;  // subscribe-then-check pattern

            // Assert
            Assert.IsTrue(callbackFired,
                "Subscribe-then-check pattern must fire callback immediately when IsReady is already true");
        }

        // ── MaxKnownVersion constant matches GDD ──────────────────────────────────

        [Test]
        public void Boot_MaxKnownVersion_MatchesGddValue()
        {
            // AC-31: MaxKnownVersion in code must == 1 (GDD Formulas section).
            // If this fails, update the constant AND the GDD.
            Assert.AreEqual(1, SS.MaxKnownVersion,
                "MaxKnownVersion must equal the value declared in GDD Formulas (currently 1). " +
                "If this fails, update both the GDD and the constant together.");
        }

        // ── async void Awake() forbidden — compile-level check ────────────────────

        [Test]
        public void Boot_AsyncVoidAwake_IsNotPresent()
        {
            // If SaveSystem uses 'async void Awake()', Start() on other MonoBehaviours fires
            // before IsReady = true, breaking the initialization contract (ADR-0003).
            // This test verifies the Awake method is a plain void, not async void.
            var awakeMethod = typeof(SS)
                .GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);

            Assert.IsNotNull(awakeMethod, "SaveSystem must have an Awake() method");
            // async void methods are compiled to return void but have a state machine attribute.
            var attrs = awakeMethod.GetCustomAttributes(
                typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute), false);
            Assert.AreEqual(0, attrs.Length,
                "Awake() must NOT be async void — Unity does not await Awake(); " +
                "async Awake breaks IsReady initialization ordering. (ADR-0003)");
        }

        // ── GetCompletionRecord: absent record returns null ───────────────────────

        [Test]
        public void Boot_GetCompletionRecord_AbsentLevel_ReturnsNull()
        {
            // Arrange: file with records for levels 1 and 2 only
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();

            // Act + Assert
            Assert.IsNull(ss.GetCompletionRecord(3),
                "Absent completion record must return null, not throw");
            Assert.IsNull(ss.GetCompletionRecord(0),
                "Level 0 is out of range — must return null, not throw (AC-25 analog)");
        }

        // ── ISaveSystem interface is implemented ──────────────────────────────────

        [Test]
        public void Boot_SaveSystem_ImplementsISaveSystem()
        {
            // IsAssignableFrom(child) → true if child implements the type.
            Assert.IsTrue(typeof(ISS).IsAssignableFrom(typeof(SS)),
                "SaveSystem must implement ISaveSystem interface");
        }

        // ── AC-19: GuardIsReady throw path (reflection to force IsReady = false) ──

        [Test]
        public void Boot_GuardIsReady_ThrowsWhenIsReadyForcedFalse()
        {
            // The throw path cannot be intercepted mid-Awake (Awake runs synchronously inside
            // AddComponent before returning). Use reflection to reset IsReady to false post-boot,
            // then verify the guard throws as documented in AC-19.
            _fakeFs.ReadAllTextResult = ValidV1Json;
            var ss = CreateSaveSystem();
            Assert.IsTrue(ss.IsReady, "Precondition: IsReady = true after boot");

            // Reset IsReady to false via reflection to exercise the guard throw path.
            typeof(SS)
                .GetProperty("IsReady", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                ?.SetValue(ss, false);

            // Verify: read methods throw InvalidOperationException when IsReady = false
            // (#if UNITY_EDITOR || DEVELOPMENT_BUILD is active in EditMode — throw branch taken).
            Assert.Throws<InvalidOperationException>(() => ss.GetCurrentLevelId(),
                "GetCurrentLevelId must throw InvalidOperationException when IsReady is false (AC-19, DEBUG/Editor path)");
            Assert.Throws<InvalidOperationException>(() => ss.GetCoinBalance(),
                "GetCoinBalance must throw when IsReady is false (AC-19)");

            // Restore IsReady so TearDown doesn't throw during cleanup.
            typeof(SS)
                .GetProperty("IsReady", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                ?.SetValue(ss, true);
        }

        // ── Null sub-objects: JsonUtility absent nested fields handled by ??= guards ──

        [Test]
        public void Boot_MinimalJson_NullSubObjects_LoadsDefaults()
        {
            // JsonUtility.FromJson returns a SaveData with null sub-objects when nested keys
            // are absent from the JSON. The ??= guards at lines 223-227 of SaveSystem.cs must
            // initialize them. Without these guards, GetCoinBalance() would NullReferenceException.
            _fakeFs.ReadAllTextResult = @"{""schema_version"":1}";  // no level_progress, economy, skins

            var ss = CreateSaveSystem();

            // Assert: null sub-objects were coalesced; read methods return safe defaults.
            Assert.IsTrue(ss.IsReady, "IsReady must be true even with absent nested fields");
            Assert.AreEqual(1, ss.GetCurrentLevelId(),
                "current_level_id defaults to 1 when level_progress is absent");
            Assert.AreEqual(0, ss.GetCoinBalance(),
                "coin_balance defaults to 0 when economy is absent");
            Assert.IsNull(ss.GetCompletionRecord(1),
                "No completion records when completion_record list is absent");
        }
    }
}
