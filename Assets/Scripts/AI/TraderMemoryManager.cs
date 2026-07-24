using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


namespace FXOverdose.AI
{
    public class TraderMemoryManager : MonoBehaviour
    {
        public static TraderMemoryManager Instance { get; private set; }

        [Header("단기 기억 버퍼 (최근 대사 FIFO)")]
        [SerializeField] private int maxShortTermBufferSize = 8;
        private readonly List<string> shortTermDialogues = new List<string>();

        [Header("장기 기억 아카이브 (이벤트 및 날짜별 요약)")]
        [SerializeField] private int maxLongTermMemories = 15; // Pruning 후 유지할 최대 고중요도 기억 수
        private readonly List<MemoryEntry> longTermMemories = new List<MemoryEntry>();
        private readonly Dictionary<int, string> dailySummaries = new Dictionary<int, string>();

        [Header("시스템 참조")]
        [SerializeField] private GameManager gameManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        }

        // 1. 단기 기억 (최근 내뱉은 대사) 기록
        public void RecordDialogue(string dialogue)
        {
            if (string.IsNullOrEmpty(dialogue)) return;
            string clean = dialogue.Trim();
            
            // 중복 방지
            if (shortTermDialogues.Count > 0 && shortTermDialogues[shortTermDialogues.Count - 1] == clean) return;

            shortTermDialogues.Add(clean);
            if (shortTermDialogues.Count > maxShortTermBufferSize)
            {
                shortTermDialogues.RemoveAt(0);
            }
        }

        public string GetShortTermDialoguesText()
        {
            if (shortTermDialogues.Count == 0) return "없음 (오늘 첫 대사)";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < shortTermDialogues.Count; i++)
            {
                sb.AppendLine($"- {shortTermDialogues[i]}");
            }
            return sb.ToString().TrimEnd();
        }

        // 2. 중요 이벤트 기억 추가 (장기 기억 후보)
        public void AddMemory(EventCategory category, string description, int importanceScore)
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            int day = gameManager != null ? gameManager.CurrentDay : 1;
            string timeStr = gameManager != null ? $"{gameManager.CurrentHour:D2}:{gameManager.CurrentMinute:D2}" : "12:00";

            MemoryEntry entry = new MemoryEntry(day, timeStr, category, Mathf.Clamp(importanceScore, 1, 10), description);
            longTermMemories.Add(entry);
            Debug.Log($"[TraderMemoryManager 🧠 기억 저장] {entry}");

            // 실시간 Pruning 제한을 넘으면 즉시 저중요도부터 정리
            if (longTermMemories.Count > maxLongTermMemories * 2)
            {
                PruneMemories();
            }
        }

        // 3. 자정(24시) 지나 다음 날(Day++) 넘어갈 때 일일 마감 압축 및 최적화(Pruning) 수행
        public void OnDayAdvanced(int newDay)
        {
            int closedDay = newDay - 1;
            if (closedDay <= 0) return;

            // 전날 기억 수집
            var yesterdayMemories = longTermMemories.Where(m => m.Day == closedDay).ToList();
            if (yesterdayMemories.Count > 0)
            {
                // 일일 요약 문자열 생성
                int maxScore = yesterdayMemories.Max(m => m.ImportanceScore);
                var topEvent = yesterdayMemories.OrderByDescending(m => m.ImportanceScore).First();
                int totalEvents = yesterdayMemories.Count;

                string summary = $"[Day {closedDay} 마감 요약] 총 {totalEvents}개 주요 사건 발생. " +
                                 $"가장 충격적인 순간(중요도 {maxScore}/10): {topEvent.Description}";
                
                dailySummaries[closedDay] = summary;
                Debug.Log($"[TraderMemoryManager 📚 일일 압축 완료] {summary}");
            }
            else
            {
                dailySummaries[closedDay] = $"[Day {closedDay} 마감 요약] 평온하게 차트를 관망하며 큰 사건 없이 마감.";
            }

            // ⭐ 기억 최적화 (Pruning): 중요도가 4점 이하인 사소한 과거 기억은 삭제하고 핵심 트라우마/대박만 남김
            PruneMemories();

            // 날짜가 바뀌었으므로 단기 대사 버퍼 절반 비우기 (새로운 아침 기분 전환)
            if (shortTermDialogues.Count > 3)
            {
                shortTermDialogues.RemoveRange(0, shortTermDialogues.Count - 3);
            }
        }

        // 기억 최적화: 중요도 낮은 과거 기억 정리 및 Top N 유지
        private void PruneMemories()
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            int currentDay = gameManager != null ? gameManager.CurrentDay : 1;

            // 현재 일차가 아닌 과거 일차의 기억 중 중요도가 4 이하인 것은 과감히 삭제 (Daily Summary로 이미 압축되었으므로)
            longTermMemories.RemoveAll(m => m.Day < currentDay && m.ImportanceScore <= 4);

            // 그래도 최대 보관 수(maxLongTermMemories)를 넘으면 중요도 순 및 최신 순으로 정렬하여 상위만 유지
            if (longTermMemories.Count > maxLongTermMemories)
            {
                var sorted = longTermMemories
                    .OrderByDescending(m => m.ImportanceScore)
                    .ThenByDescending(m => m.Day)
                    .Take(maxLongTermMemories)
                    .ToList();
                
                longTermMemories.Clear();
                longTermMemories.AddRange(sorted);
            }
        }

        // 4. LLM 프롬프트 주입용 최적화된 장기 기억 컨텍스트 가공
        public string GetFormattedMemoryContextForPrompt()
        {
            StringBuilder sb = new StringBuilder();

            // (1) 과거 일일 요약본 중 최근 3일치 주입
            if (dailySummaries.Count > 0)
            {
                sb.AppendLine("<과거 일자별 압축 기억 요약>");
                var recentDays = dailySummaries.OrderByDescending(kvp => kvp.Key).Take(3);
                foreach (var kvp in recentDays.Reverse())
                {
                    sb.AppendLine($"- {kvp.Value}");
                }
            }

            // (2) 충격량이 컸던 핵심 트라우마 / 대박 경험 Top 3 주입
            var topTraumas = longTermMemories
                .Where(m => m.ImportanceScore >= 7)
                .OrderByDescending(m => m.ImportanceScore)
                .Take(3)
                .ToList();

            if (topTraumas.Count > 0)
            {
                sb.AppendLine("<각인된 강렬한 핵심 기억/트라우마>");
                foreach (var m in topTraumas)
                {
                    sb.AppendLine($"- {m.ToString()}");
                }
            }

            string result = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? "아직 특별히 각인된 과거 기억이나 충격적인 사건은 없습니다." : result;
        }

        // 새 게임 시작 시 전체 리셋
        public void ResetAll()
        {
            shortTermDialogues.Clear();
            longTermMemories.Clear();
            dailySummaries.Clear();
            Debug.Log("[TraderMemoryManager] 🧹 모든 단기/장기 기억이 초기화되었습니다.");
        }
    }
}

