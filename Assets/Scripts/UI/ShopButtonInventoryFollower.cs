using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>동적으로 커지는 인벤토리 패널 위에 SHOP 버튼을 유지합니다.</summary>
[RequireComponent(typeof(RectTransform))]
public class ShopButtonInventoryFollower : MonoBehaviour
{
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private float gap = 12f;
    [SerializeField] private float rightMargin = 24f;
    [SerializeField] private Vector2 buttonSize = new(260f, 86f);

    private RectTransform buttonRect;
    private Vector2 lastInventorySize;

    private void Awake()
    {
        buttonRect = GetComponent<RectTransform>();
        ApplyButtonDesign();
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (inventoryPanel != null && inventoryPanel.sizeDelta != lastInventorySize) UpdatePosition();
    }

    public void Configure(RectTransform targetInventoryPanel)
    {
        inventoryPanel = targetInventoryPanel;
        if (buttonRect == null) buttonRect = GetComponent<RectTransform>();
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (inventoryPanel == null || buttonRect == null) return;
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.sizeDelta = buttonSize;
        buttonRect.anchoredPosition = new Vector2(-rightMargin, inventoryPanel.anchoredPosition.y + inventoryPanel.rect.height + gap);
        lastInventorySize = inventoryPanel.sizeDelta;
    }

    private void ApplyButtonDesign()
    {
        Image background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        // 씬에 연결된 원본 스프라이트의 쇼핑백 아이콘과 픽셀 프레임을 유지합니다.
        background.type = background.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = Color.white;

        Outline outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.72f, 0.84f, 1f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.55f, 0.84f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.4f, 0.48f, 0.52f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        TMP_Text label = GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "SHOP";
            label.fontSize = 29f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.86f, 0.98f, 1f, 1f);
            label.characterSpacing = 1.5f;
            label.alignment = TextAlignmentOptions.Center;
            // 쇼핑백과 문구를 하나의 콘텐츠 묶음으로 보고 좌우 끝 여백을 동일하게 맞춥니다.
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(84f, 0f), new Vector2(-52f, 0f));
            label.transform.SetAsLastSibling();
        }
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
