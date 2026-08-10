import re

with open('Assets/Scripts/AI/AIVisualController.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

output = []
skip = False
for line in lines:
    if "private const float CostumeOpticalScale" in line:
        continue
    if "private const float CostumeOpticalFootCompensation" in line:
        continue
    if "public enum ExpressionState" in line:
        skip = True
    if skip and "        }" in line:
        skip = False
        continue
    if skip:
        continue
        
    output.append(line)

content = "".join(output)

# Replace fields
fields_to_remove = [
    r'\[SerializeField\] private Animator characterAnimator;\n',
    r'\[SerializeField\] private Image characterImage;\n',
    r'\[SerializeField\] private GameObject dangerAuraEffect;.*\n',
    r'\[SerializeField, Min\(0f\)\] private float contextualEmotionDuration.*\n',
    r'\[SerializeField\] private TraderEmotion currentEmotion.*\n',
    r'private readonly Dictionary<TraderEmotion, Sprite> emotionSprites.*\n',
    r'private readonly Dictionary<string, Sprite> itemUseSprites.*\n',
    r'private readonly Dictionary<SkillType, Sprite> skillUpgradeSprites.*\n',
    r'private readonly Dictionary<TradingController.PositionType, Sprite> positionSprites.*\n',
    r'private readonly Dictionary<Sprite, float> costumeSpriteScales.*\n',
    r'private readonly Dictionary<Sprite, Vector2> costumeSpriteOffsets.*\n',
    r'private Sprite overdoseStateSprite;\n',
    r'private float emotionOverrideUntil;\n',
    r'private bool isItemUseVisualActive;\n',
    r'private float itemUseVisualUntil;\n',
    r'private bool isSkillUpgradeVisualActive;\n',
    r'private bool isPositionVisualActive;\n',
    r'private float positionVisualUntil;\n',
    r'private bool isOverdoseStateVisualActive;\n',
    r'private string currentItemUseId;\n',
    r'private SkillType currentSkillUpgradeType;\n',
    r'private TradingController.PositionType currentPositionVisualType;\n',
    r'private bool characterBaseScaleCaptured;\n',
    r'private Vector3 characterBaseScale.*\n',
    r'private Vector2 characterBaseAnchoredPosition;\n'
]
for p in fields_to_remove:
    content = re.sub(p, '', content)

content = content.replace("private bool visualLayoutOffsetApplied;", 
    "[Header(\"외부 연결\")]\n        [SerializeField] private YomiSpriteController yomiSpriteController;\n\n        private bool visualLayoutOffsetApplied;")

# Replace in Start()
content = content.replace("ResolveCharacterImage();", "if (yomiSpriteController == null) yomiSpriteController = GetComponent<YomiSpriteController>();")
content = re.sub(r'LoadEmotionSprites\(\);\n\s*LoadItemUseSprites\(\);\n\s*LoadSkillUpgradeSprites\(\);\n\s*LoadPositionSprites\(\);\n\s*overdoseStateSprite = LoadCostumeSprite\("States", "Overdose"\);\n\s*ApplyEmotion\(currentEmotion, true\);\n\s*ApplyCostumeVisualScale\(\);\n', '', content)
content = content.replace("if (CostumeManager.Instance != null)\n            {\n                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;\n                CostumeManager.Instance.OnCostumesChanged += HandleCostumeChanged;\n            }", "")
content = content.replace("if (inventory != null)\n            {\n                inventory.ItemConsumed -= HandleItemConsumed;\n                inventory.ItemConsumed += HandleItemConsumed;\n            }", "")
content = content.replace("if (dangerAuraEffect != null) dangerAuraEffect.SetActive(false);", "")

# Replace in OnDestroy()
content = content.replace("if (CostumeManager.Instance != null)\n                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;\n            if (inventory != null)\n            {\n                inventory.ItemConsumed -= HandleItemConsumed;\n            }", "")

# Remove ApplyCharacterDialogueVerticalOffset contents
content = re.sub(r'private void ApplyCharacterDialogueVerticalOffset\(\)\s*\{[\s\S]*?visualLayoutOffsetApplied = true;\n\s*\}', '', content)
content = content.replace("ApplyCharacterDialogueVerticalOffset();", "")

# Remove Update and UpdateExpressionState
content = re.sub(r'private void Update\(\)\s*\{\s*UpdateExpressionState\(\);\s*\}\s*// 실시간 수익률 및 멘탈 상태를 기반으로 감정을 도출하고 표정 상태로 매핑\s*private void UpdateExpressionState\(\)\s*\{[\s\S]*?ApplyEmotion\(evaluatedEmotion\);\s*\}', '', content)

# Remove CalculateCurrentRoe
content = re.sub(r'private float CalculateCurrentRoe\(\)\s*\{[\s\S]*?return pnl / tradingController\.MarginAmount \* 100f;\n\s*\}', '', content)
# Wait, CalculateCurrentRoe is used by HandleAIDecisionMade! So let's not remove it completely. Or better, just restore it.
# Actually I'll let regex fail if it matches too much, so I'll just skip removing CalculateCurrentRoe. We'll leave it in AIVisualController.

# Actually let's just define the exact methods to remove using regex
methods_to_remove = [
    r'private void Update\(\)[\s\S]*?(?=private float CalculateCurrentRoe)',
    r'private void ResolveCharacterImage\(\)[\s\S]*?(?=private void HandleAIDecisionMade)',
    # HandleCostumeChanged is inside the above block
]
for p in methods_to_remove:
    content = re.sub(p, '', content)

# In StartOrPreemptDialogue:
# replace ShowEmotion(...) with yomiSpriteController?.ShowEmotion(...)
content = re.sub(r'ShowEmotion\(contextualEmotion, Mathf\.Max\(contextualEmotionDuration, displayDuration\)\);', 
                 'yomiSpriteController?.ShowEmotion(contextualEmotion, Mathf.Max(4f, displayDuration));', content)

with open('Assets/Scripts/AI/AIVisualController.cs', 'w', encoding='utf-8') as f:
    f.write(content)

