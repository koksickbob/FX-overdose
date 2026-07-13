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

        private void Start()
        {
            if (marketEngine == null)
            {
                marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            }

            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated += HandlePriceUpdated;
                marketEngine.OnCandleClosed += HandleCandleClosed;
            }

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

            // 실시간 주가 변동에 따라 Live 캔들 화면 반영
            RefreshChartDisplay();
        }

        private void HandleCandleClosed(Timeframe tf, CandleData closedCandle)
        {
            if (tf == currentSelectedTimeframe)
            {
                RefreshChartDisplay();
            }
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

            float priceAreaBottom = chartHeight * 0.26f;
            float priceAreaHeight = Mathf.Max(10f, chartHeight - priceAreaBottom);
            float priceRange = Mathf.Max(0.001f, currentChartMaxPrice - currentChartMinPrice);
            float yPos = priceAreaBottom + ((currentPrice - currentChartMinPrice) / priceRange) * priceAreaHeight;

            // 라인 위치 이동
            currentPriceLineTransform.anchoredPosition = new Vector2(0f, yPos);

            // 우측 가격 태그 갱신
            if (currentPriceTagText != null)
            {
                currentPriceTagText.text = currentPrice.ToString("N1");
            }
            if (currentPriceTagRect != null)
            {
                currentPriceTagRect.anchoredPosition = new Vector2(0f, yPos);
            }
        }

        // 화면에 차트를 그리는 메인 로직
        public void RefreshChartDisplay()
        {
            if (marketEngine == null || chartAreaTransform == null || candlePrefab == null)
            {
                return;
            }

            List<CandleData> history = marketEngine.GetCandleHistory(currentSelectedTimeframe);
            CandleData liveCandle = marketEngine.GetLiveCandle(currentSelectedTimeframe);

            // 표시할 캔들 리스트 구성 (히스토리 중 최신 N개 + Live 캔들)
            List<CandleData> visibleCandles = new List<CandleData>();
            int startIndex = Mathf.Max(0, history.Count - maxVisibleCandles + 1);
            for (int i = startIndex; i < history.Count; i++)
            {
                visibleCandles.Add(history[i]);
            }
            if (liveCandle != null)
            {
                visibleCandles.Add(liveCandle);
            }

            if (visibleCandles.Count == 0) return;

            // 1. 동적 오토 스케일링 (최고가 및 최저가 계산 + 위아래 5% 패딩)
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

            // 우측 Y축 가격 눈금 텍스트 업데이트
            UpdateYAxisLabels(currentChartMinPrice, currentChartMaxPrice);

            // 2. 기존 활성화된 캔들을 풀로 반환
            foreach (var item in activeCandleItems)
            {
                item.gameObject.SetActive(false);
                pooledCandleItems.Push(item);
            }
            activeCandleItems.Clear();

            // 3. 차트 크기 및 캔들 간격 계산
            float chartWidth = chartAreaTransform.rect.width;
            float chartHeight = chartAreaTransform.rect.height;
            if (chartWidth <= 0f) chartWidth = 700f;
            if (chartHeight <= 0f) chartHeight = 400f;

            float volumeHeight = chartHeight * volumeAreaRatio;

            // 4. 캔들 배치 (우측부터 왼쪽으로 또는 왼쪽부터 우측으로 균등 배치)
            for (int i = 0; i < visibleCandles.Count; i++)
            {
                CandleData data = visibleCandles[i];
                float xPos = i * candleSpacing;

                CandleItemUI item = GetCandleItemFromPool();
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

            // 하단 시간 눈금 업데이트
            UpdateXAxisTimeLabels(visibleCandles);
        }

        // 우측 Y축 눈금 업데이트
        private void UpdateYAxisLabels(float min, float max)
        {
            if (yAxisPriceLabels == null || yAxisPriceLabels.Length == 0) return;

            float step = (max - min) / Mathf.Max(1, yAxisPriceLabels.Length - 1);
            for (int i = 0; i < yAxisPriceLabels.Length; i++)
            {
                if (yAxisPriceLabels[i] != null)
                {
                    float labelPrice = max - (i * step);
                    yAxisPriceLabels[i].text = labelPrice.ToString("N1");
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
                return pooledCandleItems.Pop();
            }

            CandleItemUI newItem = Instantiate(candlePrefab, chartAreaTransform);
            return newItem;
        }
    }
}
