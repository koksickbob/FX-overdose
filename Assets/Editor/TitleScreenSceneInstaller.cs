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
        if (GameObject.Find("Canvas_MainMenu") != null && Object.FindAnyObjectByType<Camera>() != null) return;

        TitleScreenBuilder.BuildForCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
