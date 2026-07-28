using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using FXOverdose.UI;

public class ComicUIBuilder : EditorWindow
{
    [MenuItem("FX Overdose/Build Comic UI Canvas")]
    public static void BuildUI()
    {
        // 1. Create Canvas
        GameObject canvasGO = new GameObject("ComicCutsceneCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 화면 최상단

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        canvasGO.AddComponent<GraphicRaycaster>();
        CanvasGroup group = canvasGO.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        ComicCutsceneController controller = canvasGO.AddComponent<ComicCutsceneController>();

        // 2. Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.black;
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 3. Comic Image Display
        GameObject imgGO = new GameObject("ComicImageDisplay");
        imgGO.transform.SetParent(canvasGO.transform, false);
        Image comicImg = imgGO.AddComponent<Image>();
        comicImg.preserveAspect = true;
        RectTransform imgRect = imgGO.GetComponent<RectTransform>();
        imgRect.anchorMin = Vector2.zero;
        imgRect.anchorMax = Vector2.one;
        imgRect.offsetMin = Vector2.zero;
        imgRect.offsetMax = Vector2.zero;

        // 4. Next Button (Full Screen Invisible)
        GameObject nextBtnGO = new GameObject("NextPanelButton");
        nextBtnGO.transform.SetParent(canvasGO.transform, false);
        Image nextBtnImg = nextBtnGO.AddComponent<Image>();
        nextBtnImg.color = new Color(0, 0, 0, 0); // 투명
        Button nextBtn = nextBtnGO.AddComponent<Button>();
        nextBtn.transition = Selectable.Transition.None;
        RectTransform nextRect = nextBtnGO.GetComponent<RectTransform>();
        nextRect.anchorMin = Vector2.zero;
        nextRect.anchorMax = Vector2.one;
        nextRect.offsetMin = Vector2.zero;
        nextRect.offsetMax = Vector2.zero;

        // 5. Skip Button
        GameObject skipBtnGO = new GameObject("SkipButton");
        skipBtnGO.transform.SetParent(canvasGO.transform, false);
        Image skipImg = skipBtnGO.AddComponent<Image>();
        skipImg.color = new Color32(64, 68, 76, 165);
        Button skipBtn = skipBtnGO.AddComponent<Button>();
        RectTransform skipRect = skipBtnGO.GetComponent<RectTransform>();
        skipRect.anchorMin = new Vector2(1, 1);
        skipRect.anchorMax = new Vector2(1, 1);
        skipRect.pivot = new Vector2(1, 1);
        skipRect.anchoredPosition = new Vector2(-28, -28);
        skipRect.sizeDelta = new Vector2(154, 52);

        // Skip Button Text
        GameObject skipTextGO = new GameObject("Text");
        skipTextGO.transform.SetParent(skipBtnGO.transform, false);
        TMPro.TextMeshProUGUI skipText = skipTextGO.AddComponent<TMPro.TextMeshProUGUI>();
        skipText.text = "SKIP  >";
        skipText.fontSize = 22;
        skipText.alignment = TMPro.TextAlignmentOptions.Center;
        skipText.color = Color.white;
        RectTransform skipTextRect = skipTextGO.GetComponent<RectTransform>();
        skipTextRect.anchorMin = Vector2.zero;
        skipTextRect.anchorMax = Vector2.one;
        skipTextRect.offsetMin = new Vector2(10, 4);
        skipTextRect.offsetMax = new Vector2(-10, -4);

        // Link fields in controller using serialized object
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("comicImageDisplay").objectReferenceValue = comicImg;
        so.FindProperty("skipButton").objectReferenceValue = skipBtn;
        so.FindProperty("nextPanelButton").objectReferenceValue = nextBtn;
        so.FindProperty("fadeDuration").floatValue = 0.5f;
        so.ApplyModifiedProperties();

        // Save Scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(canvasGO.scene);

        Debug.Log("[ComicUIBuilder] ComicCutsceneCanvas UI 생성 및 연결이 완벽하게 끝났습니다!");
    }
}
