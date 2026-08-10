import re

with open('Assets/Scripts/AI/AIVisualController.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Put CalculateCurrentRoe back before HandleAIDecisionMade
roe_method = '''
        private float CalculateCurrentRoe()
        {
            if (tradingController == null ||
                tradingController.CurrentPosition == TradingController.PositionType.None ||
                tradingController.MarginAmount <= 0f)
            {
                return 0f;
            }

            float currentPrice = FindAnyObjectByType<MarketSimulationEngine>()?.CurrentPrice ?? tradingController.EntryPrice;
            float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long
                ? currentPrice - tradingController.EntryPrice
                : tradingController.EntryPrice - currentPrice;
            float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
            return pnl / tradingController.MarginAmount * 100f;
        }
'''

content = content.replace("private void HandleAIDecisionMade", roe_method + "\n        private void HandleAIDecisionMade")

# Put proxy methods at the end of the class
proxy_methods = '''
        public void BeginSkillUpgradeVisual(SkillType type) => yomiSpriteController?.BeginSkillUpgradeVisual(type);
        public void EndSkillUpgradeVisual() => yomiSpriteController?.EndSkillUpgradeVisual();
        public void ShowEmotion(TraderEmotion emotion, float duration = 4f) => yomiSpriteController?.ShowEmotion(emotion, duration);
        public void ShowPositionOpen(TradingController.PositionType type, float duration = 1f) => yomiSpriteController?.ShowPositionOpen(type, duration);
        public void ShowItemUse(string itemId, float duration = 1f) => yomiSpriteController?.ShowItemUse(itemId, duration);
'''
content = content.replace("    }\n}\n", proxy_methods + "    }\n}\n")

with open('Assets/Scripts/AI/AIVisualController.cs', 'w', encoding='utf-8') as f:
    f.write(content)

