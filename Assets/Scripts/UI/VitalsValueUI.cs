using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HP와 MENTAL 슬라이더 값을 0~100 숫자로 표시하고, 멘탈 감소 시 플로팅 UI를 표시합니다.</summary>
public class VitalsValueUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider mentalSlider;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text mentalValueText;

    private void Start()
    {
        TraderStatus canonical = TraderStatus.CanonicalInstance;
        if (canonical != null)
        {
            canonical.OnMentalChangedWithReason += HandleMentalChanged;
        }
    }

    private void OnDestroy()
    {
        TraderStatus canonical = TraderStatus.CanonicalInstance;
        if (canonical != null)
        {
            canonical.OnMentalChangedWithReason -= HandleMentalChanged;
        }
    }

    private void HandleMentalChanged(float amount, string reason)
    {
        if (amount < 0f && !string.IsNullOrEmpty(reason) && mentalSlider != null)
        {
            StartCoroutine(SpawnFloatingTextCoroutine(amount, reason));
        }
    }

    private IEnumerator SpawnFloatingTextCoroutine(float amount, string reason)
    {
        GameObject go = new GameObject("MentalDrainFloatText");
        go.transform.SetParent(mentalSlider.transform, false);
        
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = $"{amount:0.0#} ({reason})";
        text.color = Color.red;
        text.fontSize = 20;
        text.alignment = TextAlignmentOptions.Center;
        
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, 20f);
        
        float duration = 2.0f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0, 60f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            Color c = text.color;
            c.a = 1f - t;
            text.color = c;
            yield return null;
        }
        
        Destroy(go);
    }

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
