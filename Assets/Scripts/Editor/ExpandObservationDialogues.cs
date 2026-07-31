using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;

namespace FXOverdose.Editor
{
    public class ExpandObservationDialogues
    {
        [MenuItem("FX Overdose/Tools/Expand Observation Dialogues")]
        public static void Expand()
        {
            string dbPath = "Assets/YomiDialogueDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(dbPath);
            if (db == null)
            {
                Debug.LogError($"[ExpandObservationDialogues] Failed to load YomiDialogueDatabase at {dbPath}");
                return;
            }

            int countBefore = db.entries.Count;

            // 일반 관망 (None 포지션)
            string[] observationDialogues = new string[]
            {
                "지금은 확실한 타점이 안 보이네. 무리해서 들어갈 필요 없어.",
                "현금을 쥐고 있는 것도 아주 훌륭한 포지션이야. 조급해하지 마.",
                "시장이 방향을 정할 때까지 팝콘이나 먹으면서 구경하자.",
                "차트 흐름이 지저분해. 이럴 땐 쉬는 게 돈 버는 거지.",
                "어설픈 자리에서 들어가면 세력들한테 설거지 당하기 딱 좋아. 관망해.",
                "진입 시그널이 뜰 때까지 인내심을 가져. 사냥감은 아직 나타나지 않았어.",
                "지금 진입하면 기도매매밖에 안 돼. 내 완벽한 분석이 끝날 때까지 기다려.",
                "방향성이 나오기 전까진 섣불리 움직이지 않을 거야.",
                "수수료 아깝게 왜 이리저리 찔러봐? 확실한 자리 올 때까지 대기해.",
                "애매한 횡보장에서는 현금 채굴이나 하면서 때를 기다리는 거야.",
                "지금은 쉴 타이밍이야. 차트 너무 뚫어지게 보지 말고 물이나 마시고 와.",
                "세력들이 개미 털기 중인 것 같아. 휩소에 당하지 말고 가만히 있어.",
                "호랑이는 토끼를 잡을 때도 최선을 다하는 법. 진짜 타이밍은 아직이야.",
                "이런 애매한 장에서는 아무것도 안 하는 사람이 승자야."
            };

            foreach (string text in observationDialogues)
            {
                // 포지션: None, 트렌드: None, 멘탈: None
                var entry = new YomiDialogueEntry(text, "None", "None", "None", DirectionTag.None, false, 0, 0, 0, false, "");
                db.entries.Add(entry);
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ExpandObservationDialogues] 관망 대사 {observationDialogues.Length}개 추가 완료! (기존 {countBefore} -> 현재 {db.entries.Count})");
        }
    }
}
