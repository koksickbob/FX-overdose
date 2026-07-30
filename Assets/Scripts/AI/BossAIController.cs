using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.AI
{
    [RequireComponent(typeof(AITradingBrain))]
    public class BossAIController : MonoBehaviour
    {
        private AITradingBrain brain;
        private BossLevelProvider levelProvider;
        
        private void Awake()
        {
            brain = GetComponent<AITradingBrain>();
            brain.IsBossAI = true; // 체력/멘탈 소모 끄기 (무적 기믹)

            brain.GetAvailableBalance = () => BossManager.Instance != null ? BossManager.Instance.BossCurrentAsset : 0f;
            
            brain.TradeExecutor = (posType, margin, lev, tgt, sl, crazy, price) => 
            {
                if (BossManager.Instance == null || BossManager.Instance.IsBossBankrupt) return false;
                
                // 보스 모의 매매 결과 즉시 처리 (스킬 레벨에 따른 승률 적용)
                float accuracy = levelProvider != null ? levelProvider.GetSignalAccuracy() : 0.5f;
                bool isWin = UnityEngine.Random.value < accuracy;
                
                // 레버리지 비례 가상 수익률 계산
                float roiPercentage = isWin 
                    ? UnityEngine.Random.Range(5f, 25f) * (lev / 50f) 
                    : -UnityEngine.Random.Range(5f, 25f) * (lev / 50f);
                
                float pnl = margin * (roiPercentage / 100f);
                
                BossManager.Instance.UpdateBossAsset(BossManager.Instance.BossCurrentAsset + pnl);
                
                return true; // 가상 포지션 체결 성공으로 AITradingBrain 로직이 이어나갈 수 있게 함
            };
        }

        public void InitializeForBoss(BossData bossData, float startingAsset)
        {
            levelProvider = new BossLevelProvider(bossData.SkillLevel);
            brain.SetLevelProvider(levelProvider);
        }
    }
}
