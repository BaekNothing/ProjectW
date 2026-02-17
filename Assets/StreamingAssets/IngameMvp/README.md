# IngameMvp CSV Exchange Guide

1. Prepare Google Sheets with these tabs and exact headers:
- SessionConfig
- StateTransitionRules
- CharacterProfiles
- TerminationRules
- InterventionCommands

2. Export each tab as CSV and save with exact file names:
- SessionConfig.csv
- StateTransitionRules.csv
- CharacterProfiles.csv
- TerminationRules.csv
- InterventionCommands.csv

3. Replace files in this folder:
- Assets/StreamingAssets/IngameMvp

4. Press Play in Unity. `IngameMvpBootstrap` is created automatically at runtime and loads these CSV files.

Note: Spec requires CSV-first import. Direct Google Sheet runtime API binding is intentionally not used.
