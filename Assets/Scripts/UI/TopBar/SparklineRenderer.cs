using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.UI.TopBar
{
    public class SparklineRenderer : MonoBehaviour
    {
        [Header("UI 연결 및 프리팹")]
        [SerializeField] private RectTransform containerTransform;
        [SerializeField] private Image lineSegmentPrefab;
        [SerializeField] private float lineWidth = 2.5f;

        [Header("테마 색상 (TradingView)")]
        [SerializeField] private Color positiveColor = new Color(0.133f, 0.773f, 0.369f, 1f); // #22C55E (녹색)
        [SerializeField] private Color negativeColor = new Color(0.937f, 0.267f, 0.267f, 1f); // #EF4444 (적색)

        // 오브젝트 풀링 버퍼
        private List<Image> activeSegments = new List<Image>();
        private Stack<Image> pooledSegments = new Stack<Image>();

        private void Awake()
        {
            if (containerTransform == null)
            {
                containerTransform = GetComponent<RectTransform>();
            }
        }

        // 과거 고정점 리스트 + 현재 실시간 자산(Live Tip) 하이브리드 렌더러
        public void RefreshSparkline(List<float> historicalPoints, float liveTipValue)
        {
            if (containerTransform == null || lineSegmentPrefab == null)
            {
                return;
            }

            // 1. 그릴 전체 포인트 리스트 구성
            List<float> allPoints = new List<float>();
            if (historicalPoints != null)
            {
                allPoints.AddRange(historicalPoints);
            }
            allPoints.Add(liveTipValue);

            if (allPoints.Count < 2)
            {
                ClearActiveSegments();
                return;
            }

            // 2. 최소값 및 최대값 계산 (Y축 Auto-Fit)
            float minVal = float.MaxValue;
            float maxVal = float.MinValue;
            foreach (float v in allPoints)
            {
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }

            float range = maxVal - minVal;
            if (range < 0.001f)
            {
                minVal -= 1f;
                maxVal += 1f;
                range = 2f;
            }

            // 상하 5% 여백
            float padding = range * 0.05f;
            minVal -= padding;
            maxVal += padding;
            range = maxVal - minVal;

            // 3. 색상 결정 (시작점 대비 마지막 실시간 값이 같거나 높으면 양봉 색상)
            bool isPositive = liveTipValue >= allPoints[0];
            Color targetColor = isPositive ? positiveColor : negativeColor;

            // 4. 기존 활성화 선분 반환
            ClearActiveSegments();

            float width = containerTransform.rect.width;
            float height = containerTransform.rect.height;
            if (width <= 0f) width = 120f;
            if (height <= 0f) height = 40f;

            int pointCount = allPoints.Count;

            // 5. 각 선분(Segment) 배치 및 회전 계산
            for (int i = 0; i < pointCount - 1; i++)
            {
                float xA = (i / (float)(pointCount - 1)) * width;
                float yA = ((allPoints[i] - minVal) / range) * height;
                Vector2 pA = new Vector2(xA, yA);

                float xB = ((i + 1) / (float)(pointCount - 1)) * width;
                float yB = ((allPoints[i + 1] - minVal) / range) * height;
                Vector2 pB = new Vector2(xB, yB);

                Image segment = GetSegmentFromPool();
                segment.gameObject.SetActive(true);
                segment.transform.SetParent(containerTransform, false);
                segment.color = targetColor;

                RectTransform rect = segment.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                // 중심 위치
                rect.anchoredPosition = pA + (pB - pA) * 0.5f;

                // 길이 및 두께
                float distance = Vector2.Distance(pA, pB);
                rect.sizeDelta = new Vector2(distance, lineWidth);

                // 각도 회전
                float angle = Mathf.Atan2(pB.y - pA.y, pB.x - pA.x) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);

                activeSegments.Add(segment);
            }
        }

        private void ClearActiveSegments()
        {
            foreach (var seg in activeSegments)
            {
                if (seg != null)
                {
                    seg.gameObject.SetActive(false);
                    pooledSegments.Push(seg);
                }
            }
            activeSegments.Clear();
        }

        private Image GetSegmentFromPool()
        {
            if (pooledSegments.Count > 0)
            {
                return pooledSegments.Pop();
            }

            Image newSeg = Instantiate(lineSegmentPrefab, containerTransform);
            return newSeg;
        }
    }
}
