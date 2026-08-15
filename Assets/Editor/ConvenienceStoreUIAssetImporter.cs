using UnityEditor;
using UnityEngine;

/// <summary>편의점 UI 원화를 선명한 픽셀 UI 스프라이트로 일관되게 가져옵니다.</summary>
public sealed class ConvenienceStoreUIAssetImporter : AssetPostprocessor
{
    private const string Root = "Assets/Resources/DatingSim/Store/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root) || !assetPath.EndsWith(".png")) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = assetPath.Contains("/Character/") ? 220f : 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
    }
}
