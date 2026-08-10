import re

with open('Assets/Scripts/AI/AIVisualController.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# We need to create YomiSpriteController and AIVisualController

# YomiSpriteController: keeps sprite related stuff.
sprite_ctrl = '''using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    public class YomiSpriteController : MonoBehaviour
    {
        private const float CostumeOpticalScale = 0.95f;
        private const float CostumeOpticalFootCompensation = -0.020f;

        [Header("시스템 연결")]
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private Inventory inventory;

        [Header("비주얼 및 애니메이터")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Image characterImage;
        [SerializeField] private GameObject dangerAuraEffect;

        [Header("현재 상태 (읽기 전용)")]
        [SerializeField] private TraderEmotion currentEmotion = TraderEmotion.Focused;
        private readonly Dictionary<TraderEmotion, Sprite> emotionSprites = new Dictionary<TraderEmotion, Sprite>();
        private readonly Dictionary<string, Sprite> itemUseSprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<SkillType, Sprite> skillUpgradeSprites = new Dictionary<SkillType, Sprite>();
        private readonly Dictionary<TradingController.PositionType, Sprite> positionSprites = new Dictionary<TradingController.PositionType, Sprite>();
        private readonly Dictionary<Sprite, float> costumeSpriteScales = new Dictionary<Sprite, float>();
        private readonly Dictionary<Sprite, Vector2> costumeSpriteOffsets = new Dictionary<Sprite, Vector2>();
        
        private Sprite overdoseStateSprite;
        private float emotionOverrideUntil;
        private bool isItemUseVisualActive;
        private float itemUseVisualUntil;
        private bool isSkillUpgradeVisualActive;
        private bool isPositionVisualActive;
        private float positionVisualUntil;
        private bool isOverdoseStateVisualActive;
        
        private string currentItemUseId;
        private SkillType currentSkillUpgradeType;
        private TradingController.PositionType currentPositionVisualType;
        
        private bool characterBaseScaleCaptured;
        private Vector3 characterBaseScale = Vector3.one;
        private Vector2 characterBaseAnchoredPosition;

        private void Start()
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (inventory == null) inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);

            ResolveCharacterImage();
            LoadEmotionSprites();
            LoadItemUseSprites();
            LoadSkillUpgradeSprites();
            LoadPositionSprites();
            overdoseStateSprite = LoadCostumeSprite("States", "Overdose");
            ApplyEmotion(currentEmotion, true);
            ApplyCostumeVisualScale();

            if (CostumeManager.Instance != null)
            {
                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;
                CostumeManager.Instance.OnCostumesChanged += HandleCostumeChanged;
            }

            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
                inventory.ItemConsumed += HandleItemConsumed;
            }

            if (dangerAuraEffect != null) dangerAuraEffect.SetActive(false);
        }

        private void OnDestroy()
        {
            if (CostumeManager.Instance != null)
                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;
            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
            }
        }

        private void Update()
        {
            UpdateExpressionState();
        }

        private void UpdateExpressionState()
        {
            if (isSkillUpgradeVisualActive) return;

            if (isPositionVisualActive)
            {
                if (Time.unscaledTime < positionVisualUntil) return;
                isPositionVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            if (isItemUseVisualActive)
            {
                if (Time.unscaledTime < itemUseVisualUntil) return;
                isItemUseVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            if (traderStatus == null) return;

            if (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                ApplyOverdoseStateVisual();
                return;
            }

            if (isOverdoseStateVisualActive)
            {
                isOverdoseStateVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            if (Time.unscaledTime < emotionOverrideUntil) return;

            float roe = CalculateCurrentRoe();
            TraderEmotion evaluatedEmotion = TraderEmotionEvaluator.Evaluate(
                roe, 
                traderStatus.CurrentMentalState, 
                traderStatus.HealthRatio, 
                EventCategory.General, 
                ""
            );
            ApplyEmotion(evaluatedEmotion);
        }

        private float CalculateCurrentRoe()
        {
            if (tradingController == null ||
                tradingController.CurrentPosition == TradingController.PositionType.None ||
                tradingController.MarginAmount <= 0f)
            {
                return 0f;
            }

            float currentPrice = FindAnyObjectByType<MarketSimulationEngine>()?.CurrentPrice ?? tradingController.EntryPrice;
            float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long
                ? currentPrice - tradingController.EntryPrice
                : tradingController.EntryPrice - currentPrice;
            float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
            return pnl / tradingController.MarginAmount * 100f;
        }

        public void ApplyCostumeVisualScale()
        {
            ResolveCharacterImage();
            if (characterImage == null || !characterBaseScaleCaptured) return;
            float scale = characterImage.sprite != null && costumeSpriteScales.TryGetValue(characterImage.sprite, out float measuredScale)
                ? measuredScale
                : 1f;
            RectTransform rect = characterImage.rectTransform;
            Vector2 normalizedOffset = characterImage.sprite != null && costumeSpriteOffsets.TryGetValue(characterImage.sprite, out Vector2 measuredOffset)
                ? measuredOffset
                : Vector2.zero;
            bool usesMeasuredCostumeAdjustment = characterImage.sprite != null && costumeSpriteScales.ContainsKey(characterImage.sprite) &&
                !Mathf.Approximately(scale, 1f);
            if (usesMeasuredCostumeAdjustment)
            {
                scale *= CostumeOpticalScale;
                normalizedOffset.y += CostumeOpticalFootCompensation;
            }
            rect.localScale = characterBaseScale * scale;
            rect.anchoredPosition = characterBaseAnchoredPosition + new Vector2(
                normalizedOffset.x * rect.rect.width,
                normalizedOffset.y * rect.rect.height);
        }

        private void ResolveCharacterImage()
        {
            if (characterImage == null)
            {
                GameObject characterObject = GameObject.Find("ProtagonistCharacterImage");
                if (characterObject != null)
                    characterImage = characterObject.GetComponent<Image>();
            }
            if (characterImage != null && !characterBaseScaleCaptured)
            {
                characterBaseScale = characterImage.rectTransform.localScale;
                characterBaseAnchoredPosition = characterImage.rectTransform.anchoredPosition;
                characterBaseScaleCaptured = true;
            }
        }

        private void SetCharacterSprite(Sprite sprite)
        {
            ResolveCharacterImage();
            if (characterImage == null || sprite == null) return;
            characterImage.sprite = sprite;
            characterImage.preserveAspect = true;
            ApplyCostumeVisualScale();
        }

        private void LoadEmotionSprites()
        {
            emotionSprites.Clear();
            foreach (TraderEmotion emotion in Enum.GetValues(typeof(TraderEmotion)))
            {
                Sprite sprite = LoadCostumeSprite("Emotions", emotion.ToString());
                if (sprite != null)
                    emotionSprites[emotion] = sprite;
                else
                    Debug.LogWarning($"[YomiSpriteController] 감정 스프라이트를 찾지 못했습니다: {emotion}", this);
            }
        }

        private void LoadItemUseSprites()
        {
            itemUseSprites.Clear();
            LoadItemUseSprite("energy_drink", "EnergyDrink");
            LoadItemUseSprite("dessert", "Dessert");
            LoadItemUseSprite("supplement", "Supplement");
            LoadItemUseSprite("sedative", "Sedative");
            LoadItemUseSprite("malatang", "Malatang");
            LoadItemUseSprite("pasta", "Pasta");
            LoadItemUseSprite("steak", "Steak");
            LoadItemUseSprite("sushi", "Sushi");
            LoadItemUseSprite("tteokbokki", "Tteokbokki");
        }

        private void LoadSkillUpgradeSprites()
        {
            skillUpgradeSprites.Clear();
            LoadSkillUpgradeSprite(SkillType.ChartStudy, "ChartStudy");
            LoadSkillUpgradeSprite(SkillType.CubePatience, "CubePatience");
            LoadSkillUpgradeSprite(SkillType.BookJudgment, "BookJudgment");
        }

        private void LoadSkillUpgradeSprite(SkillType type, string resourceName)
        {
            Sprite sprite = LoadCostumeSprite("SkillUpgrade", resourceName);
            if (sprite != null)
                skillUpgradeSprites[type] = sprite;
            else
                Debug.LogWarning($"[YomiSpriteController] 스킬 연출 스프라이트를 찾지 못했습니다: {type}", this);
        }

        private void LoadPositionSprites()
        {
            positionSprites.Clear();
            Sprite longSprite = LoadCostumeSprite("Position", "Long");
            Sprite shortSprite = LoadCostumeSprite("Position", "Short");
            if (longSprite != null) positionSprites[TradingController.PositionType.Long] = longSprite;
            if (shortSprite != null) positionSprites[TradingController.PositionType.Short] = shortSprite;
        }

        private void ApplyOverdoseStateVisual()
        {
            if (isOverdoseStateVisualActive) return;
            if (overdoseStateSprite == null)
                overdoseStateSprite = LoadCostumeSprite("States", "Overdose");
            if (overdoseStateSprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;
            isOverdoseStateVisualActive = true;
            currentEmotion = TraderEmotion.Manic;
            SetCharacterSprite(overdoseStateSprite);

            if (dangerAuraEffect != null && dangerAuraEffect.activeSelf)
                dangerAuraEffect.SetActive(false);

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger("ExpressionState", (int)TraderEmotion.Manic);
                characterAnimator.SetTrigger("OnExpressionChanged");
            }
        }

        private void LoadItemUseSprite(string itemId, string resourceName)
        {
            Sprite sprite = LoadCostumeSprite("ItemUse", resourceName);
            if (sprite != null)
            {
                itemUseSprites[itemId] = sprite;
            }
            else
            {
                Debug.LogWarning($"[YomiSpriteController] 아이템 사용 스프라이트를 찾지 못했습니다: {itemId}", this);
            }
        }

        public void ShowItemUse(string itemId, float duration = 1f)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (itemUseSprites.Count == 0) LoadItemUseSprites();

            if (!itemUseSprites.TryGetValue(itemId, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"[YomiSpriteController] 지원하지 않는 아이템 사용 연출입니다: {itemId}", this);
                return;
            }

            ResolveCharacterImage();
            if (characterImage == null) return;

            isItemUseVisualActive = true;
            currentItemUseId = itemId;
            itemUseVisualUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
            SetCharacterSprite(sprite);
        }

        private void HandleItemConsumed(ItemData item)
        {
            if (item != null) ShowItemUse(item.ItemId, 1f);
        }

        public void BeginSkillUpgradeVisual(SkillType type)
        {
            if (skillUpgradeSprites.Count == 0) LoadSkillUpgradeSprites();
            if (!skillUpgradeSprites.TryGetValue(type, out Sprite sprite) || sprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;

            isSkillUpgradeVisualActive = true;
            currentSkillUpgradeType = type;
            isItemUseVisualActive = false;
            SetCharacterSprite(sprite);
        }

        public void EndSkillUpgradeVisual()
        {
            if (!isSkillUpgradeVisualActive) return;
            isSkillUpgradeVisualActive = false;
            ApplyEmotion(currentEmotion, true);
        }

        public void ShowPositionOpen(TradingController.PositionType type, float duration = 1f)
        {
            if (type == TradingController.PositionType.None || isSkillUpgradeVisualActive) return;
            if (positionSprites.Count == 0) LoadPositionSprites();
            if (!positionSprites.TryGetValue(type, out Sprite sprite) || sprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;

            isPositionVisualActive = true;
            currentPositionVisualType = type;
            isItemUseVisualActive = false;
            positionVisualUntil = Time.unscaledTime + Mathf.Max(0.2f, duration);
            SetCharacterSprite(sprite);
        }

        public void ShowEmotion(TraderEmotion emotion, float duration = 4f)
        {
            emotionOverrideUntil = Time.unscaledTime + Mathf.Max(0f, duration);

            if (isItemUseVisualActive)
            {
                currentEmotion = emotion;
                return;
            }

            ApplyEmotion(emotion, true);
        }

        private void ApplyEmotion(TraderEmotion emotion, bool force = false)
        {
            if (!force && emotion == currentEmotion) return;
            currentEmotion = emotion;

            ResolveCharacterImage();
            if (characterImage != null && emotionSprites.TryGetValue(emotion, out Sprite sprite))
            {
                SetCharacterSprite(sprite);
            }

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger("ExpressionState", (int)emotion);
                characterAnimator.SetTrigger("OnExpressionChanged");
            }

            bool showAura = emotion is TraderEmotion.Panicked
                or TraderEmotion.Despairing
                or TraderEmotion.Furious
                or TraderEmotion.Tearful
                or TraderEmotion.Manic
                or TraderEmotion.Obsessive
                or TraderEmotion.Vengeful;
            if (dangerAuraEffect != null && dangerAuraEffect.activeSelf != showAura)
                dangerAuraEffect.SetActive(showAura);
        }

        private Sprite LoadCostumeSprite(string category, string spriteName)
        {
            string costumeId = CostumeManager.Instance != null
                ? CostumeManager.Instance.EquippedCostumeId
                : CostumeManager.StandardId;
            string path = CostumeManager.Instance != null
                ? CostumeManager.Instance.GetResourcePath(category, spriteName)
                : $"Characters/{category}/{spriteName}";
            Sprite sprite = Resources.Load<Sprite>(path);
            bool usedCostumeSprite = sprite != null;
            if (sprite == null && !path.StartsWith("Characters/" + category + "/", StringComparison.Ordinal))
            {
                sprite = Resources.Load<Sprite>($"Characters/{category}/{spriteName}");
                Debug.LogWarning($"[YomiSpriteController] 코스튬 스프라이트가 없어 기본형으로 대체합니다: {path}", this);
            }
            if (sprite != null)
            {
                costumeSpriteScales[sprite] = usedCostumeSprite
                    ? GetMeasuredCostumeScale(costumeId, category, spriteName)
                    : 1f;
                costumeSpriteOffsets[sprite] = usedCostumeSprite
                    ? GetMeasuredCostumeOffset(costumeId, category, spriteName)
                    : Vector2.zero;
            }
            return sprite;
        }

        private void HandleCostumeChanged()
        {
            LoadEmotionSprites();
            LoadItemUseSprites();
            LoadSkillUpgradeSprites();
            LoadPositionSprites();
            overdoseStateSprite = LoadCostumeSprite("States", "Overdose");

            ResolveCharacterImage();
            if (characterImage == null) return;
            if (traderStatus != null &&
                traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                isOverdoseStateVisualActive = false;
                ApplyOverdoseStateVisual();
                return;
            }

            if (isSkillUpgradeVisualActive &&
                skillUpgradeSprites.TryGetValue(currentSkillUpgradeType, out Sprite skillSprite))
            {
                SetCharacterSprite(skillSprite);
            }
            else if (isPositionVisualActive &&
                     positionSprites.TryGetValue(currentPositionVisualType, out Sprite positionSprite))
            {
                SetCharacterSprite(positionSprite);
            }
            else if (isItemUseVisualActive &&
                     itemUseSprites.TryGetValue(currentItemUseId, out Sprite itemSprite))
            {
                SetCharacterSprite(itemSprite);
            }
            else if (emotionSprites.TryGetValue(currentEmotion, out Sprite emotionSprite))
            {
                SetCharacterSprite(emotionSprite);
            }
        }
'''

with open('Assets/Scripts/AI/CostumeMeasurements.txt', 'r', encoding='utf-16') as f:
    measurements = f.read()

with open('Assets/Scripts/AI/YomiSpriteController.cs', 'w', encoding='utf-8') as f:
    f.write(sprite_ctrl + "\n" + measurements + "\n    }\n}\n")

