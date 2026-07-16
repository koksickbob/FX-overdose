using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FXOverdose.Core;

namespace FXOverdose.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("메인 메뉴 버튼 (필수 할당)")]
        [SerializeField] private Button btnNewGame;
        [SerializeField] private Button btnLoadGame;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnQuitGame;

        [Header("팝업 패널 (필수 할당)")]
        [SerializeField] private GameObject loadGamePanel;
        [SerializeField] private GameObject settingsPanel;

        private void Start()
        {
            // 이벤트 리스너 연결
            if (btnNewGame != null) btnNewGame.onClick.AddListener(OnClickNewGame);
            if (btnLoadGame != null) btnLoadGame.onClick.AddListener(OnClickLoadGame);
            if (btnSettings != null) btnSettings.onClick.AddListener(OnClickSettings);
            if (btnQuitGame != null) btnQuitGame.onClick.AddListener(OnClickQuitGame);

            // 초기 팝업 비활성화
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void OnClickNewGame()
        {
            Debug.Log("[MainMenuController] 새 게임 시작");
            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.PrepareNewGame();
            }
            
            // LoadingScene 로드 시도. 만약 LoadingScene이 비활성화/빌드 제외 상태면 Fallback 작동
            try
            {
                SceneManager.LoadScene("LoadingScene");
            }
            catch
            {
                Debug.LogWarning("[MainMenuController] LoadingScene을 찾을 수 없으므로 GameScene으로 바로 진입합니다.");
                SceneManager.LoadScene("GameScene");
            }
        }

        public void OnClickLoadGame()
        {
            Debug.Log("[MainMenuController] 불러오기 팝업 오픈");
            if (loadGamePanel != null)
            {
                loadGamePanel.SetActive(true);
                // TODO: UI 담당자가 슬롯 버튼 생성 후 SaveLoadManager.Instance.PrepareLoadGame(slot) 호출 연결 필요
            }
        }

        public void OnClickSettings()
        {
            Debug.Log("[MainMenuController] 설정 팝업 오픈");
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
            }
        }

        public void OnClickQuitGame()
        {
            Debug.Log("[MainMenuController] 게임 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
