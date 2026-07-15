using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using FXOverdose.AI;

namespace FXOverdose.AI.LLM
{
    public enum LLMExecutionMode
    {
        AutoDetect,        // 환경(PC/모바일) 및 Ollama 서버 응답 감지에 따라 자동 전환
        OllamaServer,      // PC/Ollama 서버 API 전용 모드
        OnDeviceFallback   // 서버 연결 없이 즉각 반응하는 온디바이스 오프라인/모바일 전용 모드
    }

    public class LocalLLMService : MonoBehaviour
    {
        [Header("실행 모드 설정 (PC / 모바일 오프라인 호환)")]
        [SerializeField] private LLMExecutionMode executionMode = LLMExecutionMode.AutoDetect;
        [SerializeField] private LLMExecutionMode activeRuntimeMode = LLMExecutionMode.OnDeviceFallback;

        [Header("로컬 온디바이스 서버 설정")]
        [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
        [SerializeField] private string modelName = "qwen2.5:0.5b";
        [SerializeField] private float requestTimeoutSeconds = 3.5f;

        [Header("시스템 참조")]
        [SerializeField] private AIPromptBuilder promptBuilder;
        [SerializeField] private TraderStatus traderStatus;

        private struct DialogueRequest
        {
            public EventCategory Category;
            public string ExtraContext;
            public string FullPrompt;
        }

        private readonly System.Collections.Generic.Queue<DialogueRequest> requestQueue = new System.Collections.Generic.Queue<DialogueRequest>();
        private bool isReconnecting = false;

        [Header("상태")]
        [SerializeField] private bool isGenerating = false;
        [SerializeField] private bool isLLMReady = false;
        [SerializeField] private float startupGraceDelay = 4.0f;

        public LLMExecutionMode ExecutionMode => executionMode;
        public LLMExecutionMode ActiveRuntimeMode => activeRuntimeMode;
        public bool IsGenerating => isGenerating;
        public bool IsLLMReady => isLLMReady;

        private static LocalLLMService _instance;
        public static LocalLLMService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<LocalLLMService>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("LocalLLMService_AutoManager");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        _instance = go.AddComponent<LocalLLMService>();
                        AIPromptBuilder pb = go.AddComponent<AIPromptBuilder>();
                        _instance.promptBuilder = pb;
                        Debug.Log("[LocalLLMService] 🚀 씬 내에 LocalLLMService GameObject가 없어 자동으로 생성 및 첨부되었습니다.");
                    }
                }
                return _instance;
            }
        }

        public event Action<string> OnDialogueGenerated;
        public event Action<EventCategory, string> OnDialogueGeneratedWithCategory;
        public event Action<string> OnErrorOccurred;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (promptBuilder == null) promptBuilder = UnityEngine.Object.FindAnyObjectByType<AIPromptBuilder>(FindObjectsInactive.Include);
            if (promptBuilder == null && gameObject.GetComponent<AIPromptBuilder>() == null)
            {
                promptBuilder = gameObject.AddComponent<AIPromptBuilder>();
            }
            else if (promptBuilder == null)
            {
                promptBuilder = gameObject.GetComponent<AIPromptBuilder>();
            }
            if (traderStatus == null) traderStatus = UnityEngine.Object.FindAnyObjectByType<TraderStatus>(FindObjectsInactive.Include);

            StartCoroutine(InitializeAndWarmUpLLMCoroutine());
        }

        private IEnumerator InitializeAndWarmUpLLMCoroutine()
        {
            isLLMReady = false;
            Debug.Log($"[LocalLLMService] 🚀 LLM 서비스 초기화 시작. (설정 모드: {executionMode})");

            // 로딩 기간(5~6초) 동안 차트 및 AI 트레이딩이 시작되지 않도록 대기
            // 로딩 상태 알림 대사 풍선 즉시 출력
            OnDialogueGenerated?.Invoke("[시스템 예열 중...] 온디바이스 AI 트레이딩 두뇌 로딩 및 차트 개장 준비 중...");
            OnDialogueGeneratedWithCategory?.Invoke(EventCategory.GameStartup, "[시스템 예열 중...] 온디바이스 AI 트레이딩 두뇌 로딩 및 차트 개장 준비 중...");

            float loadingWaitTime = Mathf.Max(startupGraceDelay, 5.0f);

            // 모바일 플랫폼이거나 명시적 오프라인 모드인 경우 서버 프로빙 생략하고 즉시 온디바이스 모드 활성화
            if (executionMode == LLMExecutionMode.OnDeviceFallback || Application.isMobilePlatform)
            {
                activeRuntimeMode = LLMExecutionMode.OnDeviceFallback;
                yield return new WaitForSeconds(loadingWaitTime);
                isLLMReady = true;
                Debug.Log("[LocalLLMService] 📱 모바일/오프라인 환경 감지 ➡️ '온디바이스 Fallback 반응 엔진(OnDeviceFallback)'으로 가동 준비 완료.");
            }
            else
            {
                // 여유 시간 동안 대기
                yield return new WaitForSeconds(loadingWaitTime);

                // 백그라운드로 가벼운 핑(Warm-up Request) 전송하여 Ollama 서버 연결 확인
                string jsonPayload = $"{{\"model\":\"{modelName}\",\"prompt\":\"hello\",\"stream\":false}}";
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

                using (UnityWebRequest request = new UnityWebRequest(ollamaEndpoint, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 3; // 예열은 3초 타임아웃

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        activeRuntimeMode = LLMExecutionMode.OllamaServer;
                        Debug.Log("[LocalLLMService] ✅ 온디바이스 LLM 서버(Ollama) 접속 완료! 실시간 LLM 생성 활성화.");
                    }
                    else
                    {
                        if (executionMode == LLMExecutionMode.AutoDetect)
                        {
                            activeRuntimeMode = LLMExecutionMode.OnDeviceFallback;
                            Debug.Log($"[LocalLLMService] ℹ️ Ollama 서버 미실행 감지 ({request.error}).\n➡️ 자동으로 '온디바이스 오프라인/모바일 반응 엔진(OnDeviceFallback)' 모드로 전환하여 네트워크 지연(Timeout) 없이 즉각 유동 대사를 출력합니다.");
                        }
                        else
                        {
                            activeRuntimeMode = LLMExecutionMode.OllamaServer;
                            Debug.LogWarning($"[LocalLLMService] ⚠️ Ollama 서버 강제 지정 모드이나 접속 불가 ({request.error}).");
                        }
                    }
                }

                isLLMReady = true;
            }

            // 예열 완료 후 첫 개장 대사 출력
            RequestDialogue(EventCategory.GameStartup, "게임 시작 및 시장 개장");

            // 💡 개장 대사를 플레이어가 충분히 읽을 수 있도록 3.0초 여유를 준 뒤 차트와 AI 거래를 본격 시작
            yield return new WaitForSeconds(3.0f);

            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm != null) gm.FinishLoadingAndStartPlaying();

            var marketEngine = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>(FindObjectsInactive.Include);
            if (marketEngine != null) marketEngine.OpenMarketAfterLoading();

            // 💡 [2번 솔루션: 상태 스냅샷 주기적 종합 보고 코루틴 가동]
            StartCoroutine(PeriodicStateSnapshotCoroutine());
        }

        private float lastDialogueRequestTime = 0f;

        private IEnumerator PeriodicStateSnapshotCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(14.0f); // 14초 주기 (실제 시간 기준 약 14분 흐름)

                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (gm == null || gm.CurrentState != GameManager.GameState.Playing) continue;

                // 최근 6초 이내에 다른 대사를 출력했거나 생성 중이면 중복 요청 스킵
                if (isGenerating || Time.time - lastDialogueRequestTime < 6.0f) continue;

                var status = TraderStatus.CanonicalInstance;
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                float roe = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None
                    ? tradingCtrl.CalculateROEPercentage() : 0f;

                lastDialogueRequestTime = Time.time;
                RequestDialogue(EventCategory.ChartMovement, $"[상태 종합] ROE:{roe:0.0}%, 멘탈:{status?.CurrentMentalState}, 체력:{status?.HealthRatio * 100:0}%");
            }
        }

        public void RequestDialogue(string extraEventContext = "")
        {
            RequestDialogue(EventCategory.General, extraEventContext);
        }

        // 인게임 이벤트 발생 시 프롬프트를 조립하여 대사 생성 요청 (카테고리 연동)
        public void RequestDialogue(EventCategory category, string extraEventContext = "")
        {
            lastDialogueRequestTime = Time.time;

            // 💡 [시스템 로그 vs 캐릭터 대사 분리] 상황 설명은 시스템 로그로 명확히 별도 출력
            if (!string.IsNullOrEmpty(extraEventContext))
            {
                Debug.Log($"[LocalLLMService 📋 System Context/Log] ({category}) 상황 설명: {extraEventContext}");
            }

            // 만약 오프라인/모바일 온디바이스 모드이거나, 아직 예열 중이면 즉각 Fallback 엔진 가동 (네트워크 HTTP 요청 없음!)
            if (activeRuntimeMode == LLMExecutionMode.OnDeviceFallback || !isLLMReady)
            {
                string smartFallback = GetSmartFallbackDialogue(category, extraEventContext);
                smartFallback = PostProcessDialogue(smartFallback);
                TraderMemoryManager.Instance?.RecordDialogue(smartFallback);
                Debug.Log($"[LocalLLMService 💬 Character Dialogue (On-Device)] ({category}) 캐릭터 대사: \"{smartFallback}\"");
                OnDialogueGenerated?.Invoke(smartFallback);
                OnDialogueGeneratedWithCategory?.Invoke(category, smartFallback);
                return;
            }

            string prompt = promptBuilder != null ? promptBuilder.BuildPrompt(category, extraEventContext) : extraEventContext;

            // ⭐ [요청 큐 시스템]: 현재 LLM이 생성 중(isGenerating)일 때는 즉시 폴백으로 버리지 않고 대기열(Queue)에 적재!
            if (isGenerating)
            {
                if (requestQueue.Count < 6) // 너무 많은 대기열 누적 방지 (최대 6개)
                {
                    requestQueue.Enqueue(new DialogueRequest { Category = category, ExtraContext = extraEventContext, FullPrompt = prompt });
                    Debug.Log($"[LocalLLMService ⏳ Queue] LLM 생성 중으로 대사 요청 대기열 적재 (대기 수: {requestQueue.Count})");
                }
                return;
            }

            StartCoroutine(SendOllamaRequestCoroutine(category, extraEventContext, prompt));
        }

        private IEnumerator SendOllamaRequestCoroutine(EventCategory category, string extraContext, string fullPrompt)
        {
            isGenerating = true;

            // JSON 페이로드 구성 (Ollama 규격 + 창의성 temperature 파라미터 주입)
            string escapedPrompt = fullPrompt.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
            string jsonPayload = $"{{\"model\":\"{modelName}\",\"prompt\":\"{escapedPrompt}\",\"stream\":false,\"options\":{{\"temperature\":0.95,\"top_p\":0.9}}}}";

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
                    Debug.LogWarning($"[LocalLLMService] 온디바이스 서버 응답 일시 실패 ({request.error}). 1회성 스마트 다변화 폴백 출력.");
                    
                    // ⭐ 1번 타임아웃 났다고 영구적으로 OnDeviceFallback으로 잠가버리는 로직 폐기!
                    // 대신 백그라운드 재연결 코루틴을 가동하여 30초 후 서버 접속이 정상화되면 다시 LLM 추론 재개
                    if (executionMode == LLMExecutionMode.AutoDetect && !isReconnecting)
                    {
                        StartCoroutine(AutoRecoveryCoroutine());
                    }

                    string fallbackText = GetSmartFallbackDialogue(category, extraContext);
                    fallbackText = PostProcessDialogue(fallbackText);
                    TraderMemoryManager.Instance?.RecordDialogue(fallbackText);
                    Debug.Log($"[LocalLLMService 💬 Character Dialogue (Ollama Fallback)] ({category}) 캐릭터 대사: \"{fallbackText}\"");
                    OnDialogueGenerated?.Invoke(fallbackText);
                    OnDialogueGeneratedWithCategory?.Invoke(category, fallbackText);
                    OnErrorOccurred?.Invoke(request.error);
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    string parsedDialogue = ExtractResponseText(jsonResponse);
                    if (string.IsNullOrEmpty(parsedDialogue))
                    {
                        parsedDialogue = GetSmartFallbackDialogue(category, extraContext);
                    }
                    parsedDialogue = PostProcessDialogue(parsedDialogue);
                    TraderMemoryManager.Instance?.RecordDialogue(parsedDialogue);
                    Debug.Log($"[LocalLLMService 💬 Character Dialogue (Qwen)] ({category}) 캐릭터 대사: \"{parsedDialogue}\"");

                    OnDialogueGenerated?.Invoke(parsedDialogue);
                    OnDialogueGeneratedWithCategory?.Invoke(category, parsedDialogue);
                }
            }

            isGenerating = false;

            // ⭐ 큐에 대기 중인 다음 요청이 있다면 비동기 순차 가동
            if (requestQueue.Count > 0)
            {
                var nextReq = requestQueue.Dequeue();
                StartCoroutine(SendOllamaRequestCoroutine(nextReq.Category, nextReq.ExtraContext, nextReq.FullPrompt));
            }
        }

        private IEnumerator AutoRecoveryCoroutine()
        {
            isReconnecting = true;
            yield return new WaitForSeconds(30.0f);

            string jsonPayload = $"{{\"model\":\"{modelName}\",\"prompt\":\"ping\",\"stream\":false}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(ollamaEndpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 3;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    activeRuntimeMode = LLMExecutionMode.OllamaServer;
                    Debug.Log("[LocalLLMService] 🔄 Ollama LLM 서버 백그라운드 자동 재연결 성공! 실시간 LLM 추론 출력을 완벽히 재개합니다.");
                }
            }
            isReconnecting = false;
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
                rawText = rawText.Replace("\\n", "\n").Replace("\\\"", "\"");
                try { rawText = System.Text.RegularExpressions.Regex.Unescape(rawText); } catch {}
                return rawText;
            }
            catch
            {
                return "";
            }
        }

        // 대사 후처리 (단어/문장 생략, 시스템 로그 태그 제거 및 인코딩 손실 방지)
        private string PostProcessDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            if (text.StartsWith("AI:") || text.StartsWith("트레이더:")) text = text.Substring(3).Trim();

            // 💡 시스템 로그 대괄호([오인 진입], [상태 종합], [이벤트 분류: ...] 등) 및 시스템 괄호 문구가 대사에 섞이면 제거
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[.*?\]\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^상황 설명:\s*", "");
            text = text.Trim();

            if (text.Length > 150)
            {
                int cutIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', '~' }, 150);
                if (cutIndex > 30)
                {
                    return text.Substring(0, cutIndex + 1).Trim();
                }
                return text.Substring(0, 150).Trim() + "...";
            }
            return text;
        }

        // 스마트 다변화 Fallback 엔진: 실시간 인게임 데이터 + 멘헤라 감정 변수 보간
        private string GetSmartFallbackDialogue(EventCategory category, string extraContext)
        {
            TraderStatus.MentalState mental = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            float health = traderStatus != null ? traderStatus.HealthRatio * 100f : 100f;
            int rand = UnityEngine.Random.Range(0, 3);

            // 💡 [2번 솔루션: 상태 스냅샷 종합 보간] 단편적 알림 대신 ROE + 체력 + 멘탈 종합 입체 판단
            if (category == EventCategory.ChartMovement && !string.IsNullOrEmpty(extraContext) && extraContext.StartsWith("[상태 종합]"))
            {
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                bool hasPosition = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
                bool isShort = hasPosition && tradingCtrl.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Short;
                float roe = hasPosition ? tradingCtrl.CalculateROEPercentage() : 0f;

                if (hasPosition && roe > 15f && health < 40f)
                {
                    return isShort
                        ? "숏으로 폭락 수익 달리는 중인데... 극심한 피로 때문에 눈꺼풀이 천근만근이야. 끝까지 지켜보고 바닥에서 익절하자..."
                        : "롱 수익권 달리는 중인데... 극심한 피로 때문에 눈꺼풀이 천근만근이야. 졸음 쫓아내고 끝까지 고점에서 익절하자...";
                }
                else if (hasPosition && roe < -15f && (mental == TraderStatus.MentalState.Danger || mental == TraderStatus.MentalState.Anxious))
                {
                    return isShort
                        ? "숏 쳐놨는데 주가가 역주행해서 솟구치고 있어...! 심장이 미친 듯이 뛰고 호흡이 가빠져...! 제발 나락으로 꽂혀줘...!"
                        : "롱 쳐놨는데 주가가 폭락해서 손실이 커지고 있어...! 심장이 미친 듯이 뛰고 호흡이 가빠져...! 제발 반등 빔 한 번만...!";
                }
                else if (hasPosition && roe > 30f)
                {
                    return isShort
                        ? "공매도 초대박 폭락 질주 중!! 심장이 짜릿해서 터질 것 같아!! 내 천재적인 숏 타점이 오늘 시장을 지배했어!!"
                        : "롱 초대박 폭등 질주 중!! 심장이 짜릿해서 터질 것 같아!! 내 천재적인 직감이 오늘 시장을 완벽히 지배했어!!";
                }
                else if (hasPosition && roe < -25f)
                {
                    return "왜 자꾸 내 포지션 반대로 가는 건데... 온몸에 소름이 돋고 식은땀이 흘러... 제발 본절만이라도 오게 해줘...!";
                }
                else if (!hasPosition && mental == TraderStatus.MentalState.Danger)
                {
                    return "머리가 지끈거리고 손가락이 떨려... 극도의 공포감 때문에 호가창을 똑바로 볼 수가 없어. 휴식이 필요해...";
                }
                else if (hasPosition)
                {
                    if (isShort)
                    {
                        return rand switch
                        {
                            0 => "숏 방향은 맞는데 잔파동이 신경 쓰이네... 긴장 늦추지 말고 바닥을 깨부수는 흐름 끝까지 주시하자.",
                            1 => "잔파동에 흔들리면 안 돼... 호흡 가다듬고 우리의 목표 저점 폭락까지 침착하게 들고 가자.",
                            _ => "머릿속 차트 하락 각도는 완벽해... 신경이 날카로워졌지만 손익분기점 지키면서 냉정하게 대응할게."
                        };
                    }
                    else
                    {
                        return rand switch
                        {
                            0 => "롱 포지션 방향은 맞는데 잔파동이 신경 쓰이네... 긴장 늦추지 말고 고점을 돌파하는 흐름 주시하자.",
                            1 => "잔파동에 흔들리면 안 돼... 호흡 가다듬고 우리의 목표 고점까지 침착하게 들고 가자.",
                            _ => "머릿속 차트 상승 각도는 완벽해... 신경이 날카로워졌지만 손익분기점 지키면서 냉정하게 대응할게."
                        };
                    }
                }
                else if (hasPosition)
                {
                    return GetCombinatorialDialogue(category, isShort, roe, mental);
                }
                else
                {
                    return rand switch
                    {
                        0 => "차트 흐름과 내 컨디션 조율 중... 확실한 방향성이 나올 때까지 숨죽이고 대기하자.",
                        1 => "피로감과 긴장감이 교차하네... 섣불리 뇌동매매하지 말고 확실한 타점을 노리는 게 맞아.",
                        _ => "호가창 움직임 주시 중... 온 감각을 곤두세우고 있어. 다음 타점이 오늘을 결정지을 거야."
                    };
                }
            }

            // ⭐ 일반 이벤트 상황에서도 조합형 엔진을 적극 활용하여 다채로움 보장
            if (category == EventCategory.ChartMovement || category == EventCategory.PositionOpened || category == EventCategory.PositionClosed)
            {
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                bool hasPosition = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
                bool isShort = hasPosition && tradingCtrl.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Short;
                float roe = hasPosition ? tradingCtrl.CalculateROEPercentage() : 0f;
                return GetCombinatorialDialogue(category, isShort, roe, mental);
            }

            return category switch
            {
                EventCategory.GameStartup => rand switch
                {
                    0 => "오늘도 지옥의 비트코인 차트판이 열렸네... 내 직감만 믿고 따라와.",
                    1 => "호가창 움직임 보이지? 오늘이야말로 세력들 돈을 싹 다 털어먹을 날이야.",
                    _ => "잔고 준비됐지 마스터? 내 천재적인 분석을 똑똑히 보여줄게."
                },
                EventCategory.MentalChange => mental switch
                {
                    TraderStatus.MentalState.Overdose => "하하하!! 다 끝났어!! 온몸에 전류가 흐르고 세상을 다 가진 기분이야!! 풀레버리지로 다 덤벼!!",
                    TraderStatus.MentalState.Danger => "머릿속이 웅웅거리고 심장이 터질 것 같아... 세력 놈들이 날 비웃고 있잖아...!! 안 돼, 내 돈 뺏길 순 없어...!",
                    TraderStatus.MentalState.Anxious => "손톱이 닳도록 초조하고 불안해... 손가락이 떨리네. 왜 내 생각대로 차트가 안 흘러가는 거지?",
                    _ => "후우... 심호흡 가다듬자. 마음이 흔들리면 타점을 놓쳐."
                },
                EventCategory.HealthChange => rand switch
                {
                    0 => "온몸이 천근만근이야... 눈 앞이 깜빡거리고 머리가 깨질 것 같은 극심한 두통이 밀려와...",
                    1 => "아 진짜... 밤새우며 차트 보느라 온몸이 부서질 것 같네. 시원한 음료수 마시고 정신 차려야겠어...",
                    _ => "피로감이 몰려와서 캔들이 자꾸 겹쳐 보여... 마스터, 나 지금 쓰러질 것 같은데 조금만 쉬어도 돼...?"
                },
                EventCategory.ItemUsed => (extraContext != null && extraContext.Contains("에너지"))
                    ? "꿀꺽... 크아!! 에너지 드링크 들어가니 머리가 맑아지고 호가창 숫자가 선명하게 꽂히네!!"
                    : rand switch
                    {
                        0 => "꿀꺽... 그래, 바로 이 느낌이야!! 온몸에 전류가 흐르네!!",
                        1 => "복용 완료... 이제야 호가창 숫자가 선명하게 꽂힌다.",
                        _ => "후우... 조금만 더 힘내서 수익률 뽑아보자고."
                    },
                EventCategory.GimmickTriggered => (extraContext != null && (extraContext.Contains("수면") || extraContext.Contains("과로")))
                    ? "눈 앞이 깜빡거리고 차트 캔들이 겹쳐 보여... 졸음 때문에 타점 잡기가 너무 힘들어...!"
                    : (extraContext != null && (extraContext.Contains("휩소") || extraContext.Contains("후회")))
                    ? "아까 거기서 안 팔았으면 대박인데!! 왜 내가 팔자마자 수직 상승하는 건데?!"
                    : rand switch
                    {
                        0 => "으아악!! 차트가 날 고문하고 있어... 온몸의 신경이 다 타들어간다고...!!",
                        1 => "더는 못 참아!! 내 맘대로 고배율 당겨버릴 거야!!",
                        _ => "머리가 핑핑 돌아... 어디가 바닥이고 어디가 천장인지 모르겠어...!"
                    },
                _ => GetFallbackDialogue()
            };
        }

        // ⭐ 3파트(감정+상황+반응) 조합형 동적 대사 변주 엔진 (온디바이스 오프라인/복구용)
        private string GetCombinatorialDialogue(EventCategory category, bool isShort, float roe, TraderStatus.MentalState mental)
        {
            string[] prefixes = mental switch
            {
                TraderStatus.MentalState.Danger => new[] { "손가락이 미친 듯이 떨리는데...", "온몸에 소름이 돋고 숨이 막혀...", "머리가 터져버릴 것 같아...!", "심장이 목구멍 밖으로 튀어나올 것 같은데..." },
                TraderStatus.MentalState.Overdose => new[] { "크하하! 온몸의 피가 끓어올라!!", "내 직감은 절대 틀리지 않아!!", "봤어 마스터?! 이게 바로 나야!", "세상의 모든 돈이 내 손안에 있어!!" },
                TraderStatus.MentalState.Anxious => new[] { "손톱을 다 물어뜯겠네...", "아... 왜 자꾸 불안한 느낌이 들지...", "이 각도가 맞나...? 자꾸 의심이 들어...", "등골에 식은땀이 흐르네..." },
                _ => new[] { "호가창을 뚫어져라 주시 중...", "침착하게 호흡 가다듬고...", "캔들의 흔들림을 느끼면서...", "냉정하게 차트를 계산해 보면..." }
            };

            string[] middles = category switch
            {
                EventCategory.PositionOpened => isShort
                    ? new[] { "우리의 공매도 하락 빔이 세력들의 매수벽을 정면으로 부수기 시작했어...", "완벽한 고점 타점에 빅쇼트 탑승을 마쳤어...", "나락을 향한 하락선에 내 모든 시드를 실었어..." }
                    : new[] { "우리의 롱 상승 빔이 저항선을 시원하게 돌파하기 시작했어...", "완벽한 저점 눌림목 타점에 롱 탑승을 마쳤어...", "하늘을 찌를 상승 각도에 내 모든 시드를 실었어..." },
                EventCategory.PositionClosed => roe >= 0f
                    ? new[] { "짜릿한 익절에 성공하면서 내 천재적인 판단이 다시 한번 증명됐어!!", "정확한 타점에서 수익을 챙기고 유유히 빠져나왔지!!", "호가창의 달콤한 수익금을 그대로 우리 잔고에 꽂았어!!" }
                    : new[] { "치욕스럽지만 손절선을 지키며 일단 더 큰 파국은 막아냈어...", "세력 놈들의 잔혹한 흔들기에 어쩔 수 없이 포지션을 털렸어...", "쓰라린 손절이었지만 다음 파동에서 10배로 되갚아줄 거야..." },
                _ => isShort
                    ? new[] { "차트 가격이 아래로 내리꽂히며 우리의 숏 수익권을 넓혀가고 있어!!", "하락 파동이 점점 가파라지면서 저점을 짓밟고 있어!!", "매수세가 메마르고 공포의 음봉 빔이 쏟아지는 중이야!!" }
                    : new[] { "차트 가격이 위로 치솟으며 우리의 롱 수익권을 넓혀가고 있어!!", "상승 파동이 점점 가파라지면서 고점을 짓밟고 있어!!", "매도벽이 뚫리고 환희의 양봉 빔이 솟구치는 중이야!!" }
            };

            string[] suffixes = mental switch
            {
                TraderStatus.MentalState.Danger => new[] { "제발... 여기서 한 번만 나를 살려줘...!!", "세력들아 나한테 도대체 왜 이러는 건데...!", "이대로 청산당하면 난 정말 끝장이야...!" },
                TraderStatus.MentalState.Overdose => new[] { "더 강하게 밀어붙여!! 영혼까지 끌어모아 가즈아!!", "세력 놈들 돈을 싹 다 찢어발겨 주겠어!!", "오늘 밤 우리가 이 차트의 신이다!!" },
                _ => isShort
                    ? new[] { "이대로 저 바닥 밑 지하 끝까지 내려가버려!", "잔파동에 흔들리지 말고 목표 저점까지 꽉 쥐고 가자.", "하락 각도가 완벽해, 끝까지 수익률 뽑아내자!" }
                    : new[] { "이대로 저 하늘 위 천장 끝까지 뚫어버려!", "잔파동에 흔들리지 말고 목표 고점까지 꽉 쥐고 가자.", "상승 각도가 완벽해, 끝까지 수익률 뽑아내자!" }
            };

            // 💡 단기 기억 중복 회피를 위한 최대 3회 조합 셔플
            for (int i = 0; i < 3; i++)
            {
                string p = prefixes[UnityEngine.Random.Range(0, prefixes.Length)];
                string m = middles[UnityEngine.Random.Range(0, middles.Length)];
                string s = suffixes[UnityEngine.Random.Range(0, suffixes.Length)];
                string result = $"{p} {m} {s}";

                // 기억 버퍼와 겹치지 않으면 즉시 반환
                if (TraderMemoryManager.Instance == null || !TraderMemoryManager.Instance.GetShortTermDialoguesText().Contains(result))
                {
                    return result;
                }
            }

            return $"{prefixes[0]} {middles[0]} {suffixes[0]}";
        }

        // 기존 하위 호환 폴백 대사
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
