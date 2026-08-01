using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>인벤토리를 네 칸 단위의 가로 페이지로 표시합니다.</summary>
[RequireComponent(typeof(RectTransform))]
public class DynamicInventoryUI : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IPointerClickHandler, IScrollHandler
{
    private const int SlotsPerPage = 4;

    [Header("데이터 및 이미지")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Sprite slotFrameSprite;
    [SerializeField] private TMP_FontAsset font;

    [Header("4칸 가로 페이지")]
    [SerializeField] private float preferredSlotSize = 96f;
    [SerializeField] private float spacing = 8f;
    [SerializeField] private float horizontalPadding = 14f;
    [SerializeField] private float topPadding = 44f;
    [SerializeField] private float bottomPadding = 12f;
    [SerializeField] private float pageBarHeight = 18f;
    [SerializeField] private float swipeThreshold = 36f;
    [SerializeField] private float pageSnapDuration = 0.16f;

    private readonly List<GameObject> generatedSlots = new();
    private RectTransform panelRect;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private Scrollbar pageScrollbar;
    private TMP_Text pageText;
    private TMP_Text titleText;
    private TMP_Text activeBuffsText;
    private int lastSlotCount = -1;
    private int currentPage;
    private int pageCount = 1;
    private int dragStartPage;
    private Vector2 dragStartPosition;
    private Coroutine pageSnapRoutine;
    private GameObject dragGhost;
    private GameObject draggedSlot;
    private int draggedSlotIndex = -1;
    private int highlightedDropIndex = -1;
    private float nextEdgePageTime;

    // 바깥 배경은 투명도 5%, 겹치는 내부 카드와 헤더 면은 불투명하게 유지합니다.
    private static readonly Color PanelBackground = new(0.025f, 0.045f, 0.085f, 0.95f);
    private static readonly Color InnerSurface = new(0.045f, 0.075f, 0.13f, 1f);
    private static readonly Color HeaderSurface = new(0.075f, 0.12f, 0.20f, 1f);
    private static readonly Color BorderColor = new(0.22f, 0.34f, 0.48f, 1f);
    private static readonly Color DividerColor = new(0.15f, 0.25f, 0.36f, 1f);
    private static readonly Color AccentColor = new(0.02f, 0.72f, 0.84f, 1f);

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();
        AlignToScreenEdge();
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
        CreateBackgroundDesign();
        CreateTitleIfNeeded();
        CreateScrollStructureIfNeeded();
        HideLegacyButtons();
        Rebuild();
    }

    private void AlignToScreenEdge()
    {
        if (panelRect == null) return;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(
            -UIStrokeStyle.ScreenEdgeMargin,
            panelRect.anchoredPosition.y);
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.QuantityChanged += OnInventoryChanged;
            inventory.OrderChanged += OnInventoryOrderChanged;
        }
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshActiveBuffsText;
            ActiveItemEffectManager.Instance.OnActiveItemsChanged += RefreshActiveBuffsText;
        }
        RefreshActiveBuffsText();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.QuantityChanged -= OnInventoryChanged;
            inventory.OrderChanged -= OnInventoryOrderChanged;
        }
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshActiveBuffsText;
        }
    }

    private void RefreshActiveBuffsText()
    {
        if (activeBuffsText == null) CreateTitleIfNeeded();
        if (activeBuffsText != null && ActiveItemEffectManager.Instance != null)
        {
            activeBuffsText.text = ActiveItemEffectManager.Instance.GetSummaryText();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && generatedSlots.Count > 0) LayoutSlots();
    }

    private void OnInventoryChanged(ItemData item, int quantity)
    {
        if (inventory != null && (inventory.Slots.Count != lastSlotCount || quantity <= 0))
        {
            Rebuild();
            return;
        }

        // 기존 슬롯의 수량만 바뀐 경우에도 같은 프레임 안의 숫자를 즉시 갱신합니다.
        foreach (GameObject slot in generatedSlots)
        {
            if (slot == null) continue;
            InventoryItemButton button = slot.GetComponent<InventoryItemButton>();
            if (button != null) button.RefreshDisplay();
            TMP_Text[] texts = slot.GetComponentsInChildren<TMP_Text>(true);
            if (texts != null)
            {
                foreach (var t in texts) if (t != null) t.ForceMeshUpdate();
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    private void OnInventoryOrderChanged() => Rebuild();

    [ContextMenu("Rebuild Inventory UI")]
    public void Rebuild()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        ClearGeneratedSlots();
        if (inventory == null) return;

        foreach (InventorySlot slot in inventory.Slots)
        {
            if (slot?.Item == null || slot.Quantity <= 0) continue;
            generatedSlots.Add(CreateSlot(slot.Item));
        }

        lastSlotCount = generatedSlots.Count;
        int requiredSlots = Mathf.Max(SlotsPerPage, Mathf.CeilToInt(generatedSlots.Count / (float)SlotsPerPage) * SlotsPerPage);
        while (generatedSlots.Count < requiredSlots)
        {
            generatedSlots.Add(CreateEmptySlot(generatedSlots.Count));
        }
        LayoutSlots();
    }

    private GameObject CreateEmptySlot(int index)
    {
        GameObject slot = new($"DynamicEmptySlot_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        slot.transform.SetParent(contentRect != null ? contentRect : transform, false);

        Image frame = slot.GetComponent<Image>();
        frame.sprite = slotFrameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color(1f, 1f, 1f, 0.32f);
        frame.raycastTarget = false;

        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.45f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;
        return slot;
    }

    private GameObject CreateSlot(ItemData item)
    {
        GameObject slot = new($"DynamicSlot_{item.ItemId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(InventoryItemButton));
        slot.transform.SetParent(contentRect != null ? contentRect : transform, false);

        Image frame = slot.GetComponent<Image>();
        frame.sprite = slotFrameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = Color.white;

        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = UIStrokeStyle.DefaultColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Button button = slot.GetComponent<Button>();
        button.targetGraphic = frame;

        Image icon = CreateImage(slot.transform, "ItemIcon");
        // 슬롯 프레임 정중앙을 기준으로 상하좌우 여백을 동일하게 둡니다.
        // 기존 Y 0.19~0.91 영역은 중심이 위로 치우쳐 에너지 드링크 아래 여백이 더 크게 보였습니다.
        SetAnchors(icon.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text quantity = CreateText(slot.transform, "Quantity", 18f, TextAlignmentOptions.BottomRight);
        SetAnchors(quantity.rectTransform, new Vector2(0.43f, 0.02f), new Vector2(0.94f, 0.34f), Vector2.zero, Vector2.zero);
        quantity.fontStyle = FontStyles.Bold;

        InventoryItemButton itemButton = slot.GetComponent<InventoryItemButton>();
        itemButton.Configure(inventory, item, button, icon, null, quantity);
        ItemTooltipTrigger tooltip = slot.AddComponent<ItemTooltipTrigger>();
        tooltip.Configure(item);
        InventorySlotDragHandle dragHandle = slot.AddComponent<InventorySlotDragHandle>();
        dragHandle.Configure(this, generatedSlots.Count);
        return slot;
    }

    public bool BeginSlotInteraction(int slotIndex, PointerEventData eventData, bool reorder)
    {
        if (!reorder || slotIndex < 0 || slotIndex >= lastSlotCount) return false;
        StopPageSnap();
        if (scrollRect != null) scrollRect.StopMovement();

        draggedSlotIndex = slotIndex;
        draggedSlot = generatedSlots[slotIndex];
        CanvasGroup canvasGroup = draggedSlot.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = draggedSlot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.28f;

        dragGhost = new GameObject("InventoryDragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dragGhost.transform.SetParent(transform, false);
        RectTransform ghostRect = dragGhost.GetComponent<RectTransform>();
        // ScreenPointToLocalPointInRectangle 결과는 패널 피벗 기준 좌표이므로
        // 고스트의 앵커도 같은 피벗에 맞춰야 커서/손가락 중심을 정확히 따라갑니다.
        ghostRect.anchorMin = panelRect.pivot;
        ghostRect.anchorMax = panelRect.pivot;
        ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = new Vector2(preferredSlotSize, preferredSlotSize);
        Image source = draggedSlot.GetComponent<Image>();
        Image ghostImage = dragGhost.GetComponent<Image>();
        ghostImage.sprite = source.sprite;
        ghostImage.type = source.type;
        ghostImage.color = new Color(1f, 1f, 1f, 0.92f);
        ghostImage.raycastTarget = false;
        Outline ghostOutline = dragGhost.GetComponent<Outline>();
        ghostOutline.effectColor = AccentColor;
        ghostOutline.effectDistance = new Vector2(3f, -3f);

        Image sourceIcon = draggedSlot.transform.Find("ItemIcon")?.GetComponent<Image>();
        if (sourceIcon != null)
        {
            Image ghostIcon = CreateImage(dragGhost.transform, "GhostIcon");
            ghostIcon.sprite = sourceIcon.sprite;
            ghostIcon.preserveAspect = true;
            ghostIcon.raycastTarget = false;
            SetAnchors(ghostIcon.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
        }
        dragGhost.transform.SetAsLastSibling();
        UpdateSlotDrag(eventData);
        return true;
    }

    public void UpdateSlotDrag(PointerEventData eventData)
    {
        if (draggedSlotIndex < 0 || dragGhost == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            dragGhost.GetComponent<RectTransform>().anchoredPosition = local;

        if (Time.unscaledTime >= nextEdgePageTime && viewportRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, eventData.position, eventData.pressEventCamera, out Vector2 viewportPoint))
        {
            float halfWidth = viewportRect.rect.width * 0.5f;
            const float edgeWidth = 34f;
            if (viewportPoint.x > halfWidth - edgeWidth && currentPage < pageCount - 1)
            {
                GoToPage(currentPage + 1, true);
                nextEdgePageTime = Time.unscaledTime + 0.38f;
            }
            else if (viewportPoint.x < -halfWidth + edgeWidth && currentPage > 0)
            {
                GoToPage(currentPage - 1, true);
                nextEdgePageTime = Time.unscaledTime + 0.38f;
            }
        }

        SetDropHighlight(GetSlotIndexAtScreenPoint(eventData.position, eventData.pressEventCamera));
    }

    public void EndSlotDrag(PointerEventData eventData)
    {
        if (draggedSlotIndex < 0) return;
        int targetIndex = GetSlotIndexAtScreenPoint(eventData.position, eventData.pressEventCamera);
        if (targetIndex >= lastSlotCount) targetIndex = lastSlotCount - 1;
        int sourceIndex = draggedSlotIndex;
        CleanupSlotDrag();
        if (targetIndex >= 0 && targetIndex != sourceIndex) inventory.SwapSlots(sourceIndex, targetIndex);
    }

    public void ForwardPageBeginDrag(PointerEventData eventData)
    {
        OnBeginDrag(eventData);
        scrollRect?.OnBeginDrag(eventData);
    }

    public void ForwardPageDrag(PointerEventData eventData) => scrollRect?.OnDrag(eventData);

    public void ForwardPageEndDrag(PointerEventData eventData)
    {
        scrollRect?.OnEndDrag(eventData);
        OnEndDrag(eventData);
    }

    private int GetSlotIndexAtScreenPoint(Vector2 screenPoint, Camera eventCamera)
    {
        if (viewportRect == null || !RectTransformUtility.RectangleContainsScreenPoint(viewportRect, screenPoint, eventCamera)) return -1;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, screenPoint, eventCamera, out Vector2 local)) return -1;
        float xFromLeft = local.x + viewportRect.rect.width * viewportRect.pivot.x;
        int column = Mathf.Clamp(Mathf.FloorToInt(xFromLeft / (preferredSlotSize + spacing)), 0, SlotsPerPage - 1);
        return currentPage * SlotsPerPage + column;
    }

    private void SetDropHighlight(int index)
    {
        if (highlightedDropIndex == index) return;
        RestoreDropHighlight();
        if (index < 0 || index >= generatedSlots.Count) return;
        highlightedDropIndex = index;
        Outline outline = generatedSlots[index].GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(4f, -4f);
        }
    }

    private void RestoreDropHighlight()
    {
        if (highlightedDropIndex < 0 || highlightedDropIndex >= generatedSlots.Count)
        {
            highlightedDropIndex = -1;
            return;
        }
        Outline outline = generatedSlots[highlightedDropIndex].GetComponent<Outline>();
        if (outline != null)
        {
            bool occupied = highlightedDropIndex < lastSlotCount;
            outline.effectColor = occupied ? UIStrokeStyle.DefaultColor : new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.45f);
            outline.effectDistance = UIStrokeStyle.EffectDistance;
        }
        highlightedDropIndex = -1;
    }

    private void CleanupSlotDrag()
    {
        RestoreDropHighlight();
        if (draggedSlot != null)
        {
            CanvasGroup group = draggedSlot.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
        draggedSlot = null;
        draggedSlotIndex = -1;
    }

    private void LayoutSlots()
    {
        int count = generatedSlots.Count;
        if (contentRect == null || viewportRect == null) CreateScrollStructureIfNeeded();

        float slotSize = preferredSlotSize;
        float viewportWidth = SlotsPerPage * slotSize + (SlotsPerPage - 1) * spacing;
        float panelWidth = horizontalPadding * 2f + viewportWidth;
        float panelHeight = topPadding + slotSize + pageBarHeight + bottomPadding;
        pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)SlotsPerPage));
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        SetAnchors(viewportRect, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(horizontalPadding, -topPadding - slotSize),
            new Vector2(horizontalPadding + viewportWidth, -topPadding));
        contentRect.sizeDelta = new Vector2(viewportWidth * pageCount, slotSize);

        for (int i = 0; i < count; i++)
        {
            int page = i / SlotsPerPage;
            int column = i % SlotsPerPage;
            RectTransform rect = generatedSlots[i].GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(
                page * viewportWidth + column * (slotSize + spacing),
                0f);
        }

        LayoutPageBar(panelWidth);
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.horizontalNormalizedPosition = PageToNormalized(currentPage);
        UpdatePageIndicator();
    }

    private void CreateScrollStructureIfNeeded()
    {
        Transform existingViewport = transform.Find("InventoryViewport");
        if (existingViewport == null)
        {
            GameObject viewport = new("InventoryViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(transform, false);
            viewportRect = viewport.GetComponent<RectTransform>();
            Image hitArea = viewport.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0.001f);
            hitArea.raycastTarget = true;
        }
        else viewportRect = existingViewport.GetComponent<RectTransform>();

        Transform existingContent = viewportRect.Find("InventoryContent");
        if (existingContent == null)
        {
            GameObject content = new("InventoryContent", typeof(RectTransform));
            content.transform.SetParent(viewportRect, false);
            contentRect = content.GetComponent<RectTransform>();
        }
        else contentRect = existingContent.GetComponent<RectTransform>();

        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null) scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.12f;
        scrollRect.scrollSensitivity = 34f;

        CreatePageBarIfNeeded();
        viewportRect.SetAsLastSibling();
        if (pageScrollbar != null) pageScrollbar.transform.SetAsLastSibling();
        if (pageText != null) pageText.transform.SetAsLastSibling();
    }

    private void CreatePageBarIfNeeded()
    {
        Transform existing = transform.Find("InventoryPageBar");
        GameObject barObject;
        if (existing == null)
        {
            barObject = new GameObject("InventoryPageBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(transform, false);
        }
        else barObject = existing.gameObject;

        pageScrollbar = barObject.GetComponent<Scrollbar>();
        Image track = barObject.GetComponent<Image>();
        track.color = new Color(0.08f, 0.14f, 0.22f, 1f);

        Transform slidingArea = barObject.transform.Find("SlidingArea");
        if (slidingArea == null)
        {
            GameObject go = new("SlidingArea", typeof(RectTransform));
            go.transform.SetParent(barObject.transform, false);
            slidingArea = go.transform;
        }
        SetAnchors((RectTransform)slidingArea, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

        Transform handle = slidingArea.Find("Handle");
        if (handle == null)
        {
            GameObject go = new("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(slidingArea, false);
            handle = go.transform;
        }
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = AccentColor;
        RectTransform handleRect = (RectTransform)handle;
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;

        pageScrollbar.handleRect = handleRect;
        pageScrollbar.targetGraphic = handleImage;
        pageScrollbar.direction = Scrollbar.Direction.LeftToRight;
        pageScrollbar.numberOfSteps = 0;
        pageScrollbar.onValueChanged.RemoveListener(OnPageBarValueChanged);
        pageScrollbar.onValueChanged.AddListener(OnPageBarValueChanged);
        scrollRect.horizontalScrollbar = pageScrollbar;
        scrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        Transform existingText = transform.Find("InventoryPageText");
        pageText = existingText != null
            ? existingText.GetComponent<TMP_Text>()
            : CreateText(transform, "InventoryPageText", 13f, TextAlignmentOptions.Center);
        pageText.fontStyle = FontStyles.Bold;
        pageText.color = new Color(0.66f, 0.86f, 0.94f, 1f);
    }

    private void LayoutPageBar(float panelWidth)
    {
        if (pageScrollbar == null || pageText == null) return;
        RectTransform barRect = pageScrollbar.GetComponent<RectTransform>();
        SetAnchors(barRect, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(horizontalPadding, bottomPadding),
            new Vector2(panelWidth - horizontalPadding - 48f, bottomPadding + 8f));
        SetAnchors(pageText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(panelWidth - horizontalPadding - 44f, bottomPadding - 4f),
            new Vector2(panelWidth - horizontalPadding, bottomPadding + 14f));
        pageScrollbar.gameObject.SetActive(pageCount > 1);
        pageText.gameObject.SetActive(pageCount > 1);
        pageScrollbar.numberOfSteps = pageCount;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStartPosition = eventData.position;
        dragStartPage = currentPage;
        StopPageSnap();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float delta = eventData.position.x - dragStartPosition.x;
        int target = Mathf.RoundToInt(scrollRect.horizontalNormalizedPosition * Mathf.Max(0, pageCount - 1));
        if (Mathf.Abs(delta) >= swipeThreshold) target = dragStartPage + (delta < 0f ? 1 : -1);
        GoToPage(target, true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (pageCount <= 1 || viewportRect == null || eventData.dragging) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewportRect, eventData.position, eventData.pressEventCamera)) return;
        GameObject hit = eventData.pointerPressRaycast.gameObject;
        if (hit != null && hit.GetComponentInParent<InventoryItemButton>() != null) return;
        GoToPage(currentPage < pageCount - 1 ? currentPage + 1 : 0, true);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (pageCount <= 1 || Mathf.Abs(eventData.scrollDelta.y) < 0.01f) return;
        GoToPage(currentPage + (eventData.scrollDelta.y < 0f ? 1 : -1), true);
    }

    private void GoToPage(int page, bool animate)
    {
        currentPage = Mathf.Clamp(page, 0, pageCount - 1);
        StopPageSnap();
        if (scrollRect == null) return;
        if (!animate || !isActiveAndEnabled)
        {
            scrollRect.horizontalNormalizedPosition = PageToNormalized(currentPage);
            UpdatePageIndicator();
            return;
        }
        pageSnapRoutine = StartCoroutine(SnapToPageRoutine(PageToNormalized(currentPage)));
    }

    private IEnumerator SnapToPageRoutine(float target)
    {
        scrollRect.StopMovement();
        float start = scrollRect.horizontalNormalizedPosition;
        float elapsed = 0f;
        while (elapsed < pageSnapDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pageSnapDuration));
            scrollRect.horizontalNormalizedPosition = Mathf.Lerp(start, target, t);
            yield return null;
        }
        scrollRect.horizontalNormalizedPosition = target;
        pageSnapRoutine = null;
        UpdatePageIndicator();
    }

    private float PageToNormalized(int page) => pageCount <= 1 ? 0f : page / (float)(pageCount - 1);

    private void UpdatePageIndicator()
    {
        if (pageText != null) pageText.text = $"{currentPage + 1}/{pageCount}";
    }

    private void OnPageBarValueChanged(float value)
    {
        if (pageCount <= 1) return;
        currentPage = Mathf.Clamp(Mathf.RoundToInt(value * (pageCount - 1)), 0, pageCount - 1);
        UpdatePageIndicator();
    }

    private void StopPageSnap()
    {
        if (pageSnapRoutine == null) return;
        StopCoroutine(pageSnapRoutine);
        pageSnapRoutine = null;
    }

    private void CreateTitleIfNeeded()
    {
        Transform existing = transform.Find("CareItemsTitle");
        titleText = existing != null ? existing.GetComponent<TMP_Text>() : CreateText(transform, "CareItemsTitle", 25f, TextAlignmentOptions.MidlineLeft);
        titleText.text = "CARE ITEMS";
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.82f, 0.97f, 1f, 1f);
        titleText.characterSpacing = 1.5f;
        SetAnchors(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -44f), new Vector2(-12f, -4f));

        Transform existingBuffs = transform.Find("ActiveBuffsSummary");
        activeBuffsText = existingBuffs != null ? existingBuffs.GetComponent<TMP_Text>() : CreateText(transform, "ActiveBuffsSummary", 18f, TextAlignmentOptions.MidlineRight);
        activeBuffsText.fontStyle = FontStyles.Bold;
        activeBuffsText.color = new Color(0.4f, 0.95f, 0.6f, 1f);
        SetAnchors(activeBuffsText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -44f), new Vector2(-22f, -4f));
        if (ActiveItemEffectManager.Instance != null)
        {
            activeBuffsText.text = ActiveItemEffectManager.Instance.GetSummaryText();
        }
    }

    private void CreateBackgroundDesign()
    {
        Image background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.color = PanelBackground;

        Outline outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Image inner = CreateOrGetBackgroundLayer("InventoryInnerSurface");
        SetAnchors(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        inner.color = InnerSurface;
        inner.transform.SetSiblingIndex(0);

        Image header = CreateOrGetBackgroundLayer("InventoryHeaderSurface");
        SetAnchors(header.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(6f, -48f), new Vector2(-6f, -6f));
        header.color = HeaderSurface;
        header.transform.SetSiblingIndex(1);

        Image accent = CreateOrGetBackgroundLayer("InventoryTopAccent");
        SetAnchors(accent.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(6f, -5f), new Vector2(-6f, -2f));
        accent.color = AccentColor;
        accent.transform.SetSiblingIndex(2);

        Image divider = CreateOrGetBackgroundLayer("InventoryHeaderDivider");
        SetAnchors(divider.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -49f), new Vector2(-14f, -47f));
        divider.color = DividerColor;
        divider.transform.SetSiblingIndex(3);

    }

    private Image CreateOrGetBackgroundLayer(string objectName)
    {
        Transform existing = transform.Find(objectName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            image = go.GetComponent<Image>();
        }

        image.sprite = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private void HideLegacyButtons()
    {
        foreach (InventoryItemButton button in GetComponentsInChildren<InventoryItemButton>(true))
        {
            if (!button.name.StartsWith("DynamicSlot_")) button.gameObject.SetActive(false);
        }
    }

    private void ClearGeneratedSlots()
    {
        foreach (GameObject slot in generatedSlots)
            if (slot != null) Destroy(slot);
        generatedSlots.Clear();
    }

    private Image CreateImage(Transform parent, string objectName)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private TMP_Text CreateText(Transform parent, string objectName, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}

/// <summary>아이템 슬롯 드래그를 재배치 또는 페이지 스와이프로 전달합니다.</summary>
public class InventorySlotDragHandle : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float MobileHoldSeconds = 0.12f;

    private DynamicInventoryUI owner;
    private int slotIndex;
    private float pointerDownTime;
    private bool reordering;

    public void Configure(DynamicInventoryUI targetOwner, int targetIndex)
    {
        owner = targetOwner;
        slotIndex = targetIndex;
    }

    public void OnPointerDown(PointerEventData eventData) => pointerDownTime = Time.unscaledTime;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null) return;
        bool mouseDrag = eventData.pointerId < 0;
        bool heldOnTouch = Time.unscaledTime - pointerDownTime >= MobileHoldSeconds;
        reordering = owner.BeginSlotInteraction(slotIndex, eventData, mouseDrag || heldOnTouch);
        if (!reordering) owner.ForwardPageBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner == null) return;
        if (reordering) owner.UpdateSlotDrag(eventData);
        else owner.ForwardPageDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner == null) return;
        if (reordering) owner.EndSlotDrag(eventData);
        else owner.ForwardPageEndDrag(eventData);
        reordering = false;
    }
}

/// <summary>아이템 UI에 공용 마우스 툴팁을 연결합니다.</summary>
public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private ItemData item;

    public void Configure(ItemData targetItem) => item = targetItem;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null) ItemTooltipController.Show(item, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (item != null) ItemTooltipController.Move(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData) => ItemTooltipController.Hide(item);

    private void OnDisable() => ItemTooltipController.Hide(item);
}

/// <summary>인벤토리와 효과 HUD가 공유하는 최상단 아이템 설명 툴팁입니다.</summary>
public class ItemTooltipController : MonoBehaviour
{
    private static ItemTooltipController instance;
    private RectTransform canvasRect;
    private RectTransform panelRect;
    private TMP_Text label;
    private ItemData currentItem;
    private Vector2 pointerPosition;
    private float nextTextRefresh;

    public static void Show(ItemData item, Vector2 screenPosition)
    {
        EnsureInstance();
        instance.currentItem = item;
        instance.pointerPosition = screenPosition;
        instance.RefreshText();
        instance.panelRect.gameObject.SetActive(true);
        instance.UpdatePosition();
    }

    public static void Move(Vector2 screenPosition)
    {
        if (instance == null || !instance.panelRect.gameObject.activeSelf) return;
        instance.pointerPosition = screenPosition;
        instance.UpdatePosition();
    }

    public static void Hide(ItemData item)
    {
        if (instance == null || instance.currentItem != item) return;
        instance.currentItem = null;
        instance.panelRect.gameObject.SetActive(false);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindAnyObjectByType<ItemTooltipController>();
        if (instance != null) return;

        GameObject canvasObject = new("ItemTooltipCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 190;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        instance = canvasObject.AddComponent<ItemTooltipController>();
        instance.Build(canvasObject.GetComponent<RectTransform>());
    }

    private void Build(RectTransform root)
    {
        canvasRect = root;
        GameObject panel = new("ItemTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        panel.transform.SetParent(transform, false);
        panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(330f, 120f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.02f, 0.035f, 0.065f, 0.98f);
        background.raycastTarget = false;
        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.82f, 0.9f, 0.95f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject textObject = new("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        label = textObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.raycastTarget = false;
        SetTooltipAnchors(label.rectTransform, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        panel.SetActive(false);
    }

    private void Update()
    {
        if (panelRect == null || !panelRect.gameObject.activeSelf || currentItem == null) return;
        if (Time.unscaledTime >= nextTextRefresh)
        {
            nextTextRefresh = Time.unscaledTime + 0.2f;
            RefreshText();
        }
    }

    private void LateUpdate()
    {
        if (panelRect == null || !panelRect.gameObject.activeSelf) return;
        Pointer pointer = Pointer.current;
        if (pointer != null) pointerPosition = pointer.position.ReadValue();
        UpdatePosition();
    }

    private void RefreshText()
    {
        if (currentItem == null || label == null) return;
        string effect = GetEffectText(currentItem);
        label.text = $"<color=#8BEAF2>{currentItem.ItemName}</color>\n{currentItem.Description}" +
            (string.IsNullOrWhiteSpace(effect) ? string.Empty : $"\n<color=#F9D66D>{effect}</color>");
        label.ForceMeshUpdate();
        float height = Mathf.Clamp(label.preferredHeight + 20f, 82f, 230f);
        panelRect.sizeDelta = new Vector2(330f, height);
        UpdatePosition();
    }

    private static string GetEffectText(ItemData item)
    {
        if (item.IsActiveItem)
        {
            int level = ActiveItemEffectManager.Instance != null ? ActiveItemEffectManager.Instance.GetItemLevel(item) : 0;
            float basePercent = item.EffectAmount >= 1f ? item.EffectAmount : item.EffectAmount * 100f;
            float total = basePercent * Mathf.Max(1, level);
            string value = item.Type switch
            {
                ItemData.EffectType.ProfitBoost => $"수익 +{total:0.#}%",
                ItemData.EffectType.LossReduction => $"손실 감소 {total:0.#}%",
                ItemData.EffectType.MentalDrainGuard => $"멘탈 감소 완화 {total:0.#}%",
                ItemData.EffectType.HealthDrainGuard => $"체력 감소 완화 {total:0.#}%",
                _ => "영구 패시브"
            };
            return $"LV.{Mathf.Max(1, level)} · {value}";
        }

        if (item.ItemId == "pasta")
        {
            float seconds = DeliveryFoodManager.Instance != null ? DeliveryFoodManager.Instance.PastaRemainingSeconds : 0f;
            int remaining = Mathf.CeilToInt(seconds);
            return seconds > 0f
                ? $"게임 시간 ×{DeliveryFoodManager.PastaTimeMultiplier:0.#} · 남은 시간 {remaining / 60}:{remaining % 60:00}"
                : $"게임 시간 ×{DeliveryFoodManager.PastaTimeMultiplier:0.#} · 3분 지속";
        }

        return item.Type switch
        {
            ItemData.EffectType.Health => $"체력 +{item.EffectAmount:0.#}",
            ItemData.EffectType.Mental => $"멘탈 +{item.EffectAmount:0.#}",
            _ => string.Empty
        };
    }

    private void UpdatePosition()
    {
        if (canvasRect == null || panelRect == null) return;
        Canvas.ForceUpdateCanvases();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, null, out Vector2 local)) return;
        Vector2 offset = new(18f, -18f);
        Vector2 position = local + offset;
        float left = -canvasRect.rect.width * 0.5f + 8f;
        float right = canvasRect.rect.width * 0.5f - panelRect.rect.width - 8f;
        float bottom = -canvasRect.rect.height * 0.5f + panelRect.rect.height + 8f;
        float top = canvasRect.rect.height * 0.5f - 8f;
        panelRect.anchoredPosition = new Vector2(Mathf.Clamp(position.x, left, right), Mathf.Clamp(position.y, bottom, top));
    }

    private static void SetTooltipAnchors(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
