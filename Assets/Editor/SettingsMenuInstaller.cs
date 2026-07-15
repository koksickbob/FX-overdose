#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 우측 상단 SettingsIcon 자리에 픽셀 설정 버튼을 설치합니다.</summary>
public static class SettingsMenuInstaller
{
    private const string SpritePath = "Assets/Img/UI/SettingsGearUnified.png";
    private const string AppliedKey = "FXOverdose_SettingsMenu_GearSprite_v3";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += TryApply;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryApply;
    }

    private static void TryApply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        GameObject settingsObject = FindSceneObject("SettingsIcon");
        if (settingsObject == null) return;
        if (EditorPrefs.GetBool(AppliedKey, false) && settingsObject.GetComponent<SettingsMenuController>() != null) return;

        if (Apply(false)) EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Install Settings Menu")]
    public static void ApplyFromMenu() => Apply(true);

    private static bool Apply(bool showResult)
    {
        PrepareSprite();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        GameObject settingsObject = FindSceneObject("SettingsIcon");
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (sprite == null || settingsObject == null || gameManager == null) return false;

        settingsObject.layer = 5;
        TMP_Text oldGlyph = settingsObject.GetComponent<TMP_Text>();
        if (oldGlyph != null) oldGlyph.enabled = false;

        // SettingsIcon에는 이미 TMP Graphic이 있으므로 Image는 전용 자식에 둡니다.
        // 같은 오브젝트에 TMP와 Image를 함께 추가하면 Unity가 Image 생성을 거부합니다.
        Transform visualTransform = settingsObject.transform.Find("SettingsButtonVisual");
        GameObject visualObject;
        if (visualTransform == null)
        {
            visualObject = new GameObject("SettingsButtonVisual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(visualObject, "Create Settings Button Visual");
            visualObject.transform.SetParent(settingsObject.transform, false);
        }
        else
        {
            visualObject = visualTransform.gameObject;
        }

        visualObject.layer = 5;
        RectTransform visualRect = visualObject.GetComponent<RectTransform>();
        visualRect.anchorMin = Vector2.zero;
        visualRect.anchorMax = Vector2.one;
        visualRect.offsetMin = new Vector2(12f, 12f);
        visualRect.offsetMax = new Vector2(-12f, -12f);

        Image image = visualObject.GetComponent<Image>();
        if (image == null) image = Undo.AddComponent<Image>(visualObject);
        if (image == null)
        {
            Debug.LogError("[SettingsMenuInstaller] 설정 버튼 Image를 생성하지 못했습니다.", settingsObject);
            return false;
        }
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;

        // 이전 버전의 폰트 기반 톱니는 글리프 미지원 시 사라질 수 있으므로
        // 전용 투명 스프라이트만 표시합니다.
        Transform glyphTransform = settingsObject.transform.Find("SettingsGearGlyph");
        if (glyphTransform != null) glyphTransform.gameObject.SetActive(false);
        visualObject.transform.SetAsLastSibling();

        Button button = settingsObject.GetComponent<Button>();
        if (button == null) button = Undo.AddComponent<Button>(settingsObject);
        if (button == null)
        {
            Debug.LogError("[SettingsMenuInstaller] 설정 Button을 생성하지 못했습니다.", settingsObject);
            return false;
        }
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.82f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.63f, 0.73f, 0.88f, 1f);
        button.colors = colors;

        SettingsMenuController controller = settingsObject.GetComponent<SettingsMenuController>();
        if (controller == null) controller = Undo.AddComponent<SettingsMenuController>(settingsObject);
        if (controller == null)
        {
            Debug.LogError("[SettingsMenuInstaller] SettingsMenuController를 생성하지 못했습니다.", settingsObject);
            return false;
        }
        SerializedObject serialized = new(controller);
        serialized.FindProperty("settingsButton").objectReferenceValue = button;
        serialized.FindProperty("gameManager").objectReferenceValue = gameManager;
        serialized.FindProperty("font").objectReferenceValue = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(settingsObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (showResult)
            EditorUtility.DisplayDialog("설정 메뉴 적용 완료", "톱니바퀴 버튼, 일시정지 팝업, 게임 종료 버튼을 연결했습니다.", "확인");

        return true;
    }

    private static void PrepareSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 512f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static GameObject FindSceneObject(string objectName) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(go => go.scene.IsValid() && go.name == objectName);
}
#endif
