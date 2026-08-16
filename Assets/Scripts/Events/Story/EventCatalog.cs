namespace FXOverdose.Events.Story
{
    /// <summary>
    /// 이벤트 대사 테이블.
    ///
    /// ⚠️ <b>ScriptableObject로 옮기지 마십시오.</b> 폰트 프리베이크(<c>Tools/Prebake All Scripts Text into Font</c>)가
    /// <c>.cs</c> 소스만 스캔하므로, 에셋으로 빼면 새 한글이 게임에서 □로 렌더됩니다.
    /// <c>YomiTalkTopics</c>가 같은 이유로 정적 테이블입니다. (계획 5.2절)
    ///
    /// 대사를 추가한 뒤에는 프리베이크를 다시 실행해야 합니다.
    /// </summary>
    public static class EventCatalog
    {
        /// <summary>
        /// 시스템 검증용 짧은 이벤트. 선택지 분기·호감도 델타·플래그를 전부 한 번씩 밟습니다.
        /// 실제 콘텐츠가 들어오면 지워도 되지만, 두 호스트의 동작이 같은지 비교하는 데 이만한 게 없습니다.
        /// </summary>
        public const string SampleEventId = "EVT_SAMPLE_001";

        private static readonly EventDefinition Sample = new EventDefinition(
            SampleEventId,
            EventHostKind.Overlay,
            hideStanding: false,
            nodes: new[]
            {
                new EventNode(0, null, new[] { "※늦은 밤, 방문 틈으로 불빛이 새어 나온다." }, nextId: 1),

                new EventNode(1, "요미", new[]
                {
                    "오빠... 아직 안 잤어?",
                    "요미도... 잠이 안 와서."
                }, nextId: 2, emotion: EventEmotion.Loneliness),

                new EventNode(2, "요미", new[] { "옆에... 있어도 돼?" }, nextId: -1,
                    emotion: EventEmotion.Flustered,
                    choices: new[]
                    {
                        new EventChoice("당연하지. 이리 와.", 3, "히히... 고마워, 오빠.", nextId: 3, setFlag: "SAMPLE_WARM"),
                        new EventChoice("나 내일 일찍 나가야 해.", -1, "...응. 알았어.", nextId: 4)
                    }),

                new EventNode(3, "요미", new[]
                {
                    "이렇게 있으면... 아무 생각도 안 나.",
                    "계속 이랬으면 좋겠다."
                }, nextId: -1, emotion: EventEmotion.Joy),

                new EventNode(4, "요미", new[] { "...먼저 잘게. 잘 자, 오빠." }, nextId: -1,
                    emotion: EventEmotion.Sadness)
            });

        /// <summary>등록된 전체 이벤트. 새 이벤트는 여기에 추가합니다.</summary>
        private static readonly EventDefinition[] All = { Sample };

        /// <summary>ID로 이벤트를 찾습니다. 없으면 false.</summary>
        public static bool TryGet(string eventId, out EventDefinition definition)
        {
            if (!string.IsNullOrEmpty(eventId))
            {
                for (int i = 0; i < All.Length; i++)
                {
                    if (All[i].Id != eventId) continue;
                    definition = All[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }
    }
}
