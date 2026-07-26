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
            Description = "Yomi's default white tee and dolphin shorts.",
            Price = 0,
            IconResourcePath = "Characters/States/Standard",
            SpriteRoot = "Characters"
        },
        new()
        {
            Id = BunnyGirlId,
            DisplayName = "BUNNY GIRL",
            Description = "Black bunny suit, ribbon, cuffs, stockings and heels.",
            Price = 200,
            IconResourcePath = "Characters/Costumes/BunnyGirl",
            SpriteRoot = "Characters/Costumes/BunnyGirl"
        },
        new()
        {
            Id = BikiniId,
            DisplayName = "WHITE BIKINI",
            Description = "White two-piece bikini with a clean summer look.",
            Price = 400,
            IconResourcePath = "Characters/Costumes/Bikini",
            SpriteRoot = "Characters/Costumes/Bikini"
        },
        new()
        {
            Id = JiraiKeiId,
            DisplayName = "JIRAI KEI",
            Description = "Black-and-pink jirai-kei dress with twin tails and ribbon bows.",
            Price = 600,
            IconResourcePath = "Characters/Costumes/JiraiKei_TwinTails",
            SpriteRoot = "Characters/Costumes/JiraiKei_TwinTails"
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
