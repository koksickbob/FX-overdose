using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public class SwapDefaultSprite
{
    static SwapDefaultSprite()
    {
        EditorApplication.delayCall += RunFix;
    }

    private static void RunFix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        
        bool changed = false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                changed |= ProcessScene(scene);
            }
        }

        if (changed)
        {
            Debug.Log("<color=green>[FX Overdose] 기본 에셋(Focused.png)으로 에디터 프리뷰 렌더링을 완전히 교체했습니다.</color>");
            AssetDatabase.DeleteAsset("Assets/Editor/SwapDefaultSprite.cs");
        }
    }

    private static bool ProcessScene(Scene scene)
    {
        bool changed = false;
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                if (img.gameObject.name == "ProtagonistCharacterImage")
                {
                    Sprite correctSprite = Resources.Load<Sprite>("Characters/Emotions/Focused");
                    if (correctSprite != null && img.sprite != correctSprite)
                    {
                        var so = new SerializedObject(img);
                        so.FindProperty("m_Sprite").objectReferenceValue = correctSprite;
                        so.ApplyModifiedProperties();
                        changed = true;
                        
                        EditorUtility.SetDirty(img.gameObject);
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }
        }
        return changed;
    }
}
