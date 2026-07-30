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
            DisplayName = "STANDARD",
            Description = "[기본 외형]\n능력치 보너스 없음",
            Price = 0,
            IconResourcePath = "Characters/States/Standard",
            SpriteRoot = "Characters"
        },
        new()
        {
            Id = BunnyGirlId,
            DisplayName = "BUNNY GIRL",
            Description = "[효과: 자동매매 익절 시 수익금 +15%]",
            Price = 200,
            IconResourcePath = "Characters/Costumes/BunnyGirl",
            SpriteRoot = "Characters/Costumes/BunnyGirl"
        },
        new()
        {
            Id = BikiniId,
            DisplayName = "WHITE BIKINI",
            Description = "[효과: 수동매매 익절 시 수익금 +15%]",
            Price = 400,
            IconResourcePath = "Characters/Costumes/Bikini",
            SpriteRoot = "Characters/Costumes/Bikini"
        },
        new()
        {
            Id = JiraiKeiId,
            DisplayName = "JIRAI KEI",
            Description = "[효과: 전체 익절 시 수익금 +20% 및 체력/멘탈 +10 회복]\n[패널티: 모든 멘탈 데미지 1.25배 가속]",
            Price = 600,
            IconResourcePath = "Characters/Costumes/JiraiKei_TwinTails",
            SpriteRoot = "Characters/Costumes/JiraiKei_TwinTails"
        },
        new()
        {
            Id = StreetCapId,
            DisplayName = "STREET CAP",
            Description = "[효과: 최대 체력 +30 확장]",
            Price = 800,
            IconResourcePath = "Characters/Costumes/StreetCap",
            SpriteRoot = "Characters/Costumes/StreetCap"
        },
        new()
        {
            Id = QipaoId,
            DisplayName = "BLACK QIPAO",
            Description = "[효과: 배달음식 체력 및 멘탈 회복량 +15%]",
            Price = 1000,
            IconResourcePath = "Characters/Costumes/Qipao",
            SpriteRoot = "Characters/Costumes/Qipao"
        },
        new()
        {
            Id = PajamaId,
            DisplayName = "MIDNIGHT PAJAMA",
            Description = "[외형 전용]\n달과 별이 수놓인 파스텔 네이비 잠옷",
            Price = 1200,
            IconResourcePath = "Characters/Costumes/Pajama",
            SpriteRoot = "Characters/Costumes/Pajama"
        },
        new()
        {
            Id = OfficeLookId,
            DisplayName = "OFFICE LOOK",
            Description = "[외형 전용]\n아이보리 블라우스와 네이비 펜슬스커트",
            Price = 1400,
            IconResourcePath = "Characters/Costumes/OfficeLook",
            SpriteRoot = "Characters/Costumes/OfficeLook"
        },
        new()
        {
            Id = BartenderId,
            DisplayName = "BARTENDER",
            Description = "[외형 전용]\n화이트 셔츠와 블랙 베스트의 클래식 바텐더 룩",
            Price = 1600,
            IconResourcePath = "Characters/Costumes/Bartender",
            SpriteRoot = "Characters/Costumes/Bartender"
        },
        new()
        {
            Id = MaidId,
            DisplayName = "CLASSIC MAID",
            Description = "[외형 전용]\n프릴 앞치마와 메리제인 슈즈의 클래식 메이드 룩",
            Price = 1800,
            IconResourcePath = "Characters/Costumes/Maid",
            SpriteRoot = "Characters/Costumes/Maid"
        },
        new()
        {
            Id = NurseId,
            DisplayName = "PINK NURSE",
            Description = "[외형 전용]\n슬림핏 파스텔 핑크 간호사 룩",
            Price = 2000,
            IconResourcePath = "Characters/Costumes/Nurse",
            SpriteRoot = "Characters/Costumes/Nurse"
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
