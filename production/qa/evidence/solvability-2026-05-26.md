# BoltSort - Level Solvability Report
Generated: 2026-05-26 UTC
Solver: BFS (Python verification), state limit 500,000
Verification: all 30 levels tested against exact SortMechanic move rules

| ID | Name | Tier | Colors | Depth | Par | BFSMin | States | Status |
|----|------|------|--------|-------|-----|--------|--------|--------|
| 1 | Tutorial 1 | 1 | 2 | 3 | 5 | 6 | 11 | SOLVABLE |
| 2 | Level 2 | 1 | 2 | 3 | 6 | 3 | 4 | SOLVABLE |
| 3 | Level 3 | 1 | 2 | 3 | 6 | 3 | 4 | SOLVABLE |
| 4 | Level 4 | 1 | 2 | 3 | 7 | 6 | 11 | SOLVABLE |
| 5 | Level 5 | 1 | 2 | 3 | 8 | 5 | 8 | SOLVABLE |
| 6 | Level 6 | 2 | 3 | 4 | 10 | 16 | 49 | SOLVABLE |
| 7 | Level 7 | 2 | 3 | 4 | 11 | 16 | 49 | SOLVABLE |
| 8 | Level 8 | 2 | 3 | 4 | 12 | 16 | 49 | SOLVABLE |
| 9 | Level 9 | 2 | 3 | 4 | 12 | 16 | 49 | SOLVABLE |
| 10 | Level 10 | 2 | 3 | 4 | 13 | 16 | 49 | SOLVABLE |
| 11 | Level 11 | 2 | 3 | 4 | 14 | 16 | 49 | SOLVABLE |
| 12 | Level 12 | 2 | 3 | 4 | 13 | 16 | 49 | SOLVABLE |
| 13 | Level 13 | 2 | 3 | 4 | 14 | 16 | 49 | SOLVABLE |
| 14 | Level 14 | 2 | 3 | 4 | 15 | 16 | 49 | SOLVABLE |
| 15 | Level 15 | 2 | 3 | 4 | 15 | 16 | 49 | SOLVABLE |
| 16 | Level 16 | 3 | 4 | 4 | 18 | 15 | 7252 | SOLVABLE |
| 17 | Level 17 | 3 | 4 | 4 | 19 | 15 | 7252 | SOLVABLE |
| 18 | Level 18 | 3 | 4 | 4 | 18 | 15 | 7252 | SOLVABLE |
| 19 | Level 19 | 3 | 4 | 4 | 20 | 15 | 7252 | SOLVABLE |
| 20 | Level 20 | 3 | 4 | 4 | 20 | 15 | 7252 | SOLVABLE |
| 21 | Level 21 | 3 | 4 | 4 | 21 | 15 | 7252 | SOLVABLE |
| 22 | Level 22 | 3 | 4 | 4 | 21 | 15 | 7252 | SOLVABLE |
| 23 | Level 23 | 3 | 4 | 4 | 22 | 15 | 7252 | SOLVABLE |
| 24 | Level 24 | 3 | 4 | 4 | 22 | 15 | 7252 | SOLVABLE |
| 25 | Level 25 | 3 | 4 | 4 | 23 | 15 | 7252 | SOLVABLE |
| 26 | Level 26 | 4 | 5 | 5 | 26 | 28 | 130626 | SOLVABLE |
| 27 | Level 27 | 4 | 5 | 5 | 28 | 28 | 130626 | SOLVABLE |
| 28 | Level 28 | 4 | 5 | 5 | 28 | 28 | 130626 | SOLVABLE |
| 29 | Level 29 | 4 | 5 | 5 | 30 | 28 | 130626 | SOLVABLE |
| 30 | Level 30 | 4 | 5 | 5 | 30 | 28 | 130626 | SOLVABLE |

## Summary
- SOLVABLE: 30
- HARD (MinMoves > Par x2): 0
- UNSOLVABLE: 0
- TIMEOUT: 0
- Total: 30

## Verdict
All 30 levels are solvable. No data fixes required.

## Par Analysis
par_moves vs BFS optimum -- design calibration note (not a solvability issue):

| ID | Par | BFSMin | Delta | Note |
|----|-----|--------|-------|------|
| 1 | 5 | 6 | -1 | par below BFS min: 3-star unreachable |
| 2 | 6 | 3 | +3 | ok |
| 3 | 6 | 3 | +3 | ok |
| 4 | 7 | 6 | +1 | ok |
| 5 | 8 | 5 | +3 | ok |
| 6 | 10 | 16 | -6 | par below BFS min: 3-star unreachable |
| 7 | 11 | 16 | -5 | par below BFS min: 3-star unreachable |
| 8 | 12 | 16 | -4 | par below BFS min: 3-star unreachable |
| 9 | 12 | 16 | -4 | par below BFS min: 3-star unreachable |
| 10 | 13 | 16 | -3 | par below BFS min: 3-star unreachable |
| 11 | 14 | 16 | -2 | par below BFS min: 3-star unreachable |
| 12 | 13 | 16 | -3 | par below BFS min: 3-star unreachable |
| 13 | 14 | 16 | -2 | par below BFS min: 3-star unreachable |
| 14 | 15 | 16 | -1 | par below BFS min: 3-star unreachable |
| 15 | 15 | 16 | -1 | par below BFS min: 3-star unreachable |
| 16 | 18 | 15 | +3 | ok |
| 17 | 19 | 15 | +4 | ok |
| 18 | 18 | 15 | +3 | ok |
| 19 | 20 | 15 | +5 | ok |
| 20 | 20 | 15 | +5 | ok |
| 21 | 21 | 15 | +6 | ok |
| 22 | 21 | 15 | +6 | ok |
| 23 | 22 | 15 | +7 | ok |
| 24 | 22 | 15 | +7 | ok |
| 25 | 23 | 15 | +8 | ok |
| 26 | 26 | 28 | -2 | par below BFS min: 3-star unreachable |
| 27 | 28 | 28 | +0 | ok |
| 28 | 28 | 28 | +0 | ok |
| 29 | 30 | 28 | +2 | ok |
| 30 | 30 | 28 | +2 | ok |
