using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.Trading
{
    /// <summary>
    /// 그날의 거시 방향성. 씬과 무관하게 SaveData를 통해 유지됩니다.
    ///
    /// 핵심 규칙: <b>하루의 거시 방향성은 딱 한 번 결정되고, 그 뒤엔 누구도 다시 굴리지 않습니다.</b>
    /// 요미의 방에서 힌트로 먼저 물어보든 GameScene에서 차트가 먼저 물어보든 같은 답이 나와야
    /// 요미의 힌트가 거짓말이 되지 않습니다.
    ///
    /// MonoBehaviour 싱글턴을 쓰지 않는 이유: 상태가 int 2개 + bool 1개뿐이고 진실 원천은 세이브입니다.
    /// 씬마다 프리팹을 배치할 이유가 없습니다.
    /// </summary>
    public static class DailyMarketOutlook
    {
        public static int Day { get; private set; } = -1;
        public static MarketSimulationEngine.MarketRegime Regime { get; private set; }

        /// <summary>요미가 힌트로 알려줬는가. true면 그날 차트 편향이 강화됩니다.</summary>
        public static bool Revealed { get; private set; }

        /// <summary>해당 일차의 방향성을 얻습니다. 미결정이면 이 자리에서 결정하고 세이브에 기록합니다.</summary>
        public static MarketSimulationEngine.MarketRegime GetOrRoll(int day)
        {
            if (Day == day) return Regime;

            Day = day;
            Revealed = false;
            Regime = Roll();

            Debug.Log($"[DailyMarketOutlook] 📅 {day}일차 거시 방향성 결정: {Regime}");
            Persist();
            return Regime;
        }

        /// <summary>요미가 힌트를 발화한 뒤 잠급니다.</summary>
        public static void MarkRevealed(int day)
        {
            if (Day != day) GetOrRoll(day);

            Revealed = true;
            Debug.Log($"[DailyMarketOutlook] 🔒 {day}일차 방향성이 힌트로 공개되어 잠겼습니다: {Regime}");
            Persist();
        }

        /// <summary>새 게임 등에서 상태를 비웁니다. static이라 이전 판의 값이 남는 것을 막습니다. (S8)</summary>
        public static void Reset()
        {
            Day = -1;
            Regime = MarketSimulationEngine.MarketRegime.Sideways;
            Revealed = false;
        }

        internal static void Load(SaveData data)
        {
            if (data == null) return;
            Day = data.OutlookDay;
            Regime = data.OutlookRegime;
            Revealed = data.OutlookRevealed;
        }

        internal static void Capture(SaveData data)
        {
            if (data == null) return;
            data.OutlookDay = Day;
            data.OutlookRegime = Regime;
            data.OutlookRevealed = Revealed;
        }

        // 엔진이 쓰던 확률표를 그대로 옮겨 왔습니다. (Sideways 35 / Bull 25 / Bear 25 / Squeeze 15)
        private static MarketSimulationEngine.MarketRegime Roll()
        {
            float rand = Random.value;
            if (rand < 0.35f) return MarketSimulationEngine.MarketRegime.Sideways;
            if (rand < 0.60f) return MarketSimulationEngine.MarketRegime.Bull;
            if (rand < 0.85f) return MarketSimulationEngine.MarketRegime.Bear;
            return MarketSimulationEngine.MarketRegime.Squeeze;
        }

        /// <summary>
        /// 결정 즉시 디스크에 기록합니다. 세이브 스컴으로 방향성을 다시 굴리지 못하게 하는 장치입니다. (S6)
        /// </summary>
        private static void Persist()
        {
            var save = SaveLoadManager.Instance;
            if (save?.CurrentData == null) return;

            Capture(save.CurrentData);
            save.SaveCurrentGame();
        }
    }
}
