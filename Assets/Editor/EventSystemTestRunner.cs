using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Events.Story;

namespace FXOverdose.EditorTools
{
    /// <summary>
    /// 이벤트 시스템의 검증 도구.
    ///
    /// <list type="bullet">
    /// <item><b>데이터 검증</b> — 플레이 모드 없이 돕니다. 노드 그래프가 성립하는지 정적으로 확인합니다.
    /// 이벤트가 40노드를 넘어가면 오타 하나가 "그 분기만 재생 중에 죽는" 형태로 나타나서, 실제로
    /// 그 선택지를 고르기 전까지 발견되지 않습니다.</item>
    /// <item><b>샘플 재생</b> — 플레이 모드에서 두 호스트를 각각 띄웁니다. 계획 10절의
    /// "같은 이벤트를 두 호스트로 완주 → 동일 결과"를 눈으로 확인하는 수단입니다.</item>
    /// </list>
    /// </summary>
    public static class EventSystemTestRunner
    {
        [MenuItem("FXOverdose/Debug/Validate Event Data")]
        public static void ValidateEventData()
        {
            if (!EventCatalog.TryGet(EventCatalog.SampleEventId, out EventDefinition sample))
            {
                Debug.LogError("[EventValidator] 샘플 이벤트를 찾지 못했습니다. 카탈로그가 비어 있습니까?");
                return;
            }

            int problems = Validate(sample) + ValidateEmotionSprites();
            if (problems == 0)
                Debug.Log($"[EventValidator] '{sample.Id}' 검증 통과 — 노드 {sample.Nodes.Length}개, 문제 없음.");
            else
                Debug.LogError($"[EventValidator] '{sample.Id}'에서 문제 {problems}건을 찾았습니다. 위 로그를 확인하십시오.");
        }

        /// <summary>
        /// <see cref="EventEmotion"/> 24종 × 티어 4구간 = 96장이 전부 로드되는지 확인합니다.
        ///
        /// <para>enum 이름이 곧 파일명이라, 값을 하나 추가하고 스프라이트를 안 넣으면 <b>그 감정이 쓰인 대사에
        /// 도달했을 때만</b> 조용히 Calm으로 떨어집니다. 여기서 미리 잡습니다.
        /// Multiple 스프라이트 모드라 <c>LoadAll</c>이어야 한다는 점도 <c>EventView</c>와 같습니다.</para>
        /// </summary>
        private static int ValidateEmotionSprites()
        {
            int problems = 0;

            foreach (string tier in new[] { "T1", "T2", "T3", "T4" })
            {
                foreach (EventEmotion emotion in System.Enum.GetValues(typeof(EventEmotion)))
                {
                    string path = $"DatingSim/Emotions/Sprites/{tier}/{emotion}";
                    if (Resources.LoadAll<Sprite>(path).Length > 0) continue;

                    Debug.LogError($"[EventValidator] 감정 스프라이트 없음 — Resources/{path}.png " +
                                   "(파일이 없거나 Texture Type이 Sprite가 아닙니다)");
                    problems++;
                }
            }

            return problems;
        }

        /// <summary>이벤트 하나를 정적으로 검사하고 문제 건수를 돌려줍니다.</summary>
        public static int Validate(EventDefinition definition)
        {
            int problems = 0;

            if (definition.Nodes == null || definition.Nodes.Length == 0)
            {
                Debug.LogError($"[EventValidator] '{definition.Id}'에 노드가 하나도 없습니다.");
                return 1;
            }

            // 1) 노드 ID 중복 — 중복이면 TryGetNode가 앞의 것만 찾아 뒤쪽이 영영 재생되지 않습니다.
            var ids = new HashSet<int>();
            foreach (EventNode node in definition.Nodes)
            {
                if (ids.Add(node.Id)) continue;
                Debug.LogError($"[EventValidator] '{definition.Id}': 노드 ID {node.Id}이(가) 중복입니다.");
                problems++;
            }

            // 2) 시작 노드
            if (!ids.Contains(0))
            {
                Debug.LogError($"[EventValidator] '{definition.Id}': 0번 노드가 없어 재생을 시작할 수 없습니다.");
                problems++;
            }

            // 3) 끊어진 링크 — 여기가 이 검사의 존재 이유입니다.
            var reachable = new HashSet<int>();
            foreach (EventNode node in definition.Nodes)
            {
                if (node.HasChoices)
                {
                    for (int i = 0; i < node.Choices.Length; i++)
                    {
                        int next = node.Choices[i].NextId;
                        if (next >= 0) reachable.Add(next);
                        if (next < 0 || ids.Contains(next)) continue;

                        Debug.LogError($"[EventValidator] '{definition.Id}': 노드 {node.Id}의 선택지 {i}가 " +
                                       $"없는 노드 {next}을(를) 가리킵니다.");
                        problems++;
                    }
                }
                else
                {
                    if (node.NextId >= 0) reachable.Add(node.NextId);
                    if (node.NextId >= 0 && !ids.Contains(node.NextId))
                    {
                        Debug.LogError($"[EventValidator] '{definition.Id}': 노드 {node.Id}이(가) " +
                                       $"없는 노드 {node.NextId}(으)로 진행합니다.");
                        problems++;
                    }
                }

                if (node.Lines == null || node.Lines.Length == 0)
                {
                    Debug.LogError($"[EventValidator] '{definition.Id}': 노드 {node.Id}에 대사가 없습니다.");
                    problems++;
                }
            }

            // 4) 도달 불가 노드 — 오류는 아니지만 대부분 오타입니다. 대사를 써 놓고 못 보는 상태입니다.
            var orphans = new StringBuilder();
            foreach (EventNode node in definition.Nodes)
            {
                if (node.Id == 0 || reachable.Contains(node.Id)) continue;
                if (orphans.Length > 0) orphans.Append(", ");
                orphans.Append(node.Id);
            }

            if (orphans.Length > 0)
                Debug.LogWarning($"[EventValidator] '{definition.Id}': 어디서도 도달할 수 없는 노드 — {orphans}");

            return problems;
        }

        [MenuItem("FXOverdose/Debug/Play Sample Event (Overlay)")]
        public static void PlaySampleOverlay() => PlaySample(EventHostKind.Overlay);

        [MenuItem("FXOverdose/Debug/Play Sample Event (Scene)")]
        public static void PlaySampleScene() => PlaySample(EventHostKind.Scene);

        [MenuItem("FXOverdose/Debug/Play Sample Event (Overlay)", true)]
        [MenuItem("FXOverdose/Debug/Play Sample Event (Scene)", true)]
        public static bool ValidatePlaySample() => Application.isPlaying;

        private static void PlaySample(EventHostKind host)
        {
            // 같은 이벤트를 두 번 보려면 완료 기록을 지워야 합니다. 테스트 편의를 위한 것이고,
            // 게이트 자체를 우회하는 것이 아니라 재생 전에 기록만 되돌립니다.
            var data = FXOverdose.Core.SaveLoadManager.Instance?.CurrentData;
            data?.EventCompletedIds.Remove(EventCatalog.SampleEventId);

            bool started = EventLauncher.Play(EventCatalog.SampleEventId, host,
                returnScene: host == EventHostKind.Scene ? "YomiRoomScene" : null);

            if (!started)
                Debug.LogError($"[EventSystemTest] 샘플 이벤트를 {host} 호스트로 재생하지 못했습니다. 게이트 로그를 확인하십시오.");
        }
    }
}
