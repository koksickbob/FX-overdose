#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>감정 및 아이템 사용 이미지를 UI용 단일 픽셀 스프라이트로 통일해 임포트합니다.</summary>
public sealed class EmotionSpriteImporter : AssetPostprocessor
{
    private const string EmotionFolder = "Assets/Resources/Characters/Emotions/";
    private const string ItemUseFolder = "Assets/Resources/Characters/ItemUse/";

    private void OnPreprocessTexture()
    {
        bool isCharacterSprite =
            assetPath.StartsWith(EmotionFolder, System.StringComparison.Ordinal) ||
            assetPath.StartsWith(ItemUseFolder, System.StringComparison.Ordinal);

        if (!isCharacterSprite ||
            !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
    }
}
#endif
