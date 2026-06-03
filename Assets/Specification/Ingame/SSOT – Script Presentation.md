# SSOT – Script Presentation

본 문서는 **외행성재척지원실 3과**의 스크립트 파트와 프레젠테이션 연출에 대한 단일 진실원이다.

상위 규칙은 `Assets/Specification/Ingame/SSOT – Ingame.md`를 따른다.
캐릭터 정체성과 관계 규칙은 `SSOT – Characters.md`, `SSOT – Character Memory and Relationships.md`를 따른다.
업무와 감사 이벤트의 생성 규칙은 `SSOT – Work.md`를 따른다.

------

## 1. Scope

스크립트 파트는 코어 루프 사이에 발생하는 대사, 캐릭터 이벤트, 보스 이벤트, 감사 이벤트, 커뮤니케이션 이벤트를 표현한다.

스크립트 파트는 다음을 정의한다.

- 대사와 화자
- 표정과 포즈
- 중앙 이미지
- 화면 연출과 이펙트
- 화자 추가/제거와 위치 이동
- 말하는 사람 포커싱
- 선택지와 비용
- 이벤트 완료 후 상태 변경

스크립트 파트는 코어 루프를 대체하지 않는다.
스크립트 파트는 코어 상태를 읽고, 명시된 효과만 코어 상태에 반영한다.

------

## 2. Event Timing

스크립트 이벤트는 다음 타이밍에 발생할 수 있다.

- `Morning`
  - 검토 전후, 업무 초안 확인, 데이터 읽기, 면담, 티타임, Alert 준비
- `Afternoon`
  - 실행 중 개입, 사고 발생, 동료 반응, 사장 지시
- `Night`
  - 휴식, 관계 회복, 결과 피드백, 보고서 검토, 다음 날 준비
- `WeeklyAudit`
  - 주간 감사, AI 기본안 대비 평가, 미검토 리스크 확인
- `MonthlyEvaluation`
  - 월별 자본/평가/대체 압력 정리
- `QuarterlyEvaluation`
  - 분기 생존성 평가, 난이도 상승, 보스 기준 변경
- `YearlySettlement`
  - 연말 결산, 장기 성과, 중간 엔딩 또는 다음 해 진입

이벤트는 선택형일 수도 있고 자동 재생형일 수도 있다.
선택형 이벤트는 플레이어가 진입을 거절할 수 있어야 한다.
자동 재생형 이벤트는 짧고, 코어 판단을 막는 반복 연출이 되어서는 안 된다.

------

## 3. Script Event Data

시나리오 이벤트는 **하나의 이벤트가 하나의 파일/에셋 단위**다.
한 이벤트 파일 안에는 수많은 대사/연출 행이 순서대로 들어간다.

Unity 구현 기준 원형은 `ScenarioEventDefinition` ScriptableObject다.

스크립트 이벤트는 최소한 아래 정보를 가진다.

- `EventId`
  - 이벤트 식별자
- `Timing`
  - 발생 가능한 루프 타이밍
- `Priority`
  - 같은 타이밍에 여러 이벤트가 있을 때의 우선순위
- `TriggerConditions`
  - 관계, 캐릭터 상태, 업무 태그, 보스 이벤트, 감사 결과, 자본 상태, AI 대체 압력 등
- `EntryCost`
  - 이벤트 진입에 필요한 시간, 집중도, 자본, 신뢰, 관계 자원
- `TextTable`
  - 이 이벤트가 참조하는 별도 텍스트 데이터
- `Nodes`
  - 대사, 연출, 선택지, 상태 변경으로 구성된 행 목록
- `ExitEffects`
  - 이벤트 종료 시 적용되는 명시적 결과
- `ReplayPolicy`
  - 1회성, 쿨다운, 반복 가능 여부

이벤트는 숨은 상태를 직접 바꿔서는 안 된다.
모든 변경은 `ExitEffects` 또는 선택지 효과에 명시되어야 한다.

------

## 4. Script Rows

이벤트 파일 안의 각 행은 `ScenarioScriptLine`으로 표현한다.

행은 다음 정보를 가진다.

- `LineId`
  - 이벤트 파일 안에서 행을 식별하는 id
- `Kind`
  - `Dialogue`, `Narration`, `Stage`, `Choice`, `Effect`, `StateEffect`
- `SpeakerId`
  - 화자 id
- `TextKey`
  - 직접 대사 문자열이 아니라 로컬라이제이션 텍스트 key
- `ExpressionKey`
  - 표정 key
- `PoseKey`
  - 포즈 key
- `VoiceToneKey`
  - 음성/톤 key
- `CenterImage`
  - 중앙 이미지 리소스
- `StageCommands`
  - 이 행에서 실행할 연출 명령 목록
- `Choices`
  - 이 행에서 표시할 선택지 목록
- `Effects`
  - 이 행에서 적용할 명시적 상태 변경 목록

대사 텍스트는 행에 직접 저장하지 않는다.
모든 표시 텍스트는 `TextKey` 또는 선택지의 `LabelTextKey`를 통해 별도 텍스트 데이터에서 가져온다.

------

## 5. Localized Text Data

텍스트 데이터는 시나리오 이벤트와 분리한다.

Unity 구현 기준 원형은 `LocalizedTextTable` ScriptableObject다.
텍스트 테이블은 key와 언어별 값을 가진다.

필수 구조:

- `TableId`
  - 텍스트 테이블 식별자
- `DefaultLanguageKey`
  - 기본 언어 key. 예: `ko`
- `DefaultCountryCode`
  - 기본 국가 코드. 예: `KR`
- `Entries`
  - 텍스트 key 목록

각 텍스트 entry는 다음을 가진다.

- `Key`
  - 예: `scenario.tea.line.001`
- `Values`
  - 언어/국가별 텍스트 목록

각 언어 값은 다음을 가진다.

- `LanguageKey`
  - 예: `ko`, `en`, `ja`
- `CountryCode`
  - 예: `KR`, `US`, `JP`
- `Text`
  - 실제 표시 문자열

텍스트 조회 인터페이스는 `GetText(key, languageKey, countryCode)` 형태를 기본으로 한다.
정확한 언어+국가 값이 없으면 같은 언어, 기본 언어+국가, 기본 언어 순으로 fallback한다.
텍스트 key가 없으면 key 자체를 반환할 수 있다.

언어 추가는 컬럼 추가에 준하는 데이터 확장이어야 한다.
새 언어를 추가하기 위해 시나리오 이벤트 파일의 행 구조를 바꾸면 안 된다.

------

### 5.1 CSV / Spreadsheet Exchange

Localized text can be edited outside Unity through CSV export/import.

Canonical CSV columns:

- `Key`
  - stable localization key used by `ScenarioScriptLine.TextKey` and `ScenarioChoice.LabelTextKey`
- language columns
  - `ko-KR`, `en-US`, `ja-JP`, or language-only keys such as `ko`

Rules:

- CSV is an exchange format for display text only.
- Scenario timing, trigger conditions, stage commands, choices, costs, and state effects stay in `ScenarioEventDefinition`.
- Adding a language means adding a new CSV column; scenario event files do not need structural edits.
- Import maps each language column into `LocalizedTextValue.LanguageKey` and `CountryCode`.
- Export writes UTF-8 with BOM so Korean text opens cleanly in Excel.
- Spreadsheet tools may be used by importing/exporting the CSV through Excel, Google Sheets, or LibreOffice.

Current implementation:

- `LocalizedTextCsv`
  - converts `LocalizedTextTable` entries to/from CSV.
- `LocalizedTextTableEditor`
  - adds Export CSV / Import CSV buttons to the text table inspector.
- `ScenarioDataWorkshopEditor`
  - exposes selected-table CSV import/export and all-table CSV export from the scenario workshop scene.

------

## 6. Ingame Data Interfaces

시나리오 파트의 인게임 인터페이스는 데이터와 런타임을 분리한다.

필수 인터페이스:

- `ILocalizedTextSource`
  - `GetText(key, languageKey, countryCode)`
  - `TryGetText(key, languageKey, countryCode, out text)`
- `IScenarioEventDefinition`
  - `EventId`, `Timing`, `Priority`, `TextTable`, `Lines`
  - `ResolveLine(index, languageKey, countryCode)`
- `IScenarioEventProvider`
  - 루프 타이밍과 코어 상태를 받아 후보 이벤트를 제공한다.

현재 구현 범위는 데이터 에셋과 조회 인터페이스까지다.
이벤트 선택, 큐잉, UI 재생, 상태 효과 적용 런타임은 추후 구현한다.

------

## 7. Dialogue

대사는 화자, 텍스트, 감정 상태, 표시 방식을 가진다.

필수 요소:

- `SpeakerId`
  - 캐릭터, 보스, 시스템, AI, 익명 화자
- `DisplayName`
  - 화면 표시 이름
- `TextKey`
  - 대사 본문을 가리키는 로컬라이제이션 key
- `Expression`
  - 표정 키
- `Pose`
  - 포즈 또는 상태 키
- `VoiceTone`
  - 선택적 음성/톤 힌트

대사는 상태 설명만 나열해서는 안 된다.
플레이어가 코어 루프에서 다음 선택을 다르게 볼 수 있는 단서, 감정, 압박, 오해를 제공해야 한다.

------

## 8. Visual Staging

스크립트 파트는 다음 시각 요소를 사용할 수 있다.

- `Portrait`
  - 캐릭터 반신 또는 얼굴 이미지
- `Expression`
  - 표정 교체
- `CenterImage`
  - 중앙에 띄우는 사건 이미지, 문서, 장소, 오브젝트
- `Background`
  - 장소 또는 상황 배경
- `Overlay`
  - 집중선, 경고, 노이즈, 서류, 붉은 조명 등
- `Effect`
  - 폭발, 섬광, 화면 흔들림, 먼지, 글리치 등

중앙 이미지는 플레이어가 확인해야 하는 사건, 문서, 물체, 장면을 보여주는 데 사용한다.
중앙 이미지는 대사창을 가리거나 핵심 선택지를 방해해서는 안 된다.

------

## 9. Staging Commands

연출은 명령 단위로 표현한다.

허용 연출 명령:

- `AddSpeaker`
  - 화자를 화면에 추가한다.
- `RemoveSpeaker`
  - 화자를 화면에서 제거한다.
- `MoveSpeaker`
  - 화자 위치를 변경한다.
- `FocusSpeaker`
  - 말하는 사람을 강조하고 나머지를 딤 처리한다.
- `SetExpression`
  - 표정을 변경한다.
- `SetPose`
  - 포즈를 변경한다.
- `ShowCenterImage`
  - 중앙 이미지를 표시한다.
- `HideCenterImage`
  - 중앙 이미지를 제거한다.
- `Shake`
  - 화면 또는 특정 캐릭터를 흔든다.
- `Collapse`
  - 쓰러짐, 주저앉음, 흔들림 같은 모션을 표시한다.
- `ShowSpeedLines`
  - 집중선 또는 긴장선을 표시한다.
- `ShowEffect`
  - 폭발, 글리치, 섬광 등 이펙트를 표시한다.
- `DimOthers`
  - 포커스 대상 외 화자를 어둡게 한다.
- `ClearStage`
  - 장면의 임시 이미지와 이펙트를 정리한다.

연출 명령은 코어 상태를 직접 변경하지 않는다.
상태 변경은 선택지 효과 또는 이벤트 종료 효과로만 적용한다.

------

## 10. Choices and Costs

스크립트 이벤트는 선택지를 가질 수 있다.

선택지는 다음 정보를 가진다.

- `ChoiceId`
- `Label`
- `VisibleCondition`
- `Cost`
- `ImmediatePresentation`
- `Effects`

선택지 비용 예시:

- 시간
- 집중도
- 자본
- 조직 신뢰
- 캐릭터 관계 자원
- AI 대체 압력

선택지 효과 예시:

- 관계 변화
- 정보 스코프 변화
- 업무 우선순위 변화
- Alert 플래그
- 감사 후보 생성
- 자본 손실 또는 회복
- 보스 평가 태그 추가
- 캐릭터 기억 추가

선택지는 단순 호감도 상승 버튼이 되어서는 안 된다.
좋은 선택은 대개 비용, 기회비용, 후속 리스크를 함께 가져야 한다.

------

## 11. Speaker Layout

화자는 화면에 추가되거나 제거될 수 있으며, 인원 변화에 따라 위치가 재배치된다.

기본 원칙:

- 화자 1명은 중앙 또는 약간 좌측에 둔다.
- 화자 2명은 좌우 대립 구도를 사용한다.
- 화자 3명 이상은 중심 화자와 보조 화자를 구분한다.
- 새 화자가 추가되면 기존 화자는 겹치지 않게 이동한다.
- 화자가 제거되면 남은 화자는 자연스럽게 재정렬된다.
- 말하는 사람은 포커싱하고, 나머지는 딤 처리할 수 있다.

화자 배치는 대사 이해를 돕는 기능이어야 한다.
화려한 이동이 대사와 선택지를 읽는 시간을 방해해서는 안 된다.

------

## 12. Core State Boundary

스크립트 파트는 다음 코어 상태를 읽을 수 있다.

- Day, Week, Month, Quarter, Year
- 오전/오후/밤 타이밍
- 업무 큐와 업무 태그
- 감사 평가 누적
- AI 대체 압력
- 자본 상태
- 보스 유형과 보스 이벤트 압력
- 캐릭터 관계, 기억, 피로, 스트레스, 정보 스코프

스크립트 파트가 변경할 수 있는 상태는 명시된 효과로 제한한다.
불명확한 임의 변경, 숨은 자동 보상, 표시되지 않은 비용 차감은 금지한다.

------

## 13. Implementation Direction

현재 구현은 스크립트 이벤트와 로컬라이즈드 텍스트를 데이터 에셋으로 생산하는 초기 단계다.

구현된 구조:

- `ScriptEventDefinition`
  - 이벤트 원형
- `ScenarioScriptLine`
  - 대사, 연출, 선택지, 상태 변경 행
- `ScenarioStageCommand`
  - 화면 연출 명령
- `ScenarioChoice`
  - 선택지와 비용/효과
- `ScenarioCondition`
  - 진입 조건과 선택지 표시 조건
- `LocalizedTextTable`
  - key와 언어/국가별 텍스트 값
- `ScenarioDataWorkshop`
  - 시나리오 데이터 제작용 씬 컴포넌트
- `ScenarioDataWorkshopEditor`
  - 별도 시나리오 도구 씬, 빈 에셋 생성, 샘플 시나리오/텍스트 생성, CSV export/import 메뉴
- `LocalizedTextCsv`
  - 로컬라이즈드 텍스트 테이블의 CSV 스프레드시트 변환
- `LocalizedTextTableEditor`
  - 텍스트 테이블 인스펙터의 CSV export/import 도구

시나리오 데이터 제작은 `Assets/Scenes/ScenarioDataWorkshop.unity`에서 수행한다.
워크샵은 기본 출력 폴더 `Assets/Resources/CaseReviewData/Scenarios` 아래에 `Events`, `Text`, `Render` 폴더를 만들 수 있다.
워크샵에서 일괄 export한 CSV는 `Assets/Resources/CaseReviewData/Scenarios/TextCsv` 아래에 저장한다.

아직 미구현인 범위:

- 시나리오 이벤트 후보 큐잉
- 시나리오 UI 재생
- 선택지 선택 처리
- `ScenarioStateEffect`를 실제 코어 상태에 적용하는 런타임

스크립트 런타임은 코어 게임 로직과 분리하되, 코어 상태 변경은 공용 변경 인터페이스를 통해 적용한다.

------

## 14. Prohibitions

다음은 금지한다.

- 스크립트 파트를 코어 루프와 무관한 감상 전용 모드로 만드는 것
- 스크립트 이벤트가 명시되지 않은 자원, 관계, 평가, 업무 상태를 변경하는 것
- 모든 커뮤니케이션 이벤트를 무료 호감도 상승 이벤트로 만드는 것
- 자동 재생 이벤트가 반복적으로 플레이어의 업무 판단 흐름을 끊는 것
- 중앙 이미지나 이펙트가 대사와 선택지를 가리는 것
- 연출 명령이 게임 규칙 판정을 직접 수행하는 것
