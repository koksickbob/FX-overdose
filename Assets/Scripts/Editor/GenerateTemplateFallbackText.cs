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
                "· 전체 덮어쓰기: 손으로 다듬은 문구도 초안으로 되돌립니다",
                "빈 항목만 채우기", "취소", "전체 덮어쓰기");

            if (choice == 1) return; // 취소
            bool overwriteAll = (choice == 2);

            int filled = 0, skipped = 0, unparsable = 0;
            var report = new StringBuilder();

            foreach (var t in templates)
            {
                if (t == null) continue;

                if (!overwriteAll && t.HasFallbackText)
                {
                    skipped++;
                    continue;
                }

                if (!TryParseTemplateId(t.TemplateID, out string cat, out string flow, out string risk, out string time))
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
            report.AppendLine();
            report.AppendLine("⚠️ 새 한국어 문구가 자산에 추가되었습니다.");
            report.AppendLine("   글자가 □로 보이면 Tools/Prebake All Scripts Text into Font 를 재실행하십시오.");
            Debug.Log(report.ToString());

            EditorUtility.DisplayDialog("완료",
                $"{filled}개 템플릿에 사전 텍스트를 채웠습니다.\n({skipped}개는 기존 문구 유지)", "확인");
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
