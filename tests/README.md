# BoltSort — Test Suite

## Framework
- **Runner**: NUnit via Unity Test Framework (built-in)
- **CI**: `game-ci/unity-test-runner@v4` on every push and PR
- **Modes**: EditMode (pure logic, no scene) and PlayMode (MonoBehaviour integration)

## Directory Structure

```
tests/
├── unit/                    # EditMode tests — logic, state machines, formulas
│   ├── sort-mechanic/       # SortMechanic FSM + move validation + win condition
│   ├── game-state-manager/  # GSM board mutation, undo, watchdog
│   ├── coin-economy/        # CE earn/spend rules, idempotency guard
│   └── save-persistence/    # SP atomic write, schema migration
│
├── integration/             # PlayMode tests — multi-system behaviour
│   └── gsm-sort-mechanic/   # GSM + SortMechanic end-to-end move cycles
│
└── helpers/                 # Shared fixtures and factory functions
    └── sort-mechanic-fixtures.cs   # Canonical deadlock fixture (AC-10, AC-22, AC-25)
```

## Naming Conventions

- **Files**: `[System]_[Feature]_Test.cs` (e.g., `SortMechanic_Fsm_Test.cs`)
- **Classes**: match file name
- **Methods**: `Test_[Scenario]_[ExpectedResult]` (e.g., `Test_EmptyDestination_LegalMove`)

## Running Tests Locally

In Unity Editor: **Window > General > Test Runner**
- EditMode tab → Run All (unit tests)
- PlayMode tab → Run All (integration tests)

## CI Gate

Tests run on every push to `main` and on every PR via `.github/workflows/tests.yml`.
A failing test blocks merge. Never skip or disable a test to make CI pass — fix the root cause.

## Required Tests (BLOCKING before story Done)

Per `coding-standards.md`:
- Sort Mechanic FSM state transitions
- GSM board mutations and bolt_count_invariant
- Win condition formula
- CE earn/spend rules
- SP atomic write

These tests must exist and pass before any Logic-type story is marked Done.
