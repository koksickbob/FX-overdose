using UnityEngine;
using UnityEditor;
using FXOverdose.Events;
using FXOverdose.Trading;
using System.IO;
using System.Collections.Generic;

namespace FXOverdose.Editor
{
    public class GenerateEventTemplates
    {
        private static string[] Categories = { "Macro", "Exchange", "Whale", "Influencer", "Technical", "Cyber", "Regulatory", "Altcoin", "OnChain", "Community" };
        private static string[] Flows = { "Pump", "Crash", "Sideways", "Whipsaw" };
        private static string[] Risks = { "Low", "Medium", "High" };
        private static string[] Times = { "AsiaSession", "USSession" };

        private static string[] Items = { "energy_drink", "sedative", "supplement", "dessert" };

        [MenuItem("Tools/FX OVERDOSE/Generate 240 Event Templates")]
        public static void GenerateTemplates()
        {
            string dir = "Assets/Resources/Events/Templates";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            int count = 0;

            foreach (var cat in Categories)
            {
                foreach (var flow in Flows)
                {
                    foreach (var risk in Risks)
                    {
                        foreach (var time in Times)
                        {
                            string templateID = $"{cat}_{flow}_{risk}_{time}";
                            string themeDesc = GetThemeDescription(cat, flow, risk, time);
                            string themeTag = $"[{templateID}] {themeDesc}";
                            
                            EventLogicOptionData[] options = GenerateRandomOptions(flow, risk);
                            
                            CreateTemplate(dir, templateID, themeTag, options);
                            count++;
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GenerateEventTemplates] 🎉 총 {count}개의 조합형 템플릿 생성 완료! 경로: {dir}");
        }

        private static string GetThemeDescription(string cat, string flow, string risk, string time)
        {
            string t = time == "AsiaSession" ? "아시아장 시간대" : "미국장 시간대 개장 직후";
            string r = risk == "High" ? "극도의 치명적인" : (risk == "Medium" ? "상당한" : "비교적 경미한");
            
            string cDesc = "";
            switch (cat)
            {
                case "Macro": cDesc = "거시경제(금리/물가) 지표 발표로 인한"; break;
                case "Exchange": cDesc = "글로벌 대형 거래소의 보안/뱅크런 이슈로 인한"; break;
                case "Whale": cDesc = "초거대 고래 지갑의 대규모 물량 이동으로 인한"; break;
                case "Influencer": cDesc = "유명 인플루언서의 기습적인 SNS 발언으로 인한"; break;
                case "Technical": cDesc = "중요한 차트 기술적 지지/저항선 붕괴 및 돌파로 인한"; break;
                case "Cyber": cDesc = "해킹, 딥웹 찌라시 또는 AI 시스템 오류로 인한"; break;
                case "Regulatory": cDesc = "각국 정부의 크립토 규제 또는 세금 법안 발표로 인한"; break;
                case "Altcoin": cDesc = "주요 알트코인 네트워크 마비 및 디페깅 사태로 인한"; break;
                case "OnChain": cDesc = "온체인 데이터 상의 비정상적인 미결제약정(OI) 폭증으로 인한"; break;
                case "Community": cDesc = "대형 커뮤니티(레딧 등)의 조직적인 펌핑/덤핑 선동으로 인한"; break;
            }

            string fDesc = "";
            switch (flow)
            {
                case "Pump": fDesc = "강력한 상승(포모/빅롱) 빔 폭발 상황"; break;
                case "Crash": fDesc = "공포의 연쇄 하락(패닉셀/빅숏) 빔 폭락 상황"; break;
                case "Sideways": fDesc = "극도의 눈치싸움과 숨막히는 횡보장 상황"; break;
                case "Whipsaw": fDesc = "위아래로 크게 요동치며 청산 맵을 터는 휩소 장세 상황"; break;
            }

            return $"{t}, {r} {cDesc} {fDesc}";
        }

        private static EventLogicOptionData[] GenerateRandomOptions(string flow, string risk)
        {
            var options = new EventLogicOptionData[3];
            
            int riskMultiplier = risk == "High" ? 3 : (risk == "Medium" ? 2 : 1);
            float baseBeam = Random.Range(5f, 15f) * riskMultiplier;

            // [Option 0: Safe]
            options[0] = new EventLogicOptionData
            {
                OptionType = ChoiceOptionType.Safe,
                ForcePosition = TradingController.PositionType.None,
                MentalChangeAmount = Random.Range(5, 20),
                HealthChangeAmount = Random.Range(5, 15),
                OverrideBeamPercent = (flow == "Pump" || flow == "Crash") ? (flow == "Pump" ? baseBeam * 0.2f : -baseBeam * 0.2f) : 0f,
                OverrideDurationSeconds = 10
            };

            // [Option 1: Aggressive / Directional]
            float aggressiveBeam = (flow == "Crash" ? -baseBeam : baseBeam) * Random.Range(1.2f, 2.0f);
            options[1] = new EventLogicOptionData
            {
                OptionType = (flow == "Crash") ? ChoiceOptionType.DirectionalShort : ChoiceOptionType.DirectionalLong,
                ForcePosition = (flow == "Crash") ? TradingController.PositionType.Short : TradingController.PositionType.Long,
                ForceLeverage = risk == "High" ? Random.Range(75, 126) : Random.Range(30, 76),
                MentalChangeAmount = Random.Range(-40, -10) * riskMultiplier,
                OverrideSignalProbTrue = Random.Range(0.4f, 0.7f),
                OverrideBeamPercent = aggressiveBeam,
                OverrideDurationSeconds = 15
            };
            if (flow == "Whipsaw") options[1].OptionType = ChoiceOptionType.Aggressive; // 방향 특정 불능

            // [Option 2: SpecialItem]
            string item = Items[Random.Range(0, Items.Length)];
            options[2] = new EventLogicOptionData
            {
                OptionType = ChoiceOptionType.SpecialItem,
                RequiredItemId = item,
                RequiredItemCount = risk == "High" ? 2 : 1,
                ForcePosition = TradingController.PositionType.Long,
                ForceLeverage = Random.Range(15, 30),
                MentalChangeAmount = Random.Range(15, 40),
                HealthChangeAmount = Random.Range(10, 30),
                OverrideSignalProbTrue = 1.0f, // 100% 성공 보장
                OverrideBeamPercent = Mathf.Abs(baseBeam) * Random.Range(0.8f, 1.2f),
                OverrideDurationSeconds = 15
            };

            return options;
        }

        private static void CreateTemplate(string dir, string name, string theme, EventLogicOptionData[] options)
        {
            string path = $"{dir}/Template_{name}.asset";
            EventLogicTemplateSO so = AssetDatabase.LoadAssetAtPath<EventLogicTemplateSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<EventLogicTemplateSO>();
                AssetDatabase.CreateAsset(so, path);
            }
            so.TemplateID = name;
            so.ThemeTag = theme;
            so.LogicOptions = options;
            
            EditorUtility.SetDirty(so);
        }
    }
}
