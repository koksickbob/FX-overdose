#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FXOverdose.UI;

/// <summary>TitleScene을 열면 타이틀 UI를 편집 가능한 실제 Hierarchy로 조립합니다.</summary>
[InitializeOnLoad]
public static class TitleScreenSceneInstaller
{
    static TitleScreenSceneInstaller()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += TryBuildActiveScene;
    }

    [MenuItem("FX Overdose/Build Title Screen")]
    private static void BuildFromMenu()
    {
        TryBuildActiveScene();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name == "TitleScene")
            EditorApplication.delayCall += TryBuildActiveScene;
    }

    private static void TryBuildActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "TitleScene") return;

        GameObject existingCanvas = GameObject.Find("Canvas_MainMenu");
        if (existingCanvas != null && Object.FindAnyObjectByType<Camera>() != null)
        {
            // 기존 타이틀 UI는 보존하고, 새 버전에 필요한 모드 선택 창만 증분 설치합니다.
            if (existingCanvas.transform.Find("GameModePanel") == null)
            {
                TitleScreenBuilder.EnsureGameModePanel(existingCanvas.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[TitleScreenSceneInstaller] GameModePanel 증분 설치 완료");
            }
            return;
        }

        TitleScreenBuilder.BuildForCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
