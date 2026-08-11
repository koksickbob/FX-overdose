using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.YomiRoom;

namespace FXOverdose.EditorTools
{
    public class YomiRoomTestSceneBuilder
    {
        [MenuItem("FX Overdose/Build YomiRoom Test Scene")]
        public static void BuildScene()
        {
            // 1. 씬 저장 경로 확인 및 생성
            string folderPath = "Assets/Scenes/DatingSim";
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "DatingSim");
            }

            string scenePath = $"{folderPath}/YomiRoom_Test.unity";
            
            // 2. 새 씬 생성
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            
            // 3. 전역 매니저 세팅 (Logic & Data Layer)
            GameObject managersObj = new GameObject("Managers");
            managersObj.AddComponent<DatingTimeManager>();
            managersObj.AddComponent<YomiRoomManager>();

            // 4. UI 캔버스 세팅
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem 세팅
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // UI Controller 스크립트 부착
            YomiRoomUIController uiController = canvasObj.AddComponent<YomiRoomUIController>();

            // 5. 버튼 및 텍스트 생성
            GameObject buttonPanel = CreatePanel("ButtonPanel", canvasObj.transform, new Vector2(200, 400), new Vector2(-120, 0));
            Button freeChatBtn = CreateButton("FreeChatButton", buttonPanel.transform, "Free Chat");
            Button restBtn = CreateButton("RestButton", buttonPanel.transform, "Rest");
            Button worldMapBtn = CreateButton("WorldMapButton", buttonPanel.transform, "World Map");
            Button tradingBtn = CreateButton("TradingButton", buttonPanel.transform, "Trading");
            
            // 10px 간격으로 정렬
            for(int i=0; i<4; i++) {
                buttonPanel.transform.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 150 - (i * 60));
            }

            GameObject statusPanel = CreatePanel("StatusPanel", canvasObj.transform, new Vector2(300, 400), new Vector2(170, 0));
            TextMeshProUGUI staminaTxt = CreateText("StaminaText", statusPanel.transform, "Stamina: 100/100");
            TextMeshProUGUI timeSlotTxt = CreateText("TimeSlotText", statusPanel.transform, "Time Slots: 5");
            TextMeshProUGUI affectionTxt = CreateText("AffectionText", statusPanel.transform, "Affection: 0");
            TextMeshProUGUI obsessionTxt = CreateText("ObsessionText", statusPanel.transform, "Obsession: 0");
            TextMeshProUGUI roomStateTxt = CreateText("RoomStateText", statusPanel.transform, "State: Idle");

            for(int i=0; i<5; i++) {
                statusPanel.transform.GetChild(i).GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 150 - (i * 50));
            }

            // 5.5 채팅 UI 패널 생성
            GameObject chatPanelObj = CreatePanel("ChatPanel", canvasObj.transform, new Vector2(600, 500), new Vector2(0, 0));
            chatPanelObj.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            
            TextMeshProUGUI chatLogTxt = CreateText("ChatLogText", chatPanelObj.transform, "대화를 시작하세요.\n");
            chatLogTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 380);
            chatLogTxt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 40);
            chatLogTxt.textWrappingMode = TextWrappingModes.Normal;
            chatLogTxt.alignment = TextAlignmentOptions.TopLeft;

            // InputField 생성을 위해 Image가 필요함
            GameObject inputFieldObj = new GameObject("ChatInputField");
            inputFieldObj.transform.SetParent(chatPanelObj.transform, false);
            RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(440, 40);
            inputRect.anchoredPosition = new Vector2(-60, -210);
            Image inputBg = inputFieldObj.AddComponent<Image>();
            inputBg.color = Color.white;

            TMP_InputField inputField = inputFieldObj.AddComponent<TMP_InputField>();
            
            // InputField의 텍스트 컴포넌트
            GameObject inputTextObj = new GameObject("Text Area");
            inputTextObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = new Vector2(-10, -10); // padding
            TextMeshProUGUI inputText = inputTextObj.AddComponent<TextMeshProUGUI>();
            inputText.color = Color.black;
            inputField.textComponent = inputText;

            Button sendBtn = CreateButton("SendButton", chatPanelObj.transform, "Send");
            sendBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 40);
            sendBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(230, -210);

            Button closeBtn = CreateButton("CloseButton", chatPanelObj.transform, "X");
            closeBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);
            closeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(280, 230);
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().color = Color.red;

            chatPanelObj.SetActive(false); // 기본 비활성화

            // 6. UI Controller 레퍼런스 바인딩 (SerializedObject 활용)
            SerializedObject so = new SerializedObject(uiController);
            so.FindProperty("freeChatButton").objectReferenceValue = freeChatBtn;
            so.FindProperty("restButton").objectReferenceValue = restBtn;
            so.FindProperty("worldMapButton").objectReferenceValue = worldMapBtn;
            so.FindProperty("tradingButton").objectReferenceValue = tradingBtn;

            so.FindProperty("staminaText").objectReferenceValue = staminaTxt;
            so.FindProperty("timeSlotText").objectReferenceValue = timeSlotTxt;
            so.FindProperty("affectionText").objectReferenceValue = affectionTxt;
            so.FindProperty("obsessionText").objectReferenceValue = obsessionTxt;
            so.FindProperty("roomStateText").objectReferenceValue = roomStateTxt;

            so.FindProperty("chatPanel").objectReferenceValue = chatPanelObj;
            so.FindProperty("chatLogText").objectReferenceValue = chatLogTxt;
            so.FindProperty("chatInputField").objectReferenceValue = inputField;
            so.FindProperty("chatSendButton").objectReferenceValue = sendBtn;
            so.FindProperty("closeChatButton").objectReferenceValue = closeBtn;

            so.ApplyModifiedProperties();

            // 7. 씬 저장
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[FX Overdose] Test Scene successfully created at: {scenePath}");
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0.5f); // 반투명 배경 추가
            return panel;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160, 40);
            
            Image image = btnObj.AddComponent<Image>();
            image.color = Color.white;
            Button button = btnObj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 20;

            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string label)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250, 40);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.fontSize = 24;

            return tmp;
        }
    }
}
