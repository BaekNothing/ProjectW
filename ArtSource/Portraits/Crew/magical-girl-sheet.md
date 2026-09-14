# Magical-girl crew sheet

- User direction: all characters female magical girls, cut multiple sprites from one large generated image; retain white background and black outlines, increase moe appeal.
- Generator: built-in OpenAI image generation; one call, one 4x4 sheet, no external references.
- Source: magical-girl-sheet.png, unmodified 1254 x 1254 output. Requested 4096 square was not honored; no upscaling is used.
- Columns: Han (star/short bob), Yoon (crescent/straight hair), Mi (twin buns/ribbons), Kang (side ponytail/wing clip).
- Rows: healthy, fatigue, overwork, injured/exhausted. Four adult women; modest sailor magical-girl costumes.
- Export: tools/export_crew_sheet.py, exact quarter-cell boundaries recorded with hashes in sheet-export.json. Downscale cells to fit 288 square and place on a 320 square white canvas with shared top/side margins.
- Review: all 16 cells reviewed on the full sheet; four healthy exports inspected individually. No neighboring character intrusions, eye obstruction, labels or grid lines. Expressions change by row; costumes/hairstyles remain identifiable. Minor hand-drawn pose differences are intentional generator variation. Fine grayscale pixels are antialiasing; no colored fills.
- Runtime: 16 complete condition textures, plus four stable healthy fallback files. Historical modular art no longer ships. Exact Texture2D Addressables/GUI overloads are reused; no new AOT calls in runtime diff.
- Delivery: resources must be configured into the portrait Addressables group before WebGL build and Butler publication.

## Generation prompt

One large square production portrait sheet, exact four columns and four rows, 16 equal cells,
white backgrounds without lines or labels. Cute moe anime magical girls, all young adult women,
white fills and clean black contours, expressive large eyes, small mouths and rounded cheeks.
Modest sailor dresses, bows and star brooches. Four consistent identities across columns: confident
short-bob technician with star hairpin; calm straight-haired analyst with crescent clip; friendly
twin-bun manager with ribbons; resilient side-ponytail adventurer with wing hairclip. Repeat the
same identity/costume down each column. Rows healthy, mildly tired, overworked, exhausted/injured
with cheek bandage. Frontal centered head-and-shoulders, consistent scale, entire crown visible,
safe top margin, unobstructed eyes/brows, natural eye spacing. No color, gray shading, weapons,
props, text, watermarks, borders or decorations outside the character. Cut at quarter boundaries.
