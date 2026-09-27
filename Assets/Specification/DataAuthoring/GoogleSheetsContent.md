# Google Sheets content authoring

- Target: `Assets/Specification/DataAuthoring/GoogleSheetsContent.md`
- Action: Create
- Scope: Two editable workbooks, character/perk samples, TID text and build-time import.
- Impact: Metadata, WebGL content packaging, office language options, authoring tools.
- SSOT Change: Yes. User requested and approved on 2026-09-27.

## Source ownership

The ProjectW folder under the user-selected `_HobbySheets` folder contains `ProjectW_Text`
and `ProjectW_GameData`. Both are public link-readable, with edits restricted to the owner
and separately authorized editors. Folder/name changes do not change configured workbook IDs.
These workbooks own the new authored rows; this specification owns schemas and behavior.
The existing campaign task-system.json remains authoritative for the legacy campaign.
The seven-day office narrative retains its existing progression and save identities.

## Tables

Text: `Texts(tid,ko,en)`. Blank language cells mean missing translation; the explicit token
`<EMPTY>` means intentionally empty. Resolution: selected locale, ko, then tid. `tid` mode
always shows the key. Variables use `{name}` and must match across translations.
Settings offer only ko/en/tid, persist selection separately from scenario saves, default ko.

GameData: Characters, CharacterPerks, Perks, PerkEffects, Events, Tasks, Relationships,
OfficeTextGroups. One identity per row; relation/effect rows are separate, never nested JSON
cells. IDs are permanent. All player-facing strings use `_tid` references. Semicolon-separated
tags mean all listed tags must match. Numeric probability is 0..1; success adjustments use
percentage points, not multiplicative percentages. Sample tuning is provisional.

Sample task probability is clamp(50 + stat - difficulty + matching perk percentage points,
5,95)/100. Event probability is clamp(base probability * matching multipliers,0,1), after
eligibility. World hazards are not multiplied by personality; requests to assist may be.
Other effect targets (duration, acceptance stress, failure stress) are explicit data and
must not be silently interpreted as success bonuses. The new sample catalog does not replace
the office's authored seven-day story or add automatic choices to it.

## Build contract

Build downloads each complete workbook as one XLSX snapshot (avoids per-tab partial downloads),
extracts plain tabular values, validates schemas/IDs/foreign keys/ranges/TIDs/placeholders,
and converts them into one generated JSON runtime bundle under Resources. Designers edit
Sheets, not this JSON. UTF-8 CSV snapshots are retained for diffs/reproduction.
Download or validation errors stop the build, never silently reuse old content. Local CSV
mode is explicit and intended for tests/offline reproduction only. No spreadsheet formulas
or arbitrary expressions are executed by the importer. Source hashes accompany the bundle.
Network access happens at build time, not in the running game.

## Implementation checklist

- Create and verify both native workbooks in the designated child folder.
- Provide deterministic importer and failure/roundtrip tests.
- Add WebGL build gate, bundled loader, locale resolver and ko/en/tid settings.
- Verify new sample effect calculations separately from the fixed office story.
- Verify Unity tests, build inclusion and Butler CLI delivery; no gameplay browser checks.

## Conflict and impact

ContentDataStructure v1.1 describes a future legacy campaign compiler. This addition activates
only the new sheet bundle, leaving that legacy contract intact. Ingame story outcomes unchanged;
Outgame gains locale selection; Metadata gains TID and normalized tables; Operation gains a
mandatory network/validation gate. Android base/patch distribution is not changed.

## Verification — 2026-09-27

- Created ProjectW folder and two native Sheets; parent IDs and tab names verified.
- 242 ko/en text rows, 12 perks, 33 effect rows, 5 sample characters, 20 perk links,
  6 events, 5 tasks, 2 relationship rows and 69 office text mappings.
- Native Google export roundtrip matches local CSV data exactly; all required TIDs resolve.
- Python validation tests: 10 passed. Unity focused EditMode tests: 20 passed,
  including locale fallback, perk probabilities, eligibility, cooldown, and existing office saves.
- New bundle/office localization is WebGL/Editor-only; legacy campaign text remains separate.
- Public unauthenticated export currently returns HTTP 401. Connected Drive permissions API
  cannot grant anyone-link access, and the available browser is signed out. The user has been
  asked to set both new workbooks to anyone-with-link Viewer. Public fetch, WebGL build and
  Butler delivery remain pending that setting. No publication is claimed.
