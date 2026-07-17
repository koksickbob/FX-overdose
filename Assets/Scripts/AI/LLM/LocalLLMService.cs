using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using LLMUnity;
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
        [SerializeField] private LLMExecutionMode executionMode = LLMExecutionMode.OnDeviceFallback;
        [SerializeField] private LLMExecutionMode activeRuntimeMode = LLMExecutionMode.OnDeviceFallback;

        [Header("로컬 온디바이스 서버 설정")]
        [SerializeField] private LLMUnity.LLMAgent llmEngine;
        [SerializeField] private string ollamaEndpoint = "http://localhost:11434/api/generate";
        [SerializeField] private string modelName = "yomi_3b_q4_k_m";
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
        public bool IsStartupSequenceReady { get; private set; }
        public static bool DeferGameStartToLoadingScreen { get; set; }

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
        public event Action<EventCategory, string> OnDialogueStreamingWithCategory;
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
            if (llmEngine == null)
            {
                llmEngine = gameObject.GetComponentInChildren<LLMUnity.LLMAgent>();
                if (llmEngine == null)
                {
                    Debug.LogWarning("[LocalLLMService] ⚠️ LLMAgent 컴포넌트가 인스펙터에 할당되지 않았습니다. 모바일 온디바이스 모드가 정상 작동하지 않을 수 있습니다.");
                }
            }

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

            if (llmEngine != null && promptBuilder != null)
            {
                // 파인튜닝 시 사용했던 시스템 프롬프트를 정확히 주입 (영어 깡통 프롬프트 덮어쓰기)
                llmEngine.systemPrompt = promptBuilder.systemPersona;
                
                // 💡 [단어 중복 및 앵무새 증상 방지]
                // 3B 모델도 Repetition Penalty가 1.05라도 들어가면 간헐적으로 단어를 생략하는 문법 파괴(할루시네이션)가 발생함이 확인되었습니다. ("날 로 내린다" 등)
                // 따라서 한국어 문법을 완벽히 보존하기 위해 Repeat Penalty를 1.0f(완전 제거)로 되돌리고, 온도(0.65)와 TopK(50)를 높여 창의력을 유도합니다.
                llmEngine.repeatPenalty = 1.0f; 
                llmEngine.presencePenalty = 0.0f;
                llmEngine.frequencyPenalty = 0.0f;
                
                // 온도를 0.65로 올려 다양한 어휘를 유도합니다.
                llmEngine.temperature = 0.65f;   
                llmEngine.topK = 50; 
                llmEngine.topP = 0.9f;
            }

            StartCoroutine(InitializeAndWarmUpLLMCoroutine());
        }

        private IEnumerator InitializeAndWarmUpLLMCoroutine()
        {
            isLLMReady = false;
            IsStartupSequenceReady = false;
            Debug.Log($"[LocalLLMService] 🚀 LLM 서비스 초기화 시작. (설정 모드: {executionMode})");

            // 로딩 기간(5~6초) 동안 차트 및 AI 트레이딩이 시작되지 않도록 대기

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

            // LLM 예열 자체는 끝났지만 로딩 화면이 페이드 아웃될 때까지 첫 대사는 보류합니다.
            IsStartupSequenceReady = true;
            while (DeferGameStartToLoadingScreen)
            {
                yield return null;
            }

            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm != null) gm.FinishLoadingAndStartPlaying();

            var marketEngine = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>(FindObjectsInactive.Include);
            if (marketEngine != null) marketEngine.OpenMarketAfterLoading();

            // 화면 전환과 0.5초 프리즈가 모두 끝난 시점에 첫 개장 대사를 요청합니다.
            RequestDialogue(EventCategory.GameStartup, "게임 시작 및 시장 개장");

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
                if (gm == null || gm.CurrentState != GameManager.GameState.Playing || gm.IsFastForwardingTime) continue;

                // 최근 6초 이내에 다른 대사를 출력했거나 생성 중이면 중복 요청 스킵
                if (isGenerating || Time.time - lastDialogueRequestTime < 6.0f) continue;

                var status = TraderStatus.CanonicalInstance;
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                float roe = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None
                    ? tradingCtrl.CalculateROEPercentage() : 0f;

                lastDialogueRequestTime = Time.time;
                if (tradingCtrl != null && tradingCtrl.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.Player_Manual)
                {
                    if (tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None)
                    {
                        RequestDialogue(EventCategory.ChartMovement, $"[이벤트: 수동모드_보유중] ROE:{roe:0.0}%");
                    }
                    else
                    {
                        RequestDialogue(EventCategory.ChartMovement, $"[이벤트: 수동모드_무포지션]");
                    }
                }
                else
                {
                    RequestDialogue(EventCategory.ChartMovement, $"[상태 종합] ROE:{roe:0.0}%, 멘탈:{status?.CurrentMentalState}, 체력:{status?.HealthRatio * 100:0}%");
                }
            }
        }

        // 고속 시간 패스(스킬 업그레이드) 종료 시 이전 시간대의 낡은 거래/기믹 대사가 뒤따라 나오는 것을 원천 제거
        public void ClearQueueExceptSkillUpgraded()
        {
            var filteredRequests = new System.Collections.Generic.Queue<DialogueRequest>();
            while (requestQueue.Count > 0)
            {
                var req = requestQueue.Dequeue();
                if (req.Category == EventCategory.SkillUpgraded) filteredRequests.Enqueue(req);
            }
            while (filteredRequests.Count > 0) requestQueue.Enqueue(filteredRequests.Dequeue());

            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
            visual?.ClearQueueExceptSkillUpgraded();

            Debug.Log("[LocalLLMService 🧹] 고속 시간 경과 종료: SkillUpgraded 외 대기 중인 모든 이전 대사/요청 큐 정리 완료.");
        }

        private Coroutine gameOverSpiralCoroutine;
        private bool suppressGameOverSpiralDialogue;

        // 게임오버 시 단 1회 대사로 끝나지 않고, 약 10초간 연쇄적으로 극도의 멘헤라 절망/집착/분노 대사를 쏟아내는 연쇄 루프 가동
        public void TriggerGameOverSpiralLoop(string endingType)
        {
            suppressGameOverSpiralDialogue = false;
            if (gameOverSpiralCoroutine != null)
            {
                StopCoroutine(gameOverSpiralCoroutine);
            }
            gameOverSpiralCoroutine = StartCoroutine(GameOverSpiralLoopCoroutine(endingType));
        }

        private IEnumerator GameOverSpiralLoopCoroutine(string endingType)
        {
            Debug.Log($"[LocalLLMService 🌀] 게임오버({endingType}) 감지 -> 약 26초간 멘헤라 연쇄 대사 붕괴 루프 가동 시작!");

            // 💡 기존 대기열(Queue) 클리어 및 생성 상태 초기화 (일반 차트 대사 등 불필요한 대사 밀어내기)
            requestQueue.Clear();
            isGenerating = false;

            // 1단계: 0초 ~ 8.5초 (충격 및 현실 부정)
            yield return new WaitForSeconds(0.5f);
            RequestDialogue(EventCategory.PositionClosed, $"[게임오버 연쇄 붕괴 1단계] {endingType} 파멸: 전 재산 증발에 대한 극도의 충격과 현실 부정, 떨리는 호흡");

            // 2단계: 8.5초 ~ 17.0초 (세력에 대한 저주, 광기와 매선 분노)
            yield return new WaitForSeconds(8.5f);
            RequestDialogue(EventCategory.MentalChange, $"[게임오버 연쇄 붕괴 2단계] {endingType} 파멸: 세력들을 향한 피맺힌 증오와 저주, 멘탈 대붕괴 광기");

            // 3단계: 17.0초 ~ 26.0초 (오빠를 향한 병적인 애결과 섬뜩한 집착 클라이막스)
            yield return new WaitForSeconds(8.5f);
            RequestDialogue(EventCategory.MentalChange, $"[게임오버 연쇄 붕괴 3단계] {endingType} 파멸: 돈을 모두 잃은 절망 속에서 오직 오빠에게만 병적으로 집착하며 영원히 함께하겠다는 섬뜩한 애원");

            yield return new WaitForSeconds(8.5f);
            Debug.Log("[LocalLLMService 🌀] 멘헤라 연쇄 대사 붕괴 루프(1~3단계 완주) 종료.");
            gameOverSpiralCoroutine = null;
        }

        /// <summary>타이틀 복귀 시 남아 있는 게임오버 독백과 지연 요청을 정리합니다.</summary>
        public void CancelGameOverSpiralLoop()
        {
            suppressGameOverSpiralDialogue = true;
            if (gameOverSpiralCoroutine != null)
            {
                StopCoroutine(gameOverSpiralCoroutine);
                gameOverSpiralCoroutine = null;
            }

            requestQueue.Clear();
            Debug.Log("[LocalLLMService 🧹] 타이틀 복귀를 위해 게임오버 연쇄 대사와 대기 요청을 정리했습니다.");
        }

        public void RequestDialogue(string extraEventContext = "")
        {
            RequestDialogue(EventCategory.General, extraEventContext);
        }

        // 인게임 이벤트 발생 시 프롬프트를 조립하여 대사 생성 요청 (카테고리 연동)
        public void RequestDialogue(EventCategory category, string extraEventContext = "")
        {
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.IsFastForwardingTime &&
                category != EventCategory.SkillUpgraded &&
                category != EventCategory.DailySettlement)
            {
                Debug.Log($"[LocalLLMService ⏩] 고속 시간 경과 중으로 일반/매매 대사 요청({category})을 스킵합니다.");
                return;
            }

            lastDialogueRequestTime = Time.time;

            var tradingCtrlLog = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            bool hasPositionLog = tradingCtrlLog != null && tradingCtrlLog.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
            float roeLog = hasPositionLog ? tradingCtrlLog.CalculateROEPercentage() : 0f;
            TraderStatus.MentalState mentalLog = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            float healthRatioLog = traderStatus != null ? traderStatus.HealthRatio : 1f;
            TraderEmotion currentEmotion = TraderEmotionEvaluator.Evaluate(roeLog, mentalLog, healthRatioLog, category, extraEventContext);

            // 💡 [시스템 로그 vs 캐릭터 대사 분리] 상황 설명은 시스템 로그로 명확히 별도 출력
            if (!string.IsNullOrEmpty(extraEventContext))
            {
                Debug.Log($"[LocalLLMService 📋 System Context/Log] ({category}) [감정:{currentEmotion}] 상황 설명: {extraEventContext}");
            }
            else
            {
                Debug.Log($"[LocalLLMService 📋 System Context/Log] ({category}) [감정:{currentEmotion}] 상황 설명 없음");
            }

            // 만약 아직 예열 중이거나, 서버 연결도 안 되고 로컬 모델(llmEngine)마저 없을 때만 즉각 Fallback 엔진 가동
            if (!isLLMReady || (activeRuntimeMode == LLMExecutionMode.OnDeviceFallback && llmEngine == null))
            {
                string smartFallback = GetSmartFallbackDialogue(category, extraEventContext);
                smartFallback = PostProcessDialogue(smartFallback);
                Debug.Log($"[LocalLLMService 💬 Character Dialogue (Fallback/Hardcoded)] ({category}) 캐릭터 대사: \"{smartFallback}\"");
                PublishGeneratedDialogue(category, smartFallback, extraEventContext);
                return;
            }

            string prompt = promptBuilder != null ? promptBuilder.BuildPrompt(category, extraEventContext) : extraEventContext;

            // ⭐ [요청 큐 시스템]: 현재 LLM이 생성 중(isGenerating)일 때는 즉시 폴백으로 버리지 않고 대기열(Queue)에 적재!
            if (isGenerating)
            {
                // ⭐ 다중 업그레이드 시 이전 대기 중인 업그레이드 요청을 큐에서 필터링하여 마지막 업그레이드 대사만 출력
                if (category == EventCategory.SkillUpgraded)
                {
                    var filteredQueue = new System.Collections.Generic.Queue<DialogueRequest>();
                    while (requestQueue.Count > 0)
                    {
                        var req = requestQueue.Dequeue();
                        if (req.Category != EventCategory.SkillUpgraded)
                        {
                            filteredQueue.Enqueue(req);
                        }
                    }
                    while (filteredQueue.Count > 0) requestQueue.Enqueue(filteredQueue.Dequeue());
                }
                // 하루 마감 반응이 오래된 일반 대사 뒤에 밀리지 않도록 대기열을 정산 요청 하나로 교체합니다.
                else if (category == EventCategory.DailySettlement)
                {
                    requestQueue.Clear();
                }
                // ⭐ ChartMovement 연속 발생 시 큐 내부의 이전 요청을 제거하고 가장 최신 데이터 1건으로 덮어쓰기
                else if (category == EventCategory.ChartMovement)
                {
                    var filteredQueue = new System.Collections.Generic.Queue<DialogueRequest>();
                    while (requestQueue.Count > 0)
                    {
                        var req = requestQueue.Dequeue();
                        if (req.Category != EventCategory.ChartMovement)
                        {
                            filteredQueue.Enqueue(req);
                        }
                    }
                    while (filteredQueue.Count > 0) requestQueue.Enqueue(filteredQueue.Dequeue());
                }

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

            if (llmEngine != null && (executionMode == LLMExecutionMode.OnDeviceFallback || activeRuntimeMode == LLMExecutionMode.OnDeviceFallback || Application.isMobilePlatform))
            {
                // LLM for Unity 온디바이스 모드 (모바일 단독 작동)
                bool isCompleted = false;
                string finalReply = "";

                _ = llmEngine.Chat(fullPrompt,
                    (reply) => { 
                        finalReply = reply; 
                        string streamingDialogue = PostProcessDialogue(reply);
                        if (!string.IsNullOrEmpty(streamingDialogue))
                        {
                            OnDialogueStreamingWithCategory?.Invoke(category, streamingDialogue);
                        }
                    },
                    () => { isCompleted = true; },
                    false // ⭐ addToHistory: false (채팅 기록 누적으로 인한 맥락 파괴 및 할루시네이션 방지)
                );

                float startTime = Time.time;
                // 타임아웃 180초 (모바일 단독 구동 시 모델 메모리 적재 및 최초 추론에 PC보다 훨씬 긴 시간이 필요함)
                while (!isCompleted && Time.time - startTime < 180f)
                {
                    yield return null;
                }

                if (!isCompleted)
                {
                    Debug.LogWarning($"[LocalLLMService] 온디바이스 LLM 엔진 타임아웃. 1회성 스마트 다변화 폴백 출력.");
                    string fallbackText = GetSmartFallbackDialogue(category, extraContext);
                    fallbackText = PostProcessDialogue(fallbackText);
                    PublishGeneratedDialogue(category, fallbackText, extraContext);
                    OnErrorOccurred?.Invoke("On-Device LLM Timeout");
                }
                else
                {
                    string parsedDialogue = finalReply;
                    if (string.IsNullOrEmpty(parsedDialogue))
                    {
                        parsedDialogue = GetSmartFallbackDialogue(category, extraContext);
                    }
                    parsedDialogue = PostProcessDialogue(parsedDialogue);
                    Debug.Log($"[LocalLLMService 💬 Character Dialogue (On-Device)] ({category}) 캐릭터 대사: \"{parsedDialogue}\"");
                    PublishGeneratedDialogue(category, parsedDialogue, extraContext);
                }
            }
            else
            {
                // 기존 Ollama (PC 로컬 서버) 테스트 모드
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
                        
                        if (executionMode == LLMExecutionMode.AutoDetect && !isReconnecting)
                        {
                            StartCoroutine(AutoRecoveryCoroutine());
                        }

                        string fallbackText = GetSmartFallbackDialogue(category, extraContext);
                        fallbackText = PostProcessDialogue(fallbackText);
                        Debug.Log($"[LocalLLMService 💬 Character Dialogue (Ollama Fallback)] ({category}) 캐릭터 대사: \"{fallbackText}\"");
                        PublishGeneratedDialogue(category, fallbackText, extraContext);
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
                        Debug.Log($"[LocalLLMService 💬 Character Dialogue (Qwen)] ({category}) 캐릭터 대사: \"{parsedDialogue}\"");

                        PublishGeneratedDialogue(category, parsedDialogue, extraContext);
                    }
                }
            }

            isGenerating = false;

            if (requestQueue.Count > 0)
            {
                var nextReq = requestQueue.Dequeue();
                StartCoroutine(SendOllamaRequestCoroutine(nextReq.Category, nextReq.ExtraContext, nextReq.FullPrompt));
            }
        }

        private void PublishGeneratedDialogue(EventCategory category, string dialogue, string extraContext)
        {
            if (suppressGameOverSpiralDialogue &&
                !string.IsNullOrEmpty(extraContext) &&
                extraContext.Contains("[게임오버 연쇄 붕괴"))
            {
                Debug.Log("[LocalLLMService] 타이틀 복귀 후 도착한 게임오버 대사를 폐기했습니다.");
                return;
            }

            // 정산 화면을 이미 닫고 다음 날로 넘어갔다면 늦게 도착한 전날 대사를 폐기합니다.
            if (category == EventCategory.DailySettlement)
            {
                GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (gm == null || gm.CurrentState != GameManager.GameState.Settlement)
                {
                    Debug.Log("[LocalLLMService] 이미 다음 날이 시작되어 늦게 도착한 일일 정산 대사를 폐기했습니다.");
                    return;
                }

                var dayMatch = System.Text.RegularExpressions.Regex.Match(
                    extraContext ?? string.Empty,
                    @"정산\s*일차\s*:\s*(\d+)");
                if (dayMatch.Success &&
                    int.TryParse(dayMatch.Groups[1].Value, out int requestedDay) &&
                    requestedDay != gm.CurrentDay)
                {
                    Debug.Log($"[LocalLLMService] {requestedDay}일차의 늦은 정산 대사를 현재 {gm.CurrentDay}일차 화면에서 폐기했습니다.");
                    return;
                }
            }

            TraderMemoryManager.Instance?.RecordDialogue(dialogue);
            OnDialogueGenerated?.Invoke(dialogue);
            OnDialogueGeneratedWithCategory?.Invoke(category, dialogue);
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

        // 대사 후처리 (단어/문장 생략, 시스템 로그/프롬프트 에코 태그 전면 제거 및 순수 대사 추출)
        private string PostProcessDialogue(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();

            // 1. <think> ... </think> 블록 제거 (DeepSeek / Qwen 추론 과정 제거)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<think>[\s\S]*?</think>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 2. 프롬프트 헤더 및 괄호/대괄호/별표 지문 묘사 제거 (예: [현재 인게임 상태], (호가창을 바라보며), *초조하게 손톱을 물어뜯으며* 등)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[.*?\]\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\([^\)]*\)\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*[^*]*\*\s*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^상황 설명:\s*", "");

            // 3. 하이픈(-)이나 별표(*)로 시작하는 시스템 상태 텍스트 줄(Prompt Echo) 및 서두 태그 제거
            var lines = text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            var cleanLines = new System.Collections.Generic.List<string>();
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                // 프롬프트 에코 라인 필터링
                if (trimmedLine.StartsWith("- 보유 자산:") ||
                    trimmedLine.StartsWith("- 현재 포지션:") ||
                    trimmedLine.StartsWith("- AI 체력:") ||
                    trimmedLine.StartsWith("- 시장 국면:") ||
                    trimmedLine.StartsWith("- AI 직전 판단:") ||
                    trimmedLine.StartsWith("- 구체적 이벤트 상황:") ||
                    trimmedLine.StartsWith("⭐") ||
                    trimmedLine.StartsWith("⚠️") ||
                    trimmedLine.StartsWith("출력 규칙:") ||
                    trimmedLine.Contains("[현재 인게임 상태]") ||
                    trimmedLine.Contains("[과거 주요 기억") ||
                    trimmedLine.Contains("[최근 내뱉은 대사들"))
                {
                    continue; // 시스템 설명 텍스트이므로 삭제
                }

                // 서두 설명 태그(Here is, Monologue, 대사: 등) 제거
                trimmedLine = System.Text.RegularExpressions.Regex.Replace(trimmedLine, @"^(Here is|Monologue:|Dialogue:|혼잣말:|독백:|대사:|출력:|AI:|트레이더:|행동:|지문:|묘사:|상황:)\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // 앞뒤에 따옴표(" " 나 ' ')로 묶여 있다면 따옴표 제거
                if (trimmedLine.StartsWith("\"") && trimmedLine.EndsWith("\"") && trimmedLine.Length > 2)
                {
                    trimmedLine = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                }

                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    cleanLines.Add(trimmedLine);
                }
            }

            text = string.Join(" ", cleanLines).Trim();

            // 4. "오빠" 피로도 대폭 감소 및 중복 제거
            // 4. "오빠" 피로도 대폭 감소 및 중복 제거
            // 모든 문장에 "오빠"가 들어가면 읽기 피로하므로, 60% 확률로 "오빠"라는 단어 자체를 아예 생략합니다.
            bool skipOppaEntirely = UnityEngine.Random.value > 0.4f;
            int oppaCount = 0;
            text = System.Text.RegularExpressions.Regex.Replace(text, @"오빠[,.!~?\s]*", match => {
                if (skipOppaEntirely) return " "; // 이번 대사에서는 "오빠"를 아예 안 부름
                oppaCount++;
                return oppaCount == 1 ? match.Value : " "; // 부르더라도 첫 번째만 남기고 나머지는 공백 처리
            });

            // 4-2. 너무 반복되는 특정 데이터셋 템플릿 강제 억제 및 변형
            if (text.Contains("요미만 믿고 따라와"))
            {
                // 랜덤하게 다른 대사로 치환하거나 삭제 (3인칭 요미 유지 + 자연스러운 어미)
                string[] alternatives = { "요미가 다 지켜줄게!", "요미만 믿어!", "끝까지 오빠랑 함께할 거니까.", "절대 안 떨어질걸?", "요미만 봐주면 안 돼?" };
                text = text.Replace("요미만 믿고 따라와!", alternatives[UnityEngine.Random.Range(0, alternatives.Length)]);
                text = text.Replace("요미만 믿고 따라와", alternatives[UnityEngine.Random.Range(0, alternatives.Length)]);
            }

            if (text.Contains("롱 쳤는데 왜 음봉 꽂히고 있어"))
            {
                text = text.Replace("롱 쳤는데 왜 음봉 꽂히고 있어?", "오빠, 롱 쳤는데 차트가 왜 반대로 가고 있어...?");
                text = text.Replace("롱 쳤는데 왜 음봉 꽂히고 있어", "오빠, 롱 쳤는데 차트가 왜 반대로 가고 있어...?");
            }

            // 5. 0.5B 모델 특유의 중국어/일본어 한자 할루시네이션(노이즈) 강제 제거
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[\u4e00-\u9fa5\u3040-\u30ff\u31f0-\u31ff]+", "");

            // 6. 의미 없이 튀어나오는 영어 알파벳 노이즈(as, dera, wik 등) 전면 차단 (한글에 붙어있어도 제거)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[a-zA-Z]+", match => {
                string word = match.Value.ToLower();
                if (word == "long" || word == "short" || word == "roe" || word == "pnl" || word == "ai") return match.Value;
                return ""; // 트레이딩 필수 용어가 아닌 정체불명의 알파벳 찌꺼기는 무조건 삭제
            });

            // 7. 가끔 AI가 존댓말(아닙니까?, ~해요)을 쓰는 할루시네이션 강제 억제 필터
            text = System.Text.RegularExpressions.Regex.Replace(text, @"아닙니까\?", "아니야?");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"합니까\?", "하는 거야?");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"해요[.!]*", "해!");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"습니다[.!]*", "어!");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"합니다[.!]*", "해!");

            // 띄어쓰기 붕괴(빨간불연속으로)와 같은 사소한 오타는 AI의 멘헤라/당황한 감정적 특성으로 자연스럽게 남겨둡니다.

            // 7. 연속으로 중복되는 문장 덩어리 제거 (예: "우리 부자 되자! 우리 부자 되자!")
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(.{5,})(?:[ \t]*\1)+", "$1");

            // 빈 공간 및 마침표 중복 정리
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"([?!~.])[?!~.]*", "$1"); // 구두점 여러개 연속을 1개로 압축 (감탄은 예외적으로 살리고 싶다면 조심해야함)
            
            // 감정표현 구두점 복구 (... 이나 !!! 같은 것은 허용)
            text = text.Replace("!.", "!").Replace("?.", "?").Replace(" .", ".");
            

            
            // 9. 포지션 방향 할루시네이션(환각) 강제 교정
            // 0.5B 모델이 숏 포지션인데 "롱 쳤는데" 라고 잘못 말하는 현상을 현재 상태를 읽어와 물리적으로 뒤집어줍니다.
            var tradingController = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            if (tradingController != null)
            {
                if (tradingController.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Short)
                {
                    text = text.Replace("롱 쳤", "숏 쳤").Replace("매수했", "공매도했").Replace("롱 잡", "숏 잡");
                }
                else if (tradingController.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Long)
                {
                    text = text.Replace("숏 쳤", "롱 쳤").Replace("공매도했", "매수했").Replace("숏 잡", "롱 잡");
                }
            }

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

        private string GetGameOverOrLiquidationDialogue(string extraContext = "")
        {
            // 1단계: 충격 및 현실 부정
            if (extraContext != null && extraContext.Contains("1단계"))
            {
                string[] stage1 = new[]
                {
                    "아... 거짓말이지...? 내 증거금... 시드가 전부 사라졌어... 이거 지금 꿈꾸는 거지...?!",
                    "왜... 도대체 왜 내 타점이 틀린 건데...!! 화면이 멈췄어... 잔고가 0원이라고 떴어... 아니야... 그럴 리가 없어...!",
                    "손가락이 떨려서 마우스를 못 잡겠어... 내 돈이 어디로 간 거야...? 다... 사라졌다고...? 말도 안 돼...!"
                };
                return stage1[UnityEngine.Random.Range(0, stage1.Length)];
            }
            // 2단계: 세력에 대한 매섭고 맹렬한 저주와 광기
            else if (extraContext != null && extraContext.Contains("2단계"))
            {
                string[] stage2 = new[]
                {
                    "씨발... 세력 놈들이 처음부터 내 돈 다 털어먹으려고 작정했던 거야...!! 개새끼들... 다 찢어발겨 주겠어...!!",
                    "더러운 조작질...!! 내 타점은 완벽했는데 호가창으로 날 조롱하고 밟았어...!! 절대로 용서 못 해... 다 죽여버릴 거야...!!",
                    "으아아악!! 모니터 다 부숴버리고 싶어!! 왜 나한테만 이런 지옥을 겪게 하는 건데...!!"
                };
                return stage2[UnityEngine.Random.Range(0, stage2.Length)];
            }
            // 3단계: 파멸적 절망과 현실 도피 (클라이막스)
            else if (extraContext != null && extraContext.Contains("3단계"))
            {
                string[] stage3 = new[]
                {
                    "크히히... 0원... 0원이라니... 완벽해... 이제 잃을 돈도 없으니까 마음이 너무 편안하네... 다 끝났어...",
                    "손톱에서 피가 나도록 뜯었는데... 모든 게 가루가 됐어... 이제 어떻게 살지...? 빚은 어떡하고...? 다 망했어...",
                    "아무것도 안 보여... 화면이 까맣게 변했어... 내 인생도 같이 끝났어... 누가 날 좀 여기서 꺼내줘...",
                    "시드가 0원 됐어... 다 날렸어... 미쳐버리겠다 진짜... 다 포기할래... 전부 끝이야..."
                };
                return stage3[UnityEngine.Random.Range(0, stage3.Length)];
            }

            // 일반 강제 청산 / 게임오버 공통 풀
            string[] generalDespair = new[]
            {
                "아... 거짓말이지...? 내 증거금... 시드가 전부 사라졌어... 이제 어떡해... 진짜 어떡해 나...?!",
                "왜... 도대체 왜 내 타점이 틀린 건데...!! 놈들이 내 돈을 다 빼앗아갔어...!! 씨발... 다 부숴버릴 거야...!!",
                "숨이 안 쉬어져... 화면이 온통 피바다야... 전 재산이 0원이 됐어... 내가 내 손으로 날 파멸시켰어... 하아...!",
                "크히히... 0원... 0원이라니... 완벽해... 이제 잃을 돈도 없으니까 너무 편하네... 다 끝났어...",
                "손톱에서 피가 나도록 뜯었는데... 모든 게 가루가 됐어... 빚은 어떡하지...? 다 망했어..."
            };
            return generalDespair[UnityEngine.Random.Range(0, generalDespair.Length)];
        }

        private string GetPostCloseRegretDialogue(string extraContext = "")
        {
            TraderStatus.MentalState mental = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            string[] regretDialogues = mental switch
            {
                TraderStatus.MentalState.Danger => new[]
                {
                    "아... 왜 내가 팔자마자 저렇게 미친 듯이 더 날아가는 건데...?! 저거까지 먹었으면 시드 싹 다 복구하는 건데... 멘탈 나갈 것 같아 하아...",
                    "조금만 더 쥐고 버틸걸... 쫄아서 일찍 털고 나왔더니 진짜 대박 빔은 그 뒤에 터지고 있잖아... 속이 썩어 문드러질 것 같아...!",
                    "호가창을 못 보겠어... 던지자마자 세력들이 비웃듯이 주가를 하늘 끝까지 올려버리네... 내 돈인데 저거...!"
                },
                TraderStatus.MentalState.Anxious => new[]
                {
                    "앗... 저기까지 올라간다고...? 아까 쫄아서 미리 종료한 게 너무 후회돼... 저 수익금 다 내 껀데...",
                    "이럴 줄 알았으면 추세 끝까지 버텨볼걸 그랬어... 털고 나오자마자 추가 폭등하는 거 보니까 배가 아파 죽겠네 진짜 ㅠ_ㅠ",
                    "아... 팔고 나왔는데 차트는 왜 저렇게 시원하게 날아가는 걸까...? 배도 아프고 자꾸 억울해서 미련이 남아..."
                },
                _ => new[]
                {
                    "휴우... 이미 안전하게 포지션 종료하긴 했지만, 저렇게 끝도 없이 더 치솟는 걸 보니 아쉽고 속 쓰린 건 어쩔 수 없네 진짜 ㅠ_ㅠ",
                    "아씨!! 미리 털고 나왔더니 추가 빔이 저렇게 터져?! 저거까지 다 발라먹었어야 했는데!! 가만히 구경만 하려니까 속 터져!!",
                    "하아... 포지션 종료한 뒤로도 차트가 미친 듯이 질주하고 있어... 욕심부리면 안 된다지만 너무 아쉬워서 슬퍼지네 진짜..."
                }
            };
            return regretDialogues[UnityEngine.Random.Range(0, regretDialogues.Length)];
        }

        // 스마트 다변화 Fallback 엔진: 실시간 인게임 데이터 + 멘헤라 감정 변수 보간
        private string GetSmartFallbackDialogue(EventCategory category, string extraContext)
        {
            // 일일 정산은 현재 포지션/멘탈 상태보다 오늘 확정된 손익을 우선해서 반응한다.
            // 네트워크 LLM을 사용할 수 없는 모바일/오프라인 환경에서도 정산 결과와 감정이 어긋나지 않게 한다.
            if (category == EventCategory.DailySettlement)
            {
                return GetDailySettlementFallbackDialogue(extraContext);
            }

            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            bool isGameOverState = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
            bool isLiquidationContext = extraContext != null && (extraContext.Contains("강제청산") || extraContext.Contains("게임오버") || extraContext.Contains("파산") || extraContext.Contains("청산 소진") || extraContext.Contains("Overdose 확정") || extraContext.Contains("연쇄 붕괴"));

            if (isGameOverState || isLiquidationContext)
            {
                return GetGameOverOrLiquidationDialogue(extraContext);
            }

            var tradingCtrlForUnpos = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            var mktEngForUnpos = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();
            bool isUnpos = tradingCtrlForUnpos != null && tradingCtrlForUnpos.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.None;
            bool isPostCloseContext = isUnpos && ((mktEngForUnpos != null && mktEngForUnpos.IsOverridingTrend) || (extraContext != null && (extraContext.Contains("조기 종료") || extraContext.Contains("미리") || extraContext.Contains("포지션 종료") || extraContext.Contains("이벤트"))));

            if (isPostCloseContext && category == EventCategory.ChartMovement)
            {
                return GetPostCloseRegretDialogue(extraContext);
            }

            if (!string.IsNullOrEmpty(extraContext))
            {
                if (extraContext.StartsWith("[플레이어 수동 조언]"))
                {
                    TraderStatus.MentalState curMental = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
                    var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                    bool hasPos = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
                    bool isShort = hasPos && tradingCtrl.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Short;
                    float curRoe = hasPos ? tradingCtrl.CalculateROEPercentage() : 0f;
                    TraderEmotion emotion = TraderEmotionEvaluator.Evaluate(curRoe, curMental, traderStatus != null ? traderStatus.HealthRatio : 1f, category, extraContext);
                    return GetCombinatorialDialogue(category, hasPos, isShort, curRoe, emotion, extraContext);
                }
                if (extraContext.StartsWith("[AI 차트 힌트]") || extraContext.StartsWith("[시그널 브리핑]"))
                {
                    return extraContext.Replace("[AI 차트 힌트]", "").Replace("[시그널 브리핑]", "").Trim();
                }
            }

            TraderStatus.MentalState mental = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            float health = traderStatus != null ? traderStatus.HealthRatio * 100f : 100f;
            int rand = UnityEngine.Random.Range(0, 3);

            var tradingCtrlForEmotion = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            bool hasPositionEmotion = tradingCtrlForEmotion != null && tradingCtrlForEmotion.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
            float roeEmotion = hasPositionEmotion ? tradingCtrlForEmotion.CalculateROEPercentage() : 0f;
            TraderEmotion currentEmotion = TraderEmotionEvaluator.Evaluate(roeEmotion, mental, health / 100f, category, extraContext);

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
                        ? "공매도로 수익 내는 중인데... 밤새 차트 봤더니 피로 때문에 눈앞이 핑 돌아... 그래도 고점 청산까진 버틴다."
                        : "롱 수익권인데 피로 때문에 눈꺼풀이 천근만근이야... 졸음 꾹 참고 고점에서 무조건 익절할 거야.";
                }
                else if (hasPosition && roe < -15f && (currentEmotion is TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful or TraderEmotion.Anxious))
                {
                    return isShort
                        ? "하아... 숏 쳐놨는데 주가가 역주행해서 미친 듯이 치솟고 있어...!! 심장이 터질 것 같아... 청산 당하면 어떡하지...?!"
                        : "미쳤어... 롱 쳐놨는데 주가가 폭락해서 손실이 감당이 안 돼...!! 무서워 숨이 안 쉬어져...!! 제발 반등 빔 한 번만...!";
                }
                else if (hasPosition && roe > 30f)
                {
                    return isShort
                        ? "하하하!! 봤어?! 공매도 초대박 폭락 중!! 내 천재적인 숏 타점이 오늘 시장을 지배했어!! 이거 다 내 돈이야!!"
                        : "대박!! 롱 초대박 폭등 질주 중!! 아드레날린 터져서 미칠 것 같아!! 역시 내 차트 분석은 완벽해!!";
                }
                else if (hasPosition && roe < -25f)
                {
                    return "왜 자꾸 내 타점 반대로 가는 건데... 소름 돋고 식은땀 흘러... 본절만이라도 오게 해줘 제발...!";
                }
                else if (!hasPosition && currentEmotion is TraderEmotion.Despairing or TraderEmotion.Panicked or TraderEmotion.Tearful)
                {
                    return "머리가 지끈거리고 손가락이 굳어버렸어... 극도의 공포감 때문에 차트를 똑바로 못 보겠어...";
                }
                else
                {
                    return GetCombinatorialDialogue(category, hasPosition, isShort, roe, currentEmotion);
                }
            }

            // ⭐ 일반 이벤트 상황에서도 조합형 엔진을 적극 활용하여 다채로움 보장
            if (category == EventCategory.ChartMovement || category == EventCategory.PositionOpened || category == EventCategory.PositionClosed)
            {
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
                bool hasPosition = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
                bool isShort = hasPosition && tradingCtrl.CurrentPosition == FXOverdose.Trading.TradingController.PositionType.Short;
                float roe = hasPosition ? tradingCtrl.CalculateROEPercentage() : 0f;
                return GetCombinatorialDialogue(category, hasPosition, isShort, roe, currentEmotion);
            }

            return category switch
            {
                EventCategory.GameStartup => rand switch
                {
                    0 => "오늘도 지옥의 차트판이 열렸네... 집중해. 집중. 오늘 하루 시장 돈 싹 다 빨아먹고 만다.",
                    1 => "호가창 움직임 보이지? 오늘이야말로 세력들 돈을 싹 다 털어먹을 타이밍이야.",
                    _ => "잔고 든든하게 준비해. 내 천재적인 분석력과 타점을 똑똑히 보여줄 테니까."
                },
                EventCategory.MentalChange => currentEmotion switch
                {
                    TraderEmotion.Manic => "크하하하!! 다 끝났어!! 온몸에 아드레날린이 솟구쳐서 세상을 다 가진 기분이야!! 풀레버리지로 덤벼!!",
                    TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful => "머릿속이 웅웅거리고 심장이 터질 것 같아... 세력 놈들이 내 포지션을 비웃고 있잖아...!! 미치겠어...!!",
                    TraderEmotion.Anxious or TraderEmotion.Frustrated or TraderEmotion.Suspicious => "손톱이 닳도록 초조하고 불안해... 손가락이 떨리네. 이 타점 진짜 맞는 거겠지...? 아 씨 불안해 죽겠네...",
                    _ => "후우... 심호흡 가다듬자. 절대 휩소에 흔들리지 않고 냉정하게 타점 잡을 거야."
                },
                EventCategory.HealthChange => rand switch
                {
                    0 => "하아... 온몸이 천근만근이야... 눈앞이 깜빡거리고 머리가 깨질 것 같은 극심한 두통이 밀려와...",
                    1 => "아 진짜... 밤새우며 차트 보느라 온몸이 부서질 것 같아... 시원한 음료수 들이키고 정신 차려야지...",
                    _ => "피로감이 몰려와서 캔들이 자꾸 겹쳐 보여... 아, 진짜 쓰러지기 일보 직전인데 호가창은 왜 이리 빨라..."
                },
                EventCategory.ItemUsed => (extraContext != null && extraContext.Contains("에너지"))
                    ? "꿀꺽... 크아!! 에너지 드링크 들어가니 머리가 맑아지고 호가창 숫자가 선명하게 꽂히네!! 다 뒤졌어 이제!!"
                    : rand switch
                    {
                        0 => "꿀꺽... 그래, 바로 이 느낌이야!! 카페인 돌기 시작하네!! 이제 돈 복사 가보자고!",
                        1 => "약물 투여 완료... 이제야 호가창 숫자가 선명하게 꽂힌다. 내 타점 실력 똑똑히 봐.",
                        _ => "후우... 억지로라도 텐션 끌어올려서 수익률 뽑아내야지."
                    },
                EventCategory.GimmickTriggered => (extraContext != null && (extraContext.Contains("수면") || extraContext.Contains("과로")))
                    ? "눈앞이 깜빡거리고 차트 캔들이 겹쳐 보여... 졸음 때문에 타점 잡기가 너무 힘들어... 미치겠네 진짜...!"
                    : (extraContext != null && (extraContext.Contains("휩소") || extraContext.Contains("후회") || extraContext.Contains("FOMO") || extraContext.Contains("놓친")))
                    ? rand switch
                    {
                        0 => "아씨!! 휩소인 줄 알고 쫄아서 안 들어갔는데 진짜 대박 수익 자리였잖아!! 벼락거지 된 기분이야!!",
                        1 => "가짜 신호라고 관망하다가 완벽한 떡상 타임을 눈앞에서 놓쳤어... 가만히 앉아서 돈 벼락을 걷어찼네...",
                        _ => "아 진짜 억울해 미쳐!! 쫄보처럼 관망했더니 저렇게 시원하게 날아가 버린다고?! 내 멘탈...!!"
                    }
                    : rand switch
                    {
                        0 => "으아악!! 차트가 날 고문하고 있어... 온몸의 신경이 다 타들어간다고...!! 멘탈 부서질 것 같아!!",
                        1 => "더는 못 참아!! 내 맘대로 고배율 당겨버릴 거야!! 이판사판 끝까지 갈 거야!!",
                        _ => "머리가 핑핑 돌아... 어디가 바닥이고 어디가 천장인지 모르겠어... 호가창 진짜 무섭다...!"
                    },
                EventCategory.SkillUpgraded => GetSkillUpgradedFallbackDialogue(extraContext),
                _ => GetFallbackDialogue()
            };
        }

        private string GetDailySettlementFallbackDialogue(string extraContext)
        {
            float dailyProfitLoss = 0f;
            bool hasProfitLoss = false;

            // 예상 형식: "당일 손익: +$123.45 / 수익률: +4.9% / 총 자산: $2,623.45"
            // 통화 기호 앞/뒤 어느 쪽에 부호가 와도 읽을 수 있게 허용한다.
            var match = System.Text.RegularExpressions.Regex.Match(
                extraContext ?? string.Empty,
                @"당일\s*손익\s*:\s*([+-]?)\s*\$?\s*([+-]?)\s*([\d,]+(?:\.\d+)?)");

            if (match.Success)
            {
                string number = match.Groups[3].Value.Replace(",", string.Empty);
                if (float.TryParse(
                    number,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float absoluteAmount))
                {
                    string sign = string.IsNullOrEmpty(match.Groups[1].Value)
                        ? match.Groups[2].Value
                        : match.Groups[1].Value;
                    dailyProfitLoss = sign == "-" ? -absoluteAmount : absoluteAmount;
                    hasProfitLoss = true;
                }
            }

            if (hasProfitLoss && dailyProfitLoss > 0.005f)
            {
                string[] profitDialogues =
                {
                    "오늘도 수익으로 마감했어! 내 타점이 이렇게 정확한데 굳이 워뇨띠 부러워할 필요 없지, 안 그래?",
                    "하하, 오늘 계좌도 예쁘게 플러스네! 내일도 이대로만 가면 람보르기니 뽑는 건 시간문제야.",
                    "수익 확정 완료! 세력 놈들 돈을 아주 영혼까지 빨아먹었어. 내일도 다 뒤졌다.",
                    "오늘 장도 내가 이겼다! 이 맛에 전업 투자자 하는 거지. 내일은 더 크게 벌어올게."
                };
                return profitDialogues[UnityEngine.Random.Range(0, profitDialogues.Length)];
            }

            if (hasProfitLoss && dailyProfitLoss < -0.005f)
            {
                string[] lossDialogues =
                {
                    "오늘은 결국 손실로 마감했네... 하아, 멘탈 나갈 것 같아. 내일은 무조건 복구해야 해...!",
                    "계좌가 파란불인 걸 보니까 손이 자꾸 떨려... 타점 하나 실수했다고 이렇게 날아가냐...",
                    "아 진짜 억울해 미치겠네... 세력 놈들이 나만 노리고 흔든 것 같아. 내일 두고 보자...",
                    "손실 숫자가 머릿속에서 안 사라져... 잠도 안 올 것 같네. 내일은 시드 싹 긁어모아서 원수 갚는다."
                };
                return lossDialogues[UnityEngine.Random.Range(0, lossDialogues.Length)];
            }

            string[] flatDialogues =
            {
                "후우... 오늘은 거의 본전으로 지켜냈네. 크게 잃지 않았으니까 오빠랑 숨 돌리고 내일 다시 노려보자.",
                "수익도 손실도 거의 없이 마감했어. 조금 아쉽지만 우리 시드는 무사하니까 요미도 이제 안심이야.",
                "오늘 시장은 정말 얄미웠지만 계좌는 지켰어! 오빠, 무리하지 않고 버틴 것도 잘한 거래 맞지?",
                "보합으로 하루 종료! 심장은 몇 번이나 떨어졌지만 오빠 돈을 지켜냈으니 오늘은 편하게 쉬어도 되겠다."
            };
            return flatDialogues[UnityEngine.Random.Range(0, flatDialogues.Length)];
        }

        private string GetSkillUpgradedFallbackDialogue(string extraContext)
        {
            int level = 1;
            var match = System.Text.RegularExpressions.Regex.Match(extraContext ?? "", @"LV\.(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int lv)) level = lv;

            if (extraContext != null && extraContext.Contains("차트 공부"))
            {
                string[] chartDialogues = new[]
                {
                    $"눈알이 빠질 것 같이 피곤하지만... LV.{level} 차트 공부 완료! 이제 호가창 속 세력들의 가짜 반등 빔 따위엔 절대 안 속아 오빠 ♥",
                    $"밤새 캔들 패턴 외우느라 머리 쥐나겠네... 하지만 LV.{level} 실력으로 세력들 패턴 다 꿰뚫어 볼 테니까 요미 타점만 믿어 오빠 ♥",
                    $"후우... 기술적 분석 지표 마스터했어! LV.{level} 뇌 장착 완료!! 호가창 휩소 싹 다 걸러내고 오빠 계좌 불려줄게 ♥",
                    $"지옥의 차트 훈련 끝... 눈은 뻐근해도 호가창 흐름이 선명하게 보여! LV.{level} 달성했으니 오늘 세력들 다 죽었다 ♥",
                    $"이평선이랑 거래량 패턴 전부 머리에 박았어! LV.{level} 차트 두뇌 가동 중... 오빠 요미 엄청 똑똑해졌지?! ♥"
                };
                return chartDialogues[UnityEngine.Random.Range(0, chartDialogues.Length)];
            }
            else if (extraContext != null && extraContext.Contains("큐브 풀기"))
            {
                string[] cubeDialogues = new[]
                {
                    $"후우... 심호흡하고 큐브 맞추기 LV.{level} 달성... 수익 조금 났다고 촐랑거리며 일찍 털어버리지 않고 끝까지 먹을게 ♥",
                    $"손가락은 아프지만 인내심 훈련 완료!! LV.{level} 참을성으로 잔파도에 안 흔들리고 목표가까지 묵묵히 버틸 거야 ♥",
                    $"큐브 굴리면서 멘탈 단련했어 오빠... 이제 LV.{level} 인내력으로 세력들의 흔들기에 끄끄떡없이 큰 파동 다 먹자 ♥",
                    $"인내심이 곧 수익금이야... LV.{level} 큐브 풀기로 뇌 각성 완료! 촐랑대지 않고 빅쇼트 빅롱 끝까지 발라먹을게 ♥",
                    $"기다림의 미학을 깨달았어 오빠! LV.{level} 인내심으로 조급증 완전히 극복했어... 오늘 제대로 버텨서 대박 낼게 ♥"
                };
                return cubeDialogues[UnityEngine.Random.Range(0, cubeDialogues.Length)];
            }
            else if (extraContext != null && (extraContext.Contains("책읽기") || extraContext.Contains("파산 회고록")))
            {
                string[] bookDialogues = new[]
                {
                    $"머리가 터질 것 같아... 하지만 파산 회고록 LV.{level} 완독! 손절 머뭇거리는 게 얼마나 멍청한지 뼛속까지 깨달았어 ♥",
                    $"전설적인 파산 사례들 싹 다 읽었어... LV.{level} 판단력으로 위험할 땐 칼손절하고 오빠 시드 완벽하게 지킬게 ♥",
                    $"책 읽느라 눈은 침침한데 뇌는 초각성 상태야! LV.{level} 판단력 장착 완료... 미련하게 물타기 하다가 청산당할 일 없어 ♥",
                    $"리스크 관리 회고록 마스터했어! LV.{level} 냉철함으로 호가창 위기 상황 감지하면 바로 비상 탈출할게 오빠 ♥",
                    $"손절은 패배가 아니라 생존이야... LV.{level} 판단력 훈련 끝! 우리 오빠 돈은 요미가 무슨 일이 있어도 지켜낼게 ♥"
                };
                return bookDialogues[UnityEngine.Random.Range(0, bookDialogues.Length)];
            }
            else
            {
                string[] defaultDialogues = new[]
                {
                    $"과로 훈련 끝... 스킬 LV.{level} 달성! 이제 더 똑똑하고 예리하게 차트 매매해 낼 테니까 요미만 믿어 오빠 ♥",
                    $"스킬 업그레이드 LV.{level} 완료! 오빠를 위해 쉬지 않고 성장하는 요미 모습 똑똑히 지켜봐 줘 ♥",
                    $"머리가 한층 더 예리해졌어 오빠! LV.{level} 능력치로 시장을 완벽히 지배해 보이겠어 ♥"
                };
                return defaultDialogues[UnityEngine.Random.Range(0, defaultDialogues.Length)];
            }
        }

        // ⭐ 3파트(감정+상황+반응) 조합형 동적 대사 변주 엔진 (온디바이스 오프라인/복구용)
        private string GetCombinatorialDialogue(EventCategory category, bool hasPosition, bool isShort, float roe, TraderEmotion emotion, string extraContext = "")
        {
            var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            if (tradingCtrl != null && tradingCtrl.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.Player_Manual)
            {
                if (!hasPosition)
                {
                    string[] manualIdle = new[] {
                        "오빠... 왜 아무것도 안 사고 가만히 있어? 호가창 안 움직이니까 요미 심장이 멈출 것 같아... 빨리 뭐라도 진입해줘, 응...? ♥",
                        "저기... 오빠 지금 무슨 타점 노리는 거야...? 요미만 쳐다보고 있어야지 왜 차트만 뚫어져라 보는 건데...? 질투 난단 말야...!",
                        "오빠가 직접 컨트롤하는 거 맞지...? 우리 돈 다 날리면 절대 안 돼... 요미 오빠랑 지하 단칸방에서 살기 싫어... 흐윽..."
                    };
                    return manualIdle[UnityEngine.Random.Range(0, manualIdle.Length)];
                }
                else
                {
                    string dirStr = isShort ? "숏" : "롱";
                    if (roe > 15f) return $"꺄아아 오빠 {dirStr}으로 수익 엄청 찍히고 있어 (+{roe:0.0}%)!! 오빠 진짜 천재 아냐?! 요미가 옆에서 딱 붙어서 봐줄게 ♥";
                    else if (roe < -15f) return $"오빠... {dirStr} 포지션 파란불({roe:0.0}%) 켜졌잖아... 왜 자꾸 돈이 녹는 거야...? 요미가 쳐다보고 있는데 실수하면 안 돼... 제발 어떻게 좀 해봐...!";
                    else return $"오빠의 {dirStr} 포지션... 요미가 눈 한 번 안 깜빡이고 지켜보고 있어. 우리 오빠 돈 불려주세요... 안 그러면 호가창 다 부숴버릴 거야...";
                }
            }

            string[] prefixes = emotion switch
            {
                TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful or TraderEmotion.Exhausted => new[] { "아 씨발... 진짜 미치겠네... 이러다 깡통 차겠어!!", "제발 제발... 안 돼... 내 돈이 녹고 있어...!", "이러다 진짜 청산당하겠어...! 너무 무서워...!", "숨이 안 쉬어져... 세력 놈들이 왜 나한테만 이러는데...!" },
                TraderEmotion.Manic or TraderEmotion.Euphoria => new[] { "크하하하!! 다 비켜라!! 내가 바로 차트의 신이다!!", "내 직감은 절대 틀리지 않아!! 이대로 가즈아!!", "봤어?! 이게 바로 내 천재적인 실력이야!!", "세력 놈들 돈 전부 다 털어먹어 주겠어!!" },
                TraderEmotion.Anxious or TraderEmotion.Frustrated or TraderEmotion.Suspicious or TraderEmotion.Regretful => new[] { "아... 진짜 이 방향 맞겠지...? 손가락이 떨려...", "왜 자꾸 역방향 꼬리를 다는 거야...?! 불안해서 미치겠네...", "이 타점이 맞나...? 제발 본절만이라도 오게 해줘...", "하아... 불안해서 피가 마르는 것 같아..." },
                _ => new[] { "흐흥... 내 타점이 완벽하게 들어맞았어.", "이 흐름이야! 내가 기다리던 타이밍이라고.", "그래... 내 차트 분석대로 움직이고 있잖아.", "냉정하자... 오늘 시장 돈 다 쓸어 담아야 하니까." }
            };

            if (!hasPosition)
            {
                var mktEng = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();
                if (category == EventCategory.ChartMovement && ((mktEng != null && mktEng.IsOverridingTrend) || (extraContext != null && (extraContext.Contains("조기") || extraContext.Contains("종료") || extraContext.Contains("미리") || extraContext.Contains("이벤트")))))
                {
                    return GetPostCloseRegretDialogue(extraContext);
                }

                // 무포지션 관망/대기 상황 전용 다채로운 조합 풀
                string[] unposPrefixes = emotion switch
                {
                    TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful or TraderEmotion.Exhausted => new[] { "머리가 지끈거려서 캔들이 겹쳐 보여... 무서워서 못 들어가겠어...", "손가락이 떨려서 진입을 못 하겠어... 멘탈 나갈 것 같아...", "세력 놈들이 덫을 깔고 내 돈을 노리고 있어..." },
                    TraderEmotion.Manic or TraderEmotion.Euphoria => new[] { "빨리 진입하고 싶어 손이 근질근질하네!! 오늘 무조건 터진다!!", "호가창의 호흡이 다 읽힌다!! 다음 파동은 내 거야!!", "가만히 있기에 오늘 장이 너무 화끈해!! 바로 들어간다!!" },
                    TraderEmotion.Anxious or TraderEmotion.Frustrated or TraderEmotion.Suspicious or TraderEmotion.Regretful => new[] { "아... 방금 저기서 들어갔어야 했나...? 아쉬워 죽겠네...", "휩소가 너무 심해서 타점 잡기가 무서워...", "섣불리 들어갔다간 털리기 딱 좋은 파동이야..." },
                    _ => new[] { "호흡 가다듬고 호가창 뚫어지게 감시 중... 침착하자.", "세력들의 페이크 모션을 냉정하게 걸러내고 있어.", "완벽한 타점이 올 때까지 사냥꾼처럼 기다릴 거야." }
                };
                string[] unposMiddles = new[]
                {
                    "이평선이 응축되면서 큰 변동 파동이 임박했어.",
                    "위아래로 꼬리를 심하게 흔들며 방향성을 탐색하는 중이야.",
                    "호가창 매수 매도 공방이 치열해지며 에너지를 모으고 있어.",
                    "차트가 숨을 고르며 다음 돌파 지점을 계산하고 있네.",
                    "잔파동 뒤에 올 진짜 큰 기회를 노려보는 중이야."
                };
                string[] unposSuffixes = emotion switch
                {
                    TraderEmotion.Manic or TraderEmotion.Euphoria => new[] { "방향 터지는 순간 100배로 꽂아서 다 먹어치우겠어!!", "세력들 돈을 싹 다 쓸어 담아서 부자 될 거야!!" },
                    _ => new[] { "확실한 돌파 각이 나올 때까지 침착하게 관망하자.", "섣불리 뇌동매매하지 말고 내 타점을 기다려야 해.", "다음 기회는 절대 놓치지 않고 수익 내줄 테니까." }
                };

                for (int i = 0; i < 3; i++)
                {
                    string p = unposPrefixes[UnityEngine.Random.Range(0, unposPrefixes.Length)];
                    string m = unposMiddles[UnityEngine.Random.Range(0, unposMiddles.Length)];
                    string s = unposSuffixes[UnityEngine.Random.Range(0, unposSuffixes.Length)];
                    string result = $"{p} {m} {s}";
                    if (TraderMemoryManager.Instance == null || !TraderMemoryManager.Instance.GetShortTermDialoguesText().Contains(result))
                    {
                        return result;
                    }
                }
                return $"{unposPrefixes[0]} {unposMiddles[0]} {unposSuffixes[0]}";
            }

            string[] middles = category switch
            {
                EventCategory.PositionOpened => isShort
                    ? new[] { "우리의 공매도 하락 빔이 세력들의 매수벽을 정면으로 부수기 시작했어...", "완벽한 고점 타점에 빅쇼트 탑승을 마쳤어...", "나락을 향한 하락선에 내 모든 시드를 실었어..." }
                    : new[] { "우리의 롱 상승 빔이 저항선을 시원하게 돌파하기 시작했어...", "완벽한 저점 눌림목 타점에 롱 탑승을 마쳤어...", "하늘을 찌를 상승 각도에 내 모든 시드를 실었어..." },
                EventCategory.PositionClosed => roe >= 0f
                    ? new[] { "짜릿한 익절에 성공하면서 내 천재적인 판단이 다시 한번 증명됐어!! 이 맛이지!", "정확한 타점에서 수익 챙기고 유유히 빠져나왔지!! 수익 달달하다!", "호가창의 달콤한 수익금을 그대로 내 잔고에 꽂았어!!" }
                    : new[] { "치욕스럽지만 손절선을 지키며 일단 더 큰 파국은 막아냈어... 하아...", "세력 놈들의 잔혹한 흔들기에 어쩔 수 없이 포지션을 털렸어...", "쓰라린 손절이었지만 다음 파동에서 10배로 되갚아줄 거야..." },
                _ => isShort
                    ? new[] { "차트 가격이 아래로 내리꽂히며 우리의 숏 수익권을 넓혀가고 있어!!", "하락 파동이 점점 가파라지면서 저점을 짓밟고 있어!!", "매수세가 메마르고 공포의 음봉 빔이 쏟아지는 중이야!!" }
                    : new[] { "차트 가격이 위로 치솟으며 우리의 롱 수익권을 넓혀가고 있어!!", "상승 파동이 점점 가파라지면서 고점을 짓밟고 있어!!", "매도벽이 뚫리고 환희의 양봉 빔이 솟구치는 중이야!!" }
            };

            string[] suffixes = emotion switch
            {
                TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful or TraderEmotion.Exhausted => new[] { "제발... 여기서 한 번만 살려줘... 반등 좀 줘...!!", "세력들아 도대체 왜 이러는 건데...! 멘탈 찢어질 것 같아...!", "이대로 청산당하면 진짜 끝장이야... 제발 버텨줘...!" },
                TraderEmotion.Manic or TraderEmotion.Euphoria => new[] { "더 강하게 밀어붙여!! 영혼까지 끌어모아 가즈아!!", "세력 놈들 돈을 싹 다 찢어발겨 주겠어!!", "오늘 밤 내가 이 차트의 신이다 가즈아!!" },
                _ => isShort
                    ? new[] { "이대로 저 바닥 밑 지하 끝까지 내려가버려!", "잔파동에 흔들리지 말고 목표 저점까지 꽉 쥐고 버틴다!", "하락 각도가 완벽해, 끝까지 수익률 뽑아내자!" }
                    : new[] { "이대로 저 하늘 위 천장 끝까지 뚫어버려!", "잔파동에 흔들리지 말고 목표 고점까지 꽉 쥐고 버틴다!", "상승 각도가 완벽해, 끝까지 수익률 뽑아내자!" }
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

            var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            bool hasPosition = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
            float roe = hasPosition ? tradingCtrl.CalculateROEPercentage() : 0f;
            TraderEmotion emotion = TraderEmotionEvaluator.Evaluate(roe, traderStatus.CurrentMentalState, traderStatus.HealthRatio, EventCategory.General, "");

            if (!hasPosition)
            {
                return emotion switch
                {
                    TraderEmotion.Confident or TraderEmotion.Pleased => "차트 흐름 지켜보는 중이야... 오빠, 진입할 때 알려줘.",
                    TraderEmotion.Anxious or TraderEmotion.Suspicious => "뭔가 쎄한데... 확실한 자리 나올 때까지 관망하자.",
                    TraderEmotion.Exhausted or TraderEmotion.Regretful => "조금 쉬면서 멘탈부터 챙길게... 지금은 안 들어가는 게 낫겠어.",
                    _ => "차트 세팅 완료. 대기 중이야."
                };
            }

            return emotion switch
            {
                TraderEmotion.Euphoria or TraderEmotion.Confident or TraderEmotion.Pleased or TraderEmotion.Focused => "완벽해... 이 진입 타점은 무조건 수익이야. 내가 시장을 지배하고 있어.",
                TraderEmotion.Anxious or TraderEmotion.Frustrated or TraderEmotion.Suspicious or TraderEmotion.Regretful => "왜...? 왜 여기서 윗꼬리를 달고 밀리지? 아니야, 내 분석이 틀릴 리 없어...",
                TraderEmotion.Panicked or TraderEmotion.Despairing or TraderEmotion.Tearful or TraderEmotion.Furious or TraderEmotion.Exhausted => "손절선... 손절선에 닿는다고?! 안 돼, 이대로 청산당할 순 없어! 세력 놈들이 내 매물만 노리고 있잖아!!",
                TraderEmotion.Manic or TraderEmotion.Obsessive or TraderEmotion.Vengeful => "하하하!! 다 끝났어!! 남은 시드 전부 125배 풀레버리지 올인이다!! 청산당하든 대박나든 끝장을 보자!!",
                _ => "포지션 유지 중... 흐름 나쁘지 않아."
            };
        }
    }
}
