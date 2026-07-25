using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;
using System.Text.RegularExpressions;

namespace FXOverdose.Editor
{
    public class DialogueTagConverterEditor : EditorWindow
    {
        [MenuItem("FX Overdose/AI/Convert Hardcoded Numbers to Tags")]
        public static void ConvertToTags()
        {
            string[] guids = AssetDatabase.FindAssets("t:YomiDialogueDatabase");
            if (guids.Length == 0)
            {
                Debug.LogError("YomiDialogueDatabase 찾을 수 없음.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            YomiDialogueDatabase db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(path);

            if (db == null)
            {
                Debug.LogError("Failed to load YomiDialogueDatabase at " + path);
                return;
            }

            Undo.RecordObject(db, "Convert Hardcoded Numbers");

            int convertedCount = 0;

            foreach (var entry in db.entries)
            {
                string originalText = entry.text;
                string newText = originalText;

                // "100배", "50배", "125배" 등을 "{leverage}배"로 변환
                newText = Regex.Replace(newText, @"\b\d+배\b", "{leverage}배");
                
                // "100%", "50%" 등을 "{margin}%"로 변환
                // 주의: "수익률 30%" 등 다른 퍼센트가 있을 수 있으므로 비중/마진 문맥만 치환하는 것이 안전하지만,
                // 기존 하드코딩된 '풀매수 100%' 등을 찾기 위해 단순히 \d+% 앞뒤로 문맥을 볼 수도 있습니다.
                // 여기서는 "비중 \d+%", "마진 \d+%" 형태나 "전재산 100%" 등을 고려하여 변환.
                // 정규식: 비중 100%, 자산의 100% 등
                newText = Regex.Replace(newText, @"(비중|마진|자산의)\s*\d+%", "$1 {margin}%");
                
                if (originalText != newText)
                {
                    entry.text = newText;
                    
                    // 만약 이 기존 대사가 원래 특정 레버리지를 가정하고 써진 거라면, 
                    // 굳이 requiredLeverage가 고정되어 있을 필요 없이 모든 레버리지에서 범용적으로 쓰일 수 있게 
                    // requiredLeverage를 0으로 해제할 수도 있지만, 일단 텍스트만 치환합니다.
                    
                    convertedCount++;
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Dialogue Tag Converter] 완료! 총 {convertedCount}개의 대사에서 하드코딩된 배율/비중을 {{leverage}}, {{margin}} 태그로 변환했습니다.");
        }
    }
}
