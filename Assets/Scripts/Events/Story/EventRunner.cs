using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.Events.Story
{
    /// <summary>대사 진행 상태. 입력 레이어는 <b>오직 이 값으로만</b> 결정됩니다. (계획 7.7절)</summary>
    public enum EventPhase
    {
        /// <summary>글자 단위 출력 중. 클릭 → 전문 즉시 표시.</summary>
        Typing,

        /// <summary>출력 완료, 대기. 아무 영역 클릭 → 다음 노드.</summary>
        Revealed,

        /// <summary>선택지 표시 중. ClickCatcher는 반드시 비활성.</summary>
        Choice,

        /// <summary>종료 도달. 커밋 대기.</summary>
        Finished
    }

    /// <summary>
    /// 이벤트 진행의 코어 — 상태기계 + 분기 + <b>지연 커밋 버퍼</b>.
    ///
    /// MonoBehaviour가 아니고 UnityEngine.UI를 참조하지 않습니다. 오버레이든 전용 씬이든
    /// <b>이 클래스 하나만 돌기 때문에</b> 두 표현의 진행 결과가 같다는 것이 보장됩니다. (계획 R12)
    ///
    /// <para><b>제1 원칙 — 진행 중에는 기존 매니저의 상태 변경 API를 일절 호출하지 않습니다.</b>
    /// <c>DatingTimeManager</c>의 상태 변경 메서드는 전부 그 자리에서 디스크에 쓰기 때문에(계획 7.1절),
    /// 도중에 부르면 "호감도는 올랐는데 이벤트는 미완료"라는 반쪽 상태가 디스크에 남습니다.
    /// 호감도·플래그·선택 이력은 전부 여기 버퍼에 쌓였다가 <see cref="Commit"/>에서 한 번에 나갑니다.</para>
    /// </summary>
    public sealed class EventRunner
    {
        private const int MaxChoiceHistory = 300; // TalkChoiceHistory와 동일한 상한 (TS4)

        private readonly EventDefinition definition;
        private EventNode currentNode;
        private bool hasNode;

        // ── 지연 커밋 버퍼 ─────────────────────────────────────────────
        private int pendingAffection;
        private readonly List<string> pendingFlags = new List<string>();
        private readonly List<string> pendingChoiceHistory = new List<string>();

        public EventPhase Phase { get; private set; }
        public EventDefinition Definition => definition;
        public EventNode CurrentNode => currentNode;
        public bool IsFinished => Phase == EventPhase.Finished;

        /// <summary>이벤트 시작부터의 (화자, 대사) 로그. <b>메모리 전용</b> — 저장하지 않습니다. (계획 4.4절)</summary>
        public readonly List<(string Speaker, string Line)> Backlog = new List<(string, string)>();

        public EventRunner(EventDefinition definition)
        {
            this.definition = definition;

            // 첫 노드는 항상 0번입니다. 없으면 데이터가 잘못된 것이라 즉시 종료 상태로 둡니다.
            if (definition.TryGetNode(0, out EventNode first))
            {
                currentNode = first;
                hasNode = true;
                Phase = EventPhase.Typing;
            }
            else
            {
                Debug.LogError($"[EventRunner] '{definition.Id}'에 0번 노드가 없습니다. 이벤트를 재생할 수 없습니다.");
                Phase = EventPhase.Finished;
            }
        }

        /// <summary>현재 줄의 출력이 끝났음을 알립니다. 선택지가 있으면 [Choice]로, 없으면 [Revealed]로 갑니다.</summary>
        public void MarkRevealed()
        {
            if (Phase != EventPhase.Typing) return;
            Phase = hasNode && currentNode.HasChoices ? EventPhase.Choice : EventPhase.Revealed;
        }

        /// <summary>백로그에 한 줄 남깁니다. 뷰가 실제로 출력한 순서대로 부릅니다.</summary>
        public void RecordLine(string speaker, string line) => Backlog.Add((speaker, line));

        /// <summary>
        /// 선택지 없는 노드에서 다음으로 넘어갑니다. 종료에 도달하면 false.
        /// </summary>
        public bool Advance()
        {
            if (Phase != EventPhase.Revealed) return !IsFinished;
            return GoTo(hasNode ? currentNode.NextId : -1);
        }

        /// <summary>
        /// 선택지를 고릅니다. 결과는 <b>버퍼에만</b> 쌓입니다.
        /// 요미의 즉답을 돌려줍니다(없으면 null) — 뷰가 다음 노드보다 먼저 출력해야 합니다. (R-2)
        /// </summary>
        public string SelectChoice(int index)
        {
            if (Phase != EventPhase.Choice || !hasNode) return null;
            if (index < 0 || index >= currentNode.Choices.Length) return null;

            EventChoice choice = currentNode.Choices[index];

            pendingAffection += choice.Affection;
            if (!string.IsNullOrEmpty(choice.SetFlag) && !pendingFlags.Contains(choice.SetFlag))
                pendingFlags.Add(choice.SetFlag);

            // 포맷은 TalkChoiceHistory와 동일합니다: "<이벤트ID>:<노드>:<선택인덱스>"
            pendingChoiceHistory.Add($"{definition.Id}:{currentNode.Id}:{index}");

            Backlog.Add(("나", choice.Text));

            GoTo(choice.NextId);
            return choice.Reply;
        }

        private bool GoTo(int nextId)
        {
            if (nextId < 0 || !definition.TryGetNode(nextId, out EventNode next))
            {
                if (nextId >= 0)
                    Debug.LogError($"[EventRunner] '{definition.Id}'의 {nextId}번 노드를 찾을 수 없습니다. 이벤트를 종료합니다.");

                hasNode = false;
                Phase = EventPhase.Finished;
                return false;
            }

            currentNode = next;
            hasNode = true;
            Phase = EventPhase.Typing;
            return true;
        }

        /// <summary>
        /// ★ <b>2단계 커밋</b> — 이 시스템의 핵심입니다. (계획 6.2절)
        ///
        /// <para>1단계는 <c>SaveLoadManager.CurrentData</c>에 반영합니다. 메모리라 <b>항상 성공</b>합니다.</para>
        /// <para>2단계는 디스크 저장을 1회 시도합니다. 실패해도 1단계가 남아 있으므로,
        /// 다음 저장이 언제 일어나든 부분 저장 베이스(<c>SaveGame</c>의 <c>CurrentData ?? ReadSaveFile</c>)를 통해
        /// 함께 실려 나갑니다. <b>결과가 사라지지는 않습니다.</b></para>
        ///
        /// <para>저장이 실패하는 경로가 실제로 셋 있습니다 — 스토리 모드가 아님 / GameManager 상태가
        /// Playing·Paused가 아님(<b>Settlement 포함</b>) / 오버도즈 중. 기존 스토리 이벤트가 바로 그
        /// Settlement 상태에서 발화하므로 이 구조가 없으면 이벤트 결과가 통째로 증발합니다.</para>
        /// </summary>
        /// <param name="returnScene">
        /// 전용 씬 호스트만 넘깁니다. EventScene은 복귀 가능 씬 목록에 없어 <c>SaveGame</c>이
        /// <c>LastSceneName</c>을 갱신해 주지 않기 때문에, 여기서 직접 적어야 프롤로그 직후 종료한
        /// 플레이어가 거래 화면이 아니라 제자리로 돌아옵니다. (계획 7.5절) 오버레이는 null.
        /// </param>
        public void Commit(string returnScene = null)
        {
            var save = SaveLoadManager.Instance;
            SaveData data = save?.CurrentData;

            if (data == null)
            {
                // 씬 단독 재생(SV-C6). 디스크에 쓸 것이 없으니 조용히 끝냅니다.
                Debug.LogWarning($"[EventRunner] 세이브 데이터가 없어 '{definition.Id}'의 결과를 기록하지 않습니다. " +
                                 "(씬 단독 재생 중으로 보입니다)");
                return;
            }

            // ── 1단계: 메모리 반영 (항상 성공) ───────────────────────────
            if (!data.EventCompletedIds.Contains(definition.Id))
                data.EventCompletedIds.Add(definition.Id);

            for (int i = 0; i < pendingFlags.Count; i++)
            {
                if (!data.StoryFlags.Contains(pendingFlags[i]))
                    data.StoryFlags.Add(pendingFlags[i]);
            }

            data.EventChoiceHistory.AddRange(pendingChoiceHistory);
            int overflow = data.EventChoiceHistory.Count - MaxChoiceHistory;
            if (overflow > 0) data.EventChoiceHistory.RemoveRange(0, overflow);

            if (!string.IsNullOrEmpty(returnScene))
                data.LastSceneName = returnScene;

            // ── 2단계: 디스크 1회 시도 (실패 가능) ────────────────────────
            // 호감도는 매니저를 통해 흘립니다. 역대 최고치 갱신(TS8)과 UI 이벤트가 그쪽에 있어서,
            // data.DatingAffection을 직접 건드리면 대화 토픽 해금이 조용히 어긋납니다.
            // ⚠️ ModifyAffection이 자체적으로 한 번 저장하므로 델타가 있으면 쓰기가 2회입니다.
            //    그 대가로 아래에서 성공 여부를 받아 볼 수 있습니다 — 이벤트 종료는 드문 일이라 이쪽이 낫습니다.
            if (pendingAffection != 0)
                FXOverdose.DatingSim.Core.DatingTimeManager.Instance?.ModifyAffection(pendingAffection);

            bool saved = save.SaveCurrentGame();
            if (!saved)
            {
                Debug.LogWarning($"[EventRunner] '{definition.Id}' 결과를 지금 디스크에 쓰지 못했습니다. " +
                                 "메모리(CurrentData)에는 반영됐으므로 다음 저장에 함께 기록됩니다.");
            }

            pendingAffection = 0;
            pendingFlags.Clear();
            pendingChoiceHistory.Clear();
        }
    }
}
