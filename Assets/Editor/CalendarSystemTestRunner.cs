using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Core;
using DTM = FXOverdose.DatingSim.Core.DatingTimeManager;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 달력 날짜 시스템의 순수 로직 검증기.
    ///
    /// 이 프로젝트는 dotnet test가 동작하지 않고 EditMode 테스트는 FXOverdose.P2P.Core 전용이라
    /// (그 어셈블리는 엔진을 참조하지 않으므로 GameCalendar를 볼 수 없습니다), 기존
    /// AITradingSystemTestRunner와 같은 에디터 메뉴 방식으로 남깁니다.
    ///
    /// 씬이 필요 없는 것만 검증합니다 — 달력 환산, 직렬화 왕복, 세이브 마이그레이션.
    /// GameManager의 시계 복원과 날짜 점프는 씬 상태가 필요하므로 계획서 7절의 수기 체크리스트로 검증합니다.
    /// </summary>
    public static class CalendarSystemTestRunner
    {
        [MenuItem("FXOverdose/Debug/Calendar System Test")]
        public static void Run()
        {
            var log = new StringBuilder();
            int failed = 0;

            void Check(string name, bool condition, string detail = "")
            {
                if (condition) log.AppendLine($"  ✅ {name}");
                else { failed++; log.AppendLine($"  ❌ {name}  {detail}"); }
            }

            DateTime epoch = GameCalendar.DefaultStartDate;

            // --- 에폭 ---
            Check("에폭이 2026-06-26", epoch == new DateTime(2026, 6, 26), $"실제 {epoch:yyyy-MM-dd}");
            Check("에폭이 금요일", epoch.DayOfWeek == DayOfWeek.Friday, $"실제 {epoch.DayOfWeek}");

            // --- 일차 ↔ 날짜 환산 ---
            Check("1일차 = 에폭", epoch.AddDays(1 - 1) == epoch);
            Check("20일차 = 2026-07-15", epoch.AddDays(20 - 1) == new DateTime(2026, 7, 15),
                  $"실제 {epoch.AddDays(19):yyyy-MM-dd}");
            Check("날짜 → 일차 역산", (epoch.AddDays(19) - epoch).Days + 1 == 20);

            // --- 경계 ---
            Check("월말 경계 6/30 → 7/1",
                  new DateTime(2026, 6, 30).AddDays(1) == new DateTime(2026, 7, 1));
            Check("연말 경계 12/31 → 1/1",
                  new DateTime(2026, 12, 31).AddDays(1) == new DateTime(2027, 1, 1));

            // --- 표기 ---
            Check("짧은 표기", GameCalendar.ToKoreanShort(epoch) == "6월 26일", GameCalendar.ToKoreanShort(epoch));
            Check("전체 표기", GameCalendar.ToKoreanFull(epoch) == "2026년 6월 26일", GameCalendar.ToKoreanFull(epoch));

            // --- 직렬화 왕복 (G-3: 문화권 달력 회귀) ---
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                // 태국은 기본 달력이 불기라, InvariantCulture를 빠뜨리면 2569년으로 저장됩니다.
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                string text = GameCalendar.ToSerialized(epoch);
                Check("불기 로케일에서도 서기로 직렬화", text == "2026-06-26", text);
                Check("불기 로케일에서 왕복 성공", GameCalendar.TryParse(text, out var back) && back == epoch);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Check("손상된 날짜는 예외 없이 실패", !GameCalendar.TryParse("2026/13/99", out _));
            Check("빈 문자열은 예외 없이 실패", !GameCalendar.TryParse("", out _));

            // --- 마이그레이션 (G-1) ---
            var legacy = new SaveData { Version = "1.6.0", CurrentDay = 20, CurrentDate = "", StartDate = "" };
            SaveDataMigrator.Migrate(ref legacy);
            Check("구버전 세이브의 날짜 역산", legacy.CurrentDate == "2026-07-15", legacy.CurrentDate);
            Check("구버전 세이브의 일차 불변", legacy.CurrentDay == 20, legacy.CurrentDay.ToString());

            // 이미 날짜가 있는 세이브를 덮어쓰면 진행 중인 게임이 1일차로 되돌아갑니다.
            var existing = new SaveData { Version = "1.6.0", CurrentDay = 20, CurrentDate = "2026-08-01", StartDate = "2026-06-26" };
            SaveDataMigrator.Migrate(ref existing);
            Check("기존 날짜를 덮어쓰지 않음", existing.CurrentDate == "2026-08-01", existing.CurrentDate);

            // 세이브가 자기 에폭을 들고 있으므로, 기본 에폭이 바뀌어도 일차 계산이 어긋나지 않아야 합니다. (G-4)
            var customEpoch = new SaveData { Version = "1.6.0", CurrentDay = 5, CurrentDate = "", StartDate = "2025-01-01" };
            SaveDataMigrator.Migrate(ref customEpoch);
            Check("세이브 고유 에폭 기준으로 역산", customEpoch.CurrentDate == "2025-01-05", customEpoch.CurrentDate);

            // --- 시간 슬롯 ↔ 시각 환산 ---
            var dating = typeof(FXOverdose.DatingSim.Core.DatingTimeManager);
            Check("슬롯 5개 잔여 = 09:00", DTM.MinuteOfDayForSlots(5) == 9 * 60);
            Check("슬롯 3개 잔여 = 15:00", DTM.MinuteOfDayForSlots(3) == 15 * 60, DTM.ClockTextForSlots(3));
            Check("슬롯 1개 잔여 = 21:00", DTM.MinuteOfDayForSlots(1) == 21 * 60, DTM.ClockTextForSlots(1));
            Check("슬롯 전소 = 24:00", DTM.MinuteOfDayForSlots(0) == 24 * 60, DTM.ClockTextForSlots(0));
            Check("5슬롯 × 3시간이 09:00~24:00과 정확히 일치",
                  DTM.DayStartMinuteOfDay + DTM.DefaultTimeSlots * DTM.MinutesPerTimeSlot == 24 * 60);
            Check("범위를 벗어난 슬롯 수도 잘림", DTM.MinuteOfDayForSlots(99) == 9 * 60 && DTM.MinuteOfDayForSlots(-3) == 24 * 60);

            // --- 마이그레이션 1.8.0 (슬롯 소비가 시계를 밀지 않던 세이브) ---
            var roomSave = new SaveData { Version = "1.7.0", CurrentDay = 3, CurrentHour = 9, CurrentMinute = 0, DatingTimeSlot = 2 };
            SaveDataMigrator.Migrate(ref roomSave);
            Check("방 세이브: 슬롯 2 잔여 → 18:00", roomSave.CurrentHour == 18 && roomSave.CurrentMinute == 0,
                  $"{roomSave.CurrentHour:00}:{roomSave.CurrentMinute:00}");

            // ⚠️ 이쪽을 보정하면 진행 중이던 하루가 통째로 날아갑니다.
            var tradingSave = new SaveData { Version = "1.7.0", CurrentDay = 3, CurrentHour = 14, CurrentMinute = 30, DatingTimeSlot = 2 };
            SaveDataMigrator.Migrate(ref tradingSave);
            Check("거래 중 세이브(14:30)는 보정하지 않음", tradingSave.CurrentHour == 14 && tradingSave.CurrentMinute == 30,
                  $"{tradingSave.CurrentHour:00}:{tradingSave.CurrentMinute:00}");

            var fullSlots = new SaveData { Version = "1.7.0", CurrentDay = 3, CurrentHour = 9, CurrentMinute = 0, DatingTimeSlot = 5 };
            SaveDataMigrator.Migrate(ref fullSlots);
            Check("슬롯을 안 쓴 세이브는 09:00 유지", fullSlots.CurrentHour == 9 && fullSlots.CurrentMinute == 0);

            // --- 임시 비활성화 스위치가 실제로 꺼져 있는지 ---
            Check("보스 비활성", !FXOverdose.Core.BossManager.BossesEnabled);
            Check("위약금 비활성", !GameManager.StoryPenaltiesEnabled);
            Check("20일 제한 비활성", !GameManager.StoryDayLimitEnabled);

            string header = failed == 0
                ? "🗓️ [달력 시스템 검증] 전체 통과"
                : $"🗓️ [달력 시스템 검증] 실패 {failed}건";

            if (failed == 0) Debug.Log($"{header}\n{log}");
            else Debug.LogError($"{header}\n{log}");
        }
    }
}
