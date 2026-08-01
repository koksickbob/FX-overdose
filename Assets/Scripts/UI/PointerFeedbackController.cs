using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>
    /// PC 마우스 이동과 모바일 드래그/탭에 픽셀 잔상 및 클릭 피드백을 표시합니다.
    /// 입력을 가로채지 않는 최상단 Overlay Canvas에서 풀링 방식으로 동작합니다.
    /// </summary>
    public sealed class PointerFeedbackController : MonoBehaviour
    {
        private const int PoolSize = 96;
        private const int OverlaySortingOrder = 32000;
        private const float DesktopSpawnDistance = 7f;
        private const float MobileSpawnDistance = 11f;
        private const float DesktopSpawnInterval = 0.014f;
        private const float MobileSpawnInterval = 0.022f;

        private static PointerFeedbackController instance;

        private readonly List<FxParticle> particles = new(PoolSize);
        private RectTransform effectRoot;
        private Sprite diamondSprite;
        private Sprite ringSprite;
        private Vector2 lastPointerPosition;
        private bool hasPointerPosition;
        private float lastTrailSpawnTime;
        private int colorSequence;

        private static readonly Color Cyan = new(0.024f, 0.714f, 0.831f, 1f);
        private static readonly Color LightCyan = new(0.812f, 0.98f, 0.996f, 1f);
        private static readonly Color Magenta = new(0.925f, 0.286f, 0.6f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            GameObject root = new("PointerFeedbackSystem");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<PointerFeedbackController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (diamondSprite != null)
            {
                Destroy(diamondSprite.texture);
                Destroy(diamondSprite);
            }
            if (ringSprite != null)
            {
                Destroy(ringSprite.texture);
                Destroy(ringSprite);
            }
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            UpdateParticles(deltaTime);

            bool useMobileInput = Application.isMobilePlatform && Touchscreen.current != null;
            Vector2 pointerPosition;
            bool pointerTracked;
            bool pointerMovingAllowed;
            bool clicked;

            if (useMobileInput)
            {
                Touchscreen touchscreen = Touchscreen.current;
                pointerPosition = touchscreen.primaryTouch.position.ReadValue();
                bool pressed = touchscreen.primaryTouch.press.isPressed;
                clicked = touchscreen.primaryTouch.press.wasPressedThisFrame;
                pointerTracked = pressed || clicked;
                pointerMovingAllowed = pressed;
            }
            else
            {
                Pointer pointer = Pointer.current;
                if (pointer == null)
                {
                    hasPointerPosition = false;
                    return;
                }

                pointerPosition = pointer.position.ReadValue();
                clicked = pointer.press.wasPressedThisFrame;
                pointerTracked = true;
                pointerMovingAllowed = true;
            }

            if (!pointerTracked)
            {
                hasPointerPosition = false;
                return;
            }

            if (clicked)
            {
                SpawnClickBurst(pointerPosition);
            }

            if (hasPointerPosition && pointerMovingAllowed)
            {
                float distance = Vector2.Distance(pointerPosition, lastPointerPosition);
                float requiredDistance = useMobileInput ? MobileSpawnDistance : DesktopSpawnDistance;
                float interval = useMobileInput ? MobileSpawnInterval : DesktopSpawnInterval;
                if (distance >= requiredDistance && Time.unscaledTime - lastTrailSpawnTime >= interval)
                {
                    SpawnTrail(pointerPosition, lastPointerPosition, useMobileInput);
                    lastTrailSpawnTime = Time.unscaledTime;
                }
            }

            lastPointerPosition = pointerPosition;
            hasPointerPosition = true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) hasPointerPosition = false;
        }

        private void BuildOverlay()
        {
            GameObject canvasObject = new("PointerFeedbackCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = OverlaySortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject rootObject = new("Effects", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            effectRoot = rootObject.GetComponent<RectTransform>();
            effectRoot.anchorMin = Vector2.zero;
            effectRoot.anchorMax = Vector2.one;
            effectRoot.offsetMin = Vector2.zero;
            effectRoot.offsetMax = Vector2.zero;

            diamondSprite = CreatePixelSprite("PointerTrailDiamond", false);
            ringSprite = CreatePixelSprite("PointerClickRing", true);

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject particleObject = new($"PointerFx_{i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                particleObject.transform.SetParent(effectRoot, false);
                RectTransform rect = particleObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                Image image = particleObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.maskable = false;
                particleObject.SetActive(false);
                particles.Add(new FxParticle(particleObject, rect, image));
            }
        }

        private static Sprite CreatePixelSprite(string spriteName, bool ring)
        {
            const int size = 24;
            const int center = size / 2;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = spriteName + "Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    bool visible = ring ? distance >= 8 && distance <= 10 : distance <= 9;
                    pixels[y * size + x] = visible
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        private void SpawnTrail(Vector2 position, Vector2 previousPosition, bool mobile)
        {
            Vector2 direction = position - previousPosition;
            float speed = direction.magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
            float size = Mathf.Clamp((mobile ? 13f : 9f) + speed * 0.0025f, mobile ? 13f : 9f, mobile ? 25f : 18f);
            Color color = colorSequence++ % 3 == 2 ? Magenta : (colorSequence % 2 == 0 ? LightCyan : Cyan);
            color.a = mobile ? 0.72f : 0.58f;

            FxParticle particle = AcquireParticle();
            particle.Play(diamondSprite, position, Vector2.zero, size, size * 0.25f,
                mobile ? 0.32f : 0.24f, color, Random.Range(-20f, 20f));
        }

        private void SpawnClickBurst(Vector2 position)
        {
            FxParticle ring = AcquireParticle();
            ring.Play(ringSprite, position, Vector2.zero, 22f, 78f, 0.38f, LightCyan, 0f);

            const int shardCount = 10;
            for (int i = 0; i < shardCount; i++)
            {
                float angle = i * Mathf.PI * 2f / shardCount;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Color color = i % 2 == 0 ? Cyan : Magenta;
                FxParticle shard = AcquireParticle();
                shard.Play(diamondSprite, position + direction * 10f, direction * Random.Range(95f, 150f),
                    i % 3 == 0 ? 13f : 9f, 2f, Random.Range(0.28f, 0.42f), color, i * 18f);
            }
        }

        private FxParticle AcquireParticle()
        {
            FxParticle oldest = particles[0];
            for (int i = 0; i < particles.Count; i++)
            {
                if (!particles[i].Active) return particles[i];
                if (particles[i].NormalizedAge > oldest.NormalizedAge) oldest = particles[i];
            }
            return oldest;
        }

        private void UpdateParticles(float deltaTime)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].Active) particles[i].Tick(deltaTime);
            }
        }

        private sealed class FxParticle
        {
            private readonly GameObject gameObject;
            private readonly RectTransform rect;
            private readonly Image image;
            private Vector2 position;
            private Vector2 velocity;
            private float startSize;
            private float endSize;
            private float lifetime;
            private float age;
            private float rotationSpeed;
            private Color color;

            public bool Active { get; private set; }
            public float NormalizedAge => lifetime > 0f ? age / lifetime : 1f;

            public FxParticle(GameObject gameObject, RectTransform rect, Image image)
            {
                this.gameObject = gameObject;
                this.rect = rect;
                this.image = image;
            }

            public void Play(Sprite sprite, Vector2 screenPosition, Vector2 initialVelocity, float fromSize,
                float toSize, float duration, Color tint, float spin)
            {
                position = screenPosition;
                velocity = initialVelocity;
                startSize = fromSize;
                endSize = toSize;
                lifetime = Mathf.Max(0.01f, duration);
                age = 0f;
                rotationSpeed = spin;
                color = tint;
                image.sprite = sprite;
                image.color = tint;
                rect.anchoredPosition = position;
                rect.sizeDelta = Vector2.one * fromSize;
                rect.localRotation = Quaternion.identity;
                gameObject.SetActive(true);
                Active = true;
            }

            public void Tick(float deltaTime)
            {
                age += deltaTime;
                if (age >= lifetime)
                {
                    Active = false;
                    gameObject.SetActive(false);
                    return;
                }

                float t = Mathf.Clamp01(age / lifetime);
                position += velocity * deltaTime;
                velocity *= Mathf.Pow(0.025f, deltaTime);
                rect.anchoredPosition = position;
                float size = Mathf.Lerp(startSize, endSize, t);
                rect.sizeDelta = Vector2.one * size;
                rect.Rotate(0f, 0f, rotationSpeed * deltaTime);
                Color current = color;
                current.a = color.a * (1f - t) * (1f - t);
                image.color = current;
            }
        }
    }
}
