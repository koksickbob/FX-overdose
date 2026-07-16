using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>ShopManager.CatalogItems에 맞춰 고품질 픽셀 상점 카드를 동적으로 생성합니다.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class DynamicShopUI : MonoBehaviour
{
    private const string CareBadgeResource = "UI/Shop/CareBadgeFrame";
    private const string ActiveBadgeResource = "UI/Shop/ActiveBadgeFrame";
    private const string CareBuyResource = "UI/Shop/CareBuyButton";
    private const string ActiveBuyResource = "UI/Shop/ActiveBuyButton";

    [SerializeField] private ShopManager shopManager;
    [SerializeField] private Sprite cardFrameSprite;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField, Min(1)] private int maxColumns = 2;
    [Header("Marketplace Skin")]
    [SerializeField] private Sprite careBadgeSprite;
    [SerializeField] private Sprite activeBadgeSprite;
    [SerializeField] private Sprite careBuyButtonSprite;
    [SerializeField] private Sprite activeBuyButtonSprite;

    private RectTransform modal;
    private RectTransform content;
    private TMP_Text balanceText;
    private readonly List<GameObject> cards = new();
    private float lastDisplayedBalance = -1f;

    private void Awake()
    {
        if (shopManager == null) shopManager = FindAnyObjectByType<ShopManager>();
        BuildStructure();
    }

    private void OnEnable()
    {
        if (modal == null) BuildStructure();
        Rebuild();
        RefreshBalance();
    }

    private void Update() => RefreshBalance();

    [ContextMenu("Rebuild Shop UI")]
    public void Rebuild()
    {
        if (content == null || shopManager == null) return;
        ClearCards();
        lastDisplayedBalance = -1f;

        // 상점 아이템에 사용되는 모든 문자열을 수집하여 폰트 아틀라스에 사전 등록합니다.
        // 스크롤 중 새 글자가 발견되어 SDF 아틀라스가 동적 갱신되고 ImportAsset이 호출되는 현상(스크롤 끊김 및 Importer 경고)을 방지합니다.
        TMP_FontAsset targetFont = font != null ? font : TMP_Settings.defaultFontAsset;
        if (targetFont != null)
        {
            System.Text.StringBuilder sb = new();
            sb.Append("FX MARKET BALANCE $0123456789.,-+% ALL ITEMS CARE ACTIVE GEAR INSTANT DELIVERY UPGRADE OWNED x MAXED BUY LV ✓ ");
            foreach (ItemData item in shopManager.CatalogItems)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(item.ItemName)) sb.Append(item.ItemName);
                if (!string.IsNullOrEmpty(item.Description)) sb.Append(item.Description);
                if (ActiveItemEffectManager.Instance != null && item.IsActiveItem)
                {
                    sb.Append(ActiveItemEffectManager.Instance.GetItemStatusLabel(item));
                }
            }
            targetFont.TryAddCharacters(sb.ToString(), out _);
        }

        foreach (ItemData item in shopManager.CatalogItems)
        {
            if (item == null) continue;
            cards.Add(CreateProductCard(item));
        }

        LayoutCards();
    }

    private void BuildStructure()
    {
        // TradingViewCanvas(sortingOrder 10)보다 위에 표시되는 전용 상점 Canvas입니다.
        Canvas overlayCanvas = GetOrAdd<Canvas>(gameObject);
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;
        GetOrAdd<GraphicRaycaster>(gameObject);

        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(0.005f, 0.009f, 0.018f, 0.82f);

        HideLegacyProductButtons();
        modal = GetOrCreateRect(transform, "ShopModal");
        SetRect(modal, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero, Vector2.zero);
        Image modalImage = GetOrAdd<Image>(modal.gameObject);
        modalImage.color = new Color(0.018f, 0.043f, 0.078f, 0.995f);
        Outline modalOutline = GetOrAdd<Outline>(modal.gameObject);
        modalOutline.effectColor = new Color(0.62f, 0.49f, 0.78f, 1f);
        modalOutline.effectDistance = UIStrokeStyle.EffectDistance;

        BuildHeader();
        BuildCategoryBar();
        BuildFooter();
        BuildScrollArea();

        Transform vp = modal?.Find("ProductViewport");
        ScrollRect scroll = vp != null ? vp.GetComponent<ScrollRect>() : null;
        if (scroll != null)
        {
            ForwardScrollEvents(gameObject, scroll);
            ForwardScrollEvents(modal?.gameObject, scroll);
            Transform header = modal?.Find("Header");
            if (header != null) ForwardScrollEvents(header.gameObject, scroll);
            Transform categoryBar = modal?.Find("CategoryBar");
            if (categoryBar != null) ForwardScrollEvents(categoryBar.gameObject, scroll);
            Transform footerBackground = modal?.Find("FooterBackground");
            if (footerBackground != null) ForwardScrollEvents(footerBackground.gameObject, scroll);
        }
    }

    private void BuildHeader()
    {
        RectTransform header = GetOrCreateRect(modal, "Header");
        SetRect(header, new Vector2(0.025f, 0.855f), new Vector2(0.975f, 0.98f), Vector2.zero, Vector2.zero);

        TMP_Text title = GetOrCreateText(header, "Title", 32f, TextAlignmentOptions.MidlineLeft, true);
        title.text = "FX MARKET";
        title.color = Color.white;
        SetRect(title.rectTransform, Vector2.zero, new Vector2(0.53f, 1f), new Vector2(10f, 0f), Vector2.zero);

        balanceText = GetOrCreateText(header, "Balance", 24f, TextAlignmentOptions.MidlineRight, true);
        balanceText.color = new Color(1f, 0.78f, 0.25f, 1f);
        SetRect(balanceText.rectTransform, new Vector2(0.50f, 0f), new Vector2(0.87f, 1f), Vector2.zero, new Vector2(-8f, 0f));

        Button close = FindCloseButton();
        if (close != null)
        {
            close.transform.SetParent(header, false);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.90f, 0.10f), new Vector2(0.99f, 0.90f), Vector2.zero, Vector2.zero);
            Image image = close.GetComponent<Image>();
            image.color = new Color(0.16f, 0.13f, 0.25f, 1f);
            Outline closeOutline = GetOrAdd<Outline>(close.gameObject);
            closeOutline.effectColor = UIStrokeStyle.DefaultColor;
            closeOutline.effectDistance = UIStrokeStyle.EffectDistance;
            closeOutline.useGraphicAlpha = true;
            TMP_Text label = close.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = "X"; ApplyTextStyle(label, 25f, TextAlignmentOptions.Center, true); }
        }
    }

    private void BuildCategoryBar()
    {
        RectTransform bar = GetOrCreateRect(modal, "CategoryBar");
        SetRect(bar, new Vector2(0.025f, 0.775f), new Vector2(0.975f, 0.845f), Vector2.zero, Vector2.zero);
        Image background = GetOrAdd<Image>(bar.gameObject);
        background.color = new Color(0.035f, 0.075f, 0.12f, 0.96f);

        TMP_Text categories = GetOrCreateText(bar, "Categories", 18f, TextAlignmentOptions.MidlineLeft, true);
        categories.text = "ALL ITEMS     CARE     ACTIVE GEAR";
        categories.color = new Color(0.66f, 0.88f, 0.96f, 1f);
        SetRect(categories.rectTransform, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(18f, 0f), Vector2.zero);

        TMP_Text delivery = GetOrCreateText(bar, "Delivery", 16f, TextAlignmentOptions.MidlineRight, true);
        delivery.text = "INSTANT DELIVERY  •  BUFFS APPLY NOW";
        delivery.color = new Color(1f, 0.76f, 0.25f, 1f);
        SetRect(delivery.rectTransform, new Vector2(0.58f, 0f), Vector2.one, Vector2.zero, new Vector2(-18f, 0f));
    }

    private void BuildScrollArea()
    {
        RectTransform viewport = GetOrCreateRect(modal, "ProductViewport");
        SetRect(viewport, new Vector2(0.025f, 0.145f), new Vector2(0.975f, 0.755f), Vector2.zero, Vector2.zero);
        GetOrAdd<RectMask2D>(viewport.gameObject);

        content = GetOrCreateRect(viewport, "ProductContent");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;

        ScrollRect scroll = GetOrAdd<ScrollRect>(viewport.gameObject);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
    }

    private void BuildFooter()
    {
        RectTransform footerBackground = GetOrCreateRect(modal, "FooterBackground");
        SetRect(footerBackground, new Vector2(0.035f, 0.025f), new Vector2(0.965f, 0.12f), Vector2.zero, Vector2.zero);
        Image bg = GetOrAdd<Image>(footerBackground.gameObject);
        bg.color = new Color(0.07f, 0.08f, 0.16f, 0.9f);

        TMP_Text footer = GetOrCreateText(footerBackground, "Footer", 18f, TextAlignmentOptions.Center, true);
        footer.text = "SECURE CHECKOUT  •  PURCHASES APPLY IMMEDIATELY  •  TRADE RESPONSIBLY";
        footer.color = new Color(0.79f, 0.74f, 0.91f, 1f);
        SetRect(footer.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private GameObject CreateProductCard(ItemData item)
    {
        GameObject card = new($"ShopCard_{item.ItemId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        card.transform.SetParent(content, false);
        Image frame = card.GetComponent<Image>();
        frame.sprite = cardFrameSprite;
        frame.type = cardFrameSprite != null ? Image.Type.Sliced : Image.Type.Simple;

        Outline cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = UIStrokeStyle.DefaultColor;
        cardOutline.effectDistance = UIStrokeStyle.EffectDistance;
        cardOutline.useGraphicAlpha = true;

        bool isActive = item.IsActiveItem;
        frame.color = isActive ? new Color(1f, 0.88f, 0.5f, 1f) : (item.Type == ItemData.EffectType.Health ? new Color(0.62f, 0.91f, 1f, 1f) : new Color(1f, 0.72f, 0.98f, 1f));

        GameObject badgeObject = new("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeObject.transform.SetParent(card.transform, false);
        Image badgeBackground = badgeObject.GetComponent<Image>();
        badgeBackground.sprite = isActive
            ? ResolveSprite(activeBadgeSprite, ActiveBadgeResource)
            : ResolveSprite(careBadgeSprite, CareBadgeResource);
        badgeBackground.type = Image.Type.Simple;
        badgeBackground.preserveAspect = false;
        badgeBackground.color = Color.white;
        SetRect(badgeObject.GetComponent<RectTransform>(), new Vector2(0.04f, 0.82f), new Vector2(0.30f, 0.94f), Vector2.zero, Vector2.zero);
        TMP_Text badge = CreateText(badgeObject.transform, "Label", 13f, TextAlignmentOptions.Center);
        badge.text = isActive ? "ACTIVE GEAR" : "CARE ITEM";
        badge.color = Color.white;
        SetRect(badge.rectTransform, new Vector2(0.16f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);

        Image icon = CreateImage(card.transform, "Icon");
        icon.sprite = item.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetRect(icon.rectTransform, new Vector2(0.04f, 0.32f), new Vector2(0.39f, 0.80f), Vector2.zero, Vector2.zero);

        TMP_Text name = CreateText(card.transform, "Name", 21f, TextAlignmentOptions.MidlineLeft);
        name.text = item.ItemName.ToUpperInvariant();
        name.color = isActive ? new Color(1f, 0.75f, 0.2f, 1f) : (item.Type == ItemData.EffectType.Health ? new Color(0.16f, 0.82f, 1f, 1f) : new Color(0.91f, 0.50f, 1f, 1f));
        SetRect(name.rectTransform, new Vector2(0.41f, 0.72f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero);

        TMP_Text effect = CreateText(card.transform, "Effect", 18f, TextAlignmentOptions.MidlineLeft);
        string effectString = "";
        switch (item.Type)
        {
            case ItemData.EffectType.Health: effectString = $"HP +{item.EffectAmount:0}"; break;
            case ItemData.EffectType.Mental: effectString = $"MENTAL +{item.EffectAmount:0}"; break;
            case ItemData.EffectType.ProfitBoost: effectString = $"PROFIT +{item.EffectAmount:0}% (UPGRADE)"; break;
            case ItemData.EffectType.LossReduction: effectString = $"LOSS -{item.EffectAmount:0}% (UPGRADE)"; break;
            case ItemData.EffectType.MentalDrainGuard: effectString = $"MENTAL DRAIN -{item.EffectAmount:0}%"; break;
            case ItemData.EffectType.HealthDrainGuard: effectString = $"HP DRAIN -{item.EffectAmount:0}%"; break;
            default: effectString = $"EFFECT +{item.EffectAmount:0}"; break;
        }
        effect.text = effectString;
        effect.color = isActive ? new Color(0.4f, 0.95f, 0.6f, 1f) : (item.Type == ItemData.EffectType.Health ? new Color(0.28f, 0.88f, 0.52f, 1f) : new Color(0.72f, 0.43f, 0.96f, 1f));
        SetRect(effect.rectTransform, new Vector2(0.41f, 0.53f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);

        TMP_Text description = CreateText(card.transform, "Description", 13f, TextAlignmentOptions.TopLeft);
        description.text = item.Description;
        description.color = new Color(0.70f, 0.75f, 0.83f, 1f);
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(description.rectTransform, new Vector2(0.41f, 0.35f), new Vector2(0.96f, 0.53f), Vector2.zero, Vector2.zero);

        TMP_Text price = CreateText(card.transform, "Price", 25f, TextAlignmentOptions.MidlineLeft);
        price.text = $"$ {item.Price:N0}";
        price.color = new Color(1f, 0.76f, 0.22f, 1f);
        SetRect(price.rectTransform, new Vector2(0.06f, 0.18f), new Vector2(0.54f, 0.33f), Vector2.zero, Vector2.zero);

        TMP_Text owned = CreateText(card.transform, "Owned", 15f, TextAlignmentOptions.MidlineRight);
        SetRect(owned.rectTransform, new Vector2(0.50f, 0.18f), new Vector2(0.94f, 0.33f), Vector2.zero, Vector2.zero);

        GameObject buyObject = new("BuyButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(ShopItemButton));
        buyObject.transform.SetParent(card.transform, false);
        SetRect(buyObject.GetComponent<RectTransform>(), new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.17f), Vector2.zero, Vector2.zero);
        Image buyImage = buyObject.GetComponent<Image>();
        buyImage.sprite = isActive
            ? ResolveSprite(activeBuyButtonSprite, ActiveBuyResource)
            : ResolveSprite(careBuyButtonSprite, CareBuyResource);
        buyImage.type = Image.Type.Simple;
        buyImage.preserveAspect = false;
        buyImage.color = Color.white;
        Outline buyOutline = buyObject.GetComponent<Outline>();
        buyOutline.enabled = buyImage.sprite == null;
        buyOutline.effectColor = UIStrokeStyle.DefaultColor;
        buyOutline.effectDistance = UIStrokeStyle.EffectDistance;
        buyOutline.useGraphicAlpha = true;
        Button buy = buyObject.GetComponent<Button>();
        buy.targetGraphic = buyImage;
        TMP_Text buyLabel = CreateText(buyObject.transform, "Label", 22f, TextAlignmentOptions.Center);
        buyLabel.text = "BUY";
        buyLabel.color = Color.white;
        SetRect(buyLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        buyObject.GetComponent<ShopItemButton>().Configure(shopManager, shopManager.Inventory, item, buy, name, price, owned);
        Transform vp = modal?.Find("ProductViewport");
        ScrollRect scroll = vp != null ? vp.GetComponent<ScrollRect>() : null;
        if (scroll != null) ForwardScrollEvents(card, scroll);
        return card;
    }

    private void LayoutCards()
    {
        int count = cards.Count;
        int columns = Mathf.Max(1, Mathf.Min(maxColumns, count));
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = content.parent as RectTransform;
        float width = viewport != null && viewport.rect.width > 0f
            ? viewport.rect.width
            : (content.rect.width > 0f ? content.rect.width : 900f);
        float spacing = 18f;
        float sidePadding = 12f; // RectMask2D 경계에서 카드 테두리가 잘리지 않도록 안전 여백 확보
        float cardWidth = (width - sidePadding * 2f - spacing * (columns - 1)) / columns;
        float cardHeight = Mathf.Clamp(cardWidth * 0.72f, 250f, 330f);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        content.sizeDelta = new Vector2(0f, rows * cardHeight + (rows - 1) * spacing);

        for (int i = 0; i < count; i++)
        {
            int col = i % columns;
            int row = i / columns;
            RectTransform rect = cards[i].GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(cardWidth, cardHeight);
            rect.anchoredPosition = new Vector2(sidePadding + col * (cardWidth + spacing), -row * (cardHeight + spacing));
        }
    }

    private void RefreshBalance()
    {
        if (balanceText != null && shopManager?.GameManager != null)
        {
            float cur = shopManager.GameManager.CurrentBalance;
            if (Mathf.Abs(cur - lastDisplayedBalance) > 0.01f)
            {
                lastDisplayedBalance = cur;
                balanceText.text = $"BALANCE  ${cur:N0}";
            }
        }
    }

    private void HideLegacyProductButtons()
    {
        foreach (ShopItemButton item in GetComponentsInChildren<ShopItemButton>(true))
            if (!item.name.Equals("BuyButton")) item.gameObject.SetActive(false);
        Transform oldTitle = transform.Find("Title");
        if (oldTitle != null) oldTitle.gameObject.SetActive(false);
    }

    private Button FindCloseButton()
    {
        Transform found = transform.Find("CloseButton");
        if (found == null) found = GetComponentInChildren<Button>(true)?.transform;
        return found != null ? found.GetComponent<Button>() : null;
    }

    private void ClearCards()
    {
        foreach (GameObject card in cards) if (card != null) Destroy(card);
        cards.Clear();
    }

    private TMP_Text GetOrCreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, bool autoSize = false)
    {
        Transform found = parent.Find(name);
        TMP_Text text = found != null ? found.GetComponent<TMP_Text>() : null;
        if (text != null) ApplyTextStyle(text, size, alignment, autoSize);
        return text != null ? text : CreateText(parent, name, size, alignment, autoSize);
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, bool autoSize = false)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        ApplyTextStyle(text, size, alignment, autoSize);
        return text;
    }

    private void ApplyTextStyle(TMP_Text text, float size, TextAlignmentOptions alignment, bool autoSize = false)
    {
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.enableAutoSizing = autoSize;
        if (autoSize)
        {
            text.fontSizeMin = Mathf.Max(10f, size - 6f);
            text.fontSizeMax = size;
        }
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static Image CreateImage(Transform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static RectTransform GetOrCreateRect(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.GetComponent<RectTransform>();
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T value = go.GetComponent<T>();
        return value != null ? value : go.AddComponent<T>();
    }

    private static Sprite ResolveSprite(Sprite assigned, string resourcePath)
    {
        if (assigned != null) return assigned;
        Sprite loaded = Resources.Load<Sprite>(resourcePath);
        if (loaded != null) return loaded;
        Sprite[] all = Resources.LoadAll<Sprite>(resourcePath);
        if (all != null && all.Length > 0) return all[0];

        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError($"[DynamicShopUI] 상점 스킨을 불러오지 못했습니다: Resources/{resourcePath}");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void ForwardScrollEvents(GameObject go, ScrollRect scroll)
    {
        if (go == null || scroll == null) return;
        ScrollEventForwarder forwarder = GetOrAdd<ScrollEventForwarder>(go);
        forwarder.targetScrollRect = scroll;
    }
}

/// <summary>
/// UI 요소(헤더, 푸터, 마스크 외 영역 등)에 발생한 마우스 휠 및 터치 드래그 스크롤 이벤트를 대상 ScrollRect로 전달합니다.
/// </summary>
public class ScrollEventForwarder : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ScrollRect targetScrollRect;

    public void OnScroll(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnScroll(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (targetScrollRect != null) targetScrollRect.OnEndDrag(eventData);
    }
}
