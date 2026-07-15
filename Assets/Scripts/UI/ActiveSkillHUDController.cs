using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

/// <summary>우측 상단의 3개 스킬 버튼과 스킬 정보 팝업을 관리합니다.</summary>
public sealed class ActiveSkillHUDController : MonoBehaviour
{
    private static readonly SkillType[] SkillOrder =
    {
        SkillType.ChartStudy,
        SkillType.CubePatience,
        SkillType.BookJudgment
    };

    private TraderLevelSystem levelSystem;
    private RectTransform skillRow;
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

    private void Awake()
    {
        EnsureLevelSystem();
        BuildSkillRow();
        BuildInfoPopup();
        infoOverlay.SetActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeAfterLayout());
    }

    private IEnumerator InitializeAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutBelowSettings();

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
        skillRow.pivot = new Vector2(1f, 1f);
        skillRow.sizeDelta = new Vector2(66f, 222f);

        for (int i = 0; i < SkillOrder.Length; i++)
        {
            SkillType type = SkillOrder[i];
            Button button = CreateSkillButton(row.transform, type, i);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(66f, 66f);
            rect.anchoredPosition = new Vector2(0f, -i * 78f);
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
        SetRect(levelBadge.rectTransform, new Vector2(0.40f, 0.02f), new Vector2(0.96f, 0.31f));
        levelBadge.color = new Color32(207, 250, 254, 255);
        levelBadge.fontStyle = FontStyles.Bold;
        levelBadge.outlineWidth = 0.2f;
        levelBadge.outlineColor = new Color32(11, 15, 25, 255);

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

    private void LayoutBelowSettings()
    {
        Canvas canvas = GetComponent<Canvas>();
        RectTransform canvasRect = GetComponent<RectTransform>();
        RectTransform settingsRect = GameObject.Find("SettingsIcon")?.GetComponent<RectTransform>();
        if (canvas == null || canvasRect == null || settingsRect == null || skillRow == null) return;

        Vector3[] corners = new Vector3[4];
        settingsRect.GetWorldCorners(corners);
        Vector3 settingsBottomRight = canvas.transform.InverseTransformPoint(corners[3]);

        // 차트 좌측 외곽 여백(ChartReferenceStyler의 12px)과 동일하게 맞춥니다.
        const float rightMargin = 12f;
        const float settingsGap = 24f;
        Rect bounds = canvasRect.rect;
        // 화면 우측에 딱 붙이고, 첫 스킬 버튼은 설정 버튼 아래에서 별도 여백을 둡니다.
        float x = bounds.xMax - rightMargin;
        float y = Mathf.Clamp(
            settingsBottomRight.y - settingsGap,
            bounds.yMin + skillRow.sizeDelta.y + rightMargin,
            bounds.yMax - rightMargin);
        skillRow.localPosition = new Vector3(x, y, 0f);
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

        bool upgraded = levelSystem.TryUpgradeSkillWithCost(selectedSkill);

        // 비용 차감과 레벨 변경 결과를 먼저 UI 전체에 반영합니다.
        RefreshButtonLevels();
        RefreshInfo();

        // RefreshInfo가 다음 단계의 조건 부족 메시지를 표시하더라도,
        // 방금 실행한 업그레이드의 성공 결과를 사용자가 확인할 수 있게 마지막에 덮어씁니다.
        if (upgraded)
        {
            if (upgradeFeedbackText != null)
            {
                upgradeFeedbackText.text = $"{GetDisplayName(selectedSkill)} 업그레이드 완료!";
                upgradeFeedbackText.color = new Color32(34, 197, 94, 255);
            }
        }
        else if (upgradeFeedbackText != null)
        {
            levelSystem.CanUpgradeSkill(selectedSkill, out string reason);
            upgradeFeedbackText.text = string.IsNullOrEmpty(reason) ? "업그레이드에 실패했습니다." : reason;
            upgradeFeedbackText.color = new Color32(239, 68, 68, 255);
        }
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
            if (badge != null) badge.text = $"LV.{levelSystem.GetSkillLevel(type)}";
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
        text.fontSizeMin = 12f;
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

/// <summary>GameScene의 메인 Canvas에 액티브 스킬 HUD를 자동 설치합니다.</summary>
public static class ActiveSkillHUDBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Canvas target = null;
        foreach (Canvas canvas in canvases)
        {
            if (canvas.isRootCanvas && (target == null || canvas.sortingOrder < target.sortingOrder)) target = canvas;
        }
        if (target == null || target.GetComponent<ActiveSkillHUDController>() != null) return;
        target.gameObject.AddComponent<ActiveSkillHUDController>();
    }
}
