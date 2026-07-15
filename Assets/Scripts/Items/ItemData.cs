using UnityEngine;

// Unity Project 창에서 아이템 에셋을 생성할 수 있게 설정
[CreateAssetMenu(
    fileName = "NewItem",
    menuName = "FX Overdose/Item Data"
)]
public class ItemData : ScriptableObject
{
    // 아이템이 어떤 능력치에 영향을 주는지 구분
    public enum EffectType
    {
        Health,           // 체력 회복 (소모형)
        Mental,           // 멘탈 회복 (소모형)
        ProfitBoost,      // 수익률 추가 보정 (액티브 - 최대 2회 구매, 중첩)
        LossReduction,    // 손해 감소 보정 (액티브 - 최대 2회 구매, 중첩)
        MentalDrainGuard, // 멘탈 감소치 보정 (액티브 - 단회 영구)
        HealthDrainGuard  // 체력 감소치 보정 (액티브 - 단회 영구)
    }

    [Header("기본 정보")]
    [SerializeField] private string itemId;          // 아이템 고유 ID
    [SerializeField] private string itemName;        // 화면에 표시할 이름

    [TextArea]
    [SerializeField] private string description;     // 아이템 설명

    [SerializeField] private Sprite icon;             // 아이템 아이콘

    [Header("사용 효과 (소모형: 회복량 / 액티브: 보정 비율 또는 증가량)")]
    [SerializeField] private EffectType effectType;   // 체력, 멘탈 또는 액티브 보정
    [SerializeField] private float effectAmount = 10f;// 회복량 또는 보정치 (예: 10이면 +10% 또는 10점)

    [Header("상점 정보")]
    [SerializeField] private int price = 100;         // 아이템 가격

    [Header("액티브 아이템 설정 (패시브 버프/업그레이드)")]
    [Tooltip("true이면 구매 즉시 활성화되는 액티브 아이템입니다.")]
    [SerializeField] private bool isActiveItem = false;
    [Tooltip("최대 구매 가능 횟수(레벨)입니다. 1이면 단회 영구 적용, 2이면 1회 재구매(최대 LV.2)까지 가능합니다.")]
    [SerializeField, Min(1)] private int maxLevel = 1;
    [Tooltip("재구매(업그레이드) 시 적용되는 가격 증가 배율입니다.")]
    [SerializeField] private float priceMultiplierPerLevel = 1.5f;

    // 다른 스크립트에서 아이템 정보를 읽을 수 있도록 공개
    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public EffectType Type => effectType;
    public float EffectAmount => effectAmount;
    public int Price => price;
    public bool IsActiveItem => isActiveItem;
    public int MaxLevel => maxLevel;
    public float PriceMultiplierPerLevel => priceMultiplierPerLevel;
}