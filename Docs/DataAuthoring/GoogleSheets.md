# 시트에서 데이터 편집하기

Spec Ref: `Assets/Specification/DataAuthoring/GoogleSheetsContent.md`

- 폴더: https://drive.google.com/drive/folders/1JQPBe2_LgY8YuixfqXVC6PhTUC7x1BSz
- 문구: https://docs.google.com/spreadsheets/d/1eIdY31tip9qJNvoc8nxHl25ZIRW7e_9EmdkI851w6No/edit
- 데이터: https://docs.google.com/spreadsheets/d/1uOFm5t18aEB_jEHs_R5rtvJfj15Q7r_grSeXwLOxtvU/edit

## 편집

문구는 Texts 탭의 ko/en을 편집합니다. tid는 연결에 사용하므로 유지합니다.
빈 셀은 번역 누락으로 취급하며 한국어, tid 순으로 대체합니다.
의도적으로 아무것도 표시하지 않으려면 `<EMPTY>`를 입력합니다.
`{day}`, `{name}` 같은 변수는 언어별로 동일하게 유지합니다.

게임 데이터는 탭마다 한 종류입니다. Characters에서 능력을 수정하고 CharacterPerks에서
인물에 퍽을 연결합니다. Perks는 퍽의 이름·설명을 연결하고 PerkEffects가 실제 효과를 정의합니다.
Events의 base_probability는 0~1, Tasks의 difficulty와 인물 능력은 0~100입니다.
PerkEffects의 task_success/add 값은 %p입니다. 여러 tags_all은 세미콜론으로 구분하며
모두 일치해야 효과가 적용됩니다. Relationships는 방향이 있는 관계로 각 방향을 한 행씩 씁니다.
required_flags는 호출자가 제공하는 현재 상황 태그입니다. 시트에서 실행 코드를 입력하지 않습니다.

OfficeTextGroups는 기존 7일 이야기의 문구 연결입니다. group과 position은 유지하며,
새 이야기는 아직 이 고정 진행 모델에서 자동 생성되지 않습니다. 새 캐릭터/퍽/작업/상황은
별도 샘플 카탈로그와 판정 API로 제공되며 기존 사무소의 하나·소리·유나를 대체하지 않습니다.
현재 언어 옵션은 사무소 설정에 있습니다. 기존 캠페인 화면의 전체 번역은 이 이관에 포함되지 않습니다.

### 7일 업무 서사 개정 (2026-10-02)

Texts의 `office.roles.*`는 인물 성격, `office.bodies.*`는 요청·누락 사항,
`office.options.*`와 `office.reasons.*`는 처리안·비용, `office.results.*`는 반응·미결 업무를 담습니다.
하나는 예의와 권리 주장, 소리는 무기력과 연락 부담, 유나는 갸루 말투와 규정 반발을 함께 보여줍니다.
1일 일정 충돌 → 2일 결석 → 3일 서류 보완 → 4일 현장 통제 → 5일 인계 범위 →
6일 후속 업무 → 7일 정산으로 반복 등장합니다. 공통 후속은 두 이전 분기 모두와 양립해야 합니다.
분기별 결과는 활동 기록에 남으며 별도 호감도·조건부 후속 엔진은 없습니다.
기존 저장도 새 문구를 표시합니다. 첫날부터 보려면 사무소 설정에서 7일 시나리오를 다시 시작합니다.
관리자 UI는 접수·승인·보류·이관 중심으로 작성하며 자동 감사·회복·화해를 결말로 강제하지 않습니다.

## 빌드

두 시트의 공유는 `링크가 있는 모든 사용자 / 뷰어`여야 합니다. 편집 권한은 별도입니다.
빌드 머신에는 Python 3가 필요합니다. PATH의 python을 사용하며, 필요하면
`PROJECTW_PYTHON` 환경 변수로 실행 파일을 지정할 수 있습니다. 추가 Python 패키지는 없습니다.

기존 `tools/Build-WebPreview.ps1` 또는 Unity의 WebGL 빌드 모두 공통 전처리 게이트를 통과합니다.
각 시트를 XLSX로 한 번에 내려받아 표를 추출하고, 전체 쌍을 검증한 다음
`Assets/MilestonePrototype/Resources/sheet-content.json` 하나로 변환합니다.
사람이 편집하는 원본은 Google Sheets이며 JSON은 게임용 생성물입니다.
수정 내용은 다음 빌드·배포에 반영되고 실행 중인 게임은 인터넷에서 시트를 읽지 않습니다.
시트 이름이나 폴더 위치를 바꿔도 ID가 같으면 연결됩니다. 탭 이름과 열 이름은 유지합니다.

직접 가져오기: `python -X utf8 tools/sheet_content.py`

명시적 오프라인 재현: `python -X utf8 tools/sheet_content.py --local Data/Sheets`

검증 테스트: `python -X utf8 -m unittest discover -s tools -p test_sheet_content.py`

일반 빌드는 오프라인 데이터를 몰래 재사용하지 않습니다. 공개 권한, 네트워크, 중복 ID,
참조 누락, 번역 변수, 잘못된 수치 또는 수식 셀이 있으면 빌드를 중단합니다.
CSV 스냅샷과 원본 SHA-256은 재현·차이 확인용으로 보존합니다.
