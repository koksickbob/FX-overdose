# 보스·위약금·20일 제한 임시 비활성화 (2026-08-15)

**상태**: **적용 완료** — 스토리 개편 기간 동안 유지
**사유**: 세 시스템이 스토리 편입 작업을 방해한다. 개편 후 되살린다.
**관련**: [Settlement_In_YomiRoom_Plan.md](Settlement_In_YomiRoom_Plan.md) 12절 #3을 해소한다 (아래 4.3)

| 스위치 | 위치 | 값 | 적용 범위 |
| --- | --- | --- | --- |
| `BossesEnabled` | `BossManager` | `false` | **스토리 모드만** (6.1) |
| `StoryPenaltiesEnabled` | `GameManager` | `false` | 스토리 모드만 — 원래 그러함 (6.3) |
| `StoryDayLimitEnabled` | `GameManager` | `false` | **스토리 모드만** (6.1) |

> ### ⚠️ 스토리 모드에서는 성공 엔딩에 도달할 수 없습니다
> 성공 판정은 「최종 보스 격파」와 「20일차 백업 판정」 둘뿐인데 **양쪽이 다 닫혔습니다.** 남는 결말은 **파산과 오버도즈**뿐이며, 게임은 20일차를 넘겨 무한히 이어집니다. 개편 기간의 의도된 상태입니다 — 자세한 근거는 3.1.
>
> **엔드리스·챌린지·P2P는 영향을 받지 않습니다.** 보스전과 종료 조건이 그 모드들의 존재 이유이므로 스위치와 무관하게 종전대로 동작합니다 — 자세한 내용은 6절.

---

## 1. 방식 — 주석이 아니라 관문 스위치

소비 지점이 **보스 11곳 / 위약금 5곳**에 흩어져 있다. 전부 주석 처리하면 되살릴 때 누락이 나고, 죽은 코드가 남아 다음 사람이 "이건 왜 죽어 있나"를 매번 다시 조사하게 된다. **하위 경로가 반드시 지나가는 관문 한 곳씩**을 끊었다.

### 1.1 보스 — `BossManager.BossesEnabled = false`

[BossManager.cs](../../Assets/Scripts/System/BossManager.cs)
```csharp
public static readonly bool BossesEnabled = false;

// 스위치가 꺼져도 스토리가 아닌 모드에서는 계속 동작합니다. (6.1)
private static bool BossesActive =>
    BossesEnabled || (SaveLoadManager.Instance != null &&
                      SaveLoadManager.Instance.CurrentGameMode != GameMode.Story);

public bool HasBossToday(int day) { if (!BossesActive) return false;  ... }
public BossData GetBossData(int day) { if (!BossesActive) return null; ... }
public void SpawnBossForDay(...)     { ClearBoss(); if (!BossesActive) return; ... }
```
보스의 모든 경로가 이 세 메서드를 지난다. 스폰만 관문을 하나 더 둔 것은 UI 설치·AI 오브젝트 생성이라는 **부작용**이 있기 때문이다.

> **`const`가 아니라 `static readonly`인 이유**: `const`로 두면 뒤 코드가 도달 불가로 판정돼 `CS0162` 경고가 쏟아지고, 되살릴 때까지 그 경고가 진짜 문제를 가린다. 현재 빌드는 경고 0개다.

### 1.2 위약금 — `GameManager.StoryPenaltiesEnabled = false`

위약금은 `evt.isPenalty && evt.penaltyAmount > 0`을 읽는 곳이 5군데라 **관문이 될 함수가 없다.** 대신 `Awake`에서 **데이터를 0으로 만든다.**
```csharp
private void DisableStoryPenaltiesIfNeeded()
{
    if (StoryPenaltiesEnabled || storyEvents == null) return;
    foreach (var evt in storyEvents)
    {
        if (evt == null || !evt.isPenalty) continue;
        evt.isPenalty = false; evt.penaltyAmount = 0f;
    }
}
```
읽는 쪽 5곳이 전부 자연스럽게 "위약금 없음"을 본다. 씬 에셋은 건드리지 않으므로 **`true`로 되돌리면 원값이 그대로 복귀**한다.

---

## 2. 죽었는지 검증 — 전 소비 지점

### 2.1 보스 (11곳)

| # | 위치 | 결과 |
| --- | --- | --- |
| 1 | `SyncBossForCurrentDay` ([GameManager.cs:409](../../Assets/Scripts/GameManager.cs#L409)) | `HasBossToday` false → 조기 반환 ✅ |
| 2 | 차트 예열 후 분기 ([:487](../../Assets/Scripts/GameManager.cs#L487)) | else 분기 → 정상 `Playing` 진입 ✅ |
| 3 | `ProceedToNextDay` 승패 판정 ([:778](../../Assets/Scripts/GameManager.cs#L778)) | 블록 통째로 건너뜀 ✅ |
| 4 | `FinalizeProceedToNextDay` 아침 등장 ([:890](../../Assets/Scripts/GameManager.cs#L890)) | `hasBossMorningEvent = false` ✅ |
| 5 | `SpawnBossForDay` 호출 2곳 ([:1004](../../Assets/Scripts/GameManager.cs#L1004), `PlayBossMorningSequence`) | 이중 관문으로 무동작 ✅ |
| 6 | `BossBattleUIBootstrap.EnsureInstalled` | `SpawnBossForDay` 안에서만 호출 → **HUD가 설치되지 않음** ✅ |
| 7 | `BossBattleUIController.RefreshFromManager` | `CurrentBoss == null` → `SetVisible(false)` ✅ |
| 8 | `BossAIController` 생성 | 생성되지 않음 ✅ |
| 9 | 세이브 기록 ([SaveLoadManager.cs:212](../../Assets/Scripts/System/SaveLoadManager.cs#L212)) | `CurrentBoss != null` 게이트 → 미기록 ✅ |
| 10 | 세이브 복원 ([:625](../../Assets/Scripts/System/SaveLoadManager.cs#L625)) | `LoadedBoss*`에 값은 들어가나 `SpawnBossForDay`가 소비하지 않음 → 무해한 잔존 ⚠️ |
| 11 | `AITradingBrain.IsBossAI` | 설정 주체가 `BossAIController`뿐 → 항상 false → **평상시 자동 매매 경로 정상** ✅ |

> **⚠️ #11이 가장 중요하다. `AITradingBrain`은 보스 시스템이 아니다.** `IsBossAI` 분기를 갖고 있을 뿐 평상시 자동 매매의 실행 주체이며, 함께 끄면 자동 매매·FOMO 후회 기믹·차트 힌트가 같이 죽는다. (CLAUDE.md 7.7과 같은 함정)

### 2.2 위약금 (5곳)

| # | 위치 | 결과 |
| --- | --- | --- |
| 1 | 24:00 파산 예측 `expectedPenalty` ([:663](../../Assets/Scripts/GameManager.cs#L663)) | 0 → 파산 예측이 완화되는 **안전한 방향** ✅ |
| 2 | 지출 예고 대사 `AnnounceTodayExpenses` ([:741](../../Assets/Scripts/GameManager.cs#L741)) | 위약금 문구 사라지고 정기 지출 문구만 ✅ |
| 3 | 정산 시 차감 ([:784](../../Assets/Scripts/GameManager.cs#L784)) | 건너뜀 ✅ |
| 4 | 16일차 아침 차감 ([:970](../../Assets/Scripts/GameManager.cs#L970)) | 건너뜀 ✅ |
| 5 | 정산 UI 표시 2곳 ([DailySettlementUIController.cs:266](../../Assets/Scripts/UI/DailySettlementUIController.cs#L266), [:336](../../Assets/Scripts/UI/DailySettlementUIController.cs#L336)) | `isPenalty` false → 위약금 행·요미 대사 미표시 ✅ |

`TraderStatus.AdjustPeakBalanceForExpenditure`도 위약금 경로에서만 호출되던 것이라 함께 멈춘다. 돈이 나가지 않으므로 최고점 보정도 필요 없다 — 정합 ✅

---

## 3. 시스템적으로 문제가 되는 것

### 3.1 ★★ 성공 엔딩이 닫혔다

성공 판정은 두 갈래뿐이었다.

| 경로 | 상태 |
| --- | --- |
| 최종 보스(20일차 사채업자) 격파 ([GameManager.cs:850](../../Assets/Scripts/GameManager.cs#L850)) | `BossesEnabled = false`로 **닫힘** |
| 20일차 백업 판정 (`totalEquity > StartOfDayEquity × 0.9`) | `StoryDayLimitEnabled = false`로 **닫힘** |

**따라서 지금은 성공 엔딩이 발생하지 않는다.** 도달 가능한 결말은 파산(`Bankruptcy`)과 오버도즈(`Overdose`)뿐이고, 게임은 20일차를 넘겨 무한히 이어진다.

이는 **의도된 상태다.** 20일 제한을 없애기로 한 이상 "20일차에 승패를 가른다"는 판정 자체가 성립하지 않으며, 새 종료 조건은 스토리 개편에서 정의된다. 임시 승리 조건을 지금 만들면 개편 때 버려질 상수를 하나 더 남길 뿐이다.

**그동안 유지되는 압박**: 정기 지출은 21일차부터 3일마다 1.3배씩 불어난다 ([GameManager.cs:705](../../Assets/Scripts/GameManager.cs#L705)). 무한 진행이어도 잔고 압박은 계속 커지므로 "아무 일도 일어나지 않는 상태"가 되지는 않는다.

**무한 진행에서 확인해 둘 것**:
- 시장 난이도는 **25일차에서 상한**에 걸린다 ([MarketSimulationEngine.cs:490](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L490)) — 그 이후로는 난이도가 더 오르지 않는다
- 보스 편성표는 20일차가 마지막이라 되살려도 21일차 이후에는 보스가 없다
- 달력은 임의 길이를 수용한다(월말·연말 자동) — 20일차 = 7월 15일 이후로도 정상 진행

### 3.2 Overdose 엔딩은 살아 있다 ✅

보스 패배 경로([:874](../../Assets/Scripts/GameManager.cs#L874))는 죽지만, 멘탈 오버도즈 경로 2곳([:1228](../../Assets/Scripts/GameManager.cs#L1228), [:1256](../../Assets/Scripts/GameManager.cs#L1256))이 남아 정상 발생한다. 세 엔딩(Success/Bankruptcy/Overdose)이 전부 도달 가능하다.

### 3.3 ★ 아침 시작 씬이 바뀐다

[GameManager.cs:1040](../../Assets/Scripts/GameManager.cs#L1040)
```csharp
if (!hasStoryMorningEvent && !hasBossMorningEvent) ReturnToYomiRoomForNewMorning();
```
`hasBossMorningEvent`가 항상 false이므로 **3·9·12·15·18일차가 이제 요미의 방에서 아침을 시작한다** (기존에는 보스 등장 연출 때문에 GameScene에 머물렀다). 이제 GameScene에 머무는 날은 **6·16일차뿐**이다.

- 요미의 방 정산 계획과는 **오히려 정합적**이다 — 방에서 시작하는 날이 늘어 그 계획의 적용 범위가 커진다.
- 20일차는 `CurrentDay == StoryLastDay`에서 엔딩 처리 후 `return`하므로 방으로 돌아가지 않는다 ✅

### 3.4 ★ 16일차 독백이 일어나지 않은 일을 말한다

[GameManager.cs:491](../../Assets/Scripts/GameManager.cs#L491) `day16Monologue`:
> "말도 안 돼... **10만 달러 배상 청구 폭탄**이라니!! 잔고가 박살나고 멘탈이 부서질 것 같지만..."

**하드코딩된 문자열이라 스위치가 닿지 않는다.** 차감은 없는데 독백은 그대로 나온다. 5절 미결 #1.

### 3.5 5·11일차 만화 컷 불일치 (코스메틱)

위약금 장면을 그린 만화 컷(`comicPanels`)은 그대로 재생되는데 돈은 나가지 않는다. 요미의 위약금 **대사**는 `isPenalty` 게이트에 걸려 사라지므로, 남는 것은 그림뿐이다. 5절 미결 #2.

### 3.6 난이도 급락

제거된 압박: 보스 자산 경쟁 7회 + 위약금 총 **$140,000**(5일 1만 / 11일 3만 / 16일 10만). 정기 지출($12,000~$150,000)만 남는다. 임시 상태로서는 의도한 바이나, 이 상태의 플레이 감각을 밸런스 기준으로 삼지 말 것.

### 3.7 구버전 세이브

보스날 도중에 저장된 세이브를 열면 보스 HUD와 보스 자산이 사라진 채 그날이 진행된다. `SavedBossStartingAsset`/`SavedBossCurrentAsset`은 세이브에 남아 있다가 되살릴 때 다시 쓰인다 — **데이터 손실 없음** ✅

### 3.8 P2P 무관 ✅

`P2PGameplayUIController.AlignLeaderboardLikeBossHud`는 이름만 boss인 **레이아웃 정렬 헬퍼**다. `BossManager`를 참조하지 않으며 P2P는 보스 시스템을 쓰지 않는다.

---

## 4. 안전장치가 살아 있는지

### 4.1 `AdvanceDate()`의 날짜 점프 클램프 — **수정 완료**

[Calendar_DateTime_System_Plan.md](Calendar_DateTime_System_Plan.md) G-2로 넣은 클램프가 `HasBossToday`를 보고 있었다. 스위치를 끄자 **보스 일차를 그냥 넘어가게 됐다.**

이것은 "기능이 꺼졌으니 그 날은 비었다"는 **잘못된 추론**이다. 지금 건너뛴 날은 보스를 되살려도 돌아오지 않는다 — 이미 그 일차를 지나쳐 저장된 세이브가 남기 때문이다. 그리고 하필 `AdvanceDate`가 첫 호출부를 얻는 시점이 **스토리 개편 작업**, 즉 보스가 꺼져 있는 바로 이 기간이다.

**해결: 판정 축을 둘로 나눴다.**

| 메서드 | 답하는 질문 | 스위치 영향 | 쓰는 쪽 |
| --- | --- | --- | --- |
| `HasBossToday(day)` | 오늘 보스가 **나오는가** | 받음 (꺼지면 false) | 조우·스폰·승패 판정 |
| **`IsBossScheduledDay(day)`** (신규) | 그 날에 보스가 **편성돼 있는가** | **받지 않음** | 날짜 점프 클램프 등 **일정 보호** |

```csharp
// GameManager.AdvanceDate
bool reserved = day == StoryLastDay
    || (bossManager != null && bossManager.IsBossScheduledDay(day))   // ← 스위치와 무관
    || storyEvents.Exists(e => e.triggerDay == day);
```
**기능을 실행하는 쪽과 일정을 보호하는 쪽은 다른 질문을 한다.** 스위치로 죽여야 하는 것은 앞쪽뿐이다. 앞으로 보스 일정을 참조하는 코드를 추가할 때 이 구분을 따를 것.

> `storyEvents.Exists(...)`는 그대로 둬도 안전하다. 위약금 비활성화는 이벤트를 **삭제하지 않고 금액만 0으로** 만들기 때문에 일정 자체는 남아 있다. 만약 나중에 이벤트를 리스트에서 제거하는 방식으로 바꾼다면 이 클램프가 같은 방식으로 조용히 약해진다.

### 4.2 그대로 살아 있는 안전장치 ✅

| 안전장치 | 상태 |
| --- | --- |
| 24:00 파산 예측 → 자동 저장 생략 | 정기 지출은 계속 계산하므로 정상 동작 |
| 정산 중 파산 판정 유예 (`isSettlementProcessing`) | 무관, 정상 |
| `CheckEnding` 파산 판정 | 무관, 정상 |
| 오버도즈 중 저장 금지 | 무관, 정상 |
| 달력 개편분(G-1·G-3·G-4) | 무관, 정상 |

### 4.3 요미의 방 정산 계획의 #3이 해소된다

그 계획서 12절 #3은 *"11일차 위약금 $30,000 + 지출 $20,000으로 파산하면 방에 게임오버 UI가 필요하다"* 였다. 위약금이 꺼지면 **11일차 파산 압력이 $50,000 → $20,000으로 낮아진다.**

다만 **완전히 사라지지는 않는다** — 정기 지출만으로도 잔고가 부족하면 파산한다(7일차 $12,000, 11일차 $20,000, 그리고 잔고 부족 자체). 따라서 *"위약금을 껐으니 방에서 파산 엔딩이 안 난다"* 고 결론지으면 **틀린다.** 판단 근거가 "파산 확률이 낮아졌다"로 바뀔 뿐이므로, #3은 여전히 결정이 필요하다.

---

## 5. 조치 방안

### 5.1 16일차 독백 (3.4) — 첫 줄 교체 **적용 완료**

독백 3줄이 하는 일은 **충격 → 100배 레버리지 필요성 → 남은 5일**이다. 금액을 명시하는 것은 **첫 줄뿐**이고 2·3줄은 위약금과 무관하게 성립하므로, 줄 하나만 갈아끼워 스토리 기능을 온전히 유지했다.

```
변경 전: "말도 안 돼... 10만 달러 배상 청구 폭탄이라니!! 잔고가 박살나고 멘탈이 부서질 것 같지만, 여기서 포기할 순 없어."
변경 후: "슬슬 한계야... 이 속도로는 사채업자 마감일까지 절대 못 맞춰!! 멘탈이 부서질 것 같지만, 여기서 포기할 순 없어."
```
`DisableStoryPenaltiesIfNeeded()`에서 `day16Monologue[0]`만 교체한다. 위약금을 되살리면 원문이 그대로 돌아온다.

> ⚠️ **"남은 시간은 단 5일"(3번째 줄)은 20일 제한을 전제한 문구다.** 제한을 없앤 지금은 이 줄도 엄밀히는 사실이 아니다. 다만 16일차 시점의 **긴박감 연출**로 읽히고 숫자가 게임 상태와 직접 대조되지 않아 이번에는 두었다. 개편 때 함께 정리 대상 (5.5 #2).

**근본 해결**: 독백이 하드코딩돼 스위치가 닿지 않는 것이 원인이다. [Settlement_In_YomiRoom_Plan.md](Settlement_In_YomiRoom_Plan.md) 5.1-B의 **`StoryDatabase` ScriptableObject 추출 때 독백도 함께 옮기면** 이 분기는 삭제된다.

### 5.2 5·11일차 만화 컷 (3.5) — 비활성 **적용 완료**

정산 시점에 재생되는 위약금 만화(5일차 2장·11일차 2장)를 런타임에서 비웠다. 돈을 빼앗기는 장면인데 차감이 없으면 앞뒤가 맞지 않기 때문이다.

```csharp
// 위약금 이벤트 중 '정산 시점 재생' 건만 비웁니다.
if (evt.triggerDay != 6 && evt.triggerDay != 16 && evt.comicPanels?.Count > 0)
    evt.comicPanels.Clear();
```
**6·16일차는 제외한다** — 그 둘의 만화는 아침 스토리 컷씬이라 위약금과 별개이며, 기존 코드도 정산 컷씬 조건에서 두 날을 이미 제외하고 있다 ([GameManager.cs:753](../../Assets/Scripts/GameManager.cs#L753)). 1일차 만화 4장은 위약금 이벤트가 아니라 그대로 재생된다.

씬 에셋은 건드리지 않으므로 스위치를 되돌리면 원본이 복귀한다.

### 5.3 20일차 제한 (3.1) — 제거 **적용 완료**

`StoryDayLimitEnabled = false`. 승리 조건 수식을 손보는 대신 **판정 자체를 제거**했다. 20일 제한을 없애기로 한 이상 "20일차에 승패를 가른다"가 성립하지 않고, 임시 승리 조건을 만들면 개편 때 버려질 상수가 하나 더 늘 뿐이다.

날짜 점프 클램프의 `StoryLastDay` 항목도 같은 스위치로 함께 꺼진다 — 강제 종료가 없으면 그 날을 보호할 이유도 없기 때문이다. **다만 20일차는 보스 편성일이기도 하므로 `IsBossScheduledDay`가 계속 보호한다** (4.1). 안전장치가 이중으로 걸려 있어 제한 제거로 구멍이 생기지 않는다.

결과는 3.1에 정리했다 — **성공 엔딩이 닫히고 무한 진행이 된다.**

### 5.4 정기 지출 — 유지

요청 범위는 위약금까지였다. 정기 지출(3·7·11·15·18일차, $12,000~$150,000)까지 끄면 **돈이 나갈 일이 완전히 사라져** 트레이딩 파트의 압박 구조가 통째로 없어진다. 그러면 5.3의 "바는 낮지만 자산도 낮다"는 균형도 무너진다.

끄고 싶어지면 `CalculateExpectedDeduction`이 **순수 함수 한 곳**이라 그 입구에서 0을 돌려주면 끝난다 — 필요해질 때 3줄이다. 지금 만들어 둘 이유가 없다.

---

## 5.5 남은 결정 (전부 스토리 개편으로 이월)

| # | 항목 | 내용 |
| --- | --- | --- |
| 1 | **새 종료 조건** | 성공 엔딩이 닫힌 상태다(3.1). 무한 진행을 어디서·무엇으로 끝낼지 정의해야 한다 |
| 2 | 16일차 독백 3번째 줄 "남은 시간은 단 5일" | 20일 제한을 전제한 문구다 (5.1) |
| 3 | 25일차 난이도 상한 | 무한 진행이면 그 이후 난이도가 평평하다 (3.1) |

---

## 6. 모드별 적용 범위 (2026-08-15 보강)

처음에는 스위치를 전역으로 두어 **엔드리스·챌린지까지 함께 꺼졌다.** 그 모드들은 보스전과 종료 조건이 곧 존재 이유이므로 잘못된 적용이었다. 판정을 모드 인지형으로 좁혔다.

### 6.1 관문에 모드 조건을 더했다

```csharp
// BossManager
private static bool BossesActive =>
    BossesEnabled || (SaveLoadManager.Instance != null &&
                      SaveLoadManager.Instance.CurrentGameMode != GameMode.Story);

// GameManager — 20일차 강제 종료
private static bool DayLimitActive =>
    StoryDayLimitEnabled || (SaveLoadManager.Instance != null &&
                             SaveLoadManager.Instance.CurrentGameMode != GameMode.Story);
```
`HasBossToday`·`GetBossData`·`SpawnBossForDay`와 20일차 판정·날짜 점프 클램프가 모두 이 값을 본다.

> **세이브 매니저가 없으면 스토리로 간주해 꺼 둔다.** 테스트 씬처럼 모드를 알 수 없는 환경에서 보스가 되살아나면, 개편 기간에 꺼 두기로 한 의도가 조용히 뒤집힌다. 모르면 끄는 쪽이 안전하다.

### 6.2 모드별 현재 동작

| | 스토리 | 엔드리스 · 챌린지 | P2P |
| --- | --- | --- | --- |
| 보스 | **꺼짐** | 종전대로 등장 | 애초에 `BossManager`를 쓰지 않음 |
| 20일차 종료 | **꺼짐 (무한 진행)** | 종전대로 20일차 종료 | 하루짜리 경기라 무관 |
| 위약금 | **꺼짐** | 원래 스토리 전용 (6.3) | 무관 |
| 성공 엔딩 | **도달 불가** | 종전대로 도달 가능 | 무관 |
| 아침 시작 위치 | 요미의 방 (6·16일차 제외) | **GameScene 유지** (6.5) | 무관 |

### 6.3 위약금은 원래부터 스토리 전용이었다

`isPenalty`/`penaltyAmount`를 읽는 5곳이 **전부 `CurrentGameMode == GameMode.Story` 블록 안에 있다** ([GameManager.cs:844](../../Assets/Scripts/GameManager.cs#L844)·[:913](../../Assets/Scripts/GameManager.cs#L913)·[:966](../../Assets/Scripts/GameManager.cs#L966)·[:992](../../Assets/Scripts/GameManager.cs#L992)·[:1157](../../Assets/Scripts/GameManager.cs#L1157)). 따라서 1.2의 데이터 0 처리는 다른 모드에 아무 영향이 없어 모드 조건을 더할 필요가 없었다.

> 오히려 **모드 조건을 붙이면 안 된다.** `DisableStoryPenaltiesIfNeeded`는 `Awake`에서 한 번만 도는데 `GameManager`가 이제 상주하므로, 「엔드리스로 시작 → 타이틀 → 스토리」 순서에서 조건을 걸면 스토리에서 위약금이 되살아난다. 무조건 0으로 만드는 편이 오히려 견고하다.

### 6.4 함께 고친 교차 오염 하나

`ChoiceEventController.DailyEventCap()`이 잔여 슬롯으로 이벤트 한도를 정하는데, `DatingTimeManager`가 `DontDestroyOnLoad`라 **스토리 세션의 잔여 슬롯이 엔드리스 세션까지 따라와** 한도를 조용히 깎을 수 있었다. 모드를 먼저 보고 스토리가 아니면 종전값 2를 돌려주게 했다.

### 6.5 아침 복귀도 스토리 전용으로 — **수정 완료**

`ReturnToYomiRoomForNewMorning()`에는 모드 조건이 없어 **엔드리스·챌린지도 아침마다 요미의 방으로 보내지고 있었다.** 그 모드들은 데이팅 파트도 세이브도 쓰지 않으므로 방에는 할 일이 없다.

**이번 변경으로 생긴 문제가 아니라 그 전부터 있던 것이다** — 보스가 살아 있던 시절에도 보스 없는 날은 방으로 갔다. 다만 보스를 껐던 동안에는 *모든* 날이 방으로 가서 증상이 커졌다.

```csharp
private void ReturnToYomiRoomForNewMorning()
{
    var saveManager = SaveLoadManager.Instance;
    if (saveManager == null || saveManager.CurrentGameMode != GameMode.Story)
    {
        currentState = GameState.Playing;   // 거래 화면에서 그대로 다음 날
        return;
    }
    ...
}
```

**판정을 호출부가 아니라 메서드 안에 둔 이유**: 아침 복귀 경로가 나중에 늘어나도 모드 조건이 한 곳에 남는다. 호출부에 조건을 흩으면 새 경로가 그것을 빠뜨린다.

**다른 방 진입 경로는 전부 이미 스토리 전용이었다** — 새 게임 시작([MainMenuController.cs:706](../../Assets/Scripts/UI/MainMenuController.cs#L706)), 튜토리얼 종료([TutorialManager.cs:1211](../../Assets/Scripts/System/TutorialManager.cs#L1211)), 월드맵 복귀([WorldMapManager.cs:169](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L169)). 엔드리스·챌린지 진입은 `StartNewMode` → `LoadGameFlow()`로 `GameScene`이 기본값이다. 새던 곳은 아침 복귀 한 곳뿐이었다.

---

## 7. 되살릴 때 체크리스트

```csharp
BossManager.BossesEnabled = true;
GameManager.StoryPenaltiesEnabled = true;
GameManager.StoryDayLimitEnabled = true;   // 새 종료 조건이 정해졌다면 이 스위치 대신 그쪽을 쓸 것
```
세 줄이 전부다. 그 뒤 확인할 것:

- [ ] 3·6·9·12·15·18·20일차에 보스가 등장하고 HUD가 표시되는지
- [ ] 최종 보스 격파 → Success 엔딩 (백업 판정이 아니라 보스 경로로)
- [ ] 최종 보스 패배 → Overdose 엔딩
- [ ] 20일차에 게임이 종료되는지 (제한 복귀)
- [ ] 5·11·16일차 위약금 차감 + 요미 대사 + 정산 UI 표시
- [ ] **5·11일차 정산 만화가 다시 재생되는지** (5.2)
- [ ] **16일차 독백 첫 줄이 "10만 달러 배상 청구 폭탄" 원문으로 복귀하는지** (5.1)
- [ ] 보스날 아침 시작 씬이 GameScene으로 복귀하는지 (3.3의 역방향)
- [ ] `AdvanceDate` 클램프는 **켜든 끄든 보스 일차를 막는다** (4.1) — 켠 뒤에도 동작이 바뀌지 않아야 정상
- [ ] 비활성 기간에 만들어진 세이브를 로드해도 보스 자산이 정상 초기화되는지
- [ ] `dotnet build` 4종 — 특히 `CS0162` 경고가 생기지 않는지 (1.1)
