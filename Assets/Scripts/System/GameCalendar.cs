using System;
using System.Globalization;

namespace FXOverdose.Core
{
    /// <summary>
    /// 게임 내 달력 날짜의 직렬화 포맷과 화면 표기를 한곳에 모읍니다.
    ///
    /// 흩어놓지 않는 이유는 두 가지입니다.
    ///  1) 표기 형식(`6월 26일`)이 7개 UI 파일에 중복됩니다.
    ///  2) 직렬화는 반드시 <see cref="CultureInfo.InvariantCulture"/>여야 합니다. 이걸 빼먹으면
    ///     태국(불기)·일본(연호) 로케일에서 연도가 2569 따위로 저장되고 왕복이 깨집니다. (계획서 G-3)
    /// </summary>
    public static class GameCalendar
    {
        /// <summary>세이브에 기록하는 날짜 포맷. 사람이 읽을 수 있어야 세이브 디버깅이 됩니다.</summary>
        public const string SerializedFormat = "yyyy-MM-dd";

        /// <summary>게임 시작 날짜(에폭)의 기본값. 2026년 6월 26일 금요일.</summary>
        public static readonly DateTime DefaultStartDate = new DateTime(2026, 6, 26);

        /// <summary>기본 에폭의 문자열 표현. 마이그레이터가 참조합니다.</summary>
        public static string DefaultStartDateText => ToSerialized(DefaultStartDate);

        public static string ToSerialized(DateTime date)
        {
            return date.ToString(SerializedFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 실패해도 예외를 던지지 않습니다. 손상된 날짜 한 줄 때문에 세이브 전체를 못 읽으면 안 됩니다.
        /// </summary>
        public static bool TryParse(string text, out DateTime date)
        {
            return DateTime.TryParseExact(text, SerializedFormat, CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out date);
        }

        /// <summary>상단바·정산 등 대부분의 화면 표기. 예: `6월 26일`</summary>
        public static string ToKoreanShort(DateTime date) => $"{date.Month}월 {date.Day}일";

        /// <summary>연도까지 밝히는 화면(엔딩 등) 표기. 예: `2026년 6월 26일`</summary>
        public static string ToKoreanFull(DateTime date) => $"{date.Year}년 {date.Month}월 {date.Day}일";
    }
}
