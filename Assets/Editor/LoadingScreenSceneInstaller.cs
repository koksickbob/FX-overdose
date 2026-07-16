#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FXOverdose.UI;

[InitializeOnLoad]
public static class LoadingScreenSceneInstaller
{
    static LoadingScreenSceneInstaller()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += TryBuild;
    }

    [MenuItem("FX Overdose/Build Loading Screen")]
    private static void BuildFromMenu() => TryBuild();

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name == "LoadingScene") EditorApplication.delayCall += TryBuild;
    }

    private static void TryBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "LoadingScene") return;
        if (GameObject.Find("Canvas_LoadingScreen") != null && Object.FindAnyObjectByType<Camera>() != null) return;
        LoadingScreenBuilder.BuildForCurrentScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
