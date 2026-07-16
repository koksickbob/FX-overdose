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
            var gm = FindAnyObjectByType<GameManager>();
            var status = TraderStatus.CanonicalInstance;
            var levelSys = TraderLevelSystem.Instance;
            var memory = TraderMemoryManager.Instance;
            var trading = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);

            if (gm == null || status == null || levelSys == null || memory == null)
            {
                Debug.LogError("[SaveLoadManager] 저장에 실패했습니다. 필요한 시스템 중 일부를 찾을 수 없습니다.");
                return false;
            }

            if (gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.Paused)
            {
                Debug.LogWarning($"[SaveLoadManager] 현재 상태({gm.CurrentState})에서는 저장할 수 없습니다.");
                return false;
            }

            // 현재 저장 포맷은 포지션/증거금을 직렬화하지 않습니다. 열린 포지션을 현금만 저장하면
            // 불러오기 후 증거금이 사라지고 일일 손익도 왜곡되므로 안전하게 저장을 막습니다.
            if (trading != null && trading.IsActive)
            {
                Debug.LogWarning("[SaveLoadManager] 열린 포지션이 있어 저장하지 않았습니다. 포지션을 정리한 뒤 다시 저장해 주세요.");
                return false;
            }

            SaveData data = new SaveData
            {
                // GameManager
                Balance = gm.CurrentBalance,
                CurrentDay = gm.CurrentDay,
                CurrentHour = gm.CurrentHour,
                CurrentMinute = gm.CurrentMinute,
                SecondsPerGameMinute = gm.SecondsPerGameMinute,
                StartOfDayEquity = gm.StartOfDayEquity,

                // TraderStatus
                PeakBalance = status.PeakBalance,
                CurrentMental = status.CurrentMental,
                CurrentMentalState = status.CurrentMentalState,
                CurrentHealth = status.CurrentHealth,

                // LevelSystem
                ProtagonistLevel = levelSys.ProtagonistLevel,
                ProtagonistEXP = levelSys.ProtagonistEXP,
                ChartStudyLevel = levelSys.ChartStudyLevel,
                CubePatienceLevel = levelSys.CubePatienceLevel,
                BookJudgmentLevel = levelSys.BookJudgmentLevel,
            };

            // MemoryManager
            // private 필드들에 접근하기 위해 Reflection을 사용할 수도 있지만, 
            // SaveLoadManager에서 직접 데이터를 얻거나 GameManager처럼 public Getter가 있으면 좋음.
            // 임시로 Reflection으로 추출. 추후 TraderMemoryManager에 GetData() 메서드를 추가하는 것이 좋음.
            ExtractMemoryData(memory, data);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetSaveFilePath(slotIndex), json);
            Debug.Log($"[SaveLoadManager] 슬롯 {slotIndex}에 게임 저장 완료:\n{GetSaveFilePath(slotIndex)}");
            return true;
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

        public void PrepareLoadGame(int slotIndex)
        {
            string path = GetSaveFilePath(slotIndex);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveLoadManager] 슬롯 {slotIndex}에 세이브 파일이 존재하지 않습니다.");
                return;
            }

            string json = File.ReadAllText(path);
            CurrentData = JsonUtility.FromJson<SaveData>(json);
            IsPendingLoad = true;
            Debug.Log($"[SaveLoadManager] 데이터 로드 대기 중 (GameScene 진입 시 복원 예정)");
        }

        public void PrepareNewGame()
        {
            CurrentData = null;
            IsPendingLoad = false;
        }

        public void ApplyLoadedDataToGame()
        {
            if (!IsPendingLoad || CurrentData == null) return;

            var gm = FindAnyObjectByType<GameManager>();
            var status = TraderStatus.CanonicalInstance;
            var levelSys = TraderLevelSystem.Instance;
            var memory = TraderMemoryManager.Instance;

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

            if (status != null)
            {
                // TraderStatus 필드 복구
                var stType = typeof(TraderStatus);
                stType.GetField("peakBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(status, CurrentData.PeakBalance);
                stType.GetField("currentMental", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(status, CurrentData.CurrentMental);
                stType.GetField("currentMentalState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(status, CurrentData.CurrentMentalState);
                stType.GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(status, CurrentData.CurrentHealth);
            }

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

            IsPendingLoad = false;
            CurrentData = null;
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
