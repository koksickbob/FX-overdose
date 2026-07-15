#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>주인공 idle 시트를 픽셀 애니메이션용 텍스처로 자동 임포트합니다.</summary>
public static class ProtagonistIdleSheetInstaller
{
    private const string SheetPath = "Assets/Resources/Characters/ProtagonistIdleSheet16.png";
    private const string AppliedKey = "FXOverdose_ProtagonistIdleSheet16_v1";

    [InitializeOnLoadMethod]
    private static void ApplyAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AppliedKey, false)) return;
            TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            EditorPrefs.SetBool(AppliedKey, true);
        };
    }
}
#endif
