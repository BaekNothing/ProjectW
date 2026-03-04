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
