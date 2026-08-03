using UnityEngine;
using UnityEditor;
using System.IO;
using FXOverdose.AI.Dialogue;
using System.Collections.Generic;

[System.Serializable]
public class DialogueExportData
{
    public List<YomiDialogueEntry> entries;
}

public class YomiDialogueTools
{
    private const string AssetPath = "Assets/YomiDialogueDatabase.asset";
    private const string ExportPath = "DialogueDB_Export.json";
    private const string ImportPath = "DialogueDB_Import.json";

    [MenuItem("FX Overdose/Dialogue/Export Database to JSON")]
    public static void ExportDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(AssetPath);
        if (db == null)
        {
            Debug.LogError($"[YomiDialogueTools] Cannot find YomiDialogueDatabase at {AssetPath}");
            return;
        }

        var exportData = new DialogueExportData { entries = db.entries };
        string json = JsonUtility.ToJson(exportData, true);
        File.WriteAllText(ExportPath, json);
        Debug.Log($"[YomiDialogueTools] Exported {db.entries.Count} entries to {ExportPath}");
        AssetDatabase.Refresh();
    }

    [MenuItem("FX Overdose/Dialogue/Import Database from JSON")]
    public static void ImportDatabase()
    {
        if (!File.Exists(ImportPath))
        {
            Debug.LogError($"[YomiDialogueTools] Cannot find {ImportPath}");
            return;
        }

        string json = File.ReadAllText(ImportPath);
        var importData = JsonUtility.FromJson<DialogueExportData>(json);

        if (importData == null || importData.entries == null)
        {
            Debug.LogError("[YomiDialogueTools] Failed to parse JSON or entries list is empty.");
            return;
        }

        var db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(AssetPath);
        if (db == null)
        {
            Debug.LogError($"[YomiDialogueTools] Cannot find YomiDialogueDatabase at {AssetPath}");
            return;
        }

        Undo.RecordObject(db, "Import Yomi Dialogue Database");
        db.entries = importData.entries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[YomiDialogueTools] Imported {db.entries.Count} entries from {ImportPath}");
    }
}
