#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FXOverdose.DatingSim.UI;

[InitializeOnLoad]
public static class DatingSimSceneInstaller
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/YomiRoomScene.unity",
        "Assets/Scenes/WorldMapScene.unity"
    };

    static DatingSimSceneInstaller()
    {
        EditorApplication.delayCall += EnsureScenes;
    }

    [MenuItem("FX Overdose/Phase 2/Build Dating Sim Scenes")]
    public static void EnsureScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene previousActive = SceneManager.GetActiveScene();
        foreach (string path in ScenePaths)
        {
            if (File.Exists(path)) continue;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // 먼저 경로에 저장해야 Scene.name이 YomiRoomScene/WorldMapScene으로 확정됩니다.
            EditorSceneManager.SaveScene(scene, path);
            DatingSimSceneBuilder.BuildForScene(scene);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
        }

        if (previousActive.IsValid() && previousActive.isLoaded)
            SceneManager.SetActiveScene(previousActive);

        List<EditorBuildSettingsScene> buildScenes = new(EditorBuildSettings.scenes);
        foreach (string path in ScenePaths)
        {
            if (buildScenes.Exists(entry => entry.path == path)) continue;
            buildScenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
