using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.AI.LLM;
using FXOverdose.Trading;

namespace FXOverdose.UI
{
    /// <summary>게임 씬과 LLM/차트 시스템을 준비한 뒤 페이드로 게임을 개시합니다.</summary>
    public class LoadingScreenController : MonoBehaviour
    {
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
            SetProgress(0f, "INITIALIZING");
            StartCoroutine(LoadAndPrepareGame());
        }

        private IEnumerator LoadAndPrepareGame()
        {
            LocalLLMService.DeferGameStartToLoadingScreen = true;

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

            while (!operation.isDone)
            {
                float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                SetProgress(sceneProgress * 0.65f, "LOADING MARKET DATA");
                yield return null;
            }

            Scene gameScene = SceneManager.GetSceneByName(targetSceneName);
            if (gameScene.IsValid())
                SceneManager.SetActiveScene(gameScene);

            LocalLLMService llm = LocalLLMService.Instance;
            while (!AreGameSystemsReady(llm))
            {
                float llmProgress = llm != null && llm.IsLLMReady ? 1f : 0.35f;
                float startupProgress = llm != null && llm.IsStartupSequenceReady ? 1f : 0f;
                float systemProgress = AreChartSystemsPresent() ? 1f : 0f;
                float preparation = (llmProgress + startupProgress + systemProgress) / 3f;
                string message = llm != null && llm.IsLLMReady ? "PREPARING CHART" : "WARMING UP LLM";
                SetProgress(0.65f + preparation * 0.34f, message);
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

            // 정지 구간이 끝난 다음 LLM 첫 대사, 게임 시간과 시장을 시작합니다.
            LocalLLMService.DeferGameStartToLoadingScreen = false;
            yield return null;

            Scene loadingScene = gameObject.scene;
            if (loadingScene.IsValid() && loadingScene.isLoaded)
                SceneManager.UnloadSceneAsync(loadingScene);
        }

        private static bool AreGameSystemsReady(LocalLLMService llm)
        {
            return llm != null
                && llm.IsLLMReady
                && llm.IsStartupSequenceReady
                && AreChartSystemsPresent();
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

            if (LocalLLMService.DeferGameStartToLoadingScreen)
                LocalLLMService.DeferGameStartToLoadingScreen = false;
        }
    }
}
