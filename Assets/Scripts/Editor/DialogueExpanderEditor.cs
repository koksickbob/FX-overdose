using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;
using System.Collections.Generic;

namespace FXOverdose.Editor
{
    public class DialogueExpanderEditor : EditorWindow
    {
        [MenuItem("FX Overdose/AI/Expand Dialogue Database")]
        public static void ExpandDialogues()
        {
            string[] guids = AssetDatabase.FindAssets("t:YomiDialogueDatabase");
            if (guids.Length == 0)
            {
                Debug.LogError("YomiDialogueDatabase ?? ??? ? ????.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            YomiDialogueDatabase db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(path);

            if (db == null)
            {
                Debug.LogError("Failed to load YomiDialogueDatabase at " + path);
                return;
            }

            int addedCount = 0;

            // 추가할 레버리지 케이스
            int[] leverageCases = { 10, 25, 50, 75, 100, 125 };
            
            // 공통 변수
            string[] trends = { "Bull", "Bear", "Sideways", "Volatile" };

            // 롱(Long) 진입 대사 템플릿
            List<(string text, string mental, DirectionTag dir)> longTemplates = new List<(string, string, DirectionTag)>
            {
                ("이번엔 {leverage}배 롱이다! 내 자산의 {margin}%를 걸었어!", "Manic", DirectionTag.Up),
                ("신호 떴다! 롱 {leverage}배 진입. 비중 {margin}%!", "Focused", DirectionTag.Up),
                ("하하하! {leverage}배 롱 가즈아! 자산의 {margin}%를 불태운다!", "Euphoria", DirectionTag.Up),
                ("진정하자... {leverage}배 롱으로 비중 {margin}% 투입.", "Stable", DirectionTag.Up),
                ("진짜 무섭지만... {leverage}배 롱 간다! {margin}% 베팅!", "Panicked", DirectionTag.Up)
            };

            // 숏(Short) 진입 대사 템플릿
            List<(string text, string mental, DirectionTag dir)> shortTemplates = new List<(string, string, DirectionTag)>
            {
                ("숏이 답이다! {leverage}배로 내리꽂아!! 비중 {margin}%!!", "Furious", DirectionTag.Down),
                ("확실한 숏 타점. {leverage}배 진입, 마진 {margin}%.", "Focused", DirectionTag.Down),
                ("크큭... {leverage}배 숏 간다! {margin}% 비중 슛!!", "Manic", DirectionTag.Down),
                ("반등 끝! 숏 {leverage}배 진입. {margin}% 들어간다.", "Stable", DirectionTag.Down),
                ("제발 떡락해라... {leverage}배 숏! {margin}% 걸었어...", "Panicked", DirectionTag.Down)
            };

            Undo.RecordObject(db, "Expand Dialogues");

            foreach (var lev in leverageCases)
            {
                // 레버리지에 따른 기본 마진 리스크 설정
                bool isHighRisk = lev >= 50;
                
                // 트렌드별 반복
                foreach (var trend in trends)
                {
                    // 롱 추가
                    foreach (var temp in longTemplates)
                    {
                        var entry = new YomiDialogueEntry(temp.text, trend, temp.mental, "Long", temp.dir, false, lev, 0, 0, isHighRisk, "");
                        db.entries.Add(entry);
                        addedCount++;
                    }

                    // 숏 추가
                    foreach (var temp in shortTemplates)
                    {
                        var entry = new YomiDialogueEntry(temp.text, trend, temp.mental, "Short", temp.dir, false, lev, 0, 0, isHighRisk, "");
                        db.entries.Add(entry);
                        addedCount++;
                    }
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Dialogue Expander] 성공적으로 {addedCount}개의 동적 레버리지/마진 대사가 YomiDialogueDatabase에 추가되었습니다!");
        }
    }
}
