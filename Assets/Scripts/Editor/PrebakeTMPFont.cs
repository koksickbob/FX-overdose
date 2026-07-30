using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

public class PrebakeTMPFont
{
    [MenuItem("Tools/Prebake All Scripts Text into Font")]
    public static void Bake()
    {
        string fontPath = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null)
        {
            Debug.LogError("Font not found at " + fontPath);
            return;
        }

        HashSet<char> uniqueChars = new HashSet<char>();
        string[] scriptFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        
        foreach (string file in scriptFiles)
        {
            string content = File.ReadAllText(file);
            foreach (char c in content)
            {
                // Only add hangul and common symbols
                if ((c >= 0xAC00 && c <= 0xD7A3) || 
                    (c >= 0x3131 && c <= 0x318E) || 
                    (c >= 0x0020 && c <= 0x007E)) 
                {
                    uniqueChars.Add(c);
                }
            }
        }

        StringBuilder sb = new StringBuilder();
        foreach (char c in uniqueChars)
        {
            sb.Append(c);
        }

        font.TryAddCharacters(sb.ToString());
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully prebaked {uniqueChars.Count} characters into the font atlas!");
    }
}
