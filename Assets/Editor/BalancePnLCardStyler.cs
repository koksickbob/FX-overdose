#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>BALANCE/P&amp;L 카드를 픽셀 프레임과 PF스타더스트 폰트로 꾸밉니다.</summary>
public static class BalancePnLCardStyler
{
    private const string FramePath = "Assets/Img/StatusCardFrame.png";
    private const string FontPath = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
    private const string AppliedKey = "FXOverdose_BalancePnLStyle_v3";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AppliedKey, false)) return;
            if (Find("BalanceCard") == null || Find("PnLCard") == null) return;
            Apply(false);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(AppliedKey, true);
        };
    }

    [MenuItem("Tools/FX OVERDOSE/Style BALANCE and P&L Cards")]
    public static void ApplyFromMenu() => Apply(true);

    public static void ApplySilently() => Apply(false);

    private static void Apply(bool showResult)
    {
        GameObject balanceCard = Find("BalanceCard");
        GameObject pnlCard = Find("PnLCard");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? TMP_Settings.defaultFontAsset;
        Sprite frame = PrepareFrameSprite();

        if (balanceCard == null || pnlCard == null || font == null || frame == null)
        {
            if (showResult)
                EditorUtility.DisplayDialog("카드 적용 실패", "BALANCE/P&L 카드, 폰트 또는 프레임 이미지를 찾지 못했습니다.", "확인");
            return;
        }

        PinTopBarAcrossScreen(balanceCard.transform.parent);
        StyleBalance(balanceCard, font, frame);
        StylePnL(pnlCard, font, frame);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        if (showResult)
        {
            Selection.activeGameObject = balanceCard;
            EditorUtility.DisplayDialog("카드 적용 완료", "BALANCE와 P&L 카드에 픽셀 프레임과 PF스타더스트 폰트를 적용했습니다.", "확인");
        }
    }

    private static void PinTopBarAcrossScreen(Transform topBar)
    {
        GameObject canvas = Find("TradingViewCanvas");
        if (topBar == null || canvas == null) return;

        Undo.SetTransformParent(topBar, canvas.transform, "Move Top Status Bar");
        RectTransform rect = topBar.GetComponent<RectTransform>();
        Undo.RecordObject(rect, "Stretch Top Status Bar");
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 105f);
        rect.offsetMin = new Vector2(0f, -105f);
        rect.offsetMax = Vector2.zero;

        HorizontalLayoutGroup layout = topBar.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 4);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        GameObject chart = Find("ChartMainPanel");
        if (chart != null)
        {
            RectTransform chartRect = chart.GetComponent<RectTransform>();
            Undo.RecordObject(chartRect, "Keep Chart Below Top Bar");
            chartRect.anchorMax = new Vector2(chartRect.anchorMax.x, 0.90f);
        }
    }

    private static void StyleBalance(GameObject card, TMP_FontAsset font, Sprite frame)
    {
        StyleCard(card, frame, 310f);

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(card);
        layout.padding = new RectOffset(30, 22, 15, 14);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = Find("BalanceTitle")?.GetComponent<TMP_Text>();
        TMP_Text value = Find("BalanceValue")?.GetComponent<TMP_Text>();
        ConfigureText(title, font, 21f, new Color(0.74f, 0.78f, 0.86f, 1f));
        ConfigureText(value, font, 34f, Color.white);
        SetHeight(title, 26f);
        SetHeight(value, 48f);
    }

    private static void StylePnL(GameObject card, TMP_FontAsset font, Sprite frame)
    {
        StyleCard(card, frame, 420f);
        LayoutElement pnlSize = GetOrAdd<LayoutElement>(card);
        pnlSize.minHeight = 90f;
        pnlSize.preferredHeight = 90f;

        HorizontalLayoutGroup layout = card.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = Undo.AddComponent<HorizontalLayoutGroup>(card);
        layout.padding = new RectOffset(30, 28, 14, 13);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        GameObject textGroup = Find("PnLTextGroup");
        if (textGroup != null)
        {
            LayoutElement groupSize = GetOrAdd<LayoutElement>(textGroup);
            groupSize.preferredWidth = 175f;
            VerticalLayoutGroup groupLayout = textGroup.GetComponent<VerticalLayoutGroup>();
            if (groupLayout != null)
            {
                groupLayout.padding = new RectOffset(0, 0, 0, 0);
                groupLayout.spacing = 0f;
                groupLayout.childForceExpandHeight = false;
            }
        }

        TMP_Text title = Find("PnLTitle")?.GetComponent<TMP_Text>();
        TMP_Text percentage = Find("PnLPct")?.GetComponent<TMP_Text>();
        TMP_Text amount = Find("PnLAmt")?.GetComponent<TMP_Text>();
        ConfigureText(title, font, 21f, new Color(0.34f, 0.90f, 0.43f, 1f));
        ConfigureText(percentage, font, 34f, new Color(0.26f, 0.84f, 0.38f, 1f));
        SetHeight(title, 26f);
        SetHeight(percentage, 48f);
        if (amount != null) amount.gameObject.SetActive(false);

        GameObject sparkline = Find("SparklineContainer");
        if (sparkline != null)
        {
            LayoutElement sparkSize = GetOrAdd<LayoutElement>(sparkline);
            sparkSize.preferredWidth = 160f;
            sparkSize.preferredHeight = 64f;
            sparkSize.flexibleWidth = 0f;
        }
    }

    private static void StyleCard(GameObject card, Sprite frame, float width)
    {
        LayoutElement size = GetOrAdd<LayoutElement>(card);
        size.minWidth = width;
        size.preferredWidth = width;
        size.preferredHeight = 96f;
        size.flexibleWidth = 0f;

        Image image = GetOrAdd<Image>(card);
        image.sprite = frame;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;

        Outline oldOutline = card.GetComponent<Outline>();
        if (oldOutline != null) oldOutline.enabled = false;
    }

    private static void ConfigureText(TMP_Text text, TMP_FontAsset font, float size, Color color)
    {
        if (text == null) return;
        Undo.RecordObject(text, "Apply PF Stardust Font");
        text.font = font;
        text.fontStyle = FontStyles.Normal;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 17f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = color;
        text.raycastTarget = false;
    }

    private static void SetHeight(TMP_Text text, float height)
    {
        if (text == null) return;
        LayoutElement size = GetOrAdd<LayoutElement>(text.gameObject);
        size.preferredHeight = height;
    }

    private static Sprite PrepareFrameSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(FramePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static GameObject Find(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene.IsValid() && go.name == objectName);
    }
}
#endif
