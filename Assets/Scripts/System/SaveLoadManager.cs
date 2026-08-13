using System;
using System.IO;
using UnityEngine;
using FXOverdose.AI;
using FXOverdose.Trading;
using System.Collections.Generic;

namespace FXOverdose.Core
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; private set; }

        public SaveData CurrentData { get; private set; }

        /// <summary>현재 타이틀에서 선택해 실행 중인 게임 모드입니다.</summary>
        public GameMode CurrentGameMode { get; private set; } = GameMode.Story;
        public bool IsTutorialCompleted { get; set; } = false;

        /// <summary>스토리 모드에서 현재 사용 중인 저장 슬롯입니다.</summary>
        public int ActiveStorySlotIndex { get; private set; }

        public bool AllowsAITrading => CurrentGameMode != GameMode.Challenge;
        public bool AllowsSaving => CurrentGameMode == GameMode.Story;

        // 로드 진행 후 GameScene 진입 시 상태를 복원해야 하는지 여부 플래그
        public bool IsPendingLoad { get; private set; }

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

        public bool SaveGame(int slotIndex)
        {
            if (!AllowsSaving)
            {
                Debug.LogWarning($"[SaveLoadManager] {CurrentGameMode} 모드는 저장을 지원하지 않습니다. 스토리 모드에서만 저장할 수 있습니다.");
                return false;
            }

            slotIndex = Mathf.Clamp(slotIndex, 0, 2);
            ActiveStorySlotIndex = slotIndex;

            var gm = FindAnyObjectByType<GameManager>();
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
            SaveData data = CurrentData ?? ReadSaveFile(slotIndex) ?? new SaveData();

            data.Version = Application.version;
            data.GameMode = CurrentGameMode;
            data.IsTutorialCompleted = this.IsTutorialCompleted;

            if (gm != null)
            {
                data.Balance = gm.CurrentBalance;
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
            slotIndex = Mathf.Clamp(slotIndex, 0, 2);
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
            ActiveStorySlotIndex = slotIndex;
            IsPendingLoad = true;
            Debug.Log($"[SaveLoadManager] 스토리 슬롯 {slotIndex + 1} 데이터 로드 대기 중 (GameScene 진입 시 복원 예정)");
            return true;
        }

        public void PrepareNewGame()
        {
            PrepareNewGame(GameMode.Story, 0);
        }

        public void PrepareNewGame(GameMode mode, int storySlotIndex = 0)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode))
                mode = GameMode.Story;

            CurrentGameMode = mode;
            ActiveStorySlotIndex = mode == GameMode.Story
                ? Mathf.Clamp(storySlotIndex, 0, 2)
                : 0;
            CurrentData = null;
            IsPendingLoad = false;
            DeliveryFoodManager.ResetStateForNewGame();
            // static이라 이전 판의 방향성이 남습니다. 새 게임에서 반드시 비웁니다. (S8)
            DailyMarketOutlook.Reset();
            Debug.Log($"[SaveLoadManager] 새 게임 준비: {CurrentGameMode}" +
                      (CurrentGameMode == GameMode.Story ? $" / Slot {ActiveStorySlotIndex + 1}" : string.Empty));
        }

        public void ApplyLoadedDataToGame()
        {
            if (!IsPendingLoad || CurrentData == null) return;

            var gm = FindAnyObjectByType<GameManager>();
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

            this.IsTutorialCompleted = CurrentData.IsTutorialCompleted;
            
            // 과거 세이브 파일 보정: 튜토리얼 완료 플래그가 없더라도 이미 2일차 이상이라면 완료된 것으로 간주
            if (!this.IsTutorialCompleted && CurrentData.CurrentDay > 1)
            {
                this.IsTutorialCompleted = true;
                CurrentData.IsTutorialCompleted = true;
            }

            if (gm != null)
            {
                // GameManager 필드 복구 (Reflection)
                var gmType = typeof(GameManager);
                gmType.GetField("currentBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.Balance);
                gmType.GetField("currentDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.CurrentDay);
                gmType.GetField("currentHour", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.CurrentHour);
                gmType.GetField("currentMinute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.CurrentMinute);
                gmType.GetField("secondsPerGameMinute", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gm, CurrentData.SecondsPerGameMinute);
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

            if (inventory != null && shopManager != null)
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
