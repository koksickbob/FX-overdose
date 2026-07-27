using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>
    /// 게임 시각에 맞춰 동일한 방의 아침/해질녘/밤 배경을 교체합니다.
    /// 기존 BackGround Image의 RectTransform과 AspectRatioFitter는 그대로 유지합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayTimeBackgroundController : MonoBehaviour
    {
        private enum DayPeriod
        {
            Morning,
            Sunset,
            Night
        }

        [Header("시간대 시작 시각")]
        [SerializeField, Range(0, 23)] private int morningStartHour = 6;
        [SerializeField, Range(0, 23)] private int sunsetStartHour = 17;
        [SerializeField, Range(0, 23)] private int nightStartHour = 20;

        [Header("전환")]
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.8f;

        private GameManager gameManager;
        private Image backgroundImage;
        private Image transitionImage;

        private Sprite morningSprite;
        private Sprite sunsetSprite;
        private Sprite nightSprite;
        private bool ownsMorningSprite;
        private bool ownsSunsetSprite;

        private DayPeriod currentPeriod = (DayPeriod)(-1);
        private Coroutine transitionRoutine;

        private void Awake()
        {
            gameManager = GetComponent<GameManager>();
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            ResolveBackgroundImage();
            LoadTimeOfDaySprites();
            // 런타임 자동 부착 시 첫 렌더 프레임부터 현재 시간대가 보이게 합니다.
            RefreshForCurrentTime(true);
        }

        private IEnumerator Start()
        {
            // 세이브 데이터의 시각이 GameManager.Start에서 복구된 뒤 최초 배경을 결정합니다.
            yield return null;
            RefreshForCurrentTime(true);
        }

        private void Update()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            if (backgroundImage == null)
            {
                ResolveBackgroundImage();
                if (backgroundImage == null) return;
                LoadTimeOfDaySprites();
            }

            RefreshForCurrentTime(false);
        }

        /// <summary>에디터 테스트나 외부 시간 변경 직후 현재 시각의 배경을 즉시 반영합니다.</summary>
        public void RefreshImmediately()
        {
            RefreshForCurrentTime(true);
        }

        private void RefreshForCurrentTime(bool immediate)
        {
            if (gameManager == null || backgroundImage == null) return;

            DayPeriod desiredPeriod = GetPeriod(gameManager.CurrentHour);
            if (!immediate && desiredPeriod == currentPeriod) return;

            if (FXOverdose.Core.AudioManager.Instance != null)
            {
                string bgmPath = desiredPeriod switch
                {
                    DayPeriod.Morning => "Audio/BGM/bgm_morning",
                    DayPeriod.Sunset => "Audio/BGM/bgm_sunset",
                    _ => "Audio/BGM/bgm_night"
                };
                FXOverdose.Core.AudioManager.Instance.SetTimeOfDayBgm(bgmPath, !immediate);
            }

            Sprite desiredSprite = GetSprite(desiredPeriod);
            if (desiredSprite == null) return;

            currentPeriod = desiredPeriod;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            if (immediate || crossFadeDuration <= 0f || transitionImage == null)
            {
                backgroundImage.sprite = desiredSprite;
                SetTransitionAlpha(0f);
                if (transitionImage != null)
                    transitionImage.enabled = false;
                return;
            }

            transitionRoutine = StartCoroutine(CrossFadeTo(desiredSprite));
        }

        private IEnumerator CrossFadeTo(Sprite targetSprite)
        {
            transitionImage.sprite = targetSprite;
            transitionImage.enabled = true;
            SetTransitionAlpha(0f);

            float elapsed = 0f;
            while (elapsed < crossFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetTransitionAlpha(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, crossFadeDuration)));
                yield return null;
            }

            backgroundImage.sprite = targetSprite;
            SetTransitionAlpha(0f);
            transitionImage.enabled = false;
            transitionRoutine = null;
        }

        private DayPeriod GetPeriod(int hour)
        {
            hour = ((hour % 24) + 24) % 24;

            if (hour >= nightStartHour || hour < morningStartHour)
                return DayPeriod.Night;
            if (hour >= sunsetStartHour)
                return DayPeriod.Sunset;
            return DayPeriod.Morning;
        }

        private Sprite GetSprite(DayPeriod period)
        {
            return period switch
            {
                DayPeriod.Morning => morningSprite != null ? morningSprite : nightSprite,
                DayPeriod.Sunset => sunsetSprite != null ? sunsetSprite : nightSprite,
                _ => nightSprite
            };
        }

        private void ResolveBackgroundImage()
        {
            GameObject backgroundObject = GameObject.Find("BackGround");
            if (backgroundObject == null) return;

            backgroundImage = backgroundObject.GetComponent<Image>();
            if (backgroundImage == null) return;

            nightSprite = backgroundImage.sprite;
            EnsureTransitionImage();
        }

        private void EnsureTransitionImage()
        {
            if (backgroundImage == null || transitionImage != null) return;

            Transform existing = backgroundImage.transform.Find("DayTimeBackgroundFade");
            if (existing != null)
                transitionImage = existing.GetComponent<Image>();

            if (transitionImage == null)
            {
                GameObject overlay = new(
                    "DayTimeBackgroundFade",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlay.transform.SetParent(backgroundImage.transform, false);
                transitionImage = overlay.GetComponent<Image>();
            }

            RectTransform rect = transitionImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            transitionImage.raycastTarget = false;
            transitionImage.maskable = backgroundImage.maskable;
            transitionImage.preserveAspect = false;
            transitionImage.enabled = false;
            SetTransitionAlpha(0f);
        }

        private void LoadTimeOfDaySprites()
        {
            if (morningSprite == null)
                morningSprite = LoadBackgroundSprite("Backgrounds/RoomMorning", out ownsMorningSprite);
            if (sunsetSprite == null)
                sunsetSprite = LoadBackgroundSprite("Backgrounds/RoomSunset", out ownsSunsetSprite);

            if (morningSprite == null || sunsetSprite == null)
            {
                Debug.LogWarning("[DayTimeBackgroundController] 시간대 배경 일부를 불러오지 못해 기존 밤 배경을 대신 사용합니다.");
            }
        }

        private static Sprite LoadBackgroundSprite(string resourcePath, out bool ownsSprite)
        {
            ownsSprite = false;

            Sprite importedSprite = Resources.Load<Sprite>(resourcePath);
            if (importedSprite != null)
                return importedSprite;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            ownsSprite = true;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
        }

        private void SetTransitionAlpha(float alpha)
        {
            if (transitionImage == null) return;

            Color sourceColor = backgroundImage != null ? backgroundImage.color : Color.white;
            transitionImage.color = new Color(sourceColor.r, sourceColor.g, sourceColor.b, Mathf.Clamp01(alpha));
        }

        private void OnDestroy()
        {
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);

            if (ownsMorningSprite && morningSprite != null)
                Destroy(morningSprite);
            if (ownsSunsetSprite && sunsetSprite != null)
                Destroy(sunsetSprite);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            sunsetStartHour = Mathf.Max(morningStartHour, sunsetStartHour);
            nightStartHour = Mathf.Max(sunsetStartHour, nightStartHour);
            crossFadeDuration = Mathf.Max(0f, crossFadeDuration);
        }
#endif
    }
}
