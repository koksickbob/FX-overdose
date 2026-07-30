using UnityEngine;
using UnityEditor;
using TMPro;

[InitializeOnLoad]
public class TMPFixer
{
    static TMPFixer()
    {
        EditorApplication.delayCall += RunFix;
    }

    static void RunFix()
    {
        if (SessionState.GetBool("TMPFixer_Run2", false))
            return;

        SessionState.SetBool("TMPFixer_Run2", true);

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset != null)
            {
                if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                {
                    try
                    {
                        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                        {
                            fontAsset.ClearFontAssetData(true);
                            EditorUtility.SetDirty(fontAsset);
                            Debug.Log($"[TMPFixer] Cleared dynamic data for {fontAsset.name}.");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[TMPFixer] Could not clear data for {fontAsset.name}: {e.Message}");
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TMPFixer] Font assets reimported successfully. The NativeFormatImporter error should now be gone.");
    }
}
