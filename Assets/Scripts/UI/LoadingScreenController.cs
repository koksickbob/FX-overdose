using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FXOverdose.UI
{
    public class LoadingScreenController : MonoBehaviour
    {
        [Header("로딩 UI 연결 (필수)")]
        [SerializeField] private Slider progressBar;
        [SerializeField] private Animator loadingAnimator;
        [SerializeField] private float minimumLoadingTime = 2.0f; // 페이크 로딩 최소 대기 시간

        private void Start()
        {
            if (progressBar != null) progressBar.value = 0f;
            StartCoroutine(LoadTargetScene("GameScene"));
        }

        private IEnumerator LoadTargetScene(string sceneName)
        {
            float elapsedTime = 0f;

            // 로딩 애니메이션 트리거 (있는 경우)
            if (loadingAnimator != null)
            {
                loadingAnimator.SetTrigger("StartLoad");
            }

            // 비동기 씬 로드
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(op.progress / 0.9f); // 0.9가 로딩 완료 시점

                if (progressBar != null)
                {
                    progressBar.value = progress;
                }

                // 로딩이 실제로는 너무 빨리 끝나 애니메이션을 못 볼 수 있으므로 최소 시간 대기
                if (op.progress >= 0.9f && elapsedTime >= minimumLoadingTime)
                {
                    // 페이드 아웃 등의 애니메이션이 있다면 여기서 대기 후 씬 전환
                    if (loadingAnimator != null)
                    {
                        loadingAnimator.SetTrigger("FinishLoad");
                        // 페이드아웃 애니메이션 시간을 위해 약간 대기 (예: 0.5초)
                        yield return new WaitForSeconds(0.5f);
                    }
                    
                    op.allowSceneActivation = true;
                }

                yield return null;
            }
        }
    }
}
