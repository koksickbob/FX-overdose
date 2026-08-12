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

        /// <summary>이벤트 쉴드 기본 지속 시간(실시간 초). ChoiceEventController.DefaultEventShieldSeconds와 같아야 합니다.</summary>
        private const int EventShieldSeconds = 150;

        /// <summary>
        /// ⚠️ 이 메뉴는 기존 자산을 <b>덮어씁니다.</b>
        ///
        /// 현재 Resources/Events/Templates의 242개 자산은 이 코드와 다른 버전으로 생성되어 있습니다.
        /// 실측: 베팅 선택지의 OverrideSignalProbTrue가 자산은 0.002~0.399인데 아래 코드는 0.4~0.7입니다.
        /// 즉 재실행하면 성공 확률이 평균 0.2에서 0.55로 뛰어 밸런스가 통째로 바뀝니다.
        ///
        /// 기존 자산에 C3/C4 규격만 반영하려면 이 메뉴가 아니라
        /// <c>Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)</c> 를 쓰십시오.
        /// </summary>
        [MenuItem("Tools/FX OVERDOSE/Generate 240 Event Templates (전체 재생성 · 밸런스 변경 주의)")]
        public static void GenerateTemplates()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "템플릿 전체 재생성",
                "기존 242개 템플릿 자산을 덮어씁니다.\n\n" +
                "⚠️ 현재 자산의 베팅 성공 확률은 0.002~0.399이지만 이 생성기는 0.4~0.7로 만듭니다.\n" +
                "재생성하면 난이도가 크게 낮아집니다.\n\n" +
                "기존 값을 지키면서 C3/C4 규격만 맞추려면\n" +
                "'Migrate Event Templates (C3+C4)' 메뉴를 사용하십시오.\n\n" +
                "그래도 전체 재생성을 진행할까요?",
                "재생성", "취소");
            if (!proceed) return;

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
                OptionTitle = "안전하게 포지션을 종료하고 관망한다.",
                OptionDescription = "시장의 불확실성을 피하여 잠시 휴식하며 멘탈과 체력을 회복합니다.",
                ForcePosition = TradingController.PositionType.None,
                // Safe는 성패 판정을 받지 않으므로 보상이 무조건 적용됩니다. 페널티 필드는 쓰이지 않습니다.
                MentalChangeAmount = Random.Range(5, 20),
                HealthChangeAmount = Random.Range(5, 15),
                OverrideBeamPercent = (flow == "Pump" || flow == "Crash") ? (flow == "Pump" ? baseBeam * 0.2f : -baseBeam * 0.2f) : 0f,
                OverrideDurationSeconds = EventShieldSeconds
            };

            // [Option 1: Aggressive / Directional]
            float aggressiveBeam = (flow == "Crash" ? -baseBeam : baseBeam) * Random.Range(1.2f, 2.0f);
            var optType = (flow == "Crash") ? ChoiceOptionType.DirectionalShort : ChoiceOptionType.DirectionalLong;
            string optTitle = optType == ChoiceOptionType.DirectionalLong ? "상승에 모든 것을 걸고 롱(Long) 베팅!" : "하락에 모든 것을 걸고 숏(Short) 베팅!";
            if (flow == "Whipsaw") 
            {
                optType = ChoiceOptionType.Aggressive;
                optTitle = "시장의 변동성에 공격적으로 몸을 맡긴다!";
            }

            options[1] = new EventLogicOptionData
            {
                OptionType = optType,
                OptionTitle = optTitle,
                OptionDescription = "시장의 방향성에 공격적으로 베팅하여 큰 수익을 노리거나 큰 손실을 감수합니다.",
                ForcePosition = (flow == "Crash") ? TradingController.PositionType.Short : TradingController.PositionType.Long,
                ForceLeverage = risk == "High" ? Random.Range(75, 126) : Random.Range(30, 76),
                // C4: 베팅 선택지는 성공하면 보상, 실패하면 페널티를 받습니다.
                //     과거에는 음수 한 개만 있어서 "성공하면 멘탈 폭락, 실패하면 무사"가 됐습니다.
                MentalChangeAmount = Random.Range(5, 13) * riskMultiplier,
                MentalPenaltyOnFail = Random.Range(-40, -10) * riskMultiplier,
                OverrideSignalProbTrue = Random.Range(0.4f, 0.7f),
                OverrideBeamPercent = aggressiveBeam,
                OverrideDurationSeconds = EventShieldSeconds
            };

            // [Option 2: SpecialItem]
            string item = Items[Random.Range(0, Items.Length)];
            options[2] = new EventLogicOptionData
            {
                OptionType = ChoiceOptionType.SpecialItem,
                OptionTitle = "[아이템 사용] 특수 대응 알고리즘 가동",
                OptionDescription = "보유한 아이템을 사용하여 위기를 기회로 바꾸고 확정적인 수익을 창출합니다.",
                RequiredItemId = item,
                RequiredItemCount = risk == "High" ? 2 : 1,
                ForcePosition = TradingController.PositionType.Long,
                ForceLeverage = Random.Range(15, 30),
                MentalChangeAmount = Random.Range(15, 40),
                HealthChangeAmount = Random.Range(10, 30),
                OverrideSignalProbTrue = 1.0f, // 100% 성공 보장 → 보상만 적용되고 페널티 필드는 쓰이지 않음
                OverrideBeamPercent = Mathf.Abs(baseBeam) * Random.Range(0.8f, 1.2f),
                OverrideDurationSeconds = EventShieldSeconds
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
