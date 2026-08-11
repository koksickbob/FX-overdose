using System;
using System.Threading.Tasks;
using UnityEngine;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.Dialogue;
using FXOverdose.UI;
using UnityEngine.SceneManagement;

namespace FXOverdose.DatingSim.YomiRoom
{
    public enum YomiRoomState
    {
        Idle,           // 대기 상태 (UI 입력 대기)
        Chatting,       // 대화 패널을 연 상태
        Responding,     // 요미의 응답을 만드는 중
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

        // 자리 표시자 응답이 즉시 반환되어 '생각 중' 연출이 보이지 않는 것을 막는 최소 지연입니다.
        private const int ResponseDelayMilliseconds = 600;

        // 대화 응답 공급자. 대체 대화 시스템이 확정되면 이 참조만 교체하면 됩니다.
        private IYomiDialogueProvider dialogueProvider = PlaceholderDialogueProvider.Default;

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

        private void Start()
        {
            ChangeState(YomiRoomState.Idle);
        }

        /// <summary>대체 대화 시스템을 주입합니다. 미주입 시 자리 표시자가 사용됩니다.</summary>
        public void SetDialogueProvider(IYomiDialogueProvider provider)
        {
            dialogueProvider = provider ?? PlaceholderDialogueProvider.Default;
        }

        // --- 외부(UI 버튼 등) 호출용 public 인터페이스 ---

        public void TryStartChat()
        {
            if (currentState != YomiRoomState.Idle) return;

            // TODO(P2): 대체 대화 시스템 확정 시 시간 슬롯 소모 비용을 다시 붙입니다.
            //           자리 표시자 응답만 나오는 현재는 자원을 소모시키지 않습니다.
            ChangeState(YomiRoomState.Chatting);
        }

        public async void ProcessUserChatInput(string userMessage)
        {
            if (currentState != YomiRoomState.Chatting) return;

            ChangeState(YomiRoomState.Responding);

            string response = dialogueProvider.GetResponse(userMessage);
            await Task.Delay(ResponseDelayMilliseconds);

            // 씬 전환 등으로 매니저가 파괴된 뒤 응답이 도착하는 경우를 방어합니다.
            if (this == null) return;

            OnChatUpdated?.Invoke(userMessage, response);

            // TODO(P2): 실제 대화가 성립할 때만 호감도를 지급합니다.
            //           자리 표시자 응답으로 호감도를 올리면 밸런스가 왜곡되므로 보류합니다.

            // 응답 완료 후 다시 대화 대기 상태로 복귀
            ChangeState(YomiRoomState.Chatting);
        }

        public void CloseChat()
        {
            if (currentState == YomiRoomState.Chatting)
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
            LoadingScreenController.TargetSceneToLoad = "WorldMapScene";
            SceneManager.LoadScene("LoadingScene");
        }

        public void StartTrading()
        {
            if (currentState != YomiRoomState.Idle) return;
            ChangeState(YomiRoomState.Transitioning);
            LoadingScreenController.TargetSceneToLoad = "GameScene";
            SceneManager.LoadScene("LoadingScene");
        }

        /// <summary>대화 UI 또는 방 내부 연출이 종료된 뒤 탐색 상태로 복귀합니다.</summary>
        public void CompleteRoomInteraction()
        {
            if (currentState == YomiRoomState.Chatting || currentState == YomiRoomState.Resting)
                ChangeState(YomiRoomState.Idle);
        }

        private void ChangeState(YomiRoomState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }
    }
}
