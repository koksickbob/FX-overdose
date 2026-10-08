using FXOverdose.Trading;

namespace FXOverdose.DatingSim.Dialogue
{
    public enum TalkCategory
    {
        Greeting,
        Eating,
        Sleeping,
        Playing,
        Anger,
        Affection,
    }

    /// <summary>
    /// 장면이 성립하는 시간대. 남은 시간 슬롯(5칸)으로 판정합니다.
    /// 5=아침 · 4=낮 · 3=저녁 · 2,1=밤. 밤만 두 칸입니다.
    ///
    /// 이 축이 없으면 "야식 라면이 불었어" 장면이 대낮에 열립니다. 장면은 시간을 갖기 때문입니다.
    /// </summary>
    [System.Flags]
    public enum TalkTime
    {
        Morning = 1,
        Noon = 2,
        Evening = 4,
        Night = 8,
        Any = Morning | Noon | Evening | Night,
    }

    /// <summary>
    /// 플레이어 성격 5축 (바이블 2.2절). 어느 축을 쓰는지 데이터에 남깁니다.
    ///
    /// 태그가 없던 시절 실측에서 <b>무던함이 75%, 감춘 불안이 0건</b>이었습니다.
    /// 무던함은 기본값이지 전부가 아닙니다. 적히지 않으면 편중을 잡을 수 없어 태그를 필수로 둡니다. (P-1)
    /// </summary>
    public enum TalkTrait
    {
        Plain,    // 무던함 — 기본값. 짧게 받는다
        Warm,     // 속정 — 걱정을 행동·조건으로 돌려 말한다
        Duty,     // 책임감 — 문제를 자기 몫으로 가져온다
        Anxious,  // 감춘 불안 — 자기 불안이 말줄임으로 샌다. 설명하지 않는다
        Waver,    // 우유부단 — 끊지 못하고 끌려간다
    }

    /// <summary>
    /// 플레이어(오빠)의 선택지. 25자 이내, 반말, 자칭은 "나".
    /// 캐릭터 기준은 바이블 2.2절, 말투 하드 룰은 4.6절입니다.
    /// 요미보다 항상 한 톤 낮게 씁니다. 점수는 거절/수락이 아니라 마음이 드러난 정도입니다.
    /// +0은 냉담이 아니라 회피·얼버무림입니다.
    /// -1은 요미의 아픈 곳을 <b>외면·묵살</b>하는 선택지입니다 (2026-08-14 신설).
    /// 비웃음·조롱은 여전히 금지 — 플레이어의 결함(화면 우선, 귀찮음)이 그대로 나간 말이어야 합니다.
    /// </summary>
    public readonly struct TalkChoice
    {
        public readonly string Text;
        public readonly TalkTrait Trait;
        public readonly int Affection; // -1 ~ +2. 토픽 총획득 +3 / 총감소 -2 규격 (2026-08-14)

        /// <summary>
        /// 고른 직후 요미가 <b>그 선택에</b> 반응하는 1줄. null이면 생략합니다. (R-2)
        /// 반응만 합니다. 화제를 옮기는 건 다음 노드의 몫입니다 — 안 지키면 같은 말을 두 번 합니다. (TS15)
        /// </summary>
        public readonly string Reply;

        /// <summary>축은 <b>선택 인자가 아닙니다.</b> 기본값을 주면 전부 Plain으로 굳습니다. (TS29)</summary>
        public TalkChoice(string text, TalkTrait trait, int affection, string reply = null)
        {
            Text = text;
            Trait = trait;
            Affection = affection;
            Reply = reply;
        }
    }

    /// <summary>
    /// 요미의 말풍선 1~3개 + 선택지 2~4개.
    ///
    /// 사람은 문장을 완성해서 보내지 않고 끊어서 연달아 보냅니다. 그래서 노드 하나가 말풍선 여러 개입니다. (R-1)
    /// <b>"※"로 시작하는 줄은 지문입니다</b> — 말풍선 없이 회색 서술로 출력되며 요미의 대사가 아닙니다. (10.3절)
    /// 지문에는 행동과 사물만 적습니다. 감정을 설명하면 대사가 할 일을 뺏습니다.
    /// </summary>
    public readonly struct TalkNode
    {
        /// <summary>지문 줄임을 나타내는 접두사. 출력 시 UI가 떼어냅니다. 린터의 말투 검사에서도 제외됩니다.</summary>
        public const string NarrationMark = "※";

        public readonly string[] YomiLines;
        public readonly TalkChoice[] Choices;

        /// <summary>말풍선 1개짜리 노드. 기존 데이터가 그대로 컴파일되도록 남겨 둡니다.</summary>
        public TalkNode(string yomiLine, params TalkChoice[] choices)
        {
            YomiLines = new[] { yomiLine };
            Choices = choices;
        }

        public TalkNode(string[] yomiLines, params TalkChoice[] choices)
        {
            YomiLines = yomiLines;
            Choices = choices;
        }

        public static bool IsNarration(string line)
        {
            return !string.IsNullOrEmpty(line) && line.StartsWith(NarrationMark);
        }

        /// <summary>지문 접두사를 떼어낸 본문. 일반 대사는 그대로 돌려줍니다.</summary>
        public static string StripMark(string line)
        {
            return IsNarration(line) ? line.Substring(NarrationMark.Length).TrimStart() : line;
        }
    }

    /// <summary>시나리오 1편(= 장면). 노드 4~8개.</summary>
    public readonly struct TalkTopic
    {
        public readonly string Id;
        public readonly TalkCategory Category;
        public readonly TalkNode[] Nodes;
        public readonly string ClosingLine;

        /// <summary>
        /// 해금에 필요한 호감도. 판정은 현재 호감도가 아니라 <b>역대 최고치</b>로 합니다.
        /// 호감도가 깎였다고 이미 열린 화제가 다시 잠기면 안 되기 때문입니다. (TS8)
        /// </summary>
        public readonly int MinAffection;

        /// <summary>이 값을 넘으면 더 이상 나오지 않습니다. 현재는 전부 100(= 닫지 않음).</summary>
        public readonly int MaxAffection;

        /// <summary>이 장면이 성립하는 시간대. 여러 개를 겹쳐 둘 수 있습니다.</summary>
        public readonly TalkTime Time;

        public TalkTopic(string id, TalkCategory category, TalkTime time, int minAffection, int maxAffection,
            string closingLine, params TalkNode[] nodes)
        {
            Id = id;
            Category = category;
            Time = time;
            MinAffection = minAffection;
            MaxAffection = maxAffection;
            ClosingLine = closingLine;
            Nodes = nodes;
        }

        public bool IsUnlocked(int peakAffection)
        {
            return peakAffection >= MinAffection && peakAffection <= MaxAffection;
        }

        public bool FitsTime(TalkTime now)
        {
            return (Time & now) != 0;
        }
    }

    /// <summary>
    /// 호감도 티어 경계. 단일 진실 원천: docs/P2_04_System/Affection_Tier_Table.md (2026-08-14 확정).
    /// 해금 판정은 역대 최고치(peak), 연출 판정은 현재치를 입력으로 씁니다. 90은 T3입니다.
    /// 경계값은 세이브에 저장하지 않습니다 — 저장하면 이중 진실 원천이 됩니다.
    /// </summary>
    public static class AffectionTier
    {
        public const int T2Min = 31;
        public const int T3Min = 61;
        public const int T4Min = 91;   // T4 전면화 신설 (2026-08-14)
    }

    /// <summary>
    /// 요미 선택형 대화 데이터.
    ///
    /// ScriptableObject가 아니라 C# 정적 테이블인 이유는 폰트 프리베이크 때문입니다.
    /// PrebakeTMPFont는 .cs 파일만 스캔하므로, .asset에 넣으면 새 한글이 □로 렌더됩니다.
    /// 대사를 추가/수정한 뒤에는 Tools/Prebake All Scripts Text into Font 를 실행하십시오.
    ///
    /// 토픽은 화제가 아니라 <b>장면</b>입니다. 시간·장소·요미가 하려던 것·어긋난 것·
    /// 그리고 <b>요미가 말하지 않을 것</b>을 먼저 정하고 씁니다. 숨기는 게 있어야 대사에 밀도가 생깁니다. (10.2·N-1)
    ///
    /// 대사 규격은 docs/P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md,
    /// 밀도와 작성 지침은 docs/P2_04_System/YomiRoom_ChoiceTalk_System_Plan.md 10장을 따릅니다.
    /// 시나리오 번호는 영구 식별자입니다. 폐기해도 재사용하지 마십시오. (TS5)
    ///
    /// 노드는 감정 곡선 하나를 나눠 갖습니다 — 도입 · 곁길 · 균열 · 직면 · 전환 · 착지.
    /// 호감도는 노드가 아니라 <b>토픽 단위로 배분</b>합니다. (2026-08-14 개편, 14장)
    ///   · 총획득 정확히 +3 (직면 +2 · 착지 +1) — 자유 채팅이 하루 1회가 되면서 하루 획득 상한이기도 합니다
    ///   · 총감소 정확히 -2 (-1 선택지를 서로 다른 노드에 2개) — 한 판에서 잃을 수 있는 최대치
    ///   · 힌트 임계는 이 배분에 맞춰 2(모호)/3(명시)입니다
    /// </summary>
    public static class YomiTalkTopics
    {
        /// <summary>남은 시간 슬롯(5칸)을 시간대로 옮깁니다. 밤만 두 칸(2·1)입니다.</summary>
        public static TalkTime TimeOfSlot(int remainingSlots)
        {
            if (remainingSlots >= 5) return TalkTime.Morning;
            if (remainingSlots == 4) return TalkTime.Noon;
            if (remainingSlots == 3) return TalkTime.Evening;
            return TalkTime.Night;
        }

        /// <summary>
        /// 방에 들어왔을 때의 선제 인사. 시간대별로 갈립니다.
        /// 오빠는 같은 원룸에 있으므로 "왔다"가 아니라 "이제 봤네" 쪽입니다.
        /// 빈 문자열이면 인사하지 않습니다 (2026-10-08 요미 대사 전면 삭제로 전부 비어 있음).
        /// </summary>
        public static string GreetingFor(TalkTime time)
        {
            switch (time)
            {
                case TalkTime.Morning: return "";
                case TalkTime.Noon: return "";
                case TalkTime.Evening: return "";
                default: return "";
            }
        }

        public static readonly TalkTopic[] All =
        {
            // 전제: 오빠의 좁은 자취방(원룸) 하나. 요미가 얹혀산다. 둘은 같은 공간에 있다.
            //       그래서 갈등은 "오빠가 늦게 온다"가 아니라 "옆에 있는데 화면만 본다"에서 나온다.
            //
            // 곁길 노드는 전부 <b>요미가 오빠에게 묻는</b> 자리다. 요미가 묻지 않으면 플레이어는
            // 응답만 하게 되고, 응답만 하는 사람에게서는 감춘 불안이 나올 수 없다. (12.2절 P-3)
            //
            // 2026-10-08 요미 대사 전면 삭제로 비어 있습니다. 토픽이 없으면 '대화하기'는
            // "지금 시간대에 나눌 이야기가 없어요." 안내로 끝납니다 (YomiRoomManager.TryStartTalk).
            // 폐기된 토픽 ID — 세이브의 대화 이력(TalkTopicsSeenTotal·TalkCompletedFlags·TalkChoiceHistory)에
            // 남아 있으므로 새 토픽에 재사용하지 마십시오 (TS5):
            //   TALK_GREET_001 TALK_PLAY_001 TALK_MEAL_001 TALK_ANGER_001 TALK_SLEEP_001 TALK_LOVE_001 TALK_ANGER_002 TALK_LOVE_002 TALK_LOVE_003
        };

        /// <summary>
        /// 차트 방향성 힌트 대사. [Regime][티어] 로 접근합니다.
        /// 티어 1은 자칭 "나"로 흘리는 예감, 티어 2는 자칭 "요미"로 못 박는 단언입니다.
        /// Squeeze 티어 2는 방향 단어(위/아래)를 쓰지 않습니다. 그 날은 방향 자체가 없기 때문입니다. (S4)
        ///
        /// 모든 힌트에는 <b>주어(차트/장/시세)를 반드시 넣습니다.</b> "오늘 떨어져!"처럼 주어를
        /// 생략하면 플레이어에게 하는 명령("떨어져 있어")으로 읽힙니다. (2026-08-14, S5)
        /// 표가 비어 있으면 힌트를 발급하지 않습니다 (YomiRoomManager.TryIssueHint). 2026-10-08 전면 삭제로 8개 모두 비어 있습니다.
        /// </summary>
        public static string[] HintLinesFor(MarketSimulationEngine.MarketRegime regime, int tier)
        {
            bool clear = tier >= 2;

            switch (regime)
            {
                case MarketSimulationEngine.MarketRegime.Bull:
                    return clear ? BullClear : BullVague;
                case MarketSimulationEngine.MarketRegime.Bear:
                    return clear ? BearClear : BearVague;
                case MarketSimulationEngine.MarketRegime.Squeeze:
                    return clear ? SqueezeClear : SqueezeVague;
                default:
                    return clear ? SidewaysClear : SidewaysVague;
            }
        }

        private static readonly string[] BullVague =
        {
        };

        private static readonly string[] BullClear =
        {
        };

        private static readonly string[] BearVague =
        {
        };

        private static readonly string[] BearClear =
        {
        };

        private static readonly string[] SidewaysVague =
        {
        };

        private static readonly string[] SidewaysClear =
        {
        };

        private static readonly string[] SqueezeVague =
        {
        };

        // Squeeze는 방향이 없는 날입니다. 방향 단어를 쓰면 반드시 거짓말이 됩니다. (S4)
        private static readonly string[] SqueezeClear =
        {
        };
    }
}
