using TMPro;
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

        /// <summary>런타임 타이틀 빌더가 생성한 UI를 컨트롤러에 연결합니다.</summary>
        public void Configure(
            Button newGame,
            Button loadGame,
            Button settings,
            Button quitGame,
            GameObject loadPanel,
            GameObject settingsPopup)
        {
            btnNewGame = newGame;
            btnLoadGame = loadGame;
            btnSettings = settings;
            btnQuitGame = quitGame;
            loadGamePanel = loadPanel;
            settingsPanel = settingsPopup;
        }

        private void Start()
        {
            // 이벤트 리스너 연결
            if (btnNewGame != null) btnNewGame.onClick.AddListener(OnClickNewGame);
            if (btnLoadGame != null) btnLoadGame.onClick.AddListener(OnClickLoadGame);
            if (btnSettings != null) btnSettings.onClick.AddListener(OnClickSettings);
            if (btnQuitGame != null) btnQuitGame.onClick.AddListener(OnClickQuitGame);

            BindPopupControls();

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
            
            LoadGameFlow();
        }

        public void OnClickLoadGame()
        {
            Debug.Log("[MainMenuController] 불러오기 팝업 오픈");
            if (loadGamePanel != null)
            {
                BindLoadPanelControls();
                loadGamePanel.SetActive(true);
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

        public void CloseLoadGamePanel()
        {
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
        }

        public void CloseSettingsPanel()
        {
            PlayerPrefs.Save();
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void BindPopupControls()
        {
            BindLoadPanelControls();
            BindSettingsPanelControls();
        }

        private void BindLoadPanelControls()
        {
            if (loadGamePanel == null) return;

            Button close = loadGamePanel.transform.Find("ModalWindow/Btn_Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(CloseLoadGamePanel);
            }

            for (int i = 0; i < 3; i++)
            {
                int slotIndex = i;
                Transform slot = loadGamePanel.transform.Find($"ModalWindow/SaveSlot_{i + 1}");
                if (slot == null) continue;

                bool hasSave = SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasSave(i);
                Button button = slot.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.interactable = hasSave;
                    button.onClick.AddListener(() => OnClickLoadSlot(slotIndex));
                }

                TMP_Text state = slot.Find("State")?.GetComponent<TMP_Text>();
                if (state != null) state.text = hasSave ? "DATA FOUND" : "EMPTY SLOT";
            }
        }

        private void BindSettingsPanelControls()
        {
            if (settingsPanel == null) return;

            Button close = settingsPanel.transform.Find("ModalWindow/Btn_Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(CloseSettingsPanel);
            }

            BindVolumeSlider(settingsPanel.transform, "ModalWindow/Slider_BGM", "BGMVolume");
            BindVolumeSlider(settingsPanel.transform, "ModalWindow/Slider_SFX", "SFXVolume");
        }

        private static void BindVolumeSlider(Transform panel, string path, string key)
        {
            Slider slider = panel.Find(path)?.GetComponent<Slider>();
            if (slider == null) return;

            slider.onValueChanged.RemoveAllListeners();
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 0.8f));
            slider.onValueChanged.AddListener(value => PlayerPrefs.SetFloat(key, value));
        }

        public void OnClickLoadSlot(int slotIndex)
        {
            if (SaveLoadManager.Instance == null || !SaveLoadManager.Instance.HasSave(slotIndex))
            {
                Debug.LogWarning($"[MainMenuController] 슬롯 {slotIndex + 1}에 저장 데이터가 없습니다.");
                return;
            }

            SaveLoadManager.Instance.PrepareLoadGame(slotIndex);
            LoadGameFlow();
        }

        private static void LoadGameFlow()
        {
            if (Application.CanStreamedLevelBeLoaded("LoadingScene"))
            {
                SceneManager.LoadScene("LoadingScene");
                return;
            }

            Debug.LogWarning("[MainMenuController] LoadingScene이 아직 없어 GameScene으로 바로 진입합니다.");
            SceneManager.LoadScene("GameScene");
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
