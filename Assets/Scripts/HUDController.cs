using TMPro;
using UnityEngine;
using UnityEngine.UI;

#pragma warning disable CS0649

public class HUDController : MonoBehaviour
{
    [Header("게임 시스템")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TraderStatus traderStatus;

    [Header("날짜와 시간 UI")]
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timeText;

    [Header("트레이더 상태 UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider mentalSlider;

    private TMP_Text healthValueText;
    private TMP_Text mentalValueText;
    private Image healthFillImage;
    private Image mentalFillImage;
    private bool statusVisualsResolved;

    private static readonly Color HealthGood = new Color32(45, 212, 191, 255);
    private static readonly Color MentalGood = new Color32(168, 85, 247, 255);
    private static readonly Color Warning = new Color32(251, 191, 36, 255);
    private static readonly Color Danger = new Color32(248, 67, 92, 255);

    private void Start()
    {
        ResolveReferences();
        UpdateHUD();
    }

    private void Update()
    {
        UpdateHUD();
    }

    private void ResolveReferences()
    {
        if (gameManager == null) gameManager = Object.FindAnyObjectByType<GameManager>();
        traderStatus = TraderStatus.CanonicalInstance;

        if (healthSlider == null)
        {
            GameObject healthObject = GameObject.Find("HP");
            if (healthObject != null) healthSlider = healthObject.GetComponent<Slider>();
        }

        if (mentalSlider == null)
        {
            GameObject mentalObject = GameObject.Find("Mental");
            if (mentalObject != null) mentalSlider = mentalObject.GetComponent<Slider>();
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            EnsureSliderVisualSetup(healthSlider);
        }

        if (mentalSlider != null)
        {
            mentalSlider.minValue = 0f;
            mentalSlider.maxValue = 1f;
            EnsureSliderVisualSetup(mentalSlider);
        }

        ResolveStatusVisuals();
    }

    private void ResolveStatusVisuals()
    {
        if (statusVisualsResolved || healthSlider == null || mentalSlider == null) return;

        Transform vitalsPanel = healthSlider.transform.parent;
        if (vitalsPanel == null) return;

        healthValueText = FindChild(vitalsPanel, "HealthValue")?.GetComponent<TMP_Text>()
                          ?? FindChild(vitalsPanel, "HPValue")?.GetComponent<TMP_Text>();
        mentalValueText = FindChild(vitalsPanel, "MentalValue")?.GetComponent<TMP_Text>();
        healthFillImage = healthSlider.fillRect != null ? healthSlider.fillRect.GetComponent<Image>() : null;
        mentalFillImage = mentalSlider.fillRect != null ? mentalSlider.fillRect.GetComponent<Image>() : null;

        ConfigureValueText(healthValueText);
        ConfigureValueText(mentalValueText);
        statusVisualsResolved = true;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName) return child;
        }
        return null;
    }

    private static void ConfigureValueText(TMP_Text text)
    {
        if (text == null) return;
        text.gameObject.SetActive(true);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 17f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineRight;
        text.color = new Color32(226, 245, 250, 255);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private void EnsureSliderVisualSetup(Slider slider)
    {
        if (slider == null || slider.fillRect == null) return;

        RectTransform fillRect = slider.fillRect;
        if (fillRect.parent is RectTransform fillArea)
        {
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
        }

        Vector2 aMin = fillRect.anchorMin;
        Vector2 aMax = fillRect.anchorMax;
        aMin.y = 0f;
        aMax.y = 1f;
        fillRect.anchorMin = aMin;
        fillRect.anchorMax = aMax;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    // 현재 게임 정보를 UI에 표시
    private void UpdateHUD()
    {
        if (gameManager == null || traderStatus == null || healthSlider == null || mentalSlider == null)
        {
            ResolveReferences();
        }

        // 필요한 오브젝트가 연결되지 않았다면 실행하지 않음
        if (gameManager == null || traderStatus == null)
        {
            return;
        }

        UpdateTimeUI();
        UpdateStatusUI();
    }

    // 날짜와 시간 텍스트 갱신
    private void UpdateTimeUI()
    {
        // 01, 02처럼 두 자리 숫자로 표시
        if (dayText != null) dayText.text = $"DAY {gameManager.CurrentDay:00}";

        // 09:05처럼 시와 분을 두 자리 숫자로 표시
        if (timeText != null) timeText.text =
            $"{gameManager.CurrentHour:00}:{gameManager.CurrentMinute:00}";
    }

    // 체력과 멘탈 게이지 갱신
    private void UpdateStatusUI()
    {
        // 최신 메인 트레이더 상태 동기화 바인딩
        TraderStatus canonical = TraderStatus.CanonicalInstance;
        if (canonical != null) traderStatus = canonical;

        // TraderStatus에서 0~1 사이 비율을 받아 Slider에 적용
        if (healthSlider != null && traderStatus != null)
        {
            float ratio = Mathf.Clamp01(traderStatus.HealthRatio);
            healthSlider.value = ratio;
            EnsureSliderVisualSetup(healthSlider);
            if (healthValueText != null)
                healthValueText.text = $"{Mathf.CeilToInt(traderStatus.CurrentHealth)} / {Mathf.CeilToInt(traderStatus.MaxHealth)}";
            if (healthFillImage != null)
                healthFillImage.color = GetVitalColor(ratio, HealthGood);
        }
        if (mentalSlider != null && traderStatus != null)
        {
            float ratio = Mathf.Clamp01(traderStatus.MentalRatio);
            mentalSlider.value = ratio;
            EnsureSliderVisualSetup(mentalSlider);
            if (mentalValueText != null)
                mentalValueText.text = $"{Mathf.CeilToInt(traderStatus.CurrentMental)} / {Mathf.CeilToInt(traderStatus.EffectiveMaxMental)}";
            if (mentalFillImage != null)
                mentalFillImage.color = GetVitalColor(ratio, MentalGood);
        }
    }

    private static Color GetVitalColor(float ratio, Color healthyColor)
    {
        if (ratio <= 0.25f) return Danger;
        if (ratio <= 0.5f) return Warning;
        return healthyColor;
    }
}
