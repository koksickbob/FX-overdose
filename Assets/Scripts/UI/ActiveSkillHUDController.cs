using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

/// <summary>SHOP 버튼 위의 통합 스킬 버튼과 스킬 정보 팝업을 관리합니다.</summary>
public sealed class ActiveSkillHUDController : MonoBehaviour
{
    private const float InventoryRightMargin = UIStrokeStyle.ScreenEdgeMargin;
    private const float ShopButtonGap = 12f;
    private static readonly Vector2 FallbackMainButtonSize = new(260f, 86f);

    private static readonly SkillType[] SkillOrder =
    {
        SkillType.ChartStudy,
        SkillType.CubePatience,
        SkillType.BookJudgment
    };

    private TraderLevelSystem levelSystem;
    private RectTransform skillRow;
    private Button mainSkillButton;
    private Image mainSkillButtonImage;
    private RectTransform shopButtonRect;
    private GameObject infoOverlay;
    private readonly Dictionary<SkillType, SkillCardView> skillCards = new();
    private Button upgradeButton;
    private TMP_Text upgradeFeedbackText;
    private GameObject timeTransitionOverlay;
    private CanvasGroup timeTransitionGroup;
    private TMP_Text timeTransitionTitle;
    private TMP_Text timeTransitionClock;
    private bool isUpgradeSequencePlaying;

    private sealed class SkillCardView
    {
        public TMP_Text Level;
        public TMP_Text Effect;
        public TMP_Text Cost;
        public Button UpgradeButton;
        public TMP_Text UpgradeLabel;
    }

    private void Awake()
    {
        EnsureLevelSystem();
        BuildSkillRow();
        BuildInfoPopup();
        BuildTimeTransitionOverlay();
        infoOverlay.SetActive(false);
        timeTransitionOverlay.SetActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeAfterLayout());
    }

    private IEnumerator InitializeAfterLayout()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        ResolveShopButton();
        LayoutAboveShopButton();

        levelSystem = TraderLevelSystem.Instance;
        if (levelSystem != null)
        {
            levelSystem.OnSkillLevelChanged -= OnSkillLevelChanged;
            levelSystem.OnSkillLevelChanged += OnSkillLevelChanged;
        }
        RefreshButtonLevels();
    }

    private void OnDisable()
    {
        if (levelSystem != null) levelSystem.OnSkillLevelChanged -= OnSkillLevelChanged;
    }

    private void LateUpdate()
    {
        if (shopButtonRect == null)
            ResolveShopButton();
        LayoutAboveShopButton();
    }

    private void EnsureLevelSystem()
    {
        levelSystem = TraderLevelSystem.Instance;
        if (levelSystem != null) return;

        GameObject host = GameObject.Find("TraderLevelSystem") ?? new GameObject("TraderLevelSystem");
        levelSystem = host.GetComponent<TraderLevelSystem>() ?? host.AddComponent<TraderLevelSystem>();
    }

    private void BuildSkillRow()
    {
        // SHOP 버튼과 동일하게 외곽 Outline이 잘리지 않도록 루트에는 RectMask2D를 두지 않습니다.
        GameObject row = new("ActiveSkillButtonRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        row.transform.SetParent(transform, false);
        skillRow = row.GetComponent<RectTransform>();
        skillRow.anchorMin = skillRow.anchorMax = new Vector2(0.5f, 0.5f);
        skillRow.pivot = new Vector2(1f, 0f);
        skillRow.sizeDelta = FallbackMainButtonSize;

        mainSkillButtonImage = row.GetComponent<Image>();
        mainSkillButtonImage.color = Color.white;

        Outline outline = row.GetComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.72f, 0.84f, 1f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        mainSkillButton = row.GetComponent<Button>();
        mainSkillButton.targetGraphic = mainSkillButtonImage;
        mainSkillButton.onClick.AddListener(OpenSkillOverview);

        Image iconBackdrop = CreateImage(row.transform, "IconBackdrop");
        iconBackdrop.color = new Color32(4, 12, 35, 255);
        iconBackdrop.preserveAspect = false;
        SetRect(iconBackdrop.rectTransform, new Vector2(0.065f, 0.18f), new Vector2(0.285f, 0.82f));

        Image mainIcon = CreateImage(row.transform, "Icon");
        mainIcon.sprite = Resources.Load<Sprite>("UI/Skills/SkillMenuIcon");
        mainIcon.preserveAspect = true;
        SetRect(mainIcon.rectTransform, new Vector2(0.085f, 0.20f), new Vector2(0.265f, 0.80f));

        TMP_Text label = CreateText(row.transform, "Label", "SKILL", 29f, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, new Vector2(0.30f, 0f), new Vector2(0.94f, 1f));
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.86f, 0.98f, 1f, 1f);
        label.characterSpacing = 1.5f;

    }

    private void ResolveShopButton()
    {
        GameObject shopButton = GameObject.Find("ShopOpenButton");
        shopButtonRect = shopButton != null ? shopButton.GetComponent<RectTransform>() : null;
        ApplyShopButtonStyle(shopButton);
    }

    private void ApplyShopButtonStyle(GameObject shopButton)
    {
        if (shopButton == null || mainSkillButton == null || mainSkillButtonImage == null) return;

        Image shopImage = shopButton.GetComponent<Image>();
        Button shopUiButton = shopButton.GetComponent<Button>();
        if (shopImage != null)
        {
            mainSkillButtonImage.sprite = shopImage.sprite;
            mainSkillButtonImage.type = shopImage.type;
            mainSkillButtonImage.pixelsPerUnitMultiplier = shopImage.pixelsPerUnitMultiplier;
        }
        if (shopUiButton != null)
        {
            mainSkillButton.transition = shopUiButton.transition;
            mainSkillButton.colors = shopUiButton.colors;
            mainSkillButton.spriteState = shopUiButton.spriteState;
        }
    }

    private void LayoutAboveShopButton()
    {
        if (skillRow == null) return;

        RectTransform canvasRect = GetComponent<RectTransform>();
        skillRow.anchorMin = skillRow.anchorMax = new Vector2(1f, 0f);
        skillRow.pivot = new Vector2(1f, 0f);

        if (canvasRect != null && shopButtonRect != null)
        {
            Vector3[] shopCorners = new Vector3[4];
            shopButtonRect.GetWorldCorners(shopCorners);
            Vector3 shopBottomLeft = canvasRect.InverseTransformPoint(shopCorners[0]);
            Vector3 shopTopRight = canvasRect.InverseTransformPoint(shopCorners[2]);
            Vector3 shopBottomRight = canvasRect.InverseTransformPoint(shopCorners[3]);
            skillRow.sizeDelta = new Vector2(
                Mathf.Abs(shopBottomRight.x - shopBottomLeft.x),
                Mathf.Abs(shopTopRight.y - shopBottomRight.y));
            skillRow.anchoredPosition = new Vector2(
                shopTopRight.x - canvasRect.rect.xMax,
                shopTopRight.y - canvasRect.rect.yMin + ShopButtonGap);

        }
        else
        {
            // 씬 참조를 아직 찾지 못한 첫 프레임의 안전한 임시 위치입니다.
            skillRow.anchoredPosition = new Vector2(-InventoryRightMargin, 270f);
        }

        skillRow.SetAsLastSibling();
    }

    private void BuildInfoPopup()
    {
        infoOverlay = CreateUIObject("ActiveSkillInfoOverlay", transform, typeof(Canvas), typeof(GraphicRaycaster));
        Stretch(infoOverlay.GetComponent<RectTransform>());
        infoOverlay.GetComponent<Image>().color = new Color(0.043f, 0.059f, 0.098f, 0.78f); // #0B0F19

        Canvas overlayCanvas = infoOverlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 280;

        GameObject panel = CreateUIObject("SkillInfoPanel", infoOverlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        // 해상도와 화면 비율에 관계없이 스킬 상세창이 화면의 약 70%를 차지합니다.
        panelRect.anchorMin = new Vector2(0.15f, 0.15f);
        panelRect.anchorMax = new Vector2(0.85f, 0.85f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color32(15, 23, 42, 252); // #0F172A
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color32(6, 182, 212, 255);
        panelOutline.effectDistance = UIStrokeStyle.EffectDistance;

        TMP_Text overviewTitle = CreateText(panel.transform, "OverviewTitle", "SKILL UPGRADE", 48f, TextAlignmentOptions.Center);
        SetRect(overviewTitle.rectTransform, new Vector2(0.12f, 0.88f), new Vector2(0.88f, 0.97f));
        overviewTitle.color = new Color32(207, 250, 254, 255);
        overviewTitle.fontStyle = FontStyles.Bold;

        for (int i = 0; i < SkillOrder.Length; i++)
        {
            SkillType type = SkillOrder[i];
            float left = 0.025f + i * 0.325f;
            float right = left + 0.30f;

            GameObject card = CreateUIObject($"SkillCard_{type}", panel.transform, typeof(Outline));
            SetRect(card.GetComponent<RectTransform>(), new Vector2(left, 0.10f), new Vector2(right, 0.86f));
            card.GetComponent<Image>().color = new Color32(20, 29, 51, 250);
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color32(6, 182, 212, 210);
            cardOutline.effectDistance = UIStrokeStyle.EffectDistance;

            Image icon = CreateImage(card.transform, "Icon");
            icon.sprite = Resources.Load<Sprite>($"UI/Skills/{GetIconName(type)}");
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, new Vector2(0.34f, 0.72f), new Vector2(0.66f, 0.94f));

            TMP_Text title = CreateText(card.transform, "Title", GetDisplayName(type), 34.5f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0.06f, 0.61f), new Vector2(0.94f, 0.72f));
            title.color = new Color32(207, 250, 254, 255);
            title.fontStyle = FontStyles.Bold;

            SkillCardView view = new();
            view.Level = CreateText(card.transform, "Level", "", 27f, TextAlignmentOptions.Center);
            SetRect(view.Level.rectTransform, new Vector2(0.08f, 0.53f), new Vector2(0.92f, 0.62f));
            view.Level.color = new Color32(6, 182, 212, 255);

            view.Effect = CreateText(card.transform, "Effect", "", 24f, TextAlignmentOptions.TopLeft);
            SetRect(view.Effect.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.52f));
            view.Effect.textWrappingMode = TextWrappingModes.Normal;

            view.Cost = CreateText(card.transform, "Cost", "", 21f, TextAlignmentOptions.Center);
            SetRect(view.Cost.rectTransform, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.28f));
            view.Cost.color = new Color32(234, 179, 8, 255);
            view.Cost.textWrappingMode = TextWrappingModes.Normal;

            view.UpgradeButton = CreateButton(card.transform, "UpgradeButton", "UPGRADE");
            SetRect(view.UpgradeButton.GetComponent<RectTransform>(), new Vector2(0.20f, 0.035f), new Vector2(0.80f, 0.13f));
            view.UpgradeLabel = view.UpgradeButton.GetComponentInChildren<TMP_Text>();
            view.UpgradeLabel.fontSize = 27f;
            view.UpgradeLabel.fontSizeMax = 27f;
            view.UpgradeLabel.fontSizeMin = 18f;
            view.UpgradeButton.onClick.AddListener(() => UpgradeSkill(type));
            skillCards[type] = view;
        }

        upgradeFeedbackText = CreateText(panel.transform, "UpgradeFeedback", "", 21f, TextAlignmentOptions.Center);
        SetRect(upgradeFeedbackText.rectTransform, new Vector2(0.10f, 0.015f), new Vector2(0.90f, 0.085f));
        upgradeFeedbackText.textWrappingMode = TextWrappingModes.Normal;

        Button cornerClose = CreateButton(panel.transform, "CornerClose", "X");
        SetRect(cornerClose.GetComponent<RectTransform>(), new Vector2(0.93f, 0.89f), new Vector2(0.98f, 0.97f));
        cornerClose.onClick.AddListener(CloseInfo);
    }

    private void BuildTimeTransitionOverlay()
    {
        timeTransitionOverlay = CreateUIObject("SkillTimeTransitionOverlay", transform, typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Stretch(timeTransitionOverlay.GetComponent<RectTransform>());
        // 캐릭터의 공부 포즈가 뒤에서 충분히 보이도록 반투명 딤만 적용합니다.
        timeTransitionOverlay.GetComponent<Image>().color = new Color32(3, 8, 20, 190);

        Canvas canvas = timeTransitionOverlay.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 360;
        timeTransitionGroup = timeTransitionOverlay.GetComponent<CanvasGroup>();
        timeTransitionGroup.alpha = 0f;
        timeTransitionGroup.blocksRaycasts = true;

        timeTransitionTitle = CreateText(timeTransitionOverlay.transform, "Activity", "", 32f, TextAlignmentOptions.Center);
        SetRect(timeTransitionTitle.rectTransform, new Vector2(0.2f, 0.52f), new Vector2(0.8f, 0.62f));
        timeTransitionTitle.color = new Color32(207, 250, 254, 255);
        timeTransitionTitle.fontStyle = FontStyles.Bold;

        timeTransitionClock = CreateText(timeTransitionOverlay.transform, "TimeJump", "", 52f, TextAlignmentOptions.Center);
        SetRect(timeTransitionClock.rectTransform, new Vector2(0.15f, 0.39f), new Vector2(0.85f, 0.52f));
        timeTransitionClock.color = new Color32(6, 182, 212, 255);
        timeTransitionClock.fontStyle = FontStyles.Bold;

        TMP_Text hint = CreateText(timeTransitionOverlay.transform, "Hint", "시간이 흐릅니다", 17f, TextAlignmentOptions.Center);
        SetRect(hint.rectTransform, new Vector2(0.25f, 0.33f), new Vector2(0.75f, 0.39f));
        hint.color = new Color32(148, 163, 184, 255);
    }

    private void OpenSkillOverview()
    {
        if (upgradeFeedbackText != null) upgradeFeedbackText.text = "";
        RefreshAllSkillCards();
        infoOverlay.SetActive(true);
        infoOverlay.transform.SetAsLastSibling();
    }

    private void CloseInfo() => infoOverlay.SetActive(false);

    private void RefreshAllSkillCards()
    {
        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null) return;

        foreach (SkillType type in SkillOrder)
        {
            if (!skillCards.TryGetValue(type, out SkillCardView view)) continue;

            int level = levelSystem.GetSkillLevel(type);
            view.Level.text = $"LEVEL {level} / 10";
            view.Effect.text = GetEffectDescription(type);
            view.Cost.text = level >= 10
                ? "MAX LEVEL\n모든 효과가 최대치입니다."
                : $"${levelSystem.GetSkillCost(type):N0}  |  HP -{levelSystem.GetSkillHealthCost(type):N0}\n{levelSystem.GetSkillTimeCostHours(type)}시간 소요";

            bool canUpgrade = levelSystem.CanUpgradeSkill(type, out _);
            view.UpgradeButton.interactable = canUpgrade && !isUpgradeSequencePlaying;
            view.UpgradeLabel.text = level >= 10 ? "MAX LEVEL" : canUpgrade ? "UPGRADE" : "조건 부족";
            view.UpgradeLabel.color = canUpgrade
                ? new Color32(207, 250, 254, 255)
                : new Color32(148, 163, 184, 255);
        }
    }

    private void UpgradeSkill(SkillType type)
    {
        skillCards.TryGetValue(type, out SkillCardView selectedCard);
        upgradeButton = selectedCard?.UpgradeButton;

        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null) return;

        if (isUpgradeSequencePlaying) return;
        if (!levelSystem.CanUpgradeSkill(type, out string reason))
        {
            if (upgradeFeedbackText != null)
            {
                upgradeFeedbackText.text = reason;
                upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
            }
            return;
        }

        StartCoroutine(PlayUpgradeSequence(type));
    }

    private IEnumerator PlayUpgradeSequence(SkillType type)
    {
        isUpgradeSequencePlaying = true;
        if (upgradeButton != null) upgradeButton.interactable = false;

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        FXOverdose.AI.AIVisualController visual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
        int timeHours = levelSystem.GetSkillTimeCostHours(type);
        string beforeTime = FormatGameTime(gameManager);

        visual?.BeginSkillUpgradeVisual(type);
        infoOverlay.SetActive(false);
        timeTransitionTitle.text = $"{GetDisplayName(type)} 중...";
        timeTransitionClock.text = $"{beforeTime}  →  +{timeHours}시간";
        timeTransitionOverlay.SetActive(true);
        timeTransitionOverlay.transform.SetAsLastSibling();

        yield return FadeTransition(0f, 1f, 0.35f);
        yield return new WaitForSecondsRealtime(0.7f);

        bool upgraded = levelSystem.TryUpgradeSkillWithCost(type);
        timeTransitionTitle.text = upgraded ? $"{GetDisplayName(type)} 완료" : "업그레이드 중단";
        timeTransitionClock.text = upgraded
            ? $"{beforeTime}  →  {FormatGameTime(gameManager)}"
            : beforeTime;

        yield return new WaitForSecondsRealtime(1.1f);
        yield return FadeTransition(1f, 0f, 0.35f);

        timeTransitionOverlay.SetActive(false);
        visual?.EndSkillUpgradeVisual();

        RefreshButtonLevels();
        isUpgradeSequencePlaying = false;
        OpenSkillOverview();
        if (!upgraded)
        {
            if (upgradeFeedbackText != null)
            {
                upgradeFeedbackText.text = "업그레이드에 실패했습니다.";
                upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
            }
        }
    }

    private IEnumerator FadeTransition(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            timeTransitionGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        timeTransitionGroup.alpha = to;
    }

    private static string FormatGameTime(GameManager gameManager)
    {
        return gameManager == null
            ? "--:--"
            : $"{gameManager.CurrentHour:00}:{gameManager.CurrentMinute:00}";
    }

    private string GetEffectDescription(SkillType type) => type switch
    {
        SkillType.ChartStudy => $"신호 정확도  {levelSystem.GetSignalAccuracy() * 100f:N0}%\n진입 지연 패널티  {levelSystem.GetEntryDelayPenaltyRatio() * 100f:N0}%\n\n차트 신호를 더 정확하게 판별하고 불리한 추격 진입을 줄입니다.",
        SkillType.CubePatience => $"익절 인내 배율  x{levelSystem.GetTakeProfitMultiplier():0.##}\n\n작은 수익에 성급하게 포지션을 닫지 않고 목표가까지 버티는 힘을 높입니다.",
        _ => $"손절 기준  -{levelSystem.GetStopLossTightness() * 100f:0.0}%\n\n손실 포지션에 대한 미련을 줄여 더 빠르고 정확하게 손절하도록 합니다."
    };

    private void RefreshButtonLevels()
    {
        if (levelSystem == null) return;
        if (infoOverlay != null && infoOverlay.activeSelf) RefreshAllSkillCards();
    }

    private void OnSkillLevelChanged(SkillType type, int level)
    {
        RefreshButtonLevels();
        if (infoOverlay != null && infoOverlay.activeSelf) RefreshAllSkillCards();
    }

    private static string GetDisplayName(SkillType type) => type switch
    {
        SkillType.ChartStudy => "차트 공부",
        SkillType.CubePatience => "큐브 풀기",
        _ => "파산 회고록 읽기"
    };

    private static string GetIconName(SkillType type) => type switch
    {
        SkillType.ChartStudy => "ChartStudyIcon",
        SkillType.CubePatience => "CubePatienceIcon",
        _ => "BookJudgmentIcon"
    };

    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject go = CreateUIObject(name, parent, typeof(Button), typeof(Outline));
        Image image = go.GetComponent<Image>();
        image.color = new Color32(20, 29, 51, 255);
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = UIStrokeStyle.DefaultColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(go.transform, "Label", label, 18f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.color = new Color32(207, 250, 254, 255);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(8f, size - 4f);
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(Transform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] extra)
    {
        System.Type[] types = new System.Type[3 + extra.Length];
        types[0] = typeof(RectTransform);
        types[1] = typeof(CanvasRenderer);
        types[2] = typeof(Image);
        for (int i = 0; i < extra.Length; i++) types[i + 3] = extra[i];
        GameObject go = new(name, types);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

/// <summary>게임 및 튜토리얼 씬의 메인 Canvas에 액티브 스킬 HUD를 자동 설치합니다.</summary>
public static class ActiveSkillHUDBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GameScene" &&
            !string.Equals(scene.name, "tutorial", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Install(scene);
    }

    private static void Install(Scene gameScene)
    {
        Canvas target = null;
        foreach (GameObject root in gameScene.GetRootGameObjects())
        {
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.isRootCanvas && (target == null || canvas.sortingOrder < target.sortingOrder))
                    target = canvas;
            }
        }

        if (target == null || target.GetComponent<ActiveSkillHUDController>() != null) return;
        target.gameObject.AddComponent<ActiveSkillHUDController>();
        Debug.Log($"[ActiveSkillHUD] {gameScene.name} 메인 Canvas에 스킬 UI 복구 완료");
    }
}
