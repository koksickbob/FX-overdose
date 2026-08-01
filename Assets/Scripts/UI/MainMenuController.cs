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
        [SerializeField] private Button btnAchievements;

        [Header("팝업 패널 (필수 할당)")]
        [SerializeField] private GameObject gameModePanel;
        [SerializeField] private GameObject loadGamePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject tutorialPromptPanel;
        [SerializeField] private GameObject overwritePromptPanel;
        [SerializeField] private AchievementUIController achievementUI;

        private bool allowCreatingStorySlot;
        private int pendingSlotIndex = -1;

        /// <summary>런타임 타이틀 빌더가 생성한 UI를 컨트롤러에 연결합니다.</summary>
        public void Configure(
            Button newGame,
            Button loadGame,
            Button settings,
            Button quitGame,
            Button achievements,
            GameObject loadPanel,
            GameObject settingsPopup,
            GameObject modePanel = null)
        {
            btnNewGame = newGame;
            btnLoadGame = loadGame;
            btnSettings = settings;
            btnQuitGame = quitGame;
            btnAchievements = achievements;
            loadGamePanel = loadPanel;
            settingsPanel = settingsPopup;
            gameModePanel = modePanel;
            
            if (achievementUI == null)
            {
                achievementUI = Object.FindAnyObjectByType<AchievementUIController>();
            }
        }

        private void Start()
        {
            EnsureGameModePanel();

            if (btnAchievements == null && transform.parent != null)
            {
                Transform btnTransform = transform.parent.Find("Btn_Achievements");
                if (btnTransform != null) btnAchievements = btnTransform.GetComponent<Button>();
            }

            // 이벤트 리스너 연결
            if (btnNewGame != null) btnNewGame.onClick.AddListener(OnClickNewGame);
            if (btnLoadGame != null) btnLoadGame.onClick.AddListener(OnClickLoadGame);
            if (btnSettings != null) btnSettings.onClick.AddListener(OnClickSettings);
            if (btnQuitGame != null) btnQuitGame.onClick.AddListener(OnClickQuitGame);
            if (btnAchievements != null) btnAchievements.onClick.AddListener(OnClickAchievements);

            BindPopupControls();

            // 초기 팝업 비활성화
            if (gameModePanel != null) gameModePanel.SetActive(false);
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        public void OnClickNewGame()
        {
            Debug.Log("[MainMenuController] 게임 모드 선택 팝업 오픈");
            EnsureGameModePanel();
            BindGameModePanelControls();
            if (gameModePanel != null)
            {
                if (loadGamePanel != null) loadGamePanel.SetActive(false);
                if (settingsPanel != null) settingsPanel.SetActive(false);
                gameModePanel.SetActive(true);
                gameModePanel.transform.SetAsLastSibling();
                return;
            }

            // 런타임 UI 생성에 실패한 예외 상황에서는 기존처럼 스토리 새 게임으로 안전하게 진입합니다.
            StartNewMode(GameMode.Story, 0);
        }

        public void OnClickLoadGame()
        {
            Debug.Log("[MainMenuController] 스토리 불러오기 팝업 오픈");
            allowCreatingStorySlot = false;
            OpenStorySlotPanel();
        }

        public void OnClickStoryMode()
        {
            Debug.Log("[MainMenuController] 스토리 모드 선택");
            allowCreatingStorySlot = true;
            if (gameModePanel != null) gameModePanel.SetActive(false);
            OpenStorySlotPanel();
        }

        public void OnClickEndlessMode()
        {
            Debug.Log("[MainMenuController] 무한 모드 선택 (AI 자동매매 사용 가능)");
            StartNewMode(GameMode.Endless);
        }

        public void OnClickChallengeMode()
        {
            Debug.Log("[MainMenuController] 챌린지 모드 선택 (USER 수동매매 고정)");
            StartNewMode(GameMode.Challenge);
        }

        public void OnClickSettings()
        {
            Debug.Log("[MainMenuController] 설정 팝업 오픈");
            if (settingsPanel != null)
            {
                if (gameModePanel != null) gameModePanel.SetActive(false);
                if (loadGamePanel != null) loadGamePanel.SetActive(false);
                settingsPanel.SetActive(true);
                settingsPanel.transform.SetAsLastSibling();
            }
        }

        public void OnClickAchievements()
        {
            Debug.Log("[MainMenuController] 업적 팝업 오픈");
            
            if (achievementUI == null)
            {
                achievementUI = Object.FindAnyObjectByType<AchievementUIController>();
            }
            
            if (achievementUI != null)
            {
                if (gameModePanel != null) gameModePanel.SetActive(false);
                if (loadGamePanel != null) loadGamePanel.SetActive(false);
                if (settingsPanel != null) settingsPanel.SetActive(false);
                achievementUI.Open();
            }
        }

        public void CloseLoadGamePanel()
        {
            if (loadGamePanel != null) loadGamePanel.SetActive(false);
        }

        public void CloseGameModePanel()
        {
            if (gameModePanel != null) gameModePanel.SetActive(false);
        }

        public void CloseSettingsPanel()
        {
            PlayerPrefs.Save();
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void BindPopupControls()
        {
            BindGameModePanelControls();
            BindLoadPanelControls();
            BindSettingsPanelControls();
            BindTutorialPromptControls();
            BindOverwritePromptControls();
        }

        private void EnsureGameModePanel()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            if (gameModePanel == null)
            {
                gameModePanel = TitleScreenBuilder.EnsureGameModePanel(canvas.transform);
            }

            if (tutorialPromptPanel == null)
            {
                tutorialPromptPanel = TitleScreenBuilder.EnsureTutorialPromptPanel(canvas.transform);
            }

            if (overwritePromptPanel == null)
            {
                overwritePromptPanel = TitleScreenBuilder.EnsureOverwritePromptPanel(canvas.transform);
            }
        }

        private void BindGameModePanelControls()
        {
            if (gameModePanel == null) return;

            BindButton(gameModePanel.transform, "ModalWindow/Btn_StoryMode", OnClickStoryMode);
            BindButton(gameModePanel.transform, "ModalWindow/Btn_EndlessMode", OnClickEndlessMode);
            BindButton(gameModePanel.transform, "ModalWindow/Btn_ChallengeMode", OnClickChallengeMode);
            BindButton(gameModePanel.transform, "ModalWindow/Btn_Close", CloseGameModePanel);
        }

        private static void BindButton(Transform root, string path, UnityEngine.Events.UnityAction action)
        {
            Button button = root.Find(path)?.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void BindLoadPanelControls()
        {
            if (loadGamePanel == null) return;

            TMP_Text title = loadGamePanel.transform.Find("ModalWindow/Txt_Title")?.GetComponent<TMP_Text>();
            TMP_Text subtitle = loadGamePanel.transform.Find("ModalWindow/Txt_Subtitle")?.GetComponent<TMP_Text>();
            if (title != null) title.text = allowCreatingStorySlot ? "STORY MODE" : "CONTINUE";
            if (subtitle != null)
            {
                subtitle.text = allowCreatingStorySlot
                    ? "저장 슬롯을 선택하세요. 빈 슬롯에서는 새 이야기가 시작됩니다."
                    : "이어서 플레이할 스토리 저장 기록을 선택하세요.";
            }

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
                    button.interactable = hasSave || allowCreatingStorySlot;
                    button.onClick.AddListener(() => OnClickLoadSlot(slotIndex));
                }

                TMP_Text state = slot.Find("State")?.GetComponent<TMP_Text>();
                if (state != null)
                {
                    if (allowCreatingStorySlot)
                    {
                        state.text = hasSave ? "OVERWRITE" : "NEW STORY";
                    }
                    else
                    {
                        state.text = hasSave ? "CONTINUE" : "EMPTY SLOT";
                    }
                }
            }
        }

        private void OpenStorySlotPanel()
        {
            if (gameModePanel != null) gameModePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (loadGamePanel == null) return;

            BindLoadPanelControls();
            loadGamePanel.SetActive(true);
            loadGamePanel.transform.SetAsLastSibling();
        }

        private void BindSettingsPanelControls()
        {
            if (settingsPanel == null) return;
            
            Transform modalWindow = settingsPanel.transform.Find("ModalWindow");
            if (modalWindow != null && modalWindow.Find("Btn_FPS") == null)
            {
                InjectFPSSettingsUI(modalWindow);
            }

            Button close = settingsPanel.transform.Find("ModalWindow/Btn_Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(CloseSettingsPanel);
            }

            BindVolumeSlider(settingsPanel.transform, "ModalWindow/Slider_BGM", "BGMVolume");
            BindVolumeSlider(settingsPanel.transform, "ModalWindow/Slider_SFX", "SFXVolume");
        }

        private void InjectFPSSettingsUI(Transform window)
        {
            Transform bgmLabel = window.Find("BGMLabel");
            Transform bgmSlider = window.Find("Slider_BGM");
            Transform sfxLabel = window.Find("SFXLabel");
            Transform sfxSlider = window.Find("Slider_SFX");
            
            Transform fpsLabel = null;
            if (sfxLabel != null)
            {
                fpsLabel = Instantiate(sfxLabel, window);
                fpsLabel.name = "FPSLabel";
                var text = fpsLabel.GetComponent<TMP_Text>();
                if (text != null) text.text = "MAX FPS (FRAME LIMIT)";
            }

            if (bgmLabel != null) ShiftRectY(bgmLabel, 0.08f);
            if (bgmSlider != null) ShiftRectY(bgmSlider, 0.08f);
            if (sfxLabel != null) ShiftRectY(sfxLabel, 0.11f);
            if (sfxSlider != null) ShiftRectY(sfxSlider, 0.11f);
            
            if (fpsLabel != null) ShiftRectY(fpsLabel, -0.07f);
            
            Transform closeBtn = window.Find("Btn_Close");
            if (closeBtn != null)
            {
                Transform fpsBtn = Instantiate(closeBtn, window);
                fpsBtn.name = "Btn_FPS";
                var text = fpsBtn.GetComponentInChildren<TMP_Text>();
                int currentFps = FXOverdose.Core.SystemSettingsManager.GetCurrentFPS();
                if (text != null) text.text = $"{currentFps} FPS";
                
                RectTransform rt = fpsBtn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.10f, 0.25f);
                rt.anchorMax = new Vector2(0.90f, 0.32f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                
                Button btn = fpsBtn.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    int current = FXOverdose.Core.SystemSettingsManager.GetCurrentFPS();
                    var options = FXOverdose.Core.SystemSettingsManager.FpsOptions;
                    int idx = System.Array.IndexOf(options, current);
                    if (idx < 0) idx = 1;
                    idx = (idx + 1) % options.Length;
                    int nextFps = options[idx];
                    FXOverdose.Core.SystemSettingsManager.SetFPS(nextFps);
                    if (text != null) text.text = $"{nextFps} FPS";
                });
            }
        }
        
        private void ShiftRectY(Transform t, float amount)
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(rt.anchorMin.x, rt.anchorMin.y + amount);
                rt.anchorMax = new Vector2(rt.anchorMax.x, rt.anchorMax.y + amount);
            }
        }

        private static void BindVolumeSlider(Transform panel, string path, string key)
        {
            Slider slider = panel.Find(path)?.GetComponent<Slider>();
            if (slider == null) return;

            slider.onValueChanged.RemoveAllListeners();
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 0.8f));
            slider.onValueChanged.AddListener(value => {
                PlayerPrefs.SetFloat(key, value);
                if (FXOverdose.Core.AudioManager.Instance != null)
                {
                    if (key == "BGMVolume") FXOverdose.Core.AudioManager.Instance.SetBGMVolume(value);
                    else if (key == "SFXVolume") FXOverdose.Core.AudioManager.Instance.SetSFXVolume(value);
                }
            });
        }

        private void BindTutorialPromptControls()
        {
            if (tutorialPromptPanel == null) return;

            Button btnYes = tutorialPromptPanel.transform.Find("ModalWindow/Btn_Yes")?.GetComponent<Button>();
            Button btnNo = tutorialPromptPanel.transform.Find("ModalWindow/Btn_No")?.GetComponent<Button>();

            if (btnYes != null)
            {
                btnYes.onClick.RemoveAllListeners();
                btnYes.onClick.AddListener(OnClickTutorialYes);
            }

            if (btnNo != null)
            {
                btnNo.onClick.RemoveAllListeners();
                btnNo.onClick.AddListener(OnClickTutorialNo);
            }
        }

        private void BindOverwritePromptControls()
        {
            if (overwritePromptPanel == null) return;

            Button btnYes = overwritePromptPanel.transform.Find("ModalWindow/Btn_Yes")?.GetComponent<Button>();
            Button btnNo = overwritePromptPanel.transform.Find("ModalWindow/Btn_No")?.GetComponent<Button>();

            if (btnYes != null)
            {
                btnYes.onClick.RemoveAllListeners();
                btnYes.onClick.AddListener(OnClickOverwriteYes);
            }

            if (btnNo != null)
            {
                btnNo.onClick.RemoveAllListeners();
                btnNo.onClick.AddListener(OnClickOverwriteNo);
            }
        }

        public void OnClickLoadSlot(int slotIndex)
        {
            if (SaveLoadManager.Instance == null)
            {
                Debug.LogWarning("[MainMenuController] SaveLoadManager를 찾지 못했습니다.");
                return;
            }

            if (allowCreatingStorySlot)
            {
                // 새 게임 모드 (빈 슬롯이거나 기존 세이브 덮어쓰기)
                pendingSlotIndex = slotIndex;
                if (loadGamePanel != null) loadGamePanel.SetActive(false);
                
                if (SaveLoadManager.Instance.HasSave(slotIndex))
                {
                    if (overwritePromptPanel != null)
                    {
                        overwritePromptPanel.SetActive(true);
                        overwritePromptPanel.transform.SetAsLastSibling();
                    }
                    else
                    {
                        OnClickOverwriteYes();
                    }
                }
                else
                {
                    ProceedToTutorialPrompt();
                }
            }
            else
            {
                // 이어하기 모드 (반드시 세이브 파일이 있어야 함)
                if (SaveLoadManager.Instance.HasSave(slotIndex))
                {
                    if (!SaveLoadManager.Instance.PrepareLoadGame(slotIndex))
                        return;
                    LoadGameFlow();
                }
                else
                {
                    Debug.LogWarning($"[MainMenuController] 슬롯 {slotIndex + 1}에 저장 데이터가 없습니다.");
                    return;
                }
            }
        }

        private void OnClickOverwriteYes()
        {
            if (overwritePromptPanel != null) overwritePromptPanel.SetActive(false);
            ProceedToTutorialPrompt();
        }

        private void OnClickOverwriteNo()
        {
            if (overwritePromptPanel != null) overwritePromptPanel.SetActive(false);
            if (loadGamePanel != null)
            {
                loadGamePanel.SetActive(true);
                loadGamePanel.transform.SetAsLastSibling();
            }
        }

        private void ProceedToTutorialPrompt()
        {
            if (tutorialPromptPanel != null)
            {
                tutorialPromptPanel.SetActive(true);
                tutorialPromptPanel.transform.SetAsLastSibling();
            }
            else
            {
                OnClickTutorialNo();
            }
        }

        private void OnClickTutorialYes()
        {
            if (tutorialPromptPanel != null) tutorialPromptPanel.SetActive(false);
            SaveLoadManager.Instance.PrepareNewGame(GameMode.Story, pendingSlotIndex);
            
            if (Application.CanStreamedLevelBeLoaded("LoadingScene") && Application.CanStreamedLevelBeLoaded("tutorial"))
            {
                LoadingScreenController.TargetSceneToLoad = "tutorial";
                SceneManager.LoadScene("LoadingScene");
            }
            else if (Application.CanStreamedLevelBeLoaded("tutorial"))
            {
                SceneManager.LoadScene("tutorial");
            }
            else
            {
                Debug.LogWarning("[MainMenuController] tutorial 씬을 찾을 수 없어 GameScene으로 진입합니다.");
                LoadGameFlow();
            }
        }

        private void OnClickTutorialNo()
        {
            if (tutorialPromptPanel != null) tutorialPromptPanel.SetActive(false);
            SaveLoadManager.Instance.PrepareNewGame(GameMode.Story, pendingSlotIndex);
            LoadGameFlow();
        }

        private static void StartNewMode(GameMode mode, int storySlotIndex = 0)
        {
            SaveLoadManager manager = SaveLoadManager.Instance;
            if (manager == null)
            {
                manager = new GameObject("SaveLoadManager").AddComponent<SaveLoadManager>();
            }

            if (manager == null)
            {
                Debug.LogError($"[MainMenuController] {mode} 모드 세션을 준비하지 못해 씬 전환을 중단합니다.");
                return;
            }

            manager.PrepareNewGame(mode, storySlotIndex);
            LoadGameFlow();
        }

        private static void LoadGameFlow()
        {
            LoadingScreenController.TargetSceneToLoad = "GameScene";

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
