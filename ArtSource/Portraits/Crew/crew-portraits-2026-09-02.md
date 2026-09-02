# Crew Portrait Generation Record — 2026-09-02

## Status

- Stage: Runtime-approved monochrome line-art replacement masters
- Generator: OpenAI built-in image generation
- Generation date: 2026-09-02
- Selected master format: 1254 x 1254 square PNG, sRGB
- Runtime export: 512 x 512 square PNG `Texture2D` assets in `ProjectW Remote Portraits`
- Approval: User explicitly approved overwriting the four stable-ID portraits and deployment

## Monochrome Line-Art Revision — 2026-09-02

User review rejected beauty-oriented rendering because small anatomical and registration
imperfections became more conspicuous. The approved replacement direction deliberately simplifies
the crew into pure-white shapes with bold black contours and a slightly naive hand-drawn quality.
There are no role colors, gray tones, gradients, cel shading, detailed irises, or detailed hair
strands.

The existing four complete portraits were supplied as edit targets to OpenAI built-in image
generation. The generated images established the visual target; the shipping registered layers
were then deterministically reduced to the same black/white contour language so all modular parts
retain their exact `1254 x 1254` coordinates and alpha behavior. The body/background layers use a
shared deliberately simple uniform outline on opaque white. Facial marks and condition overlays
remain transparent black-line layers. The four complete fallbacks were recomposited from the same
shipping parts and exported to `512 x 512` for runtime.

### Final style-transfer prompt

```text
Use case: style-transfer
Asset type: square game UI crew portrait style target
Primary request: Redraw the supplied portrait as intentionally simple monochrome line art. Keep
the same single character identity, hairstyle silhouette, role-appropriate expression, uniform
silhouette, centered straight-on head-and-shoulders crop, and square composition. Remove the
polished beauty-rendering completely.
Scene/backdrop: pure solid white.
Style/medium: bold black hand-drawn contour lines, slightly naive and pleasantly awkward,
simplified graphic doodle/indie-game portrait, sparse interior detail, large calm shapes. All
enclosed shapes including skin, hair, eyes, and clothing are filled only with pure white; black is
used only for thick outlines and a few essential facial marks. No gray and no color.
Composition/framing: preserve the existing face size, registration, and margins.
Constraints: exactly one adult character; preserve readable hair and role-uniform silhouette; pure
white background and white fills; thick black lines; no text, logo, badge, watermark, props, or
extra person.
Avoid: attractive glossy anime rendering, moe polish, realistic anatomy, photorealism, gradients,
shading, hatching, gray tones, colored accents, detailed irises, detailed hair strands, smooth
vector perfection, 3D.
```

### Selected style targets

The built-in outputs remain under
`C:\Users\king0\.codex\generated_images\01a0600c-fad7-7482-acf5-8f542c8b22c8`:

- `crew-han-tech`: `exec-a289ed9c-69f8-4a08-964d-2e821a777433.png`
- `crew-yoon-analysis`: `exec-fa850637-403a-4e78-b5ee-a88cb65528c6.png`
- `crew-mi-management`: `exec-cb070163-787a-41a9-acda-607d020dada6.png`
- `crew-kang-adaptation`: `exec-40fd9207-4986-4884-a6e8-17fe6847fec7.png`

These are non-shipping visual targets rather than registered runtime layers.

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

## Registration Correction — 2026-09-02

User review found the eyes too widely separated and the hairstyles slightly low. A built-in image
generation edit was used as a visual target, with the existing management composite as the edit
target. The selected reference output is:

`C:\Users\king0\.codex\generated_images\01a05f0b-a607-70a2-9df4-f9ca9ad33579\exec-cb3bf67a-b165-431d-ad07-45701b0ad366.png`

Final edit prompt:

```text
Use case: precise-object-edit. Preserve the character, face shape, skin tone, expression, clothing,
background, linework, shading, colors, crop, and 1254x1254 composition. Move the left and right
eyes inward by an equal amount so the eye spacing is natural, roughly one eye width between the
inner corners, without changing eye size, shape, angle, iris, highlights, or vertical position.
Move the entire hairstyle upward very slightly without scaling or redesigning it. Keep eyebrows
aligned above the corrected eyes. Do not change the mouth, nose, face base, body, role accent, or
background. No text, logo, watermark, accessories, extra person, wrinkles, or age lines.
```

The generated edit established the target relationship but changed unrelated proportions, so it is
not a runtime asset. The approved deterministic correction keeps every existing part drawing and
changes only full-canvas registration:

- all eye pairs: each side moved 60 px inward on the `1254 x 1254` authoring canvas;
- all eyebrow pairs: each side moved 55 px inward;
- all dark-circle pairs: each side moved 60 px inward;
- all hairstyles: moved 30 px upward;
- mouth, face base, body/background, color, alpha, and layer order: unchanged.

The four stable complete-portrait fallbacks were recomposited from the corrected parts, and the
registered runtime layers were re-exported at `512 x 512`.

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
