using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;

namespace FXOverdose.UI.Chart
{
    public class TradingPanelUIController : MonoBehaviour
    {
        [Header("시스템 연결")]
        [SerializeField] private TradingController tradingController;
        [SerializeField] private GameManager gameManager;

        [Header("포지션 진입/종료 버튼")]
        [SerializeField] private Button longButton;
        [SerializeField] private Button shortButton;
        [SerializeField] private Button closePositionButton;
        [SerializeField] private TMP_Text longSubtitleText; // "Tap to Open Long" 또는 "LONG 보유중"
        [SerializeField] private TMP_Text shortSubtitleText; // "Tap to Open Short" 또는 "SHORT 보유중"

        [Header("증거금(Margin) 설정")]
        [SerializeField] private Slider marginPercentageSlider; // 자산 대비 투입 비율 (10% ~ 100%)
        [SerializeField] private TMP_Text marginAmountText;

        [Header("레버리지(Leverage) 선택")]
        [SerializeField] private Button btnLeverageMinus;
        [SerializeField] private Button btnLeveragePlus;
        [SerializeField] private TMP_Text leverageDisplayText; // "10x"
        [SerializeField] private Button btnPreset1x;
        [SerializeField] private Button btnPreset5x;
        [SerializeField] private Button btnPreset10x;
        [SerializeField] private Button btnPreset25x;
        [SerializeField] private Button btnPreset50x;
        [SerializeField] private Button btnPreset100x;
        [SerializeField] private Button btnPreset125x;

        [Header("실시간 포지션 상태 오버레이 (ROE & PnL)")]
        [SerializeField] private GameObject positionStatusPanel;
        [SerializeField] private TMP_Text positionTypeText;
        [SerializeField] private TMP_Text roeText;
        [SerializeField] private TMP_Text pnlText;
        [SerializeField] private TMP_Text entryPriceText;
        [SerializeField] private TMP_Text liquidationPriceText;

        // 색상 토큰
        private readonly Color cyanHighlight = new Color(0.024f, 0.714f, 0.831f, 1f); // #06B6D4
        private readonly Color inactivePresetColor = new Color(0.122f, 0.161f, 0.235f, 1f);
        private readonly Color bullishColor = new Color(0.133f, 0.773f, 0.369f, 1f);
        private readonly Color bearishColor = new Color(0.937f, 0.267f, 0.267f, 1f);

        private int currentSelectedLeverage = 10;
        private float selectedMarginPercentage = 0.25f; // 기본 25% 투입

        private void Start()
        {
            if (tradingController == null) tradingController = FindFirstObjectByType<TradingController>();
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();

            if (tradingController != null)
            {
                tradingController.OnPositionChanged += RefreshPanelUI;
                tradingController.OnPositionLiquidated += HandlePositionLiquidated;
            }

            SetupButtons();
            SelectLeverage(10);
            RefreshPanelUI();
        }

        private void OnDestroy()
        {
            if (tradingController != null)
            {
                tradingController.OnPositionChanged -= RefreshPanelUI;
                tradingController.OnPositionLiquidated -= HandlePositionLiquidated;
            }
        }

        private void Update()
        {
            // 포지션 보유 중일 때 매 프레임 실시간 ROE 및 PnL 숫자 갱신
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                UpdatePositionStatusNumbers();
            }

            // 슬라이더 변경 감지 및 증거금 표시
            if (marginPercentageSlider != null && gameManager != null)
            {
                selectedMarginPercentage = Mathf.Clamp(marginPercentageSlider.value, 0.05f, 1f);
                float margin = gameManager.CurrentBalance * selectedMarginPercentage;
                if (marginAmountText != null)
                {
                    marginAmountText.text = $"투입: ${margin:N0} ({selectedMarginPercentage * 100f:F0}%)";
                }
            }
        }

        private void SetupButtons()
        {
            if (longButton != null) longButton.onClick.AddListener(OnLongButtonClicked);
            if (shortButton != null) shortButton.onClick.AddListener(OnShortButtonClicked);
            if (closePositionButton != null) closePositionButton.onClick.AddListener(() => tradingController?.ClosePosition());

            if (btnLeverageMinus != null) btnLeverageMinus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage - 1));
            if (btnLeveragePlus != null) btnLeveragePlus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage + 1));

            if (btnPreset1x != null) btnPreset1x.onClick.AddListener(() => SelectLeverage(1));
            if (btnPreset5x != null) btnPreset5x.onClick.AddListener(() => SelectLeverage(5));
            if (btnPreset10x != null) btnPreset10x.onClick.AddListener(() => SelectLeverage(10));
            if (btnPreset25x != null) btnPreset25x.onClick.AddListener(() => SelectLeverage(25));
            if (btnPreset50x != null) btnPreset50x.onClick.AddListener(() => SelectLeverage(50));
            if (btnPreset100x != null) btnPreset100x.onClick.AddListener(() => SelectLeverage(100));
            if (btnPreset125x != null) btnPreset125x.onClick.AddListener(() => SelectLeverage(125));

            if (marginPercentageSlider != null)
            {
                marginPercentageSlider.minValue = 0.05f;
                marginPercentageSlider.maxValue = 1f;
                marginPercentageSlider.value = 0.25f;
            }
        }

        public void SelectLeverage(int lev)
        {
            currentSelectedLeverage = Mathf.Clamp(lev, 1, 125);

            if (leverageDisplayText != null)
            {
                leverageDisplayText.text = $"{currentSelectedLeverage}x";
            }

            // 프리셋 버튼 하이라이트
            UpdatePresetHighlight(btnPreset1x, currentSelectedLeverage == 1);
            UpdatePresetHighlight(btnPreset5x, currentSelectedLeverage == 5);
            UpdatePresetHighlight(btnPreset10x, currentSelectedLeverage == 10);
            UpdatePresetHighlight(btnPreset25x, currentSelectedLeverage == 25);
            UpdatePresetHighlight(btnPreset50x, currentSelectedLeverage == 50);
            UpdatePresetHighlight(btnPreset100x, currentSelectedLeverage == 100);
            UpdatePresetHighlight(btnPreset125x, currentSelectedLeverage == 125);
        }

        private void UpdatePresetHighlight(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? cyanHighlight : inactivePresetColor;
            }
        }

        private void OnLongButtonClicked()
        {
            if (tradingController == null || gameManager == null) return;

            if (tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                float margin = gameManager.CurrentBalance * selectedMarginPercentage;
                tradingController.OpenPosition(TradingController.PositionType.Long, margin, currentSelectedLeverage);
            }
        }

        private void OnShortButtonClicked()
        {
            if (tradingController == null || gameManager == null) return;

            if (tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                float margin = gameManager.CurrentBalance * selectedMarginPercentage;
                tradingController.OpenPosition(TradingController.PositionType.Short, margin, currentSelectedLeverage);
            }
        }

        public void RefreshPanelUI()
        {
            if (tradingController == null) return;

            bool hasPosition = tradingController.CurrentPosition != TradingController.PositionType.None;

            // 진입 버튼 상태 업데이트
            if (longButton != null) longButton.interactable = !hasPosition;
            if (shortButton != null) shortButton.interactable = !hasPosition;
            if (closePositionButton != null) closePositionButton.gameObject.SetActive(hasPosition);

            if (longSubtitleText != null)
            {
                longSubtitleText.text = tradingController.CurrentPosition == TradingController.PositionType.Long 
                    ? "포지션 보유중" : "Tap to Open Long";
            }
            if (shortSubtitleText != null)
            {
                shortSubtitleText.text = tradingController.CurrentPosition == TradingController.PositionType.Short 
                    ? "포지션 보유중" : "Tap to Open Short";
            }

            // 상태 오버레이 패널 표시 여부
            if (positionStatusPanel != null)
            {
                positionStatusPanel.SetActive(hasPosition);
            }

            if (hasPosition)
            {
                UpdatePositionStatusNumbers();
                if (positionTypeText != null)
                {
                    positionTypeText.text = $"{tradingController.CurrentPosition} {tradingController.CurrentLeverage}x";
                    positionTypeText.color = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? bullishColor : bearishColor;
                }
                if (entryPriceText != null) entryPriceText.text = $"진입가: ${tradingController.EntryPrice:N1}";
                if (liquidationPriceText != null) liquidationPriceText.text = $"청산가: ${tradingController.LiquidationPrice:N1}";
            }
        }

        private void UpdatePositionStatusNumbers()
        {
            if (tradingController == null) return;

            float roe = tradingController.CalculateROEPercentage();
            float pnl = tradingController.CalculateUnrealizedPnL();

            if (roeText != null)
            {
                string sign = roe >= 0 ? "+" : "";
                roeText.text = $"{sign}{roe:F2}%";
                roeText.color = roe >= 0 ? bullishColor : bearishColor;
            }

            if (pnlText != null)
            {
                string sign = pnl >= 0 ? "+" : "";
                pnlText.text = $"PnL: {sign}${pnl:N2}";
                pnlText.color = pnl >= 0 ? bullishColor : bearishColor;
            }
        }

        private void HandlePositionLiquidated()
        {
            RefreshPanelUI();
        }
    }
}
