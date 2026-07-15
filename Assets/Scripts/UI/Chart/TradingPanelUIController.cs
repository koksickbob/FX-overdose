using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;

#pragma warning disable CS0649

namespace FXOverdose.UI.Chart
{
    public class TradingPanelUIController : MonoBehaviour
    {
        public enum ControlMode
        {
            Leverage,
            MarginRatio
        }

        [Header("시스템 연결")]
        [SerializeField] private TradingController tradingController;
        [SerializeField] private GameManager gameManager;

        [Header("포지션 진입/종료 버튼")]
        [SerializeField] private Button longButton;
        [SerializeField] private Button shortButton;
        [SerializeField] private Button closePositionButton;
        [SerializeField] private TMP_Text longSubtitleText; // "Tap to Open Long" 또는 "LONG 보유중"
        [SerializeField] private TMP_Text shortSubtitleText; // "Tap to Open Short" 또는 "SHORT 보유중"

        [Header("조작부 모드 전환 탭 (우측 상단)")]
        [SerializeField] private Button btnTabLeverageMode;   // "LEVERAGE 배율" 탭
        [SerializeField] private Button btnTabMarginRatioMode; // "MARGIN 비율" 탭
        [SerializeField] private GameObject leverageControlContainer;    // 레버리지 조작부 컨테이너
        [SerializeField] private GameObject marginRatioControlContainer; // 투자비율 조작부 컨테이너
        [SerializeField] private GameObject tabsBarContainer;            // 모드 전환 탭 바 컨테이너

        [Header("증거금(Margin Ratio) 설정 [10% 단위 스태퍼 + 프리셋 + 슬라이더 호환]")]
        [SerializeField] private Slider marginPercentageSlider; // 기존 슬라이더 (호환 유지)
        [SerializeField] private TMP_Text marginAmountText;     // "투입: $3,737 (30%)"
        [SerializeField] private Button btnMarginRatioMinus;    // -10% 버튼
        [SerializeField] private Button btnMarginRatioPlus;     // +10% 버튼
        [SerializeField] private TMP_Text marginRatioDisplayText; // "30% ($3,737)"
        [SerializeField] private Button btnPresetRatio10;       // 10% 프리셋
        [SerializeField] private Button btnPresetRatio25;       // 25% 프리셋
        [SerializeField] private Button btnPresetRatio50;       // 50% 프리셋
        [SerializeField] private Button btnPresetRatio75;       // 75% 프리셋
        [SerializeField] private Button btnPresetRatio100;      // 100% 프리셋

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
        [SerializeField] private TMP_Text targetPriceText; // AI 결정 목표 주가 (TARGET PRICE) 표시기

        // 색상 토큰
        private readonly Color cyanHighlight = new Color(0.024f, 0.714f, 0.831f, 1f); // #06B6D4
        private readonly Color inactivePresetColor = new Color(0.122f, 0.161f, 0.235f, 1f);
        private readonly Color bullishColor = new Color(0.133f, 0.773f, 0.369f, 1f);
        private readonly Color bearishColor = new Color(0.937f, 0.267f, 0.267f, 1f);

        private ControlMode currentControlMode = ControlMode.Leverage;
        private int currentSelectedLeverage = 10;
        private int currentSelectedMarginPercent = 30; // 기본 30%
        private float selectedMarginPercentage = 0.30f;

        private void Start()
        {
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

            if (tradingController != null)
            {
                tradingController.OnPositionChanged += RefreshPanelUI;
                tradingController.OnPositionLiquidated += HandlePositionLiquidated;
                tradingController.OnTradingModeChanged += (mode) => RefreshPanelUI();
            }

            SetupButtons();
            SelectLeverage(currentSelectedLeverage);
            SelectMarginRatio(currentSelectedMarginPercent);
            SwitchControlMode(ControlMode.Leverage); // 기본 레버리지 탭 활성화
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

            // 슬라이더를 드래그한 경우 동기화
            if (marginPercentageSlider != null && gameManager != null)
            {
                if (Mathf.Abs(marginPercentageSlider.value - selectedMarginPercentage) > 0.005f)
                {
                    int roundedPercent = Mathf.RoundToInt(marginPercentageSlider.value * 100f);
                    SelectMarginRatio(roundedPercent);
                }
            }

            // 실시간 자본금 변화에 맞춰 투자 비율 금액 텍스트 동적 갱신
            if (gameManager != null)
            {
                float marginAmount = gameManager.CurrentBalance * selectedMarginPercentage;
                if (marginRatioDisplayText != null)
                {
                    marginRatioDisplayText.text = $"{currentSelectedMarginPercent}% (${marginAmount:N0})";
                }
                if (marginAmountText != null)
                {
                    marginAmountText.text = $"투입: ${marginAmount:N0} ({currentSelectedMarginPercent}%)";
                }
            }
        }

        private void SetupButtons()
        {
            if (longButton != null) longButton.onClick.AddListener(OnLongButtonClicked);
            if (shortButton != null) shortButton.onClick.AddListener(OnShortButtonClicked);
            if (closePositionButton != null) closePositionButton.onClick.AddListener(OnCloseButtonClicked);

            // 모드 전환 탭 버튼 바인딩
            if (btnTabLeverageMode != null) btnTabLeverageMode.onClick.AddListener(() => SwitchControlMode(ControlMode.Leverage));
            if (btnTabMarginRatioMode != null) btnTabMarginRatioMode.onClick.AddListener(() => SwitchControlMode(ControlMode.MarginRatio));

            // 레버리지 조작 버튼 바인딩
            if (btnLeverageMinus != null) btnLeverageMinus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage - 1));
            if (btnLeveragePlus != null) btnLeveragePlus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage + 1));

            if (btnPreset1x != null) btnPreset1x.onClick.AddListener(() => SelectLeverage(1));
            if (btnPreset5x != null) btnPreset5x.onClick.AddListener(() => SelectLeverage(5));
            if (btnPreset10x != null) btnPreset10x.onClick.AddListener(() => SelectLeverage(10));
            if (btnPreset25x != null) btnPreset25x.onClick.AddListener(() => SelectLeverage(25));
            if (btnPreset50x != null) btnPreset50x.onClick.AddListener(() => SelectLeverage(50));
            if (btnPreset100x != null) btnPreset100x.onClick.AddListener(() => SelectLeverage(100));
            if (btnPreset125x != null) btnPreset125x.onClick.AddListener(() => SelectLeverage(125));

            // 투자 사용 비율(Margin Ratio) 10% 단위 증감 및 프리셋 바인딩
            if (btnMarginRatioMinus != null) btnMarginRatioMinus.onClick.AddListener(() => SelectMarginRatio(currentSelectedMarginPercent - 10));
            if (btnMarginRatioPlus != null) btnMarginRatioPlus.onClick.AddListener(() => SelectMarginRatio(currentSelectedMarginPercent + 10));

            if (btnPresetRatio10 != null) btnPresetRatio10.onClick.AddListener(() => SelectMarginRatio(10));
            if (btnPresetRatio25 != null) btnPresetRatio25.onClick.AddListener(() => SelectMarginRatio(25));
            if (btnPresetRatio50 != null) btnPresetRatio50.onClick.AddListener(() => SelectMarginRatio(50));
            if (btnPresetRatio75 != null) btnPresetRatio75.onClick.AddListener(() => SelectMarginRatio(75));
            if (btnPresetRatio100 != null) btnPresetRatio100.onClick.AddListener(() => SelectMarginRatio(100));

            if (marginPercentageSlider != null)
            {
                marginPercentageSlider.minValue = 0.10f;
                marginPercentageSlider.maxValue = 1f;
                marginPercentageSlider.value = 0.30f;
            }
        }

        // 1. 우측 상단 탭 모드 전환
        public void SwitchControlMode(ControlMode mode)
        {
            currentControlMode = mode;

            bool hasPosition = tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None;

            if (leverageControlContainer != null)
            {
                leverageControlContainer.SetActive(!hasPosition && mode == ControlMode.Leverage);
            }

            if (marginRatioControlContainer != null)
            {
                marginRatioControlContainer.SetActive(!hasPosition && mode == ControlMode.MarginRatio);
            }

            // 탭 버튼 하이라이트 색상 갱신
            UpdatePresetHighlight(btnTabLeverageMode, mode == ControlMode.Leverage);
            UpdatePresetHighlight(btnTabMarginRatioMode, mode == ControlMode.MarginRatio);
        }

        // 2. 투자 사용 비율 설정 (10% 단위 또는 프리셋)
        public void SelectMarginRatio(int percent)
        {
            float maxAllowedRatio = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMaxAllowedMarginRatio() : 1.0f;
            int maxAllowedPercent = Mathf.RoundToInt(maxAllowedRatio * 100f);
            if (percent > maxAllowedPercent)
            {
                currentSelectedMarginPercent = maxAllowedPercent;
            }
            else
            {
                currentSelectedMarginPercent = Mathf.Clamp(percent, 10, 100);
            }
            selectedMarginPercentage = currentSelectedMarginPercent / 100f;

            if (marginPercentageSlider != null)
            {
                marginPercentageSlider.value = selectedMarginPercentage;
            }

            float marginAmount = gameManager != null ? gameManager.CurrentBalance * selectedMarginPercentage : 0f;

            if (marginRatioDisplayText != null)
            {
                marginRatioDisplayText.text = $"{currentSelectedMarginPercent}% (${marginAmount:N0})";
            }

            if (marginAmountText != null)
            {
                marginAmountText.text = $"MARGIN: ${marginAmount:N0} ({currentSelectedMarginPercent}%)";
            }

            // 프리셋 비율 버튼 하이라이트
            UpdatePresetHighlight(btnPresetRatio10, currentSelectedMarginPercent == 10);
            UpdatePresetHighlight(btnPresetRatio25, currentSelectedMarginPercent == 25);
            UpdatePresetHighlight(btnPresetRatio50, currentSelectedMarginPercent == 50);
            UpdatePresetHighlight(btnPresetRatio75, currentSelectedMarginPercent == 75);
            UpdatePresetHighlight(btnPresetRatio100, currentSelectedMarginPercent == 100);
        }

        // 3. 레버리지 배율 선택
        public void SelectLeverage(int lev)
        {
            int maxAllowed = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMaxAllowedLeverage() : 125;
            if (lev > maxAllowed)
            {
                currentSelectedLeverage = maxAllowed;
            }
            else
            {
                currentSelectedLeverage = Mathf.Clamp(lev, 1, 125);
            }

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

        // 플레이어 직접 포지션 진입. 보유 중인 포지션은 전용 매도 버튼으로만 정리합니다.
        private void OnLongButtonClicked()
        {
            if (tradingController == null) return;
            if (tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 포지션 보유 중에는 LONG 재클릭을 무시합니다. 포지션 매도 버튼을 사용하세요.");
                return;
            }

            tradingController.OpenPlayerPosition(TradingController.PositionType.Long, selectedMarginPercentage, currentSelectedLeverage);
        }

        private void OnShortButtonClicked()
        {
            if (tradingController == null) return;
            if (tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 포지션 보유 중에는 SHORT 재클릭을 무시합니다. 포지션 매도 버튼을 사용하세요.");
                return;
            }

            tradingController.OpenPlayerPosition(TradingController.PositionType.Short, selectedMarginPercentage, currentSelectedLeverage);
        }

        private void OnCloseButtonClicked()
        {
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 플레이어가 직접 포지션 종료(청산) 버튼을 클릭했습니다.");
                tradingController.ClosePosition();
            }
        }

        public void RefreshPanelUI()
        {
            if (tradingController == null) return;

            bool hasPosition = tradingController.CurrentPosition != TradingController.PositionType.None;
            bool isManualMode = tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual;
            bool showPlayerSellButton = hasPosition && isManualMode;

            // 진입 버튼은 포지션이 없을 때만 동작하고, 보유 중에는 전용 매도 버튼이 위를 덮습니다.
            if (longButton != null) longButton.interactable = isManualMode && !hasPosition;
            if (shortButton != null) shortButton.interactable = isManualMode && !hasPosition;
            if (closePositionButton != null)
            {
                closePositionButton.gameObject.SetActive(showPlayerSellButton);
                closePositionButton.interactable = showPlayerSellButton;
                if (showPlayerSellButton) closePositionButton.transform.SetAsLastSibling();
            }

            if (longSubtitleText != null)
            {
                longSubtitleText.text = hasPosition
                    ? (isManualMode ? "포지션 매도 버튼 사용" : (tradingController.CurrentPosition == TradingController.PositionType.Long ? "LONG 보유중 (AI)" : "대기중"))
                    : (isManualMode ? "LONG 수동 매수" : "AI 자동 매수 대기");
            }
            if (shortSubtitleText != null)
            {
                shortSubtitleText.text = hasPosition
                    ? (isManualMode ? "포지션 매도 버튼 사용" : (tradingController.CurrentPosition == TradingController.PositionType.Short ? "SHORT 보유중 (AI)" : "대기중"))
                    : (isManualMode ? "SHORT 수동 매도" : "AI 자동 매도 대기");
            }

            // 상태 오버레이 패널 및 탭 바 표시 여부
            if (positionStatusPanel != null)
            {
                positionStatusPanel.SetActive(hasPosition);
            }
            if (tabsBarContainer != null)
            {
                tabsBarContainer.SetActive(!hasPosition);
            }
            if (leverageControlContainer != null)
            {
                leverageControlContainer.SetActive(!hasPosition && currentControlMode == ControlMode.Leverage);
            }
            if (marginRatioControlContainer != null)
            {
                marginRatioControlContainer.SetActive(!hasPosition && currentControlMode == ControlMode.MarginRatio);
            }

            if (hasPosition)
            {
                UpdatePositionStatusNumbers();
                if (positionTypeText != null)
                {
                    string ownerTag = tradingController.CurrentOwner == TradingController.OwnerType.Player ? "[플레이어]" : "[AI]";
                    positionTypeText.text = $"{ownerTag} {tradingController.CurrentPosition} {tradingController.CurrentLeverage}x";
                    positionTypeText.color = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? bullishColor : bearishColor;
                }
                if (entryPriceText != null) entryPriceText.text = $"ENTRY: ${tradingController.EntryPrice:N1}";
                if (targetPriceText != null)
                {
                    if (tradingController.CurrentOwner == TradingController.OwnerType.Player)
                    {
                        targetPriceText.text = "TARGET: 직접 판단 익절 (수동)";
                    }
                    else
                    {
                        targetPriceText.text = tradingController.TargetPrice > 0f 
                            ? $"TARGET: ${tradingController.TargetPrice:N1} (AI 목표가)" 
                            : "TARGET: 무제한 (Overdose 뇌동매매)";
                    }
                }
                if (liquidationPriceText != null)
                {
                    if (tradingController.CurrentOwner == TradingController.OwnerType.Player)
                    {
                        liquidationPriceText.text = $"LIQ: ${tradingController.LiquidationPrice:N1} | STOP: 직접 판단 손절 (수동)";
                    }
                    else
                    {
                        string stopText = tradingController.StopLossPrice > 0f ? $"${tradingController.StopLossPrice:N1}" : "없음";
                        liquidationPriceText.text = $"LIQ: ${tradingController.LiquidationPrice:N1} | STOP: {stopText}";
                    }
                }
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
