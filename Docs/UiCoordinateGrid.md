# UI coordinate grid convention

The UI direction grid divides the screen into 12 square columns. Column labels increase from left
to right (`1`, `2`, ... `12`), and row labels increase from top to bottom (`A`, `B`, ... `Z`, `AA`,
...). The square size is derived from the current screen width, so the same coordinates remain
meaningful across aspect ratios while additional rows appear on taller screens.

## Direction format

- Position: write the top-left cell first, for example `B3`.
- Size: write width in columns and height in rows as `W x H`, for example `4 x 2`.
- Combined direction: `B3, 4 x 2` means start at row B / column 3 and occupy four columns by two
  rows. Its covered range is `B3:C6`.
- A partial-cell offset may be written as a fraction, for example `B3 + (0.5, 0.25)`.
- Unless explicitly stated otherwise, positions and sizes refer to the outer bounds of an element.

The grid is a runtime drawing overlay only. It does not create GameObjects, Transforms, UI
elements, textures, or scene data.
