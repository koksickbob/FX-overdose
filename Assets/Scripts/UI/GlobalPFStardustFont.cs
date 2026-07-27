using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>모든 런타임 TMP 텍스트에 프로젝트 기본 폰트(PF Stardust)를 적용합니다.</summary>
public static class GlobalPFStardustFont
{
    private const string CompactHudCharacters = " AUTOUSERLV.EXP/0123456789";
    private static readonly HashSet<TMP_FontAsset> WarmedFonts = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        WarmedFonts.Clear();
        SceneManager.sceneLoaded -= ApplyToScene;
        SceneManager.sceneLoaded += ApplyToScene;
    }

    private static void ApplyToScene(Scene scene, LoadSceneMode mode)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                // fontSharedMaterial을 강제로 덮으면 개별 Outline/패딩 Material이
                // 씬 콜백 순서에 따라 사라질 수 있으므로 폰트가 다를 때만 교체합니다.
                if (text.font != font) text.font = font;
            }
        }
    }

    /// <summary>
    /// 작은 ASCII HUD 라벨을 PF Stardust의 고정 크기로 안정화합니다.
    /// 빌드에서 동적 아틀라스가 비워져도 필요한 글자를 첫 사용 전에 한 번 준비합니다.
    /// </summary>
    public static void ConfigureCompactHudText(
        TMP_Text text,
        TMP_FontAsset preferredFont,
        float fontSize,
        float outlineWidth = 0f)
    {
        if (text == null) return;

        TMP_FontAsset targetFont = preferredFont != null ? preferredFont : TMP_Settings.defaultFontAsset;
        if (targetFont != null)
        {
            EnsureCompactHudCharacters(targetFont);
            if (text.font != targetFont) text.font = targetFont;
        }

        // 원본 TTF 자체가 Bold이므로 합성 Bold를 더하지 않습니다.
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.fontSizeMin = fontSize;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.outlineWidth = Mathf.Clamp(outlineWidth, 0f, 0.05f);
        CompactHudTextStabilizer stabilizer = text.GetComponent<CompactHudTextStabilizer>();
        if (stabilizer == null)
            stabilizer = text.gameObject.AddComponent<CompactHudTextStabilizer>();
        stabilizer.RequestRefresh();
        RefreshCompactHudText(text);
    }

    /// <summary>값이 바뀐 작은 HUD 텍스트의 TMP 메쉬 업데이트를 예약합니다.</summary>
    public static void RefreshCompactHudText(TMP_Text text)
    {
        if (text == null) return;
        CompactHudTextStabilizer stabilizer = text.GetComponent<CompactHudTextStabilizer>();
        if (stabilizer != null) stabilizer.RequestRefresh();
        text.SetAllDirty();
    }

    private static void EnsureCompactHudCharacters(TMP_FontAsset font)
    {
        if (!WarmedFonts.Add(font)) return;
        if (font.HasCharacters(CompactHudCharacters)) return;

        if (!font.TryAddCharacters(CompactHudCharacters, out string missing) && !string.IsNullOrEmpty(missing))
            Debug.LogWarning($"[GlobalPFStardustFont] 소형 HUD 글리프 준비 실패: {missing}", font);
    }
}

/// <summary>
/// 동적 TMP 아틀라스가 같은 프레임에 변경되더라도 소형 HUD 글리프가 깨지지 않도록
/// 값 변경 후 여러 렌더 프레임에 걸쳐 메쉬와 머티리얼을 다시 확정합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CompactHudTextStabilizer : MonoBehaviour
{
    private const int StabilizationFrames = 3;

    private TMP_Text target;
    private string lastText;
    private TMP_FontAsset lastFont;
    private int pendingFrames;

    private void Awake()
    {
        target = GetComponent<TMP_Text>();
        RequestRefresh();
    }

    private void OnEnable()
    {
        RequestRefresh();
    }

    public void RequestRefresh()
    {
        pendingFrames = StabilizationFrames;
    }

    private void LateUpdate()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;

        if (lastText != target.text || lastFont != target.font || target.havePropertiesChanged)
        {
            lastText = target.text;
            lastFont = target.font;
            pendingFrames = StabilizationFrames;
        }

        if (pendingFrames <= 0) return;

        target.SetAllDirty();
        target.ForceMeshUpdate(true, true);
        target.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        pendingFrames--;
    }
}
