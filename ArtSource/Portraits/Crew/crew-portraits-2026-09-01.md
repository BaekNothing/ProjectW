# Crew Portrait Generation Record — 2026-09-01

## Status

- Stage: Runtime-approved authoring masters
- Generator: OpenAI built-in image generation
- Selected master format: 1254 x 1254 square PNG, sRGB
- Runtime promotion: Approved as 512px-max imported `Texture2D` assets in the remote portrait group

## Asset Mapping

| Stable ID | Display data | Presentation |
|---|---|---|
| `crew-han-tech` | `한기술관` | man, late 40s, senior technical officer |
| `crew-yoon-analysis` | `윤분석관` | woman, early 30s, analysis officer |
| `crew-mi-management` | `미관리자` | woman, late 30s, operations manager |
| `crew-kang-adaptation` | `강적응관` | man, early 30s, adaptation officer |

## Shared Prompt

```text
Use case: stylized-concept
Asset type: square game UI crew portrait
Scene/backdrop: one pale cool gray-blue institutional background, identical across the crew
Style/medium: original simplified American adult TV cartoon character design for a workplace
strategy game; clean 2D vector-like drawing, bold rounded dark outlines, broad geometric face shapes,
lightly exaggerated individual silhouettes, minimal facial lines, solid flat colors, only one simple
cel-shadow shape and at most 3 tones per material, crisp readable shapes at 64 pixels; do not imitate
any existing show or artist
Composition/framing: centered straight-on head-and-shoulders employee ID portrait, eye-level, both
shoulders visible, face fully visible, square canvas, consistent head scale and safe margins
Lighting/mood: neutral institutional badge portrait translated into simple flat cartoon shapes
Constraints: fictional adult; exactly one person; no text; no letters; no name tag; no physical badge
card; no logos; no symbols; no watermark; no eyewear; no hat; plain background
Avoid: photorealism, semi-realistic anatomy, graphic-novel rendering, detailed skin texture,
detailed hair strands, gradients, airbrush shading, painterly texture, anime or manga, chibi, 3D,
glamour pose, military styling, weapons, busy background, sci-fi armor
```

## Subject Prompts

- `crew-han-tech`: fictional Korean male senior technical officer in his late 40s; principled,
  precise, safety-first, quietly authoritative; short neat black hair with subtle gray temples,
  mature angular face, steady neutral expression; slate-gray operations jacket with muted orange
  technical trim.
- `crew-yoon-analysis`: fictional Korean female analysis officer in her early 30s; analytical,
  observant, composed; neat chin-length dark bob, focused almond-shaped eyes; slate-gray operations
  jacket with muted blue analytical trim.
- `crew-mi-management`: fictional Korean female operations manager in her late 30s; warm,
  dependable, empathetic; dark hair neatly tied back, gently rounded face, approachable closed-mouth
  half-smile; slate-gray operations jacket with muted olive-gold management trim.
- `crew-kang-adaptation`: fictional Korean male adaptation officer in his early 30s; bold, decisive,
  resilient; short practical dark hair slightly tousled, athletic angular face, confident neutral
  expression; slate-gray operations jacket with muted brick-red adaptation trim.

## Review Notes

Yoon's initial output used a transparent background and a more anime-like facial proportion. The
selected Yoon master is an identity-preserving edit using the other three selected portraits as
style references; it restores the shared background and set proportions while retaining her bob,
analytical expression, clothing family, and blue accent.

The four selected authoring masters passed a full-size visual check for one subject, square crop,
role accent, shared clothing family, and absence of accidental text, logos, or watermarks. Check all
four together at 512, 128, and 64 pixels before runtime promotion.
