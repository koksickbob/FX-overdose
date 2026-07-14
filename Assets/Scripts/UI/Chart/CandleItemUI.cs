using UnityEngine;
using UnityEngine.UI;
using FXOverdose.Trading;

#pragma warning disable CS0649

namespace FXOverdose.UI.Chart
{
    public class CandleItemUI : MonoBehaviour
    {
        [Header("UI 컴포넌트 연결")]
        [SerializeField] private RectTransform candleContainerTransform;
        [SerializeField] private RectTransform upperWickRect;
        [SerializeField] private RectTransform bodyRect;
        [SerializeField] private RectTransform lowerWickRect;
        [SerializeField] private Image upperWickImage;
        [SerializeField] private Image bodyImage;
        [SerializeField] private Image lowerWickImage;

        [Header("거래량 바 연결")]
        [SerializeField] private RectTransform volumeBarRect;
        [SerializeField] private Image volumeBarImage;

        // 양봉 및 음봉 색상 설정 (TradingView 테마)
        private readonly Color bullishColor = new Color(0.25f, 0.80f, 0.60f, 1f); // 민트 양봉
        private readonly Color bearishColor = new Color(0.96f, 0.31f, 0.40f, 1f); // 코랄 음봉
        private readonly Color bullishVolumeColor = new Color(0.12f, 0.48f, 0.52f, 0.62f);
        private readonly Color bearishVolumeColor = new Color(0.55f, 0.18f, 0.27f, 0.62f);

        private CandleData currentData;
        public CandleData CurrentData => currentData;

        // 캔들 및 거래량 렌더링 갱신
        public void UpdateCandleDisplay(
            CandleData data,
            float chartMinPrice,
            float chartMaxPrice,
            float chartHeight,
            float xPos,
            float candleWidth,
            float maxVolume,
            float volumeAreaHeight)
        {
            if (data == null) return;
            currentData = data;

            // 가격 차이가 0 이하인 경우 방어 코딩
            float priceRange = Mathf.Max(0.001f, chartMaxPrice - chartMinPrice);

            // 1. 캔들 컨테이너의 X 좌표 및 너비 설정
            if (candleContainerTransform == null)
            {
                candleContainerTransform = GetComponent<RectTransform>();
            }

            if (candleContainerTransform != null)
            {
                candleContainerTransform.anchorMin = Vector2.zero;
                candleContainerTransform.anchorMax = Vector2.zero;
                candleContainerTransform.pivot = Vector2.zero;
                candleContainerTransform.anchoredPosition = new Vector2(xPos, 0f);
                candleContainerTransform.sizeDelta = new Vector2(candleWidth, chartHeight);
            }

            // 양봉/음봉 색상 결정
            bool isBullish = data.IsBullish;
            Color targetColor = isBullish ? bullishColor : bearishColor;
            Color volColor = isBullish ? bullishVolumeColor : bearishVolumeColor;

            if (upperWickImage != null) upperWickImage.color = targetColor;
            if (bodyImage != null) bodyImage.color = targetColor;
            if (lowerWickImage != null) lowerWickImage.color = targetColor;
            if (volumeBarImage != null) volumeBarImage.color = volColor;

            // 2. Y 좌표 변환 함수 (차트 영역 상단 26% ~ 100% 전용 구역으로 분리)
            float priceAreaBottom = chartHeight * 0.26f;
            float priceAreaHeight = Mathf.Max(10f, chartHeight - priceAreaBottom);

            float PriceToY(float price)
            {
                return priceAreaBottom + ((price - chartMinPrice) / priceRange) * priceAreaHeight;
            }

            float highY = PriceToY(data.high);
            float lowY = PriceToY(data.low);
            float topY = PriceToY(data.BodyTop);
            float bottomY = PriceToY(data.BodyBottom);

            // 3. 몸통(Body) 배치 및 크기 계산
            float bodyHeight = Mathf.Max(2f, topY - bottomY); // 도지(Doji) 방어로 최소 2px 보장
            if (bodyRect != null)
            {
                bodyRect.anchorMin = new Vector2(0.5f, 0f);
                bodyRect.anchorMax = new Vector2(0.5f, 0f);
                bodyRect.pivot = new Vector2(0.5f, 0f);
                bodyRect.anchoredPosition = new Vector2(0f, bottomY);
                bodyRect.sizeDelta = new Vector2(Mathf.Max(2f, candleWidth * 0.75f), bodyHeight);
            }

            // 4. 윗꼬리(Upper Wick) 배치
            if (upperWickRect != null)
            {
                float wickHeight = Mathf.Max(0f, highY - topY);
                upperWickRect.anchorMin = new Vector2(0.5f, 0f);
                upperWickRect.anchorMax = new Vector2(0.5f, 0f);
                upperWickRect.pivot = new Vector2(0.5f, 0f);
                upperWickRect.anchoredPosition = new Vector2(0f, topY);
                upperWickRect.sizeDelta = new Vector2(2f, wickHeight);
                upperWickImage.enabled = wickHeight > 0.5f;
            }

            // 5. 아랫꼬리(Lower Wick) 배치
            if (lowerWickRect != null)
            {
                float wickHeight = Mathf.Max(0f, bottomY - lowY);
                lowerWickRect.anchorMin = new Vector2(0.5f, 0f);
                lowerWickRect.anchorMax = new Vector2(0.5f, 0f);
                lowerWickRect.pivot = new Vector2(0.5f, 0f);
                lowerWickRect.anchoredPosition = new Vector2(0f, lowY);
                lowerWickRect.sizeDelta = new Vector2(2f, wickHeight);
                lowerWickImage.enabled = wickHeight > 0.5f;
            }

            // 6. 하단 거래량 바(Volume Bar) 배치 (하단 0% ~ 22% 전용 구역으로 분리)
            if (volumeBarRect != null)
            {
                float maxVolHeight = chartHeight * 0.22f; // 거래량 최대 높이 22% 제한
                float volRatio = data.volume / Mathf.Max(1f, maxVolume);
                float volHeight = Mathf.Max(2f, volRatio * maxVolHeight);
                volumeBarRect.anchorMin = new Vector2(0.5f, 0f);
                volumeBarRect.anchorMax = new Vector2(0.5f, 0f);
                volumeBarRect.pivot = new Vector2(0.5f, 0f);
                volumeBarRect.anchoredPosition = new Vector2(0f, 0f);
                volumeBarRect.sizeDelta = new Vector2(Mathf.Max(2f, candleWidth * 0.6f), volHeight);
            }
        }
    }
}
