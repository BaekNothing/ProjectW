# ProjectW

Working title: **외행성재척지원실 3과**

ProjectW is a Unity 6 operations-management prototype. The player assigns a four-person field team
to interdependent Works and Tasks, manages fatigue and resource pressure, and survives an endless
day-based run. There is no victory state or fixed campaign ending; resource depletion ends the run
and the reached day is the score.

## Current authority

Use documents in this order:

1. `Assets/Specification/System/TaskSystem.md` — gameplay and UI SSOT
2. `Assets/Specification/DataAuthoring/` — simulation and content-authoring SSOT
3. `Assets/Specification/Operation/HotUpdateAotSafety.md` — mandatory patch safety boundary
4. `Docs/GameCompletionMap.md` — implementation/completion snapshot
5. Runtime code and Git history

`task-system.json` is the production gameplay-data source. The Markdown authoring template is a
draft/interchange aid and is not loaded by the game.

## Current implementation

| Area | Status |
|---|---|
| Day loop | Implemented: Monday–Sunday calendar, weekend rest, Friday checkups, payroll, endless survival |
| Work and Task | Implemented: hierarchy, prerequisites, deadlines, priorities, focus flags, assignments, reservations, Gantt |
| People | Implemented core: competencies, fatigue, injury, medical files, experience, salary, regeneration, trust display |
| Outcomes | Implemented: fatigue-driven failure/success/great-success, accidents, rewards and resource penalties |
| Mail and events | Implemented: critical chains, PM proposals, boss requests, Monday field-status digest, medical results |
| Automation | Implemented: learned assignment and competency assignment with reservation/priority constraints |
| Content | 20 authored Works, 39 Tasks, 20 authored mail entries, 7 critical-event chains, generated missions |
| Save | Implemented: campaign schema 2 and separate desktop schema 1, with selected legacy backfills |
| Data operations | Implemented: local JSON category editing, validation, working-copy reload, clipboard export |
| Patch delivery | Implemented: HybridCLR download, SHA-256/size verification, promotion and rollback |
| Presentation depth | Partial: functional IMGUI desktop and placeholders; final art/audio and deeper feedback are pending |
| Long-run validation | Partial: automated tests and endurance tooling exist; full on-device long-run QA is pending |

The obsolete `CaseReviewGame` and `RoutineObservationMvpSession` prototypes were removed on
2026-07-20. `Assets/MilestonePrototype` is the only current gameplay implementation.

## Runtime and deployment boundary

- Fixed in base APK: `ProjectW.Bootstrap`, `ProjectW.Contracts`, Unity/package/native integration,
  PlayerSettings, and HybridCLR configuration.
- Patchable: `ProjectW.HotUpdate`, currently rooted at `Assets/MilestonePrototype/Runtime`, plus
  manifest-listed gameplay data.
- Current required base: **base APK v9**.
- Active channel:
  `https://raw.githubusercontent.com/BaekNothing/ProjectW/ai-integration/PatchChannels/dev.json`
- Remote Addressables content delivery is not implemented. New scenes, prefabs, textures, and audio
  still require a base APK unless a future reviewed content pipeline changes that boundary.

See `Docs/HotUpdatePoC.md` for build and release operations. Never publish a HotUpdate patch without
the AOT preflight in `Assets/Specification/Operation/HotUpdateAotSafety.md`.

## Development baseline

- Unity: `6000.3.8f1`
- Persistent development/deployment branch: `ai-integration`
- Base APK artifact: `APK/ProjectW-HybridCLR.apk`
- Gameplay data: `Assets/MilestonePrototype/Resources/task-system.json`
- Main EditMode suite: `Assets/MilestonePrototype/Tests/EditMode/MilestoneSimulationTests.cs`

Read `AGENTS.md` before changing code, data, Unity settings, builds, patches, or releases.
