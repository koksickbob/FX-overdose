using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.DatingSim.Store
{
    /// <summary>매대 한 종류. 판매가는 없습니다 — 요미는 시급제라 일급이 매출과 무관합니다.</summary>
    [Serializable]
    public struct ShelfEntry
    {
        public string shelfId;
        public string displayName;
        public int maxStock;
        public int initialStock;
        public float refillHoldSeconds;
        public int unitsPerRefill;
    }

    /// <summary>누적 근무 횟수에 따른 손님 밀도. 위에서부터 훑어 minTotalShifts를 넘긴 마지막 항목이 선택됩니다.</summary>
    [Serializable]
    public struct CustomerTier
    {
        public int minTotalShifts;
        public float spawnIntervalSeconds;
        public int maxConcurrent;
        public float patienceSeconds;
        public int minItems;
        public int maxItems;
    }

    /// <summary>랜덤 인카운트 손님이 주는 선물. itemId는 ItemData 에셋의 itemId와 반드시 일치해야 합니다.</summary>
    [Serializable]
    public struct GiftEntry
    {
        public string itemId;
        public int weight;
        public int amount;
    }

    /// <summary>
    /// 편의점 알바 타이쿤의 모든 수치. Resources에서 로드하되, 에셋이 없으면 이 클래스의 기본값으로 동작합니다.
    ///
    /// 씬을 런타임 빌더가 통째로 생성하므로 인스펙터에 손으로 꽂은 참조는 매번 사라집니다.
    /// 그래서 씬 참조가 아니라 Resources 경로 하나로만 접근합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "ConvenienceStoreConfig", menuName = "FX Overdose/Convenience Store Config")]
    public sealed class StoreConfig : ScriptableObject
    {
        public const string ResourcePath = "Store/ConvenienceStoreConfig";

        [Header("근무")]
        public float shiftDurationSeconds = 180f;
        public float introSeconds = 3f;
        public int timeSlotCost = 2;
        [Tooltip("감점이 아무리 커도 기본급 × 이 값 아래로는 내려가지 않습니다.")]
        public float minPayRatio = 0.3f;

        [Header("조작")]
        public float moveSpeed = 4.2f;
        public float interactRadius = 1.4f;
        [Tooltip("창고에서 한 번에 챙기는 재고 유닛 수")]
        public int stockUnitsPerPickup = 6;

        [Header("액션 홀드 시간(초)")]
        public float checkoutBaseSeconds = 1.2f;
        public float checkoutPerItemSeconds = 0.5f;
        public float cleanSeconds = 4f;
        public float leftoverSeconds = 2.5f;

        [Header("감점")]
        public float walkoutPenalty = 60f;
        public float dirtPenaltyPerSpot = 25f;
        public float emptyShelfPenalty = 20f;
        public float leftoverPenalty = 15f;
        [Range(0f, 1f)] public float dirtSpawnChance = 0.15f;

        [Header("선물")]
        [Range(0f, 1f)] public float giftChance = 0.05f;
        public int dailyGiftCap = 3;

        [Header("테이블")]
        public List<ShelfEntry> shelves = new List<ShelfEntry>();
        public List<CustomerTier> tiers = new List<CustomerTier>();
        public List<GiftEntry> gifts = new List<GiftEntry>();

        private static StoreConfig cached;

        /// <summary>에셋이 있으면 그것을, 없으면 기본값 인스턴스를 돌려줍니다. 절대 null을 반환하지 않습니다.</summary>
        public static StoreConfig Load()
        {
            if (cached != null) return cached;

            cached = Resources.Load<StoreConfig>(ResourcePath);
            if (cached == null)
            {
                // 에셋 없이도 플레이 가능해야 합니다. 밸런싱을 하려는 사람만 에셋을 만들면 됩니다.
                cached = CreateInstance<StoreConfig>();
                cached.name = "ConvenienceStoreConfig (기본값)";
            }
            cached.EnsureDefaults();
            return cached;
        }

        /// <summary>테이블이 비어 있으면 기본값을 채웁니다. 반쯤 채워진 에셋도 그대로 존중합니다.</summary>
        public void EnsureDefaults()
        {
            if (shelves.Count == 0)
            {
                shelves.Add(NewShelf("shelf_drink", "음료 코너"));
                shelves.Add(NewShelf("shelf_snack", "과자 코너"));
                shelves.Add(NewShelf("shelf_lunch", "도시락 코너"));
                shelves.Add(NewShelf("shelf_daily", "생활용품 코너"));
                shelves.Add(NewShelf("shelf_ice", "아이스크림 코너"));
            }

            if (tiers.Count == 0)
            {
                tiers.Add(new CustomerTier { minTotalShifts = 0, spawnIntervalSeconds = 12f, maxConcurrent = 3, patienceSeconds = 40f, minItems = 1, maxItems = 2 });
                tiers.Add(new CustomerTier { minTotalShifts = 5, spawnIntervalSeconds = 9.5f, maxConcurrent = 4, patienceSeconds = 35f, minItems = 1, maxItems = 3 });
                tiers.Add(new CustomerTier { minTotalShifts = 12, spawnIntervalSeconds = 7.5f, maxConcurrent = 5, patienceSeconds = 30f, minItems = 2, maxItems = 3 });
                tiers.Add(new CustomerTier { minTotalShifts = 25, spawnIntervalSeconds = 6f, maxConcurrent = 5, patienceSeconds = 26f, minItems = 2, maxItems = 4 });
            }

            if (gifts.Count == 0)
            {
                // itemId는 Assets/Data/Items/ 의 에셋 값입니다. 오타는 조용한 아이템 증발로 이어집니다.
                gifts.Add(new GiftEntry { itemId = "energy_drink", weight = 50, amount = 1 });
                gifts.Add(new GiftEntry { itemId = "dessert", weight = 50, amount = 1 });
            }
        }

        private static ShelfEntry NewShelf(string id, string label)
        {
            return new ShelfEntry
            {
                shelfId = id,
                displayName = label,
                maxStock = 8,
                initialStock = 5,
                refillHoldSeconds = 3f,
                unitsPerRefill = 2
            };
        }

        /// <summary>누적 근무 횟수에 해당하는 tier. 테이블이 비어 있어도 안전한 값을 돌려줍니다.</summary>
        public CustomerTier GetTier(int totalShifts)
        {
            CustomerTier best = default;
            bool found = false;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].minTotalShifts > totalShifts) continue;
                if (found && tiers[i].minTotalShifts < best.minTotalShifts) continue;
                best = tiers[i];
                found = true;
            }

            if (!found)
                return new CustomerTier { spawnIntervalSeconds = 12f, maxConcurrent = 3, patienceSeconds = 40f, minItems = 1, maxItems = 2 };
            return best;
        }

        /// <summary>가중치 추첨. 테이블이 비어 있으면 amount 0인 항목을 돌려줍니다(지급 없음).</summary>
        public GiftEntry RollGift()
        {
            int total = 0;
            for (int i = 0; i < gifts.Count; i++) total += Mathf.Max(0, gifts[i].weight);
            if (total <= 0) return default;

            int roll = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < gifts.Count; i++)
            {
                roll -= Mathf.Max(0, gifts[i].weight);
                if (roll < 0) return gifts[i];
            }
            return gifts[gifts.Count - 1];
        }
    }
}
