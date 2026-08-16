using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 이벤트 전용 씬의 껍데기를 만들고 빌드 세팅에 등록합니다.
    ///
    /// 내용물은 이 스크립트가 만들지 않습니다 — 씬 로드 시 <c>DatingSimSceneBuilder</c>가 카메라·입력·
    /// <c>EventSceneHost</c>를 조립하고, 화면은 <c>EventView</c>가 런타임에 만듭니다.
    /// 편의점·요미 방과 같은 구조이며, 그래서 씬 파일에 UI가 직렬화되지 않습니다.
    ///
    /// <b>화면을 런타임에 만드는 것이 핵심입니다.</b> 프리팹을 씬에 배치하면 오버레이 호스트가 만드는
    /// 화면과 조용히 어긋나고, 그 순간 "두 표현이 같다"는 계약(계획 R12)이 깨집니다.
    /// </summary>
    public static class EventSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/EventScene.unity";

        [MenuItem("FX Overdose/Build Event Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterInBuildSettings();
            Debug.Log($"[FX Overdose] 이벤트 씬 생성 완료: {ScenePath}\n" +
                      "내용물은 실행 시 DatingSimSceneBuilder + EventView가 조립합니다. 씬을 직접 편집하지 마십시오.");
        }

        private static void RegisterInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene entry in scenes)
                if (entry.path == ScenePath) return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[FX Overdose] 빌드 세팅에 EventScene을 등록했습니다.");
        }
    }
}
