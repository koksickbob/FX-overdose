using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.AI.Dialogue
{
    public enum DailyDialogueCategory
    {
        Greeting,
        Eating,
        Sleeping,
        Playing,
        Anger,
        Affection,
        Default
    }

    [Serializable]
    public class YomiDailyDialogueEntry
    {
        [TextArea(2, 5)]
        public string text;
        public DailyDialogueCategory category = DailyDialogueCategory.Default;
        
        public YomiDailyDialogueEntry(string t, DailyDialogueCategory cat = DailyDialogueCategory.Default)
        {
            text = t;
            category = cat;
        }
    }

    [CreateAssetMenu(fileName = "YomiDailyDialogueDatabase", menuName = "FX Overdose/AI/Yomi Daily Dialogue Database")]
    public class YomiDailyDialogueDatabase : ScriptableObject
    {
        public List<YomiDailyDialogueEntry> entries = new List<YomiDailyDialogueEntry>();
    }
}
