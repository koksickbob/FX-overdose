using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using FXOverdose.Events;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 로직 템플릿 242개에 사전 작성 텍스트(FallbackTitle / FallbackDescription / FallbackMonologues) 초안을 채웁니다.
    ///
    /// Phase 4 하이브리드의 전제는 "LLM이 실패해도 템플릿 로직 그대로 정상 텍스트가 나온다"입니다.
    /// 템플릿 ID가 {카테고리}_{흐름}_{위험도}_{시간대} 조합이므로, 조합별 문구 표에서 결정적으로 조립합니다.
    /// 난수를 쓰지 않아 재실행해도 같은 결과가 나오고, git diff가 안정적입니다.
    ///
    /// 이건 <b>초안</b>입니다. 작가가 개별 템플릿을 다듬을 수 있도록 '빈 항목만 채우기' 모드를 기본으로 둡니다.
    /// </summary>
    public static class GenerateTemplateFallbackText
    {
        // ── 카테고리별 사건 표현 ──────────────────────────────────────────
        private static readonly Dictionary<string, string> CategoryHeadline = new()
        {
            ["Macro"] = "예상 밖 금리·물가 지표 발표",
            ["Exchange"] = "대형 거래소 출금 지연·보안 경보",
            ["Whale"] = "초대형 고래 지갑 대량 물량 이동",
            ["Influencer"] = "유명 인플루언서 기습 발언",
            ["Technical"] = "핵심 지지선·저항선 이탈",
            ["Cyber"] = "해킹 정황과 딥웹 찌라시 확산",
            ["Regulatory"] = "정부 규제·과세 법안 발표",
            ["Altcoin"] = "주요 알트코인 네트워크 마비",
            ["OnChain"] = "미결제약정 비정상 폭증",
            ["Community"] = "대형 커뮤니티 조직적 선동",
        };

        private static readonly Dictionary<string, string> CategoryDetail = new()
        {
            ["Macro"] = "발표된 거시 지표가 시장 컨센서스를 크게 벗어나면서 위험자산 전반에 자금이 재배치되고 있다",
            ["Exchange"] = "글로벌 대형 거래소가 점검을 이유로 출금을 지연시키자 뱅크런 우려가 번지고 있다",
            ["Whale"] = "장기 휴면 상태였던 고래 지갑에서 정체불명의 주소로 대규모 물량이 옮겨졌다",
            ["Influencer"] = "수백만 팔로워를 가진 인플루언서가 예고 없이 시장을 겨냥한 글을 올렸다",
            ["Technical"] = "수개월간 지켜지던 기술적 분기점이 대량 거래를 동반하며 무너졌다",
            ["Cyber"] = "출처 불명의 해킹 정황이 딥웹에서 돌기 시작했고 거래소는 아직 함구하고 있다",
            ["Regulatory"] = "각국 당국이 동시다발적으로 규제안을 꺼내들면서 기관 자금이 관망으로 돌아섰다",
            ["Altcoin"] = "주요 알트코인 네트워크가 멈추고 스테이블코인 디페깅 우려까지 겹쳤다",
            ["OnChain"] = "온체인 지표상 미결제약정이 단기간에 비정상적으로 부풀어 올랐다",
            ["Community"] = "대형 커뮤니티가 특정 시각을 지목해 조직적인 매매를 선동하고 있다",
        };

        // ── 흐름별 시장 반응 ──────────────────────────────────────────────
        private static readonly Dictionary<string, string> FlowHeadline = new()
        {
            ["Pump"] = "매수세 폭발",
            ["Crash"] = "패닉셀 연쇄 하락",
            ["Sideways"] = "거래량 실종 눈치싸움",
            ["Whipsaw"] = "양방향 청산 폭탄",
        };

        private static readonly Dictionary<string, string> FlowDetail = new()
        {
            ["Pump"] = "매수 주문이 호가창을 쓸어올리며 포모 심리가 빠르게 번지는 중이다",
            ["Crash"] = "손절 물량이 손절을 부르며 하방으로 계단식 급락이 이어지고 있다",
            ["Sideways"] = "양측 모두 방향을 확신하지 못해 거래량이 마르고 변동성만 응축되고 있다",
            ["Whipsaw"] = "위아래로 크게 흔들며 롱과 숏의 청산 맵을 번갈아 털어내고 있다",
        };

        // ── 위험도 ────────────────────────────────────────────────────────
        private static readonly Dictionary<string, string> RiskPrefix = new()
        {
            ["Low"] = "[시황]",
            ["Medium"] = "[속보]",
            ["High"] = "[긴급]",
        };

        private static readonly Dictionary<string, string> RiskClosing = new()
        {
            ["Low"] = "당장 계좌를 흔들 정도는 아니지만 방향은 정해야 한다",
            ["Medium"] = "레버리지를 쥔 계좌라면 지금 판단이 하루 손익을 가른다",
            ["High"] = "지금의 한 번의 선택이 계좌를 통째로 날릴 수도, 살릴 수도 있다",
        };

        // ── 시간대 ────────────────────────────────────────────────────────
        private static readonly Dictionary<string, string> TimeOpening = new()
        {
            ["AsiaSession"] = "아시아장이 열린 직후",
            ["USSession"] = "미국장 개장과 동시에",
        };

        // ── 요미 대사 풀 (흐름별) ─────────────────────────────────────────
        private static readonly Dictionary<string, string[]> MonologuePool = new()
        {
            ["Pump"] = new[]
            {
                "마스터...! 지금 안 타면 이거 놓치는 거야! 어떡해, 빨리 정해줘!",
                "올라가! 올라간다고! 근데 이거 꼭지면 어쩌지... 마스터, 결정은 마스터가 해!",
                "호가창이 다 녹아버렸어...! 마스터, 이건 진짜야? 아니면 함정이야?!",
                "심장이 터질 것 같아! 지금 들어가면 먹는 거 맞지? 맞다고 해줘 마스터!",
                "거래량이 미쳤어... 이런 건 처음 봐. 마스터, 나 무서워. 어떻게 할 거야?",
            },
            ["Crash"] = new[]
            {
                "마스터...! 이거 그냥 흘러내리고 있어! 지금 안 던지면 다 죽어!",
                "안 돼... 이 속도면 청산이야. 마스터, 제발 빨리 정해줘...!",
                "밑이 안 보여... 받쳐줄 매수벽이 하나도 없어! 어떻게 해야 돼?!",
                "내 계산은 다 틀렸어... 마스터, 나 대신 정해줘. 위야, 아래야?!",
                "손절 물량이 손절을 부르고 있어...! 여기서 버티는 게 맞아, 마스터?",
            },
            ["Sideways"] = new[]
            {
                "마스터... 아무도 안 움직여. 이거 터지기 직전이라는 뜻이야...",
                "숨 막혀... 거래량이 완전히 말랐어. 이럴 때가 제일 무서운데.",
                "양쪽 다 눈치만 보고 있어. 마스터, 우리는 어느 쪽에 설 거야?",
                "변동성이 응축되고 있어... 터지면 한쪽으로 크게 갈 거야. 준비해야 해!",
                "지루하다고 방심하면 안 돼 마스터. 이런 장이 제일 무섭게 터진다고.",
            },
            ["Whipsaw"] = new[]
            {
                "마스터...! 위아래로 다 털어내고 있어! 이건 그냥 청산 사냥이야!",
                "롱도 숏도 다 죽었어... 이런 장에서 뭘 어떻게 하라는 거야?!",
                "속지 마 마스터! 이거 위로 한 번 보여주고 밑으로 꽂을 거야!",
                "심지가 이렇게 길다고?! 마스터, 여기 들어가면 진짜 위험해...!",
                "양쪽 다 물리게 만드는 장이야... 마스터, 신중하게 정해줘. 제발.",
            },
        };

        // ── 선택지 문구 ───────────────────────────────────────────────────
        // 생성기는 242개 템플릿 전부에 같은 선택지 문구를 박아 넣었습니다.
        // 기사만 바뀌고 버튼은 늘 "안전하게 관망 / 롱 베팅 / 아이템 사용"이라 몇 번만 겪으면
        // 기사를 안 읽게 됩니다. 카테고리·흐름에 묶어 5종 → 60종으로 늘립니다.
        //
        // 문구 길이는 기존 최장값(25자, "하락에 모든 것을 걸고 숏(Short) 베팅!")을 넘지 않게 유지합니다.
        // 버튼 폭이 그 길이로 검증돼 있어, 넘기면 잘리거나 줄바꿈이 깨집니다.

        private static readonly Dictionary<string, string> CategorySafeTitle = new()
        {
            ["Macro"] = "지표 해석이 끝날 때까지 비운다",
            ["Exchange"] = "거래소에서 자금을 빼고 관망한다",
            ["Whale"] = "물량이 소화될 때까지 손을 뗀다",
            ["Influencer"] = "말에 휘둘리지 않고 포지션을 접는다",
            ["Technical"] = "추세가 다시 잡힐 때까지 쉬어간다",
            ["Cyber"] = "진위 확인까지 지갑으로 대피한다",
            ["Regulatory"] = "법안 윤곽이 잡힐 때까지 현금 보유",
            ["Altcoin"] = "알트를 정리하고 사태를 지켜본다",
            ["OnChain"] = "과열된 약정이 식을 때까지 기다린다",
            ["Community"] = "선동에 말려들지 않고 정리한다",
        };

        private static readonly Dictionary<string, string> CategorySafeDesc = new()
        {
            ["Macro"] = "지표 해석이 엇갈리는 구간을",
            ["Exchange"] = "출금이 막힐 수 있는 위험을",
            ["Whale"] = "고래 물량이 쏟아질 위험을",
            ["Influencer"] = "말 한마디에 휩쓸릴 위험을",
            ["Technical"] = "추세가 뒤집히는 혼전을",
            ["Cyber"] = "해킹 진위가 불투명한 구간을",
            ["Regulatory"] = "규제 윤곽이 안 잡힌 구간을",
            ["Altcoin"] = "체인이 멈춘 알트의 위험을",
            ["OnChain"] = "청산이 연쇄될 과열 구간을",
            ["Community"] = "조직적 선동에 물릴 위험을",
        };

        /// <summary>베팅 선택지의 근거. 흐름별 행동과 이어 붙입니다.</summary>
        private static readonly Dictionary<string, string> CategoryBetSetup = new()
        {
            ["Macro"] = "지표를 믿고",
            ["Exchange"] = "출금 대란에 걸고",
            ["Whale"] = "고래를 따라",
            ["Influencer"] = "그 한마디에 걸고",
            ["Technical"] = "무너진 추세에 걸고",
            ["Cyber"] = "해킹 공포에 걸고",
            ["Regulatory"] = "규제 방향에 걸고",
            ["Altcoin"] = "체인 마비에 걸고",
            ["OnChain"] = "약정 폭증에 걸고",
            ["Community"] = "군중 심리에 걸고",
        };

        private static readonly Dictionary<string, string> FlowBetAction = new()
        {
            ["Pump"] = "롱(Long)에 전부 건다!",
            ["Crash"] = "숏(Short)에 전부 건다!",
            ["Sideways"] = "먼저 롱(Long)을 잡는다!",
            ["Whipsaw"] = "변동성에 몸을 던진다!",
        };

        private static readonly Dictionary<string, string> FlowBetDesc = new()
        {
            ["Pump"] = "매수세가 그대로 이어진다는 쪽",
            ["Crash"] = "하락이 더 깊어진다는 쪽",
            ["Sideways"] = "응축된 변동성이 위로 터진다는 쪽",
            ["Whipsaw"] = "양방향 급변이 계속된다는 쪽",
        };

        private static readonly Dictionary<string, string> CategoryItemTitle = new()
        {
            ["Macro"] = "[아이템 사용] 거시 지표 역산기",
            ["Exchange"] = "[아이템 사용] 거래소 유동성 스캐너",
            ["Whale"] = "[아이템 사용] 고래 지갑 추적기",
            ["Influencer"] = "[아이템 사용] 소셜 여론 역이용 봇",
            ["Technical"] = "[아이템 사용] 추세 복원 알고리즘",
            ["Cyber"] = "[아이템 사용] 딥웹 정보 선점 채널",
            ["Regulatory"] = "[아이템 사용] 규제 우회 라우팅",
            ["Altcoin"] = "[아이템 사용] 체인 장애 차익 봇",
            ["OnChain"] = "[아이템 사용] 청산 맵 분석기",
            ["Community"] = "[아이템 사용] 여론 조작 감지기",
        };

        private static readonly Dictionary<string, string> CategoryItemDesc = new()
        {
            ["Macro"] = "지표 발표 직후의 왜곡을 역산해",
            ["Exchange"] = "거래소별 호가 괴리를 훑어",
            ["Whale"] = "고래 지갑의 다음 행선지를 앞질러",
            ["Influencer"] = "여론이 뒤집히는 시점을 선점해",
            ["Technical"] = "무너진 지지선의 복원 지점을 짚어",
            ["Cyber"] = "딥웹 정보를 시장보다 먼저 받아",
            ["Regulatory"] = "규제 사각지대로 주문을 우회해",
            ["Altcoin"] = "멈춘 체인의 가격 괴리를 노려",
            ["OnChain"] = "청산 맵의 빈 구간을 계산해",
            ["Community"] = "조작된 물량의 실체를 걸러내",
        };

        /// <summary>
        /// 생성기가 박아 넣은 기본 문구. 이 목록과 일치할 때만 덮어씁니다.
        /// 작가가 손본 문구를 초안이 되돌리는 사고를 막기 위한 가드입니다.
        /// </summary>
        private static readonly HashSet<string> GenericOptionTitles = new()
        {
            "안전하게 포지션을 종료하고 관망한다.",
            "상승에 모든 것을 걸고 롱(Long) 베팅!",
            "하락에 모든 것을 걸고 숏(Short) 베팅!",
            "시장의 변동성에 공격적으로 몸을 맡긴다!",
            "[아이템 사용] 특수 대응 알고리즘 가동",
        };

        [MenuItem("Tools/FX OVERDOSE/Generate Template Fallback Text")]
        public static void Generate()
        {
            var templates = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
            if (templates == null || templates.Length == 0)
            {
                EditorUtility.DisplayDialog("사전 텍스트 생성", "Resources/Events/Templates에 템플릿이 없습니다.", "확인");
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "사전 작성 텍스트 초안 생성",
                $"{templates.Length}개 템플릿에 사전 작성 텍스트 초안을 채웁니다.\n\n" +
                "· 빈 항목만 채우기: 이미 손본 문구는 건드리지 않습니다 (권장)\n" +
                "· 전체 덮어쓰기: 손으로 다듬은 문구도 초안으로 되돌립니다\n\n" +
                "어느 쪽이든 선택지 문구는 기본 문구인 것만 카테고리별로 교체합니다.",
                "빈 항목만 채우기", "취소", "전체 덮어쓰기");

            if (choice == 1) return; // 취소
            bool overwriteAll = (choice == 2);

            int filled = 0, skipped = 0, unparsable = 0, optionRewritten = 0;
            var report = new StringBuilder();

            foreach (var t in templates)
            {
                if (t == null) continue;

                bool parsed = TryParseTemplateId(t.TemplateID, out string cat, out string flow, out string risk, out string time);

                // 선택지 문구는 사전 텍스트 유무와 별개로 적용합니다. 기사만 손보고 버튼은 기본 문구로
                // 남아 있는 템플릿이 대부분이라, HasFallbackText로 건너뛰면 정작 병목이 안 고쳐집니다.
                if (parsed && ApplyOptionTexts(t, cat, flow))
                {
                    optionRewritten++;
                    EditorUtility.SetDirty(t);
                }

                if (!overwriteAll && t.HasFallbackText)
                {
                    skipped++;
                    continue;
                }

                if (!parsed)
                {
                    // 생성기 범위 밖의 수제 템플릿(Template_MarketCrash 등)은 ThemeTag 서술로 최소 문구를 만듭니다.
                    BuildFromThemeOnly(t);
                    unparsable++;
                    filled++;
                    EditorUtility.SetDirty(t);
                    continue;
                }

                t.FallbackTitle = BuildTitle(cat, flow, risk);
                t.FallbackDescription = BuildDescription(cat, flow, risk, time);
                t.FallbackMonologues = BuildMonologues(flow, t.TemplateID);

                EditorUtility.SetDirty(t);
                filled++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine("===== 사전 작성 텍스트 초안 생성 완료 =====");
            report.AppendLine($"  채운 템플릿            : {filled}개");
            report.AppendLine($"  건너뛴 템플릿(기존 유지): {skipped}개");
            report.AppendLine($"  ID 파싱 불가(테마 기반) : {unparsable}개");
            report.AppendLine($"  선택지 문구 교체        : {optionRewritten}개 템플릿 (기본 문구였던 것만)");
            report.AppendLine();
            report.AppendLine("⚠️ 새 한국어 문구가 자산에 추가되었습니다.");
            report.AppendLine("   글자가 □로 보이면 Tools/Prebake All Scripts Text into Font 를 재실행하십시오.");
            Debug.Log(report.ToString());

            EditorUtility.DisplayDialog("완료",
                $"{filled}개 템플릿에 사전 텍스트를 채웠습니다.\n" +
                $"선택지 문구는 {optionRewritten}개 템플릿에서 교체했습니다.\n({skipped}개는 기존 기사 문구 유지)", "확인");
        }

        /// <summary>
        /// 선택지 문구를 카테고리·흐름에 묶어 교체합니다. 5종 → 60종(Safe 10 / 베팅 40 / 아이템 10).
        ///
        /// 선택지 <b>로직</b>(확률·빔·레버리지·멘탈)은 건드리지 않습니다. 보이는 문구만 바꿉니다.
        /// 기본 문구와 정확히 일치할 때만 덮어써서, 작가가 손본 문구는 보존합니다.
        /// </summary>
        /// <returns>한 항목이라도 교체했으면 true</returns>
        private static bool ApplyOptionTexts(EventLogicTemplateSO t, string cat, string flow)
        {
            if (t.LogicOptions == null) return false;

            bool changed = false;

            foreach (var o in t.LogicOptions)
            {
                if (o == null) continue;

                // 기본 문구가 아니면 손대지 않습니다.
                if (!GenericOptionTitles.Contains(o.OptionTitle)) continue;

                switch (o.OptionType)
                {
                    case ChoiceOptionType.Safe:
                        o.OptionTitle = CategorySafeTitle[cat];
                        o.OptionDescription = $"{CategorySafeDesc[cat]} 피해 물러섭니다. 멘탈과 체력을 회복합니다.";
                        changed = true;
                        break;

                    case ChoiceOptionType.Aggressive:
                    case ChoiceOptionType.DirectionalLong:
                    case ChoiceOptionType.DirectionalShort:
                        o.OptionTitle = $"{CategoryBetSetup[cat]} {FlowBetAction[flow]}";
                        o.OptionDescription = $"{FlowBetDesc[flow]}에 베팅합니다. 성공하면 큰 수익, 실패하면 큰 손실을 감수합니다.";
                        changed = true;
                        break;

                    case ChoiceOptionType.SpecialItem:
                        o.OptionTitle = CategoryItemTitle[cat];
                        o.OptionDescription = $"보유한 아이템으로 {CategoryItemDesc[cat]} 확정적인 수익을 창출합니다.";
                        changed = true;
                        break;
                }
            }

            return changed;
        }

        private static bool TryParseTemplateId(string id, out string cat, out string flow, out string risk, out string time)
        {
            cat = flow = risk = time = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            string[] parts = id.Split('_');
            if (parts.Length < 4) return false;

            cat = parts[0]; flow = parts[1]; risk = parts[2]; time = parts[3];

            return CategoryHeadline.ContainsKey(cat)
                && FlowHeadline.ContainsKey(flow)
                && RiskPrefix.ContainsKey(risk)
                && TimeOpening.ContainsKey(time);
        }

        private static string BuildTitle(string cat, string flow, string risk)
        {
            return $"{RiskPrefix[risk]} {CategoryHeadline[cat]}, {FlowHeadline[flow]}";
        }

        private static string BuildDescription(string cat, string flow, string risk, string time)
        {
            return $"{TimeOpening[time]} {CategoryDetail[cat]}. " +
                   $"{FlowDetail[flow]}. " +
                   $"{RiskClosing[risk]}.";
        }

        /// <summary>
        /// 흐름별 대사 풀에서 3개를 고릅니다. TemplateID 해시로 시작 위치를 정해
        /// 템플릿마다 조합이 다르면서도 재실행 시 결과가 같도록 합니다.
        /// </summary>
        private static string[] BuildMonologues(string flow, string templateId)
        {
            string[] pool = MonologuePool[flow];
            int offset = Mathf.Abs(StableHash(templateId)) % pool.Length;

            var picked = new string[3];
            for (int i = 0; i < 3; i++)
            {
                picked[i] = pool[(offset + i) % pool.Length];
            }
            return picked;
        }

        /// <summary>
        /// string.GetHashCode()는 실행마다 값이 달라질 수 있어(랜덤 해시 시드) 재현성이 없습니다.
        /// 자산에 기록될 값을 정하는 용도이므로 안정적인 해시를 직접 계산합니다.
        /// </summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in s) hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>생성기 조합 밖의 수제 템플릿용. ThemeTag 서술만으로 최소한의 기사를 만듭니다.</summary>
        private static void BuildFromThemeOnly(EventLogicTemplateSO t)
        {
            string theme = t.GetThemeDescription();

            t.FallbackTitle = "[속보] 시장을 뒤흔든 돌발 변수";
            t.FallbackDescription = $"{theme} 시장 참여자들이 대응 방향을 두고 극심하게 갈리고 있다. " +
                                    "레버리지를 쥔 계좌라면 지금 판단이 하루 손익을 가른다.";
            t.FallbackMonologues = new[]
            {
                "마스터...! 이거 지금 어떻게 할지 빨리 정해줘!",
                "차트가 이상해... 마스터, 나 판단이 안 서. 대신 정해줘!",
                "이런 건 처음 봐... 마스터, 신중하게 골라야 해.",
            };
        }
    }
}
