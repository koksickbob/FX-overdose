using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

/// <summary>우측 상단의 3개 스킬 버튼과 스킬 정보 팝업을 관리합니다.</summary>
public sealed class ActiveSkillHUDController : MonoBehaviour
{
    private const float SkillButtonSize = UIStrokeStyle.CompactHudHeight;
    private const float SkillButtonGap = 12f;
    private const float InventoryRightMargin = UIStrokeStyle.ScreenEdgeMargin;
    private const float ShopButtonGap = 12f;

    private static readonly SkillType[] SkillOrder =
    {
        SkillType.ChartStudy,
        SkillType.CubePatience,
        SkillType.BookJudgment
    };

    private TraderLevelSystem levelSystem;
    private RectTransform skillRow;
    private RectTransform shopButtonRect;
    private GameObject infoOverlay;
    private Image infoIcon;
    private TMP_Text infoTitle;
    private TMP_Text infoLevel;
    private TMP_Text infoEffect;
    private TMP_Text infoCost;
    private Button upgradeButton;
    private TMP_Text upgradeButtonText;
    private TMP_Text upgradeFeedbackText;
    private SkillType selectedSkill;
    private GameObject timeTransitionOverlay;
    private CanvasGroup timeTransitionGroup;
    private TMP_Text timeTransitionTitle;
    private TMP_Text timeTransitionClock;
    private bool isUpgradeSequencePlaying;

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
        GameObject row = new("ActiveSkillButtonRow", typeof(RectTransform));
        row.transform.SetParent(transform, false);
        skillRow = row.GetComponent<RectTransform>();
        skillRow.anchorMin = skillRow.anchorMax = new Vector2(0.5f, 0.5f);
        skillRow.pivot = new Vector2(1f, 0f);
        skillRow.sizeDelta = new Vector2(
            SkillButtonSize,
            SkillButtonSize * SkillOrder.Length + SkillButtonGap * (SkillOrder.Length - 1));

        for (int i = 0; i < SkillOrder.Length; i++)
        {
            SkillType type = SkillOrder[i];
            Button button = CreateSkillButton(row.transform, type, i);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(SkillButtonSize, SkillButtonSize);
            rect.anchoredPosition = new Vector2(
                0f,
                skillRow.sizeDelta.y - SkillButtonSize - i * (SkillButtonSize + SkillButtonGap));
        }
    }

    private Button CreateSkillButton(Transform parent, SkillType type, int index)
    {
        GameObject go = new($"SkillButton_{type}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        go.transform.SetParent(parent, false);

        Image background = go.GetComponent<Image>();
        background.color = new Color32(20, 29, 51, 245); // #141D33

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = new Color32(6, 182, 212, 255); // #06B6D4
        outline.effectDistance = UIStrokeStyle.EffectDistance;

        Image icon = CreateImage(go.transform, "Icon");
        icon.sprite = Resources.Load<Sprite>($"UI/Skills/{GetIconName(type)}");
        icon.preserveAspect = true;
        SetRect(icon.rectTransform, new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));

        TMP_Text levelBadge = CreateText(go.transform, "LevelBadge", "LV.1", 11f, TextAlignmentOptions.BottomRight);
        SetRect(levelBadge.rectTransform, new Vector2(0.30f, 0.02f), new Vector2(0.96f, 0.31f));
        levelBadge.color = new Color32(207, 250, 254, 255);
        levelBadge.outlineColor = new Color32(11, 15, 25, 255);
        GlobalPFStardustFont.ConfigureCompactHudText(levelBadge, null, 11f, 0.05f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 0.95f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.75f, 0.82f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => OpenInfo(type));
        return button;
    }

    private void ResolveShopButton()
    {
        GameObject shopButton = GameObject.Find("ShopOpenButton");
        shopButtonRect = shopButton != null ? shopButton.GetComponent<RectTransform>() : null;
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
            Vector3 shopTopRight = canvasRect.InverseTransformPoint(shopCorners[2]);
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
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 430f);
        panel.GetComponent<Image>().color = new Color32(15, 23, 42, 252); // #0F172A
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color32(6, 182, 212, 255);
        panelOutline.effectDistance = UIStrokeStyle.EffectDistance;

        infoIcon = CreateImage(panel.transform, "SkillIcon");
        infoIcon.preserveAspect = true;
        SetRect(infoIcon.rectTransform, new Vector2(0.07f, 0.55f), new Vector2(0.31f, 0.90f));

        infoTitle = CreateText(panel.transform, "SkillTitle", "", 31f, TextAlignmentOptions.Left);
        SetRect(infoTitle.rectTransform, new Vector2(0.35f, 0.76f), new Vector2(0.87f, 0.91f));
        infoTitle.color = new Color32(207, 250, 254, 255);
        infoTitle.fontStyle = FontStyles.Bold;

        infoLevel = CreateText(panel.transform, "SkillLevel", "", 21f, TextAlignmentOptions.Left);
        SetRect(infoLevel.rectTransform, new Vector2(0.35f, 0.62f), new Vector2(0.87f, 0.75f));
        infoLevel.color = new Color32(6, 182, 212, 255);

        infoEffect = CreateText(panel.transform, "SkillEffect", "", 22f, TextAlignmentOptions.TopLeft);
        SetRect(infoEffect.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.55f));
        infoEffect.textWrappingMode = TextWrappingModes.Normal;

        infoCost = CreateText(panel.transform, "SkillCost", "", 18f, TextAlignmentOptions.Center);
        SetRect(infoCost.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.29f));
        infoCost.color = new Color32(234, 179, 8, 255); // #EAB308
        infoCost.textWrappingMode = TextWrappingModes.Normal;

        upgradeButton = CreateButton(panel.transform, "UpgradeButton", "UPGRADE");
        SetRect(upgradeButton.GetComponent<RectTransform>(), new Vector2(0.31f, 0.075f), new Vector2(0.69f, 0.17f));
        upgradeButtonText = upgradeButton.GetComponentInChildren<TMP_Text>();
        upgradeButton.onClick.AddListener(UpgradeSelectedSkill);

        upgradeFeedbackText = CreateText(panel.transform, "UpgradeFeedback", "", 14f, TextAlignmentOptions.Center);
        SetRect(upgradeFeedbackText.rectTransform, new Vector2(0.10f, 0.01f), new Vector2(0.90f, 0.07f));
        upgradeFeedbackText.textWrappingMode = TextWrappingModes.Normal;

        Button cornerClose = CreateButton(panel.transform, "CornerClose", "X");
        SetRect(cornerClose.GetComponent<RectTransform>(), new Vector2(0.89f, 0.86f), new Vector2(0.97f, 0.96f));
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

    private void OpenInfo(SkillType type)
    {
        selectedSkill = type;
        if (upgradeFeedbackText != null) upgradeFeedbackText.text = "";
        RefreshInfo();
        infoOverlay.SetActive(true);
        infoOverlay.transform.SetAsLastSibling();
    }

    private void CloseInfo() => infoOverlay.SetActive(false);

    private void RefreshInfo()
    {
        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null) return;

        int level = levelSystem.GetSkillLevel(selectedSkill);
        infoIcon.sprite = Resources.Load<Sprite>($"UI/Skills/{GetIconName(selectedSkill)}");
        infoTitle.text = GetDisplayName(selectedSkill);
        infoLevel.text = $"LEVEL {level} / 10";
        infoEffect.text = GetEffectDescription(selectedSkill);
        infoCost.text = level >= 10
            ? "MAX LEVEL · 모든 효과가 최대치입니다."
            : $"필요 조건   ${levelSystem.GetSkillCost(selectedSkill):N0}   |   HP -{levelSystem.GetSkillHealthCost(selectedSkill):N0}   |   {levelSystem.GetSkillTimeCostHours(selectedSkill)}시간";

        bool canUpgrade = levelSystem.CanUpgradeSkill(selectedSkill, out string reason);
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
        if (upgradeButtonText != null)
        {
            upgradeButtonText.text = level >= 10 ? "MAX LEVEL" : canUpgrade ? "UPGRADE" : "조건 부족";
            upgradeButtonText.color = canUpgrade
                ? new Color32(207, 250, 254, 255)
                : new Color32(148, 163, 184, 255);
        }
        if (!canUpgrade && level < 10 && upgradeFeedbackText != null)
        {
            upgradeFeedbackText.text = reason;
            upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
        }
    }

    private void UpgradeSelectedSkill()
    {
        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null) return;

        if (isUpgradeSequencePlaying) return;
        if (!levelSystem.CanUpgradeSkill(selectedSkill, out string reason))
        {
            if (upgradeFeedbackText != null)
            {
                upgradeFeedbackText.text = reason;
                upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
            }
            return;
        }

        StartCoroutine(PlayUpgradeSequence(selectedSkill));
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
        if (upgraded)
        {
            CloseInfo();
        }
        else
        {
            OpenInfo(type);
            if (upgradeFeedbackText != null)
            {
                upgradeFeedbackText.text = "업그레이드에 실패했습니다.";
                upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
            }
        }
        isUpgradeSequencePlaying = false;
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
        foreach (SkillType type in SkillOrder)
        {
            Transform button = skillRow.Find($"SkillButton_{type}");
            TMP_Text badge = button?.Find("LevelBadge")?.GetComponent<TMP_Text>();
            if (badge != null)
            {
                badge.text = $"LV.{levelSystem.GetSkillLevel(type)}";
                GlobalPFStardustFont.RefreshCompactHudText(badge);
            }
        }
    }

    private void OnSkillLevelChanged(SkillType type, int level)
    {
        RefreshButtonLevels();
        if (infoOverlay != null && infoOverlay.activeSelf && type == selectedSkill) RefreshInfo();
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
