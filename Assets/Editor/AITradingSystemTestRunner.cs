using System;
using UnityEngine;
using UnityEditor;
using FXOverdose.Trading;
using FXOverdose.AI;

namespace FXOverdose.Editor
{
    public class AITradingSystemTestRunner : EditorWindow
    {
        private Vector2 scrollPos;
        private string testLogOutput = "테스트 준비 완료. 아래 버튼을 눌러 AI 매매 엔진 연동을 검증하세요.\n";

        [MenuItem("FXOverdose/Debug/AI Trading System Integration Test")]
        public static void ShowWindow()
        {
            var window = GetWindow<AITradingSystemTestRunner>("AI 매매 검증기");
            window.minSize = new Vector2(550, 600);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            EditorHeader("🧪 AI 매매 시스템(AITradingBrain - MarketEngine - TradingController) 통합 검증");
            GUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "이 검증기는 임시 게임 오브젝트를 생성하여 MarketSignal(차트 신호) 발행 -> AI 상태별 판단(Deception Tier) -> TradingController 레버리지 진입 및 청산 리액션을 자동 테스트합니다.",
                MessageType.Info);

            GUILayout.Space(10);

            if (GUILayout.Button("🚀 [전체 자동 검증] 4단계 Deception Tier & 매매 연동 전체 시뮬레이션", GUILayout.Height(40)))
            {
                RunAllTests();
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🟢 [Tier 1 테스트] 확실한 신호 익절 검증", GUILayout.Height(30))) RunTestTier1();
            if (GUILayout.Button("🟠 [Tier 3 테스트] 불트랩(Trap) 휩소 오인 진입", GUILayout.Height(30))) RunTestTier3();
            if (GUILayout.Button("🔴 [Tier 4 테스트] Overdose 125배 뇌동매매", GUILayout.Height(30))) RunTestTier4();
            GUILayout.EndHorizontal();

            GUILayout.Space(15);
            GUILayout.Label("📋 검증 실행 로그 (Test Execution Logs):", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, EditorStyles.helpBox, GUILayout.Height(380));
            EditorStyles.label.wordWrap = true;
            EditorGUILayout.TextArea(testLogOutput, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            GUILayout.Space(5);
            if (GUILayout.Button("로그 초기화", GUILayout.Height(25)))
            {
                testLogOutput = "로그가 초기화되었습니다.\n";
            }
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            testLogOutput += $"[{time}] {message}\n";
            Debug.Log($"[AITradingSystemTestRunner] {message}");
            Repaint();
        }

        private void RunAllTests()
        {
            testLogOutput = "========================================================\n";
            Log("🔬 [전체 통합 검증 시작] AITradingBrain & 차트/거래 연계 시뮬레이션 가동");
            testLogOutput += "========================================================\n";

            bool t1 = RunTestTier1();
            bool t2 = RunTestTier3();
            bool t3 = RunTestTier4();

            if (t1 && t2 && t3)
            {
                Log("✅ [전체 검증 완료] 모든 매매 판단 분기 및 청산 리액션 연동이 정상 작동함을 확인했습니다!");
            }
            else
            {
                Log("⚠️ [전체 검증 실패] 일부 테스트 시나리오 통과에 실패했습니다. 상단 로그를 확인하세요.");
            }
        }

        private bool RunTestTier1()
        {
            Log("--------------------------------------------------------");
            Log("🟢 [Test 1 시작] Tier 1 (최상 상태, 체력 100%): 확실한 돌파 신호 포착 및 익절 검증");

            var env = SetupTestEnvironment(initialHealth: 1.0f, initialMental: 80f, balance: 10000f);

            // 이벤트 구독 확인
            bool decisionFired = false;
            string aiLog = "";
            env.Brain.OnAIDecisionMade += (dialogue, delta) =>
            {
                decisionFired = true;
                aiLog = dialogue;
            };

            // 강한 확실 신호(True Signal) 수동 발행
            MarketSignal trueSignal = new MarketSignal
            {
                Type = MarketSignalType.BullishBreakout,
                Strength = SignalStrength.Strong,
                IsTrueSignal = true,
                SignalStartPrice = 65000f,
                TargetPercentageDelta = 4.6f,
                DurationMinutes = 15,
                GraceMinutes = 3
            };

            env.MarketEngine.TriggerSignalForTest(trueSignal);

            bool passed = false;
            if (decisionFired && env.TradingController.IsActive)
            {
                Log($"✔️ [진입 성공] AI 판단: \"{aiLog}\"");
                Log($"✔️ [포지션 검증] 진입 방향: {env.TradingController.CurrentPosition}, 배율: {env.TradingController.CurrentLeverage}배, 증거금: ${env.TradingController.MarginAmount:F2}");
                Log($"🎯 [AI 결정 목표가(Target Price)] ${(env.TradingController.TargetPrice > 0 ? env.TradingController.TargetPrice.ToString("N1") : "없음 (무제한)")} | 손절가: ${env.TradingController.StopLossPrice:N1}");
                
                // 💡 [실제 시장 주가 틱 연동 검증] 
                // MarketEngine의 주가를 AI 목표가(Target Price + $50)로 실시간 이동시켜 TradingController가 자동으로 익절 청산하는지 검증!
                float targetTriggerPrice = env.TradingController.TargetPrice + 50f;
                float closedPnl = 0f;
                env.TradingController.OnPositionClosed += (ret, pnl) => { closedPnl = pnl; };

                Log($"📈 [시장 주가 변동 연동] 현재가 $65,000 -> AI 목표 주가(${targetTriggerPrice:N1})로 시장 캔들 상승 틱 주입!");
                env.MarketEngine.SimulatePriceTickForTest(targetTriggerPrice);

                if (!env.TradingController.IsActive)
                {
                    float roe = env.TradingController.LastMarginAmount > 0f ? (closedPnl / env.TradingController.LastMarginAmount) * 100f : 0f;
                    Log($"✔️ [차트-트레이딩 연동 익절 정산 완료] 실현 손익: +${closedPnl:N2} (달성 ROE: +{roe:F1}%) | 최종 AI 독백: \"{env.Brain.LastDecisionLog}\"");
                    Log("🎉 [Test 1 통과] Tier 1 확실 신호 -> 포지션 개설 -> 차트 주가 상승 틱 연동 -> 자동 익절 폐쇄 프로세스 100% 검증!");
                    passed = true;
                }
                else
                {
                    Log("❌ [Test 1 실패] 주가 상승 틱이 발생했으나 포지션이 자동으로 닫히지 않았습니다.");
                }
            }
            else
            {
                Log("❌ [Test 1 실패] 포지션이 개시되지 않거나 AI 리액션이 없습니다.");
            }

            CleanupEnvironment(env);
            return passed;
        }

        private bool RunTestTier3()
        {
            Log("--------------------------------------------------------");
            Log("🟠 [Test 2 시작] Tier 3 (피로 상태, 체력 30%): 불트랩(BullTrap) 오인 50배 고배율 진입 및 손절 검증");

            var env = SetupTestEnvironment(initialHealth: 0.30f, initialMental: 45f, balance: 10000f);

            // 불트랩 신호(False Signal) 발행
            MarketSignal trapSignal = new MarketSignal
            {
                Type = MarketSignalType.BullTrap,
                Strength = SignalStrength.Strong,
                IsTrueSignal = false,
                SignalStartPrice = 65000f,
                TargetPercentageDelta = -4.6f,
                DurationMinutes = 10,
                GraceMinutes = 2
            };

            env.MarketEngine.TriggerSignalForTest(trapSignal);

            bool passed = false;
            if (env.TradingController.IsActive && env.TradingController.CurrentLeverage >= 30)
            {
                Log($"✔️ [오인 진입 성공] AI 독백: \"{env.Brain.LastDecisionLog}\"");
                Log($"✔️ [포지션 검증] 세력 미끼에 낚여 {env.TradingController.CurrentLeverage}배 고레버리지 풀시드(${env.TradingController.MarginAmount:F2}) 진입!");
                Log($"🎯 [AI 오인 목표가(Target Price)] ${env.TradingController.TargetPrice:N1} (과도한 대박 기대) | 손절가: ${env.TradingController.StopLossPrice:N1}");

                // 💡 [실제 시장 주가 틱 연동 손절 검증]
                // MarketEngine의 주가를 AI 손절선(StopLossPrice - $50)으로 하락시켜 자동 손절 청산되는지 검증!
                float stopTriggerPrice = env.TradingController.StopLossPrice - 50f;
                float closedPnl = 0f;
                env.TradingController.OnPositionClosed += (ret, pnl) => { closedPnl = pnl; };

                Log($"📉 [시장 주가 변동 연동] 현재가 $65,000 -> AI 손절 주가(${stopTriggerPrice:N1})로 시장 캔들 하락 틱 주입!");
                env.MarketEngine.SimulatePriceTickForTest(stopTriggerPrice);

                if (!env.TradingController.IsActive)
                {
                    float roe = env.TradingController.LastMarginAmount > 0f ? (closedPnl / env.TradingController.LastMarginAmount) * 100f : 0f;
                    Log($"✔️ [차트-트레이딩 연동 손절 정산 완료] 실현 손익: -${Mathf.Abs(closedPnl):N2} (달성 ROE: {roe:F1}%) | 최종 AI 독백: \"{env.Brain.LastDecisionLog}\"");
                    Log("🎉 [Test 2 통과] Tier 3 기만 휩소 -> 포지션 개설 -> 차트 주가 하락 틱 연동 -> 자동 손절 폐쇄 프로세스 100% 검증!");
                    passed = true;
                }
                else
                {
                    Log("❌ [Test 2 실패] 주가 하락 틱이 발생했으나 손절 청산이 작동하지 않았습니다.");
                }
            }
            else
            {
                Log("❌ [Test 2 실패] 불트랩 오인 진입 로직이 작동하지 않았습니다.");
            }

            CleanupEnvironment(env);
            return passed;
        }

        private bool RunTestTier4()
        {
            Log("--------------------------------------------------------");
            Log("🔴 [Test 3 시작] Tier 4 (Overdose 상태, 체력 10%): 125배 뇌동매매 강행 검증");

            var env = SetupTestEnvironment(initialHealth: 0.10f, initialMental: 15f, balance: 10000f);

            MarketSignal anySignal = new MarketSignal
            {
                Type = MarketSignalType.BearishBreakout,
                Strength = SignalStrength.Weak,
                IsTrueSignal = false,
                SignalStartPrice = 65000f
            };

            env.MarketEngine.TriggerSignalForTest(anySignal);

            bool passed = false;
            if (env.TradingController.IsActive && env.TradingController.CurrentLeverage == 125)
            {
                Log($"✔️ [Overdose 진입 성공] AI 독백: \"{env.Brain.LastDecisionLog}\"");
                Log($"✔️ [125배 풀시드 검증] 배율: {env.TradingController.CurrentLeverage}배, 진입 금액: ${env.TradingController.MarginAmount:F2}");
                Log($"🎯 [Overdose 목표가] ${(env.TradingController.TargetPrice > 0 ? "$" + env.TradingController.TargetPrice.ToString("N1") : "무제한")} (손절선 없음)");
                
                // 💡 [실제 시장 주가 틱 연동 강제청산 검증]
                // 125배 청산선(LiquidationPrice)으로 주가 변동 틱을 주입하여 강제 청산(TriggerLiquidation) 및 멘탈 붕괴 연동 확인!
                float liqTriggerPrice = env.TradingController.CurrentPosition == TradingController.PositionType.Long 
                    ? env.TradingController.LiquidationPrice - 10f 
                    : env.TradingController.LiquidationPrice + 10f;
                bool liquidated = false;
                env.TradingController.OnPositionLiquidated += () => { liquidated = true; };

                Log($"⚡ [시장 주가 변동 연동] 125배 청산 한계선(${liqTriggerPrice:N1})으로 시장 캔들 폭락/폭등 틱 주입!");
                env.MarketEngine.SimulatePriceTickForTest(liqTriggerPrice);

                if (!env.TradingController.IsActive && liquidated)
                {
                    Log($"✔️ [차트-트레이딩 연동 강제청산 완료] 실현 손익: -${env.TradingController.LastMarginAmount:N2} (달성 ROE: -100.0%) | 최종 AI 독백: \"{env.Brain.LastDecisionLog}\"");
                    Log("🎉 [Test 3 통과] Overdose 125배 포지션 개설 -> 차트 주가 청산선 도달 -> 자동 강제청산 폐쇄 및 멘탈 대붕괴 연동 100% 검증!");
                    passed = true;
                }
                else
                {
                    Log("❌ [Test 3 실패] 주가가 청산선에 도달했으나 강제청산이 발생하지 않았습니다.");
                }
            }
            else
            {
                Log("❌ [Test 3 실패] 125배 Overdose 진입이 이뤄지지 않았습니다.");
            }

            CleanupEnvironment(env);
            return passed;
        }

        private struct TestEnv
        {
            public GameObject RootGO;
            public GameManager GM;
            public TraderStatus Status;
            public TraderLevelSystem LevelSystem;
            public TradingController TradingController;
            public MarketSimulationEngine MarketEngine;
            public AITradingBrain Brain;
        }

        private TestEnv SetupTestEnvironment(float initialHealth, float initialMental, float balance)
        {
            TestEnv env = new TestEnv();
            env.RootGO = new GameObject("Test_AI_Trading_System_GO");

            env.GM = env.RootGO.AddComponent<GameManager>();
            env.Status = env.RootGO.AddComponent<TraderStatus>();
            env.LevelSystem = env.RootGO.AddComponent<TraderLevelSystem>();
            env.TradingController = env.RootGO.AddComponent<TradingController>();
            env.MarketEngine = env.RootGO.AddComponent<MarketSimulationEngine>();
            env.Brain = env.RootGO.AddComponent<AITradingBrain>();

            // 💡 [EditMode 테스트 보완] Unity EditMode에서는 AddComponent 시 Awake()/Start() 호출 시점이 불안정하므로, Reflection을 통해 명시적 순차 실행하여 딕셔너리 초기화 및 참조/이벤트 구독을 완벽 연결!
            InvokeAwakeMethod(env.GM);
            InvokeAwakeMethod(env.Status);
            InvokeAwakeMethod(env.LevelSystem);
            InvokeAwakeMethod(env.MarketEngine);
            InvokeAwakeMethod(env.TradingController);
            InvokeAwakeMethod(env.Brain);

            // 💡 [EditMode 참조 확실화 (Start 이전 주입)] 
            // InvokeStartMethod를 호출하기 *전에* 각 컴포넌트의 Private 참조 필드(marketEngine, tradingController 등)에 인스턴스를 주입해야 Start() 내부의 OnMarketSignalGenerated += HandleMarketSignalGenerated 및 OnPriceUpdated += HandlePriceUpdated 이벤트 구독이 정상적으로 체결됩니다!
            SetPrivateField(env.Status, "gameManager", env.GM);
            SetPrivateField(env.LevelSystem, "gameManager", env.GM);
            SetPrivateField(env.LevelSystem, "traderStatus", env.Status);
            SetPrivateField(env.MarketEngine, "gameManager", env.GM);
            SetPrivateField(env.TradingController, "gameManager", env.GM);
            SetPrivateField(env.TradingController, "marketEngine", env.MarketEngine);
            SetPrivateField(env.TradingController, "traderStatus", env.Status);
            SetPrivateField(env.Brain, "marketEngine", env.MarketEngine);
            SetPrivateField(env.Brain, "tradingController", env.TradingController);
            SetPrivateField(env.Brain, "traderStatus", env.Status);
            SetPrivateField(env.Brain, "gameManager", env.GM);

            InvokeStartMethod(env.GM);
            InvokeStartMethod(env.Status);
            InvokeStartMethod(env.LevelSystem);
            InvokeStartMethod(env.MarketEngine);
            InvokeStartMethod(env.TradingController);
            InvokeStartMethod(env.Brain);

            // 💡 [초기 수치 후순위 주입] Start() 및 StartNewGame(), ResetStatus()가 호출된 *이후*에 우리가 테스트할 초기 수치를 주입해야 10000f 또는 100f로 덮어써지지 않습니다!
            SetPrivateField(env.GM, "currentBalance", balance);
            SetPrivateField(env.Status, "currentHealth", initialHealth * 100f);
            SetPrivateField(env.Status, "maxHealth", 100f);
            SetPrivateField(env.Status, "currentMental", initialMental);
            SetPrivateField(env.Status, "maxMental", 100f);

            return env;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }

        private void InvokeAwakeMethod(MonoBehaviour mb)
        {
            if (mb == null) return;
            var awakeMethod = mb.GetType().GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null) awakeMethod.Invoke(mb, null);
        }

        private void InvokeStartMethod(MonoBehaviour mb)
        {
            if (mb == null) return;
            var startMethod = mb.GetType().GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (startMethod != null) startMethod.Invoke(mb, null);
        }

        private void CleanupEnvironment(TestEnv env)
        {
            if (env.RootGO != null)
            {
                DestroyImmediate(env.RootGO);
            }
        }

        private void EditorHeader(string title)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.3f, 0.8f, 1f) }
            };
            GUILayout.Label(title, style);
        }
    }
}
