using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FXOverdose.AI.LLM
{
    public class LocalLLMService : MonoBehaviour
    {
        [Header("로컬 온디바이스 서버 설정")]
        [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
        [SerializeField] private string modelName = "qwen2.5:0.5b";
        [SerializeField] private float requestTimeoutSeconds = 3.5f;

        [Header("시스템 참조")]
        [SerializeField] private AIPromptBuilder promptBuilder;
        [SerializeField] private TraderStatus traderStatus;

        [Header("상태")]
        [SerializeField] private bool isGenerating = false;
        public bool IsGenerating => isGenerating;

        public event Action<string> OnDialogueGenerated;
        public event Action<string> OnErrorOccurred;

        private void Start()
        {
            if (promptBuilder == null) promptBuilder = FindAnyObjectByType<AIPromptBuilder>();
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
        }

        // 인게임 이벤트 발생 시 프롬프트를 조립하여 대사 생성 요청
        public void RequestDialogue(string extraEventContext = "")
        {
            if (isGenerating) return;

            string prompt = promptBuilder != null ? promptBuilder.BuildPrompt(extraEventContext) : extraEventContext;
            StartCoroutine(SendOllamaRequestCoroutine(prompt));
        }

        private IEnumerator SendOllamaRequestCoroutine(string fullPrompt)
        {
            isGenerating = true;

            // JSON 페이로드 구성 (Ollama 규격)
            string escapedPrompt = fullPrompt.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
            string jsonPayload = $"{{\"model\":\"{modelName}\",\"prompt\":\"{escapedPrompt}\",\"stream\":false}}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            using (UnityWebRequest request = new UnityWebRequest(ollamaEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = (int)Mathf.Ceil(requestTimeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[LocalLLMService] 온디바이스 서버 응답 실패 ({request.error}). 안전망(Fallback) 대사 출력.");
                    string fallbackText = GetFallbackDialogue();
                    OnDialogueGenerated?.Invoke(fallbackText);
                    OnErrorOccurred?.Invoke(request.error);
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    string parsedDialogue = ExtractResponseText(jsonResponse);
                    if (string.IsNullOrEmpty(parsedDialogue))
                    {
                        parsedDialogue = GetFallbackDialogue();
                    }
                    else
                    {
                        // 2문장 이내 길이 자르기 및 검열 후처리
                        parsedDialogue = PostProcessDialogue(parsedDialogue);
                    }

                    Debug.Log($"[LocalLLMService 🧠 Qwen] {parsedDialogue}");
                    OnDialogueGenerated?.Invoke(parsedDialogue);
                }
            }

            isGenerating = false;
        }

        // Ollama JSON 응답 파싱
        private string ExtractResponseText(string json)
        {
            try
            {
                int responseIndex = json.IndexOf("\"response\":\"");
                if (responseIndex == -1) return "";

                int startIndex = responseIndex + 12;
                int endIndex = json.IndexOf("\"", startIndex);
                while (endIndex != -1 && json[endIndex - 1] == '\\')
                {
                    endIndex = json.IndexOf("\"", endIndex + 1);
                }

                if (endIndex == -1) return "";
                string rawText = json.Substring(startIndex, endIndex - startIndex);
                return rawText.Replace("\\n", "\n").Replace("\\\"", "\"");
            }
            catch
            {
                return "";
            }
        }

        // 대사 후처리 (1~2문장 길이 제한 및 불필요 접두어 제거)
        private string PostProcessDialogue(string text)
        {
            text = text.Trim();
            if (text.StartsWith("AI:") || text.StartsWith("트레이더:")) text = text.Substring(3).Trim();

            // 3개 이상의 마침표/느낌표 등으로 문장이 길어질 경우 자르기
            string[] sentences = text.Split(new char[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length > 2)
            {
                char lastChar = text[sentences[0].Length + sentences[1].Length + 1];
                return $"{sentences[0].Trim()}{text[sentences[0].Length]} {sentences[1].Trim()}{lastChar}";
            }
            return text;
        }

        // 서버 지연/오프라인 시 출력되는 멘탈 상태별 폴백 대사 안전망
        private string GetFallbackDialogue()
        {
            if (traderStatus == null) return "차트 흐름이 이상해... 집중하자.";

            return traderStatus.CurrentMentalState switch
            {
                TraderStatus.MentalState.Stable => "완벽해... 이 돌파 각도는 무조건 상방이야. 내가 시장을 지배하고 있어.",
                TraderStatus.MentalState.Anxious => "왜...? 왜 여기서 윗꼬리를 달고 밀리지? 아니야, 내 분석이 틀릴 리 없어...",
                TraderStatus.MentalState.Danger => "손절선... 손절선에 닿는다고?! 안 돼, 이대로 청산당할 순 없어! 세력 놈들이 내 매물만 노리고 있잖아!!",
                TraderStatus.MentalState.Overdose => "하하하!! 다 끝났어!! 남은 시드 전부 125배 풀레버리지 올인이다!! 청산당하든 대박나든 끝장을 보자!!",
                _ => "차트 분석 중..."
            };
        }
    }
}
