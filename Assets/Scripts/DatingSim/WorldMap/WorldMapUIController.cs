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
            if (filterButtons != null)
            {
                for (int i = 0; i < filterButtons.Length; i++)
                {
                    int filter = i;
                    filterButtons[i]?.onClick.AddListener(() => ApplyFilter(filter));
                }
            }
            SelectLocation(MapLocation.Room);
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
                WorldMapManager.Instance.OnJobFinished += HandleJobFinished;
                WorldMapManager.Instance.OnDateStarted += HandleDateStarted;
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
                WorldMapManager.Instance.OnJobFinished -= HandleJobFinished;
                WorldMapManager.Instance.OnDateStarted -= HandleDateStarted;
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
            if (timeSlotText != null)
                timeSlotText.text = $"남은 시간  {currentSlots}/5";
            if (timePips != null)
            {
                for (int i = 0; i < timePips.Length; i++)
                    if (timePips[i] != null) timePips[i].color = i < currentSlots ? new Color32(250, 184, 45, 255) : new Color32(53, 62, 70, 255);
            }
        }

        private void UpdateBalanceUI()
        {
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null && balanceText != null)
            {
                balanceText.text = $"보유 자산  ₩{gm.CurrentBalance:N0}";
            }
            else if (balanceText != null && SaveLoadManager.Instance?.CurrentData != null)
            {
                balanceText.text = $"보유 자산  ₩{SaveLoadManager.Instance.CurrentData.Balance:N0}";
            }
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
                    SetDetails("편의점 알바", "체력 -20  ·  시간 -2  ·  보상 ₩1,200", "야간 편의점 업무를 마치고 자산을 획득합니다.", "알바 시작");
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

        private void HandleJobFinished(string jobName, float reward)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{jobName} 완료! 보상금: ${reward:N0}이 자산에 합산되었습니다.";
        }

        private void HandleDateStarted(string courseName)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{courseName} 코스로 진입합니다...";
        }
    }
}
