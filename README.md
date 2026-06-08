# ProjectW

## Current authority

- Working title: **외행성재척지원실 3과**
- AI entry point: `Assets/Specification/Project_W – System Index (AI Entry Point).md`
- Current implementation overview: `Assets/Specification/Architecture – ProjectW System Overview.md`
- Build/push guide for AI sessions: `Assets/Specification/AI Build and Git Push Guide.md`

## SSOT implementation status

Update this section whenever an SSOT gains or loses an implementation.

| SSOT area | Current implementation status |
|-----------|-------------------------------|
| Ingame daily work loop | Partially implemented in `CaseReviewGame` command loop |
| Work data and dynamic generation | Initial `WorkDefinition` / `WorkGenerationSystem` implemented |
| Character base/runtime data | Initial ScriptableObject pipeline implemented |
| Character cards/perks/memory/relationships | Data and mutation interfaces partially implemented |
| Merit token approval flow | Partially implemented in `CaseReviewGame`, `CaseReviewRules`, and MVP character profiling UI for regeneration requests |
| Script Presentation scenario events | Data assets, localization interfaces, scenario data workshop, and CSV text import/export implemented; runtime playback UI not implemented |
| Long loop: weekly audit, monthly/quarterly evaluation, yearly settlement | SSOT only; no runtime system yet |
| Boss events and AI-baseline audit scoring | SSOT only; no runtime scoring system yet |
| Outgame systems | SSOT only; no Unity runtime module yet |
| Clone disposal/regeneration loop | Partially implemented through merit-token regeneration approval; no complete long-loop clone lifecycle yet |

README update rule:

- If a new SSOT section is added without implementation, add it to the table as `SSOT only`.
- If code implements an SSOT section, update the status in the same commit.
- If an implementation is partial, name the concrete implemented types and the missing runtime surface.
- Keep every `SSOT only` or partially implemented item visible here so another AI session can identify docs-without-code immediately from README.
- After changing `Assets/Specification`, run `python tools\sync_architecture_doc.py` and include the updated Architecture document.

## Visual pipeline (current)

- **Characters**: Unity `Animator` based pipeline (`RoutineCharacterAnimatorDriver` bridge).
- **Objects/Zones**: Sprite animation pipeline (`RoutineObjectSpriteAnimationPlayer`) for low-cost looping frames.
- Side view uses left/right flip via transform X sign.

### Character Animator setup (next step ready)

1. In Unity menu, run:
   - `ProjectW/Animation/Create Default Character Animator Controller`
2. This generates:
   - `Assets/Resources/AnimatorControllers/routine_character_default.controller`
   - Placeholder loop clips under `Assets/Resources/AnimatorControllers/Clips/`
3. `RoutineObservationMvpSession` auto-loads this controller from Resources when no controller is assigned.

Animator parameters expected by runtime bridge:
- `IsMoving` (bool)
- `Speed` (float)
- `CurrentAction` (int)
- `IntendedAction` (int)
- `FacingX` (float)

## Placeholder visual resources

You can replace object visuals by editing files in:

- `Assets/Resources/PlaceholderSprites/`

Current dummy white PNG files (editable in-place):

- Characters: `character_a.png`, `character_b.png`, `character_c.png`
- Zones: `zone_mission.png`, `zone_cafeteria.png`, `zone_sleep.png`
- Item tags: `item_desk.png`, `item_computer.png`, `item_bed.png`, `item_pillow.png`, `item_blanket.png`, `item_table.png`, `item_tray.png`, `item_cup.png`

`RoutineObservationMvpSession` auto-loads these sprites (runtime square fallback if missing).


## Runtime crash/error console (build)

- A runtime log overlay is auto-created by `RuntimeErrorConsole` in builds and editor play mode.
- Toggle overlay with **` (BackQuote)** or **F1**.
- It captures `Debug.Log*`, warnings, exceptions, and unhandled exceptions.
- Logs are also saved to `Application.persistentDataPath/runtime-log-YYYYMMDD-HHMMSS.txt`.

## GitHub PR flow

- PRs with base branch `ai-integration` automatically get `auto-merge` enabled via `.github/workflows/auto-merge-ai-integration.yml`.
- Merge method is `squash`.
- Keep `main` updates manual by merging from `ai-integration` when ready.

## Recommended branch strategy

If your repository currently only has `main`, create `ai-integration` first and use it as the default PR base for AI-generated changes.

### 1) Create `ai-integration`

```bash
git checkout main
git pull origin main
git checkout -b ai-integration
git push -u origin ai-integration
```

### 2) Protect branches (GitHub Settings)

- `main` (strict):
  - Require pull request before merging
  - Require approvals (at least 1)
  - Optionally restrict who can push
  - Optionally include administrators
- `ai-integration` (operational):
  - Keep required checks aligned with your CI
  - Allow auto-merge to complete after checks pass

This keeps `main` hard to modify by mistake while still allowing fast AI iteration on `ai-integration`.

### 3) Open PRs with base=`ai-integration`

The workflow `.github/workflows/auto-merge-ai-integration.yml` triggers only when the PR base branch is `ai-integration`.
