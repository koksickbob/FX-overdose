# 트레이딩 시스템 전수 감사 — 수정 계획서

- **작성일**: 2026-08-22
- **기준 브랜치**: `Dev_koksickbob3` (7305f43)
- **감사 범위**: 트레이딩 코어 루프(GameManager / MarketSimulationEngine / TradingController / DynamicTimeRegulator / TraderLevelSystem), UI 레이어(TradingPanelUIController / ChartUIController / HUDController / TutorialManager 등), AI·보스·이벤트(AITradingBrain / BossManager / MentalDrainGimmickController / ChoiceEvent / Events/Story), 상태·저장·아이템·업적(TraderStatus / SaveLoadManager / Inventory / ItemUser / CostumeManager / AchievementManager)
- **검증 방식**: 모든 항목은 이벤트 발화↔구독 쌍, 플래그 쓰기 지점 전수, 호출자 존재 여부를 grep으로 교차 확인함. 씬 직렬화 값(`GameScene.unity`, `tutorial.unity`)까지 대조한 항목 포함.

## 우선순위 체계

| 등급 | 기준 |
|---|---|
| **P0** | 게임 진행 불가 또는 한 번 발생하면 복구 불가한 영구 고착 |
| **P1** | 기획된 기능이 전면 무효 (플레이어가 체감하는 기능 실종) |
| **P2** | 연출·밸런스·데이터 정합 오류 (동작은 하나 어긋남) |
| **P3** | 죽은 코드 정리·삭제 (동작 영향 없음, 유지보수 비용) |

각 항목 형식: 체크박스 / 위치 / 원인 / 수정 방법. `[기획 결정 필요]` 표시는 코드 수정 전에 살릴지 죽일지 먼저 정해야 하는 항목.

---

## P0 — 진행 불가 / 영구 고착 (7건) — ✅ 2026-08-22 전건 적용 완료

> 적용 후 `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj` 모두 **경고 0 · 오류 0**.
> Unity 에디터에서의 실제 플레이 검증은 아직 수행하지 않았습니다 (아래 "P0 검증 항목" 참조).

### P0-1. 튜토리얼 Step9 영구 데드락
- [x] `Assets/Scripts/System/TutorialManager.cs:972-1000` + `Assets/Scripts/GameManager.cs:1440-1456`
- **원인**: Step9가 `fullScreenBlocker.blocksRaycasts = false`로 입력을 풀었을 때 상점/설정을 열면 `Paused` 상태 진입 → 이후 차단막(sortingOrder 999)이 다시 켜져 상점(sortingOrder 100) 닫기 버튼을 못 누름 → `AdvanceGameMinutes(150)`이 `currentState != Playing`으로 즉시 break → `IsFastForwardingTime = true` 잔존 → `while (gameManager.IsFastForwardingTime)` 무한 대기.
- **추가 확인**: 상점을 열지 않아도 같은 교착이 납니다. 선택 효과로 파산·Overdose 엔딩(`GameOver`)이 나면 동일하게 `AdvanceGameMinutes`가 즉시 break하고 무한 대기에 걸립니다 — 오히려 이쪽이 발생 확률이 높습니다.
- **적용한 수정**:
  - `blocksRaycasts`를 끄는 대신 **차단막 캔버스(`highlightCanvas`)의 sortingOrder를 999 → 140으로 잠시 내렸습니다.** 팝업(150)만 클릭을 받고 SHOP(100)·설정은 계속 차단됩니다. 팝업 종료 후 원래 값으로 복원.
  - 고속 진행 대기 루프에 `GameOver`/`Settlement` 탈출 분기 추가 (`Paused`는 창을 닫으면 재개되므로 계속 대기).

### P0-2. 스토리 이벤트 중단 시 일시정지·IsRunning·Commit 영구 미복구
- [x] `Assets/Scripts/Events/Story/EventOverlayHost.cs:17-65`, `EventView.cs:79-84`, `EventLauncher.cs:22,61-92`
- **원인**: 오버레이 캔버스는 현재 씬 소속이라 이벤트 도중 씬 전환 시 통째로 파괴되는데, `EventOverlayHost`에 `OnDestroy`가 없어 ① `pausedByThisEvent`로 건 `PauseGame()` 미복구, ② `EventLauncher.NotifyFinished()` 미호출로 **static** `IsRunning`이 영구 true → 이후 모든 스토리 이벤트 차단, ③ `runner.Commit()` 누락으로 호감도·플래그·완료기록 증발. 씬 호스트 경로도 `IsRunning = true` 직후 씬 로드 실패 시 동일하게 고착.
- **적용한 수정**:
  - `EventOverlayHost`에 `completed` 플래그와 `OnDestroy` 추가 — 미완료 파괴 시 일시정지 복구 + `NotifyFinished()`. **커밋은 하지 않습니다** (중도 이탈 무기록은 `EventLauncher.IsCompleted` 주석에 명시된 설계).
  - `EventLauncher`에 `SceneManager.sceneLoaded` 감시자(`[RuntimeInitializeOnLoadMethod]`) 추가 — `LoadingScene`/`EventScene`이 아닌 씬에 도착했는데 `IsRunning`이 서 있으면 호스트가 없다는 뜻이므로 플래그를 내립니다. 전용 씬 진입 실패·`EventSceneHost` 미배치를 개별 방어하는 대신 한곳에서 걷어냅니다.
  - `EventView.OnDestroy`는 손대지 않았습니다 — 위 두 방어가 상위에서 덮습니다.

### P0-3. 선택 이벤트 팝업 중 컴포넌트 비활성화 시 Paused 고착
- [x] `Assets/Scripts/Events/ChoiceEventController.cs:76, 807-825, 860-873`
- **원인**: `pausedByChoiceEvent` 복구 지점이 `OnOptionSelected` 단 하나. `P2PGameplayUIController.cs:60`의 `DisableAll<ChoiceEventController>()`가 팝업이 뜬 상태에서 컴포넌트를 끌 수 있음 → `GameState.Paused` 고정.
- **적용한 수정**: `OnDisable` 추가 — `pausedByChoiceEvent`면 팝업 `Hide()` + `Paused` 상태일 때 `ResumeGame()` + 플래그·`currentActiveEvent` 정리. `OnDestroy`는 항상 `OnDisable` 뒤에 오므로 두 경로 모두 덮입니다.

### P0-4. 설정 메뉴가 timeScale=0을 복원해 게임 영구 정지
- [x] `Assets/Scripts/UI/SettingsMenuController.cs:404`
- **원인**: 로딩 직후 freeze 구간(`LoadingScreenController.cs:137-142`, `timeScale=0`)에 설정을 열면 `previousTimeScale = 0` 캡처 → 닫을 때 `RestoreGameState()`가 가드 없이 그대로 복원. (397행 `QuitGame()`에는 `> 0f` 가드가 있어 비대칭.)
- **적용한 수정**: 397행과 동일한 `previousTimeScale > 0f ? previousTimeScale : 1f` 가드 적용.

### P0-5. 스킬 업그레이드 오버레이가 코루틴 중단 시 화면 영구 차단
- [x] `Assets/Scripts/UI/ActiveSkillHUDController.cs:372-406`
- **원인**: `timeTransitionOverlay`(blocksRaycasts, sortingOrder 360) 해제가 코루틴 말미에만 존재. 코루틴이 시간 진행을 유발해 하루 종료/씬 전환으로 HUD가 비활성화되면 오버레이와 `isUpgradeSequencePlaying = true`가 잔존 → 화면 차단 + 이후 업그레이드 전부 거부.
- **적용한 수정**: `AbortUpgradeSequenceState()`를 만들어 `OnDisable`에서 호출 — 오버레이 비활성 + `CanvasGroup.alpha = 0` + 업그레이드 버튼 재활성 + 플래그 해제. AI 연출도 되돌릴 수 있도록 `activeUpgradeVisual` 필드를 추가해 `FindAnyObjectByType` 없이 `EndSkillUpgradeVisual()`을 호출합니다.

### P0-6. 에디터 빌더가 수동매매 버튼 연결을 지우는 시한폭탄
- [x] `Assets/Editor/TradingViewUIBuilder.cs:778-782`
- **⚠️ 착수 중 원인 정정**: 감사 시점의 진단("null 대입 4줄")은 **증상이었고 실제 위험은 더 큽니다.** `BuildTradingChartUI()`는 시작부에서 `Undo.DestroyObjectImmediate(existingCanvas)`로 **`TradingViewCanvas`를 통째로 파괴**합니다. 씬 확인 결과 `LongButtonCard`(GameScene.unity:11345)의 부모는 `BottomTradingPanel`(fileID 317263479)이고 그 위가 `TradingViewCanvas`입니다. 즉 재실행하면 버튼이 **아예 삭제**되며, `LongButtonCard`는 `Assets` 전체에서 씬 3개와 스타일러/P2P 참조에만 존재할 뿐 **생성하는 코드가 없어 복구 불가**입니다.
- **더 심각한 점**: 이 빌더는 `[InitializeOnLoadMethod] AutoBuildOnRecompileOnce`로 **자동 실행**되고 직후 `SaveOpenScenes()`까지 합니다. EditorPrefs 키(`..._v9_IntegrityRebuild`)가 없는 환경 — 새 클론·새 PC·키를 올린 경우 — 에서 프로젝트를 열기만 해도 씬이 파괴된 채 디스크에 저장됩니다. `Force Rebuild Korean Font Asset Integrity` 메뉴도 같은 파괴 경로를 탑니다.
- **적용한 수정**:
  - 자동 실행 경로에 `GameObject.Find("TradingViewCanvas") == null` 가드 — 이미 캔버스가 있으면 손대지 않습니다.
  - `BuildTradingChartUI()` 파괴 직전에 `EditorUtility.DisplayDialog` 확인 추가 (LONG/SHORT 카드가 사라진다는 경고 문구 포함). 모든 호출자가 이 한곳을 통과하므로 폰트 무결성 메뉴도 함께 보호됩니다.
  - `SetField(controller, "longButton"/"shortButton"/서브타이틀, null)` 4줄 삭제 + 사유 주석.

### P0-7. DynamicTimeRegulator 중복 처리가 GameManager를 파괴할 수 있음
- [x] `Assets/Scripts/Core/DynamicTimeRegulator.cs:20-23`
- **원인**: 중복 인스턴스 처리가 `Destroy(gameObject)`. 이 컴포넌트는 `GameManager.EnsureDynamicTimeRegulator`(GameManager.cs:674)가 GameManager 오브젝트에 부착하므로, 씬에 별도 배치된 인스턴스와의 Awake 순서에 따라 **GameManager 오브젝트가 통째로 파괴**될 수 있음.
- **적용한 수정**: `Destroy(this)`로 변경 + 사유 주석.

---

## P0 검증 항목 (Unity 에디터에서 확인 필요)

코드 수정과 컴파일(경고 0·오류 0)은 끝났지만 아래는 실제 플레이로만 확인할 수 있습니다.

- [ ] **P0-1**: `tutorial` 씬 Step9에서 돌발 이벤트 팝업이 정상적으로 **클릭되는지** — 차단막 캔버스를 sortingOrder 140으로 내린 방식이 팝업(150) 입력을 실제로 통과시키는지. 동시에 그 구간에 SHOP(100)·설정 버튼이 **눌리지 않는지**.
- [ ] **P0-1**: 팝업 종료 후 차단막이 999로 복귀해 이후 단계가 정상 차단되는지.
- [ ] **P0-6**: `Tools/FX OVERDOSE/Build Trading Chart UI` 실행 시 확인 다이얼로그가 뜨고, "취소"를 누르면 씬이 그대로인지.
- [ ] **P0-2 / P0-3**: 이벤트·팝업 도중 씬을 전환해도 게임이 `Paused`로 굳지 않고 이후 이벤트가 다시 뜨는지.

---

## P1 — 기능 전면 무효 (11건) — 2026-08-22 **전건 처리 완료**

> 4개 어셈블리(`Assembly-CSharp`, `Assembly-CSharp-Editor`, `FXOverdose.P2P.Core`, `.Tests`) 모두 **경고 0 · 오류 0**.
> 10건 코드 수정, 1건(**P1-1**)은 기획 확인 결과 **의도된 미연결 상태**로 조치 보류.

### P1-1. 신규 스토리 이벤트 시스템이 런타임에서 전혀 트리거되지 않음 → **조치 보류 (의도된 상태)**
- [x] ~~조치 불필요~~ `Assets/Scripts/Events/Story/EventLauncher.cs:34`, `EventCatalog.cs:53`
- **2026-08-22 기획 확인**: 이 시스템은 **프롤로그와 미연시(DatingSim) 파트에서 사용할 예정**이라 아직 트리거가 없는 것이 정상입니다. 현 시점에 연결하지 않습니다.
- 다만 **P0-2에서 중단 복구는 이미 넣어 뒀으므로**, 나중에 연결할 때 진행 플래그 고착 문제는 재발하지 않습니다.
- **원인**: `EventLauncher.Play`의 호출자가 에디터 디버그 메뉴(`Assets/Editor/EventSystemTestRunner.cs:159`) 1곳뿐. 클래스 주석이 명시한 호출처 4곳(요미 방 · 월드맵 · 정산 중 GameManager · 타이틀 직후)이 전부 미구현. 카탈로그 등록 이벤트도 검증용 `EVT_SAMPLE_001` 1건뿐이며, 커밋된 `EVT_MAIN_001` 아트 8장에 대응하는 `EventDefinition`이 없음. 결과적으로 `SaveData.EventCompletedIds`/`StoryFlags`/`EventChoiceHistory`도 항상 빈 값.
- **수정**: ① 트리거 지점 4곳 중 우선 구현할 곳 결정(정산 경로 `GameManager.ProcessDailySettlementWithStory` 연동이 1순위 후보), ② `EVT_MAIN_001` `EventDefinition` 작성 및 카탈로그 등록. P0-2를 먼저 수정한 뒤 연결할 것.

### P1-2. "요미의 매매 주도권 강탈" 락이 100% 무효
- [x] `Assets/Scripts/Trading/TradingController.cs:1223, 101-106`, `Assets/Scripts/AI/AITradingBrain.cs:273`
- **원인**: 락을 거는 유일한 게임플레이 경로가 `OnPositionClosed` 구독자(`MentalDrainGimmickController.cs:288`)인데, 그 이벤트를 발화한 `ClosePosition()`이 직후 1223행에서 무조건 `IsManualModeLockedByYomi = false` → 같은 콜스택 안에서 락이 지워짐. 추가로 `TemporaryLockRoutine`(101-106행)이 종료 시 무조건 해제해 영구 락을 덮어쓰고, `AITradingBrain.cs:273`이 매 진입 시도마다 `UnlockManualMode()` 호출.
- **⚠️ 착수 중 진단 정정**: `AITradingBrain.cs:273`은 **버그가 아니라 설계된 락 수명의 끝**입니다. 이 호출은 `if (ForceNextTradeHighLeverage)` 블록 안에 있어 "요미가 강탈한 뒤 실제로 고배율 매매를 실행하는 순간"에만 돕니다. 감사 보고의 "매 진입마다 해제"는 오판이므로 **손대지 않았습니다.**
- **적용한 수정**:
  - `ClosePosition`의 1223행 무조건 해제 삭제 + 재발 방지 주석. 락 해제 소유권은 `UnlockManualMode`(= AITradingBrain의 강제 매매 실행 시점)에만 둡니다.
  - `manualLockGeneration` 세대 카운터 도입. `TemporaryLockRoutine`은 대기 중 다른 곳에서 락/언락이 걸리면 세대가 어긋나 해제를 포기합니다 — 2초 임시 락이 그 사이 걸린 영구 락을 지우던 문제 제거.

### P1-3. 돌발 이벤트 쉴드가 항상 조기 종료
- [x] `Assets/Scripts/Trading/TradingController.cs:417-421, 775-786`
- **원인**: `isMarketEventOver = !marketEngine.IsExternalEventOverride` 판정이 ① 빔 없는 선택지(`OverrideBeamPercent == 0` — `ChoiceEventRuntimeData.cs:42, 62`에 실재)에서는 override가 애초에 안 걸려 **1프레임 만에** 쉴드 종료, ② 빔이 있어도 빔 수명 30 인게임 분(≈20 실초, `secondsPerGameMinute=0.666` 기준) < 쉴드 최소 30 실초라 **단위 불일치로 항상 2/3 지점에서 강제 종료**. `MarketSimulationEngine.cs:1110-1111` 주석에 경고만 남아 있음.
- **적용한 수정**: 417행과 775행의 `isMarketEventOver` 항 제거 — 쉴드 만료는 실시간 타이머(`eventProtectionEndTime`) 단독 판정. 두 지점에 사유 주석을 남겼습니다.

### P1-4. `isExternalEventOverride` 영구 고착 (스프레드 ×5·세션 변동성·오더블록 무력화)
- [x] `Assets/Scripts/Trading/MarketSimulationEngine.cs:386, 1437`
- **원인**: `HandleFastForwardEnded()`(386)와 `CancelOverdoseTrapSignal()`(1437)이 `currentSignalPhase = None`만 쓰고 플래그를 안 꺼서, 유일한 해제 경로(1261/1292행 페이즈 전이)에 영영 도달 불가 → 다음 날 `ResetEngine`까지 스프레드 ×5, 세션별 변동성(590행)·오더블록(724행) 정지, 서버 렉 발동 차단(438행) 지속.
- **적용한 수정**: 두 지점에 `isExternalEventOverride = false;` 동반 + 사유 주석.

### P1-5. 서버 렉 기믹의 핵심(가격 정지→일괄 반영)이 미구현 → **안A(구현)로 결정·적용**
- [x] `Assets/Scripts/Trading/MarketSimulationEngine.cs:159-160, 445-464, 534-838`
- **원인**: `accumulatedLagPriceDelta`/`accumulatedLagVolume`의 쓰기 지점이 전부 `= 0` 초기화뿐 — `+=`가 코드 전체에 없음. 렉 중에도 `SimulateTickMovement`가 가격을 정상 갱신·발화하므로 "차트 정지 후 일괄 갱신" 연출이 아무 일도 안 함. 남는 효과는 스프레드 ×3과 버튼 차단뿐이라, **가격은 흐르는데 손절만 못 하는 3~5초**가 됨 (연출 의도와 정반대 — P2-14와 연동).
- **결정 근거**: 착수 중 확인해 보니 **복구(flush) 코드는 이미 완성돼 있고 정확합니다**(445-470행: 누적분 반영 → NaN 방어 → 캔들 갱신 → `OnPriceUpdated` 발화, 복구 로그 문구까지 작성됨). 빠진 것은 누적 한 곳뿐이라 구현 비용이 폐기보다 낮고, 폐기는 day 16+ 긴장 기믹과 전용 UI 오버레이까지 함께 버려야 합니다. 그래서 안A를 택했습니다.
- **적용한 수정**: `SimulateTickMovement`의 가격 적용부를 `isServerLagging` 분기로 나눔 — 렉 중에는 `accumulatedLagPriceDelta`/`accumulatedLagVolume`에 쌓기만 하고 `currentPrice` 갱신·캔들 갱신·`OnPriceUpdated` 발화를 모두 보류합니다. 이로써 P2-14(가격은 흐르는데 손절만 막히는 모순)도 함께 해소됩니다 — 이제 가격이 실제로 멈춘 동안 버튼이 막힙니다.
- **남긴 단순화**: 렉 구간(3~5초, 약 15~25틱) 동안 복리를 무시하고 델타를 단순 합산합니다. 코드에 `ponytail:` 주석으로 상한과 개선 경로(렉 시작가 기준 누적 수익률)를 표기했습니다.

### P1-6. 업적 `use_parfait_100` 해금 불가 → 메이드 코스튬 영구 잠김 + 로드 시 박탈
- [x] `Assets/Scripts/System/AchievementManager.cs:237-241`, `Assets/Scripts/Items/CostumeManager.cs:259-263`
- **원인**: `RecordItemUsage`가 `lowerId.Contains("parfait")`로 판정하지만 파르페의 실제 `ItemId`는 `dessert`(`Assets/Data/Items/Dessert.asset`) → 카운터 영구 0. 설상가상 `CostumeManager.Restore`의 검수 롤백 가드가 이 업적 미해금 시 **로드할 때마다 메이드를 소유 목록에서 제외** — 어떤 경로로 얻어도 소실.
- **적용한 수정**: ① 판정 문자열을 `"dessert"`로 교정. 에셋 4종의 `itemId`를 전수 확인했습니다(dessert / energy_drink / sedative / supplement) — 나머지 판정어(energy·malatang·sushi·tteokbokki)는 실제 ID와 일치해 정상입니다. ② `CostumeManager.Restore`의 검수 롤백 가드 삭제.

### P1-7. P2P 관전자 잠금이 매 스냅샷마다 무효화
- [x] `Assets/Scripts/UI/Chart/TradingPanelUIController.cs:1256-1258` (호출: 1095행)
- **원인**: `RefreshPanelUI`가 `!p2pSpectating`으로 버튼을 잠근 직후 호출하는 `UpdateTradeCooldownUI()`가 `p2pSpectating` 항 없이 `canEnter`를 재계산해 덮어씀. P2P 스냅샷마다 `OnPositionChanged` → 이 경로가 실행되므로 탈락한 관전자의 LONG/SHORT가 다시 눌림.
- **적용한 수정**: `canEnter`에 `!p2pSpectating`과 게임 상태(`CurrentState == Playing`) 항을 함께 추가 — **P2-15도 이 수정으로 함께 해결**되어 정산/게임오버 중 주문 버튼이 오버레이 raycast 차단에 기대지 않게 됐습니다.

### P1-8. LLM 프리페치 취소 미전달 + grammar 전역 해제 경합
- [x] `Assets/Scripts/AI/LLM/LLMSafeGenerator.cs:129, 137-145`, `Assets/Scripts/Events/ChoiceEventController.cs:165-178, 570-582`
- **원인**: `GenerateChoiceEventAsync`가 `cancellationToken`을 `llmAgent.Chat`에 전달하지 않음 → `CancelPendingPreFetch()`는 플래그만 되돌리고 실제 요청은 계속 진행. 일자 전환/다음 트리거 예약 시 2차 프리페치가 겹치고, 먼저 끝난 쪽의 `finally`가 `llmAgent.grammar = ""`로 **진행 중인 요청의 GBNF 제약을 해제** → JSON 파싱 실패 → 폴백. 하루 경계마다 재현 가능한 구조.
- **적용한 수정**: LLMUnity의 `Chat`에는 취소 인자 자체가 없어 진행 중 호출을 끊을 수 없습니다. 그래서 **정적 `SemaphoreSlim AgentGate`로 에이전트 접근을 직렬화**했습니다 — 두 생성이 겹치지 않으므로 grammar를 서로 지우는 일이 원천적으로 사라집니다. 게이트를 얻은 뒤 취소 여부를 다시 확인해, 대기 중 취소된 요청은 아예 시작하지 않습니다(취소가 실질적 효과를 갖게 됨). 에이전트는 `LLM_Manager`에 하나뿐이라 게이트는 정적입니다.

### P1-9. 치료 아이템이 사실상 무의미 (2중 차단)
- [x] `Assets/Scripts/Items/ItemUser.cs:120, 169-173`, `Assets/Scripts/AI/MentalDrainGimmickController.cs:248, 377`
- **원인**: ① 멘탈 만땅 시 조기 return이 그 아래의 `CureMentalGimmicks()`·`CureLeverageAddiction()`까지 차단 — 중독은 멘탈과 독립 상태라 취지("회복량 낭비 방지")와 무관. 배달음식(120행)도 동일. ② 치료에 성공해도 `OnPositionOpened`/`OnPositionClosed`가 무조건 `isUnrealizedPnLCured = false` → 다음 매매 즉시 무효.
- **적용한 수정**:
  - `RestoreMental`에서 중독 치료 판정(`curesAddiction`)을 만땅 가드보다 **앞**으로 옮겼습니다. 멘탈이 가득 차 있어도 치료할 중독이 있으면 아이템 사용이 성립하고, 회복량만 건너뜁니다.
  - `OnPositionOpened`의 `isUnrealizedPnLCured = false` 삭제. 이제 치료는 "포지션 하나를 덮는 1회성"으로 일관되게 동작합니다(무포지션에서 먹고 진입해도 유효, 해제는 `OnPositionClosed`에서만).
- **의도적으로 남긴 것**: 배달음식 경로(`ItemUser.cs:121`)의 "HP·멘탈 둘 다 만땅" 가드는 그대로 뒀습니다. 기믹이 실제로 걸려 있는지 판정하려면 `MentalDrainGimmickController`에 새 조회 API가 필요한데, 해당 상태(`isUnrealizedPnLCured`)는 기본값이 false라 단순 질의로는 거의 항상 참이 되어 만땅 가드를 무력화합니다. 중독 치료처럼 명확한 근거가 없어 보류합니다.

### P1-10. `PeakBalance`(고점 자산) 시스템 전면 사문 → **폐기(안B)로 결정·적용**
- [x] `Assets/Scripts/TraderStatus.cs:44, 114-137`
- **원인**: `peakBalance`를 올리는 코드가 리포지토리 어디에도 없음(쓰기는 리셋·복원·동기화·감소 전용뿐) → 항상 0 → `peakBalance <= 0f` 가드에서 `AdjustPeakBalanceForExpenditure` 즉시 return → 호출부 4곳(`GameManager.cs:1013, 1028, 1211, 1533`) 전부 no-op. `SaveData.PeakBalance`도 항상 0이 저장되는 죽은 항목.
- **수정**: (안A) `GameManager.ChangeBalance`에서 `peakBalance = max(peakBalance, currentBalance)` 갱신 추가 + `AdjustPeakBalanceForExpenditure`에 canonical 위임·`SyncAllInstances()` 보강. (안B) 기능 폐기 — 필드·메서드·호출부·SaveData 필드 삭제. 이 값을 읽어 쓰려던 기획(고점 대비 하락 연출 등)이 살아 있는지에 따라 결정.
- **⚠️ 착수 중 추가 조사 — 미착수 사유**:
  - `TraderStatus.peakBalance`를 **읽는 코드가 저장 왕복과 `AdjustPeakBalanceForExpenditure` 자기 자신 외에 하나도 없습니다.** 즉 안A로 고점 추적을 붙여도 그 값을 소비하는 곳이 없어 아무 동작 변화가 없습니다. 드로다운 % 유지라는 주석상 목적을 실제로 쓰는 기능이 아직 없다는 뜻입니다.
  - 업적의 "누적 최고 자산"(백만장자 / 억만장자 I·II, 간호사·버니걸·비키니 보상)은 **완전히 별개 시스템**이며 정상 동작합니다 — `AchievementManager.RecordPeakBalance`가 `GameManager.cs:1502`에서 호출되어 PlayerPrefs(`Stat_GlobalPeakBalance`)에 기록됩니다. 이쪽은 손댈 필요가 없습니다.
  - 따라서 남은 선택지는 사실상 "드로다운 기획을 되살릴 것인가"이며, 폐기 시 `SaveData.PeakBalance` 삭제가 따라옵니다.
- **폐기 근거 (검증 완료)**: `peakBalance`는 **드로다운 트라우마 천장 기믹(Phase 1 "6대 기믹" 중 ⑥) 전용 필드**였습니다. 그 기믹은 [Mental_Drain_Rebalance_Plan.md](Mental_Drain_Rebalance_Plan.md) 5장 "트라우마 천장(`maxMentalLimit`) 전면 제거"로 폐지됐고, 코드에서 `HasDrawdownTrauma` / `MaxMentalLimit` / `CurrentDrawdownPercent`가 모두 사라진 것을 확인했습니다(씬 YAML에 직렬화 잔재만 남음). **그런데 그 제거 계획서에 `PeakBalance`는 언급조차 없어** 함께 정리되지 못하고 남았습니다 — 갱신 코드도 소비자도 없는 완전한 고아 필드입니다.
- **적용한 수정** (삭제 지점 전량):
  - `TraderStatus`: `peakBalance` 필드, `PeakBalance` 프로퍼티, `AdjustPeakBalanceForExpenditure()`, 저장 gather/scatter 2곳, 인스턴스 동기화 2곳, `ResetStatus` 초기화 1곳.
  - `GameManager`: `AdjustPeakBalanceForExpenditure` 호출 4곳(1013 / 1028 / 1211 / 1533) — 전부 no-op이었으므로 잔고 차감 동작은 그대로입니다.
  - `SaveData.PeakBalance` 필드 (사유 주석으로 대체).
- **⚠️ 남겨 둔 것 — `AchievementManager`의 "누적 최고 자산"은 완전히 별개 시스템입니다.** `RecordPeakBalance()` / `Pref_PeakBalance("Stat_GlobalPeakBalance")` / `AchievementType.PeakBalance`는 PlayerPrefs 기반 전역 기록이며 백만장자·억만장자 I·II 업적(간호사·버니걸·비키니 보상)을 구동합니다. **정상 동작 중이므로 손대지 않았습니다.** 이름이 같아 헷갈리기 쉬우니 주의.
- **세이브 호환**: 구버전 JSON에 남은 `"PeakBalance"` 키는 `JsonUtility`가 조용히 무시하므로 마이그레이션 불필요. 씬의 `peakBalance: 0` 직렬화 잔재는 Unity가 다음 씬 저장 때 정리합니다.

### P1-11. 코스튬 효과 5종이 설명만 있고 미구현 → **구현으로 결정·적용**
- [x] `Assets/Scripts/Items/CostumeManager.cs:123-165`, `Assets/Scripts/Items/ItemUser.cs`, `Assets/Scripts/Items/ShopManager.cs:111-145`
- **원인**: 메이드(파르페 회복 +15%) · 간호사(영양제/진정제 효율 +15%) · 한복/유카타(배달음식 회복 +15%) · 동탄룩(구매 비용 -15%)의 수치 로직이 `ItemUser`/`ShopManager` 어디에도 없음 (grep 결과 참조는 정의부·업적 매핑·스프라이트 오프셋뿐). 구현된 것은 8종(StreetCap/Pajama/JiraiKei/Qipao/Bartender/OfficeLook/BunnyGirl/Bikini).
- **결정 근거**: 설명 문구에 배율(+15% / -15%)과 대상 아이템이 이미 확정돼 있어 기획 의도가 명확하고, 기존 치파오·바텐더 구현 패턴을 그대로 따르면 되므로 구현이 문구 삭제보다 낫다고 판단했습니다.
- **적용한 수정** (에셋의 `effectType`을 확인해 각 아이템이 어느 경로를 타는지 대조했습니다 — 영양제·에너지드링크는 `Health`, 진정제·파르페는 `Mental`):
  - `CostumeManager.IsAnyEquipped(params string[])` 정적 헬퍼 추가 — 기존 인스턴스 메서드 `IsEquipped`를 감싸 null 검사와 묶음 판정을 한 곳에서 처리.
  - **한복·유카타**: 배달음식 회복 +15% (기존 치파오 분기에 합류)
  - **메이드**: 파르페(`dessert`) 멘탈 회복 +15% — `RestoreMental`
  - **간호사**: 영양제(`supplement`) +15% → `RestoreHealth`, 진정제(`sedative`) +15% → `RestoreMental`
  - **동탄룩**: `ShopManager.GetPurchasePrice`에서 최종가 ×0.85. 더불어 `ShopItemButton.cs:168`이 표시가로 `GetInflatedPrice`를 쓰던 것을 `GetPurchasePrice`로 바꿔 **표시가와 차감액이 어긋나지 않도록** 했습니다(할인이 표시에 반영되지 않는 문제 예방).

---

## P1 검증 항목 (Unity 에디터에서 확인 필요)

- [ ] **P1-2**: 고배율 중독 상태에서 50배 이하 저배율 매매를 2회 연속하면 요미가 주도권을 뺏고, 그 직후 **수동 모드로 되돌릴 수 없는지**. 이어서 요미가 강제 고배율 매매를 실행하면 다시 전환 가능해지는지.
- [ ] **P1-3**: 차트 빔이 없는 선택지(`ForceLeverage > 0`, `OverrideBeamPercent == 0`)를 골랐을 때 쉴드가 30초를 온전히 버티고 `handlingMode`가 적용되는지.
- [ ] **P1-5**: day 16 이후 서버 렉 발생 시 **차트가 실제로 멈췄다가** 3~5초 뒤 한 번에 갱신되는지. 렉 중 주문 버튼이 막히는 것이 자연스럽게 느껴지는지.
- [ ] **P1-6**: 파르페를 사용하면 업적 진행도가 오르는지(누적 100회 시 메이드 해금). 메이드 보유 상태로 저장 → 불러오기 후에도 유지되는지.
- [ ] **P1-8**: 하루 경계에서 선택 이벤트 텍스트가 폴백으로 떨어지지 않고 LLM 생성물이 나오는지.
- [ ] **P1-11**: 각 코스튬 착용 시 회복량·가격이 설명대로 바뀌는지. 특히 동탄룩 착용 시 **상점 표시가와 실제 차감액이 같은지**.

---

## P2 — 연출·밸런스·데이터 정합 (17건) — 2026-08-22 **전건 처리 완료**

> 4개 어셈블리 모두 **경고 0 · 오류 0**. 15건 코드 수정, 2건(P2-2 / P2-17)은 조사 결과
> **코드가 아니라 진단이 틀린 경우**로 판명되어 주석 정정·검증 기록만 남겼습니다.

### P2-1. 슬로우모션 연출이 씬당 1회로 사문화
- [x] `Assets/Scripts/Trading/TradingController.cs:228-229, 816-818, 1267-1272`
- **원인**: `isTargetBreakthroughSlowMotionTriggered`/`isMarginCallSlowMotionTriggered`를 false로 되돌리는 지점이 코드 전체에 없음.
- **적용한 수정**: `ClosePosition`과 `TriggerLiquidation`의 공통 초기화 블록에 두 플래그 리셋 추가. 두 연출은 포지션 단위 이벤트이므로 포지션이 끝날 때마다 다시 무장됩니다.

### P2-2. `OnFastForwardEnded`가 중단 시 발화하지 않음 (주석과 불일치)
- [x] `Assets/Scripts/GameManager.cs:1452-1460` (선언부 주석: 88행)
- **원인(감사 시점)**: 일시정지 break·오버도즈 중단 경로 모두 미발화 — 정상 완주에서만 발화.
- **⚠️ 조사 결과 — 코드가 아니라 주석이 틀렸습니다. 동작은 정상입니다.**
  - 중단된 고속 진행은 남은 분이 보존되었다가 `ResumeGame`(1628-1634) 또는 `ResumePreservedFastForward`(1470-1479)가 **`AdvanceGameMinutes`를 다시 호출**합니다. 즉 최종 완료 시점에 이벤트가 반드시 발화하며, "갱신 기회 상실"은 일어나지 않습니다.
  - 중단 시점에 발화시키면 오히려 해롭습니다 — `MarketSimulationEngine.HandleFastForwardEnded`가 진행 중인 신호 페이즈를 `None`으로 리셋하고 `isExternalEventOverride`까지 내립니다(P1-4에서 추가). 잠시 멈춘 것뿐인데 신호가 취소됩니다.
  - 중단 구간의 시계 표시도 문제없습니다. `TopStatusBarUIController`는 `OnGameMinuteAdvanced`(매분)와 `OnFastForwardEnded` **둘 다** 구독합니다.
- **적용한 수정**: 코드는 그대로 두고 `GameManager.cs:88`의 선언부 주석을 실제 계약("끝까지 소진되었을 때만 발행")으로 정정 + 중단 시 발행하면 안 되는 이유 명시.

### P2-3. 스킬 업그레이드가 옛 능력치로 시뮬레이션됨
- [x] `Assets/Scripts/Trading/TraderLevelSystem.cs:441-460`
- **원인**: `AdvanceGameMinutes`(동기 루프)를 레벨 증가·`OnSkillLevelChanged` 발화보다 **먼저** 실행 → 고속 진행 전체(강제청산 판정 포함)가 업그레이드 이전 레벨로 돌아감. `MarketSimulationEngine.cs:388`의 "새 능력치 반영" 로그도 거짓이 됨.
- **적용한 수정**: `AdvanceGameMinutes` 호출을 레벨 증가 switch와 `OnSkillLevelChanged` 발행 **뒤로** 이동 + 순서를 지켜야 하는 이유를 경고 주석으로 명시.

### P2-4. 보스 자산 세이브 누수 (gather 조건부 ↔ scatter 무조건)
- [x] `Assets/Scripts/System/SaveLoadManager.cs:211-216, 625-630`, `Assets/Scripts/System/BossManager.cs:197-207`
- **원인**: 보스 자산 gather는 `CurrentBoss != null`일 때만, scatter는 무조건. 저장값을 -1로 되돌리는 코드가 없어 5일차 보스 자산이 세이브에 잔존 → 12일차 보스가 `LoadedBossStartingAsset > 0f` 분기로 플레이어 자산 기반 스케일링을 건너뜀.
- **적용한 수정**: gather 조건을 `bossManager != null`로 바꾸고 보스가 없으면 **-1을 명시 기록**합니다. 부분 저장(직전 스냅샷을 베이스로 삼음) 구조라 명시하지 않으면 옛 값이 계속 살아남습니다. (`BossManager`는 이미 사용 후 `LoadedBossStartingAsset = -1f`로 소모 처리하고 있어 메모리 쪽은 정상이었습니다.)

### P2-5. 새 게임 1일차 알바 보상 소실
- [x] `Assets/Scripts/System/SaveLoadManager.cs:584-590` (지급: `StoreShiftManager.cs:557`)
- **원인**: `NeedsStartingItems == true`인 첫 GameScene 진입이 `GrantItemToSave`로 쌓인 목록을 읽지 않고 `ResetForNewGame()`만 수행. 스토리 모드 정상 플로우(요미 방 → 알바 → GameScene)에서 재현.
- **적용한 수정**: 인벤토리 복원 루프를 `ApplySavedInventoryItems()` 헬퍼로 뽑아 **두 분기가 공유**하게 했습니다. 시작 지급(`ResetForNewGame`) 뒤에도 이 헬퍼를 호출해 알바 선물이 얹힙니다. 새 게임의 저장 목록에는 `GrantItemToSave`로 들어온 것만 있으므로 중복 지급이 없습니다.

### P2-6. 저장 슬롯 인덱스가 저장 실패 시에도 변경됨
- [x] `Assets/Scripts/System/SaveLoadManager.cs:100-101, 117-130`
- **원인**: `ActiveStorySlotIndex` 변경이 상태 가드보다 앞 → 다른 슬롯 저장이 거부된 뒤의 모든 자동 저장이 새 슬롯을 향함.
- **적용한 수정**: `ActiveStorySlotIndex = slotIndex`를 상태·오버도즈 가드 **뒤로** 이동. 클램프는 `ReadSaveFile(slotIndex)`가 쓰므로 앞에 남겨 뒀습니다.

### P2-7. 슬롯 소비가 저장 실패를 무시
- [x] `Assets/Scripts/DatingSim/Core/DatingTimeManager.cs:141-157`
- **원인**: 시계를 먼저 밀고 `SaveCurrentGame()` 반환값을 버림 → 저장 거부 시 메모리와 디스크 불일치. `StoreShiftManager.cs:559-561`은 반환값을 확인하는데 여기만 안 함.
- **적용한 수정**: 반환값을 확인해 실패 시 경고를 남깁니다. **슬롯·시계를 되돌리지는 않습니다** — 되돌리면 플레이어 행동만 무효가 되고, `CurrentData` 변경은 메모리에 남아 다음 성공 저장에 딸려 갑니다(`StoreShiftManager.Commit`과 같은 방침).

### P2-8. TraderStatus 비정본 세터 — canonical 위임 누락
- [x] `Assets/Scripts/TraderStatus.cs:94-118`, `Assets/Scripts/AI/MentalDrainGimmickController.cs:143, 250, 379, 471`
- **원인**: `CurrentLosingStreak`/`IsLeverageAddicted`/`ConsecutiveHighLevWins`/`ConsecutiveLowLevTrades`/`PeakBalance` 세터에만 canonical 위임 가드가 없음. 쓰기 지점 전부가 이 세터를 쓰는 `MentalDrainGimmickController`는 `traderStatus`를 `FindAnyObjectByType`(정본 보장 없음)으로 잡음 — GameScene에 TraderStatus 2개 직렬화 확인. 미러에 쓰면 다음 동기화가 조용히 삼킴.
- **적용한 수정**:
  - `Owner` 프로퍼티(정본이 있으면 정본, 없으면 자기 자신)를 두고 네 세터의 **읽기·쓰기 양쪽**을 통과시켰습니다. 쓰기만 위임하면 미러에서 쓴 직후 읽을 때 한 프레임 낡은 값이 나옵니다.
  - `MentalDrainGimmickController`의 `FindAnyObjectByType<TraderStatus>()` 4곳을 전부 `TraderStatus.CanonicalInstance`로 교체하고, `CureMentalGimmicks`에 null 가드 추가.
  - `PeakBalance`는 P1-10에서 통째로 삭제되어 이 목록에서 빠졌습니다.

### P2-9. 보스 스폰이 두 번째 AITradingBrain을 만들어 기믹 대상이 비결정적
- [x] `Assets/Scripts/System/BossManager.cs:216-220`, `Assets/Scripts/AI/MentalDrainGimmickController.cs:73, 106, 146-154`, `Assets/Scripts/AI/AITradingBrain.cs:64-68`
- **원인**: `[RequireComponent]`로 보스 전용 브레인이 추가 생성되고, 멘탈 기믹이 `FindAnyObjectByType<AITradingBrain>()`으로 어느 쪽을 잡을지 보장 없음 → `ForceNextTradeHighLeverage`가 보스에 걸릴 수 있음 (엔드리스/챌린지에서만 노출). 보스 브레인이 플레이어 `TradingController` 이벤트를 구독하는 격리 위반도 동반.
- **적용한 수정**:
  - `MentalDrainGimmickController.FindPlayerBrain()` 추가 — 모든 브레인을 훑어 `!IsBossAI`인 것만 돌려줍니다. 탐색 3곳을 전부 이걸로 교체.
  - `AITradingBrain`의 `TradingController` 구독을 `!IsBossAI`로 감쌌습니다.
  - **덤으로 발견해 함께 고침**: 같은 자리의 `FindAnyObjectByType<MentalDrainGimmickController>()`가 기본값(비활성 제외)이라, 기존 인스턴스가 비활성 오브젝트에 있으면 새로 `AddComponent`하고 → 그 컴포넌트는 `Awake`에서 중복 판정으로 스스로를 `Destroy` → 곧 사라질 컴포넌트에 `Initialize`가 걸려 **모든 멘탈 기믹이 죽는** 경로가 있었습니다. `FindObjectsInactive.Include`로 교정.

### P2-10. `MentalRatio`가 코스튬 최대치 보너스를 무시
- [x] `Assets/Scripts/TraderStatus.cs:160`
- **원인**: `HealthRatio`는 보너스 포함 프로퍼티를 나누는데 `MentalRatio`만 원시 필드 `maxMental`을 나눔 → 잠옷(+15) 착용 시 비율 최대 1.15. HUD 슬라이더 항상 만땅 표시, 저멘탈 이벤트 임계치(`MentalRatio <= 0.15`)가 실질 17.25로 밀림.
- **적용한 수정**: `maxMental` → `MaxMental` 프로퍼티 사용.

### P2-11. `RestoreFromSaveData`가 멘탈 상태 전이·동기화를 생략
- [x] `Assets/Scripts/TraderStatus.cs:198-217`
- **원인**: 값만 복원하고 `UpdateMentalState()`/`SyncAllInstances()` 미호출 → `OnMentalStateChanged` 미발화로 위험 상태 세이브 로드 시 구독자가 옛 상태 유지.
- **적용한 수정**: `lastTrackedMentalState` 정렬 + `OnMentalStateChanged` 발행 + `SyncAllInstances()`를 복원 말미에 추가.
- **⚠️ `UpdateMentalState()`는 일부러 부르지 않았습니다.** 그 메서드는 `currentMental <= 0f`이면 오버도즈 강제매매(`TriggerOverdoseTrade`)와 슬로우모션을 실행합니다 — 멘탈 0인 세이브를 불러오는 순간 연출이 터집니다. 필요한 것은 추적기 정렬과 구독자 통지뿐이므로 그 둘만 직접 수행합니다.

### P2-12. `ResetEngine` 부분 초기화 (RestoreFromSaveData와 불일치)
- [x] `Assets/Scripts/Trading/MarketSimulationEngine.cs:392-425` (대조: 301-318)
- **원인**: `isOverdoseTrapOverride`/`overdoseTrapEndTime`/`isServerLagging`/`serverLagTimer`/`currentVolatility` 초기화 누락 — 같은 역할의 `RestoreFromSaveData`는 전부 초기화함. 일차 전환이 함정/렉 도중이면 새 날 첫 틱까지 상태가 넘어옴.
- **적용한 수정**: `ResetEngine`에 `isOverdoseTrapOverride` / `overdoseTrapEndTime` / `isServerLagging` / `serverLagTimer` / 렉 누산기 2개를 추가. 확인해 보니 `RestoreFromSaveData`도 앞의 두 개만 초기화하고 있어 **양쪽 목록을 같게** 맞췄습니다 — 불러오기 직후 차트가 몇 초간 멈춘 채 시작하는 경우를 막습니다.
- `currentVolatility`는 매 틱 국면·일차에서 다시 계산되어 자가 교정되므로 어느 쪽에도 넣지 않았습니다.

### P2-13. EventView가 UI 버튼 클릭을 대사 진행으로 이중 소비
- [x] `Assets/Scripts/Events/Story/EventView.cs:259-265, 484, 490`
- **원인**: 로그/설정을 **여는** 클릭 시점엔 `logOpen == false`라 `clickPending = true`가 서고 대사가 한 줄 넘어감 (닫는 클릭만 방어돼 있음).
- **⚠️ `IsPointerOverGameObject` 방식은 쓸 수 없습니다.** `ClickCatcher`가 전체 화면을 덮는 투명 Button(`onClick → clickPending = true`)이라 이벤트 진행 중에는 그 검사가 **항상 참**이 됩니다. 그대로 넣으면 대사 진행 입력이 통째로 죽습니다. (착수 중 확인해 되돌렸습니다.)
- **적용한 수정**: `topButtonClickFrame` 필드를 두고 기록·설정 버튼의 `onClick`에 `MarkTopButtonClick`을 함께 붙였습니다. `Update`는 그 프레임과 **다음 프레임까지** 진행 입력을 세지 않습니다 — EventSystem의 버튼 처리와 `Update`의 실행 순서가 보장되지 않기 때문입니다.

### P2-14. 서버 렉 중 버튼 차단과 차트 진행의 모순
- [x] `Assets/Scripts/Trading/TradingController.cs:978, 1069-1073`
- **P1-5(안A 구현)로 해소됨.** 렉 구간 동안 가격이 실제로 멈추므로 버튼 차단이 연출 의도와 일치하게 됐습니다. 조기 return 2곳은 그대로 둡니다 — 이 항목은 코드 변경 없음.

### P2-15. 주문 버튼이 게임 상태(Settlement/GameOver)를 반영하지 않음
- [x] `Assets/Scripts/UI/Chart/TradingPanelUIController.cs:1256`
- **원인**: `canEnter`에 게임 상태 항이 없어 정산/게임오버 중에도 `interactable = true` — 현재는 오버레이 raycast 차단에 우연히 의존.
- **P1-7 수정에 동반 적용 완료.** `canEnter`에 `gameManager.CurrentState == Playing` 항을 추가했습니다.

### P2-16. 100x/125x 레버리지 버튼 영구 비활성 + LV1 증거금 프리셋 무반응
- [x] `Assets/Editor/TradingViewUIBuilder.cs:657`, `Assets/Scripts/UI/Chart/TradingPanelUIController.cs:948-984`, `Assets/Scripts/Trading/TraderLevelSystem.cs:155-175`
- **원인**: ① 100x/125x 버튼이 씬·빌더 모두 비활성으로 저장되고 다시 켜는 코드 없음 — LV15/LV20 보상이 UI 미노출. ② LV1 상한 15%가 프리셋(10/25/50/75/100)과 어긋나 클램프 결과 어떤 프리셋도 하이라이트 안 되고 스테퍼가 10↔15만 오감.
- **적용한 수정**:
  - ① `RefreshLeveragePresetAvailability(maxAllowed)` 추가 — 100x는 상한 100 이상, 125x는 125 이상일 때 노출. `SelectLeverage`와 `RefreshPanelUI` 양쪽에서 호출해 레벨업이 곧바로 반영됩니다. 에디터 빌더의 `SetActive(false)`는 **초기 상태**로 남기고 노출 소유권이 런타임에 있다는 주석을 달았습니다.
  - ② `UpdateMarginPreset(btn, presetPercent, maxAllowedPercent)` 추가 — 상한을 넘는 프리셋은 `interactable = false`로 잠급니다. 눌러도 조용히 클램프되던 것이 이제 잠긴 것으로 보이고, 하이라이트는 정확히 일치할 때만 켜집니다. (LV1에서는 10%만 활성, 15%는 ±10 스테퍼로 도달.)

### P2-17. 폰트·텍스트 전역 훅 3건
- [x] `Assets/Scripts/UI/GlobalPFStardustFont.cs:22-32, 137-155`, `Assets/Scripts/UI/MobileFontScaleController.cs:88`
- **⚠️ 조사 결과 세 건 모두 현재 실피해가 없어 코드를 바꾸지 않았습니다.** 근거를 남깁니다.
  - **①은 무해합니다.** 씬에 직렬화된 `font` 값(`DynamicShopUI`·`SettingsMenuController`)의 GUID가 `d1468a85461624a529d0385449060eb0`인데, 이는 `TMP Settings`의 `m_defaultFontAsset`과 **완전히 같은 에셋**입니다. 즉 훅이 되돌리는 값과 지정 값이 동일해 덮어쓰기가 no-op입니다. `ConfigureCompactHudText`의 `preferredFont`도 호출부 4곳 중 3곳이 `null`, 1곳이 같은 기본 폰트입니다. 존재하지 않는 문제를 위해 예외 마커를 도입하는 것은 불필요한 복잡도라 보류합니다 — **실제로 다른 폰트를 지정하게 되는 시점에** 마커를 넣으십시오.
  - **②는 무한 루프가 아닙니다.** `SetAllDirty()`가 `havePropertiesChanged`를 세우지만 바로 다음 줄의 `ForceMeshUpdate(true, true)`가 메쉬 생성 끝에서 그것을 **다시 내립니다**. 따라서 다음 프레임 검사에서 재충전 조건이 성립하지 않고 `pendingFrames`가 3→0으로 정상 소진됩니다.
  - **③은 모바일 빌드 한정**이며 실기기 확인이 선행되어야 합니다. 그대로 남깁니다.

---

## P2 검증 항목 (Unity 에디터에서 확인 필요)

- [ ] **P2-1**: ROE +50% 돌파와 마진콜 임박 슬로우모션이 **매 포지션마다** 재생되는지(예전에는 씬당 1회).
- [ ] **P2-3**: 스킬 업그레이드 직후 고속 진행 구간이 **새 레벨** 기준으로 흘러가는지.
- [ ] **P2-5**: 새 게임 1일차에 편의점 알바를 먼저 하고 GameScene에 들어갔을 때 알바 선물이 인벤토리에 있는지.
- [ ] **P2-13**: 이벤트 중 "기록"·"설정" 버튼을 눌렀을 때 대사가 함께 넘어가지 않는지. 반대로 **화면 아무 데나 클릭하면 대사는 정상 진행되는지**(가장 중요 — 진행 입력을 죽이지 않았는지 확인).
- [ ] **P2-16**: LV15/LV20 도달 시 100x·125x 버튼이 나타나는지. LV1에서 25%+ 증거금 프리셋이 잠겨 보이는지.

---

## P3 — 죽은 코드 정리 (9건) — 2026-08-22 **처리 완료** (순삭감 약 360줄)

> 4개 어셈블리 모두 **경고 0 · 오류 0**. 삭제 전 모든 대상의 호출자 부재를 다시 grep으로 확인했고,
> 그 과정에서 **감사 보고가 "사문"이라 판정한 것 중 2건이 실제로는 살아 있었습니다**(아래 P3-1 참조).

### P3-1. 호출자 없는 메서드 삭제
- [x] **삭제함**: `MarketSimulationEngine.TriggerMacroEvent` / `TriggerMarketShock`("순간이동 제거" 리팩터링 잔해), `TradingController.SimulateCloseForTest`(테스트 러너도 미사용 + `ClosePosition`과 초기화 목록이 갈라져 되살리면 오동작), `TopStatusBarUIController.AddOrStyleRule`, `CandleItemUI.GetXPos` / `CurrentData`.
- [x] **⚠️ 삭제하지 않음 — 감사 보고가 틀렸습니다**: `EventLogicTemplateSO.HasFallbackText`는 **에디터 스크립트 2곳에서 실제로 쓰입니다** (`ChoiceEventDebugMenu.cs:166`, `GenerateTemplateFallbackText.cs:271`). 호출자 0건이라는 판정은 오류였습니다.
- [x] **보류 (의도적)**: `GameManager.AdvanceDate`, `EventLauncher.HasFlag`. 둘 다 **프롤로그·미연시 스토리 작업에서 쓸 물건**입니다(P1-1 참조). 특히 `AdvanceDate`의 주석은 "보스·스토리 예약일을 건너뛰면 엔딩이 영영 발생하지 않는다"는 비자명한 위험을 담고 있어, 지우면 그 지식이 함께 사라집니다.
- `ChartUIController.currentPriceLineImage`는 에디터 빌더가 연결하고 있어 남겨 뒀습니다(빌더 수정이 따라와야 함).

### P3-2. P2P 차트 경로 (도달 불가) — **삭제함**
- [x] `Assets/Scripts/UI/Chart/ChartUIController.cs`
- **`[기획 결정 필요]`가 아니었습니다.** 확인해 보니 P2P는 이미 **`MarketSimulationEngine.ApplyP2PExternalTick` / `PrepareP2PChartHistory`로 기존 엔진에 가격을 먹이는 방식**으로 동작 중입니다(`P2PGameplayUIController.RefreshOriginalChart`). `ChartUIController`의 P2P 경로는 그 이전 접근법의 잔재였습니다.
- **적용한 수정**: `ApplyP2PSnapshot` / `GetP2PCandles` / `GetP2PLiveCandle` / `UpdatePriceHeaderP2P` 4개 메서드와 `p2pMinuteCandles` / `p2pLiveCandle` / `p2pCurrentPrice` / `useP2PMarket` 필드, 사용처 4곳의 삼항 분기, `using FXOverdose.P2P.Market`까지 제거.

### P3-3. TraderMemoryManager 단기 기억 / YomiSpriteController
- [x] **`YomiSpriteController.cs`(604줄) 삭제.** 씬·프리팹 어디에도 배치돼 있지 않고(`.unity`/`.prefab` 전수 확인), 유일한 외부 참조인 `P2PGameplayUIController.cs:250`은 바로 옆에서 `AIVisualController`에 같은 인자로 같은 호출을 하고 있어 그 한 줄만 지웠습니다.
- [ ] `TraderMemoryManager` 단기 기억(`RecordDialogue` / `GetShortTermDialoguesText` / `dailySummaries`)은 **남겼습니다.** 자유 채팅 제거의 잔재이며, 대체 대화 시스템 설계에서 재사용할지 정해진 뒤 정리하는 것이 맞습니다. `[기획 결정 필요]`

### P3-4. 멘탈 기믹 잔해
- [x] 빈 껍데기였던 `OnGameMinuteAdvanced` 핸들러와 **`GameManager.OnGameMinuteAdvanced` 구독까지** 제거(매 인게임 분 헛돌던 호출이 사라짐). 클래스 주석과 초기화 로그의 "6대 기믹"을 실제인 **5종**으로 정정.
- [ ] `TriggerGimmickDialogue`의 `gimmickContext` 인자는 **남겼습니다.** 호출부 8곳이 만드는 상세 컨텍스트 문자열은 원래 LLM 대사 생성용이며, 대체 대화 시스템이 그대로 쓸 재료입니다. `[기획 결정 필요]`

### P3-5. 도달 불가 감정 3종 — **보류**
- [ ] `Furious` / `Jealous` / `Affectionate`. `Furious`는 강제청산 감정으로 정의돼 있고 `AIVisualController`의 오라 분기도 이미 갖춰져 있어, **평가기에 분기를 추가하는 쪽이 자연스럽습니다**(삭제가 아니라). 연출 기획 판단이 필요해 남깁니다. `[기획 결정 필요]`

### P3-6. 검수용 킬스위치·1회성 코드 — **전량 제거**
- [x] `AchievementManager`: `DisableAchievementRequirementsForTesting`(true면 모든 코스튬 잠금 해제), `UnlockMaidAchievementForReview`, 1회성 롤백 블록과 `Pref_MaidReviewRollbackApplied`.
- [x] `CostumeManager`: `UnlockAllCostumesForReview`, `UnlockMaidCostumeForReview`, `ApplyReviewOwnership()`와 호출 3곳.

### P3-7. 업적·카운터 정리
- [x] `RecordLevelUp` **삭제** — 쓰던 `Pref_Level9Reached` / `Pref_HighestLevel`을 읽는 판정이 하나도 없었습니다. 상수·`DeleteKey`·호출부(`TraderLevelSystem`)까지 함께 정리.
- [x] `purchase_delivery_200` 카운터 교정 — `RecordItemPurchase()`가 인자 없이 **모든 소모품** 구매를 세던 것을 `RecordItemPurchase(ItemData)`로 바꿔 **배달음식만** 세게 했습니다. 배달음식을 한 번도 사지 않아도 스테이크가 해금되던 문제가 사라집니다.
- [x] `IsUnlocked` 삭제하고 `IsAchievementUnlocked`로 통합(호출부 1곳 교체).

### P3-8. UI 소소 정리
- [x] **차트 Y축 매핑 통일** — `GetPriceArea(chartHeight, out bottom, out top)` 하나로 모았습니다. 현재가 라인·진입선·**캔들 본체**(`CandleItemUI`에 인자로 전달)·Y축 눈금 라벨이 모두 같은 값을 씁니다. 하드코딩 `0.26f` / `0.74f` 제거 — 이제 `volumeAreaRatio`를 바꿔도 어긋나지 않고, 상단 4px 불일치도 사라집니다.
- [x] **람다 구독 2건** — `TradingPanelUIController`(해제 목록 누락)와 `SettingsMenuController`(`-=`가 no-op이라 구독 누적)를 모두 메서드 그룹으로 바꾸고 `OnDestroy` 해제를 추가.
- [x] **증거금 텍스트 이중 기입** — `SelectMarginRatio` 쪽 기입 제거(`Update`가 매 프레임 갱신하므로 소유권을 그쪽에 둠).
- [x] **현재가 태그 경쟁 기입** — 포지션 보유 중에는 `UpdatePositionDirectionVisuals`가 소유하고 `UpdateCurrentPriceLine`은 쓰지 않도록 분리.
- [x] **HP/멘탈 슬라이더** — `HUDController`가 매 프레임 `EnsureSliderVisualSetup`으로 `fillRect` 앵커를 강제 리셋해 `TopStatusBarUIController.StyleVitals`의 조정을 계속 지우던 것을 제거(앵커 보정은 초기화 1회면 충분). 값 기입은 출처가 같아 충돌하지 않으므로 그대로 둡니다.
- [x] **P&L 금액 라벨** — 영구 숨김 상태에서 매 프레임 갱신하던 것을 `activeSelf` 검사로 건너뜁니다.
- [x] **효과 HUD 비음식 아이콘 분기 제거** — `CreateEffectIcon`의 뱃지 경로는 `activeItemStates`가 채워지는 곳이 없어 도달 불가였습니다. 시그니처를 `CreateEffectIcon(ItemData, Color)`로 축소하고 `activeItemStates` / `EffectIconView.Badge`도 삭제.
- [x] **`TutorialManager.SetButtonsInteractable` → `BlockAllInput()`** — 이름과 달리 `Button.interactable`을 건드리지 않고 차단막만 토글했고, 호출부 7곳이 전부 `false`만 넘겨 `specificBtn`·`true` 분기가 사문이었습니다. 실제 동작에 맞게 개명·축소.
- [x] **`ResetToNormal`의 남의 Canvas 파괴 방지** — `overrideSorting && sortingOrder == 1000`만 보고 지우면 원래부터 그 설정이던 Canvas까지 파괴하고 복원할 수 없었습니다. 튜토리얼이 만든 것만 `HashSet<GameObject>`로 기억해 그것만 지웁니다.

### P3-9. 저장 데이터 계약 정리
- [x] `SaveData.GameMode`에 "사실상 기록 전용 — `PrepareLoadGame`이 로드 시 무조건 Story로 덮어씀" 주석 추가. `SecondsPerGameMinute`는 이미 진단 전용임이 명시돼 있어 그대로 둡니다.
- [x] `NeedsStartingItems`에 **기본값을 true로 뒤집지 말 것**과 그 이유(구버전 세이브의 인벤토리가 로드마다 리셋됨), 뒤집으려면 마이그레이터 백필이 선행되어야 함을 경고로 명시. 마이그레이터에 항목을 추가하지 않은 이유는 현재 `JsonUtility`의 초기화자 유지 동작이 **이미 올바르게** 처리하고 있어서입니다(가설적 위험에 대한 선제 코드는 넣지 않음).
- [x] `TraderLevelSystem.ResetLevels`가 `OnSkillLevelChanged`를 3종 모두 발행하도록 수정 — 새 게임 직후 HUD에 이전 판 스킬 레벨이 남던 문제.
- [x] `BossManager.ClearBoss`에 `IsBossBankrupt = false` 추가.
- [x] `GameManager.StartNewGame`이 `AllowsSaving`을 확인한 뒤에만 저장하도록 수정 — 엔드리스·챌린지에서 매번 나던 경고 로그 제거.
- [ ] `Inventory.cs`의 `sedative` 시작 지급 분기는 **남겼습니다.** 씬의 인벤토리 슬롯에 진정제가 직렬화돼 있지 않아 사문이지만, 해결책이 "씬에 슬롯 추가"(= 시작 아이템 구성 변경)라 밸런스 판단이 필요합니다. `[기획 결정 필요]`

---

## P3 검증 항목 (Unity 에디터에서 확인 필요)

- [ ] **P3-8 (차트)**: 캔들·현재가 라인·Y축 눈금이 **같은 가격에서 같은 높이**에 있는지. 포지션 보유 시 현재가 태그가 깜빡이지 않는지.
- [ ] **P3-8 (튜토리얼)**: `BlockAllInput()`으로 개명한 뒤에도 각 단계의 입력 차단이 그대로인지.
- [ ] **P3-3**: `YomiSpriteController` 삭제 후 씬에 깨진 스크립트 참조(Missing Script)가 없는지 — 배치 흔적이 없음을 확인했지만 에디터에서 최종 확인 권장.
- [ ] **P3-7**: 배달음식이 아닌 소모품을 사도 `purchase_delivery_200` 진행도가 오르지 않는지.
- [ ] **P3-6**: 검수 킬스위치 제거 후 코스튬 잠금이 정상 동작하는지.

> ⚠️ `Assembly-CSharp.csproj`에서 `YomiSpriteController.cs` 항목을 수동으로 지우고 빌드 검증했습니다. 이 csproj는 Unity가 재생성하므로 에디터를 한 번 열면 정상화됩니다.

---

## 감사에서 정상 확인된 항목 (수정 불요)

- 일시정지 소유권: 호출자 4곳(설정/상점/선택이벤트/스토리이벤트) 모두 "내가 멈췄는가" 플래그로 자기 방어 — 중첩 안전.
- `Time.timeScale` ↔ `DynamicTimeRegulator` 충돌 없음 (조절기는 `SetSecondsPerGameMinute`만 사용).
- `GameCalendar` 전체, `OnGameMinuteAdvanced`/`OnDayEnded`/`OnPriceUpdated`/`OnCandleClosed` 등 핵심 이벤트 발화-구독 쌍, SaveData 주요 그룹(Market 9필드 · Dating 8필드 · Talk 13필드 · 이벤트 계약 5필드 등)의 gather/scatter 대칭.
- `ChoiceEventController.OnOptionSelected`의 아이템 부족 조기 return (팝업 유지되므로 안전), `AdvanceClockWithoutSimulation`의 시계 점프 보정.

## 진행 규칙

1. **순서**: P0 전체 → P1 중 `[기획 결정 필요]` 아닌 것(P1-2/3/4/6/7/8/9) → 기획 결정 항목(P1-1/5/10/11, P3 일부) → P2 → P3.
2. **컴파일 검증**: 매 묶음마다 `dotnet build "Assembly-CSharp.csproj" -v:m` + `dotnet build "Assembly-CSharp-Editor.csproj" -v:m` (P2P를 건드리면 P2P csproj 2종도).
3. **테스트**: P1-7 등 P2P 접점 수정 시 Unity Test Runner EditMode 실행. 트레이딩 코어 수정 후 `FXOverdose/Debug/AI Trading System Integration Test` 실행.
4. **저장 형식 변경**(P2-4/5, P3-9): `SaveData` + gather + scatter + `SaveDataMigrator` 4곳 동시 수정 원칙 준수.
5. **구조 변경 발생 시** `docs/P2_04_System/Refactored_Architecture_Master.md`에 기록.
6. 새 한국어 UI 문자열 추가 시 `Tools/Prebake All Scripts Text into Font` 재실행.
