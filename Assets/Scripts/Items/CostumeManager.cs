using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>요미 코스튬의 구매, 보유, 장착 상태와 리소스 경로를 관리합니다.</summary>
public class CostumeManager : MonoBehaviour
{
    public const string StandardId = "standard";
    public const string BunnyGirlId = "bunny_girl";
    public const string BikiniId = "bikini";
    public const string JiraiKeiId = "jirai_kei";
    public const string StreetCapId = "street_cap";
    public const string QipaoId = "qipao";
    public const string PajamaId = "pajama";
    public const string OfficeLookId = "office_look";
    public const string BartenderId = "bartender";
    public const string MaidId = "maid";
    public const string NurseId = "nurse";
    public const string DongtanLookId = "dongtan_look";
    public const string HanbokId = "hanbok";
    public const string YukataId = "yukata";

    [Serializable]
    public sealed class CostumeDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public int Price;
        public string IconResourcePath;
        public string SpriteRoot;
    }

    private static readonly CostumeDefinition[] Definitions =
    {
        new()
        {
            Id = StandardId,
            DisplayName = "기본 복장",
            Description = "[기본 외형]\n능력치 보너스 없음",
            Price = 0,
            IconResourcePath = "Characters/States/Standard",
            SpriteRoot = "Characters"
        },
        new()
        {
            Id = BunnyGirlId,
            DisplayName = "바니걸",
            Description = "[효과: 자동매매 익절 시 수익금 +15%]",
            Price = 1000000,
            IconResourcePath = "Characters/Costumes/BunnyGirl",
            SpriteRoot = "Characters/Costumes/BunnyGirl"
        },
        new()
        {
            Id = BikiniId,
            DisplayName = "화이트 비키니",
            Description = "[효과: 수동매매 익절 시 수익금 +15%]",
            Price = 1000000,
            IconResourcePath = "Characters/Costumes/Bikini",
            SpriteRoot = "Characters/Costumes/Bikini"
        },
        new()
        {
            Id = JiraiKeiId,
            DisplayName = "지뢰계 패션",
            Description = "[효과: 전체 익절 시 수익금 +20% 및 체력/멘탈 +10 회복]\n[패널티: 모든 멘탈 데미지 1.25배 가속]",
            Price = 80000,
            IconResourcePath = "Characters/Costumes/JiraiKei_TwinTails",
            SpriteRoot = "Characters/Costumes/JiraiKei_TwinTails"
        },
        new()
        {
            Id = StreetCapId,
            DisplayName = "스트리트 볼캡",
            Description = "[효과: 최대 체력 +30 확장]",
            Price = 5000,
            IconResourcePath = "Characters/Costumes/StreetCap",
            SpriteRoot = "Characters/Costumes/StreetCap"
        },
        new()
        {
            Id = QipaoId,
            DisplayName = "블랙 치파오",
            Description = "[효과: 배달음식 체력 및 멘탈 회복량 +15%]",
            Price = 15000,
            IconResourcePath = "Characters/Costumes/Qipao",
            SpriteRoot = "Characters/Costumes/Qipao"
        },
        new()
        {
            Id = PajamaId,
            DisplayName = "미드나잇 잠옷",
            Description = "[효과: 최대 멘탈 +15 확장]",
            Price = 12000,
            IconResourcePath = "Characters/Costumes/Pajama",
            SpriteRoot = "Characters/Costumes/Pajama"
        },
        new()
        {
            Id = OfficeLookId,
            DisplayName = "오피스룩",
            Description = "[효과: 거래 정확도 5% 상승]",
            Price = 250000,
            IconResourcePath = "Characters/Costumes/OfficeLook",
            SpriteRoot = "Characters/Costumes/OfficeLook"
        },
        new()
        {
            Id = BartenderId,
            DisplayName = "바텐더",
            Description = "[효과: 에너지 드링크 사용 시 체력 회복량 +15%]",
            Price = 30000,
            IconResourcePath = "Characters/Costumes/Bartender",
            SpriteRoot = "Characters/Costumes/Bartender"
        },
        new()
        {
            Id = MaidId,
            DisplayName = "클래식 메이드",
            Description = "[효과: 파르페 사용시 멘탈 회복량 +15%]",
            Price = 30000,
            IconResourcePath = "Characters/Costumes/Maid",
            SpriteRoot = "Characters/Costumes/Maid"
        },
        new()
        {
            Id = NurseId,
            DisplayName = "핑크 간호사",
            Description = "[효과: 영양제, 진정제 효율 15% 증가]",
            Price = 10000,
            IconResourcePath = "Characters/Costumes/Nurse",
            SpriteRoot = "Characters/Costumes/Nurse"
        },
        new()
        {
            Id = DongtanLookId,
            DisplayName = "동탄룩",
            Description = "[효과: 아이템 구매 비용 15% 감소]",
            Price = 150000,
            IconResourcePath = "Characters/Costumes/DongtanLook",
            SpriteRoot = "Characters/Costumes/DongtanLook"
        },
        new()
        {
            Id = HanbokId,
            DisplayName = "전통 한복",
            Description = "[효과: 배달음식 체력 및 멘탈 회복량 +15%]",
            Price = 15000,
            IconResourcePath = "Characters/Costumes/Hanbok",
            SpriteRoot = "Characters/Costumes/Hanbok"
        },
        new()
        {
            Id = YukataId,
            DisplayName = "나팔꽃 유카타",
            Description = "[효과: 배달음식 체력 및 멘탈 회복량 +15%]",
            Price = 15000,
            IconResourcePath = "Characters/Costumes/Yukata",
            SpriteRoot = "Characters/Costumes/Yukata"
        }
    };

    public static CostumeManager Instance { get; private set; }

    private readonly HashSet<string> ownedCostumeIds = new(StringComparer.Ordinal);
    private string equippedCostumeId = StandardId;

    public event Action OnCostumesChanged;

    public IReadOnlyList<CostumeDefinition> Catalog => Definitions;
    public string EquippedCostumeId => equippedCostumeId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureDefaults();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsOwned(string costumeId)
    {
        EnsureDefaults();
        return !string.IsNullOrWhiteSpace(costumeId) && ownedCostumeIds.Contains(costumeId);
    }

    public bool IsEquipped(string costumeId) =>
        string.Equals(equippedCostumeId, costumeId, StringComparison.Ordinal);

    public CostumeDefinition GetDefinition(string costumeId)
    {
        foreach (CostumeDefinition definition in Definitions)
        {
            if (string.Equals(definition.Id, costumeId, StringComparison.Ordinal))
                return definition;
        }
        return null;
    }

    public bool Purchase(string costumeId, GameManager gameManager)
    {
        CostumeDefinition definition = GetDefinition(costumeId);
        if (definition == null || gameManager == null || IsOwned(costumeId)) return false;
        if (FXOverdose.Core.AchievementManager.Instance != null && !FXOverdose.Core.AchievementManager.Instance.IsCostumeUnlocked(costumeId, out _)) return false;
        if (definition.Price < 0 || !gameManager.TrySpendBalance(definition.Price)) return false;

        ownedCostumeIds.Add(costumeId);
        OnCostumesChanged?.Invoke();
        Debug.Log($"[CostumeManager] 코스튬 구매 완료: {definition.DisplayName}");
        return true;
    }

    public bool Equip(string costumeId)
    {
        if (!IsOwned(costumeId) || IsEquipped(costumeId)) return false;
        equippedCostumeId = costumeId;
        OnCostumesChanged?.Invoke();
        Debug.Log($"[CostumeManager] 코스튬 장착: {GetDefinition(costumeId)?.DisplayName ?? costumeId}");
        return true;
    }

    public string GetResourcePath(string category, string spriteName)
    {
        CostumeDefinition equipped = GetDefinition(equippedCostumeId) ?? Definitions[0];
        return $"{equipped.SpriteRoot}/{category}/{spriteName}";
    }

    public List<string> GetOwnedCostumeIds()
    {
        EnsureDefaults();
        return new List<string>(ownedCostumeIds);
    }

    public void Restore(IEnumerable<string> ownedIds, string equippedId)
    {
        ownedCostumeIds.Clear();
        ownedCostumeIds.Add(StandardId);

        if (ownedIds != null)
        {
            foreach (string id in ownedIds)
            {
                if (GetDefinition(id) != null) ownedCostumeIds.Add(id);
            }
        }

        equippedCostumeId = ownedCostumeIds.Contains(equippedId) ? equippedId : StandardId;
        OnCostumesChanged?.Invoke();
    }

    public void ResetAll()
    {
        ownedCostumeIds.Clear();
        ownedCostumeIds.Add(StandardId);
        equippedCostumeId = StandardId;
        OnCostumesChanged?.Invoke();
    }

    private void EnsureDefaults()
    {
        ownedCostumeIds.Add(StandardId);
        if (!ownedCostumeIds.Contains(equippedCostumeId))
            equippedCostumeId = StandardId;
    }
}
