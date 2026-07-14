#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>머지 후 GameScene의 트레이딩 HUD를 마지막 작업 상태로 다시 조립합니다.</summary>
public static class CompleteUIRecoveryBuilder
{
    [MenuItem("Tools/FX OVERDOSE/Restore Complete Game UI")]
    public static void RecoverFromMenu() => Recover(true);

    private static void Recover(bool showResult)
    {
        FXOverdose.EditorTools.TradingViewUIBuilder.BuildTradingChartUI();

        // 빌더가 만든 실제 TradingViewCanvas 하위 카드에 순서대로 스타일을 적용합니다.
        DayTimeCardStyler.ApplySilently();
        BalancePnLCardStyler.ApplySilently();
        VitalsPanelStyler.ApplySilently();

        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        if (showResult)
        {
            GameObject canvas = GameObject.Find("TradingViewCanvas");
            if (canvas != null) Selection.activeGameObject = canvas;
            EditorUtility.DisplayDialog(
                "UI 복구 완료",
                "날짜/시간, BALANCE, P&L, HP, MENTAL, 차트, 매매 패널과 캐릭터 UI를 다시 조립했습니다.",
                "확인");
        }
    }
}
#endif
