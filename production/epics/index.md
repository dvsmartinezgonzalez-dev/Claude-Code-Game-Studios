# Epics Index

> Last Updated: 2026-05-14
> Engine: Unity 6.3 LTS
> Manifest Version: 2026-05-12

| Epic Slug | Layer | System | GDD | ADRs | Stories | Status |
|-----------|-------|--------|-----|------|---------|--------|
| [level-data-system](level-data-system/EPIC.md) | Foundation | Level Data System | design/gdd/level-data-system.md | ADR-0001, ADR-0004 | 6 stories (all Complete) | Complete |
| [save-persistence](save-persistence/EPIC.md) | Foundation | Save & Persistence | design/gdd/save-persistence.md | ADR-0001, ADR-0002, ADR-0003 | Not yet created | Ready |
| [audio-system](audio-system/EPIC.md) | Foundation | Audio System | design/gdd/audio-system.md | ADR-0001, ADR-0011 | Not yet created | Ready |
| [quality-tier-system](quality-tier-system/EPIC.md) | Foundation | Quality Tier System | design/gdd/quality-tier-system.md | ADR-0001, ADR-0005 | Not yet created | Ready |
| [game-state-manager](game-state-manager/EPIC.md) | Core | Game State Manager | design/gdd/game-state-manager.md | ADR-0001, ADR-0002, ADR-0006, ADR-0012 | Not yet created | Ready |
| [sort-mechanic](sort-mechanic/EPIC.md) | Feature | Sort Mechanic | design/gdd/sort-mechanic.md | ADR-0002, ADR-0006, ADR-0007, ADR-0013 | Not yet created | Ready (blocked on GSM) |

## Pending (no GDD yet — cannot create epic)

| System | Layer | Priority | Blocked On |
|--------|-------|----------|------------|
| Rewarded Ad System | Foundation | Beta | `/design-system rewarded-ad-system` first |
| IAP System | Foundation | Launch | `/design-system iap-system` first |

## Layers Not Yet Created

| Layer | Status |
|-------|--------|
| Core | Game State Manager epic created — run `/create-stories game-state-manager` |
| Feature | Sort Mechanic epic created — blocked on GSM implementation before stories can start |
| Presentation | Run `/create-epics layer:presentation` when Feature is nearly complete |
