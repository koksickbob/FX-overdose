namespace FXOverdose.Events.Story
{
    /// <summary>이벤트를 어떤 표현으로 재생할지. 계획 3장.</summary>
    public enum EventHostKind
    {
        /// <summary>현재 씬 위에 캔버스를 얹고 콜백으로 끝냅니다. 씬 전환 0회. 기본값.</summary>
        Overlay = 0,

        /// <summary>전용 씬으로 넘어갔다가 ReturnScene으로 복귀합니다. 긴 이벤트·화면 전체 전환용.</summary>
        Scene = 1
    }

    /// <summary>
    /// 요미 스탠딩의 표정. 미연시 전용 24종([P2_07])이고, 트레이딩 파트의 <c>TraderEmotion</c>과는
    /// 서로 변환하지 않습니다 (P2_07 7절).
    ///
    /// <para>⚠️ <b>이름이 곧 파일명입니다</b> — <c>Resources/DatingSim/Emotions/Sprites/{T1~T4}/{이름}.png</c>.
    /// 값을 추가·개명하면 스프라이트도 같이 개명해야 합니다.</para>
    /// </summary>
    public enum EventEmotion
    {
        // 기본 12종 (P2_07 3.1절)
        Calm = 0,
        Joy,
        Sadness,
        Anger,
        Fear,
        Surprise,
        Flustered,
        Anxiety,
        Relief,
        Disappointment,
        Fatigue,
        Curiosity,

        // 연애 12종 (P2_07 3.2절)
        Interest,
        Fondness,
        Excitement,
        Affection,
        Love,
        Trust,
        Shyness,
        Jealousy,
        Hurt,
        Loneliness,
        Doubt,
        Obsession
    }

    /// <summary>선택지 하나. 문법은 ChoiceTalk의 <c>TalkChoice</c>를 따릅니다(25자 이내·반말·즉답 1줄).</summary>
    public readonly struct EventChoice
    {
        /// <summary>플레이어가 고르는 대사.</summary>
        public readonly string Text;

        /// <summary>호감도 증감. ⚠️ 즉시 반영하지 않고 러너의 지연 커밋 버퍼에 쌓입니다. (계획 7.1절)</summary>
        public readonly int Affection;

        /// <summary>고른 직후 요미의 즉답. 없으면 null.</summary>
        public readonly string Reply;

        /// <summary>★ 분기 — 이 선택을 고르면 갈 노드. -1이면 이벤트 종료.</summary>
        public readonly int NextId;

        /// <summary>세이브에 남길 분기 플래그. 없으면 null.</summary>
        public readonly string SetFlag;

        public EventChoice(string text, int affection, string reply, int nextId, string setFlag = null)
        {
            Text = text;
            Affection = affection;
            Reply = reply;
            NextId = nextId;
            SetFlag = setFlag;
        }
    }

    /// <summary>
    /// 이벤트 노드 하나 = 클릭 한 번 분량.
    ///
    /// ChoiceTalk의 <c>TalkNode</c>는 선형 배열 + 무조건 다음 인덱스 진행이라 그대로 쓸 수 없습니다.
    /// 여기는 <see cref="NextId"/> 기반 그래프라 분기가 됩니다. (계획 5.1절)
    /// </summary>
    public readonly struct EventNode
    {
        /// <summary>이벤트 안에서만 유효한 노드 번호. 첫 노드가 반드시 존재해야 합니다.</summary>
        public readonly int Id;

        /// <summary>대사 1~3줄. "※"로 시작하면 지문 — 타자기 없이 즉시 표시됩니다. (ChoiceTalk와 동일 규칙)</summary>
        public readonly string[] Lines;

        /// <summary>말하는 사람. null이면 지문/독백으로 간주해 화자명을 숨깁니다.</summary>
        public readonly string Speaker;

        /// <summary>배경/CG 교체. null이면 직전 배경을 유지합니다.</summary>
        public readonly string BackgroundId;

        public readonly EventEmotion Emotion;

        /// <summary>비어 있으면 <see cref="NextId"/>로 자동 진행합니다.</summary>
        public readonly EventChoice[] Choices;

        /// <summary>선택지가 없을 때 갈 다음 노드. -1이면 종료.</summary>
        public readonly int NextId;

        public EventNode(int id, string speaker, string[] lines, int nextId,
            string backgroundId = null, EventEmotion emotion = EventEmotion.Calm, EventChoice[] choices = null)
        {
            Id = id;
            Speaker = speaker;
            Lines = lines;
            NextId = nextId;
            BackgroundId = backgroundId;
            Emotion = emotion;
            Choices = choices;
        }

        public bool HasChoices => Choices != null && Choices.Length > 0;
    }

    /// <summary>이벤트 한 편.</summary>
    public readonly struct EventDefinition
    {
        /// <summary>⚠️ 영구 식별자. 세이브에 기록되므로 <b>재사용 금지</b>입니다. (계획 5.1절)</summary>
        public readonly string Id;

        /// <summary>호출자가 지정하지 않았을 때 쓰는 표현 방식.</summary>
        public readonly EventHostKind DefaultHost;

        /// <summary>요미 스탠딩을 숨깁니다. CG 안에 요미가 그려진 이벤트(프롤로그 등)는 겹치면 안 됩니다.</summary>
        public readonly bool HideStanding;

        public readonly EventNode[] Nodes;

        public EventDefinition(string id, EventHostKind defaultHost, bool hideStanding, EventNode[] nodes)
        {
            Id = id;
            DefaultHost = defaultHost;
            HideStanding = hideStanding;
            Nodes = nodes;
        }

        /// <summary>노드를 ID로 찾습니다. 없으면 false — 데이터 오타를 진행 중에 잡습니다.</summary>
        public bool TryGetNode(int id, out EventNode node)
        {
            if (Nodes != null)
            {
                // LINQ를 쓰지 않는 것은 ScenarioMatcher와 같은 이유입니다(무할당). 노드 수도 적습니다.
                for (int i = 0; i < Nodes.Length; i++)
                {
                    if (Nodes[i].Id != id) continue;
                    node = Nodes[i];
                    return true;
                }
            }

            node = default;
            return false;
        }
    }
}
