# HotUpdate AOT Safety

## Document Control

- Version: 1.3
- Status: Mandatory
- Action: Update
- SSOT Change: Yes
- Rationale: Prevent patch assemblies from calling native/AOT implementations that do not exist in the installed base APK.

## Non-Negotiable Rule

`ProjectW.HotUpdate` must not add a new dependency on the base APK's AOT surface by default.

The AOT surface includes:

- UnityEngine and Unity package types, methods, properties, constructors, and overloads
- platform and native plugin APIs
- new closed generic instantiations that require AOT native code
- new delegate, reflection, serialization, or interop shapes that require AOT-generated implementations
- fixed `ProjectW.Bootstrap` and `ProjectW.Contracts` members

Supplemental AOT metadata helps HybridCLR interpret metadata. It does not create a missing native implementation.

## Default-Deny Patch Policy

A patch-only change may use an AOT member only when at least one of these proves that the installed base APK contains it:

1. The member and exact overload are called by code included in the installed base APK.
2. A committed preservation rule and the matching base build report prove it was retained.
3. An on-device smoke test against that exact base APK version proves the call executes.

Editor compilation, EditMode tests, and Editor Play Mode are not proof of device AOT compatibility.

When proof is absent, classify the dependency as APK-affecting. Do not publish it as a patch-only change.

## Prefer Patch-Owned Logic

Patch work should prefer:

- gameplay rules implemented inside `ProjectW.HotUpdate`
- primitive data and already-supported DTOs
- external manifest-listed gameplay data
- Unity/AOT calls already exercised by the installed base

This preference must not produce distorted architecture or fragile substitutes.

## No Strange Workarounds

Do not avoid an APK rebuild by introducing:

- reflection to reach an unproven AOT member
- copied engine behavior, fake UI/layout systems, or duplicated platform services
- exception swallowing or fallback paths that hide `MissingMethodException`
- convoluted state or data transformations whose only purpose is to bypass the base boundary
- unverified overload substitutions chosen only because they happen to compile

If a normal implementation requires an unproven AOT dependency and avoiding it would create such a workaround:

1. Stop the implementation before publishing.
2. Tell the user the exact required API/type and why the current base cannot safely provide it.
3. Reclassify the work as a base APK change.
4. Add an explicit preservation/reference point when appropriate.
5. Build and verify a new base APK.
6. Tell the user that the new APK must be installed.

The notification must happen before the base APK is built, even when the user has generally authorized implementation.

## Required Patch Preflight

Before publishing every HotUpdate patch:

1. Identify new Unity, package, platform, native, Contracts, reflection, and generic AOT references introduced by the diff.
2. Compare each exact member and overload with the installed base APK source/baseline.
3. Remove or replace only when the replacement is normal, readable, and already proven in the base.
4. Escalate to a base APK rebuild when proof is absent or replacement would be strange.
5. Run Unity tests.
6. Run an on-device smoke test for any changed AOT-facing execution path when a device is available.
7. Never describe Editor-only verification as device compatibility verification.

## Incident Recovery

If a patch produces `MissingMethodException`, `TypeLoadException`, missing native symbols, or a cascading IMGUI layout failure:

1. Treat the first missing AOT member as the root cause.
2. Treat later GUI clip/layout errors as secondary until proven otherwise.
3. Publish a corrected patch under a higher immutable version.
4. Do not overwrite the broken Release.
5. Record the missing member in the implementation notes or baseline so it is not reintroduced.

## Historical Incident

Patch `dev-20260727-006` called `GUILayoutUtility.GetRect` and `GUILayoutUtility.GetLastRect`, which were not exercised by the installed base APK v2. The resulting `MissingMethodException` interrupted Gantt rendering and produced secondary GUI clip imbalance errors. The corrective patch must use only IMGUI members already present in base APK v2 or trigger a base rebuild.

This incident is resolved by later base baselines and is retained as an operational lesson. The
active development channel requires base APK v9; v2-specific corrective delivery is no longer an
active channel option.

## Base APK v3 AOT Baseline

Base APK v3 was the approved base rebuild that resolved the historical incident and restored direct-content dragging.

It must bundle:

- `BaseVersion = 3`
- the latest embedded `ProjectW.HotUpdate.dll.bytes`
- the latest embedded `task-system.json`
- regenerated Android AOT metadata
- explicit linker preservation for `UnityEngine.GUILayoutUtility`
- explicit linker preservation for `UnityEngine.GUIUtility`
- the existing preserved `UnityEngine.Event`, `UnityEngine.GUI`, and `UnityEngine.GUILayout` surfaces

The stable preservation declaration must live outside generated HybridCLR output so regeneration cannot silently remove it.

Patches that rely on the restored direct-drag surface must declare `minBaseVersion = 3`. Base APK v2 devices must remain on the safe non-drag patch until v3 is installed.

This baseline does not authorize unrelated new Unity or platform APIs. Every later external member still follows the default-deny policy.

## Base APK v4 AOT Baseline

Base APK v4 adds the exact IMGUI style surface required for wider right-side scrollbars.

It must bundle:

- `BaseVersion = 4`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- stable linker preservation for `UnityEngine.GUISkin` and `UnityEngine.GUIStyle`
- the v3 IMGUI preservation surface

The approved new members are the vertical scrollbar style getters on `GUISkin` and the
`GUIStyle.fixedWidth` getter/setter. This does not authorize unrelated Unity APIs.

## Impact Matrix

| Area | Impact |
|---|---|
| Ingame | Patch UI/gameplay code must stay within the proven AOT surface |
| Outgame | None |
| Metadata | Supplemental metadata is not considered native implementation proof |
| Operation | Mandatory diff audit and escalation before every patch publication |

## Base APK v5 AOT Baseline

Base APK v5 adds the exact legacy multi-touch surface required for window pinch gestures.

It must bundle:

- `BaseVersion = 5`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- stable linker preservation for `UnityEngine.Input` and `UnityEngine.Touch`
- the v4 preservation surface

The approved new members are `Input.touchCount`, `Input.GetTouch(int)`, `Touch.position`, and the
touch value types needed to read two simultaneous pointers. This does not authorize unrelated
Unity input APIs.

## Base APK v6 AOT Baseline

Base APK v6 replaces the incompatible legacy touch path with the installed Input System package.

It must bundle:

- `BaseVersion = 6`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- stable linker preservation for the required `Unity.InputSystem` touch and control types
- the v5 preservation surface

The approved new members are `Touchscreen.current`, `Touchscreen.touches`,
`TouchControl.press`, `ButtonControl.isPressed`, `TouchControl.position`, and
`Vector2Control.ReadValue()`, including their required package-owned closed generic shapes. This
does not authorize unrelated Input System APIs.

## Base APK v7 AOT Baseline

Base APK v7 adds the fixed-to-HotUpdate diagnostics contract used to move patch download status,
version information, and logs into the Options-owned tabbed log popup.

It must bundle:

- `BaseVersion = 7`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- `IPatchDiagnostics` and `PatchDiagnosticEntry` in `ProjectW.Contracts`
- a `PatchBootstrapper` implementation of the diagnostics contract
- the v6 Input System preservation surface

The approved new Contracts calls are the diagnostics property getters, `GetLogs()`, and
`ClearLogs()`. This does not authorize unrelated Contracts or Unity APIs.

## Base APK v8 AOT Baseline

Base APK v8 adds the exact IMGUI window-depth operation required to bring an already-open
desktop application to the front when its desktop icon is pressed.

It must bundle:

- `BaseVersion = 8`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- stable linker preservation for `UnityEngine.GUI`
- the v7 diagnostics contract and Input System preservation surface

The approved new member is `GUI.BringWindowToFront(int)`. This does not authorize unrelated
Unity IMGUI APIs.

## Base APK v9 AOT Baseline

Base APK v9 adds fixed Bootstrap update-request operations and the IMGUI text-editing surface used by
the HotUpdate-owned title and gameplay-data editor screens.

It must bundle:

- `BaseVersion = 9`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- `IPatchDiagnostics.UpdateInProgress` and `IPatchDiagnostics.RequestUpdate()` in `ProjectW.Contracts`
- a `PatchBootstrapper` implementation that installs an update for activation after restart
- stable preservation for the existing full `UnityEngine.GUILayout` IMGUI surface
- the v8 window-depth, diagnostics, and Input System preservation surfaces

The approved new HotUpdate-facing members are the two diagnostics-contract update operations and
`GUILayout.TextField(string, GUILayoutOption[])`. This does not authorize unrelated Contracts,
Unity, package, or platform APIs.

The shipped v9 preservation report also retains all members of `GUIUtility`, `GUISkin`, and
`GUIStyle`. Patch UI may therefore use `GUIUtility.systemCopyBuffer` to copy editor text without
opening the virtual keyboard, and may read `GUISkin.textField` to set text colors through the
already-used `GUIStyle` state surface. This authorization does not extend to `TextEditor`, control
IDs, keyboard-control mutation, synthetic events, or programmatic text selection.

## Base APK v10 AOT Baseline

Base APK v10 adds the time and cached-texture surface required by HotUpdate-owned timed popups,
bottom-right toast notifications, rotating radial rays, pulsing glow, and small sparkle effects.

It must bundle:

- `BaseVersion = 10`
- the latest embedded `ProjectW.HotUpdate.dll.bytes` and `task-system.json`
- regenerated Android AOT metadata
- stable linker preservation for `UnityEngine.Time`, `UnityEngine.Texture`, `UnityEngine.Texture2D`,
  and `UnityEngine.TextureFormat`
- the complete v9 preservation surface

The approved new HotUpdate-facing members are `Time.unscaledTime`, the `Texture2D(int, int,
TextureFormat, bool)` constructor, `Texture2D.SetPixels(Color[])`, and `Texture2D.Apply(bool, bool)`.
The effect renderer may also use the already-preserved `GUI.Window`, `GUI.BringWindowToFront(int)`,
`GUI.DrawTexture(Rect, Texture)`, `GUIStyle`, and `Texture2D.whiteTexture` members.
This authorization does not extend to arbitrary shaders, materials, particle systems, render
textures, Resources-loaded visual assets, or unrelated time and texture APIs.

The generated radial animation frames and sparkle texture are created once and shared. Per-effect
data controls duration, rotation, pulse, sparkle count, color, and visibility; it must not regenerate
texture pixels every frame. Notification windows must render in their own frontmost GUI windows,
and effect rectangles must use that window's local coordinates so UI scaling cannot displace them.

Base APK v10 is the current installed baseline for the active development channel. Current manifests
must declare `minBaseVersion = 10` unless a later reviewed base rebuild supersedes this section.

## Base APK v11 AOT Baseline

Base APK v11 adds Unity Addressables 2.7.6 and its Resource Manager runtime so patch releases can
carry code and remote content through the same immutable GitHub Release and patch manifest.

It must bundle:

- `BaseVersion = 11`
- Unity Addressables 2.7.6 and the matching Scriptable Build Pipeline dependencies
- the Addressables runtime initialization data
- stable linker preservation for `Unity.Addressables` and `Unity.ResourceManager`
- the complete v10 preservation surface

The approved HotUpdate-facing surface is the external-catalog and typed texture-loading path used by
the popup effect loader: `Addressables.InternalIdTransformFunc`, `LoadContentCatalogAsync`,
`LoadAssetAsync<Texture2D>`, `AsyncOperationHandle`, `AsyncOperationStatus`, and the resource-location
internal ID getter. `GUIUtility.RotateAroundPivot(float, Vector2)` is covered by the existing full
GUIUtility preservation baseline. This authorization does not extend to unrelated Addressables APIs,
asset types, scenes, or arbitrary new closed generic loads.

Remote Addressables outputs must be published in the same `dev-YYYYMMDD-NNN` GitHub Release as the
HotUpdate DLL and listed in the same `patch-manifest.json`. The bootstrap continues to download,
size-check, hash-check, stage, promote, and roll back the complete release as one patch slot.

Base APK v11 becomes the active baseline when its APK is built and verified. Manifests containing
remote Addressables content must declare `minBaseVersion = 11`.

## Base APK v12 AOT Baseline

Base APK v12 replaces the removed prototype IMGUI presentation with the StudioUI runtime built on
Unity UI Toolkit. This is a reviewed base rebuild because UI Toolkit was not part of the v11
HotUpdate-facing AOT baseline.

It must bundle:

- `BaseVersion = 12`
- the fixed Bootstrap scene as the only Player scene
- a fixed-layer UI camera so Editor and Player rendering never depend on a HotUpdate camera API
- UI Toolkit's runtime Input System event path for pointer and keyboard input; no separate uGUI `EventSystem` is created
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, and USS resources
- regenerated Android AOT metadata
- stable linker preservation for `UnityEngine.UIElementsModule` and `UnityEngine.UIModule`
- the complete v11 preservation surface

The approved HotUpdate-facing surface is limited to the shipped StudioUI implementation:
`UIDocument`, the Resources-loaded `PanelSettings`/`ThemeStyleSheet`,
`Resources.Load<VisualTreeAsset>`, `Resources.Load<PanelSettings>`, UXML cloning,
`VisualElement`, `Label`, `Button`, `ScrollView`, UQuery, class-list and inline-style operations,
button callbacks, and the panel scale settings used by `ProjectWUiController`. This authorization
does not extend to arbitrary UI Toolkit controls, editor-only UIElements APIs, new generic view
types, custom mesh rendering, or unrelated resource types without another exact-member review.

Patches using the StudioUI runtime must declare `minBaseVersion = 12`. Editor compilation is not
device proof; the new title, navigation, theme switch, Gantt scrolling, next-day action, and modal
decision paths require an on-device smoke test before compatibility is claimed.

## Base APK v13 AOT Baseline

Base APK v13 adds the exact UI Toolkit pointer callback surface required for press-and-hold worker
assignment previews.

It must bundle:

- `BaseVersion = 13`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, and USS resources;
- regenerated Android AOT metadata;
- a fixed-layer reference to `VisualElement.RegisterCallback<PointerDownEvent>` and
  `VisualElement.RegisterCallback<PointerUpEvent>` using the one-argument callback overload;
- stable preservation for `PointerDownEvent`, `PointerUpEvent`, their `EventCallback<T>` delegate
  shapes, and the complete v12 UI Toolkit surface.

This authorization is limited to detecting pointer press and release for the shipped worker preview.
It does not authorize unrelated UI Toolkit event types, pointer capture, event synthesis, custom
rendering, or editor-only UI APIs. Patches using press-and-hold previews must declare
`minBaseVersion = 13`, and device compatibility still requires an on-device smoke test.

## Base APK v14 AOT Baseline

Base APK v14 adds the exact Input System keyboard surface required to dismiss a worker-assignment
preview from keyboard, Escape, or Android Back input while retaining v13 pointer dismissal.

It must bundle:

- `BaseVersion = 14`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, and USS resources;
- regenerated Android AOT metadata;
- a fixed-layer reference to `Keyboard.current`, `Keyboard.escapeKey`, `Keyboard.anyKey`, and
  `ButtonControl.isPressed`;
- stable preservation for `Keyboard`, `KeyControl`, `AnyKeyControl`, and the complete v13 surface.

This authorization is limited to dismissing the shipped worker preview on any key press, including
Escape and the Android Back mapping. It does not authorize text entry, synthetic input, arbitrary
Input System devices or controls, or unrelated keyboard members. Patches using this dismissal path
must declare `minBaseVersion = 14`, and device compatibility still requires an on-device smoke test.

## Base APK v15 AOT Baseline

Base APK v15 updates the embedded StudioUI resources with the reviewed interaction, motion,
feedback, focus, and reduced-motion rules. The HotUpdate controller continues to use only the
UI Toolkit and time surface already preserved by the v12-v14 baselines.

It must bundle:

- `BaseVersion = 15`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, and USS resources;
- the updated focus-visible, pressed, page, modal, notification, and reduced-motion styles;
- regenerated Android AOT metadata and the complete v14 preservation surface.

The approved controller changes use existing `VisualElement` class-list operations, inline display
styles, `PlayerPrefs` theme/preference persistence, and `Time.unscaledTime`. No new Unity, package,
Contracts, reflection, serialization, delegate, or closed-generic AOT surface is introduced. The
base rebuild is required because UXML and USS remain APK-embedded resources. Device compatibility
still requires an on-device smoke test of navigation, modal interruption, notification panel
retargeting, theme switching, and reduced motion.

## Base APK v16 AOT Baseline

Base APK v16 embeds the SI project-planning catalog data and the reviewed radial planning-board
styles. The fixed rebuild is required because StudioUI USS and the fallback gameplay data remain
APK-embedded resources. The HotUpdate controller uses only the UI Toolkit, time, LINQ, and managed
collection surface already exercised by the v15 implementation.

It must bundle:

- `BaseVersion = 16`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, and USS resources;
- catalog Task scope, extension-option, person-day, staffing, forecast, detail, and risk UI;
- regenerated Android AOT metadata and the complete v15 preservation surface.

The reviewed HotUpdate diff adds no new Unity, package, platform, native, Contracts, reflection,
serialization, or delegate dependency. The v16 generated AOT report adds the exact managed generic
surface introduced by catalog selection and aggregation: `Enumerable.Contains<object>`, its
`IEqualityComparer<object>` overload and comparer shape, and `Enumerable.Sum<object>` with an
integer selector. Catalog rules otherwise use patch-owned models and managed logic. Patches
containing the new catalog request must declare `minBaseVersion = 16`.
Editor tests prove managed behavior only; device compatibility requires an on-device smoke test of
scope toggles, option toggles, detail dialogs, worker assignment, plan submission, and restore.

## Base APK v17 AOT Baseline

Base APK v17 completes the catalog planning presentation as a six-segment radial board. The base
rebuild is required because the responsive orbit, segment positions, compact option rows, and
state-border styles live in the APK-embedded StudioUI USS resource.

It must bundle:

- `BaseVersion = 17`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, six-Task `task-system.json`, UXML, and USS;
- the two-ring radial board with a central project summary and six stable surrounding segments;
- explicit ON/OFF, unstaffed, and option-conflict segment states;
- regenerated Android AOT metadata and the complete v16 preservation surface.

The controller continues to construct `VisualElement`, `Label`, and `Button` instances and apply
class names through the surface preserved in v12-v16. It adds no new Unity, package, platform,
native, Contracts, reflection, serialization, delegate, or generic AOT dependency. Patches using
the completed radial board must declare `minBaseVersion = 17`. Device compatibility still requires
an on-device landscape smoke test of all six segment positions and their interactive controls.

## Base APK v18 AOT Baseline

Base APK v18 replaces the card-around-orbit approximation with the specified six-part annular
control. The base rebuild is required because the deterministic 60-degree segment sprite and its
USS layout are APK-embedded resources, and exact segment hit testing reads pointer coordinates in
the fixed UI Toolkit surface.

It must bundle:

- `BaseVersion = 18`;
- the latest embedded `ProjectW.HotUpdate.dll.bytes`, `task-system.json`, UXML, USS, and
  `catalog-segment.png`;
- six rotations of one transparent annular-sector sprite with state tinting;
- fixed-layer calls preserving `PointerDownEvent.position`, `EventBase.target`, and
  `VisualElement.WorldToLocal(Vector2)`;
- regenerated Android AOT metadata and the complete v17 preservation surface.

The HotUpdate controller uses the exact pointer members preserved by the v18 fixed layer, primitive
radius comparisons, and managed six-sector classification. It does not introduce custom mesh
rendering, reflection, serialization, platform APIs, or unreviewed generic view types. Patches
using the segmented control must declare `minBaseVersion = 18`. Device compatibility still
requires an on-device landscape smoke test of all six hit regions, option controls, detail actions,
and worker selection.

The regenerated AOT report adds `PointerEventBase<object>` and preserves `EventBase`,
`IEventHandler`, `PointerEventBase<T>`, `VisualElementExtensions`, `Vector2`, and `Vector3`. These
are the reviewed shapes reached by the fixed v18 pointer-position and world-to-local calls; they do
not authorize unrelated event types or coordinate APIs.
