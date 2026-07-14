using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HP와 MENTAL 슬라이더 값을 0~100 숫자로 표시합니다.</summary>
public class VitalsValueUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider mentalSlider;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text mentalValueText;

    private void LateUpdate()
    {
        UpdateValue(healthSlider, healthValueText, false);
        UpdateValue(mentalSlider, mentalValueText, true);
    }

    private static void UpdateValue(Slider slider, TMP_Text label, bool isMental)
    {
        if (label == null && slider == null) return;

        TraderStatus canonical = TraderStatus.CanonicalInstance;
        if (canonical != null)
        {
            float ratio = isMental ? canonical.MentalRatio : canonical.HealthRatio;
            int val = Mathf.RoundToInt(ratio * 100f);
            if (label != null) label.text = $"{val}/100";
            if (slider != null) slider.value = ratio;
            return;
        }

        if (slider != null && label != null)
        {
            int value = Mathf.RoundToInt(slider.normalizedValue * 100f);
            label.text = $"{value}/100";
        }
    }
}
