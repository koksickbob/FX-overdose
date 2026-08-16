using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace FXOverdose.DatingSim.Dialogue
{
    /// <summary>
    /// 대사 한 줄을 글자 단위로 찍는 공용 타자기입니다.
    ///
    /// 원래 요미 방 프로토타입(<c>YomiRoomTopDownPrototype</c>) 안에 말풍선 로그와 함께 박혀 있던 것을
    /// 이벤트 시스템이 같이 쓰기 위해 발췌했습니다 (이벤트 시스템 계획 8.2절).
    /// <b>두 벌로 갈라지면 대사의 리듬이 화면마다 달라집니다.</b> 여기가 유일한 출처입니다.
    ///
    /// 발췌하면서 두 가지만 바깥으로 뺐습니다:
    /// <list type="bullet">
    /// <item>스킵 판정 — 방은 화면 아무 곳 클릭, 이벤트는 상태기계가 정합니다.</item>
    /// <item>시간축 — 이벤트는 <b>반드시 unscaled</b>여야 합니다. 설정 메뉴가 <c>Time.timeScale = 0</c>을
    /// 걸기 때문에(<c>SettingsMenuController.OpenMenu</c>) 스케일드 시간을 쓰면 타자기가 멈춥니다.</item>
    /// </list>
    /// </summary>
    public static class DialogueTypewriter
    {
        // 실기로 봐야 정해지는 값들입니다. 흩어 두면 못 고치므로 한 블록에 모읍니다.
        public const float CharInterval = 0.045f;      // 글자 하나 (한글 초당 약 22자)
        public const float PauseComma = 0.08f;         // , 뒤
        public const float PausePeriod = 0.15f;        // . ! ? 뒤
        public const float PauseEllipsis = 0.35f;      // ... 뒤 — 요미의 머뭇거림이 여기서 나옵니다
        public const float LineTailBase = 0.25f;       // 줄 사이 여운 = Base + 글자수 * PerChar
        public const float LineTailPerChar = 0.012f;
        public const float LineTailMax = 0.8f;
        public const float NarrationTail = 0.55f;      // 지문은 타자기 없이 즉시 표시 후 이 텀 (D-4)
        public const float ChoiceDelay = 0.3f;         // 마지막 글자와 동시에 버튼이 튀어나오지 않게 (D-5)

        /// <summary>줄 하나를 다 읽고 나서의 여운. 긴 줄일수록 길되 상한이 있습니다.</summary>
        public static float LineTailFor(string line)
        {
            int length = line != null ? line.Length : 0;
            return Mathf.Min(LineTailMax, LineTailBase + length * LineTailPerChar);
        }

        /// <summary>
        /// 한 글자씩 찍습니다. 구두점에서는 손이 멈춥니다. (D-1 / D-2)
        /// </summary>
        /// <param name="body">대상 TMP. <c>maxVisibleCharacters</c>로 드러냅니다 — 문자열을 자르지 않는 이유는
        /// 행 높이가 전체 문자열로 이미 확정되어 글자가 늘어도 레이아웃이 흔들리지 않기 때문입니다. (TS25)</param>
        /// <param name="skipRequested">true를 돌려주면 그 즉시 전문을 표시하고 끝냅니다.</param>
        /// <param name="useUnscaledTime">이벤트 시스템은 반드시 true. 요미 방은 기존 동작 유지를 위해 false.</param>
        public static IEnumerator TypeLine(TMP_Text body, string text, Func<bool> skipRequested, bool useUnscaledTime)
        {
            if (body == null || string.IsNullOrEmpty(text)) yield break;

            body.maxVisibleCharacters = 0;

            for (int i = 0; i < text.Length; i++)
            {
                body.maxVisibleCharacters = i + 1;

                float wait = CharInterval + PauseAfter(text, i);
                float waited = 0f;
                bool skipped = false;

                while (waited < wait)
                {
                    if (skipRequested != null && skipRequested()) { skipped = true; break; }
                    waited += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }

                if (skipped) break;
            }

            body.maxVisibleCharacters = int.MaxValue; // 남은 글자 즉시 표시
        }

        /// <summary>
        /// 여운만큼 기다리되 스킵이 들어오면 즉시 끊습니다.
        /// 줄 사이 대기에도 스킵이 먹혀야 2단 스킵(현재 줄 완성 → 다음 줄)이 성립합니다. (D-6)
        /// </summary>
        public static IEnumerator WaitTail(float tail, Func<bool> skipRequested, bool useUnscaledTime)
        {
            float waited = 0f;
            while (waited < tail)
            {
                if (skipRequested != null && skipRequested()) yield break;
                waited += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 이 글자 뒤에 얼마나 쉴지. 요미 대사는 말줄임이 압도적으로 많아서,
        /// "..." 뒤의 정지가 머뭇거림을 그대로 연출로 만들어 줍니다. 대사는 한 줄도 안 고칩니다.
        /// </summary>
        public static float PauseAfter(string text, int index)
        {
            char c = text[index];

            if (c == '…') return PauseEllipsis;
            if (c == '.')
            {
                // 점이 이어지는 중간에서는 쉬지 않습니다. 점마다 멈추면 1초를 넘깁니다.
                if (index + 1 < text.Length && text[index + 1] == '.') return 0f;
                bool ellipsis = index >= 2 && text[index - 1] == '.' && text[index - 2] == '.';
                return ellipsis ? PauseEllipsis : PausePeriod;
            }
            if (c == '!' || c == '?') return PausePeriod;
            if (c == ',') return PauseComma;
            return 0f;
        }
    }
}
