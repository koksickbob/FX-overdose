using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 편의점 알바 씬의 껍데기를 만들고 빌드 세팅에 등록합니다.
    ///
    /// 내용물은 이 스크립트가 만들지 않습니다 — `ConvenienceStorePrototype`이 씬 로드 시 런타임에 조립합니다.
    /// 요미의 방·월드맵과 같은 구조이며, 그래서 씬 파일에 임시 스프라이트가 직렬화되지 않습니다.
    /// </summary>
    public static class ConvenienceStoreSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ConvenienceStoreScene.unity";

        [MenuItem("FX Overdose/Build Convenience Store Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
            Debug.Log($"[FX Overdose] 편의점 씬 생성 완료: {ScenePath}\n" +
                      "내용물은 실행 시 ConvenienceStorePrototype이 조립합니다. 씬을 직접 편집하지 마십시오.");
        }

        private static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene entry in scenes)
                if (entry.path == ScenePath) return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[FX Overdose] 빌드 세팅에 ConvenienceStoreScene을 등록했습니다.");
        }
    }
}
