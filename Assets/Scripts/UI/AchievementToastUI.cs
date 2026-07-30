using System.Collections;
using System.Collections.Generic;
using FXOverdose.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>새 업적 달성 시 우측 하단에 순차적으로 표시되는 런타임 토스트 UI입니다.</summary>
    public sealed class AchievementToastUI : MonoBehaviour
    {
        private const float SlideDuration = 0.35f;
        private const float VisibleDuration = 3f;

        private static AchievementToastUI instance;

        private readonly Queue<AchievementManager.AchievementDefinition> queue = new();
        private RectTransform toastRect;
        private Image icon;
        private TMP_Text title;
        private TMP_Text description;
        private Vector2 hiddenPosition;
        private Vector2 visiblePosition;
        private Coroutine displayRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (instance == null)
                new GameObject("AchievementToastUI").AddComponent<AchievementToastUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.OnAchievementUnlocked -= Enqueue;
        }

        private void OnDestroy()
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.OnAchievementUnlocked -= Enqueue;
            if (instance == this) instance = null;
        }

        private IEnumerator SubscribeWhenReady()
        {
            while (AchievementManager.Instance == null)
                yield return null;

            AchievementManager.Instance.OnAchievementUnlocked -= Enqueue;
            AchievementManager.Instance.OnAchievementUnlocked += Enqueue;
        }

        private void Enqueue(AchievementManager.AchievementDefinition achievement)
        {
            if (achievement == null) return;
            queue.Enqueue(achievement);
            if (displayRoutine == null)
                displayRoutine = StartCoroutine(DisplayQueue());
        }

        private IEnumerator DisplayQueue()
        {
            while (queue.Count > 0)
            {
                AchievementManager.AchievementDefinition achievement = queue.Dequeue();
                icon.sprite = Resources.Load<Sprite>($"UI/Achievements/{achievement.Id}");
                icon.enabled = icon.sprite != null;
                title.text = achievement.Title;
                description.text = achievement.Description;

                toastRect.gameObject.SetActive(true);
                yield return Slide(hiddenPosition, visiblePosition);
                yield return new WaitForSecondsRealtime(VisibleDuration);
                yield return Slide(visiblePosition, hiddenPosition);
                toastRect.gameObject.SetActive(false);
            }

            displayRoutine = null;
        }

        private IEnumerator Slide(Vector2 from, Vector2 to)
        {
            float elapsed = 0f;
            toastRect.anchoredPosition = from;
            while (elapsed < SlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                toastRect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }
            toastRect.anchoredPosition = to;
        }

        private void BuildUI()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 950;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject toast = new("AchievementToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            toast.transform.SetParent(transform, false);
            toastRect = toast.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(1f, 0f);
            toastRect.anchorMax = new Vector2(1f, 0f);
            toastRect.pivot = new Vector2(1f, 0f);
            toastRect.sizeDelta = new Vector2(470f, 128f);
            visiblePosition = new Vector2(-32f, 30f);
            hiddenPosition = new Vector2(-32f, -150f);
            toastRect.anchoredPosition = hiddenPosition;

            Image background = toast.GetComponent<Image>();
            background.color = new Color32(11, 20, 36, 248);
            Outline outline = toast.GetComponent<Outline>();
            outline.effectColor = new Color32(6, 182, 212, 255);
            outline.effectDistance = new Vector2(3f, -3f);

            Image accent = CreateImage(toast.transform, "Accent", new Color32(6, 182, 212, 255));
            SetRect(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0.018f, 1f));

            icon = CreateImage(toast.transform, "Icon", Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(92f, 92f);
            iconRect.anchoredPosition = new Vector2(65f, 0f);

            TMP_Text eyebrow = CreateText(toast.transform, "Eyebrow", "ACHIEVEMENT UNLOCKED", 15f,
                new Color32(234, 179, 8, 255), FontStyles.Bold);
            SetRect(eyebrow.rectTransform, new Vector2(0.27f, 0.67f), new Vector2(0.95f, 0.91f));

            title = CreateText(toast.transform, "Title", string.Empty, 23f, Color.white, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.27f, 0.38f), new Vector2(0.95f, 0.69f));

            description = CreateText(toast.transform, "Description", string.Empty, 15f,
                new Color32(166, 185, 207, 255), FontStyles.Normal);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
            description.maxVisibleLines = 2;
            SetRect(description.rectTransform, new Vector2(0.27f, 0.08f), new Vector2(0.95f, 0.39f));

            toast.SetActive(false);
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string objectName, string value, float size,
            Color color, FontStyles style)
        {
            GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
