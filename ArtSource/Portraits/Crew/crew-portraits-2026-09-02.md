# Crew Portrait Generation Record — 2026-09-02

## Status

- Stage: Runtime-approved replacement authoring masters
- Generator: OpenAI built-in image generation
- Generation date: 2026-09-02
- Selected master format: 1254 x 1254 square PNG, sRGB
- Runtime export: 512 x 512 square PNG `Texture2D` assets in `ProjectW Remote Portraits`
- Approval: User explicitly approved overwriting the four stable-ID portraits and deployment

## Asset Mapping

| Stable ID | Display data | Role accent |
|---|---|---|
| `crew-han-tech` | `한기술관` | muted orange |
| `crew-yoon-analysis` | `윤분석관` | muted blue |
| `crew-mi-management` | `미관리자` | muted olive-gold |
| `crew-kang-adaptation` | `강적응관` | muted brick red |

## Shared Style Prompt

```text
Create one original contemporary 2D moe game crew portrait using the supplied crew image only for
identity, age, hair, expression, uniform, and role accent. Match the approved cast master's strong
deformation: noticeably oversized head relative to shoulders, broad rounded cranium, very short
mid-face, full rounded cheeks, compact U-shaped lower face and short rounded chin. Use horizontally
elongated almond-shaped eyes with a large eye-width-to-face ratio and reduced vertical opening.
Preserve adult role readability through brows, expression, hair, selective age lines, and clothing
rather than realistic long or angular facial proportions. Use clean tapered dark lines, crisp
two-step cel shading, slightly bright clear warm East Asian skin with intact shadow depth, subtle
warm face light, cool rim light, a pale cool gray-blue background, and centered straight-on
head-and-shoulders framing. No text, logo, watermark, extra person, named-franchise character,
photorealism, plastic 3D rendering, or chibi body.
```

## Subject Constraints

- `crew-han-tech`: retain a middle-aged Korean man, gray temples, heavy brows, calm authority,
  restrained age lines, broad neck, and orange technical trim.
- `crew-yoon-analysis`: retain an adult Korean woman, chin-length asymmetrical black bob, calm
  analytical expression, warm-brown eyes, and blue analysis trim.
- `crew-mi-management`: retain an adult Korean woman, neat low updo with side tendrils, warm smile,
  and olive-gold management trim.
- `crew-kang-adaptation`: retain a young adult Korean man, short tousled black hair, thick brows,
  serious resilient expression, and brick-red adaptation trim.

## Reference Provenance

The user supplied one portrait image in the Codex conversation as a visual reference for the degree
of rounded moe deformation and horizontal eye proportions. It was used only as a non-committed
style reference. The reference file is not copied into `ArtSource`, Unity assets, or the release.
The four pre-existing ProjectW crew portraits were the identity and costume inputs.

## Review Notes

The selected set preserves the four stable identities and role colors while replacing the previous
restrained adult editorial-cartoon proportions with one consistent strong moe deformation baseline.
All four images are square, contain one centered subject, keep the face within the central safe area,
use the shared cool institutional background, and contain no text, logo, watermark, weapon, or
reference-character residue. Runtime exports retain the existing stable file names, Unity metadata,
Addressables addresses, and approved `Texture2D` load path.
