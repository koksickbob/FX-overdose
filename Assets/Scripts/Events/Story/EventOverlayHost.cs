using System;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.Events.Story
{
    /// <summary>
    /// 현재 씬 위에 이벤트 화면을 얹는 호스트. <b>씬 전환이 0회</b>입니다.
    ///
    /// 발명한 구조가 아닙니다 — <c>ComicCutsceneController.GetOrCreateRuntime</c>(캔버스 없는 씬에서
    /// 스스로 전체화면 캔버스를 만들고 콜백으로 끝냄)과 <c>ChoiceEventController.PrepareGamePause</c>
    /// (선택지 팝업 + 게임 일시정지)를 합친 것입니다. 둘 다 이미 실전에서 돌고 있습니다. (계획 8.3절)
    ///
    /// <para>전용 씬과 달리 복귀 계약이 없습니다. 밑 씬이 그대로 살아 있으므로 끝나면 콜백만 부르면 됩니다 —
    /// <c>LastSceneName</c>도 <c>PrepareLoadGame</c>도 신경 쓸 일이 없습니다.</para>
    /// </summary>
    public sealed class EventOverlayHost : MonoBehaviour
    {
        private Action onFinished;
        private bool pausedByThisEvent;

        /// <summary>오버레이를 띄웁니다. 캔버스는 현재 씬에 만들어지고 종료 시 통째로 파괴됩니다.</summary>
        public static EventOverlayHost Show(EventDefinition definition, Action onComplete)
        {
            GameObject root = new GameObject("EventOverlayCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 만화 컷씬(1200)보다 위입니다. 정산 중 스토리 이벤트에서 둘이 겹칠 수 있습니다.
            canvas.sortingOrder = 1300;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EventOverlayHost host = root.AddComponent<EventOverlayHost>();
            host.onFinished = onComplete;

            // 거래 중이었다면 멈춥니다. 이미 Paused/Settlement면 PauseGame이 아무것도 하지 않으므로,
            // 우리가 멈춘 경우에만 되돌리기 위해 직전 상태를 봐 둡니다.
            GameManager gm = GameManager.Instance;
            host.pausedByThisEvent = gm != null && gm.CurrentState == GameManager.GameState.Playing;
            if (host.pausedByThisEvent) gm.PauseGame();

            GameObject viewObject = new GameObject("EventView", typeof(RectTransform));
            viewObject.transform.SetParent(root.transform, false);
            EventView view = viewObject.AddComponent<EventView>();

            view.Begin(new EventRunner(definition), isOverlay: true, returnScene: null, finishedCallback: host.Complete);
            return host;
        }

        private void Complete()
        {
            if (pausedByThisEvent) GameManager.Instance?.ResumeGame();

            Action callback = onFinished;
            onFinished = null;

            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}
