# Crew portrait alignment review — 2026-09-13

Spec Ref: `Assets/Specification/Art/ResourceAssetPolicy.md` (shared square registration,
safe margins, eye spacing) and `Assets/Specification/System/TaskSystem.md` (Crew Detail UI).

Inspected all four complete authoring portraits and registered modular layers. The current
composites do not meet the intended consistent framing and readable facial-feature baseline:

- Han: tousled bangs overlap the left eye/brow; crown has only 8 px of clearance.
- Yoon: swept fringe obscures the left eye/brow; face-base crown is visible above the hair.
- Mi: crown is clipped at y=0 and side locks intersect the facial features.
- Kang: crown is clipped at y=0 and hair crowds both outer eye regions.
- All eye pairs span x=328..927; the inner gap is wider than one eye. Brows and condition
  overlays must move with the eyes when correcting the registration.

Coordinates refer to the 1254-square authoring canvas. Clipped hair contours cannot be
recovered merely by translating the existing pixels. Asset correction remains pending the
choice between deterministic part adjustment and generated redraw; no raster assets changed.

## Runtime correction

The worker slot previously stretched every square texture to its non-square rectangle.
`CrewPortraitCatalog.FitPortraitRect` now centers a contained square before either modular
layers or the complete fallback is drawn. Existing detail portraits retain their square size.
Tests cover tall, wide, square and zero-width areas at a nonzero origin.

The runtime diff uses only existing Rect construction/fields and Math Min/Max operations.
It preserves the exact existing GUI.DrawTexture overload and Texture2D Addressables path.
No APK, content release or device-channel change is included.
