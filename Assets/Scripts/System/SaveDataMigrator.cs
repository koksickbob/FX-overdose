using System;
using UnityEngine;

namespace FXOverdose.Core
{
    public static class SaveDataMigrator
    {
        /// <summary>
        /// 마이그레이션이 수행되어 데이터가 갱신되었는지 여부를 반환합니다.
        /// </summary>
        public static bool Migrate(ref SaveData data)
        {
            if (data == null) return false;

            string currentAppVersion = Application.version;
            
            // 기존 데이터 버전을 파악 (문자열이 비어있으면 초기 버전인 1.0.0 으로 간주)
            string dataVersion = string.IsNullOrEmpty(data.Version) ? "1.0.0" : data.Version;

            if (dataVersion == currentAppVersion)
            {
                return false;
            }

            Debug.Log($"[SaveDataMigrator] 저장 데이터 버전({dataVersion})이 현재 게임 버전({currentAppVersion})과 다릅니다. 이관(Migration)을 시작합니다.");

            // ----------------------------------------------------
            // [버전별 마이그레이션 파이프라인]
            // 아래에 구버전 -> 신버전으로 올릴 때 필요한 데이터 보정 로직을 순차적으로 작성합니다.
            // ----------------------------------------------------

            if (CompareVersions(dataVersion, "1.5.0") < 0)
            {
                // 예: 1.5.0 미만 버전에서 넘어오는 경우의 보정 로직
                // Debug.Log("[SaveDataMigrator] 1.5.0 마이그레이션 로직 적용...");
            }
            
            // 더 높은 버전의 마이그레이션이 필요하다면 계속 추가
            // if (CompareVersions(dataVersion, "1.6.0") < 0) { ... hasMigrated = true; }

            // 마이그레이션 파이프라인을 통과한 뒤 최종 버전을 기록
            data.Version = currentAppVersion;
            
            // 비록 파이프라인에 걸리지 않았더라도(예: 데이터 버전이 1.6인데 게임이 1.5인 역방향이거나 사소한 빌드번호 변경), 
            // 버전 태그 자체는 최신화되었으므로 저장 플래그를 true로 반환하여 버전을 맞춰주는 것이 좋습니다.
            return true; 
        }

        /// <summary>
        /// 두 버전 문자열을 비교합니다. v1 < v2 이면 -1, 같으면 0, v1 > v2 이면 1.
        /// 예: "1.4.0" vs "1.5.0" -> -1
        /// </summary>
        private static int CompareVersions(string v1, string v2)
        {
            if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
            if (string.IsNullOrEmpty(v1)) return -1;
            if (string.IsNullOrEmpty(v2)) return 1;

            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');
            
            int maxLength = Mathf.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? (int.TryParse(parts1[i], out int n1) ? n1 : 0) : 0;
                int p2 = i < parts2.Length ? (int.TryParse(parts2[i], out int n2) ? n2 : 0) : 0;
                
                if (p1 < p2) return -1;
                if (p1 > p2) return 1;
            }
            return 0;
        }
    }
}
