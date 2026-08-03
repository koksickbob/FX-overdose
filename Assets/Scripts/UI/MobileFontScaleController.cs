using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 모바일 빌드에서 런타임 및 씬의 모든 UGUI TMP 텍스트를 확대합니다.
/// 고정 크기 영역에서는 자동 크기 조절을 허용해 텍스트가 UI 밖으로 넘치지 않게 합니다.
/// </summary>
internal static class MobileFontScaleBootstrap
{
    private const string ControllerName = "[MobileFontScaleController]";
    private static MobileFontScaleController controller;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        controller = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!Application.isMobilePlatform) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureController();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureController();
        controller?.RequestImmediateScan();
    }

    private static void EnsureController()
    {
        if (!Application.isMobilePlatform || controller != null) return;

        GameObject root = new(ControllerName);
        Object.DontDestroyOnLoad(root);
        controller = root.AddComponent<MobileFontScaleController>();
    }
}

[DisallowMultipleComponent]
internal sealed class MobileFontScaleController : MonoBehaviour
{
    private const float MobileFontMultiplier = 2f;
    private const float MinimumReadableScale = 1f;
    private const float ScanInterval = 0.25f;
    private const float MinimumPointSize = 8f;

    private readonly HashSet<TextMeshProUGUI> processedTexts = new();
    private float nextScanTime;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ScanAndApply();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime) return;
        ScanAndApply();
    }

    internal void RequestImmediateScan()
    {
        nextScanTime = 0f;
    }

    private void ScanAndApply()
    {
        processedTexts.RemoveWhere(text => text == null);

        TextMeshProUGUI[] texts =
            Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text == null || !processedTexts.Add(text)) continue;
            ApplyMobileTypography(text);
        }

        nextScanTime = Time.unscaledTime + ScanInterval;
    }

    private static void ApplyMobileTypography(TextMeshProUGUI text)
    {
        float originalSize = Mathf.Max(MinimumPointSize, text.fontSize);
        float targetSize = originalSize * MobileFontMultiplier;

        float originalMin = text.enableAutoSizing && text.fontSizeMin > 0f
            ? text.fontSizeMin
            : originalSize;
        float minimumSize = Mathf.Max(
            MinimumPointSize,
            Mathf.Min(originalSize * MinimumReadableScale, originalMin * MobileFontMultiplier));

        text.enableAutoSizing = true;
        text.fontSize = targetSize;
        text.fontSizeMax = targetSize;
        text.fontSizeMin = Mathf.Min(minimumSize, targetSize);

        // 기존 줄바꿈 의도는 유지합니다. 여러 줄 텍스트는 확대 후에도 자연스럽게 재배치됩니다.
        if (text.textWrappingMode != TextWrappingModes.NoWrap)
            text.textWrappingMode = TextWrappingModes.Normal;

        text.SetAllDirty();

        RectTransform rect = text.rectTransform;
        if (rect == null) return;

        LayoutRebuilder.MarkLayoutForRebuild(rect);
        if (rect.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }
}
