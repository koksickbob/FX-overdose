using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.Editor
{
    public class BuildDataCleaner : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUtility.DisplayDialog(
                "빌드 전 데이터 초기화",
                "빌드를 진행하기 전에 기존 저장 데이터(슬롯 1~20)와 업적 등 모든 PlayerPrefs 데이터를 초기화하시겠습니까?\n\n(출시용 또는 클린 빌드 테스트를 위해 초기화를 권장합니다.)",
                "초기화하고 빌드",
                "유지하고 빌드"))
            {
                ClearAllSaveData();
            }
        }

        [MenuItem("Tools/FX Overdose/Clear All Save Data & Achievements")]
        public static void ClearAllSaveDataMenuItem()
        {
            if (EditorUtility.DisplayDialog(
                "모든 데이터 초기화",
                "저장된 게임 슬롯과 업적, 옵션 등 모든 데이터를 초기화하시겠습니까?",
                "예 (초기화)",
                "아니오"))
            {
                ClearAllSaveData();
            }
        }

        private static void ClearAllSaveData()
        {
            // 1. PlayerPrefs 초기화 (업적, 설정 등 모두 포함)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 2. 세이브 파일 삭제
            for (int i = 0; i < SaveLoadManager.MaxStorySlots; i++)
            {
                string path = Path.Combine(Application.persistentDataPath, $"save_slot_{i}.json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[BuildDataCleaner] 세이브 파일 삭제됨: {path}");
                }
            }

            Debug.Log("[BuildDataCleaner] 모든 저장 데이터와 업적이 초기화되었습니다.");
        }
    }
}
