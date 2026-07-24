using UnityEngine;


namespace FXOverdose.AI
{
    public static class TraderEmotionEvaluator
    {
        public static TraderEmotion Evaluate(float roe, TraderStatus.MentalState mentalState, float healthRatio, EventCategory category, string extraContext)
        {
            var gm = Object.FindAnyObjectByType<GameManager>();
            bool isGameOverState = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
            bool isLiquidationContext = extraContext != null && (extraContext.Contains("강제청산") || extraContext.Contains("게임오버") || extraContext.Contains("파산") || extraContext.Contains("청산 소진") || extraContext.Contains("Overdose 확정") || extraContext.Contains("연쇄 붕괴"));

            // 1. 극단 상태 및 게임오버
            if (isGameOverState || isLiquidationContext)
            {
                if (extraContext != null && extraContext.Contains("2단계")) return TraderEmotion.Vengeful;
                if (extraContext != null && extraContext.Contains("3단계")) return TraderEmotion.Obsessive;
                return TraderEmotion.Tearful;
            }

            if (mentalState == TraderStatus.MentalState.Overdose)
            {
                return TraderEmotion.Manic;
            }

            // 2. 체력 고갈 상태
            if (healthRatio <= 0.1f)
            {
                return TraderEmotion.Exhausted;
            }

            // 3. 이벤트 특수 상황
            if (category == EventCategory.ItemUsed)
            {
                return TraderEmotion.Relieved;
            }
            if (category == EventCategory.GimmickTriggered && extraContext != null && (extraContext.Contains("FOMO") || extraContext.Contains("놓친") || extraContext.Contains("휩소")))
            {
                return TraderEmotion.Regretful;
            }
            if (category == EventCategory.SkillUpgraded)
            {
                return TraderEmotion.Confident;
            }
            if (extraContext != null && (extraContext.Contains("조기 종료") || extraContext.Contains("미리") || extraContext.Contains("이벤트") || extraContext.Contains("포지션 종료")))
            {
                if (category == EventCategory.ChartMovement)
                {
                    return TraderEmotion.Regretful;
                }
            }

            // 포지션이 없는 상태인지 판별 (ROE = 0 이고 무포지션으로 가정하거나, 밖에서 무포지션일 때 ROE = 0f로 들어옴)
            var tradingCtrl = Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            bool hasPosition = tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;

            // 4. ROE 및 MentalState 복합 판별
            if (!hasPosition)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Despairing;
                if (mentalState == TraderStatus.MentalState.Anxious) return TraderEmotion.Anxious;
                return TraderEmotion.Focused; 
            }

            if (roe > 30f)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Manic; // 기분이 매우 나쁜데 수익이 대박이면 광기로 변함
                return TraderEmotion.Euphoria;
            }
            else if (roe > 5f)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Panicked; 
                if (mentalState == TraderStatus.MentalState.Anxious) return TraderEmotion.Pleased;
                return TraderEmotion.Confident;
            }
            else if (roe > 0f)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Despairing;
                if (mentalState == TraderStatus.MentalState.Anxious) return TraderEmotion.Anxious;
                return TraderEmotion.Focused;
            }
            else if (roe > -5f)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Despairing;
                if (mentalState == TraderStatus.MentalState.Anxious) return TraderEmotion.Anxious;
                return TraderEmotion.Suspicious;
            }
            else if (roe > -20f)
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Panicked;
                return TraderEmotion.Frustrated;
            }
            else
            {
                if (mentalState == TraderStatus.MentalState.Danger) return TraderEmotion.Tearful;
                return TraderEmotion.Panicked;
            }
        }
    }
}

