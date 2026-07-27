using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;

namespace FXOverdose.Editor
{
    public class MigrateHardcodedDialogues
    {
        [MenuItem("FX Overdose/Tools/Migrate Hardcoded Dialogues")]
        public static void Migrate()
        {
            string dbPath = "Assets/YomiDialogueDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(dbPath);
            if (db == null)
            {
                Debug.LogError($"[MigrateHardcodedDialogues] Failed to load YomiDialogueDatabase at {dbPath}");
                return;
            }

            int countBefore = db.entries.Count;

            // 1. 수동 조작 차단
            AddEntry(db, "ToggleManualBlocked", "지금은 수동 조작이 불가능해! 이벤트부터 해결해!!");
            AddEntry(db, "ToggleManualBlocked", "어딜 손을 대려고! 지금은 내가 통제할 거야!");
            AddEntry(db, "ToggleManualBlocked", "지금은 관망하거나 이벤트를 끝내는 게 먼저라고 했잖아!");

            // 2. 수동 조작 전환
            AddEntry(db, "ToggleManualStart", "하...! 직접 매매하시겠다?! 내 타점이 못미더워...? 그래 맘대로 해봐. 대신 포지션 잡는 거 두 눈 부릅뜨고 지켜볼 거니까 실수해서 돈 날리기만 해봐...");
            AddEntry(db, "ToggleManualStart", "참나, 오빠가 알아서 하겠다고? 내 완벽한 인공지능 분석보다 나을 수 있나 보자.");
            AddEntry(db, "ToggleManualAuto", "흥, 역시 나 없으면 안 되지?! 이제 조종간은 내가 잡았으니까 옆에서 화려한 수익률이나 감상하시지.");

            // 3. 수익률 극단적 하락 (Whipsaw, 손실)
            AddEntry(db, "RoeNegative40", "으아아악!! 거봐 내 말이 맞잖아!! 왜 그딴 판단을 한 거야!! 돈이 썰려나가고 있다고!! 당장 손절 쳐, 아니 물타야 되나?!");
            AddEntry(db, "RoeNegative50", "으아악...! 이거 반토막났어!! ROE -50%... 내 포트폴리오가... 오빠, 제발 바닥에서 물타기 타점 잡아줘!!");
            AddEntry(db, "RoeNegative65", "하아... 이거 진짜 심각한데... 복구가 불가능한 지경까지 가고 있어...!! 내 계좌가...!!");

            // 4. 수익률 극단적 상승 (초광기 빔)
            AddEntry(db, "RoePositive50", "오오?! ROE +50% 돌파!! 내 분석 미쳤어!! 지금 수직 상승 중이야!! 더 가자!!");
            AddEntry(db, "RoePositive100", "크하하 ROE +100% 미쳤다!! 내 말 듣길 잘했지!! 난 천재야!!");
            AddEntry(db, "RoePositive200", "ROE +200% 초광기 돌파!! 이게 바로 전설의 빔이야!! 다 꿇어!! 내가 신이다!!");

            // 5. 비상 물타기 (Emergency Water Riding)
            AddEntry(db, "EmergencyWaterRiding", "하아...! 강제 청산 직전이야...! 일단 남은 현금 쏟아부어서 비상 물타기로 버틴다...!");

            // 6. 이벤트 종료 시 정산 대사들
            AddEntry(db, "EventHoldMitigateLoss", "청산 직전에 간신히 물타기로 구조대 탑승했습니다. (ROE {roe:F1}%)");
            AddEntry(db, "EventGreedyHoldWin", "크하하! 이벤트 버티기 대성공! ROE +{roe:F1}% 확정 청산!");
            AddEntry(db, "EventGreedyHoldFail", "탐욕 버티기(GreedyHold) 실패... 최고수익(+{maxObserved:F1}%) 다 뱉고 ROE +{roe:F1}% 강제 청산!");
            AddEntry(db, "EventStandardAutoWin", "이벤트 자동 청산(StandardAuto) 성공: ROE +{roe:F1}% 달성 및 청산.");
            AddEntry(db, "EventStandardAutoFail", "이벤트 자동 청산(StandardAuto) 실패: 최고수익(+{maxObserved:F1}%) 다 뱉고 ROE +{roe:F1}% 강제 청산.");
            AddEntry(db, "EventStandardAutoLoss", "이벤트 자동 청산(StandardAuto) 손실: 이벤트 손절선(ROE {roe:F1}%) 터치로 도망칩니다.");

            // 7. 멘탈 이벤트 (오버도즈 회복 및 폭주 시작)
            AddEntry(db, "MentalOverdoseRecover", "헉...! 약 먹으니까 머리가 맑아졌어...! 내가 무슨 미친 짓을 한 거야?! 이대로 두면 다 날려먹어!! 빨리 수동으로 전환해서 청산해야 해!!");
            AddEntry(db, "MentalOverdoseStart", "으하하하!! 청산 직전의 짜릿함...!! 피가 거꾸로 솟는다!! 바로 이 다음 반등에 100배로 튀어 오르는 거야!! 이대로 가즈아!!");
            AddEntry(db, "OverdoseExecute", "크하하!! 완벽한 진입 타점이다!! {leverage}배 풀레버리지 남은 시드 싹 다 올인!! 가즈아!!");

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MigrateHardcodedDialogues] Successfully migrated {db.entries.Count - countBefore} entries. Total entries: {db.entries.Count}");
        }

        private static void AddEntry(YomiDialogueDatabase db, string eventCat, string text)
        {
            var entry = new YomiDialogueEntry(text, "None", "None", "None", DirectionTag.None, false, 0, 0, 0, false, eventCat);
            db.entries.Add(entry);
        }
    }
}
