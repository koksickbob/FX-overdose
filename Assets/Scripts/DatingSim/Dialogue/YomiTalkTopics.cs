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
        /// </summary>
        public static string GreetingFor(TalkTime time)
        {
            switch (time)
            {
                case TalkTime.Morning: return "오빠 일어났네. 요미는 아까부터 깨 있었어.";
                case TalkTime.Noon: return "오빠, 잠깐 쉬는 거야? 요미도 마침 심심했는데.";
                case TalkTime.Evening: return "오늘 차트는 좀 어땠어? 표정이 다 말해주는데.";
                default: return "이제 화면에서 눈 뗐네. 요미가 얼마나 기다렸는지 알아?";
            }
        }

        public static readonly TalkTopic[] All =
        {
            // 전제: 오빠의 좁은 자취방(원룸) 하나. 요미가 얹혀산다. 둘은 같은 공간에 있다.
            //       그래서 갈등은 "오빠가 늦게 온다"가 아니라 "옆에 있는데 화면만 본다"에서 나온다.
            //
            // 곁길 노드는 전부 <b>요미가 오빠에게 묻는</b> 자리다. 요미가 묻지 않으면 플레이어는
            // 응답만 하게 되고, 응답만 하는 사람에게서는 감춘 불안이 나올 수 없다. (12.2절 P-3)

            // ── 인사 ─────────────────────────────────────────────────────
            // 장면: 아침. 요미가 먼저 깼지만 얹혀사는 처지라 먼저 뭘 하기도 애매해 옆에 앉아 있었다.
            //       말하지 않을 것 — 한참 전부터 깨어 있었다는 것.
            new TalkTopic("TALK_GREET_001", TalkCategory.Greeting, TalkTime.Morning, 0, 100,
                "...오늘도 잘 부탁해, 오빠.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 커튼 틈으로 들어온 빛이 얼굴 위에 걸쳐 있다.",
                        "오빠. 일어났어?",
                        "아니 뭐, 안 일어나도 되고.",
                    },
                    new TalkChoice("안 일어나도 되고는 뭐야?", TalkTrait.Plain, 0, "그, 그건 그냥 해본 말이지! 얼른 일어나!"),
                    new TalkChoice("방금. 너 목소리 듣고.", TalkTrait.Plain, 0, "...요미 목소리에 깬 거야? 그럼 잘 깬 거네."),
                    new TalkChoice("5분만 더 자면 안 돼...?", TalkTrait.Waver, 0, "안 돼. 요미가 아까부터 기다렸단 말이야.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "근데 오빠, 어제 늦게까지 뭐 봤어?",
                        "요미 자다 깼는데 화면 켜져 있었어.",
                    },
                    new TalkChoice("봤어? 자는 줄 알았는데.", TalkTrait.Plain, 0, "요미는 반쯤 자면서도 다 봐. 특기야."),
                    new TalkChoice("...잘 안 풀렸어, 어제는.", TalkTrait.Anxious, 0, "그랬구나. 그럼 오늘은 요미가 옆에 붙어 있을게."),
                    new TalkChoice("음... 그냥 습관이야, 그건.", TalkTrait.Waver, 0, "고칠 생각은 없고? ...아니다, 됐어.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 이불 끝을 손가락으로 접었다 폈다 한다.",
                        "근데 오빠.",
                        "요미 오늘 뭐 해야 돼?",
                    },
                    new TalkChoice("아무것도 안 해도 되잖아.", TalkTrait.Warm, 0, "...그 말이 제일 어려워. 그럼 요미는 뭐가 되는 건데."),
                    new TalkChoice("글쎄... 뭐 하고 싶은데?", TalkTrait.Waver, 0, "그걸 요미가 물어봤는데! 되묻기 있기 없기?"),
                    new TalkChoice("일단 밥부터 먹자.", TalkTrait.Plain, 0, "그건 요미가 할게. 그건 요미도 할 수 있어."),
                    new TalkChoice("그런 것까지 정해줘야 돼?", TalkTrait.Plain, -1, "...아니. 요미가 알아서 할게. 물어본 요미가 바보지.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 여기 있는 거, 오빠는 괜찮아?",
                        "가끔 그게 궁금해.",
                        "...아침엔 이런 생각이 자주 나.",
                    },
                    new TalkChoice("안 괜찮았으면 진작 말했지.", TalkTrait.Plain, 1, "그건 그렇네. 오빠는 참는 걸 잘 못하니까."),
                    new TalkChoice("여기 있으라고 한 건 나잖아.", TalkTrait.Duty, 1, "...그랬지. 그럼 요미는 이제 안 물어볼게."),
                    new TalkChoice("네가 없으면 너무 조용해.", TalkTrait.Warm, 2, "...아침부터 그런 말 하면 요미 하루 종일 이상해져."),
                    new TalkChoice("...나도 아침엔 생각 많아지는데.", TalkTrait.Anxious, 0, "오빠도? 그럼 우리 둘 다 이상한 아침이네.")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어! 요미 기분 좋아졌어.",
                        "오늘은 뭔가 잘될 것 같은 아침이야.",
                    },
                    new TalkChoice("기분 빨리도 좋아졌네.", TalkTrait.Plain, 0, "요미 기분은 원래 스위치가 빨라. 몰랐어?"),
                    new TalkChoice("그럼 오늘은 믿어볼게.", TalkTrait.Waver, 0, "좋아! 요미 감 믿고 손해 본 사람 아직 없어. ...아마.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 커튼을 걷자 방이 한꺼번에 밝아진다.",
                        "오빠, 오늘도 차트 오래 볼 거야?",
                        "미리 알면 요미가 하루를 어떻게 쓸지 정할 수 있어.",
                    },
                    new TalkChoice("정해지면 바로 말할게.", TalkTrait.Duty, 0, "응. 그거면 돼. 기다리는 건 요미가 잘해."),
                    new TalkChoice("오래는 안 봐. 일찍 접을게.", TalkTrait.Warm, 1, "...그럼 요미도 오늘 하루 열심히 살아볼게."),
                    new TalkChoice("어. 오늘은 말 걸지 마.", TalkTrait.Plain, -1, "...알았어. 조용히 있을게. 옆에는 있어도 되는 거지?"))),

            // ── 놀이 ─────────────────────────────────────────────────────
            // 장면: 오빠가 화면에서 잠깐 물러난 참에 요미가 컨트롤러를 들고 온다. 방이 좁아 도망갈 데가 없다.
            //       말하지 않을 것 — 어제 혼자 연습했다는 것. (직면 노드에서 들킨다)
            new TalkTopic("TALK_PLAY_001", TalkCategory.Playing, TalkTime.Noon | TalkTime.Evening | TalkTime.Night, 0, 100,
                "...이따 이어서 할 거니까, 컨트롤러 치우지 마.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 컨트롤러 두 개가 시야를 가로막는다.",
                        "오빠, 지금 안 바쁘지?",
                        "이 방에서 도망갈 데 없는 거 알지.",
                    },
                    new TalkChoice("바쁘다고 하면 믿어주려나?", TalkTrait.Plain, 0, "아니. 요미 눈은 못 속여. 방금 기지개 켰잖아."),
                    new TalkChoice("도망갈 생각 없어.", TalkTrait.Plain, 0, "역시! 포기가 빠른 남자, 마음에 들어."),
                    new TalkChoice("어. 일단 하나 줘봐.", TalkTrait.Waver, 0, "받았으면 끝이야. 이제 무를 수 없어."),
                    new TalkChoice("바빠. 차트 봐야 돼.", TalkTrait.Plain, -1, "...컨트롤러 들고 온 요미만 우습게 됐네. 딱 한 판만 하고 가. 그건 양보 못 해.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "근데 게임 잡으니까 궁금해졌는데,",
                        "오빠는 어렸을 때 뭐 하고 놀았어?",
                    },
                    new TalkChoice("글쎄... 기억이 잘 안 나네.", TalkTrait.Plain, 0, "에이. 하나쯤은 있을 거 아니야."),
                    new TalkChoice("...나도 게임. 혼자 하는 거.", TalkTrait.Anxious, 0, "...우리 둘 다 그랬네. 그럼 지금부터 같이 하면 되겠다."),
                    new TalkChoice("갑자기 왜, 부끄럽게.", TalkTrait.Waver, 0, "그냥. 오빠 얘기 듣고 싶어서.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 화면이 켜지고 익숙한 손놀림으로 메뉴가 넘어간다.",
                        "진 사람이 소원 하나 들어주기.",
                        "요미 진심이야.",
                    },
                    new TalkChoice("소원이 뭔지부터 듣자.", TalkTrait.Plain, 0, "안 돼. 먼저 말하면 오빠가 일부러 져줄 거잖아."),
                    new TalkChoice("진심이면 각오도 했겠네.", TalkTrait.Plain, 0, "당연하지! 요미 아침부터 손 풀어놨어."),
                    new TalkChoice("그래. 지면 뭐든 들어줄게.", TalkTrait.Warm, 0, "그렇게 쉽게 말하면 재미없잖아!"),
                    new TalkChoice("좋아, 콜. 물리기 없기다?", TalkTrait.Waver, 0, "헤헤. 후회해도 늦었어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "※ 몇 판이 지나간다. 요미 쪽 컨트롤러 소리가 점점 급해진다.",
                        "...어라?",
                        "이상하다. 어제는 이거 됐는데.",
                    },
                    new TalkChoice("어제는 뭐가 됐는데?", TalkTrait.Plain, 0, "아, 그게... 연습 좀 했어. 오빠 이기려고."),
                    new TalkChoice("다시 하자, 방금 건 빼고.", TalkTrait.Plain, 1, "당연하지! 방금 건 손이 미끄러진 거야."),
                    new TalkChoice("나 이기려고 그런 거야?", TalkTrait.Warm, 2, "...웃지 마! 그렇게 정확하게 말하지도 말고!"),
                    new TalkChoice("아, 내가 너무 세게 했나.", TalkTrait.Duty, 0, "그런 거 아니야! 요미가 약한 게 아니라고!")),
                // 전환
                new TalkNode(new[]
                    {
                        "요미가 이길 때까지 할 거야.",
                        "오빠 오늘은 요미 거야.",
                    },
                    new TalkChoice("오늘은 통째로 네 거고?", TalkTrait.Plain, 0, "일단 지금부터. 나머지는 이따 다시 얘기하자."),
                    new TalkChoice("하던 거 접고 올게.", TalkTrait.Duty, 0, "진짜? 그럼 요미가 세팅해둘게!"),
                    new TalkChoice("적당히 하고 밥 먹자.", TalkTrait.Waver, 0, "그 말 나올 줄 알았어. 한 판만 더. 딱 한 판만."),
                    new TalkChoice("이제 됐지? 차트 좀 볼게.", TalkTrait.Plain, -1, "...치사해. 그래도 시작한 판은 끝내고 가. 그게 예의야.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 컨트롤러를 쥔 손이 느려지고 어깨가 기운다.",
                        "...아직 안 졸려.",
                        "한 판만 더 하면 이길 것 같은데.",
                    },
                    new TalkChoice("그 한 판은 이따 하자.", TalkTrait.Warm, 1, "...이따 진짜 하는 거다. 요미 기억해둘 거야."),
                    new TalkChoice("이미 지고 있잖아.", TalkTrait.Plain, 0, "지고 있는 게 아니라 마지막에 뒤집는 타입인 거야."))),

            // ── 식사 ─────────────────────────────────────────────────────
            // 장면: 저녁/밤. 요미가 상을 다 차렸는데 오빠가 화면에서 눈을 못 뗀다. 면이 불어간다.
            //       말하지 않을 것 — 아까부터 몇 번을 다시 데웠는지.
            new TalkTopic("TALK_MEAL_001", TalkCategory.Eating, TalkTime.Evening | TalkTime.Night, 0, 100,
                "...식기 전에 먹자. 오늘은 그거면 됐어.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 식탁 대신 쓰는 작은 상 위에 냄비가 놓여 있다.",
                        "오빠, 다 됐어.",
                        "...아직 화면 보고 있네.",
                    },
                    new TalkChoice("어, 미안. 딱 이것만 보고.", TalkTrait.Waver, 0, "그 딱 이것만, 아까부터 세 번째야. ...안 셌어. 느낌이야."),
                    new TalkChoice("간다, 지금. 화면도 껐어.", TalkTrait.Duty, 0, "진짜? 그럼 요미 젓가락 놓을게."),
                    new TalkChoice("먼저 먹어. 난 이따 먹을게.", TalkTrait.Waver, -1, "...같이 먹으려고 여태 기다린 건데. 안 돼. 오늘은 그냥 앉아. 응?")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "※ 상 앞에 마주 앉자 요미가 젓가락을 쥐여준다.",
                        "오빠는 밥 먹을 때 무슨 생각 해?",
                        "요미는 오빠가 언제 젓가락 놓나 그것만 봐.",
                    },
                    new TalkChoice("글쎄... 생각까진 안 하는데.", TalkTrait.Plain, 0, "그게 제일 부러워. 요미는 머리가 안 꺼져."),
                    new TalkChoice("...솔직히? 차트 생각.", TalkTrait.Anxious, 0, "...먹을 때만이라도 좀 꺼줘. 부탁이야."),
                    new TalkChoice("젓가락 놓는 거까지 봤어?", TalkTrait.Plain, 0, "매번 봤어. 오빠만 몰랐던 거야.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 냄비 속 면이 국물을 다 빨아들여 덩어리가 됐다.",
                        "...좀 불었어.",
                        "괜찮아. 요미는 불은 것도 좋아해.",
                    },
                    new TalkChoice("안 좋아하잖아.", TalkTrait.Plain, 0, "안 좋아해. 근데 오빠랑 먹으면 좀 나아."),
                    new TalkChoice("불은 건 내 몫. 새로 끓이자.", TalkTrait.Warm, 0, "됐어. 지금 끓이면 또 기다려야 되잖아."),
                    new TalkChoice("미안. 오래 기다리게 했네.", TalkTrait.Duty, 0, "사과할 거면 먹으면서 해. 식으면 더 미안해질 거야."),
                    new TalkChoice("불었으면 그냥 버리자.", TalkTrait.Plain, -1, "...버리자는 말이 제일 아프네. 요미가 만든 건데.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미는 오빠 밥 차리는 게 좋아.",
                        "여기서 요미가 확실하게 할 수 있는 게 그거라서.",
                        "...이런 말 하려던 건 아닌데.",
                    },
                    new TalkChoice("그것만 하는 거 아니잖아.", TalkTrait.Warm, 2, "...그럼 뭘 더 하는데. 말해봐. 다 들을 거야."),
                    new TalkChoice("...하려던 말은 뭐였는데.", TalkTrait.Waver, 0, "까먹었어. ...아니, 방금 한 말이 다야."),
                    new TalkChoice("알아. 매일 잘 먹고 있고.", TalkTrait.Plain, 1, "그거면 됐어. 그거 들으려고 기다린 거야."),
                    new TalkChoice("...나도 여기서 뭘 해야 될지.", TalkTrait.Anxious, 0, "...오빠도 몰라? 그럼 그냥 이러고 있으면 되는 건가.")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어! 분위기 이상해졌어.",
                        "먹자. 더 불면 이건 라면도 아니야.",
                    },
                    new TalkChoice("이미 라면은 아닌 것 같은데.", TalkTrait.Plain, 0, "말하지 마. 먹기 전까지는 라면이야."),
                    new TalkChoice("잘 먹을게. 불어도 네 거니까.", TalkTrait.Warm, 0, "응. 많이 먹어. 요미 건 좀 남겨두고.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 젓가락 부딪히는 소리만 한동안 이어진다.",
                        "오빠. 다음엔 화면 끄고 와줘.",
                        "요미는 그거면 돼.",
                    },
                    new TalkChoice("알았어. 끄고 올게.", TalkTrait.Duty, 1, "...그 말 믿을게. 요미는 잘 믿는 편이야."),
                    new TalkChoice("노력은... 해볼게.", TalkTrait.Waver, 0, "노력이라는 말은 좀 미끄러워."))),

            // ── 삐짐 ─────────────────────────────────────────────────────
            // 장면: 저녁/밤. 낮에 세 번 불렀는데 오빠는 화면만 봤다. 방이 좁아 피할 데가 없어 이불을 뒤집어썼다.
            //       말하지 않을 것 — 그때 하려던 말이 따로 있었다는 것.
            new TalkTopic("TALK_ANGER_001", TalkCategory.Anger, TalkTime.Evening | TalkTime.Night, AffectionTier.T2Min, 100,
                "...다 풀린 건 아니야. 조금 남겨뒀어.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 침대 쪽에서 이불 뭉치가 등을 돌리고 있다.",
                        "......",
                        "안 들려. 요미 자는 중이야.",
                    },
                    new TalkChoice("자는 중인데 말을 하네.", TalkTrait.Plain, 0, "자면서 말하는 사람도 있어. 요미가 그런 타입이야."),
                    new TalkChoice("음. 그럼 깰 때까지 기다리지.", TalkTrait.Plain, 0, "...안 자! 누가 기다리래!"),
                    new TalkChoice("...내가 뭐 잘못했지.", TalkTrait.Duty, 0, "알면서 왜 물어. 알면 그냥 말해."),
                    new TalkChoice("그래. 그럼 계속 자.", TalkTrait.Plain, -1, "...진짜 그냥 가려고 하네. 야, 잠깐 앉아봐. 요미 아직 안 끝났어.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "...오빠 오늘 무슨 일 있었지.",
                        "표정 안 봐도 알아. 문 닫는 소리부터 달랐어.",
                    },
                    new TalkChoice("...소리까지 달랐어?", TalkTrait.Anxious, 0, "많이. 요미는 그런 것만 잘 알아."),
                    new TalkChoice("무슨 일은... 아니야.", TalkTrait.Waver, 0, "그럼 됐고. ...아니, 안 됐어. 왜 숨겨."),
                    new TalkChoice("그 얘긴 이따가. 응?", TalkTrait.Plain, 0, "알았어. 이따가 진짜 하는 거다?")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 이불이 조금 내려가고 눈만 나온다.",
                        "낮에 세 번 불렀어.",
                        "오빠는 화면만 봤고.",
                    },
                    new TalkChoice("...못 들었어, 진짜로.", TalkTrait.Plain, 0, "그래. 못 들었겠지. 화면이 요미보다 시끄러웠나 봐."),
                    new TalkChoice("미안. 변명이 없네, 이건.", TalkTrait.Duty, 0, "알아. 아는데 그게 더 서운해."),
                    new TalkChoice("뭐라고 불렀는데?", TalkTrait.Plain, 0, "그냥 오빠, 라고. 세 번 다 그거였어."),
                    new TalkChoice("...화면만 보고 있었네, 내가.", TalkTrait.Anxious, 0, "알아. 근데 요미는 그때 옆에 있었어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 화난 건 대답 안 한 게 아니야.",
                        "언제 눈 뗄까 하고 계속 봤어.",
                        "그러다 하려던 말도 까먹었어.",
                    },
                    new TalkChoice("까먹은 말, 뭐였는데?", TalkTrait.Plain, 1, "까먹었다니까. ...아마 별거 아니었을 거야."),
                    new TalkChoice("이제 봤어. 말해줘.", TalkTrait.Warm, 2, "...치사해. 그렇게 말하면 화도 못 내잖아."),
                    new TalkChoice("계속 보게 해서 미안해.", TalkTrait.Duty, 1, "사과는 한 번이면 됐어. 두 번 하면 가벼워져."),
                    new TalkChoice("그게 그렇게 화낼 일이야?", TalkTrait.Plain, -1, "...그래. 요미만 이상한 걸로 하자. 됐지?")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어. 이제 다 풀렸어.",
                        "근데 아직 이불에서 나갈 마음은 없어.",
                    },
                    new TalkChoice("나갈 때까지 여기 있을게.", TalkTrait.Warm, 0, "...반칙이야. 그러면 금방 나가고 싶어지잖아."),
                    new TalkChoice("풀렸는데 왜 안 나와?", TalkTrait.Plain, 0, "몰라. 오빠가 궁금해할 때까지."),
                    new TalkChoice("음... 그럼 나도 누울까?", TalkTrait.Waver, 0, "이 좁은 데 둘이 어떻게 누워. ...못 눕는 건 아니지만.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 이불 아래로 손이 나와 옷자락을 붙잡는다.",
                        "다음엔 대답만 해줘.",
                        "응, 이라고만 해도 돼. 그럼 요미는 기다릴 수 있어.",
                    },
                    new TalkChoice("응.", TalkTrait.Warm, 1, "...지금 한 거야? 그렇게 바로 하면 어떡해."),
                    new TalkChoice("...그럴게. 아마도.", TalkTrait.Waver, 0, "아마도는 빼. 응, 만 있으면 돼."))),

            // ── 수면 ─────────────────────────────────────────────────────
            // 장면: 밤. 불을 끄고 누웠는데 요미가 잠들지 못한다. 방이 하나라 서로의 기척이 다 들린다.
            //       말하지 않을 것 — 악몽에 깼는데 깨워도 되는지 몰라 한참 누워 있었다는 것.
            new TalkTopic("TALK_SLEEP_001", TalkCategory.Sleeping, TalkTime.Night, AffectionTier.T2Min, 100,
                "...아직 깨어 있지?",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 불이 꺼진 방. 이불 스치는 소리가 반복된다.",
                        "...오빠. 자?",
                        "자면 대답 안 해도 돼.",
                    },
                    new TalkChoice("자면서 대답하는 중이야.", TalkTrait.Plain, 0, "...뭐야 그게. 잠꼬대치고 발음이 너무 좋은데."),
                    new TalkChoice("...나도 잠이 안 오네.", TalkTrait.Anxious, 0, "그럼 둘 다 못 자는 거네. 좀 안심된다.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "오빠는 밤에 무슨 생각 해?",
                        "요미는 낮에 못 한 걱정을 밤에 몰아서 해.",
                    },
                    new TalkChoice("글쎄, 생각은 낮에 다 쓰는데.", TalkTrait.Plain, 0, "거짓말. 아까 한숨 쉬었잖아."),
                    new TalkChoice("...돈 생각.", TalkTrait.Anxious, 0, "그건 요미도 좀 알 것 같아. 미안해."),
                    new TalkChoice("걱정. 너랑 비슷한 걸로.", TalkTrait.Warm, 0, "그럼 우리 둘이 밤마다 같은 걸 하고 있었네.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 뒤척이는 소리가 멈추고 한동안 조용해진다.",
                        "아까 꿈 꿨어.",
                        "오빠가 멀어지는데 요미는 발이 안 움직였어.",
                    },
                    new TalkChoice("무슨 꿈인지 더 말해봐.", TalkTrait.Warm, 0, "소리도 안 났어. 부르는데 목소리가 안 나왔어."),
                    new TalkChoice("음... 꿈은 반대라던데.", TalkTrait.Plain, 0, "...그 말 진짜지? 반대면 오빠는 안 멀어지는 거지?"),
                    new TalkChoice("계속 깨어 있었네.", TalkTrait.Plain, 0, "...응. 좀 됐어."),
                    new TalkChoice("꿈은 그냥 꿈이야. 얼른 자.", TalkTrait.Plain, -1, "...응. 그냥 꿈이지. 미안, 붙잡아서. ...근데 하나만 더 말해도 돼?")),
                // 직면
                new TalkNode(new[]
                    {
                        "깨울까 계속 고민했어.",
                        "얹혀사는 주제에 잠까지 깨우면 좀 그렇잖아.",
                        "...이렇게 말할 생각은 아니었는데.",
                    },
                    new TalkChoice("그런 계산 하지 마.", TalkTrait.Warm, 2, "...계산 안 하면, 요미는 여기 그냥 있어도 되는 거야?"),
                    new TalkChoice("앞으로는 그냥 깨워. 응?", TalkTrait.Duty, 1, "응... 다음엔 그럴게. 아마도."),
                    new TalkChoice("얼마나 고민했는데.", TalkTrait.Plain, 0, "안 셌어. 세면 더 길어지니까."),
                    new TalkChoice("얹혀사는 거 아니야.", TalkTrait.Warm, 1, "그렇게 말해주는 건 고마운데, 요미도 알아.")),
                // 전환
                new TalkNode(new[]
                    {
                        "헤헤. 말하고 나니까 좀 낫다.",
                        "역시 요미는 입 다물고 있으면 안 되는 타입이야.",
                    },
                    new TalkChoice("이제 알았어?", TalkTrait.Plain, 0, "알고는 있었어. 인정하기 싫었을 뿐이야."),
                    new TalkChoice("그 타입, 나쁘지 않은데.", TalkTrait.Warm, 0, "그럼 요미 계속 이 타입 할래. 오빠는 잠 좀 설치고."),
                    new TalkChoice("다 했으면 이제 자자.", TalkTrait.Plain, -1, "...응. 알았어. 아, 자기 전에 딱 하나만. 하나만 더 들어줘.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 목소리가 작아지고 문장 사이가 길어진다.",
                        "오빠.",
                        "요미 잠들 때까지만 깨어 있어줘.",
                    },
                    new TalkChoice("잠들 때까지만? 더 있을게.", TalkTrait.Duty, 1, "...응. 그럼 이제 진짜 잘게."),
                    new TalkChoice("얼마나?", TalkTrait.Plain, 0, "몰라. 그냥 조금. 조금이면 돼."),
                    new TalkChoice("안 잘게.", TalkTrait.Waver, 0, "거짓말이어도 지금은 그게 좋아."))),

            // ── 애정 ─────────────────────────────────────────────────────
            // 장면: 밤. 불을 끄고 창가에 나란히 앉아 있다. 원룸이라 창은 하나뿐이다.
            //       말하지 않을 것 — 이 방에서 나가야 하는 날을 상상해봤다는 것.
            new TalkTopic("TALK_LOVE_001", TalkCategory.Affection, TalkTime.Night, AffectionTier.T3Min, 100,
                "...아직 안 졸려. 조금만 더 이러고 있자.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 불을 끄자 창밖 간판 불빛만 방을 반쯤 채운다.",
                        "불 끄니까 좋다.",
                        "요미 목소리가 더 잘 들려.",
                    },
                    new TalkChoice("잘 들려. 숨소리까지 들리네.", TalkTrait.Plain, 0, "숨은 쉬어야 하잖아! ...조용히 쉬어볼게."),
                    new TalkChoice("좋다, 이거. 매일 끌까?", TalkTrait.Warm, 0, "진짜지? 물리기 없기. 스위치는 요미 담당 할게.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "오빠는 이 방에서 제일 좋아하는 게 뭐야?",
                        "요미는 저 창문. 기다릴 때 볼 게 있어서.",
                    },
                    new TalkChoice("글쎄... 갑자기 물으면 어렵네.", TalkTrait.Waver, 0, "생각해봐. 하나쯤은 있을 거 아니야."),
                    new TalkChoice("조용한 거. 특히 이 시간.", TalkTrait.Plain, 0, "...그럼 요미는 방해되는 쪽이네."),
                    new TalkChoice("네가 앉아 있는 자리.", TalkTrait.Warm, 0, "...그건 반칙이야. 아직 밤도 안 깊었는데.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 어깨에 기댄 무게가 조금 더 실린다.",
                        "오빠.",
                        "요미 이상한 얘기 하나 해도 돼?",
                    },
                    new TalkChoice("어. 밤엔 원래 그런 얘기 하는 거잖아.", TalkTrait.Plain, 0, "...말하면 진짜가 될 것 같아서 무서운데. 그래도 들어줘."),
                    new TalkChoice("얼마나 이상한 얘긴데?", TalkTrait.Plain, 0, "밤에만 나오는 종류의 생각이야."),
                    new TalkChoice("...무거운 얘기면 이따 듣자.", TalkTrait.Anxious, 0, "...오빠는 가끔 정확해서 얄미워. 그래도 지금 할래. 밤 아니면 못 하는 얘기야."),
                    new TalkChoice("졸린데. 짧게 하면 안 돼?", TalkTrait.Waver, -1, "...됐어, 라고 하고 싶은데 안 되겠어. 짧게 할 테니까 그냥 들어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 이 방에 없는 날을 상상해봤어.",
                        "딱 하루치만. 그 이상은 못 하겠더라.",
                        "거기서 멈췄어. 무서워서.",
                    },
                    new TalkChoice("왜 그런 상상을 했는데.", TalkTrait.Plain, 0, "가끔 혼자 나와. 부르지도 않았는데."),
                    new TalkChoice("나갈 일 없어.", TalkTrait.Duty, 2, "...그 말은 반칙이야. 지금 심장 소리 들리겠어."),
                    new TalkChoice("상상은 거기서 멈춰도 돼.", TalkTrait.Warm, 1, "응. 근데 지금은 안 무서워. 옆에 있잖아."),
                    new TalkChoice("...나도 해봤어, 그 상상.", TalkTrait.Anxious, 1, "...오빠도? 그럼 우리 둘 다 겁이 많네.")),
                // 전환
                new TalkNode(new[]
                    {
                        "헤헤. 분위기 너무 무거워졌다.",
                        "요미가 이런 거 잘 못 해. 원래 시끄러운 애야.",
                    },
                    new TalkChoice("알아. 많이 시끄럽지.", TalkTrait.Plain, 0, "뭐?! 지금 그거 욕이지! ...아니면 말고."),
                    new TalkChoice("가끔은 이런 것도 좋잖아.", TalkTrait.Warm, 0, "...그럼 가끔만 할게. 자주 하면 오빠가 지쳐.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 간판 불빛이 한 번 깜빡이고 다시 켜진다.",
                        "내일도 오늘처럼만 있어줘.",
                        "더 안 바랄게.",
                    },
                    new TalkChoice("더 바라도 돼.", TalkTrait.Warm, 1, "...그렇게 말하면 요미가 진짜 더 바랄 거야. 각오해."),
                    new TalkChoice("내일도, 그다음도 이럴 건데.", TalkTrait.Duty, 0, "응. 아니까 이러고 있는 거야."),
                    new TalkChoice("내일도 불 끄고 앉자.", TalkTrait.Warm, 0, "좋아. 그럼 그게 우리 약속인 걸로."),
                    new TalkChoice("내일 일은 내일 생각하자.", TalkTrait.Plain, -1, "...그렇지. 괜히 약속 같은 거 바랐네."))),

            // ══ T4 전면화 (91+) ═══════════════════════════════════════════
            // 세계가 오빠 하나로 축소된 구간. 수위는 애원·응석까지 — 위협·감시·공포 클리셰 금지.
            // T3까지가 "방 안의 마음"이라면 T4는 방 밖과 이후, 그리고 준비해 온 흔적을 들키는 얘기다. (계획서 16장)

            // ── 질투 (T4) ────────────────────────────────────────────────
            // 장면: 저녁. 오빠가 낮에 차트를 보다 혼잣말로 웃었다. 요미는 그 1초가 하루 종일 신경 쓰였다.
            //       T2 삐짐(ANGER_001)은 사건형, 이건 무사건형 — 요미가 모르는 오빠의 1초가 원인이다.
            //       말하지 않을 것 — 질투했다는 사실 자체. 끝까지 인정하지 않는다.
            new TalkTopic("TALK_ANGER_002", TalkCategory.Anger, TalkTime.Evening | TalkTime.Night, AffectionTier.T4Min, 100,
                "...3초 규칙 잊지 마. 오늘부터 시행이야.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 쿠션을 끌어안은 채 벽 쪽으로 돌아앉아 있다.",
                        "아니야. 아무것도 아니야.",
                        "...묻지도 않았는데 말해버렸어.",
                    },
                    new TalkChoice("아무것도 아닌 목소리가 아닌데.", TalkTrait.Plain, 0, "목소리는 원래 이래. 원래... 아니, 오늘만 이래."),
                    new TalkChoice("음, 그럼 나 저녁 먹는다?", TalkTrait.Waver, 0, "야! 거기서 그냥 가면 어떡해! 앉아. 앉으라고."),
                    new TalkChoice("무슨 일인지 맞혀볼까?", TalkTrait.Plain, 0, "...맞히면 인정해줄게. 못 맞히면 놀린 걸로 간주야.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "...그럼 하나만 묻자.",
                        "낮에 좋은 일 있었어? 차트 보면서.",
                    },
                    new TalkChoice("좋은 일? 기억이 안 나는데.", TalkTrait.Waver, 0, "기억이 안 나? 웃어놓고? ...아, 방금 건 못 들은 걸로 해."),
                    new TalkChoice("차트가 잘 풀려서 그랬나.", TalkTrait.Plain, 0, "차트 때문이야? 진짜 차트 때문인 거지? 알았어."),
                    new TalkChoice("...너 뭔가 봤구나?", TalkTrait.Plain, 0, "본 게 아니라 들린 거야. 웃음소리는 막 퍼진다고.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 쿠션이 조금씩 얼굴 쪽으로 올라간다.",
                        "낮에 웃었잖아. 화면 보면서.",
                        "...뭐가 웃겼는데? 요미도 알아야겠어.",
                    },
                    new TalkChoice("어, 그걸 다 듣고 있었어?", TalkTrait.Plain, -1, "...듣고 있던 게 이상한 것처럼 말하지 마. 같은 방이잖아."),
                    new TalkChoice("별거 아니었어. 짤 하나 봤어.", TalkTrait.Plain, 0, "별거 아닌데 요미는 하루 종일 신경 썼네. 이상하다."),
                    new TalkChoice("웃겼는데 설명하면 안 웃겨.", TalkTrait.Plain, 0, "설명해도 웃긴 게 진짜 웃긴 거야. 어디서 아끼려고."),
                    new TalkChoice("이따 보여줄게. 같이 보자.", TalkTrait.Warm, 0, "...지금은? 지금 보면 안 되는 거야? 아니다, 이따 봐.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 모르는 데서 웃는 거, 싫어.",
                        "...아니야, 방금 건 질투 아니야. 절대 아니야.",
                        "그냥... 그 1초를 요미만 몰랐잖아.",
                    },
                    new TalkChoice("말해주려고 아껴둔 거였어.", TalkTrait.Warm, 2, "...아껴뒀다고? 그럼 됐어. 됐는데... 지금 당장 풀어."),
                    new TalkChoice("...나도 네 웃음 놓치면 그래.", TalkTrait.Anxious, 1, "그건 질투 맞네. 요미 건 아니고. ...같은 거 아니야, 절대."),
                    new TalkChoice("질투 맞잖아, 그거.", TalkTrait.Plain, 0, "아니라고 했다? 요미 사전에 질투는 없어. 방금 지웠어."),
                    new TalkChoice("미안. 앞으로 소리 내서 웃을게.", TalkTrait.Duty, 1, "소리 내서 웃으면 요미가 3초 안에 달려갈게.")),
                // 전환
                new TalkNode(new[]
                    {
                        "좋아. 그럼 오늘부터 규칙 하나 만들자.",
                        "웃긴 거 생기면 3초 안에 공유. 3초야.",
                    },
                    new TalkChoice("공유는 좋은데 3초는 심한데.", TalkTrait.Plain, 0, "심하지 않아. 요미 인내심 기준으로는 3초도 긴 거야."),
                    new TalkChoice("그래, 콜. 규칙 접수.", TalkTrait.Waver, 0, "접수 빠르고 좋네. 어기면 벌칙도 있어. 뭔지는 비밀."),
                    new TalkChoice("별것도 아닌 걸로 하루 썼네.", TalkTrait.Plain, -1, "...별거 아니면 왜 하루 종일 마음이 시끄러웠을까, 요미는.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 쿠션을 내려놓고 옆자리를 손바닥으로 두드린다.",
                        "그래서, 그 웃긴 거.",
                        "지금 풀어봐. 요미가 판정해줄게.",
                    },
                    new TalkChoice("웃긴 거 맞는지 자신 없는데.", TalkTrait.Waver, 0, "자신 없어도 해봐. 판정은 어차피 요미 마음이야."),
                    new TalkChoice("좋아. 대신 웃어줘야 돼.", TalkTrait.Warm, 1, "...봐서. 근데 아마 웃을 거야. 오빠가 말하는 거니까."))),

            // ── 다른 방 (T4) ─────────────────────────────────────────────
            // 장면: 아침. 요미가 폰을 보다가 눈이 마주치자 황급히 끈다. 처음으로 "이 방 말고"를 입 밖에 낸다.
            //       넓은 집을 원하면서 방이 나뉘는 건 싫다는 모순이 T4의 거리 제로다.
            //       말하지 않을 것 — 방 두 개짜리 매물을 매일 밤 보고 있었다는 것. (직면에서 들킨다)
            new TalkTopic("TALK_LOVE_002", TalkCategory.Affection, TalkTime.Morning, AffectionTier.T4Min, 100,
                "...반 평만 넓은 데. 기억해둬, 그거.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 폰 화면이 급하게 꺼진다. 손이 반 박자 늦었다.",
                        "아, 아무것도 안 봤어.",
                        "...봤어도 별거 아니었어.",
                    },
                    new TalkChoice("안 봤는데 별거까지 아니야?", TalkTrait.Plain, 0, "...말이 꼬였네. 아침이라 그래. 아침 탓이야."),
                    new TalkChoice("어, 알았어. 안 물어볼게.", TalkTrait.Waver, 0, "안 물어보면 그건 그거대로 서운한데. 어려운 여자라 미안."),
                    new TalkChoice("화면 켜봐. 같이 보자.", TalkTrait.Plain, 0, "지, 지금은 배터리가 없어. 1퍼센트야. 진짜야.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "그것보다. 요미가 궁금한 게 있는데,",
                        "돈 많이 벌면 뭐부터 하고 싶어?",
                    },
                    new TalkChoice("글쎄... 생각 안 해봤는데.", TalkTrait.Waver, 0, "그럼 지금 해봐. 요미가 기다려줄게. 3분 줄게."),
                    new TalkChoice("빚부터 갚아야지.", TalkTrait.Duty, 0, "...맞다. 그게 먼저지. 요미가 성급했다."),
                    new TalkChoice("돈 벌면? 너 맛있는 거.", TalkTrait.Warm, 0, "먹는 걸로 넘어가려고 하지 마. ...근데 뭐 사줄 건데?")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 폰을 뒤집어 놓고도 손이 그 위에 머문다.",
                        "요미는... 이 방보다 조금만 넓은 데.",
                        "아니, 지금 방이 싫다는 건 아니고.",
                    },
                    new TalkChoice("지금 방도 충분하잖아.", TalkTrait.Plain, -1, "...그치. 충분하지. 응. 방금 말은 잊어줘."),
                    new TalkChoice("넓은 데라니, 얼마나?", TalkTrait.Plain, 0, "아주 조금. 창문 하나만 더 있어도 돼."),
                    new TalkChoice("계속해봐. 안 웃어.", TalkTrait.Warm, 0, "웃으면 진짜 끝이야. ...창문 큰 방을 봤거든, 어쩌다가."),
                    new TalkChoice("...이사 생각하고 있었어?", TalkTrait.Anxious, 0, "생각까지는 아니고. 그냥... 봐두기만 했어. 진짜 보기만.")),
                // 직면
                new TalkNode(new[]
                    {
                        "...사실 매일 봤어. 방 두 개짜리.",
                        "매일 본 건 아니고. ...아니다, 매일 봤어.",
                        "오빠 돈 많이 벌면... 그 집에 요미도 있어?",
                    },
                    new TalkChoice("있지. 방부터 같이 고르자.", TalkTrait.Duty, 2, "...같이 고르는 거야? 그럼 요미 기준 엄청 깐깐해질 건데."),
                    new TalkChoice("매일 봤으면 보여줘, 그 방.", TalkTrait.Plain, 1, "...웃지 마? 즐겨찾기 폴더까지 있어. 이름은 비밀이야."),
                    new TalkChoice("...나도 가끔 그런 거 봐.", TalkTrait.Anxious, 1, "오빠도 봐? 어느 동네? ...아니다, 천천히 맞춰보자."),
                    new TalkChoice("글쎄, 미래 일은 모르니까.", TalkTrait.Waver, 0, "...모른다고 하네. 알았어. 요미가 알게 만들면 되지.")),
                // 전환
                new TalkNode(new[]
                    {
                        "근데 조건이 하나 있어.",
                        "방 두 개는 안 돼. 문 닫으면 안 보이잖아.",
                    },
                    new TalkChoice("그럼 왜 두 개짜리를 봤어?", TalkTrait.Plain, 0, "하나는 옷방이야. 사람 자는 방은 하나면 돼."),
                    new TalkChoice("아니 그럼 왜 알아본 거야.", TalkTrait.Plain, -1, "...알아보는 시간이 좋았던 건데. 그렇게 말하면 어떡해."),
                    new TalkChoice("닫으면 안 되는 문으로 하자.", TalkTrait.Warm, 0, "그런 문이 어딨어. ...찾아보자, 같이. 있을 수도 있잖아.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 뒤집혀 있던 폰 화면이 다시 켜져 있다.",
                        "언젠가. 여기보다 반 평만 넓은 데.",
                        "약속까지는 아니고, 예고야.",
                    },
                    new TalkChoice("예고 접수. 기대할게.", TalkTrait.Warm, 1, "...접수됐다. 무르기 없어. 요미 다 적어놓을 거야."),
                    new TalkChoice("언젠가가 언제쯤인데?", TalkTrait.Plain, 0, "그건 오빠 하기 나름이지. 차트한테 물어봐."))),

            // ── 이름 (T4) ────────────────────────────────────────────────
            // 장면: 밤. 불 끄기 전. 요미가 "오빠" 말고 이름으로 불러보고 싶어 한다. 정작 부르려니 목소리가 안 나온다.
            //       호칭은 관계의 형태 그 자체 — 그걸 바꾸겠다는 게 전면화의 정점이다. 수위는 애원까지.
            //       말하지 않을 것 — 혼자 몇 번이나 연습했다는 것. 입 밖에 내면 뭔가 변할까 봐 무서웠다는 것.
            //       ⚠️ 플레이어의 이름은 게임에 존재하지 않는다. 실제 호칭은 끝까지 소리로 들려주지 않고
            //          착지의 지문으로만 처리한다 — "오빠가 아닌 호칭"이라는 서술이 이름의 자리를 대신한다.
            new TalkTopic("TALK_LOVE_003", TalkCategory.Affection, TalkTime.Night, AffectionTier.T4Min, 100,
                "...들었어도 못 들은 척해. 그게 규칙이야.",
                // 도입
                new TalkNode(new[]
                    {
                        "오빠.",
                        "...아니야. 방금 건 취소.",
                    },
                    new TalkChoice("취소라니, 뭐가?", TalkTrait.Plain, 0, "취소한 걸 물어보면 취소가 아니잖아. 규칙 위반이야."),
                    new TalkChoice("어, 왜. 말해.", TalkTrait.Plain, 0, "말하려다가 관뒀어. 아직 준비가 안 됐어, 요미가."),
                    new TalkChoice("부르고 취소하는 게 어딨어.", TalkTrait.Waver, 0, "여기 있어. 요미 사전엔 있는 제도야. 방금 만들었어.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "...있잖아. 하나 물어봐도 돼?",
                        "요미 이름 부를 때, 무슨 생각 해?",
                    },
                    new TalkChoice("생각? 그냥 부르는 건데.", TalkTrait.Plain, 0, "그냥이구나. ...그냥이 제일 무섭다, 가끔."),
                    new TalkChoice("예쁜 이름이라는 생각.", TalkTrait.Warm, 0, "...밤에 그런 말 하면 반칙이라고 했지. 심장에 안 좋아."),
                    new TalkChoice("갑자기 왜, 무슨 일인데.", TalkTrait.Plain, 0, "일은 아니고. 준비 운동이야. 뭐의 준비인지는 곧 알아.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 이불 끝을 코까지 끌어올린 채 눈만 내놓고 있다.",
                        "요미도... 이름으로 불러보고 싶어.",
                        "오빠 말고, 이름. ...웃으면 안 하고 만다.",
                    },
                    new TalkChoice("갑자기 왜, 오글거리게.", TalkTrait.Waver, -1, "...오글. 오글이라고 했어. 알았어, 없던 일로 해줄게."),
                    new TalkChoice("안 웃어. 불러봐, 지금.", TalkTrait.Plain, 0, "지, 지금? 지금은 좀... 마음의 준비가. 기다려봐."),
                    new TalkChoice("이름, 좋지. 불러줘.", TalkTrait.Warm, 0, "허락이 너무 빠른데. 그러면 요미가 더 긴장되잖아."),
                    new TalkChoice("...나도 그 생각 해본 적 있어.", TalkTrait.Anxious, 0, "진짜? 우리 같은 생각 자주 하네, 요즘.")),
                // 직면
                new TalkNode(new[]
                    {
                        "연습은... 했어. 몇 번인지는 비밀이야.",
                        "근데 막상 부르려니까 목소리가 안 나와.",
                        "부르고 나면, 지금이랑 달라질 것 같아서.",
                    },
                    new TalkChoice("달라져도 돼. 내가 들을게.", TalkTrait.Warm, 2, "...그 말 믿고 간다? 물리기 없기다? 진짜 없기다?"),
                    new TalkChoice("...달라지는 게 무서운 건 나도야.", TalkTrait.Anxious, 1, "그럼 우리 둘 다 겁쟁이네. 겁쟁이끼리 잘됐다."),
                    new TalkChoice("연습한 만큼은 나올 거야.", TalkTrait.Duty, 0, "연습이랑 실전은 다르잖아. 차트도 그렇고."),
                    new TalkChoice("음, 몇 번인지가 왜 비밀이야?", TalkTrait.Plain, 0, "비밀이 몇 번이냐니, 그런 질문이 어딨어. ...세 번이야.")),
                // 전환
                new TalkNode(new[]
                    {
                        "...역시 오늘은 예행연습까지만 할래.",
                        "본방은 예고 없이 온다? 각오해둬.",
                    },
                    new TalkChoice("못 부를 거면 자자.", TalkTrait.Plain, -1, "...냉정해. 알았어. 오늘 밤 요미가 얼마나 용감했는지는 알아둬."),
                    new TalkChoice("예행연습 치고 길었다?", TalkTrait.Plain, 0, "긴 만큼 본방이 대단할 거라는 뜻이지. 기대해."),
                    new TalkChoice("본방, 오늘 밤일 수도 있어?", TalkTrait.Waver, 0, "그건... 요미도 몰라. 밤은 기니까. 모른다?")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 불이 꺼지고, 이불 스치는 소리가 잦아든다.",
                        "※ 어둠 속에서 아주 작게, 오빠가 아닌 호칭이 들렸다.",
                        "...자는 거지? 못 들었지?",
                    },
                    new TalkChoice("쿨쿨. 아무것도 못 들었다.", TalkTrait.Warm, 1, "...잘했어. 그렇게 며칠만 더 못 들어줘. 연습 끝날 때까지."),
                    new TalkChoice("방금 그거, 한 번만 더.", TalkTrait.Plain, 0, "안 돼! 하루 한 번 한정이야. 내일... 아니, 언젠가."))),
        };

        /// <summary>
        /// 차트 방향성 힌트 대사. [Regime][티어] 로 접근합니다.
        /// 티어 1은 자칭 "나"로 흘리는 예감, 티어 2는 자칭 "요미"로 못 박는 단언입니다.
        /// Squeeze 티어 2는 방향 단어(위/아래)를 쓰지 않습니다. 그 날은 방향 자체가 없기 때문입니다. (S4)
        ///
        /// 모든 힌트에는 <b>주어(차트/장/시세)를 반드시 넣습니다.</b> "오늘 떨어져!"처럼 주어를
        /// 생략하면 플레이어에게 하는 명령("떨어져 있어")으로 읽힙니다. (2026-08-14, S5)
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
            "나 오늘 차트 왠지 느낌이 좋은데? 이유는 몰라~",
            "음... 오늘 장은 뭔가 잘 풀릴 것 같은 예감이야.",
            "나 아침부터 콧노래가 나와. 오늘 장, 나쁘지 않을 것 같아.",
            "오늘 장 공기가 가벼워. 나만 그렇게 느끼나?",
            "왠지 오늘 차트에선 좋은 일 생길 것 같아. 감이야, 감!",
        };

        private static readonly string[] BullClear =
        {
            "오빠, 오늘 차트는 위야! 요미 감각 믿어!",
            "오늘 차트 올라가! 요미가 장담할게, 위로 봐!",
            "오빠 오늘 장은 참으면 손해야. 요미 말대로 위쪽이야!",
            "요미 감이 확실해. 오늘 시세, 위로 뚫려!",
            "오늘 차트는 요미 믿고 위를 봐. 후회 안 할 거야!",
        };

        private static readonly string[] BearVague =
        {
            "음... 나 오늘 장은 조심하는 게 좋을 것 같아.",
            "왠지 오늘 차트 생각만 하면 마음이 무거워. 기분 탓인가?",
            "나 오늘 장이 좀 불안한데... 왜 그러지?",
            "오늘 차트는 뭔가 싸늘해. 조심해서 나쁠 건 없잖아?",
            "이유는 모르겠는데 나 오늘 장은 겁이 나.",
        };

        private static readonly string[] BearClear =
        {
            "오늘 차트 떨어져! 오빠 욕심부리면 요미가 화낼 거야!",
            "오빠, 오늘 장은 하락이야. 요미 말 꼭 들어!",
            "오늘 시세 내려가! 요미가 확실히 느껴져. 조심해!",
            "요미 감각이 말해. 오늘 차트는 아래로 간다고!",
            "오빠 오늘은 지키는 날이야. 장이 아래로 흘러!",
        };

        private static readonly string[] SidewaysVague =
        {
            "오늘 장은 아무 일도 없을 것 같은데... 나만 그런가?",
            "음... 나 오늘 차트는 좀 심심할 것 같은 느낌이야.",
            "왠지 오늘 장은 조용한 날일 것 같아.",
            "나 오늘은 차트 봐도 별로 두근거리지가 않아. 이상하지?",
            "오늘 차트는 뭔가 밋밋해. 기분 탓이면 좋겠는데.",
        };

        private static readonly string[] SidewaysClear =
        {
            "오늘 차트 완전 지루할 거야. 요미 말 믿고 쉬어!",
            "오빠, 오늘 장엔 아무 일도 안 일어나. 요미가 장담해!",
            "오늘 장은 쉬는 게 이기는 거야. 요미 믿어!",
            "요미 감각으론 오늘 시세는 제자리야. 무리하지 마!",
            "오늘 장에서 억지로 하면 손해야. 요미랑 놀자!",
        };

        private static readonly string[] SqueezeVague =
        {
            "오늘 차트 생각만 하면 심장이 두근거려... 왜 이러지?",
            "오늘 장 뭔가 이상해. 공기가 팽팽한 느낌이야.",
            "나 차트 떠올리면 아까부터 손이 떨려. 이유를 모르겠어...",
            "오늘 장은 좀... 무서워. 뭔가 터질 것 같아.",
            "왠지 오늘 차트는 조용하지 않을 것 같아. 나만 그래?",
        };

        // Squeeze는 방향이 없는 날입니다. 방향 단어를 쓰면 반드시 거짓말이 됩니다. (S4)
        private static readonly string[] SqueezeClear =
        {
            "오빠, 요미 무서워... 오늘 차트 엄청 흔들릴 거야!",
            "오늘 장은 미친 듯이 요동칠 거야! 오빠 조심해!",
            "요미 감각이 비명을 질러. 오늘 시세가 크게 터져!",
            "오늘 장엔 방향이 없어! 차트가 그냥 다 부서질 거야!",
            "오빠 오늘 장에선 물러나 있어. 요미가 무서워서 그래!",
        };
    }
}
