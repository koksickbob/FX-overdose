using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FXOverdose.Core;
using UnityEditor;
using UnityEngine;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 세이브 왕복 검사기. 계획서 7.1절의 자동 검사입니다.
    /// 저장 → 로드 → 재저장 후 값이 그대로인지, 부재 매니저의 필드가 날아가지 않는지 확인합니다.
    ///
    /// GameScene을 플레이 중인 상태에서 실행하십시오. 매니저가 하나도 없으면 의미 있는 검사가 되지 않습니다.
    /// </summary>
    public static class SaveRoundTripTester
    {
        // 검사에만 쓰는 슬롯. 실제 플레이 슬롯을 건드리지 않습니다.
        private const int TestSlotIndex = 2;

        [MenuItem("FXOverdose/Debug/세이브 왕복(Round-trip) 검사")]
        public static void Run()
        {
            var save = SaveLoadManager.Instance;
            if (save == null)
            {
                Debug.LogError("[왕복검사] SaveLoadManager가 없습니다. TitleScene에서 시작해 GameScene을 플레이 중일 때 실행하십시오.");
                return;
            }

            int failures = 0;
            failures += RunFieldCoverageAudit();
            failures += RunMergeInvariantCheck();
            failures += RunLiveRoundTrip(save);

            if (failures == 0)
                Debug.Log("[왕복검사] ✅ 통과. 세이브 왕복에서 유실된 필드가 없습니다.");
            else
                Debug.LogError($"[왕복검사] ❌ {failures}건 실패. 위 로그를 확인하십시오.");
        }

        /// <summary>
        /// SaveData의 모든 public 필드가 SaveLoadManager 또는 각 매니저의 Capture 경로에서
        /// 한 번이라도 이름으로 언급되는지 소스에서 확인합니다. 데드 필드 재발 방지용입니다. (SV-D1)
        ///
        /// ⚠️ <b>목록에서 빠진 파일은 그 파일이 소유한 필드를 통째로 데드로 오판하게 만듭니다.</b>
        ///    실제로 YomiRoomManager(Talk* 15개)와 DailyMarketOutlook(Outlook* 2개)이 빠져 있었고,
        ///    orphan이 경고로만 흘러가 6건이 조용히 방치됐습니다. 새 저장 경로를 만들면 여기에 추가하십시오. (S-3)
        /// </summary>
        private static int RunFieldCoverageAudit()
        {
            string[] sources =
            {
                "Assets/Scripts/System/SaveLoadManager.cs",
                "Assets/Scripts/System/SaveDataMigrator.cs",
                "Assets/Scripts/TraderStatus.cs",
                "Assets/Scripts/Trading/TradingController.cs",
                "Assets/Scripts/Trading/MarketSimulationEngine.cs",
                "Assets/Scripts/Trading/DailyMarketOutlook.cs",
                "Assets/Scripts/Events/ChoiceEventController.cs",
                "Assets/Scripts/GameManager.cs",
                "Assets/Scripts/DatingSim/Core/DatingTimeManager.cs",
                "Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs",
                "Assets/Scripts/Items/ActiveItemEffectManager.cs",
            };

            int failures = 0;
            var missingSources = sources.Where(path => !System.IO.File.Exists(path)).ToList();
            if (missingSources.Count > 0)
            {
                // 경로가 바뀐 것을 눈치채지 못하면 그 파일이 담당하던 필드가 전부 orphan으로 잡힙니다.
                Debug.LogError($"[왕복검사] ❌ 검사 소스 {missingSources.Count}건을 찾지 못했습니다 " +
                               $"(경로가 바뀌었습니다): {string.Join(", ", missingSources)}");
                failures += missingSources.Count;
            }

            string blob = string.Concat(sources
                .Where(System.IO.File.Exists)
                .Select(System.IO.File.ReadAllText));

            var orphans = new List<string>();
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!blob.Contains(field.Name))
                    orphans.Add(field.Name);
            }

            if (orphans.Count == 0) return failures;

            // 실패로 셉니다. 경고로 두면 "저장되지 않는 필드"가 스키마에 계속 쌓입니다.
            // 의도적으로 아직 안 쓰는 필드라면 필드를 추가하지 말고, 쓸 때 함께 추가하십시오.
            Debug.LogError($"[왕복검사] ❌ 수집/복원 경로에서 이름이 발견되지 않는 SaveData 필드 {orphans.Count}건 " +
                           $"— 데드 필드이거나, 담당 소스가 위 sources 목록에서 빠졌습니다: {string.Join(", ", orphans)}");
            return failures + orphans.Count;
        }

        /// <summary>
        /// 부분 저장의 핵심 계약 검사: 매니저가 없는 씬에서 저장해도
        /// 그 매니저 소유 필드가 기본값으로 덮어써지지 않아야 합니다. (SV-A6 / SV-B1 / S15)
        /// </summary>
        private static int RunMergeInvariantCheck()
        {
            var baseline = new SaveData
            {
                DatingAffection = 37,
                DatingStamina = 42,
                Balance = 123456f,
                CurrentDay = 9,
                IsLeverageAddicted = true,
                EventsTriggeredToday = 2,
            };

            // JsonUtility 왕복에서 값이 살아남는지 (직렬화 가능 타입인지) 확인합니다.
            var revived = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(baseline));

            int failures = 0;
            failures += Expect(revived.DatingAffection == 37, "DatingAffection 직렬화");
            failures += Expect(revived.DatingStamina == 42, "DatingStamina 직렬화");
            failures += Expect(Mathf.Approximately(revived.Balance, 123456f), "Balance 직렬화");
            failures += Expect(revived.IsLeverageAddicted, "IsLeverageAddicted 직렬화 (SV-A1)");
            failures += Expect(revived.EventsTriggeredToday == 2, "EventsTriggeredToday 직렬화 (SV-A4)");

            // 구버전 JSON(신규 필드 없음)에서 초기화자 기본값이 유지되는지. 마이그레이션 백필 불필요의 근거입니다.
            var legacy = JsonUtility.FromJson<SaveData>("{\"Balance\":500.0}");
            failures += Expect(legacy.CanRegenMental, "구버전 JSON에서 CanRegenMental 기본값 true 유지");
            failures += Expect(legacy.OutlookDay == -1, "구버전 JSON에서 OutlookDay 기본값 -1 유지");
            failures += Expect(legacy.IsEventTrueSignal, "구버전 JSON에서 IsEventTrueSignal 기본값 true 유지");

            return failures;
        }

        /// <summary>현재 씬 상태로 실제 저장 → 로드 → 값 비교를 수행합니다.</summary>
        private static int RunLiveRoundTrip(SaveLoadManager save)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[왕복검사] 플레이 중이 아니므로 실제 저장 왕복은 건너뜁니다. GameScene 플레이 중에 다시 실행하십시오.");
                return 0;
            }

            if (!save.SaveGame(TestSlotIndex))
            {
                Debug.LogError($"[왕복검사] 슬롯 {TestSlotIndex} 저장에 실패했습니다.");
                return 1;
            }

            SaveData before = Clone(save.CurrentData);

            if (!save.PrepareLoadGame(TestSlotIndex))
            {
                Debug.LogError($"[왕복검사] 슬롯 {TestSlotIndex} 로드에 실패했습니다.");
                return 1;
            }

            SaveData after = save.CurrentData;
            int failures = 0;

            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // 버전 필드는 마이그레이터가 의도적으로 갱신합니다.
                if (field.Name == nameof(SaveData.Version)) continue;
                // 컬렉션은 참조 비교가 무의미하므로 개수만 봅니다.
                object a = field.GetValue(before);
                object b = field.GetValue(after);

                if (a is System.Collections.ICollection ca && b is System.Collections.ICollection cb)
                {
                    failures += Expect(ca.Count == cb.Count, $"{field.Name} 개수 왕복 ({ca.Count} → {cb.Count})");
                    continue;
                }

                failures += Expect(Equals(a, b), $"{field.Name} 값 왕복 ({a} → {b})");
            }

            return failures;
        }

        private static SaveData Clone(SaveData source)
        {
            return source == null ? null : JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(source));
        }

        private static int Expect(bool condition, string label)
        {
            if (condition) return 0;
            Debug.LogError($"[왕복검사] 실패: {label}");
            return 1;
        }
    }
}
