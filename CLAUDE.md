# Tower (가제) — 프로젝트 메모

> 이 폴더(`dy-openwork/tower/`)는 타워 디펜스 게임 프로젝트 전용.
> 관련 대화·결정·작업물은 전부 여기에 정리한다.

## 개요
- **장르**: 3D 저폴리 타워 디펜스
- **엔진**: Unity 6 (6000.0.58f1) / C#
- **플랫폼**: 웹(WebGL). 개발·테스트는 PC 에디터, 완성 시 WebGL 빌드.
- **아트**: 자체 아트 없음 → Kenney Tower Defense Kit (CC0) 단일 스타일
- **비용**: 0원 (CC0/무료 에셋만)
- **상세 기획**: [기획서.md](기획서.md)

## 폴더 구조
```
tower/
├── CLAUDE.md          ← 이 파일 (결정·진행로그·실행법)
├── 기획서.md           ← GDD (게임 설계)
├── design_mockup.html ← 레벨 배치 목업(그레이박스) 이미지
└── game/              ← Unity 프로젝트 (★Unity Hub에서 이 폴더를 열 것)
    └── Assets/
        ├── Scripts/   ← 게임 코드 (GameManager/Enemy/Tower/Projectile/TowerSlot)
        ├── Art/kenney_tower-defense-kit/  ← Kenney TD 킷(FBX 160, CC0). UFO/SF 테마
        └── Audio/      ← Kenney 오디오 7팩(CC0): digital/impact/interface/music-jingles/rpg/sci-fi/ui
```
> ⚠️ Unity 작업은 **반드시 `C:\Users\fathe\dy-openwork\tower\game`** 프로젝트를 열 것.
>   (형님 Unity 작업폴더가 `Desktop\unity\`라 헷갈리기 쉬움 — DOTween을 실수로 `Desktop\unity\monster`에 임포트한 적 있음)
> ⚠️ DOTween(선택 도구)은 아직 tower/game에 없음 → 필요 시 tower/game 열고 Package Manager ▸ My Assets ▸ DOTween Import + Setup.

## 실행 방법 (예정 — 프로젝트 생성 후 갱신)
- Unity Hub → `tower/game` 프로젝트 열기 → ▶(Play)로 에디터에서 테스트
- WebGL 빌드: (모듈 설치 필요 시 안내 예정)

## 에셋 다운로드 목록 / 라이선스 (받으면 날짜 기록)
> 원칙: 아트는 **Kenney로 스타일 통일**. 사실적 에셋과 섞지 않음. 무료면 자유롭게 받아도 됨.
> 목업(그레이박스)은 에셋 없이도 돌아감 → 에셋은 "아트 패스" 단계에서 투입.

| 구분 | 에셋 | 라이선스 | 링크 | 받은날 |
|---|---|---|---|---|
| ★핵심 아트 | Kenney Tower Defense Kit (3D, 160) | CC0 | https://kenney.nl/assets/tower-defense-kit | (예정) |
| 사운드 | Kenney 오디오 (Interface/Impact Sounds 등) | CC0 | https://kenney.nl/assets/category:Audio | (예정) |
| UI | Kenney UI Pack | CC0 | https://kenney.nl/assets/ui-pack | (예정) |
| 도구(선택) | DOTween (HOTween v2) — 애니/UI 트윈 | 무료 | https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676 | (예정) |
| (참고) | Unity Asset Store 무료 전체 | 각 EULA | https://assetstore.unity.com/top-assets/top-free | - |

- Kenney = zip 다운로드 → 압축 풀어 `game/Assets/` 에 드래그.
- Unity Asset Store = "내 에셋에 추가" → Unity의 Package Manager(My Assets)에서 Import.

## 결정 로그
- **2026-06-22**
  - 새 게임 만들기로 함. 컨셉 후보 3개(타워디펜스/엔드리스러너/물리배달) 중 **타워 디펜스** 선택.
  - 엔진: 에픽/언리얼(무료 AAA 에셋) vs 유니티 검토 → **유니티** 선택 (클로드가 C# 코드 직접 작성 가능, 빠른 반복). 언리얼은 블루프린트라 클로드가 파일로 못 짜는 한계 때문에 보류.
  - 플랫폼: **웹(WebGL)** 선택 (링크 공유, 무심사/무료). 마우스+터치 겸용.
  - 폴더명 `tower`로 확정.
  - Kenney Tower Defense Kit = CC0/무료/3D/160개 실제 확인 완료.
  - **목업 먼저 보고 Firebase 배포 여부 결정**하기로 함(현재 미정).

## 백업 (GitHub)
- **repo: `wiikang84/monster`** (기존 monster repo를 재활용 — 형님 지시 2026-06-22 "monster 내용 다 지우고 tower로 엎어써, 새 repo 안 만듦").
- 즉 repo 이름은 `monster`지만 내용은 tower. 원격 origin = https://github.com/wiikang84/monster.git
- 푸시: `cd tower; git add -A; git commit; git push origin main` (초기 1회는 force-push로 monster 내용 덮어씀).
- **라이브(WebGL) URL: https://wiitower.web.app** (Firebase 프로젝트 `wiigame-448c7`, 호스팅 사이트 `wiitower`)
  - 빌드: Unity `-executeMethod BuildScript.BuildWebGL` (압축 Disabled) → 산출물 `tower/webgl-build/`
  - 재배포: `cd tower/webgl-build; npx firebase deploy --only hosting --project wiigame-448c7`
  - webgl-build/는 빌드 산출물이라 git 제외

## 진행 로그
- 2026-06-22: 폴더 생성 + 기획서/프로젝트메모 작성.
- 2026-06-22: Unity 6 프로젝트 생성(game/) + 그레이박스 프로토타입 스크립트 5종 작성(GameManager/Enemy/Tower/Projectile/TowerSlot), 배치모드 컴파일 에러 0 확인.
- 2026-06-22: git init + wiikang84/monster repo에 force-push로 백업 시작.
- 2026-06-23: **전면 재설계.** 기획 6종 작성(기획/00~05 + 06_친절UX, 07은 보류). 한글 UI 폰트(Pretendard) 적용. 아키텍처 데이터화: M0 코어 골격(ServiceLocator/GameEvents), M1 콘텐츠 JSON화(JsonUtility), M2 맵=ASCII그리드→BFS 경로 자동생성. 클래스 Tower→TowerUnit 개명.
- 2026-06-23: **깊이 업데이트.** 타워 3종(arrow/cannon/frost)×3티어 업그레이드·판매(환불70%), 적 5종(틴트/스케일/장갑/슬로우), 스테이지 3개. 타워선택→사거리원+업그레이드/판매 패널, 타게팅 4종, 광역/슬로우/치명타. Kenney 효과음 17종. 손맛(데미지숫자·골드팝업·피격플래시·비네트).
- 2026-06-23: **친절 UX + 그래픽 폴리시.** 모든 스테이지 3종 타워 선택(빌드 메뉴 카드+설명+사거리 미리보기), PC 호버 미리보기 링, 업그레이드 발견성(⬆ 표식·상시 가이드 문구·첫설치 힌트), 호버 툴팁. 그래픽: 소프트 그림자·따뜻한 조명·삼색 환경광·안개·MSAA·환경 장식(나무/바위/크리스탈 산점). ⚠️ 그래픽 한계 명시: 무료 Kenney 저폴리 톤은 유지(커스텀 3D 불가).
- 라이브: https://wiitower.web.app (재배포: cd tower; npx firebase-tools deploy --only hosting --project wiigame-448c7)
- ⚠️ WebGL 함정: OnGUI에서 `GUI.skin.font`/`GUI.skin.box` 직접 사용 시 NRE → 폰트는 개별 GUIStyle에, 패널 배경은 DrawTexture로. JSON은 JsonUtility(Newtonsoft 금지, +21MB).

## 작업 규칙
- 코드 우선(code-first): 런타임 C#로 구성해 에디터 수작업 최소화.
- 무료/CC0 에셋만. 사실적 에셋과 섞지 않음(스타일 통일).
- 디자인/그래픽은 자체 제작 불가 → 에셋으로 해결.
