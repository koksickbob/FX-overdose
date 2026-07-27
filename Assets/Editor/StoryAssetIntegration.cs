using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

public class StoryAssetIntegration : EditorWindow
{
    [MenuItem("FX Overdose/Integrate Story Assets")]
    public static void IntegrateAssets()
    {
        Debug.Log("[StoryAssetIntegration] 아트 에셋 변환 및 시스템 연동을 시작합니다...");

        // 1. Convert all textures in story_cutScene to Sprite
        string targetFolder = "Assets/Resources/story_cutScene";
        if (!Directory.Exists(targetFolder))
        {
            Debug.LogError($"[StoryAssetIntegration] 폴더를 찾을 수 없습니다: {targetFolder}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { targetFolder });
        int updatedCount = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                updatedCount++;
            }
        }
        if (updatedCount > 0)
        {
            Debug.Log($"[StoryAssetIntegration] {updatedCount}개의 이미지 텍스처 타입을 Sprite로 변환했습니다.");
        }

        // 2. Find GameManager in Scene
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("[StoryAssetIntegration] 활성화된 씬에서 GameManager를 찾을 수 없습니다! (GameScene을 열어주세요)");
            return;
        }

        // Helper to load sprites
        Sprite LoadSprite(string partialPath)
        {
            string fullPath = $"{targetFolder}/{partialPath}";
            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (spr == null)
                Debug.LogWarning($"[StoryAssetIntegration] 스프라이트를 로드할 수 없습니다: {fullPath}");
            return spr;
        }

        // 3. Populate StoryEvents
        List<GameManager.StoryEvent> storyEvents = new List<GameManager.StoryEvent>();

        storyEvents.Add(new GameManager.StoryEvent() {
            triggerDay = 1, isPenalty = false, penaltyAmount = 0f, dialogueMessage = "",
            comicPanels = new List<Sprite> {
                LoadSprite("opening_v2/opening_01_cafe_disaster_v2.png"),
                LoadSprite("opening_v2/opening_02_rainy_balance_v2.png"),
                LoadSprite("opening_v2/opening_03_fx_billboard_v2.png"),
                LoadSprite("opening_v2/opening_04_first_trade_v2.png")
            }
        });

        storyEvents.Add(new GameManager.StoryEvent() {
            triggerDay = 5, isPenalty = true, penaltyAmount = 10000f, dialogueMessage = "",
            comicPanels = new List<Sprite> {
                LoadSprite("day05/day05_01_rent_notice.png"),
                LoadSprite("day05/day05_02_forced_withdrawal.png")
            }
        });

        storyEvents.Add(new GameManager.StoryEvent() {
            triggerDay = 6, isPenalty = false, penaltyAmount = 0f, dialogueMessage = "",
            comicPanels = new List<Sprite> {
                LoadSprite("day06/day06_01_tiny_leverage.png"),
                LoadSprite("day06/day06_02_full_leverage.png")
            }
        });

        storyEvents.Add(new GameManager.StoryEvent() {
            triggerDay = 11, isPenalty = true, penaltyAmount = 30000f, dialogueMessage = "",
            comicPanels = new List<Sprite> {
                LoadSprite("day11/day11_01_loan_shark.png"),
                LoadSprite("day11/day11_02_transfer_30000.png")
            }
        });

        storyEvents.Add(new GameManager.StoryEvent() {
            triggerDay = 16, isPenalty = true, penaltyAmount = 100000f, dialogueMessage = "",
            comicPanels = new List<Sprite> {
                LoadSprite("day16/day16_01_compensation_bill.png"),
                LoadSprite("day16/day16_02_balance_shattered.png"),
                LoadSprite("day16/day16_03_100x_awakened.png")
            }
        });

        var storyEventsField = typeof(GameManager).GetField("storyEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        if (storyEventsField != null) storyEventsField.SetValue(gm, storyEvents);

        // 4. Populate Ending Comics
        List<Sprite> successComics = new List<Sprite> {
            LoadSprite("ending_success/success_01_million_balance.png"),
            LoadSprite("ending_success/success_02_penthouse.png"),
            LoadSprite("ending_success/success_03_money_rain_toast.png")
        };
        var successField = typeof(GameManager).GetField("successEndingComic", BindingFlags.NonPublic | BindingFlags.Instance);
        if (successField != null) successField.SetValue(gm, successComics);

        List<Sprite> bankruptcyComics = new List<Sprite> {
            LoadSprite("ending_bankruptcy/bankruptcy_01_liquidated.png"),
            LoadSprite("ending_bankruptcy/bankruptcy_02_evicted.png"),
            LoadSprite("ending_bankruptcy/bankruptcy_03_job_search.png")
        };
        var bankruptcyField = typeof(GameManager).GetField("bankruptcyEndingComic", BindingFlags.NonPublic | BindingFlags.Instance);
        if (bankruptcyField != null) bankruptcyField.SetValue(gm, bankruptcyComics);

        List<Sprite> overdoseComics = new List<Sprite> {
            LoadSprite("ending_overdose/overdose_01_mental_break.png"),
            LoadSprite("ending_overdose/overdose_02_money_hallucination.png"),
            LoadSprite("ending_overdose/overdose_03_chart_madness.png")
        };
        var overdoseField = typeof(GameManager).GetField("overdoseEndingComic", BindingFlags.NonPublic | BindingFlags.Instance);
        if (overdoseField != null) overdoseField.SetValue(gm, overdoseComics);

        EditorUtility.SetDirty(gm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(gm.gameObject.scene);

        Debug.Log("[StoryAssetIntegration] 모든 컷툰 이미지와 위약금 데이터가 GameManager에 성공적으로 연동 및 저장되었습니다!");
    }
}
