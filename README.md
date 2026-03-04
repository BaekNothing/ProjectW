# ProjectW

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
