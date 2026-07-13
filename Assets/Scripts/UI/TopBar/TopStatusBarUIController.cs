using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using FXOverdose.Trading;

namespace FXOverdose.UI.TopBar
{
    public class TopStatusBarUIController : MonoBehaviour
    {
        [Header("시스템 및 렌더러 연결")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private SparklineRenderer sparklineRenderer;

        [Header("[카드 1] 날짜 및 시간 UI")]
        [SerializeField] private TMP_Text dayLabel;  // "DAY 03"
        [SerializeField] private TMP_Text timeLabel; // "23:47"

        [Header("[카드 2] BALANCE (총 자산/Equity) UI")]
        [SerializeField] private TMP_Text balanceValueLabel; // "$12,458.36"

        [Header("[카드 3] P&L (누적 수익률) UI")]
        [SerializeField] private TMP_Text pnlPercentageLabel; // "+18.47%"
        [SerializeField] private TMP_Text pnlAmountLabel;     // "+$1,458.36" (선택적 부가 정보)

        [Header("테마 색상 (TradingView)")]
        [SerializeField] private Color bullishColor = new Color(0.133f, 0.773f, 0.369f, 1f); // #22C55E
        [SerializeField] private Color bearishColor = new Color(0.937f, 0.267f, 0.267f, 1f); // #EF4444

        [Header("스파크라인 설정")]
        [SerializeField] private int maxHistoryPoints = 96; // 하루 24시간 기준 (15분 주기 * 4 * 24 = 96개)

        private List<float> equityHistory = new List<float>();
        private int lastRecordedMinute = -1;

        private void Start()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (tradingController == null) tradingController = FindFirstObjectByType<TradingController>();
            if (sparklineRenderer == null) sparklineRenderer = GetComponentInChildren<SparklineRenderer>();

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += HandleGameMinuteAdvanced;
            }

            // 초기 자산 기록
            float initialEquity = CalculateTotalEquity();
            equityHistory.Add(initialEquity);

            UpdateDayTimeUI();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= HandleGameMinuteAdvanced;
            }
        }

        private void Update()
        {
            // 매 프레임 실시간 총 자산(Total Equity) 및 P&L % 연산
            if (gameManager == null) return;

            float currentEquity = CalculateTotalEquity();
            UpdateBalanceUI(currentEquity);
            UpdatePnLUI(currentEquity);

            // 스파크라인 하이브리드 실시간 렌더링 (과거 궤적 고정점 + 매 프레임 실시간 끝점)
            if (sparklineRenderer != null)
            {
                sparklineRenderer.RefreshSparkline(equityHistory, currentEquity);
            }
        }

        private void HandleGameMinuteAdvanced()
        {
            UpdateDayTimeUI();

            // 인게임 15분마다 과거 궤적 고정점 기록
            if (gameManager != null && gameManager.CurrentMinute % 15 == 0 && gameManager.CurrentMinute != lastRecordedMinute)
            {
                lastRecordedMinute = gameManager.CurrentMinute;
                float currentEquity = CalculateTotalEquity();
                equityHistory.Add(currentEquity);

                if (equityHistory.Count > maxHistoryPoints)
                {
                    equityHistory.RemoveAt(0);
                }
            }
        }

        // 1. 날짜 및 시간 카드 업데이트
        private void UpdateDayTimeUI()
        {
            if (gameManager == null) return;

            if (dayLabel != null)
            {
                dayLabel.text = $"DAY {gameManager.CurrentDay:00}";
            }

            if (timeLabel != null)
            {
                timeLabel.text = $"{gameManager.CurrentHour:00}:{gameManager.CurrentMinute:00}";
            }
        }

        // 2. 총 자산 (BALANCE) 카드 업데이트
        private void UpdateBalanceUI(float currentEquity)
        {
            if (balanceValueLabel != null)
            {
                balanceValueLabel.text = $"${currentEquity:N2}";
            }
        }

        // 3. 누적 수익률 (P&L) 카드 업데이트
        private void UpdatePnLUI(float currentEquity)
        {
            float startingBalance = gameManager != null && gameManager.StartingBalance > 0 
                ? gameManager.StartingBalance : 10000f;

            float pnlDiff = currentEquity - startingBalance;
            float pnlPercentage = (pnlDiff / startingBalance) * 100f;

            Color targetColor = pnlDiff >= 0 ? bullishColor : bearishColor;
            string sign = pnlDiff >= 0 ? "+" : "";

            if (pnlPercentageLabel != null)
            {
                pnlPercentageLabel.text = $"{sign}{pnlPercentage:F2}%";
                pnlPercentageLabel.color = targetColor;
            }

            if (pnlAmountLabel != null)
            {
                pnlAmountLabel.text = $"{sign}${pnlDiff:N2}";
                pnlAmountLabel.color = targetColor;
            }
        }

        // 실시간 총 자산(Total Equity) 공식: 보유 현금 + 투입 증거금 + 실시간 미실현 손익
        public float CalculateTotalEquity()
        {
            if (gameManager == null) return 0f;

            float equity = gameManager.CurrentBalance;

            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                equity += tradingController.MarginAmount + tradingController.CalculateUnrealizedPnL();
            }

            return equity;
        }
    }
}
