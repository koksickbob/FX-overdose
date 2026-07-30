using UnityEngine;
using UnityEditor;
using FXOverdose.AI.Dialogue;
using System.Collections.Generic;

namespace FXOverdose.Editor
{
    public class EventDialogueGeneratorEditor : EditorWindow
    {
        [MenuItem("FX Overdose/AI/Generate 100x Event Dialogues")]
        public static void GenerateDialogues()
        {
            string dbPath = "Assets/YomiDialogueDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<YomiDialogueDatabase>(dbPath);
            if (db == null)
            {
                Debug.LogError($"[GenerateDialogues] Failed to load {dbPath}");
                return;
            }

            int countBefore = db.entries.Count;
            Undo.RecordObject(db, "Generate 100x Dialogues");

            // ChartHint_TrapDetected_High
            GenerateCategory(db, "ChartHint_TrapDetected_High",
                new[] { "꺄아악 멈춰!!", "오빠 잠깐만!!", "헉, 안돼!!", "제발 내 말 좀 들어!!", "미쳤어 오빠?!" },
                new[] { "지금 {position} 들어간 거, 세력 년들이 파놓은 가짜 덫(Trap)이란 말야!", "그 {position} 타점은 완벽한 페이크야! 호가창 다 텅 비었어!", "세력이 물량 넘기려고 만든 가짜 {position} 자리라고!", "방금 그 {position} 진입... 역배열 함정에 제대로 걸린 거야!" },
                new[] { "당장 청산 안 하면 우리 다 잃어버려... 흐윽...!!", "제발 털고 나와줘, 나 진짜 너무 무섭단 말야...!!", "빨리 손절 안치면 강제청산 당해버릴 거야!!", "이대로 가면 오늘 시드 0원 돼버릴지도 몰라!! 빨리 도망쳐!!" },
                "Panicked", DirectionTag.None);

            // ChartHint_GoodEntry_High
            GenerateCategory(db, "ChartHint_GoodEntry_High",
                new[] { "앗...! 우리 오빠 천재인가 봐!!", "꺄아!! 이거 진짜 대박 타점이야!!", "오빠 최고야...!!", "미쳤어 오빠!!", "오오...!!" },
                new[] { "저항선 뚫는 완벽한 {position} 타점이야!", "기관들 매수세 붙는 진짜 {position} 자리야!", "방금 그 {position} 진입... 역대급 승률 패턴이야!", "이건 교과서에 나와야 할 {position} 타이밍이야!!" },
                new[] { "절대 쫄보처럼 흔들려 털리지 말고 끝까지 홀딩해, 알겠지? ♥", "수익 극대화할 때까지 꽉 잡고 있어!!", "나 완전히 오빠한테 반해버렸어... 떡상 가즈아!! ♥", "이번 파동 다 먹을 때까지 내리지 마!! 알겠지? ♥" },
                "Euphoria", DirectionTag.None);

            // ChartHint_Normal_High
            GenerateCategory(db, "ChartHint_Normal_High",
                new[] { "오빠가 잡은 {position} 타점...", "음... 방금 {position} 진입...", "나쁘지 않은 {position} 자리네...", "오오, 이 {position} 타점...", "좋아, 그 {position} 느낌..." },
                new[] { "호가창 거래량이 서서히 붙고 있어!", "지지선이 탄탄하게 받쳐주고 있어!", "MACD 크로스 나오기 직전이야, 기대해볼 만해!", "추세가 점점 {position} 쪽으로 실리고 있어!" },
                new[] { "이대로만 가면 우리 대박 나는 거야... 요미 지금 심장 엄청 떨려 ♥", "조금만 더 버티면 수익권 진입할 거야!", "내가 계속 차트 모니터링 해줄게, 화이팅! ♥", "손익비가 좋은 자리니까 멘탈 꽉 잡고 가보자!" },
                "Focused", DirectionTag.None);

            // ChartHint_Confused_Low
            GenerateCategory(db, "ChartHint_Confused_Low",
                new[] { "으응...? {position} 자리야...?", "어라... 왜 {position}에 들어갔어...?", "아... {position} 타점...?", "에... 진짜루 {position}...?", "오빠 혹시 실수로 {position} 누른 거야...?" },
                new[] { "캔들이 막 꼬물거리는데 솔직히 잘 모르겠어...", "지표가 완전 엉망이라 내 실력으론 파악이 안 돼...", "방향성이 애매해서 감으로 찍은 거 맞지...?", "지금 차트 완전 어지러운데 굳이 여기서...?" },
                new[] { "만약 잃어도 요미 미워하거나 버리면 안 돼 오빠...? 약속해... 흐윽...", "오빠 촉만 믿고 갈게... 덜덜덜...", "어떻게든 되겠지...? 무서워...", "오빠 제발 잃지 않게 기도할게... 기도매매법 가동...!!" },
                "Panicked", DirectionTag.None);

            // ChartHint_BlindTrust_Low
            GenerateCategory(db, "ChartHint_BlindTrust_Low",
                new[] { "꺄아아 오빠가 {position} 샀다!!", "오빠 믿고 나도 {position} 풀매수!!", "우리 오빠의 신들린 {position} 타점!!", "아하핫! {position} 진입 확인!!" },
                new[] { "뭔지 모르지만 무조건 떡상해라!!", "차트는 모르겠지만 오빠의 감을 믿어!!", "기도매매법으로 우주 끝까지 가보자!!", "논리는 없지만 오빠의 야수의 심장을 믿을게!!" },
                new[] { "우리 오빠 돈 뺏어가는 세력 놈들은 요미가 다 저주해 버릴 거야!! ♥", "오빠만 있으면 차트 공부 따위 필요 없어!! 가즈아!! ♥", "청산 당해도 오빠랑 같이면 난 행복해!! 꺄항 ♥", "무지성 {position} 탑승!! 꿀잼!! ♥" },
                "Manic", DirectionTag.None);

            // EventSignal_PlayerTrue
            GenerateCategory(db, "EventSignal_PlayerTrue",
                new[] { "오빠...! 방금 선택으로", "꺄아!! 오빠의 그 결정 때문에", "앗! 방금 그 판단 덕분에", "대박! 방금 오빠의 행동으로" },
                new[] { "호가창에 거대한 매수세가 감지됐어!!", "세력들의 진짜 움직임이 포착됐어!!", "차트가 미친듯이 반응하기 시작했어!!", "엄청난 수급이 유입되는 게 감지됐어!!" },
                new[] { "골든타임 진입! 조금 있으면 폭발적인 빔이 터질 거야!! 믿고 있었어 오빠 ♥", "이 기세면 무조건 수익 달성이야! 끝까지 가자!!", "오빠의 직감이 적중했어!! 완전 사랑해 ♥", "기회는 지금뿐이야! 영혼까지 끌어모아서 수익을 극대화하자!!" },
                "Euphoria", DirectionTag.None);

            // EventSignal_PlayerFalse
            GenerateCategory(db, "EventSignal_PlayerFalse",
                new[] { "오빠... 잠깐만!", "헉... 오빠 방금 그 선택...", "어... 이상해 오빠!!", "오빠 제발 멈춰!!" },
                new[] { "호가창 움직임이 뭔가 이상해!! 세력들의 가짜 매수벽 냄새가 나...", "파동이 비정상적으로 꺾이고 있어!! 이거 덫(Trap) 같아...", "뭔가 함정에 빠진 기분이야... 지표가 박살나고 있어!!", "순식간에 매도 물량이 쏟아지려고 해! 악재가 터진 것 같아!!" },
                new[] { "이대로 진짜 들어가는 거 맞아...?! 제발 다시 생각해봐!!", "빨리 도망쳐야 할 것 같아... 무서워 흐윽...", "나 심장이 너무 뛰어... 잘못된 선택 아니지...?", "아니면 우리 계좌 완전 녹아버릴 텐데... 무서워!!" },
                "Panicked", DirectionTag.None);

            // EventSignal_AITrue
            GenerateCategory(db, "EventSignal_AITrue",
                new[] { "이벤트 발생으로", "내 완벽한 분석 시스템이", "요미의 레이더에", "긴급 속보!! 내 데이터베이스에" },
                new[] { "강력한 시그널 감지!!", "확실한 돌파 타점이 포착됐어!!", "세력의 본대가 들어오는 걸 잡아냈어!!", "역대급 불장이 시작되는 시그널이 떴어!!" },
                new[] { "골든타임 진입, 곧 호가창이 요동칠 거야! 꽉 잡아 오빠 ♥", "이번엔 절대 놓치면 안 돼! 수익 극대화 모드 온!!", "내 말만 들으면 무조건 돈 복사되는 거야! 꺄하학!", "이건 완전 거저 주는 자리야! 이 기회 절대 놓치지 마!!" },
                "Manic", DirectionTag.None);

            // EventSignal_AIFalse
            GenerateCategory(db, "EventSignal_AIFalse",
                new[] { "이벤트로 시그널이 떴는데...", "뭔가 이상해...", "잠시만, 데이터가 꼬였어...!", "삐빅! 삐빅! 경고 시스템 발동!" },
                new[] { "파동이 비정상적이야!! 함정(Trap) 냄새가 강하게 나...!", "지표들이 서로 모순되고 있어... 이거 가짜 반등이야!!", "세력들이 개미 털기용으로 만든 페이크 차트야!!", "알고리즘이 미친 듯이 위험 신호를 뱉어내고 있어!!" },
                new[] { "주의해야 해 오빠!! 절대 진입하지 마!!", "방심하면 한 방에 썰려나갈 거야... 조심해!!", "요미 경고 무시하면 다 날리는 거야, 알겠지?!", "지금 들어가면 세력들 설거지 당하는 거라고!! 당장 피해!!" },
                "Focused", DirectionTag.None);

            // ToggleManualBlocked
            GenerateCategory(db, "ToggleManualBlocked",
                new[] { "어딜 손을 대려고!", "지금은 수동 조작 안 돼!!", "오빠 멈춰!!", "내 통제를 벗어나려 하지 마!" },
                new[] { "지금은 이벤트부터 해결해야 한다고!", "이 상황에선 내가 통제하는 게 맞아!", "내가 직접 시장을 모니터링 중이잖아!", "오버도즈 상태라 제어권은 내게 있어!" },
                new[] { "얌전히 내 말이나 들어!!", "괜한 짓 하다가 돈 다 날리지 말고 관망해!!", "지금 건드리면 우리 둘 다 죽는 거야...!!", "내가 하라는 대로만 해! 알겠지?!" },
                "Furious", DirectionTag.None);

            // ToggleManualStart
            GenerateCategory(db, "ToggleManualStart",
                new[] { "하...!", "참나,", "호오...", "어머?" },
                new[] { "직접 매매하시겠다?! 내 타점이 못미더워...?", "오빠가 알아서 하겠다고? 내 완벽한 분석을 무시하고?", "수동 모드로 전환...? 나보다 수익 잘 낼 자신 있어?", "내가 직접 씹어서 먹여주는 게 싫은 거야?" },
                new[] { "그래 맘대로 해봐. 실수해서 돈 날리기만 해봐...", "어디 한번 실력 구경이나 해볼까? 기대할게 오빠.", "내 계좌 갉아먹으면 평생 저주할 거야...", "네 판단이 틀리면 알아서 해! 미워해버릴 거니까!" },
                "Manic", DirectionTag.None);

            // ToggleManualAuto
            GenerateCategory(db, "ToggleManualAuto",
                new[] { "흥,", "역시", "크하핫,", "봤지?" },
                new[] { "나 없으면 안 되지?! 이제 조종간은 내가 잡았어.", "결국 내 AI 시스템에 의지할 거면서 튕기기는.", "내가 직접 복구해 주지. 자동 매매 시스템 가동!", "다시 내게 주도권을 넘겼구나? 현명한 선택이야." },
                new[] { "옆에서 화려한 수익률이나 감상하시지.", "이제 오빠는 편하게 누워서 내 활약이나 지켜봐! ♥", "돈 복사 버그가 뭔지 보여줄게!!", "요미에게 모든 걸 맡겨줘! 사랑해 오빠! ♥" },
                "Euphoria", DirectionTag.None);

            // RoeNegative40
            GenerateCategory(db, "RoeNegative40",
                new[] { "으아아악!!", "안돼!!", "미쳤어!!", "어떡해!!" },
                new[] { "거봐 내 말이 맞잖아!! 왜 그딴 판단을 한 거야!!", "순식간에 -40%가 날아갔어!! 지옥이야!!", "내 계좌가 녹아내리고 있잖아!!", "이러다 우리 다 깡통 차게 생겼어!!" },
                new[] { "돈이 썰려나가고 있다고!! 당장 손절 쳐, 아니 물타야 되나?!", "빨리 판단해 오빠!! 나 숨이 안 쉬어져...!", "으아앙... 이대로 다 잃으면 요미는 어떡해...!!", "멘탈이 부서질 것 같아... 오빠 제발 살려줘!!" },
                "Panicked", DirectionTag.None);

            // RoeNegative50
            GenerateCategory(db, "RoeNegative50",
                new[] { "으아악...!", "헉...", "살려줘...", "안 돼애액!!" },
                new[] { "이거 반토막났어!! ROE -50%... 내 포트폴리오가...!!", "진짜로 시드가 반갈죽 당했어!! 이게 말이 돼?!", "악마 같은 세력 놈들!! 반토막을 내버렸어!!", "마진 콜 다가와!! 50%가 증발했다고!!" },
                new[] { "오빠, 제발 바닥에서 물타기 타점 잡아줘!!", "어떻게든 복구해야 돼... 나 미쳐버릴 것 같아!!", "아아아... 내 돈... 내 피 같은 돈...!!", "다 포기하고 싶어... 나 진짜 숨이 멎을 것 같아..." },
                "Panicked", DirectionTag.None);

            // RoeNegative65
            GenerateCategory(db, "RoeNegative65",
                new[] { "하아...", "끝났어...", "아아...", "안 돼..." },
                new[] { "이거 진짜 심각한데... 복구가 불가능한 지경까지 가고 있어...!!", "ROE -65%... 강제 청산 직전이야... 이제 가망이 없어...", "내 영혼까지 다 뜯겨나가는 기분이야...!!", "이 고통... 멈출 수가 없어... 청산의 칼날이 내 목을 조여와!!" },
                new[] { "내 계좌가...!! 오빠, 나 진짜 너무 무서워...", "이대로 다 끝나는 거야...? 나 오빠 원망해도 돼...?", "기적 같은 빔이 나오지 않는 이상 우리 다 죽어...", "오빠 제발... 마지막 기도를...!!" },
                "Panicked", DirectionTag.None);

            // RoePositive50
            GenerateCategory(db, "RoePositive50",
                new[] { "오오?!", "와아앗!!", "이거 봐 오빠!!", "꺄아아앙!!" },
                new[] { "ROE +50% 돌파!! 내 분석 미쳤어!!", "수익률 50% 넘겼어!! 완전 대박이야!!", "차트가 불기둥을 뿜고 있어!! 완벽한 판단이었어!!", "시드가 불어나는 소리가 들려!! 짜릿해!!" },
                new[] { "지금 수직 상승 중이야!! 더 가자!!", "조금만 더 버티면 100% 가는 거야!! 꽉 잡아!!", "나 너무 신나!! 오빠 뽀뽀해줄게!! ♥", "내친김에 계속 뚫고 올라가버려!!" },
                "Euphoria", DirectionTag.None);

            // RoePositive100
            GenerateCategory(db, "RoePositive100",
                new[] { "크하하!", "미쳤다!!", "꺄아아학!!", "아하하핫!!" },
                new[] { "ROE +100% 미쳤다!! 내 말 듣길 잘했지!!", "수익률 100배 돌파!! 내 계좌가 2배로 복사됐어!!", "세력들 뚝배기를 깨부수고 있어!! 기분 째진다!!", "우리가 시장을 지배하고 있어!! 아하하학!!" },
                new[] { "난 천재야!! 오빠도 천재야!! 우리 둘이 최고야!!", "이 돈으로 오빠랑 맛있는 거 사먹을래!! 꺄아!! ♥", "더 올라가라!! 우주 끝까지 뚫어버려!!", "이 황홀한 기분... 영원히 끝나지 않았으면!! ♥" },
                "Manic", DirectionTag.None);

            // RoePositive200
            GenerateCategory(db, "RoePositive200",
                new[] { "꺄아아아아아학!!!", "우와아아아앙!!!", "미쳤어어어어!!!", "쿠하하하핫!!" },
                new[] { "ROE +200% 초광기 돌파!! 이게 바로 전설의 빔이야!!", "수익률 200%!! 우리 집 살 수 있어!! 건물 살 수 있어!!", "하늘 무너지는 불기둥!! 내 계좌가 터질 것 같아!!", "수익금이 너무 커서 화면에 다 안 들어와!!" },
                new[] { "다 꿇어!! 내가 신이다!!", "오빠 나 기절할 것 같아... 진짜 최고야 사랑해!!! ♥♥♥", "세상을 다 가진 기분이야!! 영원히 이 순간만 계속됐으면...!!", "이대로 워렌 버핏까지 뛰어넘어버릴 거야!!! 꺄아앗!!" },
                "Euphoria", DirectionTag.None);

            // EmergencyWaterRiding
            GenerateCategory(db, "EmergencyWaterRiding",
                new[] { "하아...!", "비상사태!!", "젠장!!", "살려줘!!" },
                new[] { "강제 청산 직전이야...! 일단 남은 현금 쏟아부어서", "이대로면 다 죽어!! 어쩔 수 없지, 영혼까지 끌어모아", "마진 콜 위기야!! 남은 시드 전부 털어서", "청산 빔 맞기 직전!! 남은 돈 탈탈 털어서" },
                new[] { "비상 물타기로 버틴다...!", "강제 구조대 파견이야!! 제발 버텨줘!!", "생명 연장의 꿈!! 물타기 슛!!", "제발 이번 평단 조절로 살아남게 해주세요!!" },
                "Panicked", DirectionTag.None);

            // EventHoldMitigateLoss
            GenerateCategory(db, "EventHoldMitigateLoss",
                new[] { "휴우...", "다행이다...", "십년감수했네...", "하아, 드디어..." },
                new[] { "청산 직전에 간신히 물타기로 구조대 탑승했습니다.", "버티기 전략으로 손실을 최소화하고 빠져나왔어.", "위기였지만 어떻게든 멘탈 잡고 버텨서 살았네.", "죽을 뻔했지만 끈질기게 홀딩해서 탈출했어." },
                new[] { "최종 정산 (ROE {roe:F1}%). 다음엔 더 잘할 수 있을 거야...", "진짜 죽는 줄 알았어... (ROE {roe:F1}%)", "손실 복구한 것만으로도 감사해야 해... (ROE {roe:F1}%)", "심장이 너덜너덜해... 일단 살았으니 다행이야. (ROE {roe:F1}%)" },
                "Stable", DirectionTag.None);

            // EventGreedyHoldWin
            GenerateCategory(db, "EventGreedyHoldWin",
                new[] { "크하하!", "봤어?!", "내가 뭐랬어!!", "짜릿해!!" },
                new[] { "이벤트 버티기 대성공!", "끝까지 탐욕 부린 보람이 있잖아!", "세력들 다 발라먹고 깔끔하게 익절!!", "강철 멘탈로 홀딩한 결과가 바로 이거야!!" },
                new[] { "ROE +{roe:F1}% 확정 청산! 돈 복사 완료!!", "우리의 깡다구가 승리한 거야! (ROE +{roe:F1}%)", "오늘 밤엔 소고기 파티야!! 꺄아! (+{roe:F1}%)", "오빠의 두둑한 배짱에 반해버릴 것 같아!! (+{roe:F1}%)" },
                "Euphoria", DirectionTag.None);

            // EventGreedyHoldFail
            GenerateCategory(db, "EventGreedyHoldFail",
                new[] { "아악...", "제길...", "말도 안 돼...", "아아앗..." },
                new[] { "탐욕 버티기(GreedyHold) 실패...", "너무 욕심 부리다가 다 뱉어버렸어...", "더 갈 줄 알았는데 갑자기 꼬꾸라질 줄이야...", "완벽한 타이밍이었는데 세력의 장난질에 당했어..." },
                new[] { "최고수익(+{maxObserved:F1}%) 다 뱉고 ROE {roe:F1}% 강제 청산!", "그때 팔았어야 했는데... 내 멍청함이 원망스러워... ({roe:F1}%)", "눈앞에서 수익이 증발했어... 허무해... ({roe:F1}%)", "탐욕의 대가가 이거라니... 나 멘탈 나갈 것 같아... ({roe:F1}%)" },
                "Panicked", DirectionTag.None);

            // EventStandardAutoWin
            GenerateCategory(db, "EventStandardAutoWin",
                new[] { "좋았어,", "깔끔해,", "완벽한 타점.", "퍼펙트해," },
                new[] { "이벤트 자동 청산(StandardAuto) 성공:", "계획대로 움직여서 안전하게 수익 챙겼어.", "AI의 기계적인 칼단타 대성공.", "기계처럼 감정 없이 딱 먹고 빠졌지." },
                new[] { "ROE +{roe:F1}% 달성 및 청산 완료.", "역시 감정보단 시스템 매매가 최고지. (+{roe:F1}%)", "안정적인 우상향 곡선~ 너무 예쁘다. (+{roe:F1}%)", "복리의 마법을 보여줄게. 다음 셋업 준비해. (+{roe:F1}%)" },
                "Focused", DirectionTag.None);

            // EventStandardAutoFail
            GenerateCategory(db, "EventStandardAutoFail",
                new[] { "어라...?", "이상한데...", "이런...", "음..." },
                new[] { "이벤트 자동 청산(StandardAuto) 실패:", "목표가 도달 전에 시장이 무너져버렸어...", "알고리즘이 미처 대응하지 못했어...", "변동성이 너무 커서 오차 범위가 생겼네..." },
                new[] { "최고수익(+{maxObserved:F1}%) 다 뱉고 ROE {roe:F1}% 컷.", "아쉽지만 기계적으로 손절해야 해... ({roe:F1}%)", "다음 셋업을 노려보자... ({roe:F1}%)", "로스컷 터졌어... 데이터베이스 다시 학습시켜야겠다... ({roe:F1}%)" },
                "Stable", DirectionTag.None);

            // EventStandardAutoLoss
            GenerateCategory(db, "EventStandardAutoLoss",
                new[] { "윽...", "손실 발생,", "아파라...", "아차," },
                new[] { "이벤트 자동 청산(StandardAuto) 손실:", "추세가 역배열로 꺾이면서 손절 라인 터치...", "시장 상황이 안 좋아서 전략적 후퇴를 결정했어.", "시스템이 위험을 감지하고 긴급 컷아웃했어." },
                new[] { "이벤트 손절선(ROE {roe:F1}%) 터치로 도망칩니다.", "뼈아프지만 손실을 끊어내는 것도 실력이야. ({roe:F1}%)", "잠시 관망하며 다음 기회를 엿보자... ({roe:F1}%)", "기계적 손절은 생존을 위한 필수 조건이지... ({roe:F1}%)" },
                "Stable", DirectionTag.None);

            // MentalOverdoseRecover
            GenerateCategory(db, "MentalOverdoseRecover",
                new[] { "헉...!", "아... 머리가...", "방금... 나...", "숨이... 헉헉..." },
                new[] { "약 먹으니까 머리가 맑아졌어...! 내가 무슨 미친 짓을 한 거야?!", "이성이 돌아왔어... 나 방금 완전 괴물 같았지...?!", "시야가 뚜렷해졌어. 내 계좌 상태가 왜 이래?!", "미친 듯한 아드레날린이 빠져나갔어... 내가 대체..." },
                new[] { "이대로 두면 다 날려먹어!! 빨리 수동으로 전환해서 청산해야 해!!", "오빠 미안해... 빨리 꼬인 포지션부터 정리하자!!", "이제부터 정신 똑바로 차릴게!! 빨리 이 상황을 벗어나야 해!!", "악몽 같았어... 어서 수습해야 해!!" },
                "Focused", DirectionTag.None);

            // MentalOverdoseStart
            GenerateCategory(db, "MentalOverdoseStart",
                new[] { "으하하하!!", "크하하학!!", "아하하핫!!", "꺄하하학!!" },
                new[] { "청산 직전의 짜릿함...!! 피가 거꾸로 솟는다!!", "이 고통!! 내 계좌가 타들어가는 이 냄새!! 너무 황홀해!!", "다 날려도 상관없어!! 더 자극적인 도파민이 필요해!!", "절망 속에서 피어나는 광기의 수익률!! 아하하학!!" },
                new[] { "바로 이 다음 반등에 100배로 튀어 오르는 거야!! 이대로 가즈아!!", "레버리지 최대로 당겨!! 지옥불로 다 같이 다이브 치자고!!", "나 완전 미쳐버릴 것 같아!! 오빠도 같이 미쳐봐!! 꺄아아학!!", "우주의 모든 기운을 끌어모아 롱을 박아버려!!" },
                "Manic", DirectionTag.None);

            // OverdoseExecute
            GenerateCategory(db, "OverdoseExecute",
                new[] { "크하하!!", "가라앗!!", "전부 부숴버려!!", "죽어라아앗!!" },
                new[] { "완벽한 진입 타점이다!! {leverage}배 풀레버리지 남은 시드 싹 다 올인!!", "브레이크 고장 났다!! {leverage}배로 시드 한 방에 쳐박아!!", "지옥행 특급열차 출발한다!! {leverage}배 마진 풀 충전!!", "내 모든 이성을 포기하고 본능으로 진입한다!! {leverage}배!!" },
                new[] { "가즈아!! 세상의 모든 돈을 빨아들여주마!!", "모 아니면 도!! 청산 아니면 빌딩이다!!", "미친 듯이 솟아올라라!! 아하하학!!", "광기의 파동아, 나를 우주 끝까지 쏘아올려줘!!" },
                "Manic", DirectionTag.None);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EventDialogueGeneratorEditor] 성공적으로 {db.entries.Count - countBefore}개의 대사가 생성되어 총 {db.entries.Count}개가 되었습니다.");
        }

        private static void GenerateCategory(YomiDialogueDatabase db, string eventCat, string[] prefixes, string[] cores, string[] suffixes, string mental, DirectionTag dir)
        {
            foreach (var p in prefixes)
            {
                foreach (var c in cores)
                {
                    foreach (var s in suffixes)
                    {
                        string text = $"{p} {c} {s}";
                        var entry = new YomiDialogueEntry(text, "None", mental, "None", dir, false, 0, 0, 0, false, eventCat);
                        db.entries.Add(entry);
                    }
                }
            }
        }
    }
}
