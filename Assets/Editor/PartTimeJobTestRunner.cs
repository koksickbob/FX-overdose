using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Core;
using FXOverdose.DatingSim.WorldMap;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 편의점 알바(즉시 정산)의 데이터 정합성 검사.
    /// 편의점 타이쿤 폐기(2026-10-08) 때 ConvenienceStoreTestRunner에서 살아남는 검사만 옮겼습니다.
    /// </summary>
    public static class PartTimeJobTestRunner
    {
        private static int passed;
        private static int failed;
        private static StringBuilder log;

        [MenuItem("FXOverdose/Debug/Part-Time Job Test")]
        public static void Run()
        {
            passed = 0;
            failed = 0;
            log = new StringBuilder();

            TestGiftsMatchItemAssets();
            TestGrantItem();

            string summary = $"[알바 테스트] 통과 {passed} / 실패 {failed}\n{log}";
            if (failed > 0) Debug.LogError(summary);
            else Debug.Log(summary);
        }

        // 1. 선물 ID가 ItemData 에셋에 실제로 있는지 — 없으면 지급이 조용히 증발합니다. (R7)
        //    결과 모달에 쓰는 이름도 에셋의 표시 이름과 같아야 합니다.
        private static void TestGiftsMatchItemAssets()
        {
            Dictionary<string, string> names = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && !string.IsNullOrEmpty(item.ItemId)) names[item.ItemId] = item.ItemName;
            }

            Check(names.Count > 0, "프로젝트에 ItemData 에셋이 존재");
            Check(WorldMapManager.JobGiftChance > 0f && WorldMapManager.JobGiftChance <= 1f,
                $"선물 확률이 0~1 사이 (실제 {WorldMapManager.JobGiftChance})");

            foreach (var gift in WorldMapManager.JobGifts)
            {
                bool exists = names.TryGetValue(gift.ItemId, out string assetName);
                Check(exists, $"선물 ID '{gift.ItemId}' 가 ItemData 에셋에 존재");
                if (exists) Check(assetName == gift.Name, $"선물 '{gift.ItemId}' 표시 이름 '{gift.Name}' == 에셋 '{assetName}'");
            }
        }

        // 2. 아이템 지급이 같은 ID로 합산되는지
        private static void TestGrantItem()
        {
            SaveLoadManager manager = Object.FindAnyObjectByType<SaveLoadManager>(FindObjectsInactive.Include);
            if (manager == null || manager.CurrentData == null)
            {
                log.AppendLine("- (건너뜀) SaveLoadManager 인스턴스가 없어 GrantItemToSave를 검사하지 못했습니다. 플레이 중 실행하십시오.");
                return;
            }

            int before = manager.CurrentData.InventoryItemIds.Count;
            manager.GrantItemToSave("__test_item", 1);
            manager.GrantItemToSave("__test_item", 2);

            int index = manager.CurrentData.InventoryItemIds.IndexOf("__test_item");
            Check(index >= 0, "지급한 아이템이 목록에 존재");
            Check(manager.CurrentData.InventoryItemIds.Count == before + 1, "같은 ID가 행을 하나만 차지");
            Check(index >= 0 && manager.CurrentData.InventoryItemQuantities[index] == 3, "수량이 합산됨 (1 + 2 = 3)");

            if (index >= 0)
            {
                manager.CurrentData.InventoryItemIds.RemoveAt(index);
                manager.CurrentData.InventoryItemQuantities.RemoveAt(index);
            }
        }

        private static void Check(bool condition, string label)
        {
            if (condition)
            {
                passed++;
                log.AppendLine($"- OK   {label}");
                return;
            }
            failed++;
            log.AppendLine($"- FAIL {label}");
        }
    }
}
