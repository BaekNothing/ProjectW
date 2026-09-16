# itch.io desktop browser preview

Spec Ref: `Assets/Specification/Operation/WebPreviewPublishing.md`.
Destination: https://baeknothing.itch.io/managingheaven

The web player compiles GameEntry into the player and uses a dedicated WebPreview scene.
It does not fetch Android patch DLLs. The existing gameplay data is bundled, and reviewed
Addressables portraits/effects use a local web catalog. Android deployment is unchanged.

## Rebuild

Close this project's Editor, then run `tools/Build-WebPreview.ps1`. Requires Unity 6000.3.8f1
with WebGL Build Support, official Unity CLI, and Butler for publication.
The builder restores shared HybridCLR, PlayerSettings, and Addressables asset configuration
after the build. Logs and build output are ignored by Git.

Run paired `ProjectW.WebPreview.Tests` EditMode tests for startup changes. Per the user's
2026-09-13 direction, build and upload through CLI and confirm delivery with `butler status`;
browser play verification is not required. The earlier 28 simulation test failures are
documented in `UnityCliTestFailures.md`.

Publish with `butler push Builds/WebGL baeknothing/managingheaven:html5 --userversion <version>`.
The itch.io upload must be marked playable in browser. Keep the project HTML, in development,
with click-to-play and fullscreen available. Saves belong to the current browser site storage;
clearing it can erase saves. Reload the page for a new web version.

## Font attribution

Noto Sans CJK KR Regular, from https://github.com/notofonts/noto-cjk,
`Sans/OTF/Korean/NotoSansCJKkr-Regular.otf`. Licensed under SIL OFL 1.1;
license is included in `Assets/WebPreview/Fonts/OFL.txt` and copied to the web build.
The scene holds the font reference; it is not put in an Android Resources folder.

## Page description

외행성 재척 지원실의 책임자가 되어 네 명의 대원과 함께 끝없이 이어지는 업무를 관리하세요.
작업을 배정하고, 마감을 맞추고, 대원들의 피로와 부상을 돌보며 한정된 자원으로 최대한 오래
버티는 운영 시뮬레이션입니다. 메일, 일정표, 대원 관리 창을 오가며 하루하루의 결정을 내려보세요.

첫 웹 테스트 버전입니다. Windows PC의 최신 Chrome 또는 Edge와 전체 화면 플레이를 권장합니다.
마우스로 창과 업무를 선택하고 휠로 스크롤합니다. 우측 하단 ‘다음날로’ 버튼으로 하루를 진행합니다.
저장은 현재 브라우저에 보관되며 사이트 데이터 삭제 시 사라질 수 있습니다.

## Validation record

- WebGL build succeeded through official Unity CLI on 2026-09-13. Final output:
  24 files, 31,925,461 bytes, longest relative path 99 characters; all itch.io HTML limits pass.
- Fixed an Addressables initialization handle lifetime error discovered during the first local
  run: retain the handle until its status is read, then release it. This is WebGL-only code.
- Before the user requested CLI-only delivery, local Chrome checks confirmed Korean text,
  day 1 to day 2 progression, save reload, and complete/modular crew portraits. The corrected
  player reported no runtime errors; Unity logged a non-blocking persistence API deprecation.
- Butler uploaded `2026.09.13-preview.1` to `baeknothing/managingheaven:html5` successfully.
  `butler status` confirmed processed build `1973499`, upload `19215817` (30.45 MiB).
- Page description and HTML embed configuration were saved earlier. The page was still Draft
  at the last edit. Browser-playable upload flag and Public visibility remain unconfirmed:
  the authenticated Chrome connection became unavailable before final page setup. No hosted
  browser verification was performed, as requested. This is an uploaded build, not a verified
  public launch; finish those two settings at https://itch.io/game/edit/5000075.

## Local tool notes

Unity CLI 1.0.0-beta.9 manages Editor shutdown itself; do not forward `-quit` to `unity run`.
The Hub module installer failed with a `writer_kind` database error on this PC. WebGL Support
was installed from the downloaded, signature-verified official Unity module installer instead.
The web bootstrap EditMode tests passed (2/2), recorded in `Logs/web-preview-tests.xml`.

## Magical-girl resource update — 2026-09-16

- Source/runtime commit: `689e2dd` on `ai-cli-itgration` (pushed).
- One generated 4x4 sheet supplies all four female magical-girl identities in four conditions.
  Sixteen 320-square textures and four healthy fallbacks replace the shipping modular set.
- Crew portrait EditMode tests: 15/15 passed. Asset source/export SHA-256 checks passed.
- WebGL builder completed successfully (`WEB_PREVIEW_BUILD_OK bytes=32169247`, Editor exit 0).
- Final upload: 24 files, 32,173,988 bytes. Built binary catalog contains all 16 condition filenames
  and the new portrait bundle; active source group has 20 portrait entries, no legacy modular entries.
- Portrait bundle: `projectwremoteportraits_assets_all_abc6f3a742128731fe2cd8cacf3e4515.bundle`,
  453,169 bytes; SHA-256 `c1be4bfb9738ad1f984bff9a5635b3b454c2720993271e40c5833f80b16bf9fb`.
- Butler push succeeded to `baeknothing/managingheaven:html5` with version
  `2026.09.16-magical-girls.1`; CLI status confirms processed build `1984018`, upload `19215817`.
- Per the latest user direction, delivery ends at resource validation and Butler status.
  No further web checks, browser-play verification or public-page configuration are required.
