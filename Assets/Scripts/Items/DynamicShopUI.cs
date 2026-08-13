using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>ShopManager.CatalogItems에 맞춰 고품질 픽셀 상점 카드를 동적으로 생성합니다.</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class DynamicShopUI : MonoBehaviour
{
    private const float ShopFontScale = 1.1f;
    private const string BrowserShellResource = "UI/Shop/MarketplaceBrowserShell";
    private const string ProductCardResource = "UI/Shop/MarketplaceProductCard";
    private const string CareBadgeResource = "UI/Shop/CareBadgeFrame";
    private const string ActiveBadgeResource = "UI/Shop/ActiveBadgeFrame";
    private const string CareBuyResource = "UI/Shop/CareBuyButton";
    private const string ActiveBuyResource = "UI/Shop/ActiveBuyButton";

    private static readonly Color DeepBackground = new Color32(11, 15, 25, 255);
    private static readonly Color PanelBackground = new Color32(15, 23, 42, 255);
    private static readonly Color HeaderBackground = new Color32(20, 29, 51, 255);
    private static readonly Color BorderColor = new Color32(59, 75, 102, 255);
    private static readonly Color Cyan = new Color32(6, 182, 212, 255);
    private static readonly Color BodyText = new Color32(203, 213, 225, 255);
    private static readonly Color MutedText = new Color32(102, 117, 143, 255);
    private static readonly Color SuccessGreen = new Color32(34, 197, 94, 255);
    private static readonly Color SpecialGold = new Color32(234, 179, 8, 255);
    private static readonly Color ApparelMagenta = new Color32(217, 70, 239, 255);
    private static readonly Color DeliveryOrange = new Color32(249, 115, 22, 255);

    private enum CategoryFilter
    {
        All,
        Care,
        DeliveryFood,
        ActiveGear,
        Apparel
    }

    [SerializeField] private ShopManager shopManager;
    [SerializeField] private Sprite cardFrameSprite;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField, Min(1)] private int maxColumns = 2;
    [Header("Marketplace Skin")]
    [SerializeField] private Sprite browserShellSprite;
    [SerializeField] private Sprite productCardSprite;
    [SerializeField] private Sprite careBadgeSprite;
    [SerializeField] private Sprite activeBadgeSprite;
    [SerializeField] private Sprite careBuyButtonSprite;
    [SerializeField] private Sprite activeBuyButtonSprite;

    private RectTransform modal;
    private RectTransform content;
    private TMP_Text balanceText;
    private TMP_Text resultCountText;
    private TMP_Text emptyStateText;
    private TMP_InputField searchInput;
    private readonly Button[] categoryButtons = new Button[5];
    private readonly Image[] categoryButtonBackgrounds = new Image[5];
    private readonly List<GameObject> cards = new();
    private float lastDisplayedBalance = -1f;
    private CategoryFilter activeFilter = CategoryFilter.All;
    private string searchQuery = string.Empty;

    private void Awake()
    {
        if (shopManager == null) shopManager = FindAnyObjectByType<ShopManager>();
        BuildStructure();
    }

    private void OnEnable()
    {
        if (modal == null) BuildStructure();
        if (CostumeManager.Instance != null)
        {
            CostumeManager.Instance.OnCostumesChanged -= HandleCostumesChanged;
            CostumeManager.Instance.OnCostumesChanged += HandleCostumesChanged;
        }
        Rebuild();
        RefreshBalance();
    }

    private void OnDisable()
    {
        if (CostumeManager.Instance != null)
            CostumeManager.Instance.OnCostumesChanged -= HandleCostumesChanged;
    }

    private void OnDestroy()
    {
        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
        }
        if (CostumeManager.Instance != null)
            CostumeManager.Instance.OnCostumesChanged -= HandleCostumesChanged;
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
            sb.Append("FX 마켓 보유 자산 $0123456789.,-+% 전체 상품 회복 아이템 배달 음식 장비 의상 즉시 적용 업그레이드 보유 최대 레벨 구매 장착 중 잠김 무료 검색 결과 없음 x ✓ ");
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
            if (CostumeManager.Instance != null)
            {
                foreach (CostumeManager.CostumeDefinition costume in CostumeManager.Instance.Catalog)
                {
                    sb.Append(costume.DisplayName);
                    sb.Append(costume.Description);
                }
            }
            if (targetFont.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                targetFont.TryAddCharacters(sb.ToString(), out _);
            }
        }

        int visibleCount = 0;
        foreach (ItemData item in shopManager.CatalogItems)
        {
            if (item == null || !ShouldShowItem(item) || (FXOverdose.P2P.Infrastructure.P2PNetworkSessionManager.Instance?.IsRunning==true && !IsP2PAllowed(item))) continue;
            cards.Add(CreateProductCard(item));
            visibleCount++;
        }

        if (FXOverdose.P2P.Infrastructure.P2PNetworkSessionManager.Instance?.IsRunning!=true && CostumeManager.Instance != null &&
            (activeFilter == CategoryFilter.All || activeFilter == CategoryFilter.Apparel))
        {
            foreach (CostumeManager.CostumeDefinition costume in CostumeManager.Instance.Catalog)
            {
                if (!ShouldShowCostume(costume)) continue;
                cards.Add(CreateCostumeCard(costume));
                visibleCount++;
            }
        }

        LayoutCards();
        if (resultCountText != null)
        {
            resultCountText.text = $"상품 {visibleCount}개";
        }
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(visibleCount == 0);
        }
    }

    private static bool IsP2PAllowed(ItemData item)=>item!=null&&(item.ItemId=="energy_drink"||item.ItemId=="dessert"||item.ItemId=="sedative"||item.ItemId=="supplement");

    private void BuildStructure()
    {
        // TradingViewCanvas보다 위에 표시되며 돌발 이벤트/설정/정산 팝업보다는 아래에 유지합니다.
        Canvas overlayCanvas = GetOrAdd<Canvas>(gameObject);
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;
        GetOrAdd<GraphicRaycaster>(gameObject);

        RectTransform root = GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        HideLegacyProductButtons();
        modal = GetOrCreateRect(transform, "ShopModal");
        SetRect(modal, new Vector2(0.035f, 0.045f), new Vector2(0.965f, 0.955f), Vector2.zero, Vector2.zero);
        Image modalImage = GetOrAdd<Image>(modal.gameObject);
        modalImage.sprite = ResolveSprite(browserShellSprite, BrowserShellResource);
        modalImage.type = Image.Type.Simple;
        modalImage.preserveAspect = false;
        modalImage.color = modalImage.sprite != null ? Color.white : PanelBackground;
        Outline modalOutline = GetOrAdd<Outline>(modal.gameObject);
        modalOutline.effectColor = BorderColor;
        modalOutline.effectDistance = UIStrokeStyle.EffectDistance;
        modalOutline.useGraphicAlpha = true;

        BuildBrowserChrome();
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
            Transform browser = modal?.Find("BrowserChrome");
            if (browser != null) ForwardScrollEvents(browser.gameObject, scroll);
            Transform header = modal?.Find("Header");
            if (header != null) ForwardScrollEvents(header.gameObject, scroll);
            Transform categoryBar = modal?.Find("CategoryBar");
            if (categoryBar != null) ForwardScrollEvents(categoryBar.gameObject, scroll);
            Transform footerBackground = modal?.Find("FooterBackground");
            if (footerBackground != null) ForwardScrollEvents(footerBackground.gameObject, scroll);
        }
    }

    private void BuildBrowserChrome()
    {
        RectTransform browser = GetOrCreateRect(modal, "BrowserChrome");
        SetRect(browser, new Vector2(0.008f, 0.874f), new Vector2(0.992f, 0.987f), Vector2.zero, Vector2.zero);

        RectTransform addressBar = GetOrCreateRect(browser, "AddressBar");
        SetRect(addressBar, new Vector2(0.105f, 0.12f), new Vector2(0.89f, 0.52f), Vector2.zero, Vector2.zero);
        Image addressImage = GetOrAdd<Image>(addressBar.gameObject);
        addressImage.color = new Color32(8, 13, 24, 235);
        ApplyOutline(addressBar.gameObject, new Color32(44, 59, 82, 255), new Vector2(2f, -2f));

        TMP_Text address = GetOrCreateText(addressBar, "AddressText", 15f, TextAlignmentOptions.MidlineLeft, true);
        address.text = "https://fxmarket.local/store/전체-상품";
        address.color = MutedText;
        SetRect(address.rectTransform, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-12f, 0f));

        Button close = FindCloseButton();
        if (close != null)
        {
            close.transform.SetParent(browser, false);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.948f, 0.55f), new Vector2(0.986f, 0.94f), Vector2.zero, Vector2.zero);
            Image image = close.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color32(20, 29, 51, 235);
            ApplyOutline(close.gameObject, BorderColor, new Vector2(2f, -2f));
            TMP_Text label = close.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "X";
                label.color = BodyText;
                ApplyTextStyle(label, 19f, TextAlignmentOptions.Center, true);
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }
    }

    private void BuildHeader()
    {
        RectTransform header = GetOrCreateRect(modal, "Header");
        SetRect(header, new Vector2(0.018f, 0.755f), new Vector2(0.982f, 0.87f), Vector2.zero, Vector2.zero);

        RectTransform brand = GetOrCreateRect(header, "BrandPlate");
        SetRect(brand, new Vector2(0.012f, 0.12f), new Vector2(0.25f, 0.88f), Vector2.zero, Vector2.zero);
        Image brandImage = GetOrAdd<Image>(brand.gameObject);
        brandImage.color = new Color32(11, 15, 25, 242);

        TMP_Text title = GetOrCreateText(brand, "Title", 31f, TextAlignmentOptions.MidlineLeft, true);
        title.text = "<color=#06B6D4>FX</color> 마켓";
        title.color = Color.white;
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, new Vector2(0f, 0.25f), Vector2.one, new Vector2(16f, 0f), new Vector2(-8f, 0f));

        TMP_Text brandCaption = GetOrCreateText(brand, "Caption", 12f, TextAlignmentOptions.MidlineLeft, true);
        brandCaption.text = "트레이딩 데스크 전문 상점";
        brandCaption.color = MutedText;
        brandCaption.characterSpacing = 0.8f;
        SetRect(brandCaption.rectTransform, Vector2.zero, new Vector2(1f, 0.32f), new Vector2(16f, 0f), new Vector2(-8f, 0f));

        BuildSearchField(header);

        RectTransform wallet = GetOrCreateRect(header, "WalletPlate");
        SetRect(wallet, new Vector2(0.73f, 0.14f), new Vector2(0.985f, 0.86f), Vector2.zero, Vector2.zero);
        Image walletImage = GetOrAdd<Image>(wallet.gameObject);
        walletImage.color = new Color32(11, 15, 25, 242);
        ApplyOutline(wallet.gameObject, BorderColor, new Vector2(2f, -2f));

        balanceText = GetOrCreateText(wallet, "Balance", 23f, TextAlignmentOptions.MidlineRight, true);
        balanceText.color = SpecialGold;
        SetRect(balanceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-14f, 0f));
    }

    private void BuildSearchField(RectTransform header)
    {
        RectTransform searchBox = GetOrCreateRect(header, "SearchBox");
        SetRect(searchBox, new Vector2(0.28f, 0.19f), new Vector2(0.69f, 0.81f), Vector2.zero, Vector2.zero);
        Image searchBackground = GetOrAdd<Image>(searchBox.gameObject);
        searchBackground.color = new Color32(8, 13, 24, 244);
        ApplyOutline(searchBox.gameObject, Cyan, new Vector2(2f, -2f));

        RectTransform viewport = GetOrCreateRect(searchBox, "TextViewport");
        SetRect(viewport, new Vector2(0f, 0f), new Vector2(0.82f, 1f), new Vector2(16f, 4f), new Vector2(-4f, -4f));
        GetOrAdd<RectMask2D>(viewport.gameObject);

        TMP_Text inputText = GetOrCreateText(viewport, "InputText", 17f, TextAlignmentOptions.MidlineLeft, false);
        inputText.color = BodyText;
        inputText.textWrappingMode = TextWrappingModes.NoWrap;
        SetRect(inputText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        TMP_Text placeholder = GetOrCreateText(viewport, "Placeholder", 16f, TextAlignmentOptions.MidlineLeft, false);
        placeholder.text = "아이템, 효과, 장비 검색...";
        placeholder.color = MutedText;
        placeholder.fontStyle = FontStyles.Italic;
        SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        searchInput = GetOrAdd<TMP_InputField>(searchBox.gameObject);
        searchInput.targetGraphic = searchBackground;
        searchInput.textViewport = viewport;
        searchInput.textComponent = inputText;
        searchInput.placeholder = placeholder;
        searchInput.lineType = TMP_InputField.LineType.SingleLine;
        searchInput.contentType = TMP_InputField.ContentType.Standard;
        searchInput.characterLimit = 40;
        searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
        searchInput.onValueChanged.AddListener(HandleSearchChanged);

        TMP_Text searchLabel = GetOrCreateText(searchBox, "SearchLabel", 14f, TextAlignmentOptions.Center, true);
        searchLabel.text = "검색";
        searchLabel.color = Cyan;
        searchLabel.fontStyle = FontStyles.Bold;
        SetRect(searchLabel.rectTransform, new Vector2(0.82f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
    }

    private void BuildCategoryBar()
    {
        RectTransform bar = GetOrCreateRect(modal, "CategoryBar");
        SetRect(bar, new Vector2(0.018f, 0.69f), new Vector2(0.982f, 0.75f), Vector2.zero, Vector2.zero);
        Image background = GetOrAdd<Image>(bar.gameObject);
        background.color = new Color32(20, 29, 51, 238);

        CreateCategoryButton(bar, 0, "전체 상품", CategoryFilter.All, new Vector2(0.012f, 0.12f), new Vector2(0.13f, 0.88f));
        CreateCategoryButton(bar, 1, "회복", CategoryFilter.Care, new Vector2(0.138f, 0.12f), new Vector2(0.235f, 0.88f));
        CreateCategoryButton(bar, 2, "배달 음식", CategoryFilter.DeliveryFood, new Vector2(0.243f, 0.12f), new Vector2(0.39f, 0.88f));
        CreateCategoryButton(bar, 3, "장비", CategoryFilter.ActiveGear, new Vector2(0.398f, 0.12f), new Vector2(0.53f, 0.88f));
        CreateCategoryButton(bar, 4, "의상", CategoryFilter.Apparel, new Vector2(0.538f, 0.12f), new Vector2(0.64f, 0.88f));

        resultCountText = GetOrCreateText(bar, "ResultCount", 15f, TextAlignmentOptions.MidlineLeft, true);
        resultCountText.text = "상품 0개";
        resultCountText.color = MutedText;
        resultCountText.characterSpacing = 0.8f;
        SetRect(resultCountText.rectTransform, new Vector2(0.66f, 0f), new Vector2(0.76f, 1f), Vector2.zero, Vector2.zero);

        TMP_Text delivery = GetOrCreateText(bar, "Delivery", 16f, TextAlignmentOptions.MidlineRight, true);
        delivery.text = "즉시 배송  /  구매 효과 즉시 적용";
        delivery.color = SpecialGold;
        SetRect(delivery.rectTransform, new Vector2(0.66f, 0f), Vector2.one, Vector2.zero, new Vector2(-18f, 0f));

        RefreshCategoryButtonStyles();
    }

    private void BuildScrollArea()
    {
        RectTransform viewport = GetOrCreateRect(modal, "ProductViewport");
        SetRect(viewport, new Vector2(0.018f, 0.085f), new Vector2(0.982f, 0.68f), Vector2.zero, Vector2.zero);
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
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 38f;

        emptyStateText = GetOrCreateText(viewport, "EmptyState", 24f, TextAlignmentOptions.Center, true);
        emptyStateText.text = "상품을 찾을 수 없습니다\n<size=65%><color=#66758F>다른 검색어나 카테고리를 선택해 보세요</color></size>";
        emptyStateText.color = BodyText;
        emptyStateText.textWrappingMode = TextWrappingModes.Normal;
        SetRect(emptyStateText.rectTransform, new Vector2(0.25f, 0.34f), new Vector2(0.75f, 0.66f), Vector2.zero, Vector2.zero);
        emptyStateText.gameObject.SetActive(false);
    }

    private void BuildFooter()
    {
        RectTransform footerBackground = GetOrCreateRect(modal, "FooterBackground");
        SetRect(footerBackground, new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.072f), Vector2.zero, Vector2.zero);
        Image bg = GetOrAdd<Image>(footerBackground.gameObject);
        bg.color = new Color32(20, 29, 51, 242);
        ApplyOutline(footerBackground.gameObject, BorderColor, new Vector2(2f, -2f));

        TMP_Text footer = GetOrCreateText(footerBackground, "Footer", 15f, TextAlignmentOptions.MidlineLeft, true);
        footer.text = "안전 결제  /  구매 즉시 적용  /  신중하게 거래하세요";
        footer.color = BodyText;
        SetRect(footer.rectTransform, Vector2.zero, new Vector2(0.78f, 1f), new Vector2(18f, 0f), Vector2.zero);

        TMP_Text status = GetOrCreateText(footerBackground, "StoreStatus", 15f, TextAlignmentOptions.MidlineRight, true);
        status.text = "●  상점 운영 중";
        status.color = SuccessGreen;
        SetRect(status.rectTransform, new Vector2(0.78f, 0f), Vector2.one, Vector2.zero, new Vector2(-18f, 0f));
    }

    private GameObject CreateProductCard(ItemData item)
    {
        GameObject card = new($"ShopCard_{item.ItemId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        card.transform.SetParent(content, false);
        Image frame = card.GetComponent<Image>();
        frame.sprite = ResolveSprite(productCardSprite, ProductCardResource) ?? cardFrameSprite;
        frame.type = frame.sprite != null && frame.sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        frame.preserveAspect = false;
        frame.color = frame.sprite != null ? Color.white : DeepBackground;

        Outline cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = BorderColor;
        cardOutline.effectDistance = UIStrokeStyle.EffectDistance;
        cardOutline.useGraphicAlpha = true;
        cardOutline.enabled = frame.sprite == null;

        bool isActive = item.IsActiveItem;
        Color accent = item.IsDeliveryFood ? DeliveryOrange : isActive ? SpecialGold : Cyan;

        RectTransform topAccent = GetOrCreateRect(card.transform, "CategoryAccent");
        SetRect(topAccent, new Vector2(0.045f, 0.952f), new Vector2(0.955f, 0.967f), Vector2.zero, Vector2.zero);
        GetOrAdd<Image>(topAccent.gameObject).color = accent;

        GameObject badgeObject = new("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeObject.transform.SetParent(card.transform, false);
        Image badgeBackground = badgeObject.GetComponent<Image>();
        badgeBackground.sprite = null;
        badgeBackground.color = HeaderBackground;
        ApplyOutline(badgeObject, accent, new Vector2(2f, -2f));
        SetRect(badgeObject.GetComponent<RectTransform>(), new Vector2(0.055f, 0.84f), new Vector2(0.36f, 0.935f), Vector2.zero, Vector2.zero);
        TMP_Text badge = CreateText(badgeObject.transform, "Label", 14f, TextAlignmentOptions.Center);
        badge.text = item.IsDeliveryFood ? "배달 음식" : isActive ? "활성 장비" : "회복 아이템";
        badge.color = accent;
        badge.fontStyle = FontStyles.Bold;
        SetRect(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        Image icon = CreateImage(card.transform, "Icon");
        icon.sprite = item.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetRect(icon.rectTransform, new Vector2(0.065f, 0.15f), new Vector2(0.46f, 0.81f), new Vector2(10f, 8f), new Vector2(-10f, -8f));

        TMP_Text name = CreateText(card.transform, "Name", 23f, TextAlignmentOptions.MidlineLeft);
        name.text = item.ItemName.ToUpperInvariant();
        name.color = Color.white;
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0.52f, 0.75f), new Vector2(0.95f, 0.91f), Vector2.zero, Vector2.zero);

        TMP_Text effect = CreateText(card.transform, "Effect", 18f, TextAlignmentOptions.MidlineLeft);
        string effectString = "";
        switch (item.Type)
        {
            case ItemData.EffectType.Health: effectString = $"체력 +{item.EffectAmount:0}"; break;
            case ItemData.EffectType.Mental: effectString = $"멘탈 +{item.EffectAmount:0}"; break;
            case ItemData.EffectType.ProfitBoost: effectString = $"수익 +{item.EffectAmount:0}% (강화)"; break;
            case ItemData.EffectType.LossReduction: effectString = $"손실 -{item.EffectAmount:0}% (강화)"; break;
            case ItemData.EffectType.MentalDrainGuard: effectString = $"멘탈 감소 -{item.EffectAmount:0}%"; break;
            case ItemData.EffectType.HealthDrainGuard: effectString = $"체력 감소 -{item.EffectAmount:0}%"; break;
            case ItemData.EffectType.DeliveryFood: effectString = GetDeliveryFoodEffect(item.ItemId); break;
            default: effectString = $"효과 +{item.EffectAmount:0}"; break;
        }
        effect.text = effectString;
        effect.color = accent;
        effect.fontStyle = FontStyles.Bold;
        if (item.IsDeliveryFood)
        {
            effect.enableAutoSizing = true;
            effect.fontSizeMin = 15f;
            effect.fontSizeMax = 19f;
            effect.textWrappingMode = TextWrappingModes.Normal;
            effect.overflowMode = TextOverflowModes.Truncate;
            effect.maxVisibleLines = 2;
            effect.lineSpacing = -4f;
            effect.margin = new Vector4(0f, 0f, 10f, 0f);
            SetRect(effect.rectTransform, new Vector2(0.52f, 0.57f), new Vector2(0.93f, 0.74f), Vector2.zero, new Vector2(-4f, 0f));
        }
        else
        {
            SetRect(effect.rectTransform, new Vector2(0.52f, 0.59f), new Vector2(0.95f, 0.73f), Vector2.zero, Vector2.zero);
        }

        TMP_Text description = CreateText(card.transform, "Description", 17f, TextAlignmentOptions.TopLeft);
        description.text = item.Description;
        description.color = BodyText;
        description.enableAutoSizing = true;
        description.fontSizeMin = 14f;
        description.fontSizeMax = 19f;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Truncate;
        description.maxVisibleLines = 3;
        description.lineSpacing = 2f;
        description.margin = new Vector4(0f, 1f, 8f, 1f);
        SetRect(description.rectTransform, new Vector2(0.52f, 0.37f), new Vector2(0.94f, 0.58f), Vector2.zero, new Vector2(-4f, 0f));

        TMP_Text owned = CreateText(card.transform, "Owned", 15f, TextAlignmentOptions.MidlineRight);
        owned.color = MutedText;
        // 우측 정렬 글리프가 카드 끝에 붙어 잘려 보이지 않도록 안전 여백을 둡니다.
        SetRect(owned.rectTransform, new Vector2(0.52f, 0.28f), new Vector2(0.95f, 0.37f), Vector2.zero, new Vector2(-14f, 0f));

        RectTransform purchaseBar = GetOrCreateRect(card.transform, "PurchaseBar");
        SetRect(purchaseBar, new Vector2(0.52f, 0.07f), new Vector2(0.95f, 0.27f), Vector2.zero, Vector2.zero);
        Image purchaseBackground = GetOrAdd<Image>(purchaseBar.gameObject);
        purchaseBackground.color = new Color32(11, 15, 25, 245);
        ApplyOutline(purchaseBar.gameObject, BorderColor, new Vector2(2f, -2f));

        TMP_Text price = CreateText(purchaseBar, "Price", 21f, TextAlignmentOptions.MidlineLeft);
        price.text = $"$ {item.Price:N0}";
        price.color = SpecialGold;
        price.fontStyle = FontStyles.Bold;
        SetRect(price.rectTransform, Vector2.zero, new Vector2(0.53f, 1f), new Vector2(12f, 0f), new Vector2(-4f, 0f));

        GameObject buyObject = new("BuyButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(ShopItemButton));
        buyObject.transform.SetParent(purchaseBar, false);
        SetRect(buyObject.GetComponent<RectTransform>(), new Vector2(0.55f, 0.10f), new Vector2(0.975f, 0.90f), Vector2.zero, Vector2.zero);
        Image buyImage = buyObject.GetComponent<Image>();
        buyImage.sprite = null;
        buyImage.color = isActive ? new Color32(126, 87, 8, 255) : new Color32(7, 104, 126, 255);
        Outline buyOutline = buyObject.GetComponent<Outline>();
        buyOutline.enabled = true;
        buyOutline.effectColor = accent;
        buyOutline.effectDistance = new Vector2(2f, -2f);
        buyOutline.useGraphicAlpha = true;
        Button buy = buyObject.GetComponent<Button>();
        buy.targetGraphic = buyImage;
        ColorBlock buyColors = buy.colors;
        buyColors.normalColor = Color.white;
        buyColors.highlightedColor = new Color32(220, 250, 255, 255);
        buyColors.pressedColor = new Color32(155, 202, 212, 255);
        buyColors.selectedColor = Color.white;
        buyColors.disabledColor = new Color32(84, 96, 116, 210);
        buyColors.fadeDuration = 0.08f;
        buy.colors = buyColors;

        TMP_Text buyLabel = CreateText(buyObject.transform, "Label", 21f, TextAlignmentOptions.Center);
        buyLabel.text = "구매";
        buyLabel.color = Color.white;
        buyLabel.fontStyle = FontStyles.Bold;
        buyLabel.enableAutoSizing = true;
        buyLabel.fontSizeMin = 12f;
        buyLabel.fontSizeMax = 23f;
        buyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        buyLabel.overflowMode = TextOverflowModes.Ellipsis;
        buyLabel.margin = new Vector4(8f, 2f, 8f, 2f);
        SetRect(buyLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

        buyObject.GetComponent<ShopItemButton>().Configure(shopManager, shopManager.Inventory, item, buy, name, price, owned);
        Transform vp = modal?.Find("ProductViewport");
        ScrollRect scroll = vp != null ? vp.GetComponent<ScrollRect>() : null;
        if (scroll != null) ForwardScrollEvents(card, scroll);
        return card;
    }

    private GameObject CreateCostumeCard(CostumeManager.CostumeDefinition costume)
    {
        GameObject card = new($"CostumeCard_{costume.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        card.transform.SetParent(content, false);
        Image frame = card.GetComponent<Image>();
        frame.sprite = ResolveSprite(productCardSprite, ProductCardResource) ?? cardFrameSprite;
        frame.type = frame.sprite != null && frame.sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        frame.preserveAspect = false;
        frame.color = frame.sprite != null ? Color.white : DeepBackground;
        Outline cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = BorderColor;
        cardOutline.effectDistance = UIStrokeStyle.EffectDistance;
        cardOutline.enabled = frame.sprite == null;

        RectTransform accent = GetOrCreateRect(card.transform, "CategoryAccent");
        SetRect(accent, new Vector2(0.045f, 0.952f), new Vector2(0.955f, 0.967f), Vector2.zero, Vector2.zero);
        GetOrAdd<Image>(accent.gameObject).color = ApparelMagenta;

        RectTransform badgeRect = GetOrCreateRect(card.transform, "Badge");
        SetRect(badgeRect, new Vector2(0.055f, 0.84f), new Vector2(0.36f, 0.935f), Vector2.zero, Vector2.zero);
        GetOrAdd<Image>(badgeRect.gameObject).color = HeaderBackground;
        ApplyOutline(badgeRect.gameObject, ApparelMagenta, new Vector2(2f, -2f));
        TMP_Text badge = GetOrCreateText(badgeRect, "Label", 14f, TextAlignmentOptions.Center, true);
        badge.text = "의상";
        badge.color = ApparelMagenta;
        badge.fontStyle = FontStyles.Bold;
        SetRect(badge.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        Image icon = CreateImage(card.transform, "Icon");
        icon.sprite = Resources.Load<Sprite>(costume.IconResourcePath);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        SetRect(icon.rectTransform, new Vector2(0.065f, 0.13f), new Vector2(0.46f, 0.82f), new Vector2(10f, 8f), new Vector2(-10f, -8f));

        TMP_Text name = CreateText(card.transform, "Name", 23f, TextAlignmentOptions.MidlineLeft);
        name.text = costume.DisplayName;
        name.color = Color.white;
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0.52f, 0.75f), new Vector2(0.95f, 0.91f), Vector2.zero, Vector2.zero);

        TMP_Text effect = CreateText(card.transform, "Effect", 18f, TextAlignmentOptions.MidlineLeft);
        effect.text = CostumeManager.Instance != null && CostumeManager.Instance.IsEquipped(costume.Id)
            ? "현재 착용 중"
            : "요미 외형 변경";
        if (costume.Id == CostumeManager.BunnyGirlId) effect.text = "자동매매 수익 +15%";
        else if (costume.Id == CostumeManager.BikiniId) effect.text = "수동매매 수익 +15%";
        else if (costume.Id == CostumeManager.JiraiKeiId) effect.text = "전체 수익 +20% (고위험)";
        else if (costume.Id == CostumeManager.StreetCapId) effect.text = "최대 체력 +30";
        if (CostumeManager.Instance != null && CostumeManager.Instance.IsEquipped(costume.Id)) effect.text += " (착용 중)";
        
        effect.color = ApparelMagenta;
        effect.fontStyle = FontStyles.Bold;
        SetRect(effect.rectTransform, new Vector2(0.52f, 0.59f), new Vector2(0.95f, 0.73f), Vector2.zero, Vector2.zero);

        TMP_Text description = CreateText(card.transform, "Description", 19f, TextAlignmentOptions.TopLeft);
        description.text = costume.Description;
        description.color = BodyText;
        description.enableAutoSizing = true;
        description.fontSizeMin = 16f;
        description.fontSizeMax = 22f;
        description.textWrappingMode = TextWrappingModes.Normal;
        description.overflowMode = TextOverflowModes.Truncate;
        description.maxVisibleLines = 6;
        description.lineSpacing = 2f;
        description.margin = new Vector4(0f, 1f, 8f, 1f);
        SetRect(description.rectTransform, new Vector2(0.52f, 0.33f), new Vector2(0.94f, 0.58f), Vector2.zero, new Vector2(-4f, 0f));

        bool owned = CostumeManager.Instance != null && CostumeManager.Instance.IsOwned(costume.Id);
        bool equipped = CostumeManager.Instance != null && CostumeManager.Instance.IsEquipped(costume.Id);
        string requirementText = string.Empty;
        bool isUnlocked = FXOverdose.Core.AchievementManager.Instance == null || FXOverdose.Core.AchievementManager.Instance.IsCostumeUnlocked(costume.Id, out requirementText);
        
        TMP_Text ownedText = CreateText(card.transform, "Owned", 15f, TextAlignmentOptions.MidlineRight);
        ownedText.text = !isUnlocked ? "잠김" : (equipped ? "착용 중 ✓" : owned ? "보유 중 ✓" : "미보유");
        ownedText.color = !isUnlocked ? new Color32(239, 68, 68, 255) : (equipped ? SuccessGreen : MutedText);
        SetRect(ownedText.rectTransform, new Vector2(0.52f, 0.28f), new Vector2(0.95f, 0.37f), Vector2.zero, new Vector2(-14f, 0f));

        RectTransform purchaseBar = GetOrCreateRect(card.transform, "PurchaseBar");
        SetRect(purchaseBar, new Vector2(0.52f, 0.07f), new Vector2(0.95f, 0.27f), Vector2.zero, Vector2.zero);
        GetOrAdd<Image>(purchaseBar.gameObject).color = new Color32(11, 15, 25, 245);
        ApplyOutline(purchaseBar.gameObject, BorderColor, new Vector2(2f, -2f));

        TMP_Text price = CreateText(purchaseBar, "Price", 21f, TextAlignmentOptions.MidlineLeft);
        price.text = !isUnlocked ? $"필요 업적: {requirementText}" : (owned ? "보유 중" : costume.Price <= 0 ? "무료" : $"${costume.Price:N0}");
        price.color = !isUnlocked ? new Color32(239, 68, 68, 255) : (owned ? SuccessGreen : SpecialGold);
        if (!isUnlocked)
        {
            price.enableAutoSizing = true;
            price.fontSizeMin = 10f;
            price.fontSizeMax = 27f;
            price.textWrappingMode = TextWrappingModes.Normal;
            price.overflowMode = TextOverflowModes.Ellipsis;
        }
        price.fontStyle = FontStyles.Bold;
        SetRect(price.rectTransform, Vector2.zero, new Vector2(0.53f, 1f), new Vector2(12f, 0f), new Vector2(-4f, 0f));

        GameObject actionObject = new("CostumeActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
        actionObject.transform.SetParent(purchaseBar, false);
        SetRect(actionObject.GetComponent<RectTransform>(), new Vector2(0.55f, 0.10f), new Vector2(0.975f, 0.90f), Vector2.zero, Vector2.zero);
        Image actionImage = actionObject.GetComponent<Image>();
        actionImage.color = !isUnlocked ? new Color32(84, 96, 116, 255) : (equipped ? new Color32(22, 101, 52, 255) : new Color32(112, 26, 117, 255));
        ApplyOutline(actionObject, !isUnlocked ? BorderColor : (equipped ? SuccessGreen : ApparelMagenta), new Vector2(2f, -2f));
        Button action = actionObject.GetComponent<Button>();
        action.targetGraphic = actionImage;
        action.interactable = isUnlocked && !equipped;
        ColorBlock colors = action.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(255, 225, 255, 255);
        colors.pressedColor = new Color32(211, 160, 216, 255);
        colors.disabledColor = new Color32(84, 96, 116, 210);
        action.colors = colors;

        TMP_Text label = CreateText(actionObject.transform, "Label", 21f, TextAlignmentOptions.Center);
        label.text = !isUnlocked ? "잠김" : (equipped ? "착용 중" : owned ? "장착" : "구매");
        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 23f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.margin = new Vector4(8f, 2f, 8f, 2f);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

        action.onClick.AddListener(() =>
        {
            if (shopManager == null) return;
            if (CostumeManager.Instance != null && CostumeManager.Instance.IsOwned(costume.Id))
                shopManager.EquipCostume(costume.Id);
            else
                shopManager.BuyCostume(costume.Id);
            Rebuild();
            RefreshBalance();
        });

        Transform vp = modal?.Find("ProductViewport");
        ScrollRect scroll = vp != null ? vp.GetComponent<ScrollRect>() : null;
        if (scroll != null) ForwardScrollEvents(card, scroll);
        return card;
    }

    private void LayoutCards()
    {
        int count = cards.Count;
        int columns = Mathf.Max(1, maxColumns);
        Canvas.ForceUpdateCanvases();
        RectTransform viewport = content.parent as RectTransform;
        float width = viewport != null && viewport.rect.width > 0f
            ? viewport.rect.width
            : (content.rect.width > 0f ? content.rect.width : 900f);
        float spacing = 20f;
        float sidePadding = 18f;
        float cardWidth = (width - sidePadding * 2f - spacing * (columns - 1)) / columns;
        float cardHeight = Mathf.Clamp(cardWidth * 0.667f, 310f, 360f);
        int rows = count > 0 ? Mathf.CeilToInt(count / (float)columns) : 0;
        content.sizeDelta = new Vector2(0f, rows > 0 ? rows * cardHeight + (rows - 1) * spacing + 12f : 0f);

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

    private void CreateCategoryButton(
        RectTransform parent,
        int index,
        string label,
        CategoryFilter filter,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        RectTransform rect = GetOrCreateRect(parent, $"Category_{filter}");
        SetRect(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        Image background = GetOrAdd<Image>(rect.gameObject);
        Button button = GetOrAdd<Button>(rect.gameObject);
        button.targetGraphic = background;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => SetCategoryFilter(filter));

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color32(220, 250, 255, 255);
        colors.pressedColor = new Color32(160, 211, 222, 255);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TMP_Text text = GetOrCreateText(rect, "Label", 16f, TextAlignmentOptions.Center, true);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));

        categoryButtons[index] = button;
        categoryButtonBackgrounds[index] = background;
        ApplyOutline(rect.gameObject, BorderColor, new Vector2(2f, -2f));
    }

    private void SetCategoryFilter(CategoryFilter filter)
    {
        if (activeFilter == filter)
        {
            return;
        }

        activeFilter = filter;
        RefreshCategoryButtonStyles();
        Rebuild();
    }

    private void RefreshCategoryButtonStyles()
    {
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            Button button = categoryButtons[i];
            Image background = categoryButtonBackgrounds[i];
            if (button == null || background == null)
            {
                continue;
            }

            CategoryFilter filter = (CategoryFilter)i;
            bool selected = filter == activeFilter;
            Color accent = filter == CategoryFilter.ActiveGear
                ? SpecialGold
                : filter == CategoryFilter.Apparel
                    ? ApparelMagenta
                    : filter == CategoryFilter.DeliveryFood ? DeliveryOrange : Cyan;
            background.color = selected
                ? Color.Lerp(HeaderBackground, accent, 0.28f)
                : new Color32(11, 15, 25, 238);

            Outline outline = background.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected ? accent : BorderColor;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = selected ? accent : BodyText;
            }
        }
    }

    private void HandleSearchChanged(string value)
    {
        searchQuery = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        Rebuild();
    }

    private bool ShouldShowItem(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (activeFilter == CategoryFilter.Care && (item.IsActiveItem || item.IsDeliveryFood))
        {
            return false;
        }
        if (activeFilter == CategoryFilter.DeliveryFood && !item.IsDeliveryFood)
        {
            return false;
        }
        if (activeFilter == CategoryFilter.ActiveGear && !item.IsActiveItem)
        {
            return false;
        }
        if (activeFilter == CategoryFilter.Apparel)
        {
            return false;
        }

        if (string.IsNullOrEmpty(searchQuery))
        {
            return true;
        }

        return ContainsIgnoreCase(item.ItemName, searchQuery)
            || ContainsIgnoreCase(item.ItemId, searchQuery)
            || ContainsIgnoreCase(item.Description, searchQuery);
    }

    private static string GetDeliveryFoodEffect(string itemId)
    {
        return itemId switch
        {
            "malatang" => "체력 +25 / 멘탈 +50",
            "sushi" => "체력 +30 / 멘탈 +55",
            "tteokbokki" => "체력 +30 / 멘탈 +60",
            "pasta" => "체력 +15 / 멘탈 +15\n시간 ×1.5",
            "steak" => "체력·멘탈 완전 회복\n최대 멘탈 +10",
            _ => "배달 음식"
        };
    }

    private bool ShouldShowCostume(CostumeManager.CostumeDefinition costume)
    {
        if (costume == null) return false;
        if (string.IsNullOrEmpty(searchQuery)) return true;
        return ContainsIgnoreCase(costume.DisplayName, searchQuery)
            || ContainsIgnoreCase(costume.Id, searchQuery)
            || ContainsIgnoreCase(costume.Description, searchQuery);
    }

    private void HandleCostumesChanged()
    {
        if (isActiveAndEnabled) Rebuild();
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RefreshBalance()
    {
        if (balanceText != null && shopManager?.GameManager != null)
        {
            float cur = shopManager.GameManager.CurrentBalance;
            if (Mathf.Abs(cur - lastDisplayedBalance) > 0.01f)
            {
                lastDisplayedBalance = cur;
                balanceText.text = $"보유 자산  ${cur:N0}";
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
        if (found == null && modal != null) found = modal.Find("BrowserChrome/CloseButton");
        if (found == null)
        {
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.name == "CloseButton")
                {
                    found = button.transform;
                    break;
                }
            }
        }
        return found != null ? found.GetComponent<Button>() : null;
    }

    private void ClearCards()
    {
        foreach (GameObject card in cards)
        {
            if (card == null) continue;
            card.SetActive(false);
            Destroy(card);
        }
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
        float scaledSize = size * ShopFontScale;
        text.fontSize = scaledSize;
        text.enableAutoSizing = autoSize;
        if (autoSize)
        {
            text.fontSizeMin = Mathf.Max(11f, scaledSize - 6f);
            text.fontSizeMax = scaledSize;
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

    private static Outline ApplyOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = GetOrAdd<Outline>(target);
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
        return outline;
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
