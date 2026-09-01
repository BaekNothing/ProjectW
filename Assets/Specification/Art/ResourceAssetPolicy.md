# ProjectW Resource Asset Policy

## Document Control

- Version: 0.1
- Status: Working baseline
- Action: Create
- SSOT Change: Yes
- Purpose: Keep slowly accumulated art and audio resources reviewable, reproducible, and separate
  from runtime delivery until their consumption path is approved.

## Lifecycle and Directory Ownership

Resource files move in one direction through four stages.

1. **Scratch:** `.utmp/art/` contains local experiments and discarded generations. It is ignored by
   Git and must never be referenced by Unity.
2. **Authoring:** `ArtSource/<Category>/...` contains selected generation outputs, editable masters,
   references that are licensed for repository use, and a generation record. Unity does not import
   this directory.
3. **Remote runtime:** `Assets/MilestonePrototype/RemoteAssets/<Category>/...` contains reviewed
   exports that may ship through the unified remote Addressables release.
4. **Base runtime:** `Assets/MilestonePrototype/LocalAssets/<Category>/...` is reserved for assets
   that must be embedded in the base APK. Moving an asset here is a base-APK decision, not an art
   organization convenience.

Generated or unfinished work stays in `ArtSource`. Copy an export into a runtime directory only
after its visual review, license/provenance review, importer settings, memory budget, and typed
runtime consumption path have all been approved.

## Category Layout

Use the same category vocabulary in authoring and runtime folders:

```text
Portraits/Crew
Portraits/Contacts
Illustrations/Events
Backgrounds/Desktop
Backgrounds/Events
UI/Icons
UI/Frames
Effects
Audio/Music
Audio/Sfx
```

Add a category only when an actual asset does not fit. Do not create folders for speculative future
content.

## Stable IDs, File Names, and Addresses

- Use lowercase ASCII kebab-case for stable asset IDs and file names.
- Do not use localized display names, spaces, dates, generation model names, or revision numbers as
  the stable ID.
- Authoring files use `<stable-id>.<extension>`.
- Runtime files preserve the same stable ID unless an explicit export suffix is meaningful.
- Addressables addresses use lowercase plural category paths, for example
  `portraits/crew/crew-han-tech`.
- A replacement keeps its stable ID and is reviewed as a normal Git diff. A simultaneous alternate
  uses a semantic suffix such as `-injured`, `-night`, or `-selected`; it does not use `-v2`.

Current crew identity mapping:

| Stable ID | Display data | Intended Addressables address |
|---|---|---|
| `crew-han-tech` | `한기술관` | `portraits/crew/crew-han-tech` |
| `crew-yoon-analysis` | `윤분석관` | `portraits/crew/crew-yoon-analysis` |
| `crew-mi-management` | `미관리자` | `portraits/crew/crew-mi-management` |
| `crew-kang-adaptation` | `강적응관` | `portraits/crew/crew-kang-adaptation` |

These IDs identify the roster slots and survive later display-name edits.

## Generation and Provenance Record

Every generated batch kept in `ArtSource` includes a nearby Markdown record containing:

- asset IDs and display-data mapping;
- generator and generation date;
- the shared style prompt and per-asset subject prompt;
- reference-image provenance, when references are used;
- selected output dimensions and format;
- known review issues and approval state.

Do not commit a reference image unless the project has the right to retain and redistribute it.
Generated assets must not deliberately imitate a named living artist or a specific protected
franchise style.

## Image Formats and Working Sizes

- Character portraits: author at a minimum of `1024 x 1024`, square PNG, sRGB. Keep the face inside
  the central 70 percent and reserve a consistent outer safe margin. Preserve a larger square
  generator output as the authoring master instead of upscaling or destructively resizing it.
- Runtime portrait export: default to `512 x 512` PNG until profiling justifies a different size.
- Flat art, line art, transparency, and UI: PNG.
- Large opaque painted backgrounds: high-quality JPG is allowed after visible artifact review.
- Do not introduce PSD, TIFF, WebP, or another format into a runtime directory without confirming
  Unity import support and repository-size impact. Editable masters may remain in `ArtSource`.
- Never upscale a runtime export beyond its selected authoring master.

## Current Crew Portrait Style Baseline

The initial crew set uses an original modern American TV editorial-cartoon direction for an adult
workplace strategy game:

- bold, clean dark outlines;
- simplified geometric facial construction;
- expressive but restrained adult proportions;
- flat color blocks with no more than three tonal steps per material;
- a shared pale cool gray-blue institutional backdrop;
- centered, straight-on head-and-shoulders employee-ID framing;
- shared slate-gray operations clothing with a small role-color accent;
- no text, badge card, logo, watermark, weapon, military decoration, or exaggerated sci-fi armor.

The direction is descriptive, not a request to imitate an existing television show or artist.
Across the four-person initial roster, present two women and two men. Profile data determines
personality, age impression, expression, and role accent; gender is an art-direction choice and is
not added to gameplay data unless design later requires it.

Role accent colors:

| Specialty | Accent |
|---|---|
| Tech | muted orange |
| Analysis | muted blue |
| Management | muted olive-gold |
| Adaptation | muted brick red |

## Unity Import and Addressables Gate

- `ArtSource` is never an Addressables source and receives no Unity `.meta` files.
- Runtime portrait exports should initially import as `Default` textures because the installed v11
  baseline proves the existing typed `Texture2D` load path, not a new `Sprite` load path.
- Use no mipmaps for fixed-size UI portraits, clamp wrapping, sRGB color, and platform compression
  only after checking small-face and outline quality on the target device.
- Create Addressables groups by update and packing behavior, not merely by file extension. Crew
  portraits use `ProjectW Remote Portraits`, packed together while the set is small.
- Adding files under `RemoteAssets` does not by itself authorize delivery. The setup tooling and
  Addressables entries must list them explicitly.
- New typed asset loads, Addressables APIs, materials, sprites, animation, audio playback paths, or
  other AOT-facing members require the repository AOT safety review before HotUpdate code uses them.
- All approved remote content ships in the same immutable `dev-YYYYMMDD-NNN` release and unified
  patch manifest as code and gameplay data. Never create a separate art channel.

## Review Checklist

Before promoting an authoring asset to runtime:

- identity and role are readable at intended UI size;
- the batch style, crop, background, and safe margins are consistent;
- no unintended text, logo, watermark, extra person, or reference-image residue exists;
- the source record and usage rights are present;
- dimensions, alpha, color space, and compression are appropriate;
- the stable ID and address do not collide;
- the consuming UI and exact typed-load path pass the AOT gate;
- bundle size and target-device appearance are checked;
- only then is the asset added to an Addressables group and patch release.
