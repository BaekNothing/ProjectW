# CharacterProfiles.csv.spec

## Purpose

자율 행동 결정을 위한 캐릭터 프로필을 주입한다.

## Required Columns

- `character_id`
- `trait_weights_json`
- `stress`
- `health`
- `trauma_level`
- `enabled`

## Sample Row

`char_001,{"risk":0.7,"cohesion":0.4},10,85,1,true`

## Validation

- `health` 범위(0..100) 위반 시 `E-CSV-003`.
