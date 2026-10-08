using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.DatingSim.Core;
using FXOverdose.Core;

namespace FXOverdose.DatingSim.WorldMap
{
    public class WorldMapUIController : MonoBehaviour
    {
        [Header("Top Bar UI")]
        [SerializeField] private TextMeshProUGUI staminaText;
        [SerializeField] private TextMeshProUGUI timeSlotText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private TextMeshProUGUI affectionText;

        [Header("Actions")]
        [SerializeField] private Button firstJobButton;
        [SerializeField] private Button firstDateButton;
        [SerializeField] private Button roomButton;
        
        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        private Button arcadeButton;
        private Button cafeButton;
        private Button primaryActionButton;
        private Button[] filterButtons;
        private GameObject[] locationMarkers;
        private TextMeshProUGUI detailTitle;
        private TextMeshProUGUI detailMeta;
        private TextMeshProUGUI detailDescription;
        private TextMeshProUGUI actionLabel;
        private Image staminaFill;
        private Image affectionFill;
        private Image[] timePips;
        private RectTransform[] routeDots;
        private RectTransform[] markerRects;
        private MapLocation selectedLocation = MapLocation.Room;
        private Image regionMapBackground;
        private TMP_Text regionTitle;
        private Button previousRegionButton;
        private Button nextRegionButton;
        private GameObject locationPinLayer;
        private GameObject locationDetailPanel;
        private int currentRegionIndex;

        // 알바 결과 모달 (ConfigureJobResultModal로 주입)
        private GameObject jobResultModal;
        private TextMeshProUGUI jobResultTitle;
        private TextMeshProUGUI jobResultPay;
        private GameObject jobResultGiftRow;
        private TextMeshProUGUI jobResultGift;
        private TextMeshProUGUI jobResultBody;

        private static readonly string[] RegionNames = { "SEOUL", "INCHEON", "GAPYEONG", "DONGHAE", "BUSAN", "JEJU" };
        private static readonly string[] RegionSpritePaths =
        {
            "DatingSim/WorldMap/UI/SeoulMapBackground",
            "DatingSim/WorldMap/UI/IncheonMapBackground",
            "DatingSim/WorldMap/UI/GapyeongMapBackground",
            "DatingSim/WorldMap/UI/DonghaeMapBackground",
            "DatingSim/WorldMap/UI/BusanMapBackground",
            "DatingSim/WorldMap/UI/JejuMapBackground"
        };

        private enum MapLocation { Room, Job, Date, Arcade, Cafe }

        public void Configure(
            TextMeshProUGUI stamina, TextMeshProUGUI timeSlot, TextMeshProUGUI balance,
            Button job, Button date, Button room, TextMeshProUGUI feedback)
        {
            staminaText = stamina;
            timeSlotText = timeSlot;
            balanceText = balance;
            firstJobButton = job;
            firstDateButton = date;
            roomButton = room;
            feedbackText = feedback;
        }

        public void ConfigureMapUI(TextMeshProUGUI stamina, TextMeshProUGUI timeSlot, TextMeshProUGUI balance,
            TextMeshProUGUI affection, Button room, Button job, Button date, Button arcade, Button cafe,
            Button action, Button[] filters, GameObject[] markers, TextMeshProUGUI title,
            TextMeshProUGUI meta, TextMeshProUGUI description, TextMeshProUGUI actionText, TextMeshProUGUI feedback,
            Image staminaBar, Image affectionBar, Image[] pips, RectTransform[] route, RectTransform[] markerTransforms)
        {
            staminaText = stamina;
            timeSlotText = timeSlot;
            balanceText = balance;
            affectionText = affection;
            roomButton = room;
            firstJobButton = job;
            firstDateButton = date;
            arcadeButton = arcade;
            cafeButton = cafe;
            primaryActionButton = action;
            filterButtons = filters;
            locationMarkers = markers;
            detailTitle = title;
            detailMeta = meta;
            detailDescription = description;
            actionLabel = actionText;
            feedbackText = feedback;
            staminaFill = staminaBar;
            affectionFill = affectionBar;
            timePips = pips;
            routeDots = route;
            markerRects = markerTransforms;
        }

        public void ConfigureRegionNavigation(Image mapBackground,TMP_Text mapTitle,Button previousButton,
            Button nextButton,GameObject pinLayer,GameObject detailPanel)
        {
            regionMapBackground=mapBackground;
            regionTitle=mapTitle;
            previousRegionButton=previousButton;
            nextRegionButton=nextButton;
            locationPinLayer=pinLayer;
            locationDetailPanel=detailPanel;
            currentRegionIndex=0;
        }

        public void ConfigureJobResultModal(GameObject modal, TextMeshProUGUI title, TextMeshProUGUI pay,
            GameObject giftRow, TextMeshProUGUI gift, TextMeshProUGUI body, Button confirm)
        {
            jobResultModal = modal;
            jobResultTitle = title;
            jobResultPay = pay;
            jobResultGiftRow = giftRow;
            jobResultGift = gift;
            jobResultBody = body;
            confirm?.onClick.AddListener(() => jobResultModal.SetActive(false));
        }

        private void Start()
        {
            BindButtons();
            SubscribeEvents();
            UpdateAllUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void BindButtons()
        {
            if (detailTitle == null)
            {
                if (firstJobButton != null) firstJobButton.onClick.AddListener(() => WorldMapManager.Instance?.TryStartPartTimeJob(0));
                if (firstDateButton != null) firstDateButton.onClick.AddListener(() => WorldMapManager.Instance?.TryStartDateCourse(0));
                if (roomButton != null) roomButton.onClick.AddListener(() => WorldMapManager.Instance?.ReturnToRoom());
                return;
            }

            roomButton?.onClick.AddListener(() => SelectLocation(MapLocation.Room));
            firstJobButton?.onClick.AddListener(() => SelectLocation(MapLocation.Job));
            firstDateButton?.onClick.AddListener(() => SelectLocation(MapLocation.Date));
            arcadeButton?.onClick.AddListener(() => SelectLocation(MapLocation.Arcade));
            cafeButton?.onClick.AddListener(() => SelectLocation(MapLocation.Cafe));
            primaryActionButton?.onClick.AddListener(ExecuteSelectedLocation);
            previousRegionButton?.onClick.AddListener(()=>ChangeRegion(-1));
            nextRegionButton?.onClick.AddListener(()=>ChangeRegion(1));
            if (filterButtons != null)
            {
                for (int i = 0; i < filterButtons.Length; i++)
                {
                    int filter = i;
                    filterButtons[i]?.onClick.AddListener(() => ApplyFilter(filter));
                }
            }
            SelectLocation(MapLocation.Room);
            ApplyRegion();
        }

        private void ChangeRegion(int direction)
        {
            int next=Mathf.Clamp(currentRegionIndex+direction,0,RegionNames.Length-1);
            if(next==currentRegionIndex)return;
            currentRegionIndex=next;
            ApplyRegion();
        }

        private void ApplyRegion()
        {
            if(currentRegionIndex<0||currentRegionIndex>=RegionSpritePaths.Length)return;
            Sprite sprite=Resources.Load<Sprite>(RegionSpritePaths[currentRegionIndex]);
            if(sprite!=null&&regionMapBackground!=null)regionMapBackground.sprite=sprite;
            else if(sprite==null)Debug.LogWarning($"[WorldMapUI] 지역 배경을 찾지 못했습니다: {RegionSpritePaths[currentRegionIndex]}");

            if(regionTitle!=null)regionTitle.text=$"{RegionNames[currentRegionIndex]} CITY MAP";
            if(previousRegionButton!=null)previousRegionButton.interactable=currentRegionIndex>0;
            if(nextRegionButton!=null)nextRegionButton.interactable=currentRegionIndex<RegionNames.Length-1;

            // 현재 장소 콘텐츠는 서울 데이터만 있으므로 다른 지역에서는 서울 핀과 상세 행동을 숨깁니다.
            bool isSeoul=currentRegionIndex==0;
            if(locationPinLayer!=null)locationPinLayer.SetActive(isSeoul);
            if(locationDetailPanel!=null)locationDetailPanel.SetActive(isSeoul);
            if(isSeoul)SelectLocation(selectedLocation);
        }

        private void SubscribeEvents()
        {
            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged += UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged += UpdateTimeSlotUI;
                DatingTimeManager.Instance.OnAffectionChanged += UpdateAffectionUI;
            }

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnActionFailed += HandleActionFailed;
                WorldMapManager.Instance.OnDateStarted += HandleDateStarted;
                WorldMapManager.Instance.OnJobFinished += HandleJobFinished;
            }
        }

        private void UnsubscribeEvents()
        {
            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged -= UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged -= UpdateTimeSlotUI;
                DatingTimeManager.Instance.OnAffectionChanged -= UpdateAffectionUI;
            }

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnActionFailed -= HandleActionFailed;
                WorldMapManager.Instance.OnDateStarted -= HandleDateStarted;
                WorldMapManager.Instance.OnJobFinished -= HandleJobFinished;
            }
        }

        private void UpdateAllUI()
        {
            if (DatingTimeManager.Instance != null)
            {
                UpdateStaminaUI(DatingTimeManager.Instance.CurrentStamina, DatingTimeManager.Instance.MaxStamina);
                UpdateTimeSlotUI(DatingTimeManager.Instance.CurrentTimeSlot);
                UpdateAffectionUI(DatingTimeManager.Instance.CurrentAffection);
            }
            UpdateBalanceUI();
            
            if (feedbackText != null) feedbackText.text = "행동을 선택해 주세요.";
        }

        private void UpdateStaminaUI(int current, int max)
        {
            if (staminaText != null)
                staminaText.text = $"체력  {current}/{max}";
            if (staminaFill != null) staminaFill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        }

        private void UpdateTimeSlotUI(int currentSlots)
        {
            // 슬롯 1개가 3시간을 소모하게 된 뒤로는 개수만으로 "지금 몇 시인지"를 알 수 없습니다.
            if (timeSlotText != null)
                timeSlotText.text = $"남은 시간  {currentSlots}/5  ({DatingTimeManager.ClockTextForSlots(currentSlots)})";
            if (timePips != null)
            {
                for (int i = 0; i < timePips.Length; i++)
                    if (timePips[i] != null) timePips[i].color = i < currentSlots ? new Color32(250, 184, 45, 255) : new Color32(53, 62, 70, 255);
            }
        }

        private void UpdateBalanceUI()
        {
            if (balanceText != null && TryGetBalance(out float balance))
                balanceText.text = $"보유 자산  ₩{balance:N0}";
        }

        /// <summary>상주 GameManager가 있으면 그 잔고를, 없으면 세이브 스냅샷의 잔고를 읽습니다.</summary>
        private static bool TryGetBalance(out float balance)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                balance = gm.CurrentBalance;
                return true;
            }

            var data = SaveLoadManager.Instance?.CurrentData;
            balance = data != null ? data.Balance : 0f;
            return data != null;
        }

        private void UpdateAffectionUI(int value)
        {
            if (affectionText != null) affectionText.text = $"호감도  {Mathf.Clamp(value, 0, 100)}%";
            if (affectionFill != null) affectionFill.fillAmount = Mathf.Clamp01(value / 100f);
        }

        private void SelectLocation(MapLocation location)
        {
            selectedLocation = location;
            UpdateRoute(location);
            if (detailTitle == null) return;
            switch (location)
            {
                case MapLocation.Job:
                    SetDetails("편의점 알바", JobMetaText(), "야간 편의점 업무를 마치고 자산을 획득합니다.", "알바 시작");
                    break;
                case MapLocation.Date:
                    SetDetails("한강공원 데이트", "체력 -10  ·  시간 -1  ·  비용 ₩500", "한강 야경을 보며 요미와 데이트합니다.", "데이트 시작");
                    break;
                case MapLocation.Arcade:
                    SetDetails("홍대 오락실", "휴식 장소  ·  준비 중", "미니게임과 추가 이벤트가 연결될 예정입니다.", "준비 중");
                    break;
                case MapLocation.Cafe:
                    SetDetails("성수 카페", "데이트 장소  ·  준비 중", "새로운 데이트 코스가 연결될 예정입니다.", "준비 중");
                    break;
                default:
                    SetDetails("요미의 방", "현재 위치  ·  비용 없음", "휴식을 취하고 다음 일정을 계획할 수 있습니다.", "돌아가기");
                    break;
            }
        }

        /// <summary>
        /// 알바 카드 문구. 지급과 같은 데이터(availableJobs)에서 만듭니다 —
        /// 즉시 정산은 "적힌 그대로 받는 것"이라 숫자를 따로 박아 두면 어긋납니다.
        /// </summary>
        private static string JobMetaText()
        {
            var manager = WorldMapManager.Instance;
            if (manager == null || manager.availableJobs.Count == 0) return "준비 중";

            PartTimeJobData job = manager.availableJobs[0];
            return $"체력 -{job.staminaCost}  ·  시간 -{WorldMapManager.PartTimeJobSlotCost}  ·  일급 ₩{job.rewardAmount:N0}";
        }

        private void UpdateRoute(MapLocation location)
        {
            if (routeDots == null || markerRects == null || markerRects.Length < 5) return;
            int targetIndex = (int)location;
            bool showRoute = location != MapLocation.Room && targetIndex < markerRects.Length;
            Vector2 start = markerRects[0].anchorMin;
            Vector2 end = showRoute ? markerRects[targetIndex].anchorMin : start;

            for (int i = 0; i < routeDots.Length; i++)
            {
                RectTransform dot = routeDots[i];
                if (dot == null) continue;
                dot.gameObject.SetActive(showRoute);
                if (!showRoute) continue;
                float t = routeDots.Length > 1 ? i / (routeDots.Length - 1f) : 0f;
                Vector2 position = Vector2.Lerp(start, end, t);
                dot.anchorMin = dot.anchorMax = position;
            }
        }

        private void SetDetails(string title, string meta, string description, string action)
        {
            detailTitle.text = title;
            detailMeta.text = meta;
            detailDescription.text = description;
            if (actionLabel != null) actionLabel.text = action;
            if (primaryActionButton != null)
                primaryActionButton.interactable = selectedLocation != MapLocation.Arcade && selectedLocation != MapLocation.Cafe;
        }

        private void ExecuteSelectedLocation()
        {
            switch (selectedLocation)
            {
                case MapLocation.Job: WorldMapManager.Instance?.TryStartPartTimeJob(0); break;
                case MapLocation.Date: WorldMapManager.Instance?.TryStartDateCourse(0); break;
                case MapLocation.Room: WorldMapManager.Instance?.ReturnToRoom(); break;
            }
        }

        private void ApplyFilter(int filter)
        {
            if (locationMarkers == null || locationMarkers.Length < 5) return;
            for (int i = 0; i < locationMarkers.Length; i++)
            {
                bool visible = filter == 0 ||
                    (filter == 1 && i == 1) ||
                    (filter == 2 && (i == 2 || i == 4)) ||
                    (filter == 3 && (i == 0 || i == 3));
                locationMarkers[i]?.SetActive(visible);
            }

            int selectedIndex = (int)selectedLocation;
            if (selectedIndex >= locationMarkers.Length || !locationMarkers[selectedIndex].activeSelf)
                SelectLocation(filter == 1 ? MapLocation.Job : filter == 2 ? MapLocation.Date : MapLocation.Room);
        }

        private void HandleActionFailed()
        {
            if (feedbackText != null) feedbackText.text = "행동 불가: 자원(체력/시간/자금)이 부족합니다.";
        }

        /// <summary>알바 즉시 정산 결과를 모달로 보여줍니다. 매니저는 이벤트만 내고 이 모달을 모릅니다.</summary>
        private void HandleJobFinished(PartTimeJobResult result)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{result.JobName} 완료  ·  일급 ₩{result.Pay:N0}";
            AudioManager.Play(AudioCue.Profit, true);
            if (jobResultModal == null) return;

            jobResultTitle.text = $"{result.JobName}  ·  근무 완료";
            jobResultPay.text = $"일급  + ₩{result.Pay:N0}";

            bool hasGift = !string.IsNullOrEmpty(result.GiftItemId);
            jobResultGiftRow.SetActive(hasGift);
            if (hasGift) jobResultGift.text = $"선물  {result.GiftName} × 1";

            var body = new System.Text.StringBuilder();
            body.AppendLine($"근무 시간   {DatingTimeManager.ClockTextForSlots(result.SlotsBefore)} → " +
                            $"{DatingTimeManager.ClockTextForSlots(result.SlotsAfter)}   (시간 -{result.SlotsBefore - result.SlotsAfter})");
            body.AppendLine($"체력        {result.StaminaBefore} → {result.StaminaAfter}   (-{result.StaminaBefore - result.StaminaAfter})");
            if (TryGetBalance(out float balance)) body.AppendLine($"보유 자산   ₩{balance:N0}");
            if (result.TotalShifts > 0) body.AppendLine($"누적 근무   {result.TotalShifts}회차");
            if (result.SaveFailed) body.AppendLine("<color=#EF4444>저장에 실패했습니다. 다음 저장 때 함께 기록됩니다.</color>");
            jobResultBody.text = body.ToString();

            jobResultModal.SetActive(true);
            jobResultModal.transform.SetAsLastSibling();
        }

        private void HandleDateStarted(string courseName)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{courseName} 코스로 진입합니다...";
        }
    }
}
