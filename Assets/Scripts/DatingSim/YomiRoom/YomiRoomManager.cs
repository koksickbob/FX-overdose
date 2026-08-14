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

        // 자유 채팅은 시간 슬롯이 아니라 체력을 소모합니다. (2026-08-14 개편)
        [SerializeField, Tooltip("대화 1회에 소모되는 미연시 체력")]
        private int talkStaminaCost = 10;

        // 임계는 총획득 상한(+3)에 맞춥니다. 옛 값(4/7)은 상한이 12이던 시절의 것이라 지금은 도달 불가입니다.
        [SerializeField, Tooltip("힌트가 나오기 시작하는 당일 호감도 획득량")]
        private int hintThresholdTier1 = 2;

        [SerializeField, Tooltip("방향을 명시하는 힌트가 나오는 당일 호감도 획득량")]
        private int hintThresholdTier2 = 3;

        // 상태 캡슐화 (외부 직접 수정 차단)
        private YomiRoomState currentState = YomiRoomState.Idle;
        public YomiRoomState CurrentState => currentState;

        // 진행 중인 대화
        private TalkTopic activeTopic;
        private int activeNodeIndex = -1;
        private bool hasActiveTopic;

        public bool HasActiveTopic => hasActiveTopic;
        public TalkNode CurrentNode => activeTopic.Nodes[activeNodeIndex];

        // UI 갱신용 이벤트
        public event Action<YomiRoomState> OnStateChanged;
        public event Action<TalkNode> OnTalkNodeAdvanced;       // 요미 대사 + 선택지 표시
        public event Action<string> OnPlayerChoiceSpoken;       // 플레이어가 고른 대사를 로그에 남길 때
        public event Action<string> OnYomiReplied;              // 고른 선택지에 대한 요미의 즉답 (R-2)
        public event Action<string, string> OnTalkFinished;     // 마무리 대사, 힌트 대사(없으면 null)
        public event Action<string> OnYomiGreeted;              // 선제 발화
        public event Action<string> OnActionFailed; // 행동 실패 시 사유 문구를 전달합니다 (UI 표시용)

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

            // 방에 도착했다는 사실 자체를 기록으로 남깁니다. SaveGame이 현재 씬을 LastSceneName에 찍으므로
            // 이 한 번의 저장이 곧 복귀 지점 갱신입니다. 이게 없으면 아침에 방으로 나오자마자 종료한
            // 플레이어가 다음 접속에서 트레이딩 화면에 떨어집니다 — 매일 아침이 그 경우입니다. (F-11)
            FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();
        }

        // 요미의 방 씬을 에디터에서 단독 재생할 때 쓰는 임시 진행 데이터입니다.
        // SaveLoadManager는 TitleScene에만 있어서, 방 씬만 열면 Instance가 null입니다. (SV-C6)
        // 이게 없으면 대화는 되는데 힌트가 영영 안 나와 기능을 테스트할 수 없습니다.
        // 디스크에 기록되지 않으므로 실제 세이브를 건드릴 위험은 없습니다.
        private FXOverdose.Core.SaveData scratchData;
        private bool warnedScratch;

        /// <summary>
        /// 대화 진행이 기록될 데이터. 정상 플로우에서는 실제 세이브를, 단독 재생 시엔 임시본을 돌려줍니다.
        ///
        /// 이 클래스의 하루 게이트는 전부 <b>data.CurrentDay</b>(트레이딩 일차)를 기준으로 합니다.
        /// DatingDay와 항상 같다는 계약에 기대고 있습니다 — DatingTimeManager.SyncToNewDay 주석 참고. (F-9)
        /// </summary>
        private FXOverdose.Core.SaveData ProgressData
        {
            get
            {
                var live = FXOverdose.Core.SaveLoadManager.Instance?.CurrentData;
                if (live != null) return live;

                if (scratchData == null)
                {
                    scratchData = new FXOverdose.Core.SaveData();
                    // 방 씬 단독 재생에서도 일차가 있어야 힌트 판정이 돕니다.
                    scratchData.CurrentDay = DatingTimeManager.Instance != null
                        ? DatingTimeManager.Instance.CurrentDay
                        : 1;
                }

                if (!warnedScratch)
                {
                    warnedScratch = true;
                    Debug.LogWarning("[YomiRoomManager] 세이브 데이터가 없어 임시 진행 데이터로 동작합니다. " +
                                     "대화·힌트는 확인할 수 있지만 저장되지 않습니다. (씬 단독 재생 중으로 보입니다)");
                }
                return scratchData;
            }
        }

        // --- 외부(UI 버튼 등) 호출용 public 인터페이스 ---

        /// <summary>
        /// 대화를 시작합니다. 하루 1회 한도를 확인하고, 체력을 소모하고, 오늘 아직 안 쓴 토픽을 하나 뽑습니다.
        ///
        /// ⚠️ 체력 차감과 '사용됨' 기록을 <b>첫 노드 진입 전에</b> 끝냅니다.
        ///    대화 도중 호감도가 오를 때마다 자동 저장이 일어나기 때문에, 종료 시점에 기록하면
        ///    중간에 나갔다 들어와 같은 토픽을 다시 열어 호감도를 재획득할 수 있습니다. (TS1)
        ///    단 하루 1회 카운트만은 기획대로 <b>대화 종료 시점</b>에 소모합니다 — 대신 끝맺지 못한
        ///    대화의 잔존 기록(TalkActiveTopicId)도 오늘 몫으로 쳐서 강제 종료 우회를 막습니다.
        /// </summary>
        public bool TryStartTalk()
        {
            if (currentState != YomiRoomState.Idle) return false;

            var time = DatingTimeManager.Instance;
            if (time == null)
            {
                OnActionFailed?.Invoke("지금은 대화를 시작할 수 없어요.");
                return false;
            }

            var data = ProgressData;
            int today = data.CurrentDay;
            EnsureDailyTalkState(data, today);

            // 자유 채팅은 하루 1회. (2026-08-14 개편)
            if (data.TalkLastSessionEndDay == today || !string.IsNullOrEmpty(data.TalkActiveTopicId))
            {
                OnActionFailed?.Invoke("오늘은 이미 요미와 이야기를 나눴어요. 내일 다시 걸어보세요.");
                return false;
            }

            // ⚠️ 토픽 추첨을 체력 차감보다 먼저 합니다.
            //    해금 조건 때문에 후보가 비는 경우가 실제로 생기는데, 순서가 반대면 체력만 날아갑니다. (TS11)
            TalkTopic? picked = PickTopic(data);
            if (picked == null)
            {
                OnActionFailed?.Invoke("지금 시간대에 나눌 이야기가 없어요.");
                return false;
            }

            // 체력은 차감하기 전에 확인합니다. 아래에서 마커를 먼저 적기 때문에,
            // 여기서 걸러내지 않으면 대화도 못 하고 토픽만 소진되는 상태를 되돌려야 합니다.
            if (time.CurrentStamina < talkStaminaCost)
            {
                OnActionFailed?.Invoke($"체력이 부족해요. 대화에는 체력 {talkStaminaCost}이 필요해요.");
                return false;
            }

            activeTopic = picked.Value;
            activeNodeIndex = 0;
            hasActiveTopic = true;

            // ⚠️ 진행 마커를 체력 차감보다 <b>먼저</b> 기록합니다. (F-4)
            //    TryConsumeStamina가 그 자리에서 디스크에 저장하기 때문입니다. 순서가 반대면
            //    "체력만 깎이고 대화 흔적은 없는" 스냅샷이 남아, 껐다 켜서 토픽을 다시 굴릴 수 있습니다.
            //    TS1(여는 즉시 소비)이 메모리에서만 지켜지던 원인이 이 순서였습니다.
            if (!data.TalkTopicsUsedToday.Contains(activeTopic.Id))
                data.TalkTopicsUsedToday.Add(activeTopic.Id);
            if (!data.TalkTopicsSeenTotal.Contains(activeTopic.Id))
                data.TalkTopicsSeenTotal.Add(activeTopic.Id);

            data.TalkActiveTopicId = activeTopic.Id;
            data.TalkActiveNodeIndex = 0;

            time.TryConsumeStamina(talkStaminaCost); // 위에서 확인했으므로 반드시 성공하며, 이 호출이 마커까지 함께 저장합니다.

            ChangeState(YomiRoomState.Chatting);
            OnTalkNodeAdvanced?.Invoke(activeTopic.Nodes[0]);
            return true;
        }

        /// <summary>선택지를 고릅니다. 호감도를 더하고 다음 노드로 진행하거나 대화를 마칩니다.</summary>
        public void SelectChoice(int choiceIndex)
        {
            if (!hasActiveTopic || currentState != YomiRoomState.Chatting) return;

            TalkNode node = activeTopic.Nodes[activeNodeIndex];
            if (choiceIndex < 0 || choiceIndex >= node.Choices.Length) return;

            ChangeState(YomiRoomState.Responding);

            TalkChoice choice = node.Choices[choiceIndex];
            OnPlayerChoiceSpoken?.Invoke(choice.Text);

            // 고른 선택지에 요미가 바로 반응합니다. 이게 없으면 무엇을 고르든 다음 대사가 같아
            // 대화가 아니라 점수가 숨겨진 메뉴판으로 읽힙니다. (C-2)
            if (!string.IsNullOrEmpty(choice.Reply)) OnYomiReplied?.Invoke(choice.Reply);

            var data = ProgressData;
            if (data != null)
            {
                data.TalkChoiceHistory.Add($"{activeTopic.Id}:{activeNodeIndex}:{choiceIndex}");
                TrimChoiceHistory(data);
                data.TalkAffectionGainToday += choice.Affection;
            }

            if (choice.Affection != 0)
            {
                // 이 호출이 자동 저장을 일으킵니다. 위에서 세이브 데이터를 먼저 갱신해 둔 이유입니다.
                // 마이너스 선택지(요미의 아픈 곳을 외면)도 같은 경로로 내려갑니다. 0~100 클램프는 저쪽이 합니다.
                DatingTimeManager.Instance?.ModifyAffection(choice.Affection);
            }

            activeNodeIndex++;
            if (data != null) data.TalkActiveNodeIndex = activeNodeIndex;

            if (activeNodeIndex < activeTopic.Nodes.Length)
            {
                ChangeState(YomiRoomState.Chatting);
                OnTalkNodeAdvanced?.Invoke(activeTopic.Nodes[activeNodeIndex]);
                return;
            }

            FinishTalk(data);
        }

        /// <summary>
        /// 대화를 중간에 닫습니다. 진행 중이던 토픽은 재개하지 않습니다. (TS1)
        /// 중도 종료도 '대화 종료'이므로 하루 1회 카운트를 여기서 소모합니다.
        /// </summary>
        public void CloseTalk()
        {
            if (currentState != YomiRoomState.Chatting && currentState != YomiRoomState.Responding) return;

            hasActiveTopic = false;
            activeNodeIndex = -1;

            var data = ProgressData;
            if (data != null)
            {
                data.TalkActiveTopicId = "";
                data.TalkActiveNodeIndex = -1;
                data.TalkLastSessionEndDay = data.CurrentDay;
                // 하루 1회 한도는 이 값 하나에 달려 있습니다. 저장하지 않으면 타이틀로 나갔다 오는 것만으로
                // 그날 대화를 다시 할 수 있습니다. 종료는 저장까지가 한 동작입니다. (F-3)
                FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();
            }

            ChangeState(YomiRoomState.Idle);
        }

        /// <summary>
        /// 방에 들어왔을 때 요미가 먼저 건네는 인사. 하루 1회만 나옵니다. (TS3)
        /// 자원도 호감도도 소모/지급하지 않습니다 — 방을 드나들며 파밍할 수 없어야 하기 때문입니다.
        /// </summary>
        public void TryGreetOnEnter()
        {
            var data = ProgressData;
            int today = data.CurrentDay;
            if (today < 0 || data.TalkLastGreetingDay == today) return;

            data.TalkLastGreetingDay = today;
            OnYomiGreeted?.Invoke(YomiTalkTopics.GreetingFor(CurrentTalkTime));
        }

        /// <summary>남은 시간 슬롯에서 지금이 언제인지 정합니다. 슬롯이 없으면 밤으로 봅니다.</summary>
        private TalkTime CurrentTalkTime
        {
            get
            {
                int slots = DatingTimeManager.Instance != null
                    ? DatingTimeManager.Instance.CurrentTimeSlot
                    : 5;
                return YomiTalkTopics.TimeOfSlot(slots);
            }
        }

        // --- 내부 로직 ---

        /// <summary>
        /// 일차가 바뀌었으면 당일 한정 대화 상태를 비웁니다.
        /// 문서 4.5절의 일차 전환 리셋이 계획만 있고 구현이 없었습니다 — 리셋이 없으면
        /// 토픽이 영구 소진되고, 힌트 임계는 누적 획득량으로 매일 공짜 통과됩니다.
        /// 매니저를 넘나드는 배선 대신, 읽는 쪽에서 대화 시작 때 스스로 맞춥니다.
        /// </summary>
        private static void EnsureDailyTalkState(FXOverdose.Core.SaveData data, int today)
        {
            if (data.TalkDailyStateDay == today) return;

            data.TalkDailyStateDay = today;
            data.TalkAffectionGainToday = 0;
            data.TalkTopicsUsedToday.Clear();

            // 어제 끝맺지 못한 대화의 잔존 기록은 어제 몫으로 소멸합니다. 오늘 한도를 막으면 안 됩니다.
            data.TalkActiveTopicId = "";
            data.TalkActiveNodeIndex = -1;
        }

        private void FinishTalk(FXOverdose.Core.SaveData data)
        {
            hasActiveTopic = false;
            activeNodeIndex = -1;

            if (data != null)
            {
                data.TalkActiveTopicId = "";
                data.TalkActiveNodeIndex = -1;
                data.TalkLastSessionEndDay = data.CurrentDay; // 하루 1회 카운트는 종료 시점에 소모
                if (!data.TalkCompletedFlags.Contains(activeTopic.Id))
                    data.TalkCompletedFlags.Add(activeTopic.Id);
            }

            string hint = TryIssueHint(data);

            // 힌트를 발급하면 DailyMarketOutlook.MarkRevealed가 저장을 대신 일으켜 주지만,
            // 그건 우연이지 계약이 아닙니다. 힌트 임계에 못 미친 대화(획득 0)는 저장 없이 끝나
            // 하루 1회 한도가 디스크에 남지 않았습니다. 종료는 항상 저장합니다. (F-3)
            if (data != null) FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();

            ChangeState(YomiRoomState.Idle);
            OnTalkFinished?.Invoke(activeTopic.ClosingLine, hint);
        }

        /// <summary>
        /// 일상 대화 끝에 그날의 차트 방향성 힌트를 이어 붙입니다.
        /// 별도 UI를 띄우지 않고 같은 말풍선 로그에 한 줄 더 얹는 형태입니다.
        /// </summary>
        private string TryIssueHint(FXOverdose.Core.SaveData data)
        {
            if (data == null) return null;

            int today = data.CurrentDay;
            if (data.TalkHintIssuedDay == today) return null;              // 하루 1회 (S3)
            if (data.TalkAffectionGainToday < hintThresholdTier1) return null;

            int tier = data.TalkAffectionGainToday >= hintThresholdTier2 ? 2 : 1;
            var regime = FXOverdose.Trading.DailyMarketOutlook.GetOrRoll(today);
            string[] pool = YomiTalkTopics.HintLinesFor(regime, tier);
            if (pool == null || pool.Length == 0) return null;

            int index = UnityEngine.Random.Range(0, pool.Length);
            data.TalkHintIssuedDay = today;
            data.TalkHintTier = tier;
            data.TalkHintLineId = $"HINT_{regime}_T{tier}_{index:00}";

            FXOverdose.Trading.DailyMarketOutlook.MarkRevealed(today);
            return pool[index];
        }

        /// <summary>
        /// 호감도로 해금되고 오늘 아직 쓰지 않은 토픽 중 하나를 뽑습니다.
        ///
        /// 해금 판정은 현재 호감도가 아니라 <b>역대 최고 호감도</b>로 합니다.
        /// 호감도가 깎였다고 이미 열린 화제가 다시 잠기면 진행하던 대화가 증발합니다. (TS8)
        /// </summary>
        private TalkTopic? PickTopic(FXOverdose.Core.SaveData data)
        {
            var all = YomiTalkTopics.All;
            if (all == null || all.Length == 0) return null;

            int peak = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.PeakAffection : 0;

            // 장면은 시간을 갖습니다. 야식 장면이 대낮에 열리면 플레이어는 "왜 지금?"부터 묻게 됩니다.
            TalkTime now = CurrentTalkTime;

            var candidates = new System.Collections.Generic.List<TalkTopic>();
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].IsUnlocked(peak)) continue;
                if (!all[i].FitsTime(now)) continue;
                if (data != null && data.TalkTopicsUsedToday.Contains(all[i].Id)) continue;
                candidates.Add(all[i]);
            }

            if (candidates.Count == 0) return null; // 오늘 쓸 수 있는 토픽 소진

            // 아직 한 번도 안 본 토픽을 우선합니다. 해금 초반에는 후보가 적어 반복이 눈에 띄기 때문입니다.
            if (data != null)
            {
                var unseen = new System.Collections.Generic.List<TalkTopic>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (!data.TalkTopicsSeenTotal.Contains(candidates[i].Id)) unseen.Add(candidates[i]);
                }
                if (unseen.Count > 0) candidates = unseen;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private const int MaxChoiceHistory = 300; // 무한 누적 방지 (TS4)

        private static void TrimChoiceHistory(FXOverdose.Core.SaveData data)
        {
            int overflow = data.TalkChoiceHistory.Count - MaxChoiceHistory;
            if (overflow > 0) data.TalkChoiceHistory.RemoveRange(0, overflow);
        }

        /// <summary>
        /// 잠깐 휴식. <b>성공 여부를 반환합니다.</b>
        /// 호출부가 반환값을 보지 않으면 실패했는데도 "체력이 회복됐다"고 표시하게 됩니다. (F-2)
        /// </summary>
        public bool TryRest()
        {
            if (currentState != YomiRoomState.Idle) return false;

            if (DatingTimeManager.Instance != null && DatingTimeManager.Instance.TryConsumeTimeSlot(restTimeSlotCost))
            {
                ChangeState(YomiRoomState.Resting);
                DatingTimeManager.Instance.RecoverStamina(restStaminaRecoverAmount);
                // 임시 복귀 로직 (향후 연출 코루틴 등으로 대체 가능)
                ChangeState(YomiRoomState.Idle);
                return true;
            }

            OnActionFailed?.Invoke("시간 슬롯이 부족해서 쉴 수 없어요.");
            return false;
        }

        /// <summary>
        /// 취침 — 거래하지 않고 하루를 마감합니다. (Q1)
        ///
        /// 일차를 여기서 직접 올리지 않습니다. GameScene으로 넘어가 남은 시간을 건너뛰게 해서
        /// 기존 24:00 일일 정산 루틴을 그대로 태웁니다. 그래야 정산·페널티·보스·엔딩 판정이
        /// 통째로 누락되는 우회 경로가 생기지 않습니다.
        /// </summary>
        public bool TrySleep()
        {
            if (currentState != YomiRoomState.Idle) return false;

            ChangeState(YomiRoomState.Transitioning);

            // 상주 GameManager가 있으면 방에서 그대로 마감합니다. 씬 전환이 0회가 됩니다.
            // 없는 경우(새 게임 첫날처럼 아직 거래 씬을 한 번도 거치지 않은 상태)에는
            // 아래의 종전 경로 — GameScene으로 넘어가 남은 시간을 가속 — 로 떨어집니다.
            var gm = GameManager.Instance;
            if (gm != null && gm.TrySettleFromRoom())
            {
                FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();
                ChangeState(YomiRoomState.Idle);
                return true;
            }

            GameManager.PendingSleepThroughToday = true;

            var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
            if (saveManager != null)
            {
                saveManager.SaveCurrentGame();
                saveManager.PrepareLoadGame(saveManager.ActiveStorySlotIndex);
            }

            LoadingScreenController.TargetSceneToLoad = "GameScene";
            SceneManager.LoadScene("LoadingScene");
            return true;
        }

        public bool MoveToWorldMap()
        {
            if (currentState != YomiRoomState.Idle) return false;
            ChangeState(YomiRoomState.Transitioning);
            FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();
            LoadingScreenController.TargetSceneToLoad = "WorldMapScene";
            SceneManager.LoadScene("LoadingScene");
            return true;
        }

        /// <summary>거래 개시. 씬 전환에 들어가면 true. false면 방에 그대로 남습니다. (F-2)</summary>
        public bool StartTrading()
        {
            if (currentState != YomiRoomState.Idle) return false;
            ChangeState(YomiRoomState.Transitioning);

            // 저장 → 복원 예약 순서로 진입해야 GameManager가 StartNewGame()으로 새 게임을 시작하지 않습니다. (SV-B10)
            var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
            if (saveManager != null)
            {
                saveManager.SaveCurrentGame();
                saveManager.PrepareLoadGame(saveManager.ActiveStorySlotIndex);
            }

            LoadingScreenController.TargetSceneToLoad = "GameScene";
            SceneManager.LoadScene("LoadingScene");
            return true;
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
