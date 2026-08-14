using UnityEditor;
using UnityEngine;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.Dialogue;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// T4(호감도 91+) QA 레버.
    ///
    /// T4는 정상 플레이로 도달할 수 없습니다 — 대화 상한이 +3/일이라 최소 31일이 걸리고,
    /// 대화 외 호감도 공급원(데이트·이벤트)은 아직 없습니다 (Affection_Tier_Table.md 5장).
    /// 그래서 이 메뉴가 T4 토픽을 실기로 확인하는 유일한 경로입니다. (계획서 16.5절 S2)
    /// </summary>
    public static class AffectionDebugMenu
    {
        [MenuItem("FXOverdose/Debug/Set Affection 91 (T4)")]
        private static void SetAffectionT4()
        {
            var time = DatingTimeManager.Instance;
            if (!Application.isPlaying || time == null)
            {
                Debug.LogWarning("[AffectionDebug] 요미의 방을 재생 중인 상태에서 실행하십시오. " +
                                 "(DatingTimeManager 인스턴스가 필요합니다)");
                return;
            }

            // ModifyAffection은 델타를 받으므로 목표치와의 차이를 넣습니다.
            // 내부에서 0~100 클램프 + peak 갱신 + 저장까지 처리합니다.
            time.ModifyAffection(AffectionTier.T4Min - time.CurrentAffection);
            Debug.Log($"[AffectionDebug] 호감도 {time.CurrentAffection} / peak {time.PeakAffection} — " +
                      "T4 토픽이 추첨 대상에 들어갑니다. (같은 날 이미 대화했다면 다음 날 확인)");
        }
    }
}
