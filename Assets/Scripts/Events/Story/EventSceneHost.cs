using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.UI;

namespace FXOverdose.Events.Story
{
    /// <summary>
    /// 전용 씬(<c>EventScene</c>)에서 이벤트를 재생하는 호스트.
    ///
    /// <para>오버레이와 달리 <b>복귀 계약</b>이 있습니다. 그 계약이 이 클래스의 존재 이유 전부입니다:
    /// 어디로 돌아갈지(<see cref="EventLaunchContext"/>), 재접속했을 때 어디로 떨어질지(<c>LastSceneName</c>),
    /// 그리고 <b>거래 씬으로는 돌아가지 않는다</b>는 제한(계획 7.4절).</para>
    /// </summary>
    public sealed class EventSceneHost : MonoBehaviour
    {
        /// <summary>
        /// 복귀를 허용하는 씬. <b>GameScene은 의도적으로 빠져 있습니다.</b>
        ///
        /// 거래 씬 복귀는 <c>SaveCurrentGame()</c> → <c>PrepareLoadGame()</c> 순서를 지켜야
        /// GameManager가 새 게임을 시작하지 않습니다(SV-B10). 그 프로토콜을 여기서 한 벌 더 구현하느니
        /// 거래 중 이벤트는 오버레이로 보내는 편이 낫습니다 — 오버레이는 씬을 아예 떠나지 않습니다.
        /// </summary>
        public static readonly string[] AllowedReturnScenes = { "YomiRoomScene", "WorldMapScene" };

        public static bool IsAllowedReturnScene(string sceneName)
            => !string.IsNullOrEmpty(sceneName) && System.Array.IndexOf(AllowedReturnScenes, sceneName) >= 0;

        private string returnScene;

        private void Start()
        {
            string eventId = EventLaunchContext.EventId;
            returnScene = EventLaunchContext.ReturnScene;
            EventLaunchContext.Clear();

            if (string.IsNullOrEmpty(returnScene) || !IsAllowedReturnScene(returnScene))
            {
                Debug.LogError($"[EventSceneHost] 복귀 씬 '{returnScene}'을(를) 쓸 수 없습니다. " +
                               $"허용: {string.Join(", ", AllowedReturnScenes)}. 요미 방으로 돌아갑니다.");
                returnScene = "YomiRoomScene";
            }

            if (!EventCatalog.TryGet(eventId, out EventDefinition definition))
            {
                Debug.LogError($"[EventSceneHost] 이벤트 '{eventId}'을(를) 찾을 수 없습니다. 즉시 복귀합니다.");
                ReturnToCaller();
                return;
            }

            EventView view = BuildCanvas().AddComponent<EventView>();
            view.Begin(new EventRunner(definition), isOverlay: false, returnScene, ReturnToCaller);
        }

        private static GameObject BuildCanvas()
        {
            GameObject root = new GameObject("EventCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject viewObject = new GameObject("EventView", typeof(RectTransform));
            viewObject.transform.SetParent(root.transform, false);
            return viewObject;
        }

        /// <summary>
        /// 복귀. <c>LastSceneName</c>은 커밋에서 이미 적혔습니다(<see cref="EventRunner.Commit"/>) —
        /// 여기서 다시 저장하지 않습니다. 이벤트 시스템의 디스크 쓰기는 커밋 1회뿐입니다.
        /// </summary>
        private void ReturnToCaller()
        {
            // 정적 진행 플래그를 여기서 내립니다. 안 내리면 이후 모든 이벤트가 "이미 진행 중"으로 막힙니다.
            EventLauncher.NotifyFinished();

            LoadingScreenController.TargetSceneToLoad = returnScene;
            SceneManager.LoadScene("LoadingScene");
        }
    }

    /// <summary>
    /// 전용 씬 호스트로 넘기는 정적 컨텍스트. 씬이 하나이므로 무엇을 재생하고 어디로 돌아갈지는
    /// 호출자만 압니다. <c>LoadingScreenController.TargetSceneToLoad</c>와 같은 방식입니다.
    /// </summary>
    public static class EventLaunchContext
    {
        public static string EventId { get; private set; }
        public static string ReturnScene { get; private set; }

        public static void Set(string eventId, string returnScene)
        {
            EventId = eventId;
            ReturnScene = returnScene;
        }

        /// <summary>읽은 쪽이 즉시 비웁니다. 남겨 두면 다음 이벤트가 엉뚱한 복귀지를 물려받습니다.</summary>
        public static void Clear()
        {
            EventId = null;
            ReturnScene = null;
        }
    }
}
