using System;
using UnityEngine;
using FXOverdose.DatingSim.Core;

namespace FXOverdose.DatingSim.YomiRoom
{
    public enum YomiRoomState
    {
        Idle,           // 대기 상태 (UI 입력 대기)
        FreeChatting,   // 자유 대화 중 (LLM 연동 중)
        Resting,        // 휴식 중 (연출 중)
        Transitioning   // 월드맵/트레이딩 등 씬 이동 중
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

        // --- 외부(UI 버튼 등) 호출용 public 인터페이스 ---
        
        public void TryStartFreeChat()
        {
            if (currentState != YomiRoomState.Idle) return;
            
            if (DatingTimeManager.Instance != null && DatingTimeManager.Instance.TryConsumeTimeSlot(freeChatTimeSlotCost))
            {
                ChangeState(YomiRoomState.FreeChatting);
                // TODO: DatingSimLLMController(LLM) 호출 로직 연동
            }
            else
            {
                OnActionFailed?.Invoke();
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
            // TODO: LoadingSceneManager 호출하여 WorldMapScene 비동기 로드
        }

        public void StartTrading()
        {
            if (currentState != YomiRoomState.Idle) return;
            ChangeState(YomiRoomState.Transitioning);
            // TODO: LoadingSceneManager 호출하여 TradingScene 비동기 로드
        }

        private void ChangeState(YomiRoomState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }
    }
}
