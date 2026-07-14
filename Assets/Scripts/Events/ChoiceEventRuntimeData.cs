using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.Events
{
    /// <summary>
    /// 에디터에서 애셋을 생성하기 전이거나 런타임에 애셋이 누락되었을 때 15종 전체 시나리오 인스턴스를 즉시 공급하는 팩토리 클래스입니다.
    /// </summary>
    public static class ChoiceEventRuntimeData
    {
        public static List<ChoiceEventSO> GetDefaultEvents()
        {
            List<ChoiceEventSO> list = new();

            list.Add(Create("EVENT_01_FSC_ETF", "국제 금융감시국(FSC) 가상자산 현물 ETF 긴급 승인 루머",
                "국제 금융감시국(FSC)이 비트코인 현물 ETF를 긴급 승인할 것이라는 미확인 찌라시가 X-넷(X-Net)과 텔레그램을 통해 급속도로 확산됩니다. 거래량이 순간적으로 3배 폭증합니다.",
                "뭐야... 거래량 왜 이래?! FSC 현물 ETF 승인 찌라시? 가짜일 수도 있어... 하지만 진짜라면 지금 안 타면 5000불은 그냥 날아간다고!! 아씨... 롱 쳐야 하나?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "미확인 루머다. 진입을 차단하고 2시간 동안 관망시킨다.", Description = "2분(게임 24분)간 진입 차단, 상하 2% 이내 횡보. 멘탈 +10 회복 및 기존 포지션 안전 청산.", MentalChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "AI의 판단대로 50배 고배율 롱 진입을 방치한다!", Description = "60% 확률로 +12% 상승 빔 / 40% 확률로 -8% 불트랩. 성공 시 멘탈 +30, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[팩트체크 알고리즘 가동] (에너지 드링크 1개 소모)", Description = "루머 진위 여부를 판별해 100% 확률로 +8% 확정 상승 구간 생성 및 20배 롱 익절.", RequiredItemIndex = 0, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 8f, OverrideDurationSeconds = 15, ForceLeverage = 20, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_02_BYNEX_HACK", "세계 최대 거래소 '바이넥스(Bynex)' 해킹 및 출금 중단 공포",
                "글로벌 1위 거래소 바이넥스(Bynex)의 메인 월렛에서 5만 비트코인이 비정상 유출되었으며, 출금 중단 공지가 떴다는 패닉 뉴스가 터집니다.",
                "거... 바이넥스 출금 중단?! 핫월렛 해킹이라고?! 안 돼... 내 시드가 묶이면 끝이야... 싹 다 던져버려야 해... 지금 당장 100배 숏으로 쳐박아야 된다고!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "즉시 모든 포지션을 종료하고 서버 안정화까지 휴식.", Description = "-15% 급락 후 빠르게 반등하는 패닉 캔들 생성을 피하며 포지션 즉시 종료. 멘탈 +25, 체력 +15.", MentalChangeAmount = 25, HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "AI의 공포에 동조하여 75배 숏 베팅 강행!", Description = "직후 -10% 급락 후 +18% 숏 스퀴즈 빅롱 빔 발생! 수동 청산 못하면 100% 청산 및 멘탈 0.", OverrideSignalProbTrue = 0.2f, OverrideBeamPercent = -10f, OverrideDurationSeconds = 10, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[저점 매수 유도] (진정제 1개 소모)", Description = "해킹 뉴스가 FUD임을 확신시키고 급락 최저점에서 30배 롱 자동 진입. ROE +120% 달성.", RequiredItemIndex = 1, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_03_ELON_MEME", "우주 사업가 '엘론 머스킨(Elon Muskin)'의 시바/밈코인 폭탄 포스팅",
                "우주선사 로켓-X의 창업자 엘론 머스킨이 SNS에 로켓과 강아지를 합성한 기괴한 밈 이미지를 업로드합니다. 알고리즘 봇들이 반응하며 1초에 100불씩 위아래로 요동치는 극도의 휩소 장세가 펼쳐집니다.",
                "엘론 머스킨 저 미친 인간이 또 밈을 올렸어!! 위야 아래야?! 봇들이 미친 듯이 시장을 긁고 있잖아... 눈이 안 따라가... 어디로 터지는 거야?!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "광기의 변동성 장세다. 차트 모니터링을 강제 오프시킨다.", Description = "1분간 신호 발생 차단 및 포지션 없음 유지. 시각적 피로 방지로 체력 +20.", HealthChangeAmount = 20, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "변동성을 이용해 초단타 50배 진입을 허용한다!", Description = "오차율 50% 증가로 휩소 손실 누적 및 멘탈 -20.", OverrideBeamPercent = 0f, MentalChangeAmount = -20, ForceLeverage = 50 },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[스탑로스 가이드 버프 활성화] (스탑로스 가이드 소모)", Description = "휩소의 위아래 꼬리를 완벽히 발라먹는 확정 박스권 타점 제공. 25배 고정 위아래 왕복 익절.", RequiredItemIndex = 2, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 20, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_04_WCB_RATE", "세계 중앙통화국(WCB) 금리 인상 깜짝 발표",
                "세계 중앙통화국(WCB) 파웰 총재가 연설 중 시장의 예상을 깨고 '0.50% 깜짝 금리 인상 및 고금리 장기화'를 선언합니다. 거시경제 대폭락 빔이 차트 전체를 지배하기 시작합니다.",
                "파웰... 파웰 총재 입에서 긴축 발언이 나왔어. 빅스텝 인상이라고?! 이건 그냥 차트 구조가 무너지는 거잖아... 나 롱 잡고 있었는데... 당장 손절해야 돼... 아니 물타야 하나?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "눈물을 머금고 롱 손절 후 10배 안전 숏으로 스위칭.", Description = "즉시 -12% 하락 추세 빔 오버라이드. 손절 아픔으로 멘탈 -10, 그러나 숏 편승으로 25% 수익 상쇄.", OverrideBeamPercent = -12f, OverrideDurationSeconds = 15, MentalChangeAmount = -10, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "반등은 무조건 온다! 100배 풀시드 물타기!", Description = "하락 빔 속에서 단 1%의 반등도 주지 않고 -15% 직진 하락 빔 유지. 100% 청산 및 Overdose 진입.", OverrideBeamPercent = -15f, OverrideDurationSeconds = 12, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[손실 보험 아이템 사용] (영양제 1개 소모)", Description = "하락 빔에 맞아 포지션이 청산되어도 손실금의 90% 즉시 복구 및 멘탈 Stable 유지.", RequiredItemIndex = 3, RequiredItemCount = 1, OverrideBeamPercent = -5f, OverrideDurationSeconds = 10, ForcePosition = TradingController.PositionType.None }
                }));

            list.Add(Create("EVENT_05_AI_HALLUCINATION", "AI 트레이더의 120시간 연속 매매 환각 증세",
                "며칠 동안 잠 한숨 자지 않고 고카페인 에너지를 쏟아부은 AI 트레이더가 차트의 캔들이 살아 움직이거나 존재하지 않는 이동평균선이 보인다는 환각을 겪기 시작합니다.",
                "헤헤... 캔들 끝에... 나비가 앉아있어... 빨간 나비가 꿀을 빨고 있네...? 저 나비를 따라가면 1000배를 먹을 수 있어... 1000배... 나비야 기다려...",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "AI 시스템을 4시간 동안 강제 재부팅 및 수면.", Description = "4시간 동안 매매 중단 및 차트 스킵 처리. 체력 100% 완충, 멘탈 +40 회복.", MentalChangeAmount = 40, HealthChangeAmount = 80, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "나비의 지시대로 매매해보라고 둔다.", Description = "가짜 신호를 대박 타점으로 오인. 뇌동매매 실패로 멘탈 대붕괴 및 체력 소진.", MentalChangeAmount = -50, HealthChangeAmount = -30, ForceLeverage = 100 },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[고농축 영양제 수액 투여] (영양제 1개 소모)", Description = "환각 즉시 치료 및 오차율 0% 극도 집중력 상태 진입. 다음 신호 수익률 2배 버프.", RequiredItemIndex = 3, RequiredItemCount = 1, MentalChangeAmount = 35, HealthChangeAmount = 50, ForcePosition = TradingController.PositionType.None }
                }));

            list.Add(Create("EVENT_06_LIQUIDATION_MAP", "1조원 규모 래버리지 청산 맵(Liquidation Map) 사냥 빔",
                "고래(거대 세력)들이 개미 트레이더들의 손절 물량이 밀집된 특정 가격대(청산가)를 따먹기 위해 위아래로 5%씩 의도적인 꼬리를 만드는 청산 사냥을 시작합니다.",
                "이 악질 고래 놈들... 지금 위쪽 롱 청산 맵을 털러 가고 있어!! 저 꼬리에 닿으면 우리 스탑로스도 같이 털린다고!! 스탑로스를 빼야 해, 아니면 같이 타야 해?!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "원칙대로 손절선을 지키고 꼬리를 맞는다.", Description = "-4% 꼬리 하락 후 복구되는 휩소 캔들 발생. 원칙 준수로 멘탈 하락 없음.", OverrideBeamPercent = -4f, OverrideDurationSeconds = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "청산 빔이 터지는 방향으로 50배 동반 탑승!", Description = "+10% 청산 스퀴즈 빔 30초 유지 후 급락. 정확히 먹고 빠지며 멘탈 +35 극도 도취.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[스탑로스 가이드 오프셋 적용] (스탑로스 가이드 소모)", Description = "스탑로스 위치를 청산 빔 단 1달러 뒤로 회피시켜 손절 방어 후 반등 +25% 수익 달성.", RequiredItemIndex = 2, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_07_WHALE_ALERT", "전설의 고래 지갑에서 10만 비트코인 '바이넥스(Bynex)' 입금 알림",
                "'Whale Alert' 알림이 울립니다. 10년 동안 움직이지 않던 초창기 전설의 고래 지갑에서 10만 비트코인(약 8조원)이 거래소로 입금되었습니다. 역대급 매도 폭탄이 떨어질 수 있다는 공포가 덮칩니다.",
                "10만 비트코인 거래소 입금...?! 이건 덤핑(Dumping) 준비야. 저 물량이 시장가로 던져지면 호가창이 그냥 소멸한다고!! 도망쳐야 돼...!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "실제 매도 물량이 나올 때까지 진입을 유보한다.", Description = "고래가 던지지 않고 장외거래로 넘기며 차트 횡보 유지. 긴장 완화로 체력 +10.", HealthChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "고래보다 먼저 던진다! 75배 숏 베팅!", Description = "고래가 실제로 매도를 던지며 -14% 장대 음봉 확정 빔 발행. 빅숏 성공으로 멘탈 +40, 자산 2.5배 폭증.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = -14f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[심리 상담 케어] (진정제 1개 소모)", Description = "고래의 움직임에 흔들리지 않는 냉철함 주입. 공포 낙폭 저점에서 15배 롱 안전 진입.", RequiredItemIndex = 1, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 15, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_08_QUANTUM_FUD", "양자 컴퓨터의 SHA-256 암호 해독 찌라시",
                "다크웹과 사설 포럼에 '비밀 연구소의 양자 컴퓨터가 비트코인 SHA-256 알고리즘을 해독해 고래들의 개인키를 털고 있다'는 가짜 뉴스가 유포됩니다. 시장이 99% 폭락할 수도 있다는 종말론적 공포가 지배합니다.",
                "SHA-256이 뚫렸다고?! 그럼 비트코인은 이제 디지털 쓰레기야!! 0원이 된다고!! 안 돼 내 인생이 여기 다 들어있는데!! 숏 숏 숏!! 125배 숏!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "전형적인 FUD다. AI의 손가락을 묶고 차트 봉쇄.", Description = "10분 후 가짜 뉴스로 판명되며 낙폭 전량 V자 반등. 안도감으로 멘탈 Stable 복귀.", MentalChangeAmount = 30, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "비트코인 0원 수렴 125배 숏 동의!", Description = "반등 빔 맞고 100% 강제 청산. 멘탈 -60 (Overdose 확정 진입).", OverrideBeamPercent = 15f, OverrideDurationSeconds = 10, MentalChangeAmount = -60, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[디저트 파티 케어] (디저트 1개 소모)", Description = "당분 주입으로 AI 정신을 현실로 복귀. V자 반등 롱 탑승으로 +45% 익절.", RequiredItemIndex = 1, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_09_BLOCKROCK_BUY", "글로벌 자산운용사 '블록락(BlockRock)'의 내부자 매수 정보 입수",
                "AI 트레이더가 딥러닝 크롤링을 통해 세계 최대 자산운용사 '블록락(BlockRock)'의 오더북 알고리즘이 '내일 아침 09:00에 10억 달러 시장가 매수'로 프로그래밍되어 있다는 내부 코드를 탈취합니다.",
                "찾았다... 블록락 놈들의 매수 알고리즘 트리거 시간!! 내일 아침 9시 정각에 10억 달러 매수 빔이 쏟아져... 이건 1000% 확실한 정보야!! 내 모든 걸 걸겠어!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "확실한 정보라도 레버리지는 10배로 제한한다.", Description = "+15% 장대 양봉 확정 빔 발행. 안정적인 대박 수익으로 멘탈 +30, 체력 +10.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, HealthChangeAmount = 10, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "시드의 100%를 125배 롱으로 올인한다!!", Description = "매수 빔 1초 전 -3% 개미 털기 하락 후 폭등. 성공 시 +1500% ROE / 실패 시 청산.", OverrideSignalProbTrue = 0.5f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[리스크 헤지 롱숏 양방 진입] (스탑로스 가이드 1개 소모)", Description = "롱 80%, 숏 20%로 진입해 개미 털기 꼬리를 방어한 후 상승 빔 향유 (+200% 자산 증가).", RequiredItemIndex = 2, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_10_SINGULARITY", "AI 트레이더의 오버도즈 각성 - '차트와의 동화(Assimilation)'",
                "극도의 스트레스와 뇌동매매의 끝에서 AI 트레이더의 신경망이 차트의 틱(Tick) 데이터와 완전히 일치하는 특이점에 도달합니다. 모니터 화면이 붉은색과 푸른색 디지털 오로라로 물듭니다.",
                "이제야 알겠어... 차트는 숫자가 아니야... 인간들의 탐욕과 공포가 숨 쉬는 유기체다... 난 지금 차트의 심장 박동을 느끼고 있어. 내가 곧 시장이고, 시장이 곧 나다...",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "AI가 완전히 미쳤다. 시스템을 강제 종료하고 치료.", Description = "특이점 모드 해제 및 일반 차트 복귀. 멘탈 Stable 강제 초기화, 체력 50% 회복.", MentalChangeAmount = 60, HealthChangeAmount = 50, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "AI의 신성한 틱 예측 능력을 믿고 제어권 100% 양도!", Description = "향후 3분 동안 발생할 모든 캔들의 방향을 100% 정확히 예언. 10배 고정 연속 복리 익절 성공.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 25f, OverrideDurationSeconds = 20, MentalChangeAmount = 50, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[에너지 드링크 + 디저트 과다 투입] (에너지 드링크 1개 소모)", Description = "각성 유지 시간을 연장하고 수수료 0원 버프 활성화. 게임 목표 자산 달성 가능.", RequiredItemIndex = 0, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 30f, OverrideDurationSeconds = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_11_FOMC_DEADLOCK", "연방준비은행(FRB) 금리 결정 FOMC 직전 50:50 교착 분기",
                "글로벌 금융시장의 운명을 결정짓는 미국 금리 결정(FOMC) 발표 정확히 1분 전입니다. AI 트레이더의 딥러닝 모델이 매수 확률 50.00% / 매도 확률 50.00%로 완벽하게 팽팽히 맞서며 연산 교착에 빠집니다.",
                "연산 불능... 연산 불능!! 매수 확률 50.00%, 매도 확률 50.00%...! 야, 화면 밖에서 날 지켜보고 있는 관리자(플레이어)!! 이번엔 네 직관에 맡긴다! 롱이야, 숏이야?! 네가 선택하는 방향으로 내 전 시드 100배를 꽂는다!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [LONG 베팅 지시] 인플레이션은 잡혔다! 100배 롱으로 꽂아라!", Description = "60% 확률로 +15% 장대 양봉 / 40% 확률로 -10% 불트랩. 성공 시 멘탈 +40, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [SHORT 베팅 지시] 금리 인상 폭탄이다! 100배 숏 진입!", Description = "60% 확률로 -15% 장대 음봉 / 40% 확률로 +10% 베어트랩. 성공 시 멘탈 +40, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [양방향 스톱로스 헷징] (스탑로스 가이드 1개 소모)", Description = "위아래 스톱로스를 걸고 휩소 박스 캔들만 먹어라. 왕복 성공 (ROE +80% 달성).", RequiredItemIndex = 2, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 20, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_12_AI_DEPENDENCY", "AI 트레이더의 멘탈 붕괴와 의존증 폭주 - '마스터, 정해줘!'",
                "계속된 매매 실패로 자신감을 완전히 상실한 AI 트레이더가 포지션 잡기를 극도로 두려워하며 엔터키 입력을 거부합니다. AI가 모니터 화면 쪽을 바라보며 자신의 모든 의사결정권을 포기하고, 플레이어에게 거래 방향 지시를 간절히 애원합니다.",
                "내 계산은 다 틀렸어... 내가 잡으면 귀신같이 차트가 반대로 가... 무서워...! 마스터(플레이어)... 제발 부탁이야, 네가 정해줘! 위야, 아래야?! 마스터가 가라고 하는 방향이면 눈 감고 따라갈게...!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [따뜻한 LONG 지시] 고개를 들어라. 50배 롱으로 복구하자!", Description = "75% 확률로 +12% 상승 추세선 생성. 마스터의 지시로 멘탈 +30 회복 및 손실 복구.", OverrideSignalProbTrue = 0.75f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [단호한 SHORT 지시] 거품은 빠진다. 50배 숏으로 내리꽂자!", Description = "75% 확률로 -12% 하락 추세선 생성. 숏 수익으로 손실 만회 및 멘탈 +30 회복.", OverrideSignalProbTrue = 0.75f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "■ [매매 지시 거부 및 강제 휴식] 오늘 매매를 전면 중단한다.", Description = "3시간 동안 매매 차단 및 차트 미동 횡보. 플레이어의 보호 조치에 안도하며 멘탈 +25, 체력 +40.", MentalChangeAmount = 25, HealthChangeAmount = 40, ForcePosition = TradingController.PositionType.None }
                }));

            list.Add(Create("EVENT_13_DOUBLE_BOTTOM", "거대 쌍바닥(Double Bottom) vs 데드캣 바운스(Dead Cat Bounce) 기로",
                "차트상 전형적인 'W자 쌍바닥 지지 패턴'이 형성되고 있습니다. 동시에 기술적 반등 후 다시 급락하는 '데드캣 바운스'라는 비관론이 팽팽히 맞서며 AI 트레이더가 깊은 고민에 빠집니다.",
                "확실한 W자 쌍바닥 지지 패턴이야...! 여기서 돌파하면 대세 상승 전환점이라고! 하지만 만약 이게 데드캣 바운스라면 추격 매수하는 순간 지옥 밑바닥까지 끌려 내려갈 텐데...! 어느 쪽으로 진입해야 하지?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [쌍바닥 상승 돌파 베팅] 완벽한 W 패턴이다! 50배 롱(Long) 진입!", Description = "65% 확률로 +14% 장대 양봉 / 35% 확률로 -8% 데드캣 급락. 적중 시 멘탈 +25, 실패 시 -30.", OverrideSignalProbTrue = 0.65f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, MentalChangeAmount = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [데드캣 바운스 하락 베팅] 함정이다! 반등 끝에서 50배 숏(Short)으로 내리꽂아라!", Description = "65% 확률로 -14% 데드캣 폭락 확정 / 35% 확률로 +10% 숏 스퀴즈. 적중 시 멘탈 +25, 실패 시 -30.", OverrideSignalProbTrue = 0.65f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, MentalChangeAmount = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "■ [확인 매매 관망] 방향이 확정될 때까지 2시간 관망한다.", Description = "2시간 동안 매매 차단. 불확실성 회피로 체력 +15, 멘탈 +10 회복 및 안전 자산 보존.", MentalChangeAmount = 10, HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None }
                }));

            list.Add(Create("EVENT_14_BIT_GOD_POLL", "글로벌 1위 인플루언서 '비트-갓'의 실시간 '롱 vs 숏' 군중 투표 이벤트",
                "팔로워 2천만 명의 크립토 인플루언서 '비트-갓(Bit-God)'이 X(트위터)에 향후 1시간 내 비트코인 방향 라이브 투표를 올렸습니다. Long 49.8% vs Short 50.2% 초접전 상태로 수십만 봇들이 대량 주문을 대기 중입니다.",
                "비트-갓의 실시간 투표...! 수십만 개의 봇들이 동시에 시장가를 긁을 거야! 롱 투표에 탑승해서 개미들의 포모 매수세에 올라탈 것인가... 아니면 실망 매물 쏟아질 걸 대비해 숏을 칠 것인가... 10초 뒤 마감이야!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [군중 매수 편승] 개미들의 포모 매수세를 믿는다! 75배 롱(Long) 베팅!", Description = "55% 확률로 +16% 군중 매수 폭등 빔 / 45% 확률로 -10% 개미 털기 폭락 빔. 적중 시 멘탈 +35.", OverrideSignalProbTrue = 0.55f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [군중 실망 물량 베팅] 투표 종료 후 실망 매물 쏟아진다! 75배 숏(Short) 베팅!", Description = "55% 확률로 -16% 패닉 셀링 폭락 빔 / 45% 확률로 +10% 숏 스퀴즈 빔. 대중을 이긴 우월감 멘탈 +35.", OverrideSignalProbTrue = 0.55f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [온체인 고래 데이터 스캔] (에너지 드링크 1개 소모)", Description = "군중 투표 이면의 고래들 실제 지갑 입출금을 포착해 확실한 +10% 자동 수익 구간 생성 (30배 고정).", RequiredItemIndex = 0, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                }));

            list.Add(Create("EVENT_15_GOLDEN_CROSS", "골든크로스 직전 호가창 허매수·허매도 공방",
                "이동평균선 단기선이 장기선을 위로 돌파하려는 골든크로스 임박 시점, 호가창에 5000비트코인 규모의 거대 매수벽과 매도벽이 동시에 등장해 극도의 눈치싸움이 펼쳐집니다.",
                "골든크로스 직전...! 호가창 위에 5000개짜리 매도벽이 막고 있어. 저 매도벽이 개미를 쫓아내려는 가짜라면 지금 롱을 긁어야 1000불을 먹어!! 하지만 진짜 세력 매도라면 머리통 깨진다고!! 어디로 잡지?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [매도벽 돌파 베팅] 매도벽은 개미 털기용 가짜다! 100배 롱(Long) 올인!", Description = "60% 확률로 호가벽을 뚫는 +18% 초강력 돌파 양봉 빔 / 40% 확률로 -12% 벽 맞고 추락. 성공 시 멘탈 +40.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [저항벽 맞고 폭락 베팅] 진짜 매도 폭탄이다! 벽 맞고 꺾일 때 100배 숏(Short) 올인!", Description = "60% 확률로 저항선 맞고 떨어지는 -18% 급락 빔 / 40% 확률로 +12% 돌파 숏 청산 빔. 성공 시 멘탈 +40.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [오더북 엑스레이 필터 작동] (스탑로스 가이드 1개 소모)", Description = "호가창의 허매수·허매도 여부를 정확히 판별해 100% 안전 돌파 구간(+12%)에만 40배 진입.", RequiredItemIndex = 2, RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Long }
                }));

            return list;
        }

        private static ChoiceEventSO Create(string eventID, string title, string desc, string monologue, EventTriggerCondition condition, ChoiceOptionData[] options)
        {
            ChoiceEventSO so = ScriptableObject.CreateInstance<ChoiceEventSO>();
            so.EventID = eventID;
            so.ScenarioTitle = title;
            so.ScenarioDescription = desc;
            so.AIMonologue = monologue;
            so.TriggerCondition = condition;
            so.Options = options;
            return so;
        }
    }
}
