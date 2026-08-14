using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Events;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 기존 242개 로직 템플릿 자산을 C3/C4 수정 규격으로 옮깁니다.
    ///
    /// ⚠️ 이 툴은 <b>생성기를 다시 돌리지 않습니다.</b>
    ///    현재 자산의 OverrideSignalProbTrue는 0.002~0.399인데 GenerateEventTemplates의 코드는
    ///    Random.Range(0.4f, 0.7f)입니다. 즉 자산은 지금 생성기와 다른 버전으로 만들어졌고,
    ///    생성기를 재실행하면 성공 확률이 평균 0.2에서 0.55로 뛰어 밸런스가 통째로 바뀝니다.
    ///    그래서 기존 값을 보존한 채 필요한 필드만 옮기는 마이그레이션 방식을 씁니다. (리스크 #2)
    ///
    /// 수행 내용
    ///  · C4 : 베팅 선택지(Aggressive/DirectionalLong/DirectionalShort)의 음수 MentalChangeAmount를
    ///         MentalPenaltyOnFail로 옮기고, MentalChangeAmount에는 성공 보상(양수)을 채웁니다.
    ///  · C5 : 이관한 멘탈 페널티를 리밸런싱 범위(-30 ~ -5)로 압축합니다.
    ///         기존 값은 최대 -120이라 베팅 한 번의 결과로 만멘탈이 통째로 지워졌습니다.
    ///         성공 보상은 원래 위험도(이관 전 수치) 기준으로 뽑아 위험/보상이 1:1에 수렴합니다.
    ///  · C3 : OverrideDurationSeconds를 이벤트 쉴드 기본값(150초)으로 맞춥니다.
    ///         기존 값 10/15는 지금까지 코드가 무시하던 값이라 그대로 살리면
    ///         쉴드가 150초 → 30초로 줄어드는 미검증 밸런스 변경이 됩니다. 현행 동작을 보존합니다.
    /// </summary>
    public static class MigrateEventTemplates
    {
        /// <summary>성공 보상 = |이관 전 페널티| × 이 비율.</summary>
        private const float SuccessRewardRatio = 0.25f;
        private const int MinSuccessReward = 5;
        private const int MaxSuccessReward = 30;

        /// <summary>멘탈 실패 페널티 압축 비율과 허용 범위(절댓값). ChoiceEventController의 상한(35)보다 보수적입니다.</summary>
        private const float MentalPenaltyRescale = 0.25f;
        private const int MinMentalPenalty = 5;
        private const int MaxMentalPenalty = 30;

        /// <summary>이벤트 쉴드 기본 지속 시간(실시간 초). ChoiceEventController와 같은 값이어야 합니다.</summary>
        private const int DefaultEventShieldSeconds = 150;

        [MenuItem("Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)")]
        public static void Migrate()
        {
            var templates = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
            if (templates == null || templates.Length == 0)
            {
                EditorUtility.DisplayDialog("마이그레이션", "Resources/Events/Templates에 템플릿이 없습니다.", "확인");
                return;
            }

            // 배치모드에는 대화상자가 없습니다(호출하면 예외 후 false 반환 → 조용히 취소됨).
            // -executeMethod로 부른 것 자체가 실행 의사이므로 확정으로 봅니다.
            bool proceed = Application.isBatchMode || EditorUtility.DisplayDialog(
                "템플릿 마이그레이션",
                $"로직 템플릿 {templates.Length}개와 하드코딩 이벤트 30개를 수정합니다.\n\n" +
                "· 베팅 선택지의 음수 멘탈을 실패 페널티로 이동하고 성공 보상을 채웁니다 (C4)\n" +
                $"· OverrideDurationSeconds를 {DefaultEventShieldSeconds}초로 맞춥니다 (C3)\n" +
                "  (기존 2~25초 값은 지금까지 코드가 무시하던 값이라 현행 동작을 보존합니다)\n\n" +
                "되돌리려면 git으로 복원해야 합니다. 커밋되지 않은 변경이 없는지 먼저 확인하십시오.\n\n" +
                "진행할까요?",
                "진행", "취소");

            if (!proceed) return;

            int changedAssets = 0, movedMental = 0, movedHealth = 0, fixedDuration = 0, rescaledMental = 0;
            var log = new StringBuilder();

            foreach (var t in templates)
            {
                if (t == null || t.LogicOptions == null) continue;

                bool dirty = false;

                foreach (var o in t.LogicOptions)
                {
                    if (o == null) continue;

                    bool isBet = o.OptionType == ChoiceOptionType.Aggressive
                              || o.OptionType == ChoiceOptionType.DirectionalLong
                              || o.OptionType == ChoiceOptionType.DirectionalShort;

                    // ── C4 + C5 ─────────────────────────────────────────
                    if (isBet)
                    {
                        if (o.MentalChangeAmount < 0)
                        {
                            int originalPenalty = o.MentalChangeAmount;
                            o.MentalPenaltyOnFail = RescalePenalty(originalPenalty);
                            // 보상은 이관 전 위험도 기준으로 뽑습니다. 압축된 페널티와 1:1에 수렴합니다.
                            o.MentalChangeAmount = DeriveReward(originalPenalty);
                            movedMental++;
                            rescaledMental++;
                            dirty = true;
                        }
                        else if (o.MentalPenaltyOnFail < -MaxMentalPenalty)
                        {
                            // 이미 이관됐지만 압축되지 않은 자산도 범위 안으로 끌어옵니다. (재실행 안전)
                            o.MentalPenaltyOnFail = -MaxMentalPenalty;
                            rescaledMental++;
                            dirty = true;
                        }

                        if (o.HealthChangeAmount < 0)
                        {
                            o.HealthPenaltyOnFail = o.HealthChangeAmount;
                            o.HealthChangeAmount = DeriveReward(o.HealthPenaltyOnFail);
                            movedHealth++;
                            dirty = true;
                        }
                    }

                    // ── C3 ──────────────────────────────────────────────
                    if (o.OverrideDurationSeconds != DefaultEventShieldSeconds)
                    {
                        o.OverrideDurationSeconds = DefaultEventShieldSeconds;
                        fixedDuration++;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(t);
                    changedAssets++;
                }
            }

            // ── 하드코딩 ChoiceEventSO 30개도 같은 규격으로 맞춥니다 ──────────
            // 이쪽 자산의 OverrideDurationSeconds는 2~25로 기입돼 있는데, 지금까지 호출부의 리터럴 150에
            // 가려 한 번도 쓰인 적이 없습니다. 그대로 두면 C3 수정 이후 쉴드가 30초로 줄어듭니다.
            int changedEvents = 0, fixedEventDuration = 0, movedEventMental = 0;
            var events = Resources.LoadAll<ChoiceEventSO>("Events");
            foreach (var ev in events)
            {
                if (ev == null || ev.Options == null) continue;
                bool dirty = false;

                foreach (var o in ev.Options)
                {
                    if (o == null) continue;

                    bool isBet = o.OptionType == ChoiceOptionType.Aggressive
                              || o.OptionType == ChoiceOptionType.DirectionalLong
                              || o.OptionType == ChoiceOptionType.DirectionalShort;

                    if (isBet && o.MentalChangeAmount < 0)
                    {
                        int originalPenalty = o.MentalChangeAmount;
                        o.MentalPenaltyOnFail = RescalePenalty(originalPenalty);
                        o.MentalChangeAmount = DeriveReward(originalPenalty);
                        movedEventMental++;
                        dirty = true;
                    }
                    else if (isBet && o.MentalPenaltyOnFail < -MaxMentalPenalty)
                    {
                        o.MentalPenaltyOnFail = -MaxMentalPenalty;
                        movedEventMental++;
                        dirty = true;
                    }

                    if (o.OverrideDurationSeconds != DefaultEventShieldSeconds)
                    {
                        o.OverrideDurationSeconds = DefaultEventShieldSeconds;
                        fixedEventDuration++;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(ev);
                    changedEvents++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine("===== 마이그레이션 완료 =====");
            log.AppendLine($"[로직 템플릿] 검사 {templates.Length}개 / 수정 {changedAssets}개");
            log.AppendLine($"  멘탈 페널티 이동   : {movedMental}개 선택지");
            log.AppendLine($"  멘탈 페널티 압축   : {rescaledMental}개 선택지 (-{MinMentalPenalty} ~ -{MaxMentalPenalty} 범위)");
            log.AppendLine($"  체력 페널티 이동   : {movedHealth}개 선택지");
            log.AppendLine($"  지속시간 정정      : {fixedDuration}개 선택지");
            log.AppendLine($"[하드코딩 이벤트] 검사 {events.Length}개 / 수정 {changedEvents}개");
            log.AppendLine($"  멘탈 페널티 이동   : {movedEventMental}개 선택지");
            log.AppendLine($"  지속시간 정정      : {fixedEventDuration}개 선택지");
            log.AppendLine();
            log.AppendLine("git diff로 변경 내용을 확인한 뒤 별도 커밋으로 분리하십시오.");
            Debug.Log(log.ToString());

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("마이그레이션 완료",
                    $"템플릿 {changedAssets}개 / 하드코딩 이벤트 {changedEvents}개를 수정했습니다.\n자세한 내용은 콘솔을 확인하십시오.", "확인");
        }

        /// <summary>실패 페널티 크기에서 성공 보상을 유도합니다. 난수를 쓰지 않아 재실행해도 결과가 같습니다.</summary>
        private static int DeriveReward(int penalty)
        {
            int magnitude = Mathf.Abs(penalty);
            int reward = Mathf.RoundToInt(magnitude * SuccessRewardRatio);
            return Mathf.Clamp(reward, MinSuccessReward, MaxSuccessReward);
        }

        /// <summary>
        /// 멘탈 실패 페널티를 리밸런싱 범위로 압축합니다.
        /// 만멘탈에서 최악의 실패를 맞아도 오버도즈(0)에 닿지 않는 것이 기준입니다.
        /// </summary>
        private static int RescalePenalty(int penalty)
        {
            if (penalty >= 0) return penalty;
            int magnitude = Mathf.RoundToInt(Mathf.Abs(penalty) * MentalPenaltyRescale);
            return -Mathf.Clamp(magnitude, MinMentalPenalty, MaxMentalPenalty);
        }
    }
}
