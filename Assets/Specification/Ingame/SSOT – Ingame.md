# SSOT – Ingame

본 문서는 **외행성재척지원실 3과**의 Ingame 시스템에 대한 단일 진실원(Source of Truth)이다.

`Project_W`는 개발 코드명이며, 본 문서의 규칙은 현재 구현과 향후 구현의 기준이다.
구현이 본 문서와 충돌할 경우 본 문서를 우선한다.

캐릭터 세부 규칙은 `Assets/Specification/Ingame/SSOT – Characters.md`를 따른다.
캐릭터 보유 요소는 `Assets/Specification/Ingame/SSOT – Character Possessions.md`를 따른다.
캐릭터 기억과 관계는 `Assets/Specification/Ingame/SSOT – Character Memory and Relationships.md`를 따른다.
스크립트 파트와 연출 규칙은 `Assets/Specification/Ingame/SSOT – Script Presentation.md`를 따른다.

------

## 1. Core Premise

플레이어는 외행성 재척 지원 조직의 중간관리자다.

플레이어의 목적은 인력을 적절하게 관리하고,
AI 제안과 인간적 검토 사이에서 비용을 조절하여,
자신이 AI로 대체될 수 없는 관리 가치를 가진다는 것을 증명하는 것이다.

이 게임은 관찰 중심 자동 서사 게임이 아니다.
현재 기준은 **업무 배치, 서류 검토, 덱 기반 개체 행동, 피드백**을 중심으로 하는 관리 시뮬레이션이다.

------

## 2. Core Loop

게임은 일일 업무를 반복하며, 상위 주기에서 감사와 평가를 받는 장기 생존 루프다.

상위 진행 구조:

1. `Daily Work`
   - 오전 검토, 오후 실행, 밤 휴식으로 하루를 운영한다.
   - 업무 배치, 검토, 커뮤니케이션, 자원 소모, 결과 피드백이 발생한다.
2. `Weekly Audit`
   - 한 주 동안의 선택을 감사한다.
   - AI 기본안 대비 플레이어 선택, 미검토 보고서, Alert, 잠복 리스크, 보스 이벤트 후속 처리를 확인한다.
3. `Monthly Evaluation`
   - 자본 상태, 조직 상태, AI 대체 압력, 감사 평가 누적을 월 단위로 정리한다.
   - 사장은 월별 성과를 보고 다음 달의 평가 기준과 보스 이벤트 압력을 조정할 수 있다.
4. `Quarterly Evaluation`
   - 분기 단위로 생존 가능성과 대체 위험을 크게 갱신한다.
   - 누적 손실, 누적 평가, 인력 상태, AI 도입 단계가 다음 분기 난이도에 반영된다.
5. `Yearly Settlement`
   - 연말 정산은 장기 성과, 누적 자본 손실, AI 대체 압력, 조직 생존성을 결산한다.
   - 1년 정산은 중간 엔딩 또는 다음 해 진입 조건이 될 수 있다.

세션 길이는 6개월에서 2년 사이를 기본 범위로 한다.
2년 뒤에는 AI의 도래를 피할 수 없으며, 세션은 최종 게임오버 또는 결산 엔딩으로 종료된다.

패배 조건:

- AI에게 대체된다.
- 회사 자본의 음수값이 사장이 감당 가능한 금액을 초과한다.
- 2년 시점에 도달하여 AI 도래를 피하지 못한다.

2년 도달은 실패를 뜻하지만, 그 전까지 얼마나 오래 버텼는지, 어떤 평가를 누적했는지, 어떤 인력과 관계를 남겼는지는 결산 가치로 사용할 수 있다.

------

## 3. Daily Loop

하루는 오전, 오후, 밤으로 진행한다.

1. `Morning / Review`
   - 개체별 행동 덱에서 카드가 1장씩 제시된다.
   - AI 또는 이전 계획이 업무 배치 초안을 제안한다.
   - 플레이어는 문서 열람, 데이터 읽기, 면담, 티타임, 보고서 검토, Alert 준비 등을 선택한다.
   - 모든 검토 행동은 비용을 가진다.
2. `Afternoon / Execution`
   - 플레이어는 업무를 인력, 슬롯, 또는 플랜에 배치한다.
   - 미리 만든 플랜은 컨펌만으로 실행 가능해야 한다.
   - 업무 결과는 업무 카드, 개체 카드, 특성, 친밀도 정보, 검토 수준, 사장 성향의 영향을 받는다.
3. `Night / Rest`
   - 플레이어는 결과를 검토하고 피드백한다.
   - 휴식, 회복, 관계 이벤트, 보고서 확인, 다음 날 준비가 발생한다.
   - 카드 추가, 특성 추가, 관계 변화, 폐기/재생성, AI 대체 압력 갱신이 발생한다.

오전, 오후, 밤에는 동료와의 커뮤니케이션 이벤트가 발생할 수 있다.
커뮤니케이션 이벤트는 플레이어가 선택할 수 있는 이벤트일 수도 있고, 조건 충족 시 자동으로 보여주는 이벤트일 수도 있다.
일부 커뮤니케이션 이벤트는 시간, 집중도, 자본, 신뢰, 관계 자원 등을 소모해야 진입하거나 선택지를 고를 수 있다.

스크립트 파트는 이 커뮤니케이션 이벤트와 보스 이벤트, 감사 이벤트, 캐릭터 이벤트를 표현하는 방법이다.
스크립트 파트는 코어 루프 상태를 읽을 수 있지만, 상태 변경은 명시된 보상/비용/플래그를 통해서만 수행한다.

이전 기준의 하루 구조는 아래 원칙으로 흡수한다.

1. `Morning Draft`
   - 개체별 행동 덱에서 카드가 1장씩 제시된다.
   - AI 또는 이전 계획이 업무 배치 초안을 제안한다.
2. `Review and Planning`
   - 플레이어는 문서 열람, 면담, 로그 확인, 보고서 검토 등을 선택한다.
   - 모든 검토 행동은 비용을 가진다.
3. `Assignment`
   - 플레이어는 업무를 인력, 슬롯, 또는 플랜에 배치한다.
   - 미리 만든 플랜은 컨펌만으로 실행 가능해야 한다.
4. `Execution`
   - 업무 결과는 업무 카드, 개체 카드, 특성, 친밀도 정보, 검토 수준, 사장 성향의 영향을 받는다.
5. `Feedback`
   - 플레이어는 결과를 검토하고 피드백한다.
   - 카드 추가, 특성 추가, 관계 변화, 폐기/재생성, AI 대체 압력 갱신이 발생한다.

------

### 3.1 Scenario Playback Between Loop Phases

Scenario playback is allowed between core-loop phases when a scenario event's trigger conditions and replay policy match the current state.

Rules:

- Scenario events may be triggered automatically at loop boundaries or explicitly from a UI location such as character outing, consultation, boss call, or audit briefing.
- Each scenario event must declare trigger conditions and must have playback state so the runtime can know whether it has been seen, completed, skipped, queued, or cooled down.
- Scenario playback may read core state, but core state may change only through declared state effects.
- Presentation requirements, including character panels, panel positions, effects, bottom text box typewriter playback, skip, and autoplay, are defined in `SSOT – Script Presentation.md`.

------

### 3.2 MVP Scene Workspace UI

The current MVP Scene is a runtime-generated UGUI workspace for validating the daily management loop. Its visual direction follows the Unity UI Design System in section 3.3; the old retro desktop concept is discarded.

Rules:

- The workspace uses stable navigation buttons with clear text labels.
- Connected player functions are grouped into purpose-centered panels instead of many independent popups:
  - `Current Work Dashboard`: current work status, queue, risks, selected work detail, progress, worker timeline, system diagnostics, people/work gauges, and recent logs.
  - `Today Work Plan`: daily plan entries and a floating right-side character selection panel for assignment slots.
  - `Daily Report`: resolved work and night summary popup.
  - `Character Profiling`: top ID/name character tabs, selected character face/status, today cards, and used-card state.
  - `Dev Tools`: sample scenario playback and future development-only tools.
- Windows are draggable, resizable, remember their current layout for the session, and enforce a minimum size.
- Window contents must support vertical scrolling by default.
- Runtime text follows section 3.3 typography: title 32-40 px, section 24-28 px, body 18-22 px, button 20-24 px, caption 16 px or larger.
- Navigation entries are stable and do not appear/disappear by phase. If a panel is opened at a phase that does not allow editing, it shows the latest available information read-only, or an explicit empty state if no information exists.
- Assignment slots in `Today Work Plan` open a separate right-side floating character picker panel. Each picker row shows a face placeholder, name/id, and compact status.
- Floating picker panels follow their owner window when the owner window is dragged or resized.
- Characters that cannot be inserted into a picker row are dimmed in that picker. `Character Profiling` keeps a consistent layout regardless of assignment context.
- Character tabs show only ID and name. For up to 12 characters, the tab area should wrap into roughly two or three rows when the window is narrow.
- `Today Work Plan` decides assignment only. `Start Work` and `Advance Day` live as fixed lower-right action buttons. Report summary is opened from `Daily Report`, not from `Today Work Plan`.
- `Today Work Plan` may show advisory card forecasts for the current assignment. These forecasts are read-only: they show the most likely attitude card, estimated use chance, and predicted `Outcome/Risk` delta, but they do not apply a card or confirm work.
- Character selection state may be shared between `Character Profiling` and `Today Work Plan`, but core state changes still pass through `CaseReviewGame.Dispatch` or explicit assignment sync boundaries.
- Workspace presentation may reorganize panels, but it must not replace the daily loop: review, assignment, confirmation, execution feedback, night summary, next morning.
- `My Intranet Page`: personal intranet page that consolidates player resources, merit tokens, approval history, relationship watch records, and mail/inbox style notices.

------

### 3.3 Unity UI Design System

All Unity runtime UI must default to mobile-readable, data-growth-safe layout.

Priority:

1. Readability over information density.
2. Scrollable content over compressed content.
3. Auto Layout over manually positioned children.
4. Dynamic text and data growth over fixed mock data assumptions.

Typography:

- Title text should use 32-40 px.
- Section text should use 24-28 px.
- Body text should use 18-22 px.
- Button text should use 20-24 px.
- Caption text should be 16 px or larger.
- Text below 16 px is forbidden unless a specific technical exception is documented near the implementation.

Text handling:

- Text length must be treated as variable because of localization, dynamic data, and user input.
- Text must not render outside its parent bounds.
- Text should wrap where content reading matters.
- Non-critical overflow should truncate or use ellipsis.
- Auto Size should not be the primary solution; containers should grow or scroll instead.

Scrollable layout:

- Any UI area that can receive dynamic lists, logs, work entries, characters, cards, mail, reports, or localized text must be built as a Scroll View from the first implementation.
- Preferred structure is `Canvas -> Scroll View -> Vertical Layout Group -> Content Size Fitter -> Item...`.
- A list must be expected to grow from 3 items to 300 items without changing the scene structure.

Layout:

- Prefer `VerticalLayoutGroup`, `HorizontalLayoutGroup`, `GridLayoutGroup`, `LayoutElement`, and `ContentSizeFitter`.
- Manual position should be reserved for top-level surfaces, overlays, drag/resize chrome, and intentionally anchored action areas.
- Anchors and layout constraints must prevent overlap under long text, large numbers, translation expansion, resolution changes, and Safe Area changes.

Spacing:

- Outer margin should generally be 24-32 px.
- Section spacing should generally be 24 px.
- Component spacing should generally be 16 px.
- Label-to-input spacing should generally be 8-12 px.
- Button internal padding should be 16 px or larger.

Color and decoration:

- Default background is white-based.
- Recommended palette: background `#FFFFFF`, surface `#F7F7F7`, border `#DDDDDD`, primary text `#222222`, secondary text `#666666`, disabled `#AAAAAA`.
- Use only one low-saturation blue or green accent family by default.
- Prefer thin borders, weak shadows, and small radius.
- When a drawn panel, slot, card, portrait, nameplate, or button frame resource is used, it must be imported as a Sprite with 9-slice borders and applied behind content through `Image.Type.Sliced`, not by hardcoding stretched raw images. Button hover/pressed/disabled art should be wired through `Button.spriteState` when available. Desktop and control icons may use decorative Sprite images, but labels must remain visible unless the icon is universally obvious, such as the close button. The black outline should read as a thin hand-drawn frame, not as a thick decorative border, and the area outside the black outline must be transparent.
- Avoid strong gradients, glow, neon, and heavy shadow.

Controls and states:

- Touch buttons must be at least 56 px high and should usually fill the parent width in mobile layouts.
- Inputs must have explicit labels; placeholder-only instructions are not allowed.
- Long text input must use multiline fields.
- Icons must not be the only carrier of meaning; pair icons with text when the icon is not universally obvious.
- State must not be represented by color alone; pair it with text or a symbol such as done, progress, or warning.

Animation:

- UI animation should support function and normally stay in the 150-250 ms range.
- Bounce, shake, flash, and similar attention effects are not default UI language.

Review checklist:

- Mobile one-handed readability.
- Large enough fonts.
- No text outside the screen or parent.
- Long text preserves layout.
- Scroll View exists where dynamic content can grow.
- Auto Layout is used for repeated and dynamic content.
- No incoherent overlap.
- Adequate spacing.
- Safe Area considered.
- White-based restrained palette.
- Data can grow by 10x without scene restructuring.

------

## 4. Cards and Decks

Current implementation rule:

- Daily card hand size is 3 cards in the MVP workspace loop.
- A card is an attitude toward the assigned work, not a source of work injury. Injury and disability risk belongs to `Work/EventCase`.
- Card use is probability-weighted, not uniform random. The weight is derived from the card tags, work tags/card hooks, and the assigned character's current mood state.
- Mood state is inferred from runtime character state such as injury, fatigue, mental stress, trust to manager, and stagnation.
- The UI may show the most likely card and expected `Outcome/Risk` result before confirmation. This is forecast text only; the actual selected card is still resolved when morning work starts.

각 개체는 행동 덱을 가진다.

- 하루 시작 시 개체마다 행동 카드 3장을 들고 온다.
- 행동 카드는 그날 해당 개체가 업무에 반응하는 주요 경향이다.
- 카드는 성공 보정만이 아니라 사고, 과잉 대응, 회피, 보고 왜곡, 협업, 성장 기회도 포함한다.
- 경험이 쌓인 개체는 카드 또는 특성이 추가된다.
- 덱 성장은 플레이어가 관리한 이력의 산물이어야 한다.

업무도 카드처럼 취급할 수 있다.

- 업무 카드는 위험도, 긴급도, 요구 적성, 검토 필요도, 폭발 가능성을 가진다.
- 같은 업무라도 배치 대상과 검토 수준에 따라 결과가 달라져야 한다.

------

## 5. Review Cost

검토는 공짜가 아니다.

다음 행동은 반드시 비용을 가진다.

- 서류 읽기
- AI 요약 원문 대조
- 개체 면담
- 업무 로그 확인
- 보고서 개별 검토
- 사장 대응 문서 작성

비용 종류:

- 시간
- 자원
- 플레이어 체력 또는 집중도
- 조직 신뢰
- AI 대체 압력

설계 원칙:

- 모든 것을 검토하는 플레이는 늦어서 실패해야 한다.
- 아무것도 검토하지 않는 플레이는 AI 대체 또는 사고 누적으로 실패해야 한다.
- 플레이어는 "무엇을 검토하지 않을 것인가"를 계속 결정해야 한다.

------

## 6. AI Suggestions and Replacement Pressure

AI는 업무 배치 초안과 요약을 제공할 수 있다.

- AI 제안은 빠르고 대체로 그럴듯해야 한다.
- AI 제안은 일부 상황에서 위험 신호를 누락하거나 조직 맥락을 오판할 수 있다.
- 플레이어가 AI 제안을 검토 없이 계속 컨펌하면 AI 대체 압력이 오른다.
- 플레이어가 의미 있는 예외 판단을 수행하면 AI 대체 압력을 낮출 수 있다.

AI 대체 압력은 패배 조건 또는 엔딩 분기 조건으로 사용한다.

------

## 7. Affinity and Information Scope

친밀도는 개체와 플레이어 사이의 정보 스코프를 결정한다.

정보 스코프 단계:

- `Surface`: 업무 표면 정보, AI 요약, 기본 상태만 표시
- `Working`: 최근 피로, 업무 선호, 일부 행동 경향 표시
- `Trusted`: 카드 의도, 숨은 리스크, 보고 왜곡 가능성 일부 표시
- `Compromised`: 과도한 친밀 또는 의존으로 인해 판단 왜곡 가능성 표시

친밀도는 무조건 이득이 아니다.
정보가 늘어나는 대신 관리 비용과 감정적 부담이 증가할 수 있다.

------

## 8. Clone Bay

개체는 클론 베이에 존재한다.

- 폐기할 수 있다.
- 재생성할 수 있다.
- 폐기와 재생성은 성장 손실, 비용, 평판 리스크, 조직 신뢰 하락을 만든다.
- 경험 많은 개체는 위험과 가치가 함께 증가해야 한다.
- 재생성은 같은 `CloneLineageId`의 새 개체를 만드는 기능이며, 이전 개체의 기억과 관계를 그대로 복사하지 않는다.
- 재생성 대상의 활성 관계/기억은 제거 또는 보관 이력으로 이동하고, 새 개체에는 제한된 카드/퍽/특성 샘플과 계보 잔향만 남길 수 있다.
- 주변 캐릭터와 조직 기록은 재생성 사건을 기억할 수 있으므로, 재생성은 대상 개체의 리셋일 수 있어도 조직 전체의 리셋은 아니다.

클론 시스템은 단순한 리셋 버튼이 아니다.
인력 관리 실패를 비용으로 환산하는 장치다.

------

## 9. Boss Archetypes

사장은 세션의 평가 기준과 난이도를 바꾼다.

초기 사장 유형:

- `Rational AI`: 수치 최적화와 예측 가능성을 요구한다.
- `Competent Operator`: 결과와 속도를 우선한다.
- `Ordinary Human`: 감정과 체면에 흔들린다.
- `Tech Hipster`: 자동화와 새 도구를 과대평가한다.
- `Psycho AI`: 인간적 비용을 무시하고 극단적 효율을 요구한다.

사장 성향은 다음에 영향을 준다.

- AI 대체 압력 증가량
- 보고서 평가 기준
- 실패 허용 범위
- 인력 폐기/재생성에 대한 반응
- 검토 지연에 대한 패널티

------

## 10. Boss Event Loop

보스 이벤트는 보스가 일을 벌이는 상위 사건이다.

보스 이벤트는 단순한 업무 카드 1장이 아니라, 세션 난이도와 평가 기준이 실제 하루 루프에 개입하는 방식이다.
이벤트는 다음 상태를 갱신할 수 있다.

- 자본 또는 운용 자원
- 조직 상태
- 현재 할일과 업무 큐
- 누적 잠복 리스크
- AI 대체 압력
- 보스 평가 기준 또는 관심 태그

보스 이벤트는 즉시 완전 공개되어서는 안 된다.
플레이어는 이벤트의 영향을 업무, 로그, 보고서, 면담 단서, AI 요약의 누락을 통해 점진적으로 읽어야 한다.

보스 이벤트의 결과는 `SSOT – Work.md`의 동적 업무 생성 규칙을 통해 `boss`, `audit`, `ai`, `emergency`, `morale`, `legal` 태그 업무로 분해될 수 있다.

------

## 11. Investigation Actions

플레이어는 현재 상황을 검토하고, 비용을 지불해 플랜 또는 우선순위를 바꿀 수 있다.

기본 조사 행동:

- `검토`
  - 업무 표면 정보와 AI 요약을 확인한다.
  - 낮은 비용으로 시작하지만 숨은 리스크를 충분히 드러내지 못할 수 있다.
- `데이터 읽기`
  - 원문, 로그, 수치, 이력 데이터를 확인한다.
  - AI 요약의 누락과 왜곡을 찾는 핵심 행동이다.
- `면담하기`
  - 개체 또는 담당자에게 직접 맥락을 묻는다.
  - 정보 스코프를 넓힐 수 있지만 시간, 집중도, 관계 비용을 가진다.
- `티타임하기`
  - 낮은 공식성으로 관계와 정서적 맥락을 얻는다.
  - 단순 호감도 버튼이 아니라, 공식 문서에 남지 않는 단서를 얻는 행동이다.
- `Alert`
  - 위험을 사장에게 상신한다.
  - 위험을 조기에 올려 평가 방어를 만들 수 있지만, 보스 성향에 따라 무능, 책임 전가, 과잉 보고로 평가될 수 있다.

모든 조사 행동은 시간, 자원, 집중도, 조직 신뢰, AI 대체 압력 중 하나 이상의 비용을 가진다.
플레이어는 모든 것을 조사할 수 없으며, 무엇을 조사하지 않을지도 선택해야 한다.

------

## 12. Audit and Evaluation

감사 시스템은 플레이어 선택이 AI 기본안보다 나았는지 평가한다.

AI 기본안은 복잡한 인격형 AI가 아니다.
MVP 기준 AI는 다음 원칙을 따른다.

- 빈 업무 또는 빈 슬롯만 자동으로 채운다.
- 놀고 있는 인력이 있으면 비어 있는 곳에 우선 배치한다.
- 기본적으로 기존에 계획된 일을 유지한다.
- 명확한 결원이 없으면 플랜을 크게 바꾸지 않는다.

평가 시스템은 주요 선택마다 `AI 기본안`과 `플레이어 선택`을 비교한다.

비교 대상:

- 업무 배치
- 플랜 유지 또는 수정
- 업무 우선순위 변경
- 검토 여부와 검토 깊이
- Alert 여부
- 결과 피드백과 보고서 검토 여부

평가 결과:

- `+`
  - 플레이어 선택이 AI 기본안보다 나았다.
  - 평가 점수가 오르고 AI 대체 압력이 낮아질 수 있다.
- `0`
  - 플레이어 선택과 AI 기본안의 차이가 의미 없거나 판단 불충분하다.
- `-`
  - 플레이어 선택이 AI 기본안보다 나빴다.
  - 평가 점수가 낮아지고, AI 대체 압력, 감사 업무, 보스 불신이 오를 수 있다.

평가는 결과 성공률만 보지 않는다.
보스 성향, 중요도, 잠복 리스크, 관계 손상, 검토 비용, 보고 가능성, Alert의 타이밍을 함께 본다.

------

## 13. Survival and Replacement

플레이어의 장기 목표는 AI보다 나은 관리 가치를 누적 증명하는 것이다.

평가 점수는 세션 동안 누적된다.
누적 평가가 충분하면 플레이어는 생존한다.
누적 평가가 낮거나 AI 대체 압력이 임계치를 넘으면 플레이어는 대체된다.

생존과 대체는 단순 승패뿐 아니라 엔딩 분기 조건으로 사용할 수 있다.
보스 유형은 생존 기준, 대체 압력 증가량, 어떤 `+` 판단을 더 크게 인정하는지를 바꿀 수 있다.

------

## 14. Current Implementation Mapping

현재 Unity 구현은 `CaseReview` MVP를 통해 다음 요소를 부분 구현한다.

- `GameState`, `EventCase`, `Personnel`: 업무, 인력, 상태 모델
- `MorningPlan`: 아침 계획과 컨펌 구조
- `Dispatch`: 명령 기반 검토/배치/보고 루프
- `ReportGenerator`: 결과 보고서와 검토 압박
- `CaseReviewPlaySceneController`: 관리실 UI 프로토타입
- `CaseReviewDatabase`: ScriptableObject 기반 시드 데이터

현재 구현은 아직 다음 요소를 완전 구현하지 않는다.

- 개체별 행동 덱
- 카드 성장
- 친밀도별 정보 스코프
- 사장 유형
- AI 대체 압력
- 클론 폐기/재생성
- 보스 이벤트
- AI 기본안 대비 감사 평가
- 생존/대체 누적 평가
- 주간/월간/분기/연간 평가 루프
- 커뮤니케이션 이벤트와 스크립트 파트

향후 구현은 위 누락 요소를 본 문서 기준에 맞춰 추가한다.

------

## 15. Decision Ledger

본 섹션은 과거 PM Log에서 현재 Ingame 규칙으로 흡수된 핵심 결정만 보존한다.
일정, 회고, 증빙성 기록은 이 문서의 권위 범위가 아니며, 규칙으로 필요한 내용만 여기에 남긴다.

### 2026-06-03 – Current Direction

- 작업 제목은 **외행성재척지원실 3과**다.
- 장르는 PM 관리 경험 기반 블랙코미디 업무 배치 시뮬레이션이다.
- 현재 기준은 순수 관찰형 자동 서사가 아니라 **Papers Please + deckbuilding** 구조다.
- 플레이어는 플랜 수립, 업무 배치, 결과 검토, 피드백을 수행한다.
- 모든 검토 행위는 비용을 가진다.
- AI 제안을 검토 없이 계속 컨펌하면 AI 대체 압력이 상승한다.
- 보스 이벤트는 자본 상태, 조직 상태, 할일, 평가 기준을 흔드는 상위 사건이다.
- 감사 평가는 플레이어 선택을 AI 기본안과 비교하며, 누적 결과가 생존/대체를 결정한다.
- MVP AI는 복잡한 판단을 하지 않고 빈 곳 보충과 기존 플랜 유지에 집중한다.
- 코어 루프는 일일 업무, 주간 감사, 월별 평가, 분기별 평가, 일년 정산으로 확장한다.
- 일일 업무는 오전 검토, 오후 실행, 밤 휴식으로 진행한다.
- 6개월에서 2년 사이의 장기 루프를 기본 범위로 하며, 2년 뒤 AI 도래는 피할 수 없는 종료 조건이다.
- 커뮤니케이션 이벤트와 스크립트 파트는 코어 루프 상태를 읽고, 명시된 비용/보상/플래그만 상태에 반영한다.

### 2026-06-03 – Work Data Direction

- 업무는 중요도, 업무량, 리스크, 태그, 요구 적성, 동시작업 가능수를 가진다.
- 업무는 카드/퍽/보스/기억/보고서와 태그 기반으로 상호작용해야 한다.
- 업무는 기본적으로 스크립트에 의해 동적으로 생성될 수 있어야 한다.
- 동적 생성 시 난이도, Day, 보스 유형, 조직 상태, 이전 실패, AI 대체 압력에 따라 등장 가중치가 달라져야 한다.
- 세부 규칙은 `Assets/Specification/Ingame/SSOT – Work.md`를 따른다.

### 2026-06-03 – Character Data Direction

- 캐릭터는 행동 덱, 카드, 퍽, 관계, 기억, 정보 스코프를 가진다.
- 캐릭터 base와 인게임 runtime data는 분리한다.
- 캐릭터 base, runtime data, 카드, 퍽, 렌더링 리소스는 ScriptableObject로 생산 가능해야 한다.
- 카드와 퍽은 단건 에셋으로 만들고, 캐릭터 데이터에 쉽게 추가/제거할 수 있어야 한다.
- 관계와 기억은 캐릭터마다 저장 가능해야 한다.

### 2026-06-03 – Clone and Growth Direction

- 클론은 정을 붙일 수 있지만 대체불가능한 유일 존재는 아니다.
- 클론 폐기/재생성은 가능하지만 비용 없는 리셋이 아니다.
- 성장은 단순 레벨업이 아니라 카드, 퍽, 특성, 위험 습관, 기억의 축적이다.
- 재생성은 기억/관계의 완전 이전도, 완전 삭제도 아니다. 대상 개체의 직접 기억/관계는 사라지거나 보관되지만, 계보 잔향과 주변 캐릭터의 기억은 남을 수 있다.

### MVP Decision Gate

MVP 판정은 재미 가설과 최소 KPI를 함께 본다.

- 주요 재미 가설: 지연 개입 긴장감, 관계 붕괴/복구 관찰 흥미, 검토 비용 선택의 압박.
- 주요 KPI: 사이클 완주율, 실패 후 재도전율, 과도한 전멸/무력감 비율.
- No-Go 시에는 개입 피드백 가시성, 실패 보상 체감, 검토 비용 밸런스를 우선 조정한다.

------

## 16. Prohibitions

다음 방향은 현재 SSOT와 충돌한다.

- 게임을 순수 관찰형 자동 서사 게임으로 되돌리는 것
- 검토 비용이 없는 완전 정보 UI를 기본값으로 삼는 것
- AI 제안을 항상 정답으로 만드는 것
- 폐기/재생성을 비용 없는 리셋으로 만드는 것
- 카드 성장을 단순 능력치 상승으로만 처리하는 것
- 보스 이벤트를 단순 랜덤 업무 생성으로만 축소하는 것
- 감사 평가를 보스 취향 점수나 절대 성공률만으로 처리하는 것
- MVP AI를 플레이어처럼 복잡하게 의사결정하는 별도 시뮬레이터로 만드는 것
- 주간/월간/분기/연간 평가를 단순 텍스트 요약으로만 처리하고 상태 갱신을 막는 것
- 스크립트 파트를 코어 상태와 무관한 별도 감상 모드로만 만드는 것
- 스크립트 이벤트가 명시되지 않은 방식으로 자원, 평가, 관계, 업무 상태를 변경하는 것
------

## Merit Tokens and Approval Requests

The MVP management loop uses a single visible resource called Merit Tokens.

Merit Tokens are both:

- a reward for successfully completing work, and
- a consolation fund that gives the player a recovery foothold when risk bursts or a project fails.

The player spends Merit Tokens on approval requests. Approval requests are not natural-language documents. They are simple filed forms with a target, a required token count, submitted tokens, status, and a short review hint.

Approval request examples:

- `Regeneration`: clone/personnel regeneration request.
- `ReportCorrection`: report correction or filing cleanup.
- `AuditDefense`: audit defense or failure containment.
- `SpecialExpense`: special spending, equipment, outsourcing, or exceptional resource use.

The approval desk may reject or conditionally approve a request even when the visible token count looks sufficient. Rejection hints must expose company state indirectly rather than dumping exact hidden formulas. Example hints include:

- `AI review hold`: AI replacement pressure is becoming relevant.
- `audit line transfer`: latent risk is becoming relevant.
- `operation capacity shortage`: overload is becoming relevant.

This system must preserve the MVP workspace loop. `Today Work Plan` remains assignment-only, while irreversible actions use fixed action buttons, approval windows, or document-like panels.
