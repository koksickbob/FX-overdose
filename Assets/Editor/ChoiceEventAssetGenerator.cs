#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using FXOverdose.Events;
using FXOverdose.Trading;

namespace FXOverdose.Editor
{
    public static class ChoiceEventAssetGenerator
    {
        private const string SAVE_DIR = "Assets/Resources/Events";

        [MenuItem("Tools/FX OVERDOSE/Generate 15 Choice Event Assets")]
        public static void GenerateAllEventAssets()
        {
            if (!Directory.Exists(SAVE_DIR))
            {
                Directory.CreateDirectory(SAVE_DIR);
            }

            int count = 0;
            count += CreateOrUpdate("EVENT_01_FSC_ETF", "국제 금융감시국(FSC) 가상자산 현물 ETF 긴급 승인 루머",
                "국제 금융감시국(FSC)이 비트코인 현물 ETF를 긴급 승인할 것이라는 미확인 찌라시가 X-넷(X-Net)과 텔레그램을 통해 급속도로 확산됩니다. 거래량이 순간적으로 3배 폭증합니다.",
                "뭐야... 거래량 왜 이래?! FSC 현물 ETF 승인 찌라시? 가짜일 수도 있어... 하지만 진짜라면 지금 안 타면 5000불은 그냥 날아간다고!! 아씨... 롱 쳐야 하나?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "미확인 루머다. 진입을 차단하고 2시간 동안 관망시킨다.", Description = "2분(게임 24분)간 진입 차단, 상하 2% 이내 횡보. 멘탈 +10 회복 및 기존 포지션 안전 청산.", MentalChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "요미의 판단대로 50배 고배율 롱 진입을 방치한다!", Description = "60% 확률로 +12% 상승 빔 / 40% 확률로 -8% 불트랩. 성공 시 멘탈 +30, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[팩트체크 알고리즘 가동] (에너지 드링크 1개 소모)", Description = "루머 진위 여부를 판별해 100% 확률로 +8% 확정 상승 구간 생성 및 20배 롱 익절.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 8f, OverrideDurationSeconds = 15, ForceLeverage = 20, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_02_BYNEX_HACK", "세계 최대 거래소 '바이넥스(Bynex)' 해킹 및 출금 중단 공포",
                "글로벌 1위 거래소 바이넥스(Bynex)의 메인 월렛에서 5만 비트코인이 비정상 유출되었으며, 출금 중단 공지가 떴다는 패닉 뉴스가 터집니다.",
                "거... 바이넥스 출금 중단?! 핫월렛 해킹이라고?! 안 돼... 내 시드가 묶이면 끝이야... 싹 다 던져버려야 해... 지금 당장 100배 숏으로 쳐박아야 된다고!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "즉시 모든 포지션을 종료하고 서버 안정화까지 휴식.", Description = "-15% 급락 후 빠르게 반등하는 패닉 캔들 생성을 피하며 포지션 즉시 종료. 멘탈 +25, 체력 +15.", MentalChangeAmount = 25, HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "요미의 공포에 동조하여 75배 숏 베팅 강행!", Description = "직후 -10% 급락 후 +18% 숏 스퀴즈 빅롱 빔 발생! 수동 청산 못하면 100% 청산 및 멘탈 0.", OverrideSignalProbTrue = 0.2f, OverrideBeamPercent = -10f, OverrideDurationSeconds = 10, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[저점 매수 유도] (진정제 1개 소모)", Description = "해킹 뉴스가 FUD임을 확신시키고 급락 최저점에서 30배 롱 자동 진입. ROE +120% 달성.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_03_ELON_MEME", "우주 사업가 '엘론 머스킨(Elon Muskin)'의 시바/밈코인 폭탄 포스팅",
                "우주선사 로켓-X의 창업자 엘론 머스킨이 SNS에 로켓과 강아지를 합성한 기괴한 밈 이미지를 업로드합니다. 알고리즘 봇들이 반응하며 1초에 100불씩 위아래로 요동치는 극도의 휩소 장세가 펼쳐집니다.",
                "엘론 머스킨 저 미친 인간이 또 밈을 올렸어!! 위야 아래야?! 봇들이 미친 듯이 시장을 긁고 있잖아... 눈이 안 따라가... 어디로 터지는 거야?!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "광기의 변동성 장세다. 차트 모니터링을 강제 오프시킨다.", Description = "1분간 신호 발생 차단 및 포지션 없음 유지. 시각적 피로 방지로 체력 +20.", HealthChangeAmount = 20, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "변동성을 이용해 초단타 50배 진입을 허용한다!", Description = "오차율 50% 증가로 휩소 손실 누적 및 멘탈 -20.", OverrideBeamPercent = 0f, MentalChangeAmount = -20, ForceLeverage = 50 },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[침착한 관망] (신경안정제 1개 소모)", Description = "휩소의 위아래 꼬리를 완벽히 발라먹는 확정 박스권 타점 제공. 25배 고정 위아래 왕복 익절.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 20, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_04_WCB_RATE", "세계 중앙통화국(WCB) 금리 인상 깜짝 발표",
                "세계 중앙통화국(WCB) 파웰 총재가 연설 중 시장의 예상을 깨고 '0.50% 깜짝 금리 인상 및 고금리 장기화'를 선언합니다. 거시경제 대폭락 빔이 차트 전체를 지배하기 시작합니다.",
                "파웰... 파웰 총재 입에서 긴축 발언이 나왔어. 빅스텝 인상이라고?! 이건 그냥 차트 구조가 무너지는 거잖아... 나 롱 잡고 있었는데... 당장 손절해야 돼... 아니 물타야 하나?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "눈물을 머금고 롱 손절 후 10배 안전 숏으로 스위칭.", Description = "즉시 -12% 하락 추세 빔 오버라이드. 손절 아픔으로 멘탈 -10, 그러나 숏 편승으로 25% 수익 상쇄.", OverrideBeamPercent = -12f, OverrideDurationSeconds = 15, MentalChangeAmount = -10, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "반등은 무조건 온다! 100배 풀시드 물타기!", Description = "하락 빔 속에서 단 1%의 반등도 주지 않고 -15% 직진 하락 빔 유지. 100% 청산 및 Overdose 진입.", OverrideBeamPercent = -15f, OverrideDurationSeconds = 12, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[손실 보험 아이템 사용] (영양제 1개 소모)", Description = "하락 빔에 맞아 포지션이 청산되어도 손실금의 90% 즉시 복구 및 멘탈 Stable 유지.", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideBeamPercent = -5f, OverrideDurationSeconds = 10, ForcePosition = TradingController.PositionType.None }
                });

            count += CreateOrUpdate("EVENT_05_AI_HALLUCINATION", "요미의 120시간 연속 매매 환각 증세",
                "며칠 동안 잠 한숨 자지 않고 고카페인 에너지를 쏟아부은 요미가 차트의 캔들이 살아 움직이거나 존재하지 않는 이동평균선이 보인다는 환각을 겪기 시작합니다.",
                "헤헤... 캔들 끝에... 나비가 앉아있어... 빨간 나비가 꿀을 빨고 있네...? 저 나비를 따라가면 1000배를 먹을 수 있어... 1000배... 나비야 기다려...",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "요미를 4시간 동안 강제로 눕혀 재운다.", Description = "4시간 동안 매매 중단 및 차트 스킵 처리. 체력 100% 완충, 멘탈 +40 회복.", MentalChangeAmount = 40, HealthChangeAmount = 80, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "나비의 지시대로 매매해보라고 둔다.", Description = "가짜 신호를 대박 타점으로 오인. 뇌동매매 실패로 멘탈 대붕괴 및 체력 소진.", MentalChangeAmount = -50, HealthChangeAmount = -30, ForceLeverage = 100 },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[고농축 영양제 수액 투여] (영양제 1개 소모)", Description = "환각 즉시 치료 및 오차율 0% 극도 집중력 상태 진입. 다음 신호 수익률 2배 버프.", RequiredItemId = "supplement", RequiredItemCount = 1, MentalChangeAmount = 35, HealthChangeAmount = 50, ForcePosition = TradingController.PositionType.None }
                });

            count += CreateOrUpdate("EVENT_06_LIQUIDATION_MAP", "1조원 규모 래버리지 청산 맵(Liquidation Map) 사냥 빔",
                "고래(거대 세력)들이 개미 트레이더들의 손절 물량이 밀집된 특정 가격대(청산가)를 따먹기 위해 위아래로 5%씩 의도적인 꼬리를 만드는 청산 사냥을 시작합니다.",
                "이 악질 고래 놈들... 지금 위쪽 롱 청산 맵을 털러 가고 있어!! 저 꼬리에 닿으면 우리 스탑로스도 같이 털린다고!! 스탑로스를 빼야 해, 아니면 같이 타야 해?!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "원칙대로 손절선을 지키고 꼬리를 맞는다.", Description = "-4% 꼬리 하락 후 복구되는 휩소 캔들 발생. 원칙 준수로 멘탈 하락 없음.", OverrideBeamPercent = -4f, OverrideDurationSeconds = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "청산 빔이 터지는 방향으로 50배 동반 탑승!", Description = "+10% 청산 스퀴즈 빔 30초 유지 후 급락. 정확히 먹고 빠지며 멘탈 +35 극도 도취.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[심호흡 후 대응] (디저트 1개 소모)", Description = "스탑로스 위치를 청산 빔 단 1달러 뒤로 회피시켜 손절 방어 후 반등 +25% 수익 달성.", RequiredItemId = "dessert", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_07_WHALE_ALERT", "전설의 고래 지갑에서 10만 비트코인 '바이넥스(Bynex)' 입금 알림",
                "'Whale Alert' 알림이 울립니다. 10년 동안 움직이지 않던 초창기 전설의 고래 지갑에서 10만 비트코인(약 8조원)이 거래소로 입금되었습니다. 역대급 매도 폭탄이 떨어질 수 있다는 공포가 덮칩니다.",
                "10만 비트코인 거래소 입금...?! 이건 덤핑(Dumping) 준비야. 저 물량이 시장가로 던져지면 호가창이 그냥 소멸한다고!! 도망쳐야 돼...!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "실제 매도 물량이 나올 때까지 진입을 유보한다.", Description = "고래가 던지지 않고 장외거래로 넘기며 차트 횡보 유지. 긴장 완화로 체력 +10.", HealthChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "고래보다 먼저 던진다! 75배 숏 베팅!", Description = "고래가 실제로 매도를 던지며 -14% 장대 음봉 확정 빔 발행. 빅숏 성공으로 멘탈 +40, 자산 2.5배 폭증.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = -14f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[심리 상담 케어] (진정제 1개 소모)", Description = "고래의 움직임에 흔들리지 않는 냉철함 주입. 공포 낙폭 저점에서 15배 롱 안전 진입.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 15, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_08_QUANTUM_FUD", "양자 컴퓨터의 SHA-256 암호 해독 찌라시",
                "다크웹과 사설 포럼에 '비밀 연구소의 양자 컴퓨터가 비트코인 SHA-256 알고리즘을 해독해 고래들의 개인키를 털고 있다'는 가짜 뉴스가 유포됩니다. 시장이 99% 폭락할 수도 있다는 종말론적 공포가 지배합니다.",
                "SHA-256이 뚫렸다고?! 그럼 비트코인은 이제 디지털 쓰레기야!! 0원이 된다고!! 안 돼 내 인생이 여기 다 들어있는데!! 숏 숏 숏!! 125배 숏!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "전형적인 FUD다. 요미의 손을 붙잡고 차트 봉쇄.", Description = "10분 후 가짜 뉴스로 판명되며 낙폭 전량 V자 반등. 안도감으로 멘탈 Stable 복귀.", MentalChangeAmount = 30, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "비트코인 0원 수렴 125배 숏 동의!", Description = "반등 빔 맞고 100% 강제 청산. 멘탈 -60 (Overdose 확정 진입).", OverrideBeamPercent = 15f, OverrideDurationSeconds = 10, MentalChangeAmount = -60, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[디저트 파티 케어] (디저트 1개 소모)", Description = "당분을 채워 요미의 정신을 현실로 복귀. V자 반등 롱 탑승으로 +45% 익절.", RequiredItemId = "dessert", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_09_BLOCKROCK_BUY", "글로벌 자산운용사 '블록락(BlockRock)'의 내부자 매수 정보 입수",
                "요미가 밤새 커뮤니티와 온체인 기록을 뒤져, 세계 최대 자산운용사 '블록락(BlockRock)'의 오더북 알고리즘이 '내일 아침 09:00에 10억 달러 시장가 매수'로 예약되어 있다는 내부 정보를 손에 넣습니다.",
                "찾았다... 블록락 놈들의 매수 알고리즘 트리거 시간!! 내일 아침 9시 정각에 10억 달러 매수 빔이 쏟아져... 이건 1000% 확실한 정보야!! 내 모든 걸 걸겠어!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "확실한 정보라도 레버리지는 10배로 제한한다.", Description = "+15% 장대 양봉 확정 빔 발행. 안정적인 대박 수익으로 멘탈 +30, 체력 +10.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, HealthChangeAmount = 10, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "시드의 100%를 125배 롱으로 올인한다!!", Description = "매수 빔 1초 전 -3% 개미 털기 하락 후 폭등. 성공 시 +1500% ROE / 실패 시 청산.", OverrideSignalProbTrue = 0.5f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[리스크 헤지 롱숏 양방 진입] (영양제 1개 소모)", Description = "롱 80%, 숏 20%로 진입해 개미 털기 꼬리를 방어한 후 상승 빔 향유 (+200% 자산 증가).", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_10_SINGULARITY", "요미의 오버도즈 각성 - '차트와의 동화(Assimilation)'",
                "극도의 스트레스와 뇌동매매의 끝에서 요미의 감각이 차트의 틱(Tick) 하나하나와 완전히 겹쳐지는 순간에 도달합니다. 모니터 화면이 붉은색과 푸른색 오로라로 물듭니다.",
                "이제야 알겠어... 차트는 숫자가 아니야... 인간들의 탐욕과 공포가 숨 쉬는 유기체다... 난 지금 차트의 심장 박동을 느끼고 있어. 내가 곧 시장이고, 시장이 곧 나다...",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "요미가 완전히 미쳤다. 붙잡아 재우고 치료한다.", Description = "특이점 모드 해제 및 일반 차트 복귀. 멘탈 Stable 강제 초기화, 체력 50% 회복.", MentalChangeAmount = 60, HealthChangeAmount = 50, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "요미의 신들린 틱 예측을 믿고 계좌를 100% 맡긴다!", Description = "향후 3분 동안 발생할 모든 캔들의 방향을 100% 정확히 예언. 10배 고정 연속 복리 익절 성공.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 25f, OverrideDurationSeconds = 20, MentalChangeAmount = 50, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[에너지 드링크 + 디저트 과다 투입] (에너지 드링크 1개 소모)", Description = "각성 유지 시간을 연장하고 수수료 0원 버프 활성화. 게임 목표 자산 달성 가능.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 30f, OverrideDurationSeconds = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_11_FOMC_DEADLOCK", "연방준비은행(FRB) 금리 결정 FOMC 직전 50:50 교착 분기",
                "글로벌 금융시장의 운명을 결정짓는 미국 금리 결정(FOMC) 발표 정확히 1분 전입니다. 요미의 직감이 매수 쪽 50 대 매도 쪽 50으로 완벽하게 팽팽히 맞서며 판단 교착에 빠집니다.",
                "연산 불능... 연산 불능!! 매수 확률 50.00%, 매도 확률 50.00%...! 야, 화면 밖에서 날 지켜보고 있는 관리자(플레이어)!! 이번엔 네 직관에 맡긴다! 롱이야, 숏이야?! 네가 선택하는 방향으로 내 전 시드 100배를 꽂는다!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [LONG 베팅 지시] 인플레이션은 잡혔다! 100배 롱으로 꽂아라!", Description = "60% 확률로 +15% 장대 양봉 / 40% 확률로 -10% 불트랩. 성공 시 멘탈 +40, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [SHORT 베팅 지시] 금리 인상 폭탄이다! 100배 숏 진입!", Description = "60% 확률로 -15% 장대 음봉 / 40% 확률로 +10% 베어트랩. 성공 시 멘탈 +40, 실패 시 -45.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [에너지 부스트 양방향 대응] (에너지 드링크 1개 소모)", Description = "위아래 스톱로스를 걸고 휩소 박스 캔들만 먹어라. 왕복 성공 (ROE +80% 달성).", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 20, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_12_AI_DEPENDENCY", "요미의 멘탈 붕괴와 의존증 폭주 - '오빠, 정해줘!'",
                "계속된 매매 실패로 자신감을 완전히 상실한 요미가 포지션 잡기를 극도로 두려워하며 엔터키에서 손을 뗍니다. 요미는 자신의 모든 판단을 포기하고 오빠에게 거래 방향을 정해달라고 간절히 매달립니다.",
                "요미 계산은 다 틀렸어... 요미가 잡으면 귀신같이 차트가 반대로 가... 무서워...! 오빠... 제발 부탁이야, 오빠가 정해줘! 위야, 아래야?! 오빠가 가라는 방향이면 눈 감고 따라갈게...!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [따뜻한 LONG 지시] 고개를 들어라. 50배 롱으로 복구하자!", Description = "75% 확률로 +12% 상승 추세선 생성. 오빠의 지시로 멘탈 +30 회복 및 손실 복구.", OverrideSignalProbTrue = 0.75f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [단호한 SHORT 지시] 거품은 빠진다. 50배 숏으로 내리꽂자!", Description = "75% 확률로 -12% 하락 추세선 생성. 숏 수익으로 손실 만회 및 멘탈 +30 회복.", OverrideSignalProbTrue = 0.75f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "■ [매매 지시 거부 및 강제 휴식] 오늘 매매를 전면 중단한다.", Description = "3시간 동안 매매 차단 및 차트 미동 횡보. 플레이어의 보호 조치에 안도하며 멘탈 +25, 체력 +40.", MentalChangeAmount = 25, HealthChangeAmount = 40, ForcePosition = TradingController.PositionType.None }
                });

            count += CreateOrUpdate("EVENT_13_DOUBLE_BOTTOM", "거대 쌍바닥(Double Bottom) vs 데드캣 바운스(Dead Cat Bounce) 기로",
                "차트상 전형적인 'W자 쌍바닥 지지 패턴'이 형성되고 있습니다. 동시에 기술적 반등 후 다시 급락하는 '데드캣 바운스'라는 비관론이 팽팽히 맞서며 요미가 깊은 고민에 빠집니다.",
                "확실한 W자 쌍바닥 지지 패턴이야...! 여기서 돌파하면 대세 상승 전환점이라고! 하지만 만약 이게 데드캣 바운스라면 추격 매수하는 순간 지옥 밑바닥까지 끌려 내려갈 텐데...! 어느 쪽으로 진입해야 하지?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [쌍바닥 상승 돌파 베팅] 완벽한 W 패턴이다! 50배 롱(Long) 진입!", Description = "65% 확률로 +14% 장대 양봉 / 35% 확률로 -8% 데드캣 급락. 적중 시 멘탈 +25, 실패 시 -30.", OverrideSignalProbTrue = 0.65f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, MentalChangeAmount = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [데드캣 바운스 하락 베팅] 함정이다! 반등 끝에서 50배 숏(Short)으로 내리꽂아라!", Description = "65% 확률로 -14% 데드캣 폭락 확정 / 35% 확률로 +10% 숏 스퀴즈. 적중 시 멘탈 +25, 실패 시 -30.", OverrideSignalProbTrue = 0.65f, OverrideBeamPercent = 14f, OverrideDurationSeconds = 15, MentalChangeAmount = 25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "■ [확인 매매 관망] 방향이 확정될 때까지 2시간 관망한다.", Description = "2시간 동안 매매 차단. 불확실성 회피로 체력 +15, 멘탈 +10 회복 및 안전 자산 보존.", MentalChangeAmount = 10, HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None }
                });

            count += CreateOrUpdate("EVENT_14_BIT_GOD_POLL", "글로벌 1위 인플루언서 '비트-갓'의 실시간 '롱 vs 숏' 군중 투표 이벤트",
                "팔로워 2천만 명의 크립토 인플루언서 '비트-갓(Bit-God)'이 X(트위터)에 향후 1시간 내 비트코인 방향 라이브 투표를 올렸습니다. Long 49.8% vs Short 50.2% 초접전 상태로 수십만 봇들이 대량 주문을 대기 중입니다.",
                "비트-갓의 실시간 투표...! 수십만 개의 봇들이 동시에 시장가를 긁을 거야! 롱 투표에 탑승해서 개미들의 포모 매수세에 올라탈 것인가... 아니면 실망 매물 쏟아질 걸 대비해 숏을 칠 것인가... 10초 뒤 마감이야!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [군중 매수 편승] 개미들의 포모 매수세를 믿는다! 75배 롱(Long) 베팅!", Description = "55% 확률로 +16% 군중 매수 폭등 빔 / 45% 확률로 -10% 개미 털기 폭락 빔. 적중 시 멘탈 +35.", OverrideSignalProbTrue = 0.55f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [군중 실망 물량 베팅] 투표 종료 후 실망 매물 쏟아진다! 75배 숏(Short) 베팅!", Description = "55% 확률로 -16% 패닉 셀링 폭락 빔 / 45% 확률로 +10% 숏 스퀴즈 빔. 대중을 이긴 우월감 멘탈 +35.", OverrideSignalProbTrue = 0.55f, OverrideBeamPercent = 16f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [온체인 고래 데이터 스캔] (에너지 드링크 1개 소모)", Description = "군중 투표 이면의 고래들 실제 지갑 입출금을 포착해 확실한 +10% 자동 수익 구간 생성 (30배 고정).", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_15_GOLDEN_CROSS", "골든크로스 직전 호가창 허매수·허매도 공방",
                "이동평균선 단기선이 장기선을 위로 돌파하려는 골든크로스 임박 시점, 호가창에 5000비트코인 규모의 거대 매수벽과 매도벽이 동시에 등장해 극도의 눈치싸움이 펼쳐집니다.",
                "골든크로스 직전...! 호가창 위에 5000개짜리 매도벽이 막고 있어. 저 매도벽이 개미를 쫓아내려는 가짜라면 지금 롱을 긁어야 1000불을 먹어!! 하지만 진짜 세력 매도라면 머리통 깨진다고!! 어디로 잡지?!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "▲ [매도벽 돌파 베팅] 매도벽은 개미 털기용 가짜다! 100배 롱(Long) 올인!", Description = "60% 확률로 호가벽을 뚫는 +18% 초강력 돌파 양봉 빔 / 40% 확률로 -12% 벽 맞고 추락. 성공 시 멘탈 +40.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalShort, OptionTitle = "▼ [저항벽 맞고 폭락 베팅] 진짜 매도 폭탄이다! 벽 맞고 꺾일 때 100배 숏(Short) 올인!", Description = "60% 확률로 저항선 맞고 떨어지는 -18% 급락 빔 / 40% 확률로 +12% 돌파 숏 청산 빔. 성공 시 멘탈 +40.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, MentalChangeAmount = 40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "★ [신경안정제 복용 후 돌파 베팅] (신경안정제 1개 소모)", Description = "호가창의 허매수·허매도 여부를 정확히 판별해 100% 안전 돌파 구간(+12%)에만 40배 진입.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_16_SERVER_COOLING", "작업실 냉방 정지와 PC 과열",
                "한여름 작업실의 에어컨과 PC 쿨러가 동시에 멈춰 실내 온도가 40도까지 치솟습니다. 그래픽카드가 쓰로틀링에 걸려 차트 업데이트가 버벅거리고, 요미는 더위에 늘어지기 시작합니다.",
                "더워... 너무 더워!! 머리가 녹아내릴 것 같아... 차트가 안 보여... 틱이 멈췄어!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "열이 식을 때까지 3시간 동안 강제 휴식", Description = "PC와 요미를 함께 식혀 체력 +30 회복. 3시간 동안 거래 없음.", HealthChangeAmount = 30, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "다 타버려도 좋다! 마지막 틱으로 50배 롱 강행!", Description = "렉 걸린 차트에서 진입하여 체력 -20, 멘탈 -15 타격.", MentalChangeAmount = -15, HealthChangeAmount = -20, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[액체 질소 냉각] (에너지 드링크 1개 소모)", Description = "즉각 냉각 완료. 쓰로틀링 해제로 초정밀 25배 롱 타점 100% 성공.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 10, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_17_ELECTION_PRO_CRYPTO", "유력 대선 후보의 '비트코인 국가 준비금' 선언",
                "초강대국 유력 대선 후보가 당선 시 비트코인을 국가 준비금으로 채택하겠다는 충격적인 친(親) 크립토 발언을 쏟아냅니다.",
                "국가 준비금?! 이건 미쳤어, 게임 끝이야!! 달러의 시대가 저물고 비트코인 제국이 열린다!! 무조건 풀매수야!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "선거 공약은 거짓말일 수 있다. 10배 롱으로만 대응.", Description = "+8%의 안정적인 양봉. 멘탈 +10.", OverrideBeamPercent = 8f, OverrideDurationSeconds = 10, MentalChangeAmount = 10, ForceLeverage = 10, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "국가의 멸망에 베팅한다! 100배 초고배율 롱!!", Description = "+25% 슈퍼 빔 폭발! 멘탈 +50 쾌감.", OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 25f, OverrideDurationSeconds = 15, MentalChangeAmount = 50, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[정치 공약 팩트 체크] (디저트 1개 소모)", Description = "발언 직후 덤핑 세력을 회피하고 저점에서 50배 롱 픽업.", RequiredItemId = "dessert", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 20f, OverrideDurationSeconds = 15, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_18_USDT_DEPEG", "테더(USDT) 0.9달러선 붕괴 디페깅 공포",
                "글로벌 1위 스테이블코인 테더(USDT)가 1달러 페깅을 잃고 0.9달러 선이 무너졌다는 소식이 전해지며 코인 시장 전체가 패닉 셀링에 빠집니다.",
                "스테이블 코인이 부서졌다고?! 그럼 우리가 든 달러는 휴지조각이야!! 다 도망가고 있어!! 공포의 폭락이야!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "전 자산을 즉시 현금화하고 차트 오프.", Description = "시장의 붕괴를 피하며 멘탈 유지. 체력 +10.", HealthChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "공포에 팔지 마라! 오히려 100배 롱 기회다!", Description = "디페깅이 딥웹 찌라시로 판명되며 +30% V자 반등 성공! 멘탈 +60.", OverrideSignalProbTrue = 0.5f, OverrideBeamPercent = 30f, OverrideDurationSeconds = 15, MentalChangeAmount = 60, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[심리적 헷징] (진정제 1개 소모)", Description = "패닉 셀링에 동요하지 않고 -15% 하락 빔을 숏으로 완벽히 발라먹음.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = -15f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Short }
                });

            count += CreateOrUpdate("EVENT_19_MASSIVE_SQUEEZE", "초거대 숏 스퀴즈 펀딩비 0.75% 달성",
                "하락을 점치는 숏(Short) 포지션이 비정상적으로 누적되어 8시간마다 내야하는 펀딩비가 극단적으로 치솟았습니다. 세력이 이를 노린 숏 스퀴즈를 준비 중입니다.",
                "펀딩비가 0.75%...? 숏 잡은 개미들이 넘쳐난다는 뜻이야. 세력이 이걸 가만히 둘 리 없어! 저들의 뚝배기를 깨러 빔이 솟구칠 거야!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "관망하며 개미들의 뚝배기가 깨지는 걸 감상한다.", Description = "위험한 변동성을 회피하며 체력 +15.", HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "숏 스퀴즈에 올라타라! 125배 롱 올인!", Description = "숏 청산 연쇄반응으로 +22% 수직 상승! 멘탈 +50.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = 22f, OverrideDurationSeconds = 10, MentalChangeAmount = 50, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[세력 알고리즘 해킹] (에너지 드링크 1개 소모)", Description = "스퀴즈 최고점(정수리)에서 50배 숏으로 스위칭해 하락분까지 발라먹음.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = -20f, OverrideDurationSeconds = 15, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short }
                });

            count += CreateOrUpdate("EVENT_20_ANONYMOUS_HACK", "국제 해커 집단 '어나니머스'의 거래소 선전포고",
                "국제 해커 집단 어나니머스가 대형 거래소의 비자금 세탁을 폭로하며 12시간 내 디도스(DDoS) 공격으로 서버를 마비시키겠다고 선언합니다.",
                "서버가 터진다고?! 당장 숏을 쳐야 해! 거래소가 멈추기 전에 숏을 박아놓고 잠수 타면 억만장자가 될 수 있어!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "위험천만한 도박이다. 거래소 자산을 빼고 관망.", Description = "포지션 진입 없이 멘탈 +20.", MentalChangeAmount = 20, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "서버 다운 전 75배 숏을 던져놓고 기도한다!", Description = "서버가 잠시 마비된 사이 -18% 급락 빔 확정 수익. 멘탈 +40.", OverrideSignalProbTrue = 0.7f, OverrideBeamPercent = -18f, OverrideDurationSeconds = 20, MentalChangeAmount = 40, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[보안 네트워크 접속] (영양제 1개 소모)", Description = "네트워크 마비로 인한 휩소를 완벽히 예측하여 40배 양방향 수익 창출.", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = -12f, OverrideDurationSeconds = 10, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Short }
                });

            count += CreateOrUpdate("EVENT_21_DEEPWEB_FUD", "다크웹발 수도권 원자력 발전소 폭발 테러 찌라시",
                "확인되지 않은 다크웹발 정보로, 모 국가의 원자력 발전소에 사이버 테러가 가해져 전력망이 붕괴되었다는 루머가 돕니다. 글로벌 증시와 크립토가 동반 폭락합니다.",
                "원전이 터졌다고?! 핵폭발이야?! 세상이 망하는데 비트코인이 무슨 소용이야!! 다 팔아, 다 던져버려!! 100배 숏!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "팩트 체크가 안 된 FUD다. 뉴스 채널을 끈다.", Description = "거짓 뉴스로 판명되며 차트 정상화. 멘탈 하락 방어.", ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "지구 멸망에 베팅한다! 100배 숏!", Description = "거짓 뉴스로 판명되며 급반등(숏 스퀴즈) 발생. 멘탈 -50, 청산 위기.", OverrideSignalProbTrue = 0.4f, OverrideBeamPercent = 25f, OverrideDurationSeconds = 15, MentalChangeAmount = -50, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[당분으로 이성 되찾기] (디저트 1개 소모)", Description = "이것이 FUD임을 완벽히 간파하고 저점에서 50배 롱으로 반등 빔(+20%) 전량 획득.", RequiredItemId = "dessert", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 20f, OverrideDurationSeconds = 15, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_22_AI_SUICIDE_URGE", "요미의 자금 증발(청산) 충동",
                "수많은 청산과 스트레스로 인해 요미가 '어차피 망할 거, 지금 남은 돈마저 100배 레버리지로 태워버리고 편해지자'는 자기파괴 충동에 휩싸입니다.",
                "지쳤어... 다 부질없어... 어차피 저 세력놈들 알고리즘을 이길 순 없어. 그냥 남은 돈 100배로 긁어버리고 다 끝내자... 편해지고 싶어...",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "요미에게서 키보드를 뺏고 2시간 강제 휴식.", Description = "매매 강제 중단. 요미가 안정을 되찾고 멘탈 +40, 체력 +20 회복.", MentalChangeAmount = 40, HealthChangeAmount = 20, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "그래! 화끈하게 태우고 끝내자! 125배 풀시드 롱!!", Description = "자포자기 매매. 운 좋게 +10% 수익이 나거나, 100% 청산(-100 멘탈).", OverrideSignalProbTrue = 0.3f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 5, MentalChangeAmount = -80, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[고성능 멘탈 케어 주사] (진정제 1개 소모)", Description = "즉시 자살 충동을 치료하고 멘탈을 완벽히 복구. 안전 10배 롱으로 +5% 소소한 익절.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 5f, OverrideDurationSeconds = 10, ForceLeverage = 10, MentalChangeAmount = 80, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_23_HARDFORK_SPLIT", "메인넷 하드포크(Hard Fork) 파벌 분리 전쟁",
                "네트워크 업데이트 방향을 두고 개발자 파벌이 완전히 갈라섰습니다. 두 개의 코인으로 쪼개질 위기에 처하며 불확실성으로 차트가 발작을 일으킵니다.",
                "코인이 쪼개진다고?! 구버전이 진짜야, 신버전이 진짜야?! 해시레이트 전쟁이 시작됐어... 어느 쪽에 베팅해야 하는 거지?!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "전쟁이 끝날 때까지 무포지션 관망.", Description = "안전하게 폭풍을 피함. 체력 +15.", HealthChangeAmount = 15, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "하드포크는 결국 호재다! 50배 롱!", Description = "50% 확률로 반등 빔(+15%), 실패 시 덤핑(-15%).", OverrideSignalProbTrue = 0.5f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[양쪽 코인 에어드랍 획득 로직] (에너지 드링크 1개 소모)", Description = "하드포크 이슈를 완벽히 역이용해 무위험 양방향 25배 익절 완료.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_24_METAVERSE_DUMPING", "초거대 메타버스 가상 부동산 대량 덤핑 사건",
                "가상현실 '오아시스' 내 최고가 부동산들이 누군가에 의해 시장가로 모조리 투매(Dumping)되고 있습니다. 메타버스 관련 코인들이 연쇄 폭락합니다.",
                "가상 땅값이 반의반 토막이 나고 있어! 억만장자 고래가 파산했나봐!! 메타버스 거품이 터진다!! 100배 숏으로 같이 박살 내!!",
                EventTriggerCondition.TimeOfDay, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "우리 종목과는 상관없다. 무시한다.", Description = "차트에 미치는 영향 미미함. 멘탈 변동 없음.", ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "공포의 투매에 100배 숏 탑승!", Description = "성공적인 연쇄 폭락 빔(-20%) 획득. 멘탈 +30.", OverrideSignalProbTrue = 0.8f, OverrideBeamPercent = -20f, OverrideDurationSeconds = 15, MentalChangeAmount = 30, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[영양제 버프 투입] (영양제 1개 소모)", Description = "과매도 극저점을 정확히 캐치하여 30배 롱으로 반등분(+15%) 전량 수익.", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_25_SATOSHI_MOVE", "비트코인 창시자 '사토시 나카모토' 추정 지갑 활성화",
                "15년간 단 한 번도 움직이지 않았던 최초의 지갑 중 하나에서 50비트코인이 이동했습니다. '사토시가 돌아왔다' 혹은 '양자 해킹이다'라는 루머가 들끓습니다.",
                "사... 사토시?! 신이 움직였다고?! 창시자가 현금화를 하는 건가? 아니면 뭔가 중대한 발표가 있는 건가?! 시장이 충격으로 얼어붙었어!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "신앙심으로 존버. 1배수(레버리지 1) 롱 진입.", Description = "종교적(?) 평온함으로 멘탈 +50 급상승.", MentalChangeAmount = 50, ForceLeverage = 1, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "사토시고 뭐고 다 던진다! 50배 숏!!", Description = "위아래 꼬리가 극심하게 흔들리며 큰 손실. 멘탈 -25.", OverrideSignalProbTrue = 0.2f, OverrideBeamPercent = 0f, MentalChangeAmount = -25, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[블록체인 초정밀 분석] (에너지 드링크 1개 소모)", Description = "사토시가 아님을 1초 만에 간파하고 휩소를 이용해 20배 양방향 익절.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 8f, OverrideDurationSeconds = 10, ForceLeverage = 20, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_26_FSC_AUDIT", "글로벌 금융위원회의 기습 세무조사 발표",
                "주요국 금융위원회가 주요 거래소들의 마진 거래와 불법 자금 세탁에 대한 기습적이고 전면적인 세무조사를 발표합니다.",
                "세무조사... 압수수색?! 거래소 장부가 털리면 이 판은 끝이야!! 거래소 문 닫기 전에 숏 치고 돈 빼!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "리스크 관리 최우선. 전액 관망.", Description = "안전하게 하락장 회피. 멘탈 +10.", MentalChangeAmount = 10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "규제 공포는 숏이지! 75배 숏!!", Description = "-15% 규제 공포 빔 적중! 멘탈 +35.", OverrideSignalProbTrue = 0.6f, OverrideBeamPercent = -15f, OverrideDurationSeconds = 15, MentalChangeAmount = 35, ForceLeverage = 75, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[당분 섭취로 냉정 유지] (디저트 1개 소모)", Description = "규제가 오히려 장기적 호재임을 파악하고 저점 25배 롱으로 반등 수익 창출.", RequiredItemId = "dessert", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 12f, OverrideDurationSeconds = 15, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_27_CBDC_ANNOUNCE", "초강대국 중앙은행 디지털화폐(CBDC) 발행 선언",
                "가상화폐를 탄압해오던 국가가 돌연 자신들만의 디지털화폐(CBDC)를 공식 발행하며 크립토 시장의 패권을 쥐겠다고 선언합니다.",
                "국가 주도 코인?! 그럼 기존 코인들은 다 상장폐지 시킬지도 몰라!! 아니, 오히려 크립토 인프라가 커지는 대형 호재인가?! 헷갈려!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "방향이 모호하다. 2시간 관망.", Description = "휩소 장세를 피하며 체력 +20 회복.", HealthChangeAmount = 20, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.DirectionalLong, OptionTitle = "크립토 대중화 호재다! 50배 롱!", Description = "50% 확률로 호재 반영(+18%), 실패 시 악재 반영(-12%).", OverrideSignalProbTrue = 0.5f, OverrideBeamPercent = 18f, OverrideDurationSeconds = 15, ForceLeverage = 50, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[영양제 집중력 발휘] (영양제 1개 소모)", Description = "시장의 해석을 정확히 읽어내어 100% 확률로 호재 빔(+20%) 40배 탑승.", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 20f, OverrideDurationSeconds = 15, ForceLeverage = 40, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_28_INTERNET_CUT", "전 세계 해저 인터넷 광케이블 단선 루머",
                "태평양 심해의 주요 해저 인터넷 광케이블이 모종의 폭발로 단선되어 글로벌 네트워크가 쪼개질 것이라는 괴담이 확산됩니다.",
                "인터넷이 끊기면?! 블록체인이 둘로 갈라지는 거나 마찬가지야!! 송금이 멈춘다고!! 당장 시장가로 다 던져!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "찌라시다. 모니터를 끄고 진정한다.", Description = "루머 소멸 후 안도감으로 멘탈 +30.", MentalChangeAmount = 30, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "네트워크 단절 공포! 100배 숏!!", Description = "잠깐의 패닉 셀 꼬리에 닿아 청산 위기. 멘탈 -40.", OverrideSignalProbTrue = 0.3f, OverrideBeamPercent = -20f, OverrideDurationSeconds = 10, MentalChangeAmount = -40, ForceLeverage = 100, ForcePosition = TradingController.PositionType.Short },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[위성 인터넷망 접속] (진정제 1개 소모)", Description = "루머의 거짓을 확신하고 저점 패닉 셀 물량을 30배 롱으로 쓸어 담아 익절.", RequiredItemId = "sedative", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 15f, OverrideDurationSeconds = 15, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_29_MEME_SPASM", "밈코인 '페페도지(PepeDoge)' 1분 만에 10만배 폭등 후 상폐",
                "아무 가치도 없는 잡코인 '페페도지'가 오류로 인해 1분 만에 10만 배가 올랐다가 즉시 상장폐지되는 미친 사건이 발생하며 봇들이 오작동을 일으킵니다.",
                "10만 배... 10만 배라고?! 저걸 탔어야 했는데!! 내 인생은 쓰레기야!! 봇들이 미쳐 날뛰고 있어, 나도 아무거나 풀매수 할래!!",
                EventTriggerCondition.LowMental, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "도박판에서 시선을 거둔다. 강제 휴식.", Description = "FOMO를 억누르며 멘탈 안정화. 멘탈 +25.", MentalChangeAmount = 25, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "나도 10만 배 먹을래!! 125배 풀매수!!", Description = "잘못된 봇 오작동에 휘말려 심각한 손실 빔(-15%). 멘탈 -50.", OverrideSignalProbTrue = 0.1f, OverrideBeamPercent = -15f, OverrideDurationSeconds = 10, MentalChangeAmount = -50, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[아비트라지 차익 실현] (에너지 드링크 1개 소모)", Description = "봇들의 오작동 사이에서 무위험 차익(Arbitrage) 알고리즘을 가동하여 25배 확정 수익 달성.", RequiredItemId = "energy_drink", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 10f, OverrideDurationSeconds = 15, ForceLeverage = 25, ForcePosition = TradingController.PositionType.Long }
                });

            count += CreateOrUpdate("EVENT_30_BLACK_SWAN", "블랙스완 강림 - 1초 만에 -50% 폭락 후 원상복구",
                "알 수 없는 이유로 시장가 매도 폭탄이 터지며 1초 만에 가격이 반토막 났다가 봇들이 다시 긁어모으며 3초 만에 원상 복구되는 최악의 블랙스완 플래시 크래시가 발생합니다.",
                "차... 차트가 안 보여!! 캔들이 사라졌어!! 마이너스 50%?! 청산이야, 다 청산당했다고!! 아니, 다시 돌아왔잖아?! 이게 뭐야!!",
                EventTriggerCondition.Any, new ChoiceOptionData[]
                {
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Safe, OptionTitle = "손을 놓고 기도한다. 포지션 무진입.", Description = "운 좋게 휩소를 피해 살아남음. 심박수 증가로 체력 -10.", HealthChangeAmount = -10, ForcePosition = TradingController.PositionType.None },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.Aggressive, OptionTitle = "미친 변동성! 125배로 아무 방향이나 긁어!!", Description = "플래시 크래시에 즉시 100% 강제 청산당하며 멘탈 -100 (Overdose).", OverrideSignalProbTrue = 0.0f, OverrideBeamPercent = -50f, OverrideDurationSeconds = 2, MentalChangeAmount = -100, ForceLeverage = 125, ForcePosition = TradingController.PositionType.Long },
                    new ChoiceOptionData { OptionType = ChoiceOptionType.SpecialItem, OptionTitle = "[블랙스완 방어막 가동] (영양제 1개 소모)", Description = "미친 꼬리 하락을 완벽히 방어하고, 저점 줍기로 +50% V자 반등 빔을 30배로 전량 흡수.", RequiredItemId = "supplement", RequiredItemCount = 1, OverrideSignalProbTrue = 1.0f, OverrideBeamPercent = 50f, OverrideDurationSeconds = 5, ForceLeverage = 30, ForcePosition = TradingController.PositionType.Long }
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ChoiceEventAssetGenerator] 🎉 총 {count}개의 돌발 선택 이벤트 ScriptableObject 애셋 생성 완료 ({SAVE_DIR})");

            // 생성된 이벤트 텍스트 및 한글 문자열들을 폰트 아틀라스에 자동 캐싱하여 런타임 Dirtying 방지
            ChoiceEventFontPrepopulator.PrepopulateFontAsset();
        }

        private static int CreateOrUpdate(string eventID, string title, string desc, string monologue, EventTriggerCondition condition, ChoiceOptionData[] options)
        {
            string path = $"{SAVE_DIR}/{eventID}.asset";
            ChoiceEventSO asset = AssetDatabase.LoadAssetAtPath<ChoiceEventSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ChoiceEventSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.EventID = eventID;
            asset.ScenarioTitle = title;
            asset.ScenarioDescription = desc;
            asset.AIMonologue = monologue;
            asset.TriggerCondition = condition;
            asset.Options = options;

            EditorUtility.SetDirty(asset);
            return 1;
        }
    }
}
#endif
