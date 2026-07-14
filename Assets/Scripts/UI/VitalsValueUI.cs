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
        UpdateValue(healthSlider, healthValueText);
        UpdateValue(mentalSlider, mentalValueText);
    }

    private static void UpdateValue(Slider slider, TMP_Text label)
    {
        if (slider == null || label == null) return;
        int value = Mathf.RoundToInt(slider.normalizedValue * 100f);
        label.text = $"{value}/100";
    }
}
