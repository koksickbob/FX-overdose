using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using FXOverdose.AI;
using FXOverdose.Trading;
using System.Collections.Generic;

namespace FXOverdose.Core
{
    public class SaveLoadManager : MonoBehaviour
    {
        public const int MaxStorySlots = 20;
        public static SaveLoadManager Instance { get; private set; }

        public SaveData CurrentData { get; private set; }

        /// <summary>현재 타이틀에서 선택해 실행 중인 게임 모드입니다.</summary>
        public GameMode CurrentGameMode { get; private set; } = GameMode.Story;
        public StoryDifficulty CurrentStoryDifficulty { get; private set; } = StoryDifficulty.Hard;
        public bool IsTutorialCompleted { get; set; } = false;

        /// <summary>스토리 모드에서 현재 사용 중인 저장 슬롯입니다.</summary>
        public int ActiveStorySlotIndex { get; private set; }

        public bool AllowsAITrading => CurrentGameMode != GameMode.Challenge;
        public bool AllowsSaving => CurrentGameMode == GameMode.Story;

        // 로드 진행 후 GameScene 진입 시 상태를 복원해야 하는지 여부 플래그
        public bool IsPendingLoad { get; private set; }

        /// <summary>
        /// 재접속 시 복귀를 허용하는 씬입니다. 복귀 가능한 씬이 늘어나면 이 배열에만 추가하십시오.
        ///
        /// 저장하는 쪽(SaveGame)과 불러오는 쪽(MainMenuController)이 같은 판정을 공유해야 하므로
        /// 여기 한 곳에 모읍니다. 목록에 없는 씬(tutorial·LoadingScene 등)에서 저장하면
        /// 기록을 갱신하지 않고 이전 값을 유지합니다 — 그러지 않으면 튜토리얼 중 저장한 세이브가
        /// 재접속 때 튜토리얼을 다시 재생합니다.
        /// </summary>
        public static readonly string[] ResumableScenes = { "GameScene", "YomiRoomScene", "WorldMapScene" };

        public static bool IsResumableScene(string sceneName)
            => !string.IsNullOrEmpty(sceneName) && Array.IndexOf(ResumableScenes, sceneName) >= 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private string GetSaveFilePath(int slotIndex)
        {
            return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        }

        public bool HasSave(int slotIndex)
        {
            return File.Exists(GetSaveFilePath(slotIndex));
        }

        /// <summary>
        /// GameScene 밖(요미의 방·월드맵·편의점)에서 아이템을 지급합니다.
        /// 그 씬들에는 Inventory 인스턴스가 없어 AddItem을 부를 대상이 없으므로 세이브 스냅샷에 직접 누적합니다.
        /// 다음 GameScene 진입 때 ApplyLoadedDataToGame이 이 목록으로 인벤토리를 재구성합니다.
        ///
        /// ⚠️ 이 메서드는 디스크에 쓰지 않습니다. 호출부가 SaveCurrentGame()으로 확정해야 합니다.
        /// ⚠️ itemId가 ShopManager 카탈로그에 없으면 복원 단계에서 조용히 버려집니다.
        /// </summary>
        public bool GrantItemToSave(string itemId, int amount = 1)
        {
            if (CurrentData == null || string.IsNullOrEmpty(itemId) || amount <= 0) return false;

            int index = CurrentData.InventoryItemIds.IndexOf(itemId);
            if (index >= 0 && index < CurrentData.InventoryItemQuantities.Count)
            {
                // 같은 ID로 행을 하나 더 만들면 복원 루프가 두 번 AddItem 하거나 한쪽을 잃습니다. 반드시 합산합니다.
                CurrentData.InventoryItemQuantities[index] += amount;
                return true;
            }

            CurrentData.InventoryItemIds.Add(itemId);
            CurrentData.InventoryItemQuantities.Add(amount);
            return true;
        }

        public bool SaveGame(int slotIndex, string saveName = null)
        {
            if (!AllowsSaving)
            {
                Debug.LogWarning($"[SaveLoadManager] {CurrentGameMode} 모드는 저장을 지원하지 않습니다. 스토리 모드에서만 저장할 수 있습니다.");
                return false;
            }

            slotIndex = Mathf.Clamp(slotIndex, 0, MaxStorySlots - 1);
            ActiveStorySlotIndex = slotIndex;

            var gm = GameManager.Instance;
            var status = TraderStatus.CanonicalInstance;
            var levelSys = TraderLevelSystem.Instance;
            var memory = TraderMemoryManager.Instance;
            var trading = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            var marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            var costumes = CostumeManager.Instance;
            var activeItems = ActiveItemEffectManager.Instance;
            var deliveryFood = DeliveryFoodManager.EnsureInstance();
            var inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);
            var dating = FXOverdose.DatingSim.Core.DatingTimeManager.Instance;

            // 저장 금지 상태는 판정할 매니저가 실제로 있을 때만 검사합니다.
            // (요미의 방/월드맵에는 GameManager도 TraderStatus도 없습니다)
            if (gm != null &&
                gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.Paused)
            {
                Debug.LogWarning($"[SaveLoadManager] 현재 상태({gm.CurrentState})에서는 저장할 수 없습니다.");
                return false;
            }

            if ((status != null && status.CurrentMentalState == TraderStatus.MentalState.Overdose) ||
                (trading != null && trading.IsOverdoseTradeActive))
            {
                Debug.LogWarning("[SaveLoadManager] 오버도즈 상태 또는 기믹 발동 중에는 저장할 수 없습니다.");
                return false;
            }

            // 💡 [부분 저장] 직전 저장/로드본을 베이스로 삼습니다.
            //    씬에 없는 매니저의 필드를 기본값으로 덮어써 날려버리는 것을 막습니다. (SV-A6)
            //    베이스가 없으면 디스크의 기존 세이브를 읽어 옵니다. 그것도 없어야 새 데이터입니다.
            // 이름을 지정한 수동 저장은 대상 슬롯의 오래된 데이터를 베이스로 삼지 않습니다.
            // 현재 플레이 스냅샷으로 새 데이터를 만든 뒤 원자적으로 파일을 교체합니다.
            SaveData data = CurrentData ??
                            (!string.IsNullOrWhiteSpace(saveName) ? new SaveData() : ReadSaveFile(slotIndex)) ??
                            new SaveData();

            if (!string.IsNullOrWhiteSpace(saveName))
                data.SaveName = saveName.Trim();

            data.Version = Application.version;
            data.GameMode = CurrentGameMode;
            data.StoryDifficulty = CurrentStoryDifficulty;
            data.IsTutorialCompleted = this.IsTutorialCompleted;

            // 재접속 복귀 지점. 복귀 가능한 씬에서만 갱신하고, 그 외에는 이전 값을 유지합니다.
            string activeScene = SceneManager.GetActiveScene().name;
            if (IsResumableScene(activeScene))
                data.LastSceneName = activeScene;

            if (gm != null)
            {
                data.Balance = gm.CurrentBalance;
                data.CurrentDate = GameCalendar.ToSerialized(gm.CurrentDate);
                data.StartDate = GameCalendar.ToSerialized(gm.StartDate);
                data.CurrentDay = gm.CurrentDay;
                data.CurrentHour = gm.CurrentHour;
                data.CurrentMinute = gm.CurrentMinute;
                data.SecondsPerGameMinute = DynamicTimeRegulator.Instance != null
                    ? DynamicTimeRegulator.Instance.BaseSecondsPerMinute
                    : gm.SecondsPerGameMinute;
                data.StartOfDayEquity = gm.StartOfDayEquity;
            }

            status?.CaptureSaveData(data);

            if (levelSys != null)
            {
                data.ProtagonistLevel = levelSys.ProtagonistLevel;
                data.ProtagonistEXP = levelSys.ProtagonistEXP;
                data.ChartStudyLevel = levelSys.ChartStudyLevel;
                data.CubePatienceLevel = levelSys.CubePatienceLevel;
                data.BookJudgmentLevel = levelSys.BookJudgmentLevel;
            }

            if (costumes != null)
            {
                data.OwnedCostumeIds = costumes.GetOwnedCostumeIds();
                data.EquippedCostumeId = costumes.EquippedCostumeId;
            }

            trading?.CaptureSaveData(data);
            gm?.CaptureSettlementContext(data);
            DailyMarketOutlook.Capture(data);
            FindAnyObjectByType<FXOverdose.Events.ChoiceEventController>(FindObjectsInactive.Include)?.CaptureSaveData(data);
            marketEngine?.CaptureSaveData(data);
            activeItems?.CaptureSaveData(data.ActiveItemIds, data.ActiveItemLevels);

            if (inventory != null)
            {
                // 베이스를 재사용하므로 목록을 비우지 않으면 저장할 때마다 누적됩니다.
                data.InventoryItemIds.Clear();
                data.InventoryItemQuantities.Clear();
                foreach (InventorySlot slot in inventory.Slots)
                {
                    if (slot?.Item == null || slot.Quantity <= 0) continue;
                    data.InventoryItemIds.Add(slot.Item.ItemId);
                    data.InventoryItemQuantities.Add(slot.Quantity);
                }
            }

            if (deliveryFood != null)
            {
                data.LastSteakPurchaseDay = deliveryFood.LastSteakPurchaseDay;
                data.PastaBuffRemainingSeconds = deliveryFood.PastaRemainingSeconds;
            }

            var bossManager = FXOverdose.Core.BossManager.Instance;
            if (bossManager != null && bossManager.CurrentBoss != null)
            {
                data.SavedBossStartingAsset = bossManager.BossStartingAsset;
                data.SavedBossCurrentAsset = bossManager.BossCurrentAsset;
            }

            // DatingSim 상태 저장 (요미의 방/월드맵에서는 이쪽만 갱신됩니다)
            dating?.SaveToData(data);

            // MemoryManager
            // private 필드들에 접근하기 위해 Reflection을 사용할 수도 있지만,
            // SaveLoadManager에서 직접 데이터를 얻거나 GameManager처럼 public Getter가 있으면 좋음.
            // 임시로 Reflection으로 추출. 추후 TraderMemoryManager에 GetData() 메서드를 추가하는 것이 좋음.
            if (memory != null)
            {
                ExtractMemoryData(memory, data);
            }

            if (!WriteSaveFile(slotIndex, data))
            {
                return false;
            }

            // 다음 부분 저장의 베이스가 되도록 최신 스냅샷을 들고 있습니다. (SV-A7)
            CurrentData = data;
            Debug.Log($"[SaveLoadManager] 슬롯 {slotIndex}에 게임 저장 완료:\n{GetSaveFilePath(slotIndex)}");
            return true;
        }

        /// <summary>세이브 파일을 읽어 역직렬화합니다. 없거나 깨졌으면 null.</summary>
        private SaveData ReadSaveFile(int slotIndex)
        {
            string path = GetSaveFilePath(slotIndex);
            if (!File.Exists(path)) return null;

            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveLoadManager] 기존 세이브를 베이스로 읽지 못했습니다: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// 임시 파일에 먼저 쓰고 교체하는 원자적 저장입니다.
        /// 쓰는 도중 게임이 죽어도 기존 세이브 파일이 살아남습니다.
        /// </summary>
        private bool WriteSaveFile(int slotIndex, SaveData data)
        {
            string path = GetSaveFilePath(slotIndex);
            string tempPath = path + ".tmp";

            try
            {
                File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));

                if (File.Exists(path))
                {
                    // 교체 실패 시 원본이 남도록 백업본을 남깁니다.
                    File.Replace(tempPath, path, path + ".bak", true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveLoadManager] 슬롯 {slotIndex} 저장 실패: {exception.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                return false;
            }
        }

        /// <summary>스토리 시작/불러오기에서 선택한 슬롯에 저장합니다.</summary>
        public bool SaveCurrentGame()
        {
            return SaveGame(ActiveStorySlotIndex);
        }

        public string GetSaveName(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxStorySlots) return string.Empty;
            SaveData data = ReadSaveFile(slotIndex);
            return data == null ? string.Empty : data.SaveName?.Trim() ?? string.Empty;
        }

        public StoryDifficulty GetSaveDifficulty(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxStorySlots) return StoryDifficulty.Hard;
            return ReadSaveFile(slotIndex)?.StoryDifficulty ?? StoryDifficulty.Hard;
        }

        private void ExtractMemoryData(TraderMemoryManager memory, SaveData data)
        {
            try
            {
                var type = typeof(TraderMemoryManager);
                
                // 단기 대사
                var shortTermField = type.GetField("shortTermDialogues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shortTermField != null)
                {
                    data.ShortTermDialogues = new List<string>((List<string>)shortTermField.GetValue(memory));
                }

                // 장기 기억
                var longTermField = type.GetField("longTermMemories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (longTermField != null)
                {
                    data.LongTermMemories = new List<MemoryEntry>((List<MemoryEntry>)longTermField.GetValue(memory));
                }

                // 일일 요약본
                var dailyField = type.GetField("dailySummaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dailyField != null)
                {
                    var dict = (Dictionary<int, string>)dailyField.GetValue(memory);
                    data.DailySummaryKeys.Clear();
                    data.DailySummaryValues.Clear();
                    foreach (var kvp in dict)
                    {
                        data.DailySummaryKeys.Add(kvp.Key);
                        data.DailySummaryValues.Add(kvp.Value);
                    }
                }
            }
            catch(Exception e)
            {
                Debug.LogError($"[SaveLoadManager] Memory 추출 오류: {e.Message}");
            }
        }

        public bool PrepareLoadGame(int slotIndex)
        {
            slotIndex = Mathf.Clamp(slotIndex, 0, MaxStorySlots - 1);
            string path = GetSaveFilePath(slotIndex);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveLoadManager] 슬롯 {slotIndex}에 세이브 파일이 존재하지 않습니다.");
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                CurrentData = JsonUtility.FromJson<SaveData>(json);
                if (CurrentData == null)
                {
                    Debug.LogError($"[SaveLoadManager] 슬롯 {slotIndex}의 저장 데이터를 읽지 못했습니다.");
                    IsPendingLoad = false;
                    return false;
                }

                // --- 데이터 마이그레이션 적용 ---
                SaveData dataToMigrate = CurrentData;
                bool wasMigrated = SaveDataMigrator.Migrate(ref dataToMigrate);
                CurrentData = dataToMigrate;

                if (wasMigrated)
                {
                    // 기존 파일을 .bak으로 백업 후 최신 포맷으로 자동 저장
                    try
                    {
                        string backupPath = path + ".bak";
                        File.Copy(path, backupPath, true);
                        Debug.Log($"[SaveLoadManager] 구버전 세이브를 백업했습니다: {backupPath}");
                        
                        string migratedJson = JsonUtility.ToJson(CurrentData, true);
                        File.WriteAllText(path, migratedJson);
                        Debug.Log($"[SaveLoadManager] 마이그레이션 된 세이브를 자동 저장했습니다: {path}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SaveLoadManager] 마이그레이션 백업/저장 중 오류 발생: {e.Message}");
                    }
                }
            }
            catch (Exception exception)
            {
                CurrentData = null;
                IsPendingLoad = false;
                Debug.LogError($"[SaveLoadManager] 슬롯 {slotIndex} 로드 실패: {exception.Message}");
                return false;
            }

            // 현재 저장 슬롯은 스토리 전용입니다. GameMode 필드가 없던 기존 세이브도 Story(0)입니다.
            CurrentData.GameMode = GameMode.Story;
            CurrentGameMode = GameMode.Story;
            CurrentStoryDifficulty = CurrentData.StoryDifficulty;
            ActiveStorySlotIndex = slotIndex;
            IsPendingLoad = true;

            // 아래 둘은 GameScene 진입을 기다리지 않고 지금 복원합니다.
            // 요미의 방/월드맵으로 바로 복귀하면 ApplyLoadedDataToGame이 돌지 않는데,
            // 그 씬들도 이 값을 읽고 또 저장까지 하기 때문입니다.
            FXOverdose.DatingSim.Core.DatingTimeManager.Instance?.LoadFromSaveData(CurrentData);

            // 방에서 저장이 일어나면 이 값이 그대로 디스크에 다시 쓰입니다.
            // 복원해 두지 않으면 완료된 튜토리얼이 false로 덮여 다시 재생됩니다.
            this.IsTutorialCompleted = CurrentData.IsTutorialCompleted;
            // 과거 세이브 보정: 플래그가 없더라도 이미 2일차 이상이면 완료된 것으로 간주합니다.
            if (!this.IsTutorialCompleted && CurrentData.CurrentDay > 1)
            {
                this.IsTutorialCompleted = true;
                CurrentData.IsTutorialCompleted = true;
            }

            Debug.Log($"[SaveLoadManager] 스토리 슬롯 {slotIndex + 1} 데이터 로드 대기 중 (복귀 씬: {(string.IsNullOrEmpty(CurrentData.LastSceneName) ? "GameScene" : CurrentData.LastSceneName)})");
            return true;
        }

        public void PrepareNewGame()
        {
            PrepareNewGame(GameMode.Story, 0);
        }

        public void PrepareNewGame(GameMode mode, int storySlotIndex = 0, StoryDifficulty difficulty = StoryDifficulty.Hard)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode))
                mode = GameMode.Story;

            CurrentGameMode = mode;
            CurrentStoryDifficulty = mode == GameMode.Story ? difficulty : StoryDifficulty.Hard;
            ActiveStorySlotIndex = mode == GameMode.Story
                ? Mathf.Clamp(storySlotIndex, 0, MaxStorySlots - 1)
                : 0;

            // ⚠️ null이 아니라 <b>빈 SaveData</b>를 넣습니다. (S-1)
            //    null로 두면 새 게임의 첫 저장에서 SaveGame()의 `CurrentData ?? ReadSaveFile(slot)` 이
            //    <b>이전 판의 슬롯 파일을 베이스로 집습니다.</b> 씬에 있는 매니저만 자기 필드를 덮어쓰므로,
            //    주인 없는 필드(요미 대화 이력 Talk* 전체 등)가 새 게임에 통째로 상속됐습니다.
            //    실제 증상: 새 게임 1일차에 TalkPeakAffection이 남아 3단계 토픽이 열려 있었습니다.
            //    파일을 지우지 않는 이유는, 플레이어가 새 게임을 시작만 하고 그만둘 수 있기 때문입니다.
            //    기존 세이브는 이 슬롯에 처음 저장하는 순간 정상적으로 덮어써집니다.
            CurrentData = new SaveData();

            // 새 게임 초기화를 여기서 끝냅니다. GameManager는 GameScene에만 있는데 스토리 모드는
            // 요미의 방에서 시작하므로, 초기 자금을 GameScene 진입까지 미루면 방에서 기본값이 보이고
            // 그 사이 벌어들인 알바 수익이 나중에 StartNewGame()에 덮여 사라집니다.
            // StoryDifficultyTables는 순수 static이라 씬 의존이 없습니다.
            // 시각·레벨·기억·코스튬은 SaveData 기본값이 곧 초기 상태라 따로 심지 않습니다.

            // ⚠️ 날짜만은 여기서 반드시 심어야 합니다. SaveData의 날짜 기본값은 "구버전 세이브"를
            //    가려내는 빈 문자열이라(SaveData 주석 참고), 그대로 두면 새 게임의 첫 저장이 빈 날짜로
            //    기록됩니다. 스토리 모드는 GameManager가 없는 요미의 방에서 시작해 거래 개시 전에
            //    저장하므로 수집 경로(`if (gm != null)`)가 채워주지 못하고, 버전 태그는 이미 최신이라
            //    마이그레이터도 돌지 않습니다.
            CurrentData.StartDate = GameCalendar.DefaultStartDateText;
            CurrentData.CurrentDate = GameCalendar.DefaultStartDateText;

            if (CurrentGameMode == GameMode.Story)
            {
                float initialBalance = StoryDifficultyTables.Get(CurrentStoryDifficulty).StartingBalance;
                CurrentData.Balance = initialBalance;
                CurrentData.StartOfDayEquity = initialBalance;
            }
            // 시작 아이템만은 씬의 ItemData 에셋이 출처라 데이터로 심을 수 없습니다.
            // 첫 GameScene 진입 때 복원 경로가 대신 지급합니다. (ApplyLoadedDataToGame)
            CurrentData.NeedsStartingItems = true;

            // 이 프로퍼티는 DontDestroyOnLoad라 이전 판의 값이 남습니다. 내리지 않으면 한 세션에서
            // 게임을 끝낸 뒤 새로 시작할 때 TutorialManager가 튜토리얼을 완료된 것으로 보고 건너뜁니다.
            IsTutorialCompleted = false;

            IsPendingLoad = false;

            DeliveryFoodManager.ResetStateForNewGame();
            // static이라 이전 판의 방향성이 남습니다. 새 게임에서 반드시 비웁니다. (S8)
            DailyMarketOutlook.Reset();
            // DontDestroyOnLoad라 이전 판의 호감도·집착도·체력·일차가 매니저에 그대로 남습니다.
            // 위에서 만든 기본값 SaveData를 그대로 먹여 초기 상태로 되돌립니다. (S-2)
            FXOverdose.DatingSim.Core.DatingTimeManager.Instance?.LoadFromSaveData(CurrentData);
            Debug.Log($"[SaveLoadManager] 새 게임 준비: {CurrentGameMode}" +
                      (CurrentGameMode == GameMode.Story ? $" / Slot {ActiveStorySlotIndex + 1}" : string.Empty));
        }

        public void ApplyLoadedDataToGame()
        {
            if (!IsPendingLoad || CurrentData == null) return;

            var gm = GameManager.Instance;
            var status = TraderStatus.CanonicalInstance;
            var levelSys = TraderLevelSystem.Instance;
            var memory = TraderMemoryManager.Instance;
            var costumes = CostumeManager.Instance;
            var activeItems = ActiveItemEffectManager.Instance;
            var deliveryFood = DeliveryFoodManager.EnsureInstance();
            var inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);
            var shopManager = FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
            var trading = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            var marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);

            // IsTutorialCompleted는 PrepareLoadGame에서 이미 복원했습니다 (방으로 바로 복귀하는 경우 때문).

            if (gm != null)
            {
                // GameManager 필드 복구 (Reflection)
                var gmType = typeof(GameManager);
                gmType.GetField("currentBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.Balance);

                // 시계는 리플렉션이 아니라 전용 복원 메서드로 되돌립니다. 리플렉션은 필드명이 바뀌어도
                // 컴파일 에러 없이 조용히 실패하는데, 시간축이 통째로 초기값이 되는 사고는 눈에 잘 띄지 않습니다.
                // 이 메서드는 날짜가 비었거나 손상된 세이브도 일차 서수로 역산해 받아냅니다.
                gm.RestoreClock(CurrentData.StartDate, CurrentData.CurrentDate,
                                CurrentData.CurrentHour, CurrentData.CurrentMinute, CurrentData.CurrentDay);
                // 시간 배속(secondsPerGameMinute)은 일부러 복원하지 않습니다.
                // 이 값을 바꾸는 유일한 경로가 DynamicTimeRegulator의 슬로우 모션 lerp라
                // 세션 중 씬에 설정된 기준값에서 벗어나지 않습니다 — 플레이어 상태가 아니라 설계 상수입니다.
                //
                // 세이브에서 되돌리면 두 가지가 깨집니다.
                //  1) 기준 배속을 조정해도 기존 세이브가 옛 값에 영구히 고착됩니다.
                //  2) 방·월드맵처럼 GameManager가 없는 씬에서 저장하면 기록이 건너뛰어져
                //     SaveData 기본값(과거 3f = 하필 강제 청산 슬로우 모션 수치)이 주입돼
                //     게임이 3배 느린 연출 속도로 고정됐습니다.
                // 기준값의 출처는 씬 하나로 유지합니다.
                // 누적 수익률(P&L)의 분모입니다. 복원하지 않으면 인스펙터 기본값 7000이 그대로 쓰여
                // 초기 자금이 7000이 아닌 난이도의 수익률이 전부 틀리게 표시됩니다.
                if (CurrentGameMode == GameMode.Story)
                {
                    gmType.GetField("startingBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                          ?.SetValue(gm, StoryDifficultyTables.Get(CurrentStoryDifficulty).StartingBalance);
                }
                bool hasDailyBaseline = float.IsFinite(CurrentData.StartOfDayEquity) && CurrentData.StartOfDayEquity > 0f;
                gm.RestoreStartOfDayEquity(
                    hasDailyBaseline ? CurrentData.StartOfDayEquity : CurrentData.Balance,
                    !hasDailyBaseline);
                if (!hasDailyBaseline)
                {
                    Debug.LogWarning("[SaveLoadManager] 구버전 저장 데이터에 일일 기준 자산이 없어, 이번 날의 손익은 불러온 시점부터 계산합니다.");
                }
            }

            // 리플렉션 대신 TraderStatus 자신이 복원합니다. 중독·연패 카운터도 함께 되돌아옵니다. (SV-A1~A3)
            status?.RestoreFromSaveData(CurrentData);

            if (levelSys != null)
            {
                // LevelSystem 복구
                var lvType = typeof(TraderLevelSystem);
                lvType.GetField("protagonistLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(levelSys, CurrentData.ProtagonistLevel);
                lvType.GetField("protagonistEXP", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(levelSys, CurrentData.ProtagonistEXP);
                lvType.GetField("chartStudyLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(levelSys, CurrentData.ChartStudyLevel);
                lvType.GetField("cubePatienceLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(levelSys, CurrentData.CubePatienceLevel);
                lvType.GetField("bookJudgmentLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(levelSys, CurrentData.BookJudgmentLevel);
                
                // 이벤트 수동 트리거로 UI 등 반영 유도 (단, 초기화 단계이므로 필요시 추가 제어)
                // Reflection을 통해 OnProtagonistLevelChanged 등을 Invoke하는 것도 가능하나, 초기 렌더링에 의존
            }

            if (memory != null)
            {
                RestoreMemoryData(memory, CurrentData);
            }

            if (costumes != null)
            {
                costumes.Restore(CurrentData.OwnedCostumeIds, CurrentData.EquippedCostumeId);
            }

            if (activeItems != null)
            {
                activeItems.RestoreFromSaveData(
                    shopManager != null ? shopManager.CatalogItems : null,
                    CurrentData.ActiveItemIds,
                    CurrentData.ActiveItemLevels);
            }

            if (inventory != null && shopManager != null && CurrentData.NeedsStartingItems)
            {
                // 새 게임의 첫 GameScene 진입입니다. 저장된 목록(비어 있음) 대신 시작 지급분을 넣습니다.
                // 아이템 구성의 출처는 씬에 배치된 ItemData 슬롯이므로 Inventory가 직접 판정합니다.
                inventory.ResetForNewGame();
                CurrentData.NeedsStartingItems = false;
            }
            else if (inventory != null && shopManager != null)
            {
                inventory.Clear();
                int count = Mathf.Min(CurrentData.InventoryItemIds.Count, CurrentData.InventoryItemQuantities.Count);
                for (int i = 0; i < count; i++)
                {
                    ItemData savedItem = null;
                    foreach (ItemData catalogItem in shopManager.CatalogItems)
                    {
                        if (catalogItem != null && catalogItem.ItemId == CurrentData.InventoryItemIds[i])
                        {
                            savedItem = catalogItem;
                            break;
                        }
                    }
                    if (savedItem != null && CurrentData.InventoryItemQuantities[i] > 0)
                        inventory.AddItem(savedItem, CurrentData.InventoryItemQuantities[i]);
                }
            }

            if (trading != null)
            {
                trading.SetTradingMode(CurrentData.ActiveTradingMode, forceRestore: true);
                trading.SetAITradingStyle(CurrentData.AITradingStyle);
                // 포지션이 없어도 호출합니다. 이벤트 계약을 꺼진 상태로 확정해야 하기 때문입니다. (SV-A5)
                trading.RestorePosition(CurrentData);
            }

            gm?.RestoreSettlementContext(CurrentData);
            DailyMarketOutlook.Load(CurrentData);
            FindAnyObjectByType<FXOverdose.Events.ChoiceEventController>(FindObjectsInactive.Include)?.RestoreFromSaveData(CurrentData);
            marketEngine?.RestoreFromSaveData(CurrentData);
            deliveryFood.Restore(CurrentData.LastSteakPurchaseDay, CurrentData.PastaBuffRemainingSeconds);

            var bossManager = FXOverdose.Core.BossManager.Instance;
            if (bossManager != null)
            {
                bossManager.LoadedBossStartingAsset = CurrentData.SavedBossStartingAsset;
                bossManager.LoadedBossCurrentAsset = CurrentData.SavedBossCurrentAsset;
            }

            // DatingSim 상태 주입
            FXOverdose.DatingSim.Core.DatingTimeManager.Instance?.LoadFromSaveData(CurrentData);

            IsPendingLoad = false;
            // CurrentData는 비우지 않습니다. 매니저가 없는 씬에서 부분 저장을 할 때
            // 이 스냅샷이 베이스가 되어야 합니다. (SV-A6 / SV-A7)
            Debug.Log("[SaveLoadManager] 저장된 데이터를 인게임에 성공적으로 주입했습니다.");
        }

        private void RestoreMemoryData(TraderMemoryManager memory, SaveData data)
        {
            try
            {
                var type = typeof(TraderMemoryManager);
                
                var shortTermField = type.GetField("shortTermDialogues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shortTermField != null)
                {
                    shortTermField.SetValue(memory, new List<string>(data.ShortTermDialogues));
                }

                var longTermField = type.GetField("longTermMemories", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (longTermField != null)
                {
                    longTermField.SetValue(memory, new List<MemoryEntry>(data.LongTermMemories));
                }

                var dailyField = type.GetField("dailySummaries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (dailyField != null)
                {
                    var dict = new Dictionary<int, string>();
                    for (int i = 0; i < data.DailySummaryKeys.Count; i++)
                    {
                        dict[data.DailySummaryKeys[i]] = data.DailySummaryValues[i];
                    }
                    dailyField.SetValue(memory, dict);
                }
            }
            catch(Exception e)
            {
                Debug.LogError($"[SaveLoadManager] Memory 복구 오류: {e.Message}");
            }
        }
    }
}
