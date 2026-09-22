using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Events;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 돌발 선택 이벤트 검증용 디버그 메뉴입니다.
    ///
    /// 돌발 이벤트는 인게임 10:00 이후 하루 2회 랜덤 발생이라 수동 테스트가 매우 느립니다.
    /// 이 메뉴로 즉시 발생시키거나, 템플릿 자산 상태를 일괄 점검합니다.
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

        // ── 2. 템플릿 자산 상태 점검 ──────────────────────────────────────

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
