using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using FXOverdose.AI.Dialogue;

namespace FXOverdose.Editor
{
    public class YomiDatasetConverter : EditorWindow
    {
        private string jsonlFilePath = "Assets/yomi_dataset.jsonl"; // Default location, wait, let's use the one in root
        private YomiDialogueDatabase databaseTarget;

        [MenuItem("FX Overdose/AI/Convert Yomi Dataset")]
        public static void ShowWindow()
        {
            GetWindow<YomiDatasetConverter>("Yomi Dataset Converter");
        }

        private void OnGUI()
        {
            GUILayout.Label("Yomi Dataset to ScriptableObject", EditorStyles.boldLabel);
            jsonlFilePath = EditorGUILayout.TextField("JSONL File Path", jsonlFilePath);
            databaseTarget = (YomiDialogueDatabase)EditorGUILayout.ObjectField("Target Database", databaseTarget, typeof(YomiDialogueDatabase), false);

            if (GUILayout.Button("Convert"))
            {
                if (databaseTarget != null && !string.IsNullOrEmpty(jsonlFilePath))
                {
                    ConvertJsonl(jsonlFilePath, databaseTarget);
                }
                else
                {
                    Debug.LogError("Database Target or File Path is missing.");
                }
            }
        }

        private void ConvertJsonl(string path, YomiDialogueDatabase db)
        {
            if (!File.Exists(path))
            {
                // Fallback to project root if not found in Assets
                string rootPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "yomi_dataset.jsonl");
                if (File.Exists(rootPath)) path = rootPath;
                else
                {
                    Debug.LogError($"File not found at {path} or {rootPath}");
                    return;
                }
            }

            db.entries.Clear();
            string[] lines = File.ReadAllLines(path);
            int count = 0;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string marketTrend = "";
                string mentalState = "";
                string positionInfo = "";
                string position = "None";
                int lev = 0;
                string roeStr = "";
                bool isProfit = false;
                int heroLv = 0;
                int skillLv = 0;
                string evCategory = "";
                string dialogue = "";

                if (line.Contains("\"Dialogue\""))
                {
                    // 새 템플릿 포맷: {"Event_Category": "...", "Mental_State": "...", "Dialogue": "..."}
                    evCategory = ExtractJsonValue(line, "Event_Category");
                    mentalState = ExtractJsonValue(line, "Mental_State");
                    dialogue = ExtractJsonValue(line, "Dialogue");
                    dialogue = dialogue.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                }
                else
                {
                    // 1. 단순 파싱 (JSONL 구조가 단순하므로 Regex나 단순 문자열 검색 활용)
                    marketTrend = ExtractValue(line, "Market_Trend");
                    mentalState = ExtractValue(line, "Mental_State");
                    positionInfo = ExtractValue(line, "Position"); // e.g. "Short (20x Leverage)" or "None (10x Leverage)"
                    if (positionInfo.Contains("Long")) position = "Long";
                    else if (positionInfo.Contains("Short")) position = "Short";

                    Match levMatch = Regex.Match(positionInfo, @"(\d+)x Leverage");
                    if (levMatch.Success) int.TryParse(levMatch.Groups[1].Value, out lev);

                    roeStr = ExtractValue(line, "Current_ROE"); // e.g. "-47.6%"
                    if (!string.IsNullOrEmpty(roeStr) && roeStr.Contains("+"))
                    {
                        isProfit = true;
                    }

                    string heroStr = ExtractValue(line, "Hero_Level");
                    if (!string.IsNullOrEmpty(heroStr)) int.TryParse(heroStr, out heroLv);
                    
                    string skillStr = ExtractValue(line, "Skill_Level");
                    if (!string.IsNullOrEmpty(skillStr)) int.TryParse(skillStr, out skillLv);
                    
                    evCategory = ExtractValue(line, "Event_Category");

                    // Assistant 대사 추출
                    dialogue = ExtractAssistantContent(line);
                }

                if (string.IsNullOrEmpty(dialogue)) continue; // 대사가 없으면 건너뜀

                // Auto-Tagging 단기 방향성 및 하이 리스크 태깅
                DirectionTag dir = AutoTagDirection(dialogue);
                bool isHighRisk = dialogue.Contains("풀시드") || dialogue.Contains("전재산") || dialogue.Contains("청산") || dialogue.Contains("몰빵");

                YomiDialogueEntry entry = new YomiDialogueEntry(dialogue, marketTrend, mentalState, position, dir, isProfit, lev, heroLv, skillLv, isHighRisk, evCategory);
                db.entries.Add(entry);
                count++;
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"Successfully converted {count} entries to {db.name}.");
        }

        private string ExtractJsonValue(string jsonLine, string key)
        {
            string pattern = $@"""{key}""\s*:\s*""(.*?)""";
            Match m = Regex.Match(jsonLine, pattern);
            if (m.Success)
            {
                return m.Groups[1].Value.Trim();
            }
            return "";
        }

        private string ExtractValue(string jsonLine, string key)
        {
            // 형식: "- Key: Value\n" 또는 "- Key: Value" (json 안에 이스케이프되어 있음)
            // ex: "- Market_Trend: Sideways\\n"
            string pattern = $@"- {key}:\s*([^\\]+)";
            Match m = Regex.Match(jsonLine, pattern);
            if (m.Success)
            {
                return m.Groups[1].Value.Trim();
            }
            return "";
        }

        private string ExtractAssistantContent(string jsonLine)
        {
            // "role": "assistant", "content": "내 숏 포지션... 빨갛게 타들어 가고 있어 오빠...!!"
            string pattern = @"""role"":\s*""assistant"",\s*""content"":\s*""(.*?)""}";
            Match m = Regex.Match(jsonLine, pattern);
            if (m.Success)
            {
                string text = m.Groups[1].Value;
                // Unescape common JSON characters
                text = text.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                return text.Trim();
            }
            return "";
        }

        private DirectionTag AutoTagDirection(string text)
        {
            string lower = text.ToLower();
            
            // 상승 키워드
            if (lower.Contains("양봉") || lower.Contains("떡상") || lower.Contains("불기둥") || lower.Contains("상승") || lower.Contains("오르") || lower.Contains("로켓") || lower.Contains("빔 쏘기 직전") || lower.Contains("하늘 뚫고"))
            {
                return DirectionTag.Up;
            }
            // 하락 키워드
            if (lower.Contains("음봉") || lower.Contains("나락") || lower.Contains("폭락") || lower.Contains("폭포수") || lower.Contains("하락") || lower.Contains("떨어") || lower.Contains("지옥행") || lower.Contains("빨갛게 타들어"))
            {
                return DirectionTag.Down;
            }
            
            return DirectionTag.None;
        }
    }
}
