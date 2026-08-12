using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FXOverdose.AI.LLM
{
    /// <summary>
    /// LLM이 생성한 돌발 이벤트 텍스트가 화면에 나가도 되는지 판정하는 출력 위생 검사기입니다.
    ///
    /// 기존 검증은 "한글이 5자 이상인가"만 봤기 때문에, 프롬프트에 박혀 있던 안내 문구
    /// (예: "[한국어로 이벤트 제목 작성]" = 한글 11자)를 모델이 그대로 복창해도 통과했습니다.
    /// 이 클래스는 다음 4가지를 검사하여 한 항목이라도 실패하면 전체를 기각합니다.
    ///
    ///   1. 안내 문구 마커 잔존 (대괄호 지시문, 필드명, 지시 동사)
    ///   2. 프롬프트 복창 (지시문 블록과 EchoWindow자 이상 연속 일치)
    ///   3. 필드별 길이 상한/하한
    ///   4. 한국어 비율 및 이질 문자(한자/가나/키릴) 혼입
    ///
    /// 판정에 쓰이는 "복창 대조 코퍼스"는 프롬프트 전체가 아니라 **정적 지시문 블록**입니다.
    /// 테마 서술·시장 상황·선택지 힌트 같은 동적 컨텍스트는 기사 본문이 정당하게 재사용할 수
    /// 있으므로 대조 대상에서 제외해야 오탐이 나지 않습니다.
    /// </summary>
    public static class LLMOutputSanitizer
    {
        // ── 필드별 길이 규격 ───────────────────────────────────────────────
        public const int MinTitleLength = 4;
        public const int MaxTitleLength = 40;
        public const int MinDescriptionLength = 10;
        public const int MaxDescriptionLength = 200;
        public const int MinMonologueLength = 5;
        public const int MaxMonologueLength = 60;

        /// <summary>복창으로 간주할 연속 일치 길이(공백·문장부호 제거 기준).</summary>
        public const int EchoWindow = 12;

        /// <summary>전체 문자 대비 한글 음절의 최소 비율.</summary>
        public const float MinKoreanRatio = 0.45f;

        /// <summary>
        /// 출력에 남아 있으면 즉시 기각하는 금칙어. 프롬프트 지시문·JSON 필드명·안내 동사입니다.
        /// </summary>
        private static readonly string[] BannedMarkers =
        {
            "ScenarioTitle", "ScenarioDescription", "AIMonologue",
            "scenariotitle", "scenariodescription", "aimonologue",
            "안내 텍스트", "안내텍스트",
            "이벤트 제목 작성", "상황 묘사 작성", "다급한 한마디",
            "한국어로", "작성하세요", "작성해주세요", "응답하세요", "출력하세요",
            "JSON", "json",
            "예시)", "작성 예시", "말투를 모방",
            "IMPORTANT", "assistant", "Assistant",
        };

        /// <summary>
        /// 검사 결과. 기각 사유를 담아 폴백률 계측과 로그에 쓰입니다.
        /// </summary>
        public readonly struct Verdict
        {
            public readonly bool Accepted;
            public readonly string Reason;

            private Verdict(bool accepted, string reason)
            {
                Accepted = accepted;
                Reason = reason;
            }

            public static Verdict Ok() => new Verdict(true, null);
            public static Verdict Reject(string reason) => new Verdict(false, reason);
        }

        /// <summary>
        /// 생성 결과를 검사합니다. 한 필드라도 실패하면 전체를 기각합니다.
        /// </summary>
        /// <param name="data">LLM이 생성한 3필드 데이터</param>
        /// <param name="instructionCorpus">
        /// 복창 대조용 정적 지시문 블록. null이면 복창 검사를 건너뜁니다.
        /// </param>
        public static Verdict Inspect(GeneratedChoiceEventData data, string instructionCorpus)
        {
            if (data == null) return Verdict.Reject("데이터가 null");

            string normalizedCorpus = string.IsNullOrEmpty(instructionCorpus)
                ? null
                : NormalizeForMatch(instructionCorpus);

            Verdict v;

            v = InspectField(data.ScenarioTitle, "ScenarioTitle", MinTitleLength, MaxTitleLength, normalizedCorpus);
            if (!v.Accepted) return v;

            v = InspectField(data.ScenarioDescription, "ScenarioDescription", MinDescriptionLength, MaxDescriptionLength, normalizedCorpus);
            if (!v.Accepted) return v;

            v = InspectField(data.AIMonologue, "AIMonologue", MinMonologueLength, MaxMonologueLength, normalizedCorpus);
            if (!v.Accepted) return v;

            // 서로 다른 필드에 같은 문장을 복사해 넣는 저품질 출력 차단
            if (SameContent(data.ScenarioTitle, data.ScenarioDescription) ||
                SameContent(data.ScenarioTitle, data.AIMonologue) ||
                SameContent(data.ScenarioDescription, data.AIMonologue))
            {
                return Verdict.Reject("필드 간 내용이 중복됨");
            }

            return Verdict.Ok();
        }

        private static Verdict InspectField(string value, string fieldName, int minLength, int maxLength, string normalizedCorpus)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Verdict.Reject($"{fieldName}이(가) 비어 있음");
            }

            string trimmed = value.Trim();

            // 1) 길이 규격
            if (trimmed.Length < minLength)
            {
                return Verdict.Reject($"{fieldName} 길이 미달 ({trimmed.Length}자 < {minLength}자)");
            }
            if (trimmed.Length > maxLength)
            {
                return Verdict.Reject($"{fieldName} 길이 초과 ({trimmed.Length}자 > {maxLength}자)");
            }

            // 2) 안내 문구 마커
            for (int i = 0; i < BannedMarkers.Length; i++)
            {
                if (trimmed.IndexOf(BannedMarkers[i], StringComparison.Ordinal) >= 0)
                {
                    return Verdict.Reject($"{fieldName}에 금칙어 '{BannedMarkers[i]}' 잔존");
                }
            }

            // 3) 대괄호 지시문 잔존 — "[...]" 형태가 통째로 남아 있으면 안내 문구 복창으로 간주.
            //    단 "[속보]", "[긴급]" 같은 짧은 말머리는 기사체로 정당하므로 6자 이상만 기각합니다.
            if (ContainsLongBracketBlock(trimmed, 6))
            {
                return Verdict.Reject($"{fieldName}에 대괄호 안내문 잔존");
            }

            // 4) 중괄호 — JSON 조각이 문자열 안에 섞여 들어온 경우
            if (trimmed.IndexOf('{') >= 0 || trimmed.IndexOf('}') >= 0)
            {
                return Verdict.Reject($"{fieldName}에 JSON 중괄호 혼입");
            }

            // 5) 한국어 검사
            Verdict korean = InspectKorean(trimmed, fieldName);
            if (!korean.Accepted) return korean;

            // 6) 프롬프트 복창
            if (normalizedCorpus != null && IsEchoOf(trimmed, normalizedCorpus))
            {
                return Verdict.Reject($"{fieldName}이(가) 프롬프트 지시문을 {EchoWindow}자 이상 복창");
            }

            return Verdict.Ok();
        }

        private static Verdict InspectKorean(string text, string fieldName)
        {
            int korean = 0;
            int foreign = 0;
            int meaningful = 0;

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;

                if (IsHangulSyllable(c) || IsHangulJamo(c))
                {
                    korean++;
                    meaningful++;
                }
                else if (IsForeignScript(c))
                {
                    foreign++;
                    meaningful++;
                }
                else if (char.IsLetter(c))
                {
                    // 라틴 알파벳 등 — BTC, AI 같은 약어는 허용하되 비율 계산에는 포함
                    meaningful++;
                }
                else if (char.IsDigit(c))
                {
                    meaningful++;
                }
                // 문장부호·기호는 비율 계산에서 제외
            }

            if (foreign > 0)
            {
                return Verdict.Reject($"{fieldName}에 한자/가나/키릴 등 이질 문자 {foreign}자 혼입");
            }

            if (meaningful == 0)
            {
                return Verdict.Reject($"{fieldName}에 유효 문자가 없음");
            }

            float ratio = (float)korean / meaningful;
            if (ratio < MinKoreanRatio)
            {
                return Verdict.Reject($"{fieldName} 한글 비율 부족 ({ratio:P0} < {MinKoreanRatio:P0})");
            }

            return Verdict.Ok();
        }

        /// <summary>
        /// 정규화한 텍스트에서 EchoWindow자 길이의 창을 밀며, 하나라도 코퍼스에 포함되면 복창으로 판정합니다.
        /// </summary>
        private static bool IsEchoOf(string text, string normalizedCorpus)
        {
            string normalized = NormalizeForMatch(text);
            if (normalized.Length < EchoWindow) return false;

            int limit = normalized.Length - EchoWindow;
            for (int i = 0; i <= limit; i++)
            {
                string window = normalized.Substring(i, EchoWindow);
                if (normalizedCorpus.IndexOf(window, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 공백과 문장부호를 제거해 비교 안정성을 높입니다. 문자/숫자만 남깁니다.
        /// </summary>
        public static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool SameContent(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            string na = NormalizeForMatch(a);
            string nb = NormalizeForMatch(b);
            if (na.Length == 0 || nb.Length == 0) return false;
            return string.Equals(na, nb, StringComparison.Ordinal);
        }

        private static bool ContainsLongBracketBlock(string text, int minInnerLength)
        {
            int open = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '[')
                {
                    open = i;
                }
                else if (c == ']' && open >= 0)
                {
                    if (i - open - 1 >= minInnerLength) return true;
                    open = -1;
                }
            }
            return false;
        }

        private static bool IsHangulSyllable(char c) => c >= 0xAC00 && c <= 0xD7A3;

        private static bool IsHangulJamo(char c) =>
            (c >= 0x3131 && c <= 0x318E) ||   // 호환 자모 (ㅋㅋ, ㅠㅠ 등)
            (c >= 0x1100 && c <= 0x11FF);     // 조합용 자모

        private static bool IsForeignScript(char c) =>
            (c >= 0x4E00 && c <= 0x9FFF) ||   // CJK 통합 한자
            (c >= 0x3400 && c <= 0x4DBF) ||   // CJK 확장 A
            (c >= 0xF900 && c <= 0xFAFF) ||   // CJK 호환 한자
            (c >= 0x3040 && c <= 0x309F) ||   // 히라가나
            (c >= 0x30A0 && c <= 0x30FF) ||   // 가타카나
            (c >= 0x0400 && c <= 0x04FF) ||   // 키릴
            (c >= 0x0600 && c <= 0x06FF);     // 아랍

        // ── UI 최종 방어선 ────────────────────────────────────────────────

        /// <summary>
        /// UI 렌더 직전 마지막 방어선입니다. 검사기를 우회해 들어온 문자열이라도
        /// 길이를 강제로 잘라 레이아웃이 무너지는 것을 막고, 잘렸다는 사실을 경고 로그로 남깁니다.
        /// </summary>
        public static string TruncateForDisplay(string text, int maxLength, string fieldName)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string trimmed = text.Trim();
            if (trimmed.Length <= maxLength) return trimmed;

            Debug.LogWarning($"[LLMOutputSanitizer] ⚠️ UI 방어선 작동: {fieldName} 길이 {trimmed.Length}자 → {maxLength}자로 절단. 원문: {trimmed}");
            return trimmed.Substring(0, maxLength).TrimEnd() + "…";
        }

        /// <summary>
        /// 표시 직전 텍스트에 프롬프트 잔재로 보이는 패턴이 있는지 확인합니다.
        /// 기각이 아니라 계측용 경고이며, 검사기를 통과하지 않는 경로(하드코딩 이벤트 등)까지 감시합니다.
        /// </summary>
        public static bool LooksLikePromptResidue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            for (int i = 0; i < BannedMarkers.Length; i++)
            {
                if (text.IndexOf(BannedMarkers[i], StringComparison.Ordinal) >= 0) return true;
            }
            return ContainsLongBracketBlock(text, 6) || text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0;
        }
    }

    /// <summary>
    /// 폴백 발생률 계측기. 계획서 리스크 #5("검증 강화로 폴백률이 급증하면 LLM 제거로 전환")의
    /// 판단 근거를 남기기 위해 세션 단위 통계를 셉니다.
    /// </summary>
    public static class LLMGenerationStats
    {
        private static int attempts;
        private static int accepted;
        private static readonly List<string> rejectReasons = new List<string>();

        public static int Attempts => attempts;
        public static int Accepted => accepted;
        public static float FallbackRate => attempts == 0 ? 0f : 1f - ((float)accepted / attempts);

        public static void RecordAccepted()
        {
            attempts++;
            accepted++;
            LogSummary();
        }

        public static void RecordRejected(string reason)
        {
            attempts++;
            rejectReasons.Add(reason ?? "(사유 없음)");
            if (rejectReasons.Count > 50) rejectReasons.RemoveAt(0);
            LogSummary();
        }

        private static void LogSummary()
        {
            Debug.Log($"[LLMGenerationStats] 누적 {attempts}회 중 채택 {accepted}회 / 폴백률 {FallbackRate:P0}");
            if (attempts >= 10 && FallbackRate > 0.5f)
            {
                Debug.LogWarning($"[LLMGenerationStats] ⚠️ 폴백률이 50%를 초과했습니다 ({FallbackRate:P0}). " +
                                 "계획서 리스크 #5에 따라 LLM 경로 유지 여부를 재검토하십시오.");
            }
        }

        public static string DumpReasons()
        {
            if (rejectReasons.Count == 0) return "(기각 사례 없음)";
            var sb = new StringBuilder();
            for (int i = 0; i < rejectReasons.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {rejectReasons[i]}");
            }
            return sb.ToString();
        }

        public static void Reset()
        {
            attempts = 0;
            accepted = 0;
            rejectReasons.Clear();
        }
    }
}
