# Crew Portrait Generation Record — 2026-09-02

## Status

- Stage: Runtime-approved replacement authoring masters
- Generator: OpenAI built-in image generation
- Generation date: 2026-09-02
- Selected master format: 1254 x 1254 square PNG, sRGB
- Runtime export: 512 x 512 square PNG `Texture2D` assets in `ProjectW Remote Portraits`
- Approval: User explicitly approved overwriting the four stable-ID portraits and deployment

## Modular Revision

- Revision: shared-face modular portrait system
- Authoring canvas: registered `1254 x 1254` PNG layers
- Runtime canvas: registered `512 x 512` PNG `Texture2D` layers
- Composition order: body/background, shared face and nose, dark circles, eyes, eyebrows, mouth, hair
- Variants: four each for eyes, eyebrows, mouth, and hair; four dark-circle states including none
- Crop change: apparent face area enlarged by approximately 25 percent from the prior set
- Age direction: no wrinkle-driven age enforcement; expression and role are carried by modular parts
- Skin direction: slightly brighter warm skin while retaining shadow separation

### Modular generation prompts

The supplied conversation image was used as the visual baseline. The canonical prompt requested an
original contemporary Japanese-inspired moe mobile-game crew portrait with a rounder face,
horizontally longer eyes, a short mid-face, slightly brighter warm skin, clean cel shading, and a
face occupying approximately 25 percent more of the square image. It explicitly excluded text,
logos, watermarks, named-franchise characters, photorealism, age-line emphasis, and extra people.

Each part prompt retained the canonical `1254 x 1254` registration and requested only its isolated
component on transparency:

- common face base with ears, neck, and nose, but no eyes, eyebrows, mouth, hair, wrinkles, or dark circles;
- four eyebrows: straight analytical, soft arch, bold decisive, and calm shallow curve;
- four eyes: focused horizontal, friendly soft, decisive upturned, and calm slightly downturned;
- four mouths: neutral, smile, determined, and concerned/tired;
- four hairstyles: asymmetrical bob, low bun, short tousled, and neat short side part;
- three visible dark-circle overlays: mild fatigue, medium overwork, and severe illness/burnout,
  plus a generated transparent `none` layer;
- an opaque institutional body/background underlay with a slate uniform and small role accent.

The generator returned visually checked checkerboard pixels instead of a real alpha channel for the
isolated layers. The retained authoring masters remove only the connected light-neutral background,
preserve enclosed eye whites, normalize mouth scale and registration, and export real PNG alpha.

### Selected generator outputs

All source outputs remain in
`C:\Users\king0\.codex\generated_images\01a05f0b-a607-70a2-9df4-f9ca9ad33579`:

- canonical composite: `exec-d2e13937-5065-4ec3-9711-851b44ff3e13.png`
- face and body: `exec-81556abb-ddc7-4b50-b16c-07435bfc8869.png`,
  `exec-9e419ca2-e70b-49a9-9c6e-5c9ed112885c.png`
- eyebrows: `exec-538798a0-2a14-46e5-a39f-c728099652ac.png`,
  `exec-6a3a5f23-cbbf-439a-b1b3-afb4efc28454.png`,
  `exec-db4a17e9-a34d-4b80-8449-f1db20105397.png`,
  `exec-bdae3bae-5aa2-4ec3-99ff-f9257cf8b630.png`
- eyes: `exec-3cc89579-a846-4bd8-b705-9335fd617893.png`,
  `exec-83290d17-7335-43d9-955a-d20e5e6d977e.png`,
  `exec-c77338c3-439c-49ee-bb9a-1150b14f34fa.png`,
  `exec-2d723f17-e9c0-46e4-a3b5-8f376a0a5aa7.png`
- mouths: `exec-80765cdd-014f-460a-88b1-a119b08946a4.png`,
  `exec-6561246f-e898-42f8-a421-d095593a615a.png`,
  `exec-e261c2df-fcd6-427c-bedc-376f5b37e9f8.png`,
  `exec-7b6a3b83-a023-41fe-ae8b-ffdfd5e2ee20.png`
- hair: `exec-bf84579c-96f2-4705-805c-a80489c160a9.png`,
  `exec-e18c1179-a19f-4aca-92c8-298911647cb9.png`,
  `exec-566b51ee-5f5d-4c4b-8b07-e070edb3249f.png`,
  `exec-d211efa3-edab-4cde-b998-e95bfe45a93c.png`
- dark circles: `exec-8d51eff4-2302-4941-aedc-8a06f3ad8f54.png`,
  `exec-02075903-c33e-412a-a11e-ca4d431f986e.png`,
  `exec-4a19f930-db14-4439-8836-abebce7d389f.png`

### HotUpdate AOT safety audit

The runtime change is patch-only. The HotUpdate diff introduces no new Unity, package, platform,
native, Contracts, reflection, delegate, serialization, or closed-generic API surface.

- `Addressables.LoadAssetAsync<Texture2D>(object)` is the same exact typed-load overload already used
  by the installed v11 portrait path.
- `AsyncOperationHandle<Texture2D>.Status` and `.Result` are the same exact members already used by
  the installed v11 portrait path.
- `GUI.DrawTexture(Rect, Texture)` is the same exact overload used by the installed baseline.
- New storage is limited to managed arrays of the already-instantiated `Texture2D` and
  `AsyncOperationHandle<Texture2D>` types.
- No new base-APK member or supplemental AOT metadata is required.

Editor compilation and 155 EditMode tests passed. Device compatibility is not claimed until an
on-device smoke test has exercised remote catalog load, all seven draw layers, and a condition-state
transition on the installed base APK.

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
