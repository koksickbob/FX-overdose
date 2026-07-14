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

        // 실시간 현재가 라인 및 우측 태그 위치 갱신 (차트 영역 경계 엄격 클램핑 및 앵커 오프셋 적용)
        private void UpdateCurrentPriceLine(float currentPrice)
        {
            if (currentPriceLineTransform == null || chartAreaTransform == null) return;

            float chartHeight = chartAreaTransform.rect.height;
            if (chartHeight <= 0f) chartHeight = 400f;

            // 가격 캔들이 그려지는 구간: 하단 26%(거래량 영역) ~ 상단 100%
            float priceAreaBottom = chartHeight * volumeAreaRatio + chartHeight * 0.01f; // 거래량 구분선 바로 위
            float priceAreaTop = chartHeight; // 차트 영역 상단 경계 (ChartArea의 RectMask2D가 헤더 침범을 잘라냄)
            float priceAreaHeight = Mathf.Max(10f, priceAreaTop - priceAreaBottom);
            float priceRange = Mathf.Max(0.001f, currentChartMaxPrice - currentChartMinPrice);
            
            // 바닥(0)을 기준으로 한 실제 Y 절대 높이
            float absoluteYPos = priceAreaBottom + ((currentPrice - currentChartMinPrice) / priceRange) * priceAreaHeight;

            // 차트 영역 경계를 절대로 넘지 않도록 엄격 클램핑
            absoluteYPos = Mathf.Clamp(absoluteYPos, priceAreaBottom, priceAreaTop);

            // 현재가 라인 및 태그의 RectTransform Anchor.y 가 0.5(중앙)로 설정되어 있으므로
            // 바닥(0) 기준 높이에서 차트 절반 높이를 빼주어야 정확한 anchoredPosition 값이 나옴
            float anchoredYPos = absoluteYPos - (chartHeight / 2f);

            // 라인 위치 이동
            currentPriceLineTransform.anchoredPosition = new Vector2(0f, anchoredYPos);

            // 우측 가격 태그 갱신
            if (currentPriceTagText != null)
            {
                currentPriceTagText.text = currentPrice.ToString("N1");
            }
            if (currentPriceTagRect != null)
            {
                currentPriceTagRect.anchoredPosition = new Vector2(0f, anchoredYPos);
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

            // 스케일 갱신 후 실시간 현재가 라인 위치 동기화 및 클램핑
            UpdateCurrentPriceLine(marketEngine.CurrentPrice);
        }

        // 우측 Y축 눈금 업데이트 (캔들 가격 구간에 동기화)
        private void UpdateYAxisLabels(float min, float max)
        {
            if (yAxisPriceLabels == null || yAxisPriceLabels.Length == 0) return;

            // Y축 라벨은 거래량 영역(하단 ~26%)을 제외한 가격 캔들 구간(26%~100%)에만 배치되어 있으므로
            // min~max 범위를 해당 라벨 개수에 맞춰 균등 분할하여 가격을 표시
            int labelCount = yAxisPriceLabels.Length;
            float priceStep = (max - min) / Mathf.Max(1, labelCount - 1);

            for (int i = 0; i < labelCount; i++)
            {
                if (yAxisPriceLabels[i] != null)
                {
                    // 라벨 인덱스 0이 차트 상단(max), 마지막 인덱스가 하단(min)에 해당
                    float labelPrice = max - (i * priceStep);
                    yAxisPriceLabels[i].text = labelPrice.ToString("N1");

                    // 라벨의 앵커 Y를 실제 가격 위치에 동기화 (거래량 구간 0.26 ~ 가격 상단 1.0)
                    RectTransform lblRect = yAxisPriceLabels[i].transform.parent.GetComponent<RectTransform>();
                    if (lblRect != null)
                    {
                        float normalizedY = 0.26f + ((labelPrice - min) / Mathf.Max(0.001f, max - min)) * 0.74f;
                        normalizedY = Mathf.Clamp(normalizedY, 0.26f, 1.0f);
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
                return pooledCandleItems.Pop();
            }

            CandleItemUI newItem = Instantiate(candlePrefab, chartAreaTransform);
            return newItem;
        }
    }
}
