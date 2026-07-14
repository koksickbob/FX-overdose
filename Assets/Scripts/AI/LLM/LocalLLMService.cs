using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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

            // 만약 오프라인/모바일 온디바이스 모드이거나, 아직 예열 중/생성 중이면 즉각 Fallback 엔진 가동 (네트워크 HTTP 요청 없음!)
            if (activeRuntimeMode == LLMExecutionMode.OnDeviceFallback || !isLLMReady || isGenerating)
            {
                string smartFallback = GetSmartFallbackDialogue(category, extraEventContext);
                smartFallback = PostProcessDialogue(smartFallback);
                Debug.Log($"[LocalLLMService 💬 Character Dialogue (On-Device)] ({category}) 캐릭터 대사: \"{smartFallback}\"");
                OnDialogueGenerated?.Invoke(smartFallback);
                OnDialogueGeneratedWithCategory?.Invoke(category, smartFallback);
                return;
            }

            string prompt = promptBuilder != null ? promptBuilder.BuildPrompt(category, extraEventContext) : extraEventContext;
            StartCoroutine(SendOllamaRequestCoroutine(category, extraEventContext, prompt));
        }

        private IEnumerator SendOllamaRequestCoroutine(EventCategory category, string extraContext, string fullPrompt)
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
                    Debug.LogWarning($"[LocalLLMService] 온디바이스 서버 응답 실패 ({request.error}). 스마트 다변화 안전망 출력.");
                    if (executionMode == LLMExecutionMode.AutoDetect)
                    {
                        activeRuntimeMode = LLMExecutionMode.OnDeviceFallback;
                        Debug.Log("[LocalLLMService] 🔄 서버 응답 중단으로 인해 향후 대사 출력을 '온디바이스 Fallback 엔진(OnDeviceFallback)'으로 자동 전환합니다.");
                    }
                    string fallbackText = GetSmartFallbackDialogue(category, extraContext);
                    fallbackText = PostProcessDialogue(fallbackText);
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
                    Debug.Log($"[LocalLLMService 💬 Character Dialogue (Qwen)] ({category}) 캐릭터 대사: \"{parsedDialogue}\"");

                    OnDialogueGenerated?.Invoke(parsedDialogue);
                    OnDialogueGeneratedWithCategory?.Invoke(category, parsedDialogue);
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
                float roe = hasPosition ? tradingCtrl.CalculateROEPercentage() : 0f;

                if (hasPosition && roe > 15f && health < 40f)
                {
                    return "수익권 달리는 중인데... 극심한 피로 때문에 눈꺼풀이 천근만근이야. 졸음 쫓아내고 끝까지 익절하자...";
                }
                else if (hasPosition && roe < -15f && (mental == TraderStatus.MentalState.Danger || mental == TraderStatus.MentalState.Anxious))
                {
                    return "손실이 커지니까 심장이 미친 듯이 뛰고 호흡이 가빠져...! 세력들이 날 벼랑 끝으로 몰고 있어... 제발 살려줘...!";
                }
                else if (hasPosition && roe > 30f)
                {
                    return "초대박 질주 중!! 심장이 짜릿해서 터질 것 같아!! 내 천재적인 직감이 오늘 시장을 완벽히 지배했어!!";
                }
                else if (hasPosition && roe < -25f)
                {
                    return "왜 자꾸 내 판단 반대로 가는 건데... 온몸에 소름이 돋고 식은땀이 흘러... 제발 반등 빔 한 번만 나와줘...!";
                }
                else if (!hasPosition && mental == TraderStatus.MentalState.Danger)
                {
                    return "머리가 지끈거리고 손가락이 떨려... 극도의 공포감 때문에 호가창을 똑바로 볼 수가 없어. 휴식이 필요해...";
                }
                else if (hasPosition)
                {
                    return rand switch
                    {
                        0 => "포지션 방향은 맞는데 잔파동이 신경 쓰이네... 긴장 늦추지 말고 호가창 흐름 끝까지 주시하자.",
                        1 => "잔파동에 흔들리면 안 돼... 호흡 가다듬고 우리의 목표 고점까지 침착하게 들고 가자.",
                        _ => "머릿속 차트 계산은 완벽해... 신경이 날카로워졌지만 손익분기점 지키면서 냉정하게 대응할게."
                    };
                }
                else
                {
                    return rand switch
                    {
                        0 => "차트 흐름과 내 컨디션 조율 중... 확실한 돌파 각이 나올 때까지 숨죽이고 대기하자.",
                        1 => "피로감과 긴장감이 교차하네... 섣불리 뇌동매매하지 말고 빅쇼트 타점을 노리는 게 맞아.",
                        _ => "호가창 움직임 주시 중... 온 감각을 곤두세우고 있어. 다음 타점이 오늘을 결정지을 거야."
                    };
                }
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
                EventCategory.PositionOpened => (extraContext != null && extraContext.Contains("OVERDOSE"))
                    ? "하하하!! 다 끝났어!! 125배 풀레버리지 올인이다!! 청산당하든 대박나든 끝장을 보자!!"
                    : rand switch
                    {
                        0 => "좋아, 포지션 진입!! 이번 돌파는 무조건 진짜야, 가즈아!!",
                        1 => "탑승 완료... 손절선 따윈 필요 없어, 내 판단은 완벽하니까.",
                        _ => "심장 터질 것 같지만, 여기서 안 들어가면 평생 후회할 자리야!"
                    },
                EventCategory.PositionClosed => (extraContext != null && (extraContext.Contains("익절") || extraContext.Contains("수익")))
                    ? "포지션 익절 완료!! 봤어?! 이게 바로 내 천재적인 매매 실력이야!!"
                    : (extraContext != null && (extraContext.Contains("손절") || extraContext.Contains("충격")))
                    ? "...아니야, 이건 그냥 시장이 잠시 미친 거야!! 손절쳤지만 다음 타점에 10배로 복구한다!!"
                    : rand switch
                    {
                        0 => "포지션 정리 완료!! 호가창 다시 보면서 다음 기회를 노린다.",
                        1 => "청산 완료... 침착하게 다음 타점을 노리자.",
                        _ => "후후... 세력 놈들, 다음 판엔 내 시드 10배로 불려주지."
                    },
                EventCategory.ChartMovement => rand switch
                {
                    0 => "호가창 요동치는 거 봐... 짜릿해서 미칠 것 같아!!",
                    1 => "아 왜 윗꼬리 달고 내려오는데?! 털어먹으려고 작정한 거 맞지?!",
                    _ => "순항 중이야... 그래, 이대로 저 위 고점까지 단숨에 뚫어버려!!"
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
