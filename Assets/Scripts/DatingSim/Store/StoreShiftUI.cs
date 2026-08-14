using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.DatingSim.Store
{
    /// <summary>
    /// 편의점 HUD. 매니저의 이벤트만 구독하며 상태를 소유하지 않습니다.
    /// 월드 추적 요소(진행 바·인내 게이지·재고)는 스크린 스페이스 캔버스에 두고 WorldToScreenPoint로 따라붙입니다.
    /// </summary>
    public sealed class StoreShiftUI : MonoBehaviour
    {
        private StoreShiftManager manager;
        private StorePlayerController player;
        private Camera worldCamera;

        private TMP_Text timeText;
        private Image timeFill;
        private TMP_Text payText;
        private TMP_Text customerText;
        private TMP_Text feedbackText;
        private TMP_Text introText;

        private RectTransform holdBar;
        private Image holdFill;
        private TMP_Text carryText;
        private TMP_Text promptText;

        private GameObject resultPanel;
        private TMP_Text resultGrade;
        private TMP_Text resultBody;
        private Button resultButton;

        private readonly List<StoreCustomer> tracked = new List<StoreCustomer>();
        private readonly List<RectTransform> patienceBars = new List<RectTransform>();
        private readonly List<Image> patienceFills = new List<Image>();
        private readonly List<StoreStation> shelves = new List<StoreStation>();
        private readonly List<TMP_Text> shelfLabels = new List<TMP_Text>();

        private RectTransform canvasRect;
        private float feedbackTimer;

        private static readonly Color Cyan = new Color32(34, 211, 238, 255);
        private static readonly Color Grey = new Color32(92, 104, 120, 255);
        private static readonly Color Warn = new Color32(239, 68, 68, 255);
        private static readonly Color Pink = new Color32(244, 114, 182, 255);
        private static readonly Color Good = new Color32(74, 222, 128, 255);
        private static readonly Color Mid = new Color32(250, 204, 21, 255);

        public void Configure(StoreShiftManager shiftManager, StorePlayerController playerController, Camera camera,
            RectTransform canvas, TMP_Text time, Image timeGauge, TMP_Text pay, TMP_Text customers,
            TMP_Text feedback, TMP_Text intro, RectTransform hold, Image holdGauge, TMP_Text carry, TMP_Text prompt,
            GameObject result, TMP_Text grade, TMP_Text body, Button confirm,
            IEnumerable<StoreStation> shelfStations, IEnumerable<TMP_Text> shelfTexts,
            IEnumerable<RectTransform> patience, IEnumerable<Image> patienceGauges)
        {
            manager = shiftManager;
            player = playerController;
            worldCamera = camera;
            canvasRect = canvas;
            timeText = time;
            timeFill = timeGauge;
            payText = pay;
            customerText = customers;
            feedbackText = feedback;
            introText = intro;
            holdBar = hold;
            holdFill = holdGauge;
            carryText = carry;
            promptText = prompt;
            resultPanel = result;
            resultGrade = grade;
            resultBody = body;
            resultButton = confirm;

            shelves.AddRange(shelfStations);
            shelfLabels.AddRange(shelfTexts);
            patienceBars.AddRange(patience);
            patienceFills.AddRange(patienceGauges);

            manager.OnFeedback += ShowFeedback;
            manager.OnPayChanged += UpdatePay;
            manager.OnStateChanged += HandleState;
            manager.OnCustomerSpawned += c => tracked.Add(c);
            manager.OnShiftFinished += ShowResult;

            resultButton.onClick.AddListener(() => manager.ReturnToWorldMap());
            resultPanel.SetActive(false);
            UpdatePay(manager.CurrentPay);
            HandleState(manager.State);
        }

        private void OnDestroy()
        {
            if (manager == null) return;
            manager.OnFeedback -= ShowFeedback;
            manager.OnPayChanged -= UpdatePay;
            manager.OnStateChanged -= HandleState;
            manager.OnShiftFinished -= ShowResult;
        }

        private void Update()
        {
            if (manager == null) return;

            UpdateTime();
            UpdateCounters();
            UpdateHold();
            UpdatePatience();
            UpdateShelves();
            UpdateFeedback();
        }

        private void UpdateTime()
        {
            float total = manager.Config != null ? manager.Config.shiftDurationSeconds : 1f;
            float remaining = Mathf.Max(0f, manager.TimeRemaining);
            if (timeText != null)
                timeText.text = $"{Mathf.FloorToInt(remaining / 60f):00}:{Mathf.FloorToInt(remaining % 60f):00}";

            if (timeFill == null) return;
            float ratio = total > 0f ? remaining / total : 0f;
            timeFill.fillAmount = ratio;
            // 마지막 15%는 붉게 점멸시켜 마무리를 재촉합니다.
            timeFill.color = ratio > 0.15f ? Pink
                : Color.Lerp(Warn, Pink, Mathf.PingPong(Time.time * 2f, 1f));
        }

        private void UpdateCounters()
        {
            if (customerText == null) return;
            StoreShiftTally t = manager.Tally;
            customerText.text = t.Walkouts > 0
                ? $"처리 {t.CheckedOut}  ·  <color=#EF4444>이탈 {t.Walkouts}</color>"
                : $"처리 {t.CheckedOut}  ·  이탈 0";
        }

        private void UpdatePay(float value)
        {
            if (payText == null) return;
            payText.text = $"{Mathf.RoundToInt(value):N0} 원";
            payText.color = Warn;
            CancelInvoke(nameof(RestorePayColor));
            Invoke(nameof(RestorePayColor), 0.4f);
        }

        private void RestorePayColor()
        {
            if (payText != null) payText.color = new Color32(226, 245, 250, 255);
        }

        private void UpdateHold()
        {
            if (player == null || holdBar == null) return;

            StoreStation target = player.Active != null ? player.Active : player.Nearby;
            bool showBar = target != null && target.Progress > 0.001f;
            holdBar.gameObject.SetActive(showBar);

            if (showBar)
            {
                holdFill.fillAmount = Mathf.Clamp01(target.Progress);
                // 손을 뗀 채 값이 남아 있으면 회색 — 다시 와서 이어갈 수 있다는 유일한 신호입니다.
                holdFill.color = target.IsRunning ? Cyan : Grey;
                PlaceAtWorld(holdBar, player.transform.position + new Vector3(0f, 1.1f, 0f));
            }

            if (carryText != null)
            {
                carryText.text = player.Carry switch
                {
                    StoreCarry.Stock => $"상자 {player.CarriedStockUnits}",
                    StoreCarry.Tool => "청소도구",
                    _ => string.Empty
                };
                if (player.Carry != StoreCarry.None)
                    PlaceAtWorld(carryText.rectTransform, player.transform.position + new Vector3(0.95f, 1.1f, 0f));
            }

            if (promptText != null)
            {
                bool showPrompt = manager.IsInputAllowed && player.Nearby != null && player.Active == null;
                promptText.gameObject.SetActive(showPrompt);
                if (showPrompt)
                {
                    bool tap = player.Nearby.Kind == StoreStationKind.PickupStock ||
                               player.Nearby.Kind == StoreStationKind.PickupTool;
                    promptText.text = tap ? $"[E]  {player.Nearby.DisplayName}" : $"[E] 길게  {player.Nearby.DisplayName}";
                    PlaceAtWorld(promptText.rectTransform, player.Nearby.transform.position + new Vector3(0f, 1.4f, 0f));
                }
            }
        }

        private void UpdatePatience()
        {
            int slot = 0;
            for (int i = 0; i < tracked.Count && slot < patienceBars.Count; i++)
            {
                StoreCustomer customer = tracked[i];
                if (customer == null || !customer.IsWaiting) continue;

                float ratio = customer.PatienceRatio;
                patienceBars[slot].gameObject.SetActive(true);
                patienceFills[slot].fillAmount = ratio;
                patienceFills[slot].color = ratio > 0.6f ? Good : ratio > 0.3f ? Mid : Warn;
                PlaceAtWorld(patienceBars[slot], customer.transform.position + new Vector3(0f, 1.0f, 0f));
                slot++;
            }

            for (int i = slot; i < patienceBars.Count; i++) patienceBars[i].gameObject.SetActive(false);
            tracked.RemoveAll(c => c == null);
        }

        private void UpdateShelves()
        {
            for (int i = 0; i < shelves.Count && i < shelfLabels.Count; i++)
            {
                if (shelves[i] == null || shelfLabels[i] == null) continue;
                shelfLabels[i].text = $"{shelves[i].Stock}/{shelves[i].MaxStock}";
                shelfLabels[i].color = shelves[i].IsShelfEmpty ? Warn : new Color32(226, 245, 250, 255);
                PlaceAtWorld(shelfLabels[i].rectTransform, shelves[i].transform.position + new Vector3(0f, 1.25f, 0f));
            }
        }

        private void UpdateFeedback()
        {
            if (feedbackTimer <= 0f) return;
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f && feedbackText != null) feedbackText.text = string.Empty;
        }

        private void ShowFeedback(string message)
        {
            if (feedbackText == null) return;
            feedbackText.text = message;
            feedbackTimer = 2.5f;
        }

        private void HandleState(StoreShiftState state)
        {
            if (introText == null) return;
            bool intro = state == StoreShiftState.Intro;
            introText.gameObject.SetActive(intro);
            if (intro) introText.text = "근무 시작!";
        }

        private void ShowResult(StoreShiftResult result)
        {
            if (resultPanel == null) return;
            resultPanel.SetActive(true);

            if (resultGrade != null)
            {
                resultGrade.text = result.Grade.ToString();
                resultGrade.color = result.Grade switch
                {
                    'S' => new Color32(255, 211, 92, 255),
                    'A' => Cyan,
                    'B' => Good,
                    'C' => Grey,
                    _ => Warn
                };
            }

            if (resultBody == null) return;

            StoreShiftTally t = result.Tally;
            StoreConfig cfg = manager.Config;
            StringBuilder sb = new StringBuilder();

            if (result.Aborted)
            {
                sb.AppendLine("<color=#EF4444><b>근무 중도 포기</b></color>");
                sb.AppendLine("근무 시간을 끝까지 채워야 기본급이 지급됩니다.");
                sb.AppendLine();
            }

            // 기본급이 맨 위, 감점이 아래. 이 패널은 "얼마를 벌었나"가 아니라 "얼마를 깎였나"를 읽는 표입니다.
            sb.AppendLine($"기본급                {Mathf.RoundToInt(result.BasePay):N0} 원");
            sb.AppendLine("──────────────────────────");
            sb.AppendLine($"계산 완료   {t.CheckedOut} 명            0");
            AppendPenalty(sb, "손님 이탈", t.Walkouts, "명", t.Walkouts * cfg.walkoutPenalty);
            AppendPenalty(sb, "남은 오염", t.RemainingDirt, "곳", t.RemainingDirt * cfg.dirtPenaltyPerSpot);
            AppendPenalty(sb, "빈 매대", t.EmptyShelves, "곳", t.EmptyShelves * cfg.emptyShelfPenalty);
            AppendPenalty(sb, "두고 간 물건", t.RemainingLeftovers, "개", t.RemainingLeftovers * cfg.leftoverPenalty);
            sb.AppendLine("══════════════════════════");
            sb.AppendLine($"<b>오늘의 일급          {Mathf.RoundToInt(result.Pay):N0} 원</b>");

            if (result.Gifts != null && result.Gifts.Count > 0)
                sb.AppendLine($"\n선물   {DescribeGifts(result.Gifts)}");

            if (result.SaveFailed)
                sb.AppendLine("\n<color=#EF4444>저장에 실패했습니다. 다음 저장 때 함께 기록됩니다.</color>");

            resultBody.text = sb.ToString();
        }

        private static void AppendPenalty(StringBuilder sb, string label, int count, string unit, float penalty)
        {
            if (count <= 0)
            {
                sb.AppendLine($"{label}   0 {unit}            0");
                return;
            }
            sb.AppendLine($"<color=#EF4444>{label}   {count} {unit}        - {Mathf.RoundToInt(penalty):N0}</color>");
        }

        private static string DescribeGifts(List<string> ids)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            for (int i = 0; i < ids.Count; i++)
            {
                counts.TryGetValue(ids[i], out int value);
                counts[ids[i]] = value + 1;
            }

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (sb.Length > 0) sb.Append(",  ");
                sb.Append($"{DisplayName(pair.Key)} × {pair.Value}");
            }
            return sb.ToString();
        }

        private static string DisplayName(string itemId) => itemId switch
        {
            "energy_drink" => "에너지 드링크",
            "dessert" => "파르페",
            _ => itemId
        };

        /// <summary>월드 좌표를 캔버스 좌표로 옮깁니다. 카메라가 없으면 조용히 넘어갑니다.</summary>
        private void PlaceAtWorld(RectTransform target, Vector3 worldPosition)
        {
            if (target == null || worldCamera == null || canvasRect == null) return;
            Vector2 screen = worldCamera.WorldToScreenPoint(worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                target.anchoredPosition = local;
        }
    }
}
