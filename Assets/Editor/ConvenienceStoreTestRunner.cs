using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Core;
using FXOverdose.DatingSim.Store;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 편의점 알바의 순수 계산과 데이터 정합성을 검사합니다.
    ///
    /// asmdef를 새로 만들지 않는 이유: 이 프로젝트에서 EditMode 테스트는 FXOverdose.P2P.Core 하나뿐이고,
    /// 미니게임 하나를 위해 어셈블리를 쪼개는 비용이 얻는 것보다 큽니다. 기존
    /// AITradingSystemTestRunner와 같은 에디터 메뉴 방식을 따릅니다.
    /// </summary>
    public static class ConvenienceStoreTestRunner
    {
        private static int passed;
        private static int failed;
        private static StringBuilder log;

        [MenuItem("FXOverdose/Debug/Convenience Store Shift Test")]
        public static void Run()
        {
            passed = 0;
            failed = 0;
            log = new StringBuilder();

            StoreConfig config = StoreConfig.Load();

            TestConfig(config);
            TestGiftIdsExistInCatalog(config);
            TestSettle(config);
            TestGrantItem();
            TestTierSelection(config);

            string summary = $"[편의점 테스트] 통과 {passed} / 실패 {failed}\n{log}";
            if (failed > 0) Debug.LogError(summary);
            else Debug.Log(summary);
        }

        // 1. 설정 로드와 테이블 기본값
        private static void TestConfig(StoreConfig config)
        {
            Check(config != null, "StoreConfig 로드 성공");
            Check(config.shelves.Count == 5, $"매대 5개 (실제 {config.shelves.Count})");
            Check(config.tiers.Count == 4, $"tier 4개 (실제 {config.tiers.Count})");
            Check(config.gifts.Count == 2, $"선물 2종 (실제 {config.gifts.Count})");
            Check(config.minPayRatio > 0f && config.minPayRatio < 1f, "minPayRatio가 0~1 사이");
        }

        // 2. 선물 ID가 상점 카탈로그에 실제로 있는지 — 없으면 지급이 조용히 증발합니다. (R7)
        private static void TestGiftIdsExistInCatalog(StoreConfig config)
        {
            List<string> catalogIds = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && !string.IsNullOrEmpty(item.ItemId)) catalogIds.Add(item.ItemId);
            }

            Check(catalogIds.Count > 0, "프로젝트에 ItemData 에셋이 존재");
            for (int i = 0; i < config.gifts.Count; i++)
            {
                string id = config.gifts[i].itemId;
                Check(catalogIds.Contains(id), $"선물 ID '{id}' 가 ItemData 에셋에 존재");
            }
        }

        // 3. 정산 경계값 — 매출 제거의 회귀 방어 포함
        private static void TestSettle(StoreConfig config)
        {
            const float basePay = 1200f;

            StoreShiftTally perfect = new() { TotalCustomers = 10, CheckedOut = 10 };
            StoreShiftResult perfectResult = StoreShiftManager.Settle(perfect, basePay, config);
            Check(perfectResult.Grade == 'S', $"무결점 근무 → S (실제 {perfectResult.Grade})");
            Check(Mathf.Approximately(perfectResult.Pay, basePay),
                $"무결점 근무 일급 == 기본급, 초과 지급 없음 (실제 {perfectResult.Pay})");

            StoreShiftTally disaster = new()
            {
                TotalCustomers = 20, CheckedOut = 0, Walkouts = 20,
                RemainingDirt = 10, EmptyShelves = 5, RemainingLeftovers = 10
            };
            StoreShiftResult disasterResult = StoreShiftManager.Settle(disaster, basePay, config);
            Check(disasterResult.Pay >= 0f, "전멸 근무에도 일급이 음수가 아님");
            Check(Mathf.Approximately(disasterResult.Pay, basePay * config.minPayRatio),
                $"전멸 근무 일급 == 하한 (실제 {disasterResult.Pay})");
            Check(disasterResult.Grade == 'D', $"전멸 근무 → D (실제 {disasterResult.Grade})");

            StoreShiftTally empty = new() { TotalCustomers = 0 };
            StoreShiftResult emptyResult = StoreShiftManager.Settle(empty, basePay, config);
            Check(emptyResult.Grade == 'S', "손님 0명 → 0 나누기 없이 S");
            Check(Mathf.Approximately(emptyResult.Pay, basePay), "손님 0명 → 기본급 전액");
        }

        // 4. 아이템 지급이 같은 ID로 합산되는지
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

        // 5. 누적 근무 횟수 → tier 선택
        private static void TestTierSelection(StoreConfig config)
        {
            Check(config.GetTier(0).minTotalShifts == 0, "0회 → tier 0");
            Check(config.GetTier(4).minTotalShifts == 0, "4회 → tier 0");
            Check(config.GetTier(5).minTotalShifts == 5, "5회 → tier 1");
            Check(config.GetTier(30).minTotalShifts == 25, "30회 → tier 3");
            Check(config.GetTier(-1).maxConcurrent > 0, "음수 입력에도 안전한 기본값");
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
