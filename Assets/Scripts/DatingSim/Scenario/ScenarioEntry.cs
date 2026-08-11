using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.DatingSim.Scenario
{
    [Serializable]
    public class ScenarioEntry
    {
        [Header("1. 식별자")]
        public string scenarioID;
        public string category; // Daily, Romance, Conflict, Jealousy, Event 등

        [Header("2. 스코어링 및 필터링 조건")]
        public string requiredTime = "Any"; // Any, Day, Night, Dawn
        public string requiredMood = "Any"; // Any, Happy, Anxious, Obsessive 등
        public int targetAffection = 50; // 이 대본의 이상적인 호감도 목표치
        public int targetObsession = 50; // 이 대본의 이상적인 집착도 목표치
        public float minIdleHours = 0f; // 이 대본이 뜨기 위해 필요한 최소 방치 시간
        public int maxTurns = 5; // 씬 강제 종료를 위한 최대 대화 턴 수
        
        [Tooltip("반드시 켜져 있어야 하는 플래그 (쉼표로 구분)")]
        public string requiredFlags; 
        
        [Tooltip("켜져 있으면 절대 이 씬이 등장하지 않는 플래그 (쉼표로 구분)")]
        public string blockingFlags;

        [Header("3. LLM 대본 및 지시문 (Actor Mode)")]
        [TextArea(2, 5)]
        public string contextDescription; // 상황 설명
        [TextArea(2, 5)]
        public string actorGoal; // LLM 행동 목표
        
        [Tooltip("작가가 작성한 모방용 예시 대사들")]
        public List<string> dialogueExamples = new List<string>();

        [Header("4. 씬 종료 후 처리 (보상)")]
        public int rewardAffection = 0;
        public int rewardObsession = 0;
        [Tooltip("대화 종료 시 새로 켜지는 플래그 (쉼표로 구분)")]
        public string setFlagsOnComplete;
    }
}
