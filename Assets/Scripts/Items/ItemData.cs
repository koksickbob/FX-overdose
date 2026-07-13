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
        Health, // 체력 회복
        Mental  // 멘탈 회복
    }

    [Header("기본 정보")]
    [SerializeField] private string itemId;          // 아이템 고유 ID
    [SerializeField] private string itemName;        // 화면에 표시할 이름

    [TextArea]
    [SerializeField] private string description;     // 아이템 설명

    [SerializeField] private Sprite icon;             // 아이템 아이콘

    [Header("사용 효과")]
    [SerializeField] private EffectType effectType;   // 체력 또는 멘탈
    [SerializeField] private float effectAmount = 10f;// 회복량

    [Header("상점 정보")]
    [SerializeField] private int price = 100;         // 아이템 가격

    // 다른 스크립트에서 아이템 정보를 읽을 수 있도록 공개
    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
    public EffectType Type => effectType;
    public float EffectAmount => effectAmount;
    public int Price => price;
}