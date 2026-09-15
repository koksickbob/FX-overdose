using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FXOverdose.DatingSim.UI;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 포인트 앤 클릭 방 검증용 빈 씬을 만듭니다. 내용물은 Play 시 DatingSimSceneBuilder가 씬 이름을 보고 조립합니다
    /// (카메라·EventSystem·DatingTimeManager·YomiRoomManager 포함). 빌드 설정에는 넣지 않습니다.
    /// </summary>
    public static class YomiRoomPointClickTestSceneBuilder
    {
        [MenuItem("FX Overdose/Build YomiRoom PointClick Test Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            const string folder = "Assets/Scenes/DatingSim";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes", "DatingSim");

            string path = $"{folder}/{YomiRoomPointClickBuilder.TestSceneName}.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[YomiRoomPointClick] 테스트 씬 생성: {path} — Play로 확인하세요.");
        }
    }
}
