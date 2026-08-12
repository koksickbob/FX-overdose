using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using FXOverdose.AI.LLM;
using FXOverdose.Events;
using FXOverdose.Trading;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 돌발 선택 이벤트 검증용 디버그 메뉴입니다.
    ///
    /// 돌발 이벤트는 인게임 10:00 이후 하루 2회 랜덤 발생이라 수동 테스트가 매우 느립니다.
    /// 이 메뉴로 즉시 발생시키거나, UI 없이 텍스트 생성만 20회 반복해 위생 검사 통과율을 잽니다.
    /// (계획서 8.1 검증 절차)
    /// </summary>
    public static class ChoiceEventDebugMenu
    {
        private const string MenuRoot = "FX Overdose/Debug/";

        // ── 1. 즉시 발생 ──────────────────────────────────────────────────

        [MenuItem(MenuRoot + "Force Choice Event (템플릿)", false, 100)]
        public static void ForceTemplateEvent()
        {
            if (!RequirePlayMode()) return;

            var controller = Object.FindAnyObjectByType<ChoiceEventController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[ChoiceEventDebug] 씬에 ChoiceEventController가 없습니다. GameScene에서 실행하십시오.");
                return;
            }

            bool shown = controller.ForceTriggerTemplateEvent();
            Debug.Log($"[ChoiceEventDebug] 템플릿 이벤트 강제 발생 {(shown ? "성공" : "실패")}");
        }

        [MenuItem(MenuRoot + "Force Choice Event (하드코딩 이벤트)", false, 101)]
        public static void ForceHardcodedEvent()
        {
            if (!RequirePlayMode()) return;

            var controller = Object.FindAnyObjectByType<ChoiceEventController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[ChoiceEventDebug] 씬에 ChoiceEventController가 없습니다.");
                return;
            }

            bool shown = controller.TriggerRandomEvent(EventTriggerCondition.Any);
            Debug.Log($"[ChoiceEventDebug] 하드코딩 이벤트 강제 발생 {(shown ? "성공" : "실패")}");
        }

        // ── 2. 텍스트 생성 검증 (UI 없이 N회 반복) ────────────────────────

        [MenuItem(MenuRoot + "Force Choice Event x20 (텍스트 검증)", false, 120)]
        public static void RunTextGenerationBatch()
        {
            _ = RunBatchAsync(20);
        }

        [MenuItem(MenuRoot + "Force Choice Event x3 (빠른 확인)", false, 121)]
        public static void RunTextGenerationBatchQuick()
        {
            _ = RunBatchAsync(3);
        }

        private static async Task RunBatchAsync(int iterations)
        {
            if (!RequirePlayMode()) return;

            var generator = LLMSafeGenerator.Instance;
            if (generator == null)
            {
                Debug.LogError(
                    "[ChoiceEventDebug] LLMSafeGenerator.Instance가 없습니다.\n" +
                    "LLM_Manager는 TitleScene에만 있으므로, TitleScene부터 재생해서 GameScene으로 진입해야 합니다.\n" +
                    "(GameScene을 직접 재생하면 사전 작성 텍스트 경로만 검증할 수 있습니다.)");
                return;
            }

            var templates = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
            if (templates == null || templates.Length == 0)
            {
                Debug.LogError("[ChoiceEventDebug] Resources/Events/Templates에 템플릿이 없습니다.");
                return;
            }

            LLMGenerationStats.Reset();

            var market = Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            string marketContext = market != null
                ? $"주가 ${market.CurrentPrice:F1}, 국면: {market.CurrentRegime}"
                : "주가 $68,400.0, 국면: Bull";

            var report = new StringBuilder();
            report.AppendLine($"===== 돌발 이벤트 텍스트 생성 검증 {iterations}회 =====");

            int accepted = 0;
            for (int i = 0; i < iterations; i++)
            {
                var template = templates[Random.Range(0, templates.Length)];

                GeneratedChoiceEventData data = null;
                try
                {
                    data = await generator.GenerateChoiceEventAsync(template, marketContext);
                }
                catch (System.Exception e)
                {
                    report.AppendLine($"[{i + 1:D2}] 예외: {e.Message}");
                    continue;
                }

                if (data == null)
                {
                    report.AppendLine($"[{i + 1:D2}] ❌ 기각 → 사전 작성 텍스트로 폴백  ({template.TemplateID})");
                }
                else
                {
                    accepted++;
                    report.AppendLine($"[{i + 1:D2}] ✅ 채택  ({template.TemplateID})");
                    report.AppendLine($"       제목: {data.ScenarioTitle}");
                    report.AppendLine($"       본문: {data.ScenarioDescription}");
                    report.AppendLine($"       요미: {data.AIMonologue}");
                }
            }

            report.AppendLine();
            report.AppendLine($"채택 {accepted}/{iterations} (폴백률 {1f - (float)accepted / iterations:P0})");
            report.AppendLine("기각 사유:");
            report.Append(LLMGenerationStats.DumpReasons());

            Debug.Log(report.ToString());
        }

        // ── 3. 템플릿 자산 상태 점검 ──────────────────────────────────────

        [MenuItem(MenuRoot + "Validate Event Templates (자산 점검)", false, 140)]
        public static void ValidateTemplates()
        {
            var templates = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
            if (templates == null || templates.Length == 0)
            {
                Debug.LogError("[ChoiceEventDebug] Resources/Events/Templates에 템플릿이 없습니다.");
                return;
            }

            int badOptions = 0, missingFallback = 0, unmigrated = 0;
            var sb = new StringBuilder();
            sb.AppendLine($"===== 템플릿 자산 점검: {templates.Length}개 =====");

            foreach (var t in templates)
            {
                if (t == null) continue;

                if (!t.HasValidOptions)
                {
                    badOptions++;
                    sb.AppendLine($"  ❌ 선택지 부족: {t.name}");
                    continue;
                }

                if (!t.HasFallbackText)
                {
                    missingFallback++;
                }

                // C4 마이그레이션 여부: 베팅 선택지의 보상이 음수로 남아 있으면 미이행
                foreach (var o in t.LogicOptions)
                {
                    bool isBet = o.OptionType == ChoiceOptionType.Aggressive
                              || o.OptionType == ChoiceOptionType.DirectionalLong
                              || o.OptionType == ChoiceOptionType.DirectionalShort;
                    if (isBet && o.MentalChangeAmount < 0)
                    {
                        unmigrated++;
                        break;
                    }
                }
            }

            sb.AppendLine($"  선택지 3개 미만          : {badOptions}개");
            sb.AppendLine($"  사전 작성 텍스트 미기입  : {missingFallback}개");
            sb.AppendLine($"  C4 마이그레이션 미이행   : {unmigrated}개");
            sb.AppendLine();
            if (missingFallback > 0 || unmigrated > 0)
            {
                sb.AppendLine("→ Tools/FX OVERDOSE/Migrate Event Templates (C3+C4) 및");
                sb.AppendLine("  Tools/FX OVERDOSE/Generate Template Fallback Text 를 실행하십시오.");
            }

            Debug.Log(sb.ToString());
        }

        private static bool RequirePlayMode()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog(
                "재생 모드 필요",
                "이 기능은 게임이 실행 중일 때만 동작합니다.\n에디터에서 재생(Play) 후 다시 실행하십시오.",
                "확인");
            return false;
        }
    }
}
