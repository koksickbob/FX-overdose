using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace FXOverdose.EditorTools
{
    public class MissingScriptCleaner
    {
        [MenuItem("Tools/FX Overdose/지긋지긋한 Missing Script 싹 지우기")]
        public static void CleanUpMissingScripts()
        {
            int totalRemoved = 0;
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            
            foreach (var go in rootObjects)
            {
                var allTransforms = go.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }
            }
            
            if (totalRemoved > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[Auto-Fix] 씬 전체에서 {totalRemoved}개의 Missing Script 찌꺼기를 찾아내어 영구 삭제했습니다! 지금 바로 Ctrl+S를 눌러 씬을 저장하세요.");
            }
            else
            {
                Debug.Log("[Auto-Fix] 현재 씬에서 더 이상 Missing Script 찌꺼기가 발견되지 않았습니다.");
            }
        }
    }
}
