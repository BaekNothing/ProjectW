# Architecture – ProjectW System Overview

**작성 목적:** ProjectW(개발 코드명) / **외행성재척지원실 3과**의 전반적인 시스템 구조를 한눈에 파악하기 위한 구현·문서 통합 개요이다.

**관련 문서**

| 역할 | 경로 |
|------|------|
| AI·문서 우선순위 | `Project_W – System Index (AI Entry Point).md` |
| 인게임 규칙 SSOT | `Ingame/SSOT – Ingame.md` |
| 업무 규칙 SSOT | `Ingame/SSOT – Work.md` |
| 아웃게임 규칙 SSOT | `SSOT – Outgame.md` |
| 워크플로·메타 | `SSOT – Workflow Confluence × Unity × GitHub.md`, `SSOT – Metadata.md` |

> **동기화 주의 (2026-06 기준):** 루트 `README.md`·빌드 설정·CI 게이트에 남아 있는 Routine Observation MVP·Outgame 씬 참조는 **현재 `Assets/Scripts` 구현과 일치하지 않는다.** 본 문서의 「구현 현황」 절을 기준으로 판단한다.

---

## 1. 프로젝트 정체성

```mermaid
mindmap
  root((ProjectW))
    게임
      PM 관리 시뮬
      블랙코미디 업무 배치
      동적 업무 생성
      불완전 정보 + 검토 비용
    문서 계층
      System Index
      SSOT Ingame/Outgame
      Deprecated PM Log
    Unity 구현
      Case Review 코어
      ScriptableObject 데이터
      에디터 워크샵
    미연결/레거시
      MVP Scene UI
      Routine Observation
      Outgame Scene 파일
```

| 항목 | 내용 |
|------|------|
| **개발 코드명** | Project_W / ProjectW |
| **작업 제목** | 외행성재척지원실 3과 |
| **장르·핵심** | 중간관리자 시점의 업무 배치·서류 검토·덱 기반 개체 행동·피드백 루프 |
| **플레이어 목표** | AI 제안과 인간 검토 사이에서 비용을 조절하며, AI 대체 불가한 관리 가치를 증명 |
| **폐기된 방향** | 순수 관찰형 자동 서사, 검토 비용 없는 완전 정보 UI, AI 제안 무조건 정답 처리 |

---

## 2. 문서·구현·Git 3층 구조

AI·협업 시 **판단 순서는 고정**이다 (System Index 기준).

```mermaid
flowchart TB
  subgraph L1["1. 거버넌스"]
    IDX["System Index"]
  end
  subgraph L2["2. 규칙 SSOT"]
    ING["SSOT – Ingame"]
    WORK["SSOT – Work"]
    OUT["SSOT – Outgame"]
    META["SSOT – Metadata"]
    WF["SSOT – Workflow"]
    CHR["Characters / Possessions / Memory"]
  end
  subgraph L3["3. 구현"]
    CODE["Assets/Scripts\nCaseReviewGame 등"]
    SO["ScriptableObject + Resources"]
    ED["Editor Workshop"]
  end
  subgraph L4["4. 이력"]
    GIT["Git Commit / PR"]
  end

  IDX --> ING & OUT & META & WF
  ING --> WORK
  ING --> CHR
  ING -.->|규칙 반영| CODE
  CHR -.->|데이터 모델| SO
  CODE --> SO
  ED --> SO
  CODE --> GIT
```

**Sync Rule:** 문서↔구현 불일치 시 → SSOT 수정 여부 확인 → 미갱신이면 SSOT 먼저 → Unity 반영 → 커밋 메시지에 SSOT 경로 명시.

---

## 3. Unity 프로젝트 물리 구조

```mermaid
graph LR
  subgraph Assets
    SPEC["Specification/\n(SSOT·PM·본 문서)"]
    SCR["Scripts/\nScripts.asmdef"]
    ED["Editor/\nAssembly-CSharp-Editor"]
    TST["Tests/EditMode/\nProjectW.Tests.EditMode"]
    SCN["Scenes/"]
    RES["Resources/\nCaseReviewData/"]
    SET["Settings/ URP 2D"]
  end
  subgraph Packages
    URP["URP 17.x"]
    INP["Input System"]
    TF["Test Framework"]
    MCP["unity-mcp"]
  end
  SCR --> TST
  SCR --> ED
  ED --> SCR
```

| 폴더 | 상태 | 역할 |
|------|------|------|
| `Assets/Specification/` | 활성 | 규칙·PM·아키텍처 문서 |
| `Assets/Scripts/IngameCore/CaseReview/` | **유일한 런타임 코드** | 순수 게임 로직 + SO 정의 |
| `Assets/Editor/` | 활성 | 캐릭터 데이터 워크샵, 에디터 리프레시 |
| `Assets/Tests/EditMode/` | 활성 | Case Review 코어 단위 테스트 (17개) |
| `Assets/Resources/CaseReviewData/Samples/` | 활성 | 샘플 SO 에셋 (로드 코드는 아직 없음) |
| `Assets/Scenes/MVP Scene.unity` | 존재 | 빌드 포함, Case Review 드라이버 미연결 |
| `Assets/Scenes/CharacterDataWorkshop.unity` | 존재 | 데이터 제작 전용, 빌드 미포함 |
| `Assets/Scenes/Outgame Scene.unity` | **누락** | 빌드 설정에만 참조 (깨진 참조) |
| `Assets/Data`, `Prefabs`, `Materials` 등 | 빈 폴더 | 예약 |

---

## 4. 어셈블리·네임스페이스

```mermaid
graph TB
  subgraph Runtime["Scripts.asmdef → Scripts"]
    NS["ProjectW.IngameCore.CaseReview"]
    CRG["CaseReviewGame"]
    MDL["Models / CoreRules"]
    RPT["ReportGenerator"]
    SO_DEF["*Definition ScriptableObjects"]
    WS_MB["CharacterDataWorkshop MonoBehaviour"]
  end
  subgraph EditorAsm["Assembly-CSharp-Editor"]
    NS_E["ProjectW.Editor"]
    WED["CharacterDataWorkshopEditor"]
    REF["ProjectWManualRefreshBridge"]
  end
  subgraph TestAsm["ProjectW.Tests.EditMode"]
    NS_T["ProjectW.Tests.EditMode"]
    TST["CaseReviewCoreTests"]
  end

  TestAsm -->|references| Runtime
  EditorAsm -.->|types| Runtime
  Runtime --> INP_REF["Unity.InputSystem"]
```

| 어셈블리 | 플랫폼 | 포함 |
|----------|--------|------|
| `Scripts` | Player + Editor | CaseReview 전 모듈 |
| `ProjectW.Tests.EditMode` | Editor only | `CaseReviewCoreTests.cs` |
| `Assembly-CSharp-Editor` | Editor | 워크샵·메뉴·리프레시 |

**설계 포인트:** `CaseReviewGame`은 `UnityEngine`에 의존하지 않는 **순수 C# 로직**이다. EditMode 테스트에서 Unity 런타임 없이 결정론·리플레이를 검증할 수 있다.

---

## 5. 인게임 설계 (SSOT) vs 구현 (Case Review)

### 5.1 SSOT 일일 루프

```mermaid
flowchart LR
  M["1. Morning Draft\n덱 1장 + AI 초안"]
  R["2. Review & Planning\n검토 비용"]
  A["3. Assignment\n배치·플랜"]
  E["4. Execution\n결과 시뮬"]
  F["5. Feedback\n카드·관계·압력"]

  M --> R --> A --> E --> F
  F -->|NEXT DAY| M
```

### 5.2 구현 슬롯·명령 매핑

`CaseReviewGame`은 텍스트 명령 REPL로 위 루프를 프로토타이핑한다.

```mermaid
stateDiagram-v2
  [*] --> Morning: Init(seed)
  Morning --> Morning: PLAN, ADJUST, QUEUE, OPEN,\nSUMMARY, LOG, CHECK, ASSIGN...
  Morning --> Noon: 시간 경과\n(UseTimePressure=true)
  Morning --> Evening: CONFIRM PLAN\n(기본: 압박 off → Noon 스킵)
  Noon --> Evening: 시간 경과
  Evening --> Evening: REPORT, REVIEW, APPROVE, HOLD...
  Evening --> Morning: NEXT DAY
```

| `Slot` | 기본 시간(초) | 비고 |
|--------|---------------|------|
| `Morning` | 90 | 아침 카드·플랜·배치 |
| `Noon` | 210 | `UseTimePressure`일 때만 |
| `Evening` | 120 | 보고·피드백·일 종료 |

**주요 API**

| 메서드 | 역할 |
|--------|------|
| `Init(config, seed)` | 시드·인력·큐·아침 플랜·카드 |
| `Dispatch(state, command)` | 플레이어 명령 처리 |
| `Advance(state, deltaSec)` | 시간 진행·슬롯 전환 |
| `Snapshot` / `Restore` | JSON 직렬화 상태 저장 |
| `Replay(seed, commands)` | 결정론 리플레이 검증 |

**대표 명령:** `HELP`, `STATUS`, `PLAN`, `CONFIRM PLAN`, `ADJUST`, `QUEUE`, `OPEN`, `SUMMARY`, `LOG`, `CHECK`, `ASSIGN`, `REDIRECT`, `REPORT`, `REVIEW`, `APPROVE`, `HOLD`, `NEXT DAY`

### 5.3 진실 vs 표면 (정보 비대칭)

```mermaid
flowchart TB
  subgraph Internal["내부 (플레이어 비공개)"]
    TF["TruthFrame\n사실 프레임"]
    SIM["시뮬 결과·잠재 리스크"]
  end
  subgraph Surface["표면 (플레이어 접근)"]
    VL["VisibleLog\n지연·누락·왜곡 플래그"]
    SUM["요약만 읽은 승인"]
    DR["DailyReportDocument\n템플릿 보고서"]
  end
  TF --> SIM
  SIM --> VL
  SIM --> DR
  VL -->|검토 비용| PLAYER["플레이어 명령"]
  DR -->|REVIEW| PLAYER
```

검토 행동마다 `ReviewCostEntry`(시간·자원·집중·신뢰·AI 대체 압력)가 기록된다. SSOT 원칙: **전부 검토하면 늦게 실패, 전혀 검토하면 누적 실패.**

---

## 6. Case Review 모듈 상세

### 6.1 클래스 역할 맵

```mermaid
classDiagram
  class CaseReviewGame {
    +Init()
    +Dispatch()
    +Advance()
    +Snapshot()
    +Replay()
  }
  class GameState {
    +Day Slot Staff Queue
    +MorningPlan MorningCards
    +TruthFrames Logs Reports
  }
  class CaseReviewRules {
    +ICardDrawService
    +IReviewCostPolicy
    +IReplacementPressurePolicy
    +IBossPolicy
  }
  class IReportGenerator {
    <<interface>>
  }
  class CharacterBaseDefinition {
    ScriptableObject
  }
  class CharacterRuntimeData {
    ScriptableObject
  }

  CaseReviewGame --> GameState
  GameState --> CaseReviewRules : via GameConfig
  CaseReviewGame --> IReportGenerator
  CharacterBaseDefinition --> Personnel : CreateRuntimeModel
  CharacterRuntimeData --> Personnel
```

| 파일 | 책임 |
|------|------|
| `CaseReviewGame.cs` | 유일한 게임 엔진 진입점, 명령·틱·일 진행 |
| `Models.cs` | `GameState`, `GameConfig`, `EventCase`, `Personnel`, DTO |
| `CoreRules.cs` | `CaseReviewRules` + 기본 정책 구현 |
| `ReportGenerator.cs` | 일일·개별 보고서 텍스트 생성 |
| `CharacterDataAssets.cs` | SO 인터페이스, 관계·기억 레코드 |
| `CharacterBaseDefinition.cs` | 시작 덱·퍼크가 있는 베이스 캐릭터 |
| `CharacterRuntimeData.cs` | 진행 상태·관계·기억 |
| `ActionCardDefinition.cs` / `PerkDefinition.cs` | 카드·퍼크 SO |
| `RenderResourceDefinition.cs` | UI/연출 메타 (로직 미연결) |
| `CharacterDataWorkshop.cs` | 씬용 `MonoBehaviour` (출력 폴더만) |

### 6.2 확장점 (플러그 정책)

```mermaid
flowchart LR
  CFG["GameConfig.Rules\nCaseReviewRules"]
  CFG --> CDS["ICardDrawService\n아침 카드 1장/인력"]
  CFG --> RCP["IReviewCostPolicy\n검토 비용"]
  CFG --> RPP["IReplacementPressurePolicy\nAI 대체 압력"]
  CFG --> BP["IBossPolicy\nBossArchetype 수정자"]
```

기본 구현은 `DefaultCardDrawService`, `DefaultReviewCostPolicy` 등이 `CoreRules.cs`에 동봉된다. 테스트에서 `ConfigRules_CanPlugCustomCardDrawService`로 교체 가능함을 검증한다.

---

## 7. 캐릭터·콘텐츠 데이터 파이프라인

### 7.1 Authoring → Runtime

```mermaid
flowchart TB
  subgraph Editor["에디터 Authoring"]
    MENU["Tools/ProjectW/Case Review/*"]
    INS["CharacterDataWorkshopEditor"]
    GEN["CharacterDataWorkshopGenerator"]
  end
  subgraph AssetsOnDisk["디스크"]
    BASE["CharacterBaseDefinition"]
    RUN["CharacterRuntimeData"]
    CARD["ActionCardDefinition"]
    PERK["PerkDefinition"]
    RR["RenderResourceDefinition"]
  end
  subgraph ResourcesPath["Resources (선택 로드)"]
    SAMP["CaseReviewData/Samples/"]
  end
  subgraph RuntimePOCO["런타임 POCO"]
    PER["Personnel"]
    AC["ActionCard"]
    PP["PersonnelPerk"]
  end
  subgraph Game["게임"]
    INIT["CaseReviewGame.Init\nGameConfig.InitialData"]
    GS["GameState.Staff"]
  end

  MENU --> GEN
  INS --> GEN
  GEN --> BASE & RUN & CARD & PERK & RR
  GEN --> SAMP
  BASE -->|CreateRuntimeModel| PER
  RUN -->|BuildInitialStaff| PER
  CARD --> AC
  PERK --> PP
  INIT --> GS
  PER --> GS
```

### 7.2 샘플 콘텐츠 인벤토리

`Assets/Resources/CaseReviewData/Samples/` (에디터 메뉴로 생성·갱신 가능)

| 종류 | 샘플 (접두) |
|------|-------------|
| Base 캐릭터 | `Base_CautiousPlanner`, `Base_QuietAuditor`, `Base_ShortcutOperator` |
| Runtime 캐릭터 | `Runtime_*` (위 3종 대응) |
| 행동 카드 | `Card_DamageControl`, `Card_Overdocument`, `Card_ShortcutPatch`, `Card_SilentAudit` |
| 퍼크 | `Perk_PanicImproviser`, `Perk_PatternAuditor`, `Perk_ProcedureLoyalist` |
| 렌더 | `RR_CautiousPlanner`, `RR_QuietAuditor`, `RR_ShortcutOperator` |

**현재 갭:** 코드베이스에 `Resources.Load` 호출이 없다. `InitialData == null`이면 `SeedStaff()` 하드코딩 시드가 사용된다. 런타임 UI 연결 시 `Resources.LoadAll<CharacterRuntimeData>` 또는 Addressables가 자연스러운 다음 단계다.

### 7.3 워크샵 씬·메뉴

| 진입 | 경로 |
|------|------|
| 씬 | `Assets/Scenes/CharacterDataWorkshop.unity` |
| 메뉴 | `Tools/ProjectW/Case Review/Open Character Data Workshop Scene` |
| 샘플 일괄 생성 | `Tools/ProjectW/Case Review/Create or Refresh Sample Data` |
| 강제 리프레시 | `Tools/ProjectW/Refresh/Force Refresh` (`Ctrl+Shift+R`) |

---

## 8. 테스트·CI·품질

```mermaid
flowchart LR
  TST["EditMode Tests\n17 × NUnit"]
  CRG["CaseReviewGame"]
  TST -->|Replay hash| CRG
  TST -->|SO → Personnel| CRG
  TST -->|Review cost / pressure| CRG
  GATE["tools/unity_gate_report.py"]
  GATE -.->|참조: 제거된 MVP 테스트| X["불일치 ⚠"]
```

| 테스트 | 검증 주제 |
|--------|-----------|
| `Replay_WithSameSeedAndTape_*` | 결정론 리플레이 |
| `Init_DrawsOneMorningCard*` | 아침 카드 |
| `ConfigRules_CanPlugCustomCardDrawService` | 정책 주입 |
| `CharacterBaseDefinition_*` / `DataDefinitions_*` | SO → 런타임 모델·렌더 참조 |
| `CharacterRuntimeData_StoresRelationships*` | 관계 저장 |
| `InitialData_CanSeedStaff*` | `InitialData` 시드 |
| `ReviewActions_RecordReviewCostEntries` | 검토 비용 |
| `ConfirmingUnadjustedAiPlan_*` | AI 대체 압력 |
| `SummaryOnlyApprove_*` | 요약만 승인 리스크 |
| `EquipLog_IsUnavailable*` | 로그 가시성·지연 |
| `RedirectBudget_*` | 리다이렉트 예산 |
| `ConfirmPlan_*` / `Report_*` / `EventReports_*` | 일 운영·보고 루프 |
| `HighRetentionRisk_*` | 이탈·채용 |
| `MorningPlan_CanBeAdjusted*` | 플랜 조정 |

PlayMode 테스트는 없다.

---

## 9. 씬·런타임 진입점 현황

```mermaid
flowchart TB
  subgraph Implemented["구현됨"]
    INIT["CaseReviewGame.Init"]
    DISP["Dispatch / Advance"]
    TEST["EditMode Tests"]
  end
  subgraph SceneOnly["씬만 존재"]
    MVP["MVP Scene.unity\n(빌드 index 1)"]
    WORK["CharacterDataWorkshop.unity"]
  end
  subgraph Broken["깨짐/레거시"]
    OUT["Outgame Scene.unity\n(빌드 index 0, 파일 없음)"]
    RO["README: RoutineObservationMvpSession"]
    REC["README: RuntimeErrorConsole"]
  end

  TEST --> INIT
  MVP -.->|드라이버 없음| INIT
  WORK -->|SO 생성만| SO["ScriptableObjects"]
  OUT -.x|missing file| OUT
```

| 진입 유형 | 상태 |
|-----------|------|
| 게임 로직 | `CaseReviewGame` 정적 API — **MonoBehaviour 부트스트랩 없음** |
| 플레이 가능 빌드 루프 | **미구현** (씬 UI 미연결) |
| 데이터 제작 | 워크샵 씬 + 에디터 메뉴 |
| `GameManager` / `Bootstrap` | 없음 |

---

## 10. 아웃게임·메타 (문서만 정의)

SSOT상 Outgame은 세션 간 인력·조직 이력·클론 베이·AI 대체 압력 추적 공간이다. **Unity C# 구현은 아직 없다.**

```mermaid
flowchart TB
  OUT_SSOT["SSOT – Outgame"]
  ING["Ingame Case Review"]
  OUT_SSOT -.->|영향: 덱·특성·압력| ING
  OUT_SCENE["Outgame Scene\n(빌드만 등록)"]
  OUT_SCENE -.x|파일 없음| OUT_SSOT
```

---

## 11. 협업·브랜치·도구

```mermaid
flowchart LR
  AI["AI 작업"]
  BR["ai-integration"]
  MAIN["main"]
  PR["PR + auto-merge squash"]
  AI --> BR
  BR --> PR
  MAIN -->|수동 병합| BR
```

| 도구 | 용도 |
|------|------|
| Git `ai-integration` | AI 기본 통합 브랜치, PR auto-merge |
| `com.coplaydev.unity-mcp` | 에디터 자동화 (MCP) |
| URP 2D + Input System | 렌더·입력 인프라 (Case Review 미사용) |

---

## 12. 기술 부채·다음 연결 작업

| 항목 | 권장 조치 |
|------|-----------|
| `Outgame Scene.unity` 빌드 참조 | 씬 복구 또는 `EditorBuildSettings`에서 제거 |
| README Routine Observation | 제거·갱신 또는 코드 복원 결정 |
| `unity_gate_report.py` | 현재 `CaseReviewCoreTests` 기준으로 게이트 목록 수정 |
| `Resources.Load` 부트스트랩 | `GameConfig.InitialData`에 샘플 SO 자동 연결 |
| MVP Scene | `CaseReviewGame` REPL/UI 브리지 MonoBehaviour |
| `RenderResourceDefinition` | UI 레이어에서 `IRenderableData` 소비 |
| Outgame 시스템 | SSOT에 맞춘 별도 모듈·씬 설계 |

---

## 13. 한 줄 요약

**ProjectW는 SSOT 문서가 규칙의 중심이고, Unity 구현은 `ProjectW.IngameCore.CaseReview` 순수 로직·ScriptableObject 캐릭터 파이프라인·에디터 워크샵·EditMode 테스트에 집중되어 있다. 씬 기반 플레이 루프와 Outgame·관찰 MVP는 문서/빌드 잔재와 분리된 상태이며, Case Review 코어를 UI·Resources에 연결하는 것이 현재 아키텍처의 주요 다음 단계다.**

---

*문서 버전: 2026-06-03 · 구현 기준 커밋 워킹 트리*
