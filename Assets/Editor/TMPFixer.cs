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
        if (SessionState.GetBool("TMPFixer_Run3", false))
            return;

        SessionState.SetBool("TMPFixer_Run3", true);

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("PFStardustBold Dynamic SDF"))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[TMPFixer] Force reimported {path} to restore font.");
            }
        }
    }
}
