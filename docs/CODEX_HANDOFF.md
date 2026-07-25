# FX OVERDOSE Codex 세션 인수인계

최종 확인: 2026-07-25 KST

## 1. 새 세션에서 가장 먼저 할 일

1. 이 문서와 [`bluem_dev.md`](./bluem_dev.md)를 먼저 읽습니다.
2. `git status --short --branch`로 사용자의 새 변경이 생겼는지 다시 확인합니다.
3. Unity Console의 현재 오류를 확인한 뒤, 요청받은 범위만 수정합니다.
4. 기능을 완료하면 [`bluem_dev.md`](./bluem_dev.md) 끝에 구현 내용을 추가합니다.

새 세션에 아래 문장을 그대로 전달해도 됩니다.

> `docs/CODEX_HANDOFF.md`와 `docs/bluem_dev.md`를 먼저 읽고 현재 git 상태를 확인해줘. 기존 UI와 사용자 변경은 보존하고, 대사/LLM 시스템은 인수인계 문서의 미완성 항목을 확인한 뒤 작업해줘.

## 2. 현재 저장소 상태

- 프로젝트 경로: `/Users/bluem/Documents/GitHub/FX-overdose`
- 브랜치: `dev_ui`
- 원격 추적: `origin/dev_ui`
- 기준 HEAD: `fdb9c3a584f889d40f746a86eae7b3c90fb134b0` (`오류 검출`)
- Unity: `6000.5.3f1`
- 타깃: Android, 모바일 가로 화면 `1920×1080` 기준
- 이 문서를 만들기 직전 작업 트리는 깨끗했습니다.
- 이 문서 생성 후의 미커밋 변경은 `docs/CODEX_HANDOFF.md` 1개가 정상입니다.

최근 주요 커밋:

| 커밋 | 내용 |
| --- | --- |
| `fdb9c3a` | 대사 시스템 개편 후 컴파일 오류 보정, 누락 `.meta` 추가 |
| `b2a2e53` | 하이브리드 LLM 시스템 개편 아이디어 문서 추가 |
| `c355ce8` | 일반 대사를 로컬 요미 데이터셋/매처 방식으로 개편 |
| `81475ed` | AI/USER, 스킬 LV, 캐릭터 LV/EXP 소형 폰트 깨짐 보정 |
| `bad57b7` | 스토리·무한·챌린지 모드와 시간대별 배경 추가 |
| `e6f0ed6` | 소모 아이템 사용 시 캐릭터 포즈 연출 추가 |

## 3. 빌드 및 씬 상태

Build Settings 순서:

1. `Assets/Scenes/TitleScene.unity`
2. `Assets/Scenes/LoadingScene.unity`
3. `Assets/Scenes/GameScene.unity`

`SampleScene`은 비활성 상태입니다. 실제 진입 흐름은 다음과 같습니다.

```text
TitleScene → LoadingScene → GameScene(Additive)
```

인수인계 작성 시점에 아래 명령으로 전체 C# 컴파일을 확인했습니다.

```bash
/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet build Assembly-CSharp-Editor.csproj -v:minimal
```

결과: 경고 `0`, 오류 `0`.

`Temp/obj`가 없는 새 환경에서는 `--no-restore`를 붙이지 말고 한 번 빌드해야 합니다. C# 컴파일만 검증했으며 Unity Play Mode 전체 흐름과 실제 Android 플레이어 빌드는 이번 인수인계 시점에 다시 실행하지 않았습니다.

자동화된 `[Test]`/`[UnityTest]` 테스트는 없습니다. AI 매매 통합 검증기는 Unity 메뉴 `FXOverdose/Debug/AI Trading System Integration Test`에서 실행할 수 있습니다.

## 4. 최근 완료된 사용자 기능

### 게임 모드와 저장

- `STORY`: 기존 3개 세이브 슬롯을 사용합니다.
- `ENDLESS`: AI/USER 전환을 사용할 수 있고 저장은 지원하지 않습니다.
- `CHALLENGE`: USER 수동매매로 고정되며 AI 자동 진입을 차단합니다.
- 돌발 이벤트에서 사용자가 직접 선택한 롱/숏은 챌린지에서도 허용합니다.
- 구버전 세이브는 `Story = 0` 기본값으로 호환됩니다.

핵심 파일:

- `Assets/Scripts/System/GameMode.cs`
- `Assets/Scripts/System/SaveData.cs`
- `Assets/Scripts/System/SaveLoadManager.cs`
- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scripts/UI/TitleScreenBuilder.cs`
- `Assets/Scripts/Trading/TradingController.cs`

### 시간대별 배경

- 아침: `06:00~16:59`
- 해질녘: `17:00~19:59`
- 밤: `20:00~05:59`
- 전환은 실시간 기준 `0.8초` 크로스페이드입니다.

핵심 파일:

- `Assets/Scripts/UI/DayTimeBackgroundController.cs`
- `Assets/Resources/Backgrounds/RoomMorning.png`
- `Assets/Resources/Backgrounds/RoomSunset.png`

### 최근 UI 보정

- 숍 카드의 `OWNED x N`을 14px 왼쪽으로 이동했습니다.
- AI/USER, 스킬 `LV.n`, 캐릭터 LV/EXP를 고정 크기 렌더링으로 변경했습니다.
- 잘못된 스킬 Auto Size 범위를 제거하고 작은 PF Stardust 글자의 합성 Bold와 과한 Outline을 줄였습니다.
- 전역 폰트 적용기가 각 TMP 텍스트의 개별 머티리얼을 덮어쓰지 않도록 했습니다.

핵심 파일:

- `Assets/Scripts/Items/DynamicShopUI.cs`
- `Assets/Scripts/UI/GlobalPFStardustFont.cs`
- `Assets/Scripts/UI/SettingsMenuController.cs`
- `Assets/Scripts/UI/ActiveSkillHUDController.cs`
- `Assets/Scripts/UI/TraderLevelUIController.cs`
- `Assets/Editor/PFStardustGlobalFontApplicator.cs`

### 소모 아이템 캐릭터 연출

- `energy_drink`, `dessert`, `supplement`, `sedative` 소비 성공 시 전용 포즈를 약 1초 표시합니다.
- 일시정지 중에도 종료되며, 이후 최신 감정 스프라이트로 복귀합니다.

핵심 파일:

- `Assets/Scripts/Items/Inventory.cs`
- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Resources/Characters/ItemUse/`

## 5. 가장 중요한 진행 중 상태: 대사 및 LLM 개편

이 부분은 “완료된 시스템”으로 간주하면 안 됩니다.

### 현재 적용된 내용

- 일반 매매 대사용 `LocalLLMService.cs`와 `AIPromptBuilder.cs`는 삭제되었습니다.
- `AITradingBrain`과 멘탈 기믹은 현재 하드코딩 대사를 바로 출력합니다.
- JSONL 기반 요미 대사 구조가 추가되었습니다.
  - `Assets/Scripts/AI/Dialogue/YomiDialogueData.cs`
  - `Assets/Scripts/AI/Dialogue/YomiDialogueDatabase.cs`
  - `Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs`
  - `Assets/Scripts/Editor/YomiDatasetConverter.cs`
  - `docs/yomi_dataset.jsonl`
- `fdb9c3a`에서 삭제된 LLM 참조, 매처 메서드 인자, 네임스페이스와 누락 `.meta`를 보정했고 C# 컴파일은 통과합니다.

### 아직 연결되지 않은 부분

- 현재 저장소에는 생성된 `YomiDialogueDatabase.asset`이 없습니다.
- 씬/프리팹에도 `YomiDialogueMatcher` 설치 흔적이 없습니다.
- 따라서 새 매처는 컴파일되지만 런타임에서 자동으로 생성·연결되지 않습니다.
- `TradingController.OutputYomiDialogue()`는 매처 인스턴스가 없으면 기존 폴백 문장을 출력합니다.
- `docs/`와 프로젝트 루트에 `yomi_dataset.jsonl`, 샘플 문서가 중복되어 있습니다. 어느 쪽을 원본으로 둘지 아직 정리되지 않았습니다.

### 확인된 런타임 위험

- 일부 폴백 문장은 실제 대사가 아니라 LLM용 생성 지시문입니다. 현재처럼 LLM을 우회해 직접 출력하면 “감정을 표현해 줘” 같은 문장이 플레이어 말풍선에 그대로 노출될 수 있습니다.
  - `Assets/Scripts/AI/MentalDrainGimmickController.cs`의 고점 돌파·드로다운 관련 폴백
  - `Assets/Scripts/TraderStatus.cs`의 `TriggerLLMDialogue()`
- 게임오버 LLM 독백 루프는 제거되었지만 `GameOverUIController.displayDelay = 13f`는 남아 있습니다. 게임오버 직후 대사 없이 입력만 막힌 채 13초 대기하는지 확인해야 합니다.
- `TraderStatus.MentalState`는 `Stable`, `Anxious`, `Danger`, `Overdose` 4종이고 데이터셋의 `mentalState`는 19종 `TraderEmotion` 명칭을 사용합니다. 명시적인 매핑이 없으면 멘탈 일치 점수가 대부분 적용되지 않습니다.
- 무포지션 상태의 매처 호출은 현재 시장 추세를 읽지 않고 `Sideways`로 계산될 수 있어 Bull/Bear/Volatile 대사가 선택되지 않을 수 있습니다.
- 매처 오브젝트만 만들고 데이터베이스를 연결하지 않으면 `"데이터베이스가 연결되지 않았어!"`가 실제 대사로 표시됩니다.

### 로컬 모델 상태

- 로컬에는 `Assets/StreamingAssets/Models/Qwen3-4B-Q4_K_M.gguf`가 약 `2.3GB`로 존재합니다.
- `*.gguf`는 `.gitignore` 대상이므로 새 클론에는 모델 바이너리가 없습니다.
- 현재 LLM 런타임 서비스가 삭제되어 이 모델을 실제로 호출하는 코드도 없습니다.
- [`LLM 시스템 개편 아이디어.md`](./LLM%20시스템%20개편%20아이디어.md)는 `Qwen 2.5 1.5B INT4`를 제안하고 있어 로컬 Qwen3 4B와 방향이 다릅니다.
- 같은 문서에서 제안한 `LLMSafeGenerator.cs`, Grammar/JSON 강제, 정산·돌발 이벤트 사전 생성은 아직 구현되지 않았습니다.

다음 세션에서는 모델과 LLM 사용 범위를 먼저 확정해야 합니다. 현재 설계안은 일반 매매와 일반 대사는 C# 규칙/데이터 기반으로 처리하고, LLM은 돌발 이벤트 반응과 일일 정산에만 사용하는 방향입니다.

## 6. 우선 검증할 체크리스트

다음 기능을 건드릴 때는 Unity Play Mode에서 관련 항목을 함께 확인합니다.

- 타이틀 `NEW GAME` → 3개 모드 팝업 → 로딩 → 게임 진입
- Story 슬롯 불러오기/저장과 구버전 세이브 호환
- Challenge에서 AI/USER 버튼 잠금과 모든 우회 자동 진입 차단
- 16:59→17:00, 19:59→20:00 배경 전환
- 숍 구매 직후 `OWNED x N`과 인벤토리 수량 즉시 갱신
- AI/USER, 스킬 LV, 캐릭터 LV/EXP의 PF Stardust 글리프 깨짐 여부
- 요미 대사 매처를 연결할 경우 데이터베이스 생성, 씬 설치, 쿨다운과 폴백 대사
- 4종 `MentalState`와 19종 `TraderEmotion`의 매핑 및 무포지션 시장 추세 전달
- 생성 지시문 형태의 폴백 대사가 플레이어에게 직접 노출되지 않는지
- 게임오버 LLM 독백 제거 후 남은 13초 입력 차단이 의도한 연출인지
- 정산, 돌발 이벤트, 게임오버에서 삭제된 `LocalLLMService`를 전제로 한 동작이 남아 있지 않은지

## 7. 핵심 문서

먼저 읽을 문서:

- [`기획서_상세.md`](./기획서_상세.md): 게임 전체 기획 기준
- [`bluem_dev.md`](./bluem_dev.md): 실제 구현 이력의 기준 문서
- [`UI_ASSEMBLY_AND_DESIGN_GUIDE.md`](./UI_ASSEMBLY_AND_DESIGN_GUIDE.md): UI 구조와 연동 기준
- [`balance_reference.md`](./balance_reference.md): 체력·멘탈·레벨·스킬·AI·아이템 수치

현재 대사 개편 관련:

- [`LLM 시스템 개편 아이디어.md`](./LLM%20시스템%20개편%20아이디어.md)
- [`yomi_hardcoded_dialogue_plan.md`](./yomi_hardcoded_dialogue_plan.md)
- [`yomi_dataset.jsonl`](./yomi_dataset.jsonl)
- [`주인공AI_개발_로드맵.md`](./주인공AI_개발_로드맵.md)

기능별 UI 가이드:

- [`모의차트_트레이딩뷰_UI제작_가이드.md`](./모의차트_트레이딩뷰_UI제작_가이드.md)
- [`UI_액티브아이템_연동_가이드.md`](./UI_액티브아이템_연동_가이드.md)
- [`타이틀화면_및_로딩화면_UI_조립_가이드.md`](./타이틀화면_및_로딩화면_UI_조립_가이드.md)
- [`주인공AI_감정스프라이트_가이드.md`](./주인공AI_감정스프라이트_가이드.md)
- [`돌발선택이벤트_시스템구현_및_메서드명세서.md`](./돌발선택이벤트_시스템구현_및_메서드명세서.md)
- [`일일정산UI_설계서.md`](./일일정산UI_설계서.md)
- [`game_over_ui_guide.md`](./game_over_ui_guide.md)

## 8. 작업 규칙과 주의사항

- 사용자는 답변을 `~해용` 말투로 받기를 원합니다.
- UI는 PF Stardust, 픽셀 아트, 네이비·시안 중심의 기존 팔레트를 유지합니다.
- 모바일 가로 `1920×1080` 기준으로 여백, 외곽선 두께, 종횡비를 특히 중요하게 봅니다.
- 씬과 폰트 에셋은 충돌이 자주 났습니다. `GameScene.unity`, `TitleScene.unity`, `PFStardustBold Dynamic SDF.asset`을 충돌 해결할 때 한쪽 버전으로 통째로 덮어쓰지 않습니다.
- `PFStardustBold Dynamic SDF.asset`은 동적 글리프 아틀라스 때문에 플레이만 해도 큰 diff가 생길 수 있습니다. 커밋 전 의도한 글리프 변경인지 확인합니다.
- 현재 폰트 커밋에는 동적 아틀라스 캐시 글리프가 포함되어 있고 `m_ClearDynamicDataOnBuild`가 활성화되어 있습니다. 에디터에서 보이는 글리프 추가만으로 빌드의 폰트 깨짐이 영구 해결됐다고 판단하지 않습니다.
- Unity `.meta` 파일을 임의로 삭제하거나 다시 생성하지 않습니다.
- `Assets/StreamingAssets/Models/Qwen3-4B-Q4_K_M.gguf`는 로컬 전용 대용량 파일이므로 Git에 추가하지 않습니다.
- 기존 변경은 사용자 작업일 수 있으므로 `git reset --hard`, `git checkout --` 같은 파괴적 복구를 사용하지 않습니다.
- 새 기능을 만들거나 완료할 때마다 [`bluem_dev.md`](./bluem_dev.md)에 기록합니다.
