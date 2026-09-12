# Web preview publishing

- Target: `Assets/Specification/Operation/WebPreviewPublishing.md`
- Action: Create
- Scope: First desktop-browser preview at `baeknothing.itch.io/managingheaven`.
- Impact: WebGL-only startup, local web Addressables, build tooling, itch.io description/upload.
- SSOT Change: Yes (delivery platform; gameplay rules unchanged).
- Authorization: User requested publishing the current version at that URL with a short description.

## Delivery

- Compile the existing gameplay into a standalone WebGL player. Do not download or execute Android
  HotUpdate DLLs in the browser. Web updates replace the itch.io web build and activate on reload.
- Use a dedicated WebGL startup scene/assembly that calls the existing GameEntry directly.
- Persist saves through existing PlayerPrefs storage. Explain that clearing browser site data may
  remove saves. Do not change Android bootstrap, patch channel, signing, or base compatibility.
- Include reviewed effect and crew portrait Addressables in the web build with local catalog/paths.
  Temporarily adapt build settings and restore shared Android/Addressables/HybridCLR settings.
- Keep desktop gameplay and Korean text from System/TaskSystem.md; no simulation rebalance.
- Publish an initial testing version, with mouse controls and browser-save limitations described.

## Validation

- Add paired tests for web bootstrap health reporting, single initialization and unavailable
  Android patch requests. Build WebGL through the existing installed Unity CLI.
- Validate index.html and itch.io file limits; browser-test startup, readable UI, next-day input
  and bundled portraits. Record any remaining test failures or browser limitations honestly.
- Publish to the supplied itch.io page, enable browser playback, add a concise description, and
  verify the resulting page. This is explicitly authorized independently of the Android channel.
