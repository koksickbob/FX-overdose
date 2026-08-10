using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace FXOverdose.DatingSim.LLM.Scenario.Editor
{
    public class ScenarioCSVImporter : EditorWindow
    {
        private string csvFilePath = "Assets/Data/Scenarios/ScenarioMaster.csv";
        private string scriptFolderPath = "Assets/Data/Scenarios/Story_Scripts";
        private string outputAssetPath = "Assets/Resources/ScenarioDatabase.asset";

        [MenuItem("FX Overdose/AI/Import Scenario (CSV+TXT)")]
        public static void ShowWindow()
        {
            GetWindow<ScenarioCSVImporter>("Scenario Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("대본 연기형 이원화(CSV+TXT) 임포터", EditorStyles.boldLabel);
            
            csvFilePath = EditorGUILayout.TextField("마스터 CSV 경로", csvFilePath);
            scriptFolderPath = EditorGUILayout.TextField("대본 폴더 경로", scriptFolderPath);
            outputAssetPath = EditorGUILayout.TextField("출력 DB 경로", outputAssetPath);

            if (GUILayout.Button("수치(CSV) + 대본(TXT) 병합 베이킹"))
            {
                BakeDualTrack();
            }
        }

        private void BakeDualTrack()
        {
            if (!File.Exists(csvFilePath))
            {
                Debug.LogError($"[ScenarioCSVImporter] 마스터 CSV 파일을 찾을 수 없습니다: {csvFilePath}");
                return;
            }
            if (!Directory.Exists(scriptFolderPath))
            {
                Debug.LogError($"[ScenarioCSVImporter] 대본(TXT) 폴더를 찾을 수 없습니다: {scriptFolderPath}");
                return;
            }

            ScenarioDatabase db = AssetDatabase.LoadAssetAtPath<ScenarioDatabase>(outputAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<ScenarioDatabase>();
                AssetDatabase.CreateAsset(db, outputAssetPath);
            }

            db.entries.Clear();
            
            // 1. CSV 파싱 (수치, 조건 등 뼈대 생성)
            // ParseMasterCSV(csvFilePath, db);

            // 2. TXT 매칭 (내용, 목표, 대사 주입)
            // MergeScripts(scriptFolderPath, db);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ScenarioCSVImporter] 이원화(CSV+TXT) 병합 완료! 총 {db.entries.Count}개의 시나리오를 구웠습니다.");
        }
    }
}
