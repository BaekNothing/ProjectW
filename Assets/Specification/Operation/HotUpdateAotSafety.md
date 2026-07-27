# HotUpdate AOT Safety

## Document Control

- Version: 1.0
- Status: Mandatory
- Action: Create
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

## Current Incident

Patch `dev-20260727-006` called `GUILayoutUtility.GetRect` and `GUILayoutUtility.GetLastRect`, which were not exercised by the installed base APK v2. The resulting `MissingMethodException` interrupted Gantt rendering and produced secondary GUI clip imbalance errors. The corrective patch must use only IMGUI members already present in base APK v2 or trigger a base rebuild.

## Impact Matrix

| Area | Impact |
|---|---|
| Ingame | Patch UI/gameplay code must stay within the proven AOT surface |
| Outgame | None |
| Metadata | Supplemental metadata is not considered native implementation proof |
| Operation | Mandatory diff audit and escalation before every patch publication |
