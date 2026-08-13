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
    /// +0은 냉담이 아니라 회피·얼버무림입니다. 요미의 불안을 비웃는 선택지는 만들지 않습니다.
    /// </summary>
    public readonly struct TalkChoice
    {
        public readonly string Text;
        public readonly TalkTrait Trait;
        public readonly int Affection; // +0 ~ +3. 마이너스는 주지 않습니다. (S9)

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
    /// 호감도는 노드가 아니라 <b>토픽 단위로 배분</b>합니다. 총합 12 상한, +3은 후반 절반에만. (10.4)
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
                    new TalkChoice("몇 시야.", TalkTrait.Plain, 0, "몰라. 근데 밝아진 지는 좀 됐어."),
                    new TalkChoice("일어났어.", TalkTrait.Plain, 1, "응! ...아, 응. 안 놀랐어. 요미는 놀란 적 없어."),
                    new TalkChoice("...더 자도 돼?", TalkTrait.Waver, 0, "안 돼. 요미가 아까부터 기다렸단 말이야.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "근데 오빠, 어제 늦게까지 뭐 봤어?",
                        "요미 자다 깼는데 화면 켜져 있었어.",
                    },
                    new TalkChoice("별거 아니야.", TalkTrait.Plain, 0, "별거 아닌 걸 새벽까지 보는 사람이 어딨어."),
                    new TalkChoice("...잘 안 풀렸어.", TalkTrait.Anxious, 1, "그랬구나. 그럼 오늘은 요미가 옆에 붙어 있을게."),
                    new TalkChoice("습관이야.", TalkTrait.Waver, 0, "고칠 생각은 없고? ...아니다, 됐어.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 이불 끝을 손가락으로 접었다 폈다 한다.",
                        "근데 오빠.",
                        "요미 오늘 뭐 해야 돼?",
                    },
                    new TalkChoice("아무것도 안 해도 돼.", TalkTrait.Warm, 2, "...그 말이 제일 어려워. 그럼 요미는 뭐가 되는 건데."),
                    new TalkChoice("그냥 있어.", TalkTrait.Waver, 1, "그냥 있는 건 잘하는데. 그것만 하면 좀 미안해서."),
                    new TalkChoice("밥이나 먹자.", TalkTrait.Plain, 0, "그건 요미가 할게. 그건 요미도 할 수 있어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 여기 있는 거, 오빠는 괜찮아?",
                        "가끔 그게 궁금해.",
                        "...아침엔 이런 생각이 자주 나.",
                    },
                    new TalkChoice("안 괜찮았으면 말했어.", TalkTrait.Plain, 2, "그건 그렇네. 오빠는 참는 걸 잘 못하니까."),
                    new TalkChoice("내가 정한 거야.", TalkTrait.Duty, 2, "...오빠가 정했으면 됐어. 그럼 요미는 안 물어볼게."),
                    new TalkChoice("네가 없으면 너무 조용해.", TalkTrait.Warm, 3, "...아침부터 그런 말 하면 요미 하루 종일 이상해져."),
                    new TalkChoice("나도 요즘 그런 생각 해.", TalkTrait.Anxious, 1, "오빠도? 그럼 우리 둘 다 이상한 아침이네.")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어! 요미 기분 좋아졌어.",
                        "오늘은 뭔가 잘될 것 같은 아침이야.",
                    },
                    new TalkChoice("근거는?", TalkTrait.Plain, 1, "요미의 감이야. 감은 원래 근거가 없어서 감인 거야."),
                    new TalkChoice("그럼 오늘은 믿어볼게.", TalkTrait.Waver, 2, "좋아! 요미 감 믿고 손해 본 사람 아직 없어. ...아마.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 커튼을 걷자 방이 한꺼번에 밝아진다.",
                        "오빠, 오늘도 차트 오래 볼 거야?",
                        "미리 알면 요미가 하루를 어떻게 쓸지 정할 수 있어.",
                    },
                    new TalkChoice("정해지면 말할게.", TalkTrait.Duty, 2, "응. 그거면 돼. 기다리는 건 요미가 잘해."),
                    new TalkChoice("오늘은 일찍 접을게.", TalkTrait.Warm, 3, "...그럼 요미도 오늘 하루 열심히 살아볼게."))),

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
                    new TalkChoice("바쁘다고 하면?", TalkTrait.Plain, 1, "그럼 요미가 옆에서 계속 쳐다볼 거야. 그게 더 방해될걸."),
                    new TalkChoice("안 바빠.", TalkTrait.Plain, 1, "역시! 요미는 오빠 표정만 봐도 알아."),
                    new TalkChoice("일단 하나 줘봐.", TalkTrait.Waver, 0, "받았으면 끝이야. 이제 무를 수 없어.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "아 맞다. 오빠는 어렸을 때 뭐 하고 놀았어?",
                        "요미는 게임 말고는 아는 게 없어서.",
                    },
                    new TalkChoice("기억 안 나.", TalkTrait.Plain, 0, "에이. 하나쯤은 있을 거 아니야."),
                    new TalkChoice("나도 혼자 하는 거였어.", TalkTrait.Anxious, 1, "...우리 둘 다 그랬네. 그럼 지금부터 같이 하면 되겠다."),
                    new TalkChoice("왜 갑자기.", TalkTrait.Waver, 0, "그냥. 오빠 얘기 듣고 싶어서.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 화면이 켜지고 익숙한 손놀림으로 메뉴가 넘어간다.",
                        "진 사람이 소원 하나 들어주기.",
                        "요미 진심이야.",
                    },
                    new TalkChoice("소원부터 말해.", TalkTrait.Plain, 1, "안 돼. 먼저 말하면 오빠가 일부러 져줄 거잖아."),
                    new TalkChoice("안 봐줄 거야.", TalkTrait.Plain, 2, "그래야지! 봐주면 요미가 더 화나."),
                    new TalkChoice("지면 뭐든 들어줄게.", TalkTrait.Warm, 0, "그렇게 쉽게 말하면 재미없잖아!"),
                    new TalkChoice("좋아. 하자.", TalkTrait.Waver, 1, "헤헤. 후회해도 늦었어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "...어라?",
                        "이상하다. 어제는 이거 됐는데.",
                    },
                    new TalkChoice("어제 뭐 했는데.", TalkTrait.Plain, 1, "아, 그게... 연습 좀 했어. 오빠 이기려고."),
                    new TalkChoice("다시 하자.", TalkTrait.Plain, 2, "당연하지! 방금 건 손이 미끄러진 거야."),
                    new TalkChoice("나 이기려고 그런 거야?", TalkTrait.Warm, 3, "...웃지 마! 그렇게 정확하게 말하지도 말고!"),
                    new TalkChoice("내가 너무 세게 했나.", TalkTrait.Duty, 1, "그런 거 아니야! 요미가 약한 게 아니라고!")),
                // 전환
                new TalkNode(new[]
                    {
                        "요미가 이길 때까지 할 거야.",
                        "오빠 오늘은 요미 거야.",
                    },
                    new TalkChoice("오늘 통째로?", TalkTrait.Plain, 1, "일단 지금부터. 나머지는 이따 다시 얘기하자."),
                    new TalkChoice("하던 거 접고 올게.", TalkTrait.Duty, 2, "진짜? 그럼 요미가 세팅해둘게!"),
                    new TalkChoice("적당히 하고 밥 먹자.", TalkTrait.Waver, 0, "그 말 나올 줄 알았어. 한 판만 더. 딱 한 판만.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 컨트롤러를 쥔 손이 느려지고 어깨가 기운다.",
                        "...아직 안 졸려.",
                        "한 판만 더 하면 이길 것 같은데.",
                    },
                    new TalkChoice("그 한 판은 이따 하자.", TalkTrait.Warm, 3, "...이따 진짜 하는 거다. 요미 기억해둘 거야."),
                    new TalkChoice("이미 지고 있잖아.", TalkTrait.Plain, 1, "지고 있는 게 아니라 마지막에 뒤집는 타입인 거야."))),

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
                    new TalkChoice("조금만.", TalkTrait.Waver, 0, "그 조금이 아까부터 세 번째야. ...아, 안 셌어. 느낌이야."),
                    new TalkChoice("지금 갈게.", TalkTrait.Duty, 1, "진짜? 그럼 요미 젓가락 놓을게.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "오빠는 밥 먹을 때 무슨 생각 해?",
                        "요미는 오빠가 언제 젓가락 놓나 그것만 봐.",
                    },
                    new TalkChoice("아무 생각 안 해.", TalkTrait.Plain, 0, "그게 제일 부러워. 요미는 머리가 안 꺼져."),
                    new TalkChoice("차트 생각.", TalkTrait.Anxious, 1, "...먹을 때만이라도 좀 꺼줘. 부탁이야."),
                    new TalkChoice("네가 보는 줄 몰랐어.", TalkTrait.Plain, 1, "매번 봤어. 오빠만 몰랐던 거야.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 냄비 속 면이 국물을 다 빨아들여 덩어리가 됐다.",
                        "...좀 불었어.",
                        "괜찮아. 요미는 불은 것도 좋아해.",
                    },
                    new TalkChoice("안 좋아하잖아.", TalkTrait.Plain, 2, "안 좋아해. 근데 오빠랑 먹으면 좀 나아."),
                    new TalkChoice("다시 끓일까.", TalkTrait.Warm, 1, "됐어. 지금 끓이면 또 기다려야 되잖아."),
                    new TalkChoice("미안.", TalkTrait.Duty, 1, "사과할 거면 먹으면서 해. 식으면 더 미안해질 거야.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미는 오빠 밥 차리는 게 좋아.",
                        "여기서 요미가 확실하게 할 수 있는 게 그거라서.",
                        "...이런 말 하려던 건 아닌데.",
                    },
                    new TalkChoice("그것만 하는 거 아니잖아.", TalkTrait.Warm, 3, "...그럼 뭘 더 하는데. 말해봐. 다 들을 거야."),
                    new TalkChoice("그런 거 아니야.", TalkTrait.Waver, 0, "...그럼 뭔데. 말 안 하면 요미는 계속 이렇게 생각할 거야."),
                    new TalkChoice("매일 잘 먹고 있어.", TalkTrait.Plain, 2, "그거면 됐어. 그거 들으려고 기다린 거야."),
                    new TalkChoice("나도 뭘 해야 될지 몰라.", TalkTrait.Anxious, 1, "...오빠도 몰라? 그럼 그냥 이러고 있으면 되는 건가.")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어! 분위기 이상해졌어.",
                        "먹자. 더 불면 이건 라면도 아니야.",
                    },
                    new TalkChoice("이미 아닌 것 같은데.", TalkTrait.Plain, 1, "말하지 마. 먹기 전까지는 라면이야."),
                    new TalkChoice("잘 먹을게.", TalkTrait.Warm, 2, "응. 많이 먹어. 요미 건 좀 남겨두고.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 젓가락 부딪히는 소리만 한동안 이어진다.",
                        "오빠. 다음엔 화면 끄고 와줘.",
                        "요미는 그거면 돼.",
                    },
                    new TalkChoice("알았어. 끄고 올게.", TalkTrait.Duty, 3, "...그 말 믿을게. 요미는 잘 믿는 편이야."),
                    new TalkChoice("노력할게.", TalkTrait.Waver, 1, "노력이라는 말은 좀 미끄러워."))),

            // ── 삐짐 ─────────────────────────────────────────────────────
            // 장면: 저녁/밤. 낮에 세 번 불렀는데 오빠는 화면만 봤다. 방이 좁아 피할 데가 없어 이불을 뒤집어썼다.
            //       말하지 않을 것 — 그때 하려던 말이 따로 있었다는 것.
            new TalkTopic("TALK_ANGER_001", TalkCategory.Anger, TalkTime.Evening | TalkTime.Night, 31, 100,
                "...다 풀린 건 아니야. 조금 남겨뒀어.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 침대 쪽에서 이불 뭉치가 등을 돌리고 있다.",
                        "......",
                        "안 들려. 요미 자는 중이야.",
                    },
                    new TalkChoice("자는 사람은 그 말 안 해.", TalkTrait.Plain, 1, "자면서 말하는 사람도 있어. 요미가 그런 타입이야."),
                    new TalkChoice("화났구나.", TalkTrait.Plain, 0, "안 났어. 화날 일도 아니야."),
                    new TalkChoice("...내가 뭐 잘못했지.", TalkTrait.Duty, 1, "알면서 왜 물어. 알면 그냥 말해.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "...오빠 오늘 손해 봤지.",
                        "표정 안 봐도 알아. 문 닫는 소리부터 달랐어.",
                    },
                    new TalkChoice("티 났어?", TalkTrait.Anxious, 1, "많이. 요미는 그런 것만 잘 알아."),
                    new TalkChoice("아니야.", TalkTrait.Waver, 0, "그럼 됐고. ...아니, 안 됐어. 왜 숨겨."),
                    new TalkChoice("그 얘긴 나중에.", TalkTrait.Plain, 0, "알았어. 나중에 진짜 할 거지?")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 이불이 조금 내려가고 눈만 나온다.",
                        "낮에 세 번 불렀어.",
                        "오빠는 화면만 봤고.",
                    },
                    new TalkChoice("못 들었어.", TalkTrait.Plain, 0, "그래. 못 들었겠지. 화면이 요미보다 시끄러웠나 봐."),
                    new TalkChoice("미안. 진짜 못 들었어.", TalkTrait.Duty, 2, "알아. 아는데 그게 더 서운해."),
                    new TalkChoice("뭐라고 불렀는데.", TalkTrait.Plain, 2, "그냥 오빠, 라고. 세 번 다 그거였어."),
                    new TalkChoice("그때 정신이 없었어.", TalkTrait.Anxious, 1, "정신없는 건 알아. 근데 요미는 그때 여기 있었어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 화난 건 대답 안 한 게 아니야.",
                        "언제 눈 뗄까 하고 계속 봤어.",
                        "그러다 하려던 말도 까먹었어.",
                    },
                    new TalkChoice("무슨 말이었는데.", TalkTrait.Plain, 2, "까먹었다니까. ...아마 별거 아니었을 거야."),
                    new TalkChoice("이제 봤어. 말해줘.", TalkTrait.Warm, 3, "...치사해. 그렇게 말하면 화도 못 내잖아."),
                    new TalkChoice("기다리게 해서 미안해.", TalkTrait.Duty, 2, "사과는 한 번이면 됐어. 두 번 하면 가벼워져.")),
                // 전환
                new TalkNode(new[]
                    {
                        "됐어. 이제 다 풀렸어.",
                        "근데 아직 이불에서 나갈 마음은 없어.",
                    },
                    new TalkChoice("나올 때까지 있을게.", TalkTrait.Warm, 2, "...반칙이야. 그러면 금방 나가고 싶어지잖아."),
                    new TalkChoice("언제 나올 건데.", TalkTrait.Plain, 1, "몰라. 오빠가 궁금해할 때까지."),
                    new TalkChoice("그럼 나도 누울까.", TalkTrait.Waver, 0, "이 좁은 데 둘이 어떻게 누워. ...못 눕는 건 아니지만.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 이불 아래로 손이 나와 옷자락을 붙잡는다.",
                        "다음엔 대답만 해줘.",
                        "응, 이라고만 해도 돼. 그럼 요미는 기다릴 수 있어.",
                    },
                    new TalkChoice("응.", TalkTrait.Warm, 3, "...지금 한 거야? 그렇게 바로 하면 어떡해."),
                    new TalkChoice("그럴게.", TalkTrait.Waver, 1, "그 말은 좀 무겁다. 응, 이 더 좋아."))),

            // ── 수면 ─────────────────────────────────────────────────────
            // 장면: 밤. 불을 끄고 누웠는데 요미가 잠들지 못한다. 방이 하나라 서로의 기척이 다 들린다.
            //       말하지 않을 것 — 악몽에 깼는데 깨워도 되는지 몰라 한참 누워 있었다는 것.
            new TalkTopic("TALK_SLEEP_001", TalkCategory.Sleeping, TalkTime.Night, 31, 100,
                "...아직 깨어 있지?",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 불이 꺼진 방. 이불 스치는 소리가 반복된다.",
                        "...오빠. 자?",
                        "자면 대답 안 해도 돼.",
                    },
                    new TalkChoice("아직 안 자.", TalkTrait.Plain, 1, "다행이다. ...아니, 다행이라고 한 거 아니야."),
                    new TalkChoice("...나도 안 와.", TalkTrait.Anxious, 0, "그럼 둘 다 못 자는 거네. 좀 안심된다.")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "오빠는 밤에 무슨 생각 해?",
                        "요미는 낮에 못 한 걱정을 밤에 몰아서 해.",
                    },
                    new TalkChoice("별로 안 해.", TalkTrait.Plain, 0, "거짓말. 아까 한숨 쉬었잖아."),
                    new TalkChoice("...돈 생각.", TalkTrait.Anxious, 1, "그건 요미도 좀 알 것 같아. 미안해."),
                    new TalkChoice("너랑 비슷한 거.", TalkTrait.Warm, 1, "그럼 우리 둘이 밤마다 같은 걸 하고 있었네.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 뒤척이는 소리가 멈추고 한동안 조용해진다.",
                        "아까 꿈 꿨어.",
                        "오빠가 멀어지는데 요미는 발이 안 움직였어.",
                    },
                    new TalkChoice("무슨 꿈인지 더 말해봐.", TalkTrait.Warm, 2, "소리도 안 났어. 부르는데 목소리가 안 나왔어."),
                    new TalkChoice("그냥 꿈이야.", TalkTrait.Plain, 1, "알아. 아는데 아는 게 도움이 안 될 때가 있잖아."),
                    new TalkChoice("계속 깨어 있었네.", TalkTrait.Plain, 2, "...응. 좀 됐어.")),
                // 직면
                new TalkNode(new[]
                    {
                        "깨울까 계속 고민했어.",
                        "얹혀사는 주제에 잠까지 깨우면 좀 그렇잖아.",
                        "...이렇게 말할 생각은 아니었는데.",
                    },
                    new TalkChoice("그런 계산 하지 마.", TalkTrait.Warm, 3, "...계산 안 하면, 요미는 여기 그냥 있어도 되는 거야?"),
                    new TalkChoice("다음엔 그냥 깨워.", TalkTrait.Duty, 2, "응... 다음엔 그럴게. 아마도."),
                    new TalkChoice("얼마나 누워 있었어.", TalkTrait.Plain, 1, "안 셌어. 세면 더 길어지니까."),
                    new TalkChoice("얹혀사는 거 아니야.", TalkTrait.Warm, 2, "그렇게 말해주는 건 고마운데, 요미도 알아.")),
                // 전환
                new TalkNode(new[]
                    {
                        "헤헤. 말하고 나니까 좀 낫다.",
                        "역시 요미는 입 다물고 있으면 안 되는 타입이야.",
                    },
                    new TalkChoice("이제 알았어?", TalkTrait.Plain, 1, "알고는 있었어. 인정하기 싫었을 뿐이야."),
                    new TalkChoice("앞으로도 말해.", TalkTrait.Warm, 2, "응. 대신 오빠가 잠을 못 자게 될걸.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 목소리가 작아지고 문장 사이가 길어진다.",
                        "오빠.",
                        "요미 잠들 때까지만 깨어 있어줘.",
                    },
                    new TalkChoice("그럴게.", TalkTrait.Duty, 3, "...응. 그럼 이제 진짜 잘게."),
                    new TalkChoice("얼마나?", TalkTrait.Plain, 1, "몰라. 그냥 조금. 조금이면 돼."),
                    new TalkChoice("안 잘게.", TalkTrait.Waver, 2, "거짓말이어도 지금은 그게 좋아."))),

            // ── 애정 ─────────────────────────────────────────────────────
            // 장면: 밤. 불을 끄고 창가에 나란히 앉아 있다. 원룸이라 창은 하나뿐이다.
            //       말하지 않을 것 — 이 방에서 나가야 하는 날을 상상해봤다는 것.
            new TalkTopic("TALK_LOVE_001", TalkCategory.Affection, TalkTime.Night, 61, 100,
                "...아직 안 졸려. 조금만 더 이러고 있자.",
                // 도입
                new TalkNode(new[]
                    {
                        "※ 불을 끄자 창밖 간판 불빛만 방을 반쯤 채운다.",
                        "불 끄니까 좋다.",
                        "요미 목소리가 더 잘 들려.",
                    },
                    new TalkChoice("확실히 잘 들려.", TalkTrait.Plain, 1, "그치? 그래서 요미는 밤이 좋아."),
                    new TalkChoice("나도 이 시간이 좋아.", TalkTrait.Warm, 1, "...그럼 매일 이러자. 매일은 무리인가?")),
                // 곁길 — 요미가 묻는다
                new TalkNode(new[]
                    {
                        "오빠는 이 방에서 제일 좋아하는 게 뭐야?",
                        "요미는 저 창문. 기다릴 때 볼 게 있어서.",
                    },
                    new TalkChoice("글쎄.", TalkTrait.Waver, 0, "생각해봐. 하나쯤은 있을 거 아니야."),
                    new TalkChoice("조용한 거.", TalkTrait.Plain, 1, "...그럼 요미는 방해되는 쪽이네."),
                    new TalkChoice("네가 앉아 있는 자리.", TalkTrait.Warm, 1, "...그건 반칙이야. 아직 밤도 안 깊었는데.")),
                // 균열
                new TalkNode(new[]
                    {
                        "※ 어깨에 기댄 무게가 조금 더 실린다.",
                        "오빠.",
                        "요미 이상한 얘기 하나 해도 돼?",
                    },
                    new TalkChoice("해봐.", TalkTrait.Plain, 2, "...아니다. 말하면 진짜가 될 것 같아."),
                    new TalkChoice("이상한 얘기?", TalkTrait.Plain, 1, "밤에만 나오는 종류의 생각이야."),
                    new TalkChoice("지금은 하지 마.", TalkTrait.Anxious, 2, "...오빠는 가끔 정확해서 얄미워.")),
                // 직면
                new TalkNode(new[]
                    {
                        "요미가 이 방에 없는 날을 상상해봤어.",
                        "딱 하루치만. 그 이상은 못 하겠더라.",
                        "거기서 멈췄어. 무서워서.",
                    },
                    new TalkChoice("왜 그런 상상을 해.", TalkTrait.Plain, 1, "가끔 혼자 나와. 부르지도 않았는데."),
                    new TalkChoice("나갈 일 없어.", TalkTrait.Duty, 3, "...그 말은 반칙이야. 지금 심장 소리 들리겠어."),
                    new TalkChoice("무서웠겠다.", TalkTrait.Warm, 2, "응. 근데 지금은 안 무서워. 옆에 있잖아."),
                    new TalkChoice("나도 가끔 그런 생각 해.", TalkTrait.Anxious, 2, "...오빠도? 그럼 우리 둘 다 겁이 많네.")),
                // 전환
                new TalkNode(new[]
                    {
                        "헤헤. 분위기 너무 무거워졌다.",
                        "요미가 이런 거 잘 못 해. 원래 시끄러운 애야.",
                    },
                    new TalkChoice("알고 있어.", TalkTrait.Plain, 2, "알면서 왜 놔뒀어. 요미 혼자 진지해졌잖아."),
                    new TalkChoice("가끔은 이래도 돼.", TalkTrait.Warm, 1, "...그럼 가끔만 할게. 자주 하면 오빠가 지쳐.")),
                // 착지
                new TalkNode(new[]
                    {
                        "※ 간판 불빛이 한 번 깜빡이고 다시 켜진다.",
                        "내일도 오늘처럼만 있어줘.",
                        "더 안 바랄게.",
                    },
                    new TalkChoice("더 바라도 돼.", TalkTrait.Warm, 3, "...그렇게 말하면 요미가 진짜 더 바랄 거야. 각오해."),
                    new TalkChoice("그러고 있어.", TalkTrait.Duty, 2, "응. 아니까 이러고 있는 거야."),
                    new TalkChoice("내일도 불 끄고 앉자.", TalkTrait.Warm, 1, "좋아. 그럼 그게 우리 약속인 걸로."))),
        };

        /// <summary>
        /// 차트 방향성 힌트 대사. [Regime][티어] 로 접근합니다.
        /// 티어 1은 자칭 "나"로 흘리는 예감, 티어 2는 자칭 "요미"로 못 박는 단언입니다.
        /// Squeeze 티어 2는 방향 단어(위/아래)를 쓰지 않습니다. 그 날은 방향 자체가 없기 때문입니다. (S4)
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
            "나 오늘 왠지 기분 좋은데? 이유는 몰라~",
            "음... 오늘은 뭔가 잘 풀릴 것 같은 날이야.",
            "나 아침부터 콧노래가 나와. 왜 이러지?",
            "오늘 공기가 가벼워. 나만 그렇게 느끼나?",
            "왠지 오늘은 좋은 일 생길 것 같아. 감이야, 감!",
        };

        private static readonly string[] BullClear =
        {
            "오빠, 오늘은 위야! 요미 감각 믿어!",
            "오늘 올라가! 요미가 장담할게, 위로 봐!",
            "오빠 오늘은 참으면 손해야. 요미 말대로 위쪽!",
            "요미 감이 확실해. 오늘 위로 뚫려!",
            "오늘은 요미 믿고 위를 봐. 후회 안 할 거야!",
        };

        private static readonly string[] BearVague =
        {
            "음... 나 오늘은 조심하는 게 좋을 것 같아.",
            "왠지 오늘은 마음이 무거워. 기분 탓인가?",
            "나 오늘 좀 불안한데... 왜 그러지?",
            "오늘은 뭔가 싸늘해. 조심해서 나쁠 건 없잖아?",
            "이유는 모르겠는데 나 오늘 겁이 나.",
        };

        private static readonly string[] BearClear =
        {
            "오늘 떨어져! 오빠 욕심부리면 요미가 화낼 거야!",
            "오빠, 오늘은 아래야. 요미 말 꼭 들어!",
            "오늘 내려가! 요미가 확실히 느껴져. 조심해!",
            "요미 감각이 말해. 오늘은 아래로 간다고!",
            "오빠 오늘은 지키는 날이야. 아래로 흘러!",
        };

        private static readonly string[] SidewaysVague =
        {
            "오늘은 아무 일도 없을 것 같은데... 나만 그런가?",
            "음... 나 오늘 좀 심심할 것 같은 느낌이야.",
            "왠지 오늘은 조용한 날일 것 같아.",
            "나 오늘 별로 두근거리지가 않아. 이상하지?",
            "오늘은 뭔가 밋밋해. 기분 탓이면 좋겠는데.",
        };

        private static readonly string[] SidewaysClear =
        {
            "오늘 완전 지루할 거야. 요미 말 믿고 쉬어!",
            "오빠, 오늘은 아무 일도 안 일어나. 요미가 장담해!",
            "오늘은 쉬는 게 이기는 거야. 요미 믿어!",
            "요미 감각으론 오늘 제자리야. 무리하지 마!",
            "오늘 억지로 하면 손해야. 요미랑 놀자!",
        };

        private static readonly string[] SqueezeVague =
        {
            "나 심장이 두근거려... 왜 이러지?",
            "오늘 뭔가 이상해. 공기가 팽팽한 느낌이야.",
            "나 아까부터 손이 떨려. 이유를 모르겠어...",
            "오늘은 좀... 무서워. 뭔가 터질 것 같아.",
            "왠지 오늘 조용하지 않을 것 같아. 나만 그래?",
        };

        // Squeeze는 방향이 없는 날입니다. 방향 단어를 쓰면 반드시 거짓말이 됩니다. (S4)
        private static readonly string[] SqueezeClear =
        {
            "오빠, 요미 무서워... 오늘 엄청 흔들릴 거야!",
            "오늘 미친 듯이 요동칠 거야! 오빠 조심해!",
            "요미 감각이 비명을 질러. 오늘 크게 터져!",
            "오늘은 방향이 없어! 그냥 다 부서질 거야!",
            "오빠 오늘은 물러나 있어. 요미가 무서워서 그래!",
        };
    }
}
