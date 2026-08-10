using System;
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

        private static float GetMeasuredCostumeScale(string costumeId, string category, string spriteName)
        {
            string key = $"{category}/{spriteName}";
            if (costumeId == CostumeManager.BikiniId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.07383f, "SkillUpgrade/CubePatience" => 1.08422f, "SkillUpgrade/BookJudgment" => 1.06363f,
                    "ItemUse/Supplement" => 1.12676f, "ItemUse/Malatang" => 1.05169f, "ItemUse/Dessert" => 1.04478f,
                    "ItemUse/Steak" => 1.07081f, "ItemUse/Tteokbokki" => 1.06775f, "ItemUse/Sedative" => 1.05660f,
                    "ItemUse/Sushi" => 1.09171f, "ItemUse/EnergyDrink" => 1.11332f, "ItemUse/Pasta" => 1.05169f,
                    "Position/Short" => 1.06061f, "Position/Long" => 1.05066f,
                    "States/Standard" => 1.06066f, "States/Overdose" => 1.07383f,
                    "Emotions/Focused" => 1.06464f, "Emotions/Vengeful" => 1.08108f, "Emotions/Confident" => 1.05561f,
                    "Emotions/Panicked" => 1.12676f, "Emotions/Affectionate" => 1.08641f, "Emotions/Jealous" => 1.01174f,
                    "Emotions/Tearful" => 1.00539f, "Emotions/Anxious" => 1.12790f, "Emotions/Relieved" => 1.06768f,
                    "Emotions/Manic" => 1.10891f, "Emotions/Exhausted" => 1.13017f, "Emotions/Despairing" => 1.15583f,
                    "Emotions/Furious" => 1.06061f, "Emotions/Obsessive" => 1.12450f, "Emotions/Regretful" => 1.13360f,
                    "Emotions/Pleased" => 1.06061f, "Emotions/Euphoria" => 1.09162f, "Emotions/Suspicious" => 1.05561f,
                    "Emotions/Frustrated" => 1.09268f,
                    _ => 1f
                };
            }

            if (costumeId == CostumeManager.HanbokId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.08350f, "SkillUpgrade/CubePatience" => 1.12513f, "SkillUpgrade/BookJudgment" => 1.09421f,
                    "ItemUse/Supplement" => 1.12286f, "ItemUse/Malatang" => 1.16079f, "ItemUse/Dessert" => 1.15083f,
                    "ItemUse/Steak" => 1.13604f, "ItemUse/Tteokbokki" => 1.16684f, "ItemUse/Sedative" => 1.15067f,
                    "ItemUse/Sushi" => 1.07700f, "ItemUse/EnergyDrink" => 1.15904f, "ItemUse/Pasta" => 1.17050f,
                    "Position/Short" => 1.16008f, "Position/Long" => 1.10934f,
                    "States/Standard" => 1.18704f, "States/Overdose" => 1.05094f,
                    "Emotions/Focused" => 1.23614f, "Emotions/Vengeful" => 1.33493f, "Emotions/Confident" => 1.12072f,
                    "Emotions/Panicked" => 1.13892f, "Emotions/Affectionate" => 1.14344f, "Emotions/Jealous" => 1.16146f,
                    "Emotions/Tearful" => 1.10626f, "Emotions/Anxious" => 1.24138f, "Emotions/Relieved" => 1.19379f,
                    "Emotions/Manic" => 1.06903f, "Emotions/Exhausted" => 1.09961f, "Emotions/Despairing" => 1.10069f,
                    "Emotions/Furious" => 1.07625f, "Emotions/Obsessive" => 1.07729f, "Emotions/Regretful" => 1.12072f,
                    "Emotions/Pleased" => 1.23725f, "Emotions/Euphoria" => 1.17884f, "Emotions/Suspicious" => 1.14008f,
                    "Emotions/Frustrated" => 1.15440f,
                    _ => 1f
                };
            }

            if (costumeId == CostumeManager.YukataId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.23043f, "SkillUpgrade/CubePatience" => 1.05288f, "SkillUpgrade/BookJudgment" => 1.10287f,
                    "ItemUse/Supplement" => 1.16510f, "ItemUse/Malatang" => 1.06877f, "ItemUse/Dessert" => 1.15321f,
                    "ItemUse/Steak" => 1.10682f, "ItemUse/Tteokbokki" => 1.05268f, "ItemUse/Sedative" => 1.12399f,
                    "ItemUse/Sushi" => 1.16199f, "ItemUse/EnergyDrink" => 1.09421f, "ItemUse/Pasta" => 1.08852f,
                    "Position/Short" => 1.06084f, "Position/Long" => 1.09091f,
                    "States/Standard" => 1.07095f, "States/Overdose" => 1.01921f,
                    "Emotions/Focused" => 1.37654f, "Emotions/Vengeful" => 1.06999f, "Emotions/Confident" => 1.08577f,
                    "Emotions/Panicked" => 1.11389f, "Emotions/Affectionate" => 1.07205f, "Emotions/Jealous" => 1.12286f,
                    "Emotions/Tearful" => 1.14374f, "Emotions/Anxious" => 1.09091f, "Emotions/Relieved" => 1.09852f,
                    "Emotions/Manic" => 1.06190f, "Emotions/Exhausted" => 1.10835f, "Emotions/Despairing" => 1.06801f,
                    "Emotions/Furious" => 1.10615f, "Emotions/Obsessive" => 1.08147f, "Emotions/Regretful" => 1.09862f,
                    "Emotions/Pleased" => 1.12274f, "Emotions/Euphoria" => 1.08471f, "Emotions/Suspicious" => 1.05288f,
                    "Emotions/Frustrated" => 1.06910f,
                    _ => 1f
                };
            }

            return 1f;
        }

        // X???ㅻ（??以묒떖, Y??諛쒕걹??湲곕낯 ?붾?? 留욎텣 600x1180 ?뺢퇋??醫뚰몴?낅땲??
        private static Vector2 GetMeasuredCostumeOffset(string costumeId, string category, string spriteName)
        {
            string key = $"{category}/{spriteName}";
            if (costumeId == CostumeManager.BikiniId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.030425f, -0.018655f), "SkillUpgrade/CubePatience" => new(-0.027106f, -0.028943f), "SkillUpgrade/BookJudgment" => new(-0.020333f, -0.015774f),
                    "ItemUse/Supplement" => new(-0.012207f, -0.049654f), "ItemUse/Malatang" => new(-0.021867f, -0.012901f), "ItemUse/Dessert" => new(-0.035697f, -0.006198f),
                    "ItemUse/Steak" => new(-0.033909f, -0.021749f), "ItemUse/Tteokbokki" => new(-0.018629f, -0.019426f), "ItemUse/Sedative" => new(-0.031651f, -0.017013f),
                    "ItemUse/Sushi" => new(-0.009021f, -0.036043f), "ItemUse/EnergyDrink" => new(-0.000928f, -0.044344f), "ItemUse/Pasta" => new(0.007888f, -0.017358f),
                    "Position/Short" => new(0.013308f, -0.022470f), "Position/Long" => new(-0.025349f, -0.008013f),
                    "States/Standard" => new(-0.031820f, -0.017053f), "States/Overdose" => new(-0.034899f, -0.040496f),
                    "Emotions/Focused" => new(-0.019518f, -0.027067f), "Emotions/Vengeful" => new(-0.045045f, -0.033898f), "Emotions/Confident" => new(-0.028103f, -0.015655f),
                    "Emotions/Panicked" => new(0.011268f, -0.047744f), "Emotions/Affectionate" => new(-0.018940f, -0.022520f), "Emotions/Jealous" => new(-0.031195f, 0.007288f),
                    "Emotions/Tearful" => new(0.000838f, 0.000000f), "Emotions/Anxious" => new(-0.012112f, -0.052093f), "Emotions/Relieved" => new(-0.030195f, -0.014025f),
                    "Emotions/Manic" => new(-0.032252f, -0.036650f), "Emotions/Exhausted" => new(-0.034847f, -0.036874f), "Emotions/Despairing" => new(-0.035508f, -0.030855f),
                    "Emotions/Furious" => new(-0.014091f, -0.023369f), "Emotions/Obsessive" => new(0.000937f, -0.049554f), "Emotions/Regretful" => new(-0.004612f, -0.054759f),
                    "Emotions/Pleased" => new(-0.032652f, -0.017078f), "Emotions/Euphoria" => new(0.001819f, -0.032378f), "Emotions/Suspicious" => new(-0.005232f, -0.021023f),
                    "Emotions/Frustrated" => new(-0.016313f, -0.008797f),
                    _ => Vector2.zero
                };
            }

            if (costumeId == CostumeManager.HanbokId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.013474f, -0.021190f), "SkillUpgrade/CubePatience" => new(-0.020419f, -0.032472f), "SkillUpgrade/BookJudgment" => new(0.022041f, -0.001974f),
                    "ItemUse/Supplement" => new(-0.028072f, -0.014430f), "ItemUse/Malatang" => new(0.016712f, -0.018267f), "ItemUse/Dessert" => new(-0.025768f, -0.035238f),
                    "ItemUse/Steak" => new(-0.004620f, -0.029903f), "ItemUse/Tteokbokki" => new(-0.008473f, -0.027758f), "ItemUse/Sedative" => new(-0.011256f, -0.038222f),
                    "ItemUse/Sushi" => new(-0.003462f, -0.007334f), "ItemUse/EnergyDrink" => new(-0.018086f, -0.019221f), "ItemUse/Pasta" => new(0.006970f, -0.021399f),
                    "Position/Short" => new(-0.007600f, -0.033562f), "Position/Long" => new(0.012109f, -0.026416f),
                    "States/Standard" => new(-0.034622f, -0.008285f), "States/Overdose" => new(-0.021937f, -0.025871f),
                    "Emotions/Focused" => new(-0.007014f, -0.033299f), "Emotions/Vengeful" => new(0.002225f, -0.028566f), "Emotions/Confident" => new(-0.030619f, -0.029545f),
                    "Emotions/Panicked" => new(-0.001782f, -0.032875f), "Emotions/Affectionate" => new(0.020369f, -0.010902f), "Emotions/Jealous" => new(0.020460f, -0.044853f),
                    "Emotions/Tearful" => new(-0.007198f, -0.035246f), "Emotions/Anxious" => new(0.015718f, -0.002835f), "Emotions/Relieved" => new(-0.029522f, -0.035997f),
                    "Emotions/Manic" => new(-0.006954f, -0.016395f), "Emotions/Exhausted" => new(-0.019993f, -0.003388f), "Emotions/Despairing" => new(-0.012590f, -0.014035f),
                    "Emotions/Furious" => new(-0.037542f, -0.014234f), "Emotions/Obsessive" => new(-0.001795f, -0.021031f), "Emotions/Regretful" => new(-0.038090f, -0.032394f),
                    "Emotions/Pleased" => new(0.008841f, -0.047384f), "Emotions/Euphoria" => new(0.001816f, -0.019632f), "Emotions/Suspicious" => new(0.014601f, -0.019985f),
                    "Emotions/Frustrated" => new(-0.000705f, -0.029969f),
                    _ => Vector2.zero
                };
            }

            if (costumeId == CostumeManager.YukataId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.000833f, -0.040341f), "SkillUpgrade/CubePatience" => new(0.010617f, -0.012514f), "SkillUpgrade/BookJudgment" => new(0.008443f, -0.049666f),
                    "ItemUse/Supplement" => new(-0.044662f, -0.048591f), "ItemUse/Malatang" => new(-0.032839f, -0.022673f), "ItemUse/Dessert" => new(-0.006599f, -0.030426f),
                    "ItemUse/Steak" => new(-0.031271f, -0.023495f), "ItemUse/Tteokbokki" => new(-0.011316f, -0.023217f), "ItemUse/Sedative" => new(-0.011033f, -0.042545f),
                    "ItemUse/Sushi" => new(-0.014255f, -0.024687f), "ItemUse/EnergyDrink" => new(-0.005314f, -0.022295f), "ItemUse/Pasta" => new(-0.007183f, -0.017103f),
                    "Position/Short" => new(-0.014978f, -0.018032f), "Position/Long" => new(-0.011742f, -0.034746f),
                    "States/Standard" => new(0.003570f, -0.016427f), "States/Overdose" => new(-0.014455f, -0.018587f),
                    "Emotions/Focused" => new(-0.019187f, -0.020893f), "Emotions/Vengeful" => new(-0.007133f, -0.001419f), "Emotions/Confident" => new(-0.029716f, -0.017555f),
                    "Emotions/Panicked" => new(-0.016613f, -0.026479f), "Emotions/Affectionate" => new(0.000180f, -0.020564f), "Emotions/Jealous" => new(-0.014869f, -0.041921f),
                    "Emotions/Tearful" => new(-0.014057f, -0.049555f), "Emotions/Anxious" => new(0.000076f, -0.031048f), "Emotions/Relieved" => new(-0.007159f, -0.027039f),
                    "Emotions/Manic" => new(-0.007810f, -0.024826f), "Emotions/Exhausted" => new(-0.016445f, -0.036300f), "Emotions/Despairing" => new(0.011740f, -0.020393f),
                    "Emotions/Furious" => new(0.008473f, -0.039975f), "Emotions/Obsessive" => new(-0.001802f, -0.013782f), "Emotions/Regretful" => new(-0.016315f, -0.027084f),
                    "Emotions/Pleased" => new(-0.024955f, -0.034357f), "Emotions/Euphoria" => new(-0.013630f, -0.038221f), "Emotions/Suspicious" => new(-0.010397f, -0.016128f),
                    "Emotions/Frustrated" => new(-0.023049f, -0.008213f),
                    _ => Vector2.zero
                };
            }

            return Vector2.zero;
        }

    }
}
