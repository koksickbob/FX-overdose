using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;

#pragma warning disable CS0649

namespace FXOverdose.UI.Chart
{
    public class ChartUIController : MonoBehaviour
    {
        [Header("시스템 연결")]
        [SerializeField] private MarketSimulationEngine marketEngine;

        [Header("상단 가격 표시 헤더")]
        [SerializeField] private TMP_Text priceHeaderLabel;
        [SerializeField] private TMP_Text priceChangeLabel;

        [Header("타임프레임 선택 버튼")]
        [SerializeField] private Button btn1m;
        [SerializeField] private Button btn5m;
        [SerializeField] private Button btn15m;
        [SerializeField] private Button btn1h;
        [SerializeField] private Button btn4h;
        [SerializeField] private Button btn1D;

        [Header("차트 영역 및 캔들 프리팹")]
        [SerializeField] private RectTransform chartAreaTransform;
        [SerializeField] private CandleItemUI candlePrefab;
        [SerializeField] private int maxVisibleCandles = 50;
        [SerializeField] private float candleSpacing = 14f;
        [SerializeField] private float candleWidth = 10f;
        [SerializeField] private float volumeAreaRatio = 0.25f; // 차트 하단 25% 거래량 영역
        [SerializeField] private float internalPriceAxisWidth = 86f; // 차트 내부 우측 가격축 전용 폭

        [Header("우측 가격축 및 하단 시간축")]
        [SerializeField] private TMP_Text[] yAxisPriceLabels;
        [SerializeField] private TMP_Text[] xAxisTimeLabels;

        [Header("실시간 현재가 라인 (Current Price Line)")]
        [SerializeField] private RectTransform currentPriceLineTransform;
        [SerializeField] private Image currentPriceLineImage;
        [SerializeField] private RectTransform currentPriceTagRect;
        [SerializeField] private TMP_Text currentPriceTagText;
        [SerializeField] private Image currentPriceTagBackground;

        // 색상 토큰 (TradingView 테마)
        private readonly Color cyanHighlight = new Color(0.024f, 0.714f, 0.831f, 1f); // #06B6D4
        private readonly Color inactiveButtonColor = new Color(0.122f, 0.161f, 0.235f, 1f);
        private readonly Color bullishText = new Color(0.133f, 0.773f, 0.369f, 1f);
        private readonly Color bearishText = new Color(0.937f, 0.267f, 0.267f, 1f);

        // 오브젝트 풀링 및 상태 변수
        private List<CandleItemUI> activeCandleItems = new List<CandleItemUI>();
        private Stack<CandleItemUI> pooledCandleItems = new Stack<CandleItemUI>();
        private Timeframe currentSelectedTimeframe = Timeframe.M5; // 기본 5분봉 선택

        private float currentChartMinPrice;
        private float currentChartMaxPrice;
        private GameManager gameManager;

        private void Start()
        {
            if (marketEngine == null)
            {
                marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            }
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated += HandlePriceUpdated;
                marketEngine.OnCandleClosed += HandleCandleClosed;
            }
            if (gameManager != null)
            {
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }

            CleanupOldScrollView();
            SetupTimeframeButtons();
            SelectTimeframe(Timeframe.M5);
        }

        private void OnDestroy()
        {
            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated -= HandlePriceUpdated;
                marketEngine.OnCandleClosed -= HandleCandleClosed;
            }
            if (gameManager != null)
            {
                gameManager.OnFastForwardEnded -= HandleFastForwardEnded;
            }
        }

        private void CleanupOldScrollView()
        {
            if (chartAreaTransform != null)
            {
                Transform existingScroll = chartAreaTransform.Find("CandleScrollView");
                if (existingScroll != null)
                {
                    Destroy(existingScroll.gameObject);
                }
            }
        }

        private void SetupTimeframeButtons()
        {
            if (btn1m != null) btn1m.onClick.AddListener(() => SelectTimeframe(Timeframe.M1));
            if (btn5m != null) btn5m.onClick.AddListener(() => SelectTimeframe(Timeframe.M5));
            if (btn15m != null) btn15m.onClick.AddListener(() => SelectTimeframe(Timeframe.M15));
            if (btn1h != null) btn1h.onClick.AddListener(() => SelectTimeframe(Timeframe.H1));
            if (btn4h != null) btn4h.onClick.AddListener(() => SelectTimeframe(Timeframe.H4));
            if (btn1D != null) btn1D.onClick.AddListener(() => SelectTimeframe(Timeframe.D1));
        }

        public void SelectTimeframe(Timeframe tf)
        {
            currentSelectedTimeframe = tf;

            // 버튼 색상 업데이트
            UpdateButtonHighlight(btn1m, tf == Timeframe.M1);
            UpdateButtonHighlight(btn5m, tf == Timeframe.M5);
            UpdateButtonHighlight(btn15m, tf == Timeframe.M15);
            UpdateButtonHighlight(btn1h, tf == Timeframe.H1);
            UpdateButtonHighlight(btn4h, tf == Timeframe.H4);
            UpdateButtonHighlight(btn1D, tf == Timeframe.D1);

            RefreshChartDisplay();
        }

        private void UpdateButtonHighlight(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? cyanHighlight : inactiveButtonColor;
            }
        }

        private void HandlePriceUpdated(float newPrice)
        {
            UpdatePriceHeader(newPrice);
            UpdateCurrentPriceLine(newPrice);

            // 💡 고속 시간 패스(AdvanceGameMinutes) 중에는 프레임당 수천 번의 UI 캔들 재배치를 생략하여 렉(Lag)을 원천 차단합니다!
            if (marketEngine != null && marketEngine.IsFastForwarding) return;

            // 실시간 주가 변동에 따라 Live 캔들 화면 반영
            RefreshChartDisplay();
        }

        private void HandleCandleClosed(Timeframe tf, CandleData closedCandle)
        {
            if (tf == currentSelectedTimeframe)
            {
                if (marketEngine != null && marketEngine.IsFastForwarding) return;
                RefreshChartDisplay();
            }
        }

        private void HandleFastForwardEnded()
        {
            if (marketEngine != null)
            {
                UpdatePriceHeader(marketEngine.CurrentPrice);
                UpdateCurrentPriceLine(marketEngine.CurrentPrice);
            }
            RefreshChartDisplay();
        }

        // 상단 가격 헤더 (67,842.1 및 변동률) 업데이트
        private void UpdatePriceHeader(float price)
        {
            if (priceHeaderLabel != null)
            {
                priceHeaderLabel.text = price.ToString("N1");
            }

            if (priceChangeLabel != null && marketEngine != null)
            {
                // 24시간 시작 시점 대비 변동률 계산
                float open24h = marketEngine.GetCandleHistory(Timeframe.D1).Count > 0 
                    ? marketEngine.GetCandleHistory(Timeframe.D1)[0].open 
                    : price;

                float diff = price - open24h;
                float pct = open24h > 0 ? (diff / open24h) * 100f : 0f;
                string sign = diff >= 0 ? "+" : "";

                priceChangeLabel.text = $"≈ ${price:N2} {sign}{pct:F2}%";
                priceChangeLabel.color = diff >= 0 ? bullishText : bearishText;
            }
        }

        // 실시간 현재가 라인 및 우측 태그 위치 갱신
        private void UpdateCurrentPriceLine(float currentPrice)
        {
            if (currentPriceLineTransform == null || chartAreaTransform == null) return;

            float chartHeight = chartAreaTransform.rect.height;
            if (chartHeight <= 0f) chartHeight = 400f;

            float priceAreaBottom = chartHeight * volumeAreaRatio + chartHeight * 0.01f;
            float priceAreaTop = chartHeight - 4f;
            float priceAreaHeight = Mathf.Max(10f, priceAreaTop - priceAreaBottom);
            float priceRange = Mathf.Max(0.001f, currentChartMaxPrice - currentChartMinPrice);
            float absoluteYPos = priceAreaBottom + ((currentPrice - currentChartMinPrice) / priceRange) * priceAreaHeight;
            absoluteYPos = Mathf.Clamp(absoluteYPos, priceAreaBottom, priceAreaTop);

            float anchoredYPos = absoluteYPos - (chartHeight * 0.5f);

            currentPriceLineTransform.anchoredPosition = new Vector2(0f, anchoredYPos);

            if (currentPriceTagText != null)
            {
                currentPriceTagText.text = currentPrice.ToString("N1");
            }
            if (currentPriceTagRect != null)
            {
                currentPriceTagRect.anchoredPosition = Vector2.zero;
            }
        }

        // 화면에 차트를 그리는 메인 로직
        public void RefreshChartDisplay()
        {
            if (marketEngine == null || chartAreaTransform == null || candlePrefab == null)
            {
                return;
            }

            CleanupOldScrollView();

            List<CandleData> history = marketEngine.GetCandleHistory(currentSelectedTimeframe);
            CandleData liveCandle = marketEngine.GetLiveCandle(currentSelectedTimeframe);

            float chartWidth = chartAreaTransform.rect.width > 0 ? chartAreaTransform.rect.width : 700f;
            float chartHeight = chartAreaTransform.rect.height > 0 ? chartAreaTransform.rect.height : 400f;
            float plotWidth = Mathf.Max(candleWidth, chartWidth - internalPriceAxisWidth);

            // 고정 캔들 폭 및 간격 유지 (예: 캔들폭 10f, 간격 14f)
            float effectiveSpacing = Mathf.Max(candleSpacing, candleWidth + 4f);

            // 최적화를 위해 화면 영역(plotWidth) 내에 들어올 수 있는 최대 캔들 개수 산출
            int maxCandlesThatFit = Mathf.Max(5, Mathf.FloorToInt(plotWidth / effectiveSpacing));
            if (maxVisibleCandles > 0 && maxCandlesThatFit > maxVisibleCandles)
            {
                maxCandlesThatFit = maxVisibleCandles;
            }

            List<CandleData> visibleCandles = new List<CandleData>();
            int startIndex = Mathf.Max(0, history.Count - maxCandlesThatFit + 1);
            for (int i = startIndex; i < history.Count; i++)
            {
                visibleCandles.Add(history[i]);
            }
            if (liveCandle != null)
            {
                visibleCandles.Add(liveCandle);
            }

            if (visibleCandles.Count == 0) return;

            // 1. 오토 스케일링 (화면에 보이는 캔들 기준 최고가/최저가 및 거래량 계산)
            float minPrice = float.MaxValue;
            float maxPrice = float.MinValue;
            float maxVolume = float.MinValue;

            foreach (var c in visibleCandles)
            {
                if (c.high > maxPrice) maxPrice = c.high;
                if (c.low < minPrice) minPrice = c.low;
                if (c.volume > maxVolume) maxVolume = c.volume;
            }

            float padding = (maxPrice - minPrice) * 0.05f;
            if (padding < 10f) padding = 10f;

            currentChartMinPrice = minPrice - padding;
            currentChartMaxPrice = maxPrice + padding;

            UpdateYAxisLabels(currentChartMinPrice, currentChartMaxPrice);

            // 2. 기존 활성화된 캔들을 풀로 반환 (지나간 캔들 및 거래량봉 제거/풀링)
            foreach (var item in activeCandleItems)
            {
                if (item != null && item.gameObject != null)
                {
                    item.gameObject.SetActive(false);
                    pooledCandleItems.Push(item);
                }
            }
            activeCandleItems.Clear();

            float volumeHeight = chartHeight * volumeAreaRatio;

            // 3. 고정 간격으로 캔들 배치 (화면 내 최신 캔들만 렌더링, 왼쪽으로 밀려난 캔들은 삭제 효과)
            for (int i = 0; i < visibleCandles.Count; i++)
            {
                CandleData data = visibleCandles[i];
                float xPos = i * effectiveSpacing;

                CandleItemUI item = GetCandleItemFromPool();
                if (item != null)
                {
                    item.gameObject.SetActive(true);
                    item.transform.SetParent(chartAreaTransform, false);

                    item.UpdateCandleDisplay(
                        data,
                        currentChartMinPrice,
                        currentChartMaxPrice,
                        chartHeight,
                        xPos,
                        candleWidth,
                        maxVolume,
                        volumeHeight
                    );

                    activeCandleItems.Add(item);
                }
            }

            UpdateXAxisTimeLabels(visibleCandles);
            UpdateCurrentPriceLine(marketEngine.CurrentPrice);
        }

        // 우측 Y축 눈금 업데이트
        private void UpdateYAxisLabels(float min, float max)
        {
            if (yAxisPriceLabels == null || yAxisPriceLabels.Length == 0) return;

            int labelCount = yAxisPriceLabels.Length;
            
            float startY = 0.28f;
            float endY = 0.96f;
            float stepY = (endY - startY) / Mathf.Max(1, labelCount - 1);

            for (int i = 0; i < labelCount; i++)
            {
                if (yAxisPriceLabels[i] != null)
                {
                    float normalizedY = startY + (i * stepY);
                    float labelPrice = min + ((normalizedY - 0.26f) / 0.74f) * (max - min);
                    yAxisPriceLabels[i].text = labelPrice.ToString("N1");

                    RectTransform lblRect = yAxisPriceLabels[i].transform.parent.GetComponent<RectTransform>();
                    if (lblRect != null)
                    {
                        lblRect.anchorMin = new Vector2(lblRect.anchorMin.x, normalizedY);
                        lblRect.anchorMax = new Vector2(lblRect.anchorMax.x, normalizedY);
                    }
                }
            }
        }

        // 하단 X축 시간 눈금 업데이트
        private void UpdateXAxisTimeLabels(List<CandleData> visibleCandles)
        {
            if (xAxisTimeLabels == null || xAxisTimeLabels.Length == 0 || visibleCandles.Count == 0) return;

            int step = Mathf.Max(1, visibleCandles.Count / xAxisTimeLabels.Length);
            for (int i = 0; i < xAxisTimeLabels.Length; i++)
            {
                if (xAxisTimeLabels[i] != null)
                {
                    int idx = Mathf.Clamp(i * step, 0, visibleCandles.Count - 1);
                    long minutes = visibleCandles[idx].timestampMinutes;
                    int hour = (int)((Mathf.Abs(minutes) / 60) % 24);
                    int min = (int)(Mathf.Abs(minutes) % 60);
                    xAxisTimeLabels[i].text = $"{hour:00}:{min:00}";
                }
            }
        }

        private CandleItemUI GetCandleItemFromPool()
        {
            if (pooledCandleItems.Count > 0)
            {
                CandleItemUI pooledItem = pooledCandleItems.Pop();
                if (pooledItem != null)
                {
                    pooledItem.transform.SetParent(chartAreaTransform, false);
                    return pooledItem;
                }
            }

            CandleItemUI newItem = Instantiate(candlePrefab, chartAreaTransform);
            return newItem;
        }
    }
}
