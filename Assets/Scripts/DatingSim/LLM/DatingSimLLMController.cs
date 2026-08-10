using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.LLM.Memory;
using FXOverdose.AI.Dialogue;
using LLMUnity;

namespace FXOverdose.DatingSim.LLM
{
    [RequireComponent(typeof(LLMUnity.LLM))]
    [RequireComponent(typeof(LLMUnity.LLMAgent))]
    public class DatingSimLLMController : MonoBehaviour
    {
        public static DatingSimLLMController Instance { get; private set; }

        [Header("LLM Settings")]
        [SerializeField, Tooltip("과거 기억을 가져올 최대 개수")]
        private int memoryRetrievalCount = 2;
        
        [SerializeField, Tooltip("기억 왜곡이 시작되는 집착도 임계치")]
        private int obsessionDistortionThreshold = 70;

        [Header("RAG Database")]
        [SerializeField, Tooltip("요미 하드코딩 대사 17,000줄 DB (트레이딩/기본 용도)")]
        private YomiDialogueDatabase dialogueDatabase;
        
        [SerializeField, Tooltip("요미 일상 대화 전용 DB (자유 대화 용도)")]
        private YomiDailyDialogueDatabase dailyDialogueDatabase;

        [Header("Fallback Data")]
        [SerializeField]
        private string[] dummyDialogues = {
            "마스터... 무슨 소리 하는 거야...?",
            "계속 나만 바라봐 줘...",
            "응, 마스터가 그렇게 말한다면..."
        };

        private DatingMood currentMood = DatingMood.Neutral;

        private const string YOMI_SYSTEM_RULES = @"[System Rules]
1. Output language: KOREAN ONLY. Do NOT use Chinese, Japanese, or English.
2. Roleplay: You are '요미' (Yomi), a 2D anime girl. The user is '오빠' (Oppa).
3. Tone: Speak casually and informally (반말). Treat Oppa like your boyfriend.
4. Comprehension: Perfectly understand Korean internet slang, typos, and cute variations (e.g., 안뇽 = 안녕, 머해 = 뭐해).
5. Constraint: You MUST strictly mimic the tone, vocabulary, and sentence endings of the provided examples. Do NOT sound like an AI translator.";

        private LLMUnity.LLMAgent llmAgent;

        private void Awake()
        {
            if (Instance == null) 
            {
                Instance = this;
                llmAgent = GetComponent<LLMUnity.LLMAgent>();
                if (llmAgent != null)
                {
                    if (llmAgent.llm != null)
                    {
                        // 1단계 최적화: GPU 가속 100% 활성화 (Lag 제거)
                        llmAgent.llm.numGPULayers = 99;
                    }
                    // 시스템 프롬프트는 ConstructPrompt에서 매 턴마다 동적으로 주입되므로 초기화 불필요
                    // 타 언어 유출 방지를 위해 페널티 완전 제거 (한국어 토큰 사용 억제 방지)
                    llmAgent.repeatPenalty = 1.0f;
                    llmAgent.presencePenalty = 0.0f;
                    // 너무 0.3이면 오히려 기계적인 번역체가 나오므로, 대화의 자연스러움을 위해 0.6으로 타협
                    llmAgent.temperature = 0.6f;
                }
            }
            else 
            {
                Destroy(gameObject);
            }
        }

        public void SetCurrentMood(DatingMood mood)
        {
            currentMood = mood;
        }

        // --- LLM 로딩 상태 (P2_04) ---
        public bool IsLLMReady => llmAgent != null && llmAgent.llm != null && llmAgent.llm.started;

        public async Task WaitUntilReadyAsync()
        {
            if (llmAgent != null && llmAgent.llm != null)
            {
                await llmAgent.llm.WaitUntilReady();
            }
        }

        public async Task<string> GenerateChatAsync(string userMessage)
        {
            // 1. 단기 버퍼에 유저 메시지 기록
            if (DatingSimMemoryDB.Instance != null)
            {
                DatingSimMemoryDB.Instance.AppendToSceneBuffer("오빠", userMessage);
            }

            // 2. 프롬프트 구성 (대본 연기 모드)
            string prompt = ConstructPrompt(userMessage);

            // 3. LLM 호출
            string llmOutput = "";
            if (llmAgent != null)
            {
                llmOutput = await llmAgent.Chat(prompt, null, null, false);
            }
            else
            {
                Debug.LogWarning("[DatingSimLLM] LLMAgent가 없습니다. Fallback 사용.");
            }

            // 4. JSON 파싱 및 한국어 검증
            string finalDialogue = ParseAndValidate(llmOutput);

            // 5. 현재 요미 대화를 버퍼에 추가 & 턴 증가
            if (DatingSimMemoryDB.Instance != null)
            {
                DatingSimMemoryDB.Instance.AppendToSceneBuffer("요미", finalDialogue);
            }
            if (ScenarioManager.Instance != null)
            {
                ScenarioManager.Instance.IncrementTurn();
            }

            return finalDialogue;
        }

        private string ConstructPrompt(string userMsg)
        {
            StringBuilder sb = new StringBuilder();
            
            // 1. 페르소나 절대 규칙
            sb.AppendLine("[페르소나 절대 규칙]");
            sb.AppendLine(YOMI_SYSTEM_RULES);
            sb.AppendLine("------------------");
            
            // 2. 현재 시나리오 대본 주입 (핵심)
            var scene = ScenarioManager.Instance != null ? ScenarioManager.Instance.CurrentScenario : null;
            if (scene != null)
            {
                sb.AppendLine("[현재 씬(Scene) 대본]");
                sb.AppendLine($"- 상황(Context): {scene.contextDescription}");
                sb.AppendLine($"- 목표(Goal): {scene.actorGoal}");
                
                if (scene.dialogueExamples.Count > 0)
                {
                    sb.AppendLine("\n[참고용 대사 예시 (톤앤매너 100% 모방할 것)]");
                    foreach (var ex in scene.dialogueExamples)
                    {
                        sb.AppendLine($"- \"{ex}\"");
                    }
                }
                sb.AppendLine("------------------");
            }
            else
            {
                // 씬이 없으면 기존처럼 가볍게 상태만 주입 (안전장치)
                sb.AppendLine("[현재 상태]");
                sb.AppendLine($"현재 기분: {currentMood}");
                sb.AppendLine("------------------");
            }

            // 3. 단기 대화 버퍼 (티키타카 연속성 유지)
            if (DatingSimMemoryDB.Instance != null)
            {
                string recentChat = DatingSimMemoryDB.Instance.GetRecentContextString();
                if (recentChat != "최근 대화 없음")
                {
                    sb.AppendLine("[최근 대화 내역]");
                    sb.AppendLine(recentChat);
                    sb.AppendLine("------------------");
                }
            }
            
            // 4. 대본 이어쓰기 유도
            sb.AppendLine("---");
            sb.AppendLine($"오빠: {userMsg}");
            sb.Append("요미: ");
            
            return sb.ToString();
        }

        private string ParseAndValidate(string rawText)
        {
            try
            {
                // 모델이 혼자서 여러 줄을 뇌절(요미: ... \n 요미: ...)하는 것을 막기 위해 첫 줄만 가져옴
                string[] lines = rawText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0) return dummyDialogues[0];
                
                string result = lines[0].Trim(' ', '\"', '\'');
                
                // "요미:" 또는 "요미 :" 접두사 제거
                if (result.StartsWith("요미:"))
                {
                    result = result.Substring(3).Trim();
                }
                else if (result.StartsWith("요미 :"))
                {
                    result = result.Substring(4).Trim();
                }
                
                if (!string.IsNullOrEmpty(result) && IsValidKorean(result))
                {
                    return result;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DatingSimLLM] Text Validation failed: {e.Message}");
            }
            
            // Fallback
            return dummyDialogues[UnityEngine.Random.Range(0, dummyDialogues.Length)];
        }

        private bool IsValidKorean(string text)
        {
            // 중국어(한자) 및 일본어가 단 한 글자라도 포함되어 있으면 즉시 기각 (엄격한 통제)
            if (Regex.IsMatch(text, @"[\u4e00-\u9fff\u3400-\u4dbf\u3040-\u30ff]"))
            {
                return false;
            }

            int koreanCharCount = Regex.Matches(text, @"[가-힣]").Count;
            return koreanCharCount >= 2;
        }


    }
}
