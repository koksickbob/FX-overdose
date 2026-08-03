using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

namespace FXOverdose.UI
{
    /// <summary>게임 씬과 차트 시스템을 준비한 뒤 페이드로 게임을 개시합니다.</summary>
    public class LoadingScreenController : MonoBehaviour
    {
        /// <summary>LoadingScene 진입 전에 이 값을 설정하면 해당 씬으로 로딩합니다. 로딩 시작 후 자동으로 GameScene으로 초기화됩니다.</summary>
        public static string TargetSceneToLoad = "GameScene";

        [Header("로딩 UI")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("전환")]
        [SerializeField] private string targetSceneName = "GameScene";
        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private float fadeOutDuration = 0.65f;
        [SerializeField] private float completedHoldDuration = 0.25f;
        [SerializeField] private float postFadeStartDelay = 0.5f;

        private float timeScaleBeforeFreeze = 1f;
        private bool ownsPostFadeFreeze;

        public void Configure(Slider bar, TMP_Text percent, TMP_Text status, CanvasGroup group)
        {
            progressBar = bar;
            progressText = percent;
            statusText = status;
            canvasGroup = group;
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(TargetSceneToLoad))
            {
                targetSceneName = TargetSceneToLoad;
                TargetSceneToLoad = "GameScene"; // 기본값으로 복원
            }

            SetProgress(0f, "INITIALIZING");
            StartCoroutine(LoadAndPrepareGame());
        }

        private IEnumerator LoadAndPrepareGame()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                yield return FadeCanvas(0f, 1f, fadeInDuration);
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                Debug.LogError($"[LoadingScreen] {targetSceneName} 로드를 시작하지 못했습니다.");
                yield break;
            }

            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                SetProgress(sceneProgress * 0.65f, "LOADING MARKET DATA");
                yield return null;
            }

            // 💡 [중복 경고 스팸 방지] GameScene이 활성화되기 직전에 이전 씬(로딩 씬)의 AudioListener를 미리 꺼줍니다.
            // 씬이 활성화된 후 꺼주면 전환되는 몇 프레임 동안 AudioListener가 2개가 되어 로그가 폭주할 수 있습니다.
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
            foreach (var listener in listeners)
            {
                if (listener.gameObject.scene == gameObject.scene)
                {
                    listener.enabled = false;
                }
            }

            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            Scene gameScene = SceneManager.GetSceneByName(targetSceneName);
            if (gameScene.IsValid())
                SceneManager.SetActiveScene(gameScene);

            while (!AreChartSystemsPresent())
            {
                SetProgress(0.9f, "PREPARING CHART");
                yield return null;
            }

            SetProgress(1f, "READY");
            yield return new WaitForSecondsRealtime(completedHoldDuration);

            if (canvasGroup != null)
                yield return FadeCanvas(1f, 0f, fadeOutDuration);

            // 게임 화면을 먼저 보여준 뒤 0.5초 동안 완전히 정지합니다.
            timeScaleBeforeFreeze = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            Time.timeScale = 0f;
            ownsPostFadeFreeze = true;
            yield return new WaitForSecondsRealtime(postFadeStartDelay);

            Time.timeScale = timeScaleBeforeFreeze;
            ownsPostFadeFreeze = false;

            // 정지 구간이 끝난 다음 게임 시간과 시장을 시작합니다.
            yield return null;
            // 매 진입마다 현재 GameScene의 매니저와 시장을 명시적으로 개장해 Loading 상태 고착을 방지합니다.
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            gameManager?.FinishLoadingAndStartPlaying();
            MarketSimulationEngine market = Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            market?.OpenMarketAfterLoading();

            Scene loadingScene = gameObject.scene;
            if (loadingScene.IsValid() && loadingScene.isLoaded)
                SceneManager.UnloadSceneAsync(loadingScene);
        }

        private static bool AreGameSystemsReady()
        {
            return AreChartSystemsPresent();
        }

        private static bool AreChartSystemsPresent()
        {
            MarketSimulationEngine market = Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            return market != null
                && market.IsDataPrepared
                && GameObject.Find("ChartMainPanel") != null;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (canvasGroup == null)
                yield break;

            float elapsed = 0f;
            canvasGroup.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
                yield return null;
            }
            canvasGroup.alpha = to;
        }

        private void SetProgress(float value, string status)
        {
            value = Mathf.Clamp01(value);
            if (progressBar != null) progressBar.SetValueWithoutNotify(value);
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(value * 100f)}%";
            if (statusText != null) statusText.text = status;
        }

        private void OnDestroy()
        {
            if (ownsPostFadeFreeze)
            {
                Time.timeScale = timeScaleBeforeFreeze;
                ownsPostFadeFreeze = false;
            }
        }
    }
}

