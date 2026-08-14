using UnityEngine;
using UnityEngine.SceneManagement;

namespace FXOverdose.UI
{
    /// <summary>
    /// 요미의 방에서 일일 정산을 진행하려면 정산·게임오버 UI가 그 씬에도 있어야 합니다.
    /// 두 컨트롤러 모두 <b>런타임에 자기 UI를 스스로 만들어내므로</b>, 씬을 편집하지 않고
    /// 기존 Canvas에 컴포넌트만 붙이면 됩니다.
    ///
    /// <see cref="BossBattleUIBootstrap"/>과 같은 방식입니다 —
    /// <c>RuntimeInitializeOnLoadMethod</c>로 씬 로드 콜백을 걸고, 멱등하게 설치합니다.
    ///
    /// 만화 컷씬(<see cref="ComicCutsceneController"/>)은 자체 정적 Instance가 지연 생성되므로
    /// 여기서 따로 설치하지 않습니다.
    /// </summary>
    public static class RoomSettlementUIBootstrap
    {
        private const string TargetSceneName = "YomiRoomScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != TargetSceneName) return;
            EnsureInstalled(scene);
        }

        /// <summary>씬 로드 콜백과 취침 직전 양쪽에서 부를 수 있는 멱등 설치 진입점입니다.</summary>
        public static void EnsureInstalled(Scene scene)
        {
            if (!GameManager.RoomSettlementEnabled) return;
            if (!scene.IsValid() || !scene.isLoaded || scene.name != TargetSceneName) return;

            Canvas target = FindTopmostRootCanvas(scene);
            if (target == null)
            {
                Debug.LogWarning($"[RoomSettlementUI] {TargetSceneName}에서 루트 Canvas를 찾지 못해 정산 UI를 설치하지 못했습니다. " +
                                 "취침 시 정산 화면이 뜨지 않습니다.");
                return;
            }

            if (target.GetComponent<DailySettlementUIController>() == null)
            {
                target.gameObject.AddComponent<DailySettlementUIController>();
                Debug.Log("[RoomSettlementUI] 요미의 방 Canvas에 일일 정산 UI 설치 완료");
            }

            // 정기 지출만으로도 파산할 수 있으므로 엔딩 UI도 방에 있어야 합니다.
            // 없으면 파산이 판정돼도 화면에 아무것도 뜨지 않습니다.
            if (target.GetComponent<GameOverUIController>() == null)
            {
                target.gameObject.AddComponent<GameOverUIController>();
                Debug.Log("[RoomSettlementUI] 요미의 방 Canvas에 게임오버 UI 설치 완료");
            }
        }

        private static Canvas FindTopmostRootCanvas(Scene scene)
        {
            Canvas best = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas) continue;
                    if (best == null || canvas.sortingOrder > best.sortingOrder) best = canvas;
                }
            }
            return best;
        }
    }
}
