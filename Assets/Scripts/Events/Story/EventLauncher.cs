using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using FXOverdose.Core;
using FXOverdose.UI;

namespace FXOverdose.Events.Story
{
    /// <summary>
    /// 이벤트 진입점. <b>모든 호출자가 여기를 통과합니다.</b>
    ///
    /// 게이트를 한곳에 모으는 이유는 계산이 아니라 <b>누락 방지</b>입니다. 호출자가 넷(요미 방 · 월드맵 ·
    /// 정산 중 GameManager · 타이틀 직후)으로 늘어나는데, "이미 본 이벤트인가"를 각자 확인하게 두면
    /// 넷째 호출자에서 조용히 빠집니다.
    /// </summary>
    public static class EventLauncher
    {
        /// <summary>
        /// 이벤트가 진행 중입니다. <b>정적이어야 합니다</b> — 전용 씬 호스트는 진입할 때 이전 인스턴스가
        /// 이미 파괴되므로, 인스턴스 필드에 두면 중복 진입 가드가 무효가 됩니다. (계획 6.3절)
        /// </summary>
        public static bool IsRunning { get; private set; }

        /// <summary>
        /// 이벤트를 재생합니다. <b>반환값을 반드시 확인하십시오.</b> false를 무시하면
        /// "이벤트가 재생됐다고 믿고 진행 상태를 올리는" 경로가 생깁니다. (F-2와 같은 부류)
        /// </summary>
        /// <param name="hostOverride">null이면 이벤트 데이터의 기본 호스트를 씁니다.</param>
        /// <param name="returnScene">전용 씬 호스트에서만 의미가 있습니다. 생략하면 현재 씬으로 돌아갑니다.</param>
        /// <param name="onComplete">
        /// <b>오버레이 호스트에서만</b> 호출됩니다. 전용 씬은 씬이 파괴되어 콜백이 살아남지 못하므로
        /// <paramref name="returnScene"/>으로 복귀합니다. (계획 3.1절)
        /// </param>
        public static bool Play(string eventId, EventHostKind? hostOverride = null,
            string returnScene = null, Action onComplete = null)
        {
            if (IsRunning)
            {
                Debug.LogWarning($"[EventLauncher] 이미 이벤트가 진행 중이라 '{eventId}'을(를) 재생하지 않습니다.");
                return false;
            }

            if (!EventCatalog.TryGet(eventId, out EventDefinition definition))
            {
                Debug.LogError($"[EventLauncher] 이벤트 '{eventId}'이(가) 테이블에 없습니다.");
                return false;
            }

            if (IsCompleted(eventId))
            {
                Debug.Log($"[EventLauncher] '{eventId}'은(는) 이미 완료된 이벤트라 재생하지 않습니다.");
                return false;
            }

            EventHostKind host = ResolveHost(hostOverride ?? definition.DefaultHost, eventId);

            // 저장 불가 상태여도 막지 않습니다. 2단계 커밋이 결과를 메모리에 남겨 다음 저장에 실어 보냅니다.
            // 다만 로그는 남깁니다 — 나중에 "왜 이때 저장이 안 됐지"를 되짚을 근거가 됩니다. (계획 6.4절)
            WarnIfSaveBlocked(eventId);

            IsRunning = true;

            if (host == EventHostKind.Scene)
            {
                string destination = returnScene ?? SceneManager.GetActiveScene().name;
                if (!EventSceneHost.IsAllowedReturnScene(destination))
                {
                    Debug.LogError($"[EventLauncher] '{destination}'(으)로는 전용 씬에서 복귀할 수 없습니다. " +
                                   "거래 씬 복귀가 필요하면 오버레이 호스트를 쓰십시오. (계획 7.4절)");
                    IsRunning = false;
                    return false;
                }

                if (onComplete != null)
                    Debug.LogWarning($"[EventLauncher] '{eventId}'은(는) 전용 씬으로 재생되므로 완료 콜백이 무시됩니다.");

                EventLaunchContext.Set(eventId, destination);
                LoadingScreenController.TargetSceneToLoad = "EventScene";
                SceneManager.LoadScene("LoadingScene");
                return true;
            }

            EventOverlayHost.Show(definition, () =>
            {
                NotifyFinished();
                onComplete?.Invoke();
            });
            return true;
        }

        /// <summary>호스트가 이벤트를 끝냈음을 알립니다. 이걸 빼먹으면 이후 모든 이벤트가 막힙니다.</summary>
        public static void NotifyFinished() => IsRunning = false;

        /// <summary>이미 끝까지 진행한 이벤트인지. 중도 이탈은 기록이 남지 않으므로 false입니다.</summary>
        public static bool IsCompleted(string eventId)
        {
            SaveData data = SaveLoadManager.Instance?.CurrentData;
            return data != null && data.EventCompletedIds.Contains(eventId);
        }

        /// <summary>분기 플래그가 서 있는지. 이후 이벤트의 조건 판정용입니다.</summary>
        public static bool HasFlag(string flag)
        {
            SaveData data = SaveLoadManager.Instance?.CurrentData;
            return data != null && data.StoryFlags.Contains(flag);
        }

        /// <summary>
        /// 전용 씬을 고를 수 없는 상황이면 오버레이로 강등합니다.
        ///
        /// <c>GameManager.HandleSceneLoaded</c>는 거래 씬이 아닌 <b>모든</b> 씬에서 상태를 Paused로 바꾼 뒤,
        /// 정산이 밀려 있으면(<c>isSettlementProcessing || CurrentHour >= 24</c>) <b>그 씬에서 일일 정산을 시작합니다.</b>
        /// EventScene도 예외가 아니라, 그대로 두면 이벤트 위로 정산 화면이 겹쳐 올라옵니다. (계획 7.3절)
        /// </summary>
        private static EventHostKind ResolveHost(EventHostKind requested, string eventId)
        {
            if (requested != EventHostKind.Scene) return requested;

            GameManager gm = GameManager.Instance;
            if (gm == null) return requested;

            bool settlementPending = gm.CurrentState == GameManager.GameState.Settlement || gm.CurrentHour >= 24;
            if (!settlementPending) return requested;

            Debug.LogWarning($"[EventLauncher] 정산이 밀려 있어 '{eventId}'을(를) 오버레이로 재생합니다. " +
                             "전용 씬으로 가면 그 씬에서 일일 정산이 겹쳐 시작됩니다.");
            return EventHostKind.Overlay;
        }

        private static void WarnIfSaveBlocked(string eventId)
        {
            SaveLoadManager save = SaveLoadManager.Instance;
            if (save == null) return;

            if (!save.AllowsSaving)
            {
                Debug.LogWarning($"[EventLauncher] {save.CurrentGameMode} 모드라 '{eventId}'의 결과가 디스크에 남지 않습니다.");
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm != null &&
                gm.CurrentState != GameManager.GameState.Playing &&
                gm.CurrentState != GameManager.GameState.Paused)
            {
                Debug.LogWarning($"[EventLauncher] 현재 상태({gm.CurrentState})에서는 저장이 막혀 있습니다. " +
                                 $"'{eventId}'의 결과는 메모리에 남았다가 다음 저장에 함께 기록됩니다.");
            }
        }
    }
}
