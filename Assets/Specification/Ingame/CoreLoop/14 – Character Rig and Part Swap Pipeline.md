# 14 – Character Rig and Part Swap Pipeline

본 문서는 Unity 2D Animation 패키지 기준의 캐릭터 리깅/파츠 교체 규칙과 MVP→본 기반 전환 계획을 정의한다.

------

## 목적

- 캐릭터 파츠(헤어/상의/하의/악세사리) 조합이 바뀌어도 동일 애니메이션을 재사용할 수 있어야 한다.
- `Animator`/본 구조는 고정하고, 스프라이트 교체만으로 외형 변경이 가능해야 한다.
- MVP 초기 구현(본 없이 트랜스폼 애니메이션)에서 본 기반 구조로 무중단 이행 가능한 경로를 제공한다.

------

## 1) 공통 스켈레톤(1종) 정의

### 스켈레톤 ID

- 기본 스켈레톤: `Humanoid2D_Common_v1`
- 모든 플레이어블/NPC 캐릭터 파츠는 본 스켈레톤 맵핑 규칙을 따른다.

### 필수 본 계층

- `Root`
  - `Hip`
    - `Spine`
      - `Chest`
        - `Neck`
          - `Head`
        - `Shoulder_L` → `UpperArm_L` → `LowerArm_L` → `Hand_L`
        - `Shoulder_R` → `UpperArm_R` → `LowerArm_R` → `Hand_R`
    - `UpperLeg_L` → `LowerLeg_L` → `Foot_L`
    - `UpperLeg_R` → `LowerLeg_R` → `Foot_R`

### 본 운용 고정값

- Bone 이름/계층은 캐릭터 타입과 무관하게 변경하지 않는다.
- Bone 길이/기본 포즈는 `T-Pose 2D`를 기준으로 유지한다.
- Bone 회전/이동 애니메이션은 본 문서와 Animator Controller 규칙을 우선한다.

------

## 2) 파츠 아트 파이프라인 맵핑 규칙

### 공통 규칙

- 모든 파츠 스프라이트는 `Humanoid2D_Common_v1` 본 이름에만 바인딩한다.
- 파츠별 Sprite Library Label은 `PartType_Variant` 네이밍을 따른다.
  - 예: `Hair_Short01`, `Top_UniformA`, `Accessory_Glasses01`
- PSD/PSB 소스 단계에서 파츠 Pivot 기준점은 기본 본의 로컬 축과 정렬한다.
- 파츠별 메시 생성(2D Skinning) 시 Weight는 인접 본만 사용하고, 원거리 본 Weight는 금지한다.

### 파츠 그룹별 권장 바인딩

- Head 계열(헤어/얼굴/모자): `Head`, 필요 시 `Neck`
- Torso 계열(상의/코트): `Spine`, `Chest`, 필요 시 `Shoulder_*`
- Arm 계열(소매/장갑): `UpperArm_*`, `LowerArm_*`, `Hand_*`
- Leg 계열(하의/신발): `UpperLeg_*`, `LowerLeg_*`, `Foot_*`
- Accessory 계열(가방/브로치 등): 장착 기준 본 1개를 우선 고정하고, 보조 본은 최대 1개까지만 허용

### 검수 체크리스트

- 같은 애니메이션 클립에서 파츠 교체 후 관절 이탈(분리/찢김)이 없어야 한다.
- `SpriteResolver` 교체 시 Draw Order가 사전 정의된 레이어 규칙을 유지해야 한다.
- 특정 파츠 전용 본 추가는 금지한다. 필요 시 공통 스켈레톤 버전 업(`v2`)으로 관리한다.

------

## 3) Animator/파츠 교체 런타임 규칙

- `Animator`는 캐릭터 루트에 1개만 둔다.
- 애니메이션은 본(Transform/Bone) 구동만 담당한다.
- 파츠 교체 시 다음 요소를 변경하지 않는다.
  - Bone hierarchy
  - Animator Controller
  - Avatar/2D Animation Rig 자산
- 파츠 교체는 `SpriteResolver` 또는 동등한 스프라이트 참조 교체 계층에서만 수행한다.

------

## 4) MVP → 본 기반 전환 마이그레이션 계획

### 1차 (MVP 빠른 검증)

- 본 없이 파츠별 트랜스폼 애니메이션으로 시작한다.
  - 예: `Arm_L`, `Arm_R` 오브젝트 회전으로 팔 흔들기 구현
- 목표
  - 연출/가독성 검증
  - 파츠 조합 런타임 교체 UX 검증
- 제약
  - 관절 품질/자연스러운 변형은 제한적
  - 복잡한 모션 재사용성 낮음

### 2차 (본 기반 전환)

- `Humanoid2D_Common_v1` 본 리그를 도입한다.
- 1차에서 사용한 파츠 프리팹/리소스를 본 맵핑 규칙에 맞춰 재수출한다.
- Animator 상태머신은 유지하고, 클립 데이터는 본 구동 기준으로 교체한다.
- 전환 완료 조건
  - 1차 대비 동일 모션에서 파츠 조합 깨짐이 감소
  - 파츠 교체 시 애니메이터 재초기화 없이 정상 동작

------

## 5) 좌우 반전(Flip) 규칙

- 루트 `Transform.localScale.x` 반전은 기본 금지한다.
- 좌우 반전은 아래 우선순위로 처리한다.
  1. 본 단위 회전/포즈 전환(권장)
  2. 파츠별 좌/우 스프라이트 세트 교체
  3. 정말 필요한 경우에만 개별 파츠의 국소 scale 반전
- 악세사리는 장착 본 기준 오프셋 프리셋(`LeftFacing`, `RightFacing`)을 별도로 둔다.
- 반전 시 필수 검수 항목
  - 안경/귀걸이/숄더백 위치 역전 여부
  - 텍스트/문양이 거울 반전되는지 여부
  - 손/발의 전후 레이어가 의도대로 유지되는지 여부

------

## Definition of Done

- 신규 파츠가 추가되어도 `Humanoid2D_Common_v1` 규칙만 따르면 애니메이션 재작업 없이 교체 가능해야 한다.
- 캐릭터 루트 Animator 1개 구조가 유지되어야 한다.
- MVP 1차와 2차 전환 기준이 문서만으로 재현 가능해야 한다.
