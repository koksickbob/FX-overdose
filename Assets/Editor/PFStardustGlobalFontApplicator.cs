#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>프로젝트 UI의 기본 TMP 폰트를 PF Stardust로 통일합니다.</summary>
public static class PFStardustGlobalFontApplicator
{
    public const string FontPath = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
    private const string KoreanFallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/KoreanDynamicFont_TMP.asset";
    private const string AppliedKey = "FXOverdose_GlobalPFStardust_v4";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += TryApplyOnce;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyAfterPlay;
    }

    private static void ApplyAfterPlay() => Apply(false);

    private static void TryApplyOnce()
    {
        // Play 중에도 현재 실행 인스턴스에 PF Stardust를 즉시 적용합니다.
        if (EditorApplication.isPlaying)
        {
            Apply(false);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(AppliedKey, false)) return;
        Apply(false);
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Apply PF Stardust To All UI")]
    public static void ApplyFromMenu() => Apply(true);

    public static TMP_FontAsset GetFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_FontAsset korean = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallbackPath);
        if (font != null && korean != null && !font.fallbackFontAssetTable.Contains(korean))
        {
            font.fallbackFontAssetTable.Add(korean);
            EditorUtility.SetDirty(font);
        }
        return font;
    }

    private static void Apply(bool showResult)
    {
        TMP_FontAsset font = GetFont();
        if (font == null) return;

        int changed = 0;
        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>()
                     .Where(text => text.gameObject.scene.IsValid()))
        {
            if (text.font == font) continue;
            Undo.RecordObject(text, "Apply PF Stardust Font");
            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
            changed++;
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
        }
        AssetDatabase.SaveAssets();

        if (showResult)
            EditorUtility.DisplayDialog("PF Stardust 적용 완료", $"현재 열린 씬의 TMP 텍스트 {changed}개와 프로젝트 기본 폰트를 변경했습니다.", "확인");
    }
}
#endif
