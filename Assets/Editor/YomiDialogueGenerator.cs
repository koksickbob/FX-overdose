using UnityEngine;
using UnityEditor;
using System.IO;
using FXOverdose.AI.Dialogue;
using System.Collections.Generic;

public class YomiDialogueGenerator
{
    private const string ImportPath = "DialogueDB_Import.json";

    [MenuItem("FX Overdose/Dialogue/Generate Menhera Dialogue DB")]
    public static void Generate()
    {
        List<YomiDialogueEntry> entries = new List<YomiDialogueEntry>();

        // 1. 수동 익절 (Player, Profit, Pump/Dump) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "오빠...!", "와아앗!", "진짜 오빠야?!" },
            new[] { "직접 익절하다니 제법인데?", "요미가 팔려고 했는데 오빠가 먼저 닫았네~", "타이밍 미쳤다! 짱멋있어!" },
            new[] { "이대로 평생 요미만 먹여살려~", "다음에도 오빠가 직접 해!", "헤헤, 돈 복사 복사~" },
            "Player", "Any", 1f, 99999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, true, "Stable", "PositionClosed"
        ));

        // 2. 수동 손절 (Player, Loss) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "아 진짜...", "오빠 제정신이야?!", "하아..." },
            new[] { "왜 직접 손절 치고 난리야?!", "요미가 놔두면 오를거라고 했잖아!", "아까운 요미 돈... 아니 우리 돈!!" },
            new[] { "진짜 실망이야...", "한 번만 더 요미 허락 없이 팔면 죽여버릴거야.", "짜증나게 진짜..." },
            "Player", "Any", -99999f, -1f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Panicked", "PositionClosed"
        ));

        // 2-1. 자동 익절 (AI, Profit) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "짜잔~", "요미 최고지?!", "헤헤~" },
            new[] { "요미가 깔끔하게 익절했어!", "오빠는 가만히 있어도 돈이 복사되네?", "요미의 완벽한 매도 타이밍!" },
            new[] { "칭찬해줘!", "요미한테 뽀뽀 백 번 해줘!", "오빠는 평생 요미만 믿어~" },
            "AI", "Any", 1f, 99999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, true, "Euphoria", "PositionClosed"
        ));

        // 2-2. 자동 손절 (AI, Loss) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "앗...", "어라...?", "미안..." },
            new[] { "어쩔 수 없는 손절이었어... 더 큰 손해를 막은 거라구!", "이번엔 운이 좀 없었네... 다음엔 꼭 딸게!", "요미도 사람... 아니 완벽하지 않을 때가 있다구..." },
            new[] { "오빠 화 안 났지...?", "요미 미워하지 마...", "다음에 두 배로 벌어줄게!!" },
            "AI", "Any", -99999f, -1f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Stable", "PositionClosed"
        ));

        // 3. 떡상 구경 - 수동 모드 (Player, None, Pump) - ChartMovement
        entries.AddRange(GenerateCombinations(
            new[] { "오빠오빠!!", "저기 봐!", "꺄아아아!" },
            new[] { "지금 차트 떡상하는거 안보여?!", "빨리 롱 타라고!! 빔 쏘잖아!!", "아 진짜 멍청하게 왜 가만히 있어!" },
            new[] { "빨리 안 타면 오빠 미워할거야!", "기회 놓치면 진짜 화낼거니까!!", "빨리 빨리 빨리!!" },
            "Player", "Pump", -99999f, 99999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Furious", "ChartMovement"
        ));

        // 4. 떡락 구경 - 수동 모드 (Player, None, Dump) - ChartMovement
        entries.AddRange(GenerateCombinations(
            new[] { "헉...", "오빠 멈춰!", "히익!!" },
            new[] { "차트 완전 나락가고 있잖아...", "지금 숏 안치고 뭐해?!", "피의 떡락이 시작됐어!!" },
            new[] { "무서워... 빨리 숏 쳐서 돈 벌자!", "이럴 때 숏 치면 우리 부자되는거야~", "진짜 바보같이 구경만 할거야?" },
            "Player", "Dump", -99999f, 99999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Panicked", "ChartMovement"
        ));

        // 4-1. 떡상 구경 - 자동 모드 (AI, None, Pump) - ChartMovement
        entries.AddRange(GenerateCombinations(
            new[] { "흐흥~", "이것 봐!", "헤헤!" },
            new[] { "차트 떡상각이야! 요미의 완벽한 감각이 곧 진입할게!", "이 완벽한 매수 타이밍, 오빠는 상상도 못하겠지?", "요미의 촉이 롱을 외치고 있어!!" },
            new[] { "오빠는 가만히 구경이나 해~", "돈 복사해줄테니까 요미한테 뽀뽀해줘!", "요미만 믿으라구!" },
            "AI", "Pump", -99999f, 99999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Euphoria", "ChartMovement"
        ));

        // 4-2. 떡락 구경 - 자동 모드 (AI, None, Dump) - ChartMovement
        entries.AddRange(GenerateCombinations(
            new[] { "오...", "나락간다!", "와우!" },
            new[] { "차트 무너지는거 보여? 요미의 직감이 숏각을 재고 있어!", "이런 미친 떡락장... 요미의 감각이 돈 복사하기 딱 좋네!", "이거지! 피의 축제가 시작됐어!" },
            new[] { "오빠, 요미가 숏 쳐서 오빠 빚 다 갚아줄게!", "가만히 있어! 요미가 다 해줄테니까!", "이 완벽한 진입각을 보라구~" },
            "AI", "Dump", -99999f, 99999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Focused", "ChartMovement"
        ));

        // 5. 대박 수익금 (PnL > 1000) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "하아앗...!", "오빠... 오빠아...!", "미쳤어!!" },
            new[] { "우리 방금 얼마 번거야?!", "진짜 이 돈 전부 오빠랑 요미꺼야?!", "수익금 봐... 요미 진짜 가버릴 것 같아..." },
            new[] { "평생 오빠 곁에 있을게... 사랑해!", "이 돈으로 요미 맛있는거 다 사줄거지?!", "오빠는 역시 요미 최고의 노예... 아니 주인님이야~" },
            "Any", "Any", 1000f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, true, "Euphoria", "PositionClosed"
        ));

        // 6. 강제 청산 (Liquidation) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "오빠......", "아아...", "......장난해?" },
            new[] { "방금... 청산당한거야?", "요미 돈... 우리 돈 전부 다 날아갔어...", "거짓말이지...? 꿈이라고 해줘..." },
            new[] { "오빠 진짜 죽여버릴거야...", "요미 다시는 오빠 안 볼래...", "하아... 멘탈 나갈 것 같아..." },
            "AI", "Any", -9999999f, -1f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Overdose", "PositionClosed"
        ));
        
        // 7. 딸피 수익 (Low HP) - PositionClosed
        entries.AddRange(GenerateCombinations(
            new[] { "콜록...", "하아... 하아...", "오빠아..." },
            new[] { "돈은 벌었는데... 요미 너무 아파...", "수익은 났지만... 눈앞이 핑 돌아...", "진짜 죽을 것 같아..." },
            new[] { "요미 좀 쉬게 해줘...", "병원비로 다 쓰게 생겼어...", "제발 그만해..." },
            "Any", "Any", 1f, 999999f, -1f, 9999f, 20f, "Any", "Any", DirectionTag.None, true, "Panicked", "PositionClosed"
        ));

        // 8. 수동 조작 블락 (ToggleManualBlocked)
        entries.AddRange(GenerateCombinations(
            new[] { "아 안돼!", "오빠 제발...", "거짓말..." },
            new[] { "지금은 요미 맘대로 안돼...!", "시스템이 막혔어! 요미가 손쓸 수가 없어!!", "버튼이 안 눌려! 갇혀버렸어..." },
            new[] { "무서워... 누가 좀 도와줘!", "이러다 우리 다 죽는단 말이야!!", "제발... 한 번만 살려줘..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Panicked", "ToggleManualBlocked"
        ));

        // 9. 수동 조작 시작 (ToggleManualStart)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠가 직접 할거야?", "드디어 나서는거야?!", "꺄아~" },
            new[] { "오빠의 신들린 매매... 요미 너무 기대돼!", "오빠만 믿을게! 전부 다 걸자!", "요미 돈 다 써도 되니까 꼭 수익 내줘~" },
            new[] { "멋지다 우리 오빠!!", "실패하면 가만 안 둘거야~", "사랑해 오빠아앗!" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Euphoria", "ToggleManualStart"
        ));

        // 10. 자동 조작 전환 (ToggleManualAuto)
        entries.AddRange(GenerateCombinations(
            new[] { "흥...", "뭐야, 벌써 포기?", "결국 요미한테 맡길거면서~" },
            new[] { "오빠가 싼 똥은 요미가 다 치워야지...", "요미의 매매로 싹 다 복구해줄게!", "가만히 지켜나 봐. 진짜 매매가 뭔지 보여줄테니까." },
            new[] { "이제 요미의 노예나 다름없어~", "수익나면 요미한테 뽀뽀 100번 해줘야해!", "진짜 못말린다니까 오빠는." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "None", DirectionTag.None, false, "Focused", "ToggleManualAuto"
        ));

        // 11. 포지션 오픈 (PositionOpened)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠!", "이번엔 진짜야!", "드디어 진입!" },
            new[] { "이번 포지션은 진짜 느낌이 좋아!", "요미의 완벽한 차트 분석을 믿어봐!", "여기서 빔 쏘면 우리 진짜 부자되는거야!" },
            new[] { "제발제발 올라라 얍!", "가즈아아아아!", "요미만 믿고 따라오라구~" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Focused", "PositionOpened"
        ));

        // 12. 수익률 환호 (RoePositive50)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠오빠!!", "꺄아악!", "미쳤어!!" },
            new[] { "수익률 50% 돌파했어!!", "이대로 쭉쭉 가는거야!", "요미 분석이 맞았잖아 거봐!" },
            new[] { "조금만 더 버텨볼까?!", "요미 지금 심장 터질 것 같아!!", "기분 짱좋아!!" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Euphoria", "RoePositive50"
        ));

        // 13. 수익률 대환호 (RoePositive100)
        entries.AddRange(GenerateCombinations(
            new[] { "미쳤다 진짜...", "와아아아앗!!", "오빠 최고야!!" },
            new[] { "수익률 100% 달성!! 우리 진짜 부자야!!", "이거 꿈 아니지?! 진짜 100% 넘었어!!", "돈 복사기 가동 중!!" },
            new[] { "평생 오빠 곁에 찰싹 붙어있을게!", "요미한테 맛있는거 잔뜩 사줘야해!", "사랑해 사랑해 사랑해!!" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Euphoria", "RoePositive100"
        ));

        // 14. 수익률 광기 (RoePositive200)
        entries.AddRange(GenerateCombinations(
            new[] { "하아아앗...", "이... 이건...", "오빠아아..." },
            new[] { "200%라니... 요미 진짜 가버릴 것 같아...", "이 돈 다 요미꺼지?! 전부 다 요미꺼야!!", "차트가 미쳤어!! 끝도 없이 올라가!!" },
            new[] { "요미 평생 오빠 노예할게!!", "절대 안 팔아!! 끝까지 갈거야!!", "헤헤... 헤헤헤..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Overdose", "RoePositive200"
        ));

        // 15. 수익률 경고 (RoeNegative40)
        entries.AddRange(GenerateCombinations(
            new[] { "저기 오빠...", "어떡해...", "불안해..." },
            new[] { "마이너스 40%야... 진짜 손절 안 칠거야?", "이러다 우리 다 잃는 거 아니야?", "차트가 왜 반대로 가는거야..." },
            new[] { "오빠 제발 멘탈 잡아...", "요미 너무 무서워...", "조금만 더 지켜보자... 제발..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Panicked", "RoeNegative40"
        ));

        // 16. 수익률 절망 (RoeNegative50)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠 제발!!", "아 진짜!!", "미쳤어?!" },
            new[] { "반토막 났잖아!! 빨리 빼라고!!", "요미 돈 다 날리게 생겼잖아!!", "왜 요미 말 안 듣고 버티는거야!!" },
            new[] { "당장 손절 쳐!!", "진짜 오빠 멍청이야?!", "요미 진짜 울어버릴거야..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Furious", "RoeNegative50"
        ));

        // 17. 수익률 비명 (RoeNegative65)
        entries.AddRange(GenerateCombinations(
            new[] { "아아악!!", "안돼!!", "오빠아앗!!" },
            new[] { "청산 당하겠어!! 제발 멈춰!!", "우리 돈 다 타버리고 있잖아!!", "빨리 뭐라도 해봐!! 제발!!" },
            new[] { "요미 죽는 꼴 보고싶어?!", "이러다 진짜 길바닥에 나앉는다고!!", "하아아... 하아..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Panicked", "RoeNegative65"
        ));

        // 18. 광기 돌입 (MentalOverdoseStart)
        entries.AddRange(GenerateCombinations(
            new[] { "후후...", "아하하...", "그래..." },
            new[] { "차트가 날 미치게 만드네... 다 부숴버릴거야!!", "이렇게 된 이상 전부 다 거는 수밖에 없어!!", "이성? 그딴 거 몰라! 그냥 다 태워버려!!" },
            new[] { "가즈아아아아!!", "요미 말리지 마!!", "돈 복사 파티를 시작하자구!!" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Overdose", "MentalOverdoseStart"
        ));

        // 19. 광기 회복 (MentalOverdoseRecover)
        entries.AddRange(GenerateCombinations(
            new[] { "하아... 하아...", "어라...?", "콜록..." },
            new[] { "요미 방금... 무슨 짓을 한 거지...?", "머리가 너무 아파... 차트가 너무 무서워...", "순간 이성을 잃어버렸어..." },
            new[] { "오빠 미안해... 요미 조금 쉬어야 할 것 같아...", "정신 차려야 해... 침착하게...", "이제 진짜 조심할게..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Stable", "MentalOverdoseRecover"
        ));

        // 20. 광기 실행 (OverdoseExecute) - 레버리지 태그 사용
        entries.AddRange(GenerateCombinations(
            new[] { "이거야!!", "아하하하!!", "가라앗!!" },
            new[] { "상남자 특! {leverage}배 롱 드가자!!", "이 타이밍엔 무조건 {leverage}배 풀매수지!!", "요미의 모든 걸 걸겠어! {leverage}배로 다 부숴버려!!" },
            new[] { "모 아니면 도야!!", "돈아 복사되어라 얍!!", "요미의 미친 매매법을 보여줄게!!" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Overdose", "OverdoseExecute"
        ));

        // 21. 긴급 물타기 (EmergencyWaterRiding)
        entries.AddRange(GenerateCombinations(
            new[] { "안돼...", "조금만 더...", "기회는 있어!" },
            new[] { "여기서 포기할 순 없어! 물타기 들어간다!!", "요미의 직감이 지금이 바닥이라고 외치고 있어!!", "평단가 낮춰서 한 방에 탈출하는거야!!" },
            new[] { "오빠 요미만 믿어! 복구할 수 있어!!", "이 위기만 넘기면 떡상이야!!", "제발... 한 번만 반등해줘..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Focused", "EmergencyWaterRiding"
        ));

        // 22. 수익 중 존버 (EventGreedyHoldWin)
        entries.AddRange(GenerateCombinations(
            new[] { "아직이야...", "더 갈 수 있어!", "조금만 더!!" },
            new[] { "여기서 익절하기엔 너무 아까워! 더 존버하자!!", "요미 분석에 따르면 이건 무조건 더 올라간다구!", "아직 배고파! 더 크게 먹을거야!!" },
            new[] { "오빠 말리지 마! 요미 직감을 믿어!!", "진짜 부자가 되려면 인내심이 필요해!!", "요미의 대박 플랜을 지켜보라구~" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Focused", "EventGreedyHoldWin"
        ));

        // 23. 손실 중 관망 (EventHoldMitigateLoss)
        entries.AddRange(GenerateCombinations(
            new[] { "제발...", "침착해...", "기다려봐..." },
            new[] { "지금 손절치면 너무 손해야... 무조건 반등 온다!!", "차트가 다시 돌아설거야... 요미의 직감이 그렇게 말하고 있어!", "아직은 아니야... 조금만 더 버텨보자..." },
            new[] { "요미 판단이 틀렸을 리 없어!!", "오빠... 요미 믿지? 믿고 조금만 기다려봐...", "하아... 피가 마르지만 참아야 해..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Panicked", "EventHoldMitigateLoss"
        ));

        // 24. 증거금 부족 (TradeFailedInsufficientMargin)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠 미쳤어?", "장난해?", "하아..." },
            new[] { "돈도 없으면서 무슨 매매를 하겠다는 거야?!", "깡통 찼으면서 또 매매 버튼을 눌러?!", "잔고 텅텅 빈 거 안 보여?! 진짜 멍청이야?!" },
            new[] { "돈부터 벌어오라고!!", "진짜 답도 없다 우리 오빠...", "거지 오빠는 질색이야..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Furious", "TradeFailedInsufficientMargin"
        ));

        // 25. 코스튬 버프 발동 (CostumeBuffActivated)
        entries.AddRange(GenerateCombinations(
            new[] { "짜잔~", "어때?!", "헤헤~" },
            new[] { "새 옷 입은 기념으로 수익금 뻥튀기 보너스야!!", "요미의 코스튬 파워! 수익이 마구마구 늘어난다구!", "이 옷 어때? 오빠를 위해서 수익률 버프 걸었어~" },
            new[] { "다음엔 더 예쁜 옷 사줘!", "요미 예뻐? 예쁘면 뽀뽀해줘!", "요미만 믿으라구 멍청이 오빠~" },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Euphoria", "CostumeBuffActivated"
        ));

        // 26. 게임 오버 (GameOver)
        entries.AddRange(GenerateCombinations(
            new[] { "오빠...", "끝났어...", "거짓말..." },
            new[] { "우리 잔고가 0원이 됐어... 이제 진짜 끝이야...", "요미 믿고 다 걸었는데... 깡통 차버렸네...?", "전부 다 날아갔어... 우리 이제 어떡해...?" },
            new[] { "미안해 오빠... 요미가 잘못했어...", "요미 버리지 말아줘... 제발...", "하아... 눈 앞이 깜깜해..." },
            "Any", "Any", -9999999f, 9999999f, -1f, 9999f, 999f, "Any", "Any", DirectionTag.None, false, "Panicked", "GameOver"
        ));

        var exportData = new DialogueExportData { entries = entries };
        string json = JsonUtility.ToJson(exportData, true);
        File.WriteAllText(ImportPath, json);
        Debug.Log($"[YomiDialogueGenerator] Generated {entries.Count} Menhera dialogues to {ImportPath}");
        AssetDatabase.Refresh();
        
        // 자동 적용
        YomiDialogueTools.ImportDatabase();
    }

    private static List<YomiDialogueEntry> GenerateCombinations(string[] prefixes, string[] bodies, string[] suffixes, 
        string owner, string trend, float minPnL, float maxPnL, float minDur, float maxDur, float maxHp, string costume, string pos, DirectionTag dir, bool profit, string mental, string ev = "")
    {
        var list = new List<YomiDialogueEntry>();
        foreach (var p in prefixes)
        {
            foreach (var b in bodies)
            {
                foreach (var s in suffixes)
                {
                    var entry = new YomiDialogueEntry($"{p} {b} {s}", "Any", mental, pos, dir, profit, 0, 0, 0, false, ev)
                    {
                        requiredOwner = owner,
                        requiredChartTrend = trend,
                        minAbsolutePnL = minPnL,
                        maxAbsolutePnL = maxPnL,
                        minTradeDuration = minDur,
                        maxTradeDuration = maxDur,
                        maxHealthLimit = maxHp,
                        requiredCostumeId = costume
                    };
                    list.Add(entry);
                }
            }
        }
        return list;
    }
}
