using System;
using UnityEngine;
using FXOverdose.Trading; // for PositionType, etc.

namespace FXOverdose.Events
{
    [Serializable]
    public class EventLogicOptionData
    {
        public ChoiceOptionType OptionType;
        public string OptionTitle;
        public string OptionDescription;

        public string RequiredItemId;
        public int RequiredItemCount;
        
        [Range(0f, 1f)] public float OverrideSignalProbTrue = 0f;
        public float OverrideBeamPercent;
        public int OverrideDurationSeconds = 150;
        
        public int MentalChangeAmount;
        public int HealthChangeAmount;
        
        public int ForceLeverage;
        public TradingController.PositionType ForcePosition;
        public TradingController.EventPositionHandlingMode PositionHandlingMode;
        
        public float CustomTargetROELimit;
        public float CustomStopLossROELimit;
    }

    [CreateAssetMenu(fileName = "NewEventLogicTemplate", menuName = "FX Overdose/AI/Event Logic Template")]
    public class EventLogicTemplateSO : ScriptableObject
    {
        public string TemplateID;
        public string ThemeTag; // e.g. "BullMarket_Pump", "BearMarket_Crash"
        
        public EventLogicOptionData[] LogicOptions = new EventLogicOptionData[3];
    }
}
