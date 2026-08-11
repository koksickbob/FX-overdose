using System;
using UnityEngine;
using FXOverdose.DatingSim.Core;
using FXOverdose.UI;
using UnityEngine.SceneManagement;
using FXOverdose.DatingSim.LLM;
using FXOverdose.DatingSim.LLM.Memory;

namespace FXOverdose.DatingSim.YomiRoom
{
    public enum YomiRoomState
    {
        LLMLoading,     // LLM 모델 로딩 대기 중 (추가)
        Idle,           // 대기 상태 (UI 입력 대기)
        FreeChatting,   // 자유 대화 중 (LLM 연동 중)
        Resting,        // 휴식 중 (연출 중)
        Transitioning,  // 월드맵/트레이딩 등 씬 이동 중
        LLMProcessing   // LLM 응답 대기 중
    }

    public class YomiRoomManager : MonoBehaviour
    {
        public static YomiRoomManager Instance { get; private set; }

        [Header("Room Settings")]
        [SerializeField, Tooltip("휴식 시 회복되는 미연시 체력량")]
        private int restStaminaRecoverAmount = 10;
        
        [SerializeField, Tooltip("휴식 시 소모되는 시간 슬롯")]
        private int restTimeSlotCost = 1;

        [SerializeField, Tooltip("자유 대화 시 소모되는 시간 슬롯")]
        private int freeChatTimeSlotCost = 1;

        // 상태 캡슐화 (외부 직접 수정 차단)
        private YomiRoomState currentState = YomiRoomState.Idle;
        public YomiRoomState CurrentState => currentState;

        // UI 갱신용 이벤트
        public event Action<YomiRoomState> OnStateChanged;
        public event Action<string, string> OnChatUpdated; // 유저 메시지, 요미 응답
        public event Action OnActionFailed; // 시간/체력 부족 등으로 행동 실패 시

        private void Awake()
        {
            if (Instance == null) 
            {
                Instance = this;
            }
            else 
            {
                Destroy(gameObject);
            }
        }

        private async void Start()
        {
            // 시작 시 모델이 준비될 때까지 대기
            ChangeState(YomiRoomState.LLMLoading);
            
            if (DatingSimLLMController.Instance != null)
            {
                await DatingSimLLMController.Instance.WaitUntilReadyAsync();
            }

            // 로딩 완료 후 대기 상태로 전환
            ChangeState(YomiRoomState.Idle);
        }

        // --- 외부(UI 버튼 등) 호출용 public 인터페이스 ---
        
        public void TryStartFreeChat()
        {
            if (currentState != YomiRoomState.Idle) return;
            
            if (DatingTimeManager.Instance != null && DatingTimeManager.Instance.TryConsumeTimeSlot(freeChatTimeSlotCost))
            {
                ChangeState(YomiRoomState.FreeChatting);
            }
            else
            {
                OnActionFailed?.Invoke();
            }
        }

        public async void ProcessUserChatInput(string userMessage)
        {
            if (currentState != YomiRoomState.FreeChatting) return;

            ChangeState(YomiRoomState.LLMProcessing);

            if (DatingSimLLMController.Instance != null)
            {
                string response = await DatingSimLLMController.Instance.GenerateChatAsync(userMessage, MemoryTopic.Daily);
                
                OnChatUpdated?.Invoke(userMessage, response);
                
                // 대화 성공 시 임시로 호감도 소폭 증가 연동
                DatingTimeManager.Instance.ModifyAffection(UnityEngine.Random.Range(1, 4));
            }
            else
            {
                Debug.LogError("[YomiRoom] DatingSimLLMController.Instance가 없습니다.");
                OnChatUpdated?.Invoke(userMessage, "(시스템 오류: LLM 응답 실패)");
            }

            // 응답 완료 후 다시 채팅 대기 상태로 복귀
            ChangeState(YomiRoomState.FreeChatting);
        }

        public void CloseFreeChat()
        {
            if (currentState == YomiRoomState.FreeChatting)
            {
                ChangeState(YomiRoomState.Idle);
            }
        }

        public void TryRest()
        {
            if (currentState != YomiRoomState.Idle) return;
            
            if (DatingTimeManager.Instance != null && DatingTimeManager.Instance.TryConsumeTimeSlot(restTimeSlotCost))
            {
                ChangeState(YomiRoomState.Resting);
                DatingTimeManager.Instance.RecoverStamina(restStaminaRecoverAmount);
                // 임시 복귀 로직 (향후 연출 코루틴 등으로 대체 가능)
                ChangeState(YomiRoomState.Idle);
            }
            else
            {
                OnActionFailed?.Invoke();
            }
        }

        public void MoveToWorldMap()
        {
            if (currentState != YomiRoomState.Idle) return;
            ChangeState(YomiRoomState.Transitioning);
            LoadingScreenController.RequireLLM = false;
            LoadingScreenController.TargetSceneToLoad = "WorldMapScene";
            SceneManager.LoadScene("LoadingScene");
        }

        public void StartTrading()
        {
            if (currentState != YomiRoomState.Idle) return;
            ChangeState(YomiRoomState.Transitioning);
            LoadingScreenController.RequireLLM = false;
            LoadingScreenController.TargetSceneToLoad = "GameScene";
            SceneManager.LoadScene("LoadingScene");
        }

        /// <summary>대화 UI 또는 방 내부 연출이 종료된 뒤 탐색 상태로 복귀합니다.</summary>
        public void CompleteRoomInteraction()
        {
            if (currentState == YomiRoomState.FreeChatting || currentState == YomiRoomState.Resting)
                ChangeState(YomiRoomState.Idle);
        }

        private void ChangeState(YomiRoomState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }
    }
}
