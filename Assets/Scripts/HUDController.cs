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

    private void Start()
    {
        // Slider는 0부터 1 사이의 비율을 사용
        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;

        mentalSlider.minValue = 0f;
        mentalSlider.maxValue = 1f;

        UpdateHUD();
    }

    private void Update()
    {
        UpdateHUD();
    }

    // 현재 게임 정보를 UI에 표시
    private void UpdateHUD()
    {
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
        // TraderStatus에서 0~1 사이 비율을 받아 Slider에 적용
        if (healthSlider != null) healthSlider.value = traderStatus.HealthRatio;
        if (mentalSlider != null) mentalSlider.value = traderStatus.MentalRatio;
    }
}