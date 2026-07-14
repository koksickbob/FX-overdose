using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>모든 런타임 TMP 텍스트에 프로젝트 기본 폰트(PF Stardust)를 적용합니다.</summary>
public static class GlobalPFStardustFont
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= ApplyToScene;
        SceneManager.sceneLoaded += ApplyToScene;
    }

    private static void ApplyToScene(Scene scene, LoadSceneMode mode)
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }
    }
}
