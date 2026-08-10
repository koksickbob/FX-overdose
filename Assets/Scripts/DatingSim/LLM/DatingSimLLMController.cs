using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.LLM.Memory;

namespace FXOverdose.DatingSim.LLM
{
    public class DatingSimLLMController : MonoBehaviour
    {
        public static DatingSimLLMController Instance { get; private set; }

        [Header("LLM Settings")]
        [SerializeField, Tooltip("과거 기억을 가져올 최대 개수")]
        private int memoryRetrievalCount = 2;
        
        [SerializeField, Tooltip("기억 왜곡이 시작되는 집착도 임계치")]
        private int obsessionDistortionThreshold = 70;

        [Header("Fallback Data")]
        [SerializeField]
        private string[] dummyDialogues = {
            "마스터... 무슨 소리 하는 거야...?",
            "계속 나만 바라봐 줘...",
            "응, 마스터가 그렇게 말한다면..."
        };

        private DatingMood currentMood = DatingMood.Neutral;

        [Serializable]
        private class LLMResponse
        {
            public string Thought;
            public string Dialogue;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void SetCurrentMood(DatingMood mood)
        {
            currentMood = mood;
        }

        // --- LLM 로딩 상태 (P2_04) ---
        public bool IsLLMReady { get; private set; } = false;
        public float LLMLoadProgress { get; private set; } = 0f;

        public async Task InitializeLLMAsync()
        {
            if (IsLLMReady) return;
            
            LLMLoadProgress = 0f;
            // Qwen2.5-7B GGUF 모델을 메모리에 올리는 시뮬레이션
            for (int i = 0; i <= 20; i++)
            {
                LLMLoadProgress = i / 20f;
                await Task.Delay(100); 
            }
            IsLLMReady = true;
        }

        public async Task<string> GenerateChatAsync(string userMessage, MemoryTopic topic)
        {
            int affection = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.CurrentAffection : 0;
            int obsession = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.CurrentObsession : 0;

            // 1. 과거 기억 로드 (비동기)
            List<string> memories = new List<string>();
            if (DatingSimMemoryDB.Instance != null)
            {
                memories = await DatingSimMemoryDB.Instance.LoadRecentConversationsAsync(userMessage, topic, memoryRetrievalCount);
            }

            // 2. 기억 왜곡 전처리 (Memory Distortion)
            StringBuilder memoryContext = new StringBuilder();
            if (memories.Count > 0)
            {
                memoryContext.AppendLine("[과거 플래시백]");
                foreach (var mem in memories)
                {
                    memoryContext.AppendLine($"- {mem}");
                }

                if (obsession >= obsessionDistortionThreshold)
                {
                    memoryContext.AppendLine("마스터는 나를 피하려고 했어... 다 거짓말이야...");
                }
            }

            // 3. 프롬프트 구성 (하이브리드 2-Layer 주입)
            string prompt = ConstructPrompt(userMessage, affection, obsession, currentMood, memoryContext.ToString());

            // 4. LLM 호출 (여기서는 실제 LLMUnity 대신 임시 시뮬레이션 코드)
            // LLMAgent.Chat(prompt)가 들어갈 자리. 현재는 에뮬레이션.
            string llmOutput = await MockLLMCall(prompt);

            // 5. JSON 파싱 및 한국어 검증
            string finalDialogue = ParseAndValidate(llmOutput);

            // 6. 현재 대화를 기억 DB에 비동기 저장
            if (DatingSimMemoryDB.Instance != null)
            {
                await DatingSimMemoryDB.Instance.SaveConversationAsync(topic, $"마스터: {userMessage}\n요미: {finalDialogue}");
            }

            return finalDialogue;
        }

        private string ConstructPrompt(string userMsg, int affection, int obsession, DatingMood mood, string memoryContext)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[System Context - 미연시 런타임]");
            sb.AppendLine($"[상태]: 호감도 {affection}/100, 집착도 {obsession}/100");
            sb.AppendLine($"[단기 텐션]: 현재 기분은 '{mood.ToString()}' 상태임.");
            sb.AppendLine(memoryContext);
            sb.AppendLine("반드시 아래 JSON 양식으로만 출력할 것. 다른 텍스트는 절대 포함하지 마시오.");
            sb.AppendLine("{");
            sb.AppendLine("  \"Thought\": \"[속마음 작성]\",");
            sb.AppendLine("  \"Dialogue\": \"[실제 대사 작성]\"");
            sb.AppendLine("}");
            sb.AppendLine($"유저 발언: {userMsg}");
            return sb.ToString();
        }

        private string ParseAndValidate(string rawJson)
        {
            try
            {
                // 정규식으로 JSON 추출
                Match match = Regex.Match(rawJson, @"\{[\s\S]*\}");
                if (match.Success)
                {
                    string jsonString = match.Value;
                    LLMResponse response = JsonUtility.FromJson<LLMResponse>(jsonString);
                    if (response != null && !string.IsNullOrEmpty(response.Dialogue))
                    {
                        if (IsValidKorean(response.Dialogue))
                        {
                            return response.Dialogue;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DatingSimLLM] JSON Parsing failed: {e.Message}");
            }
            
            // Fallback
            return dummyDialogues[UnityEngine.Random.Range(0, dummyDialogues.Length)];
        }

        private bool IsValidKorean(string text)
        {
            int koreanCharCount = 0;
            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) koreanCharCount++;
            }
            return koreanCharCount >= 2;
        }

        private async Task<string> MockLLMCall(string prompt)
        {
            // 실제 LLM 연동 전 임시 딜레이 및 하드코딩 응답 (개발 테스트용)
            await Task.Delay(1000);
            return "{\n  \"Thought\": \"마스터가 말을 걸어줬어.\",\n  \"Dialogue\": \"마스터... 정말 나 버리지 않을 거지?\"\n}";
        }
    }
}
