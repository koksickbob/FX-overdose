namespace FXOverdose.AI
{
    /// <summary>
    /// 주인공 AI 트레이더의 정형화된 감정 표현 라벨.
    /// LLM 프롬프트, 스프라이트 애니메이션, Fallback 대사 등 모든 시스템에서 공통으로 사용.
    /// </summary>
    public enum TraderEmotion
    {
        // ===== 긍정 감정 (Positive) =====
        Euphoria,        // 극도의 환희/흥분 (대박 수익, ROE > +30%)
        Confident,       // 자신만만/거만 (안정 수익권, ROE +5% ~ +30%)
        Pleased,         // 기분 좋음/만족 (소폭 수익, ROE 0% ~ +5%)
        Relieved,        // 안도/후련함 (위기 탈출 직후, 손절 회피 성공)
        Affectionate,    // 애교/집착 애정 표현 (마스터에게 칭찬 갈구)

        // ===== 중립/관망 감정 (Neutral) =====
        Focused,         // 집중/냉정 분석 (포지션 없이 차트 관망 중)
        Suspicious,      // 의심/경계 (가짜 신호 감지, 세력 경계)

        // ===== 부정 감정 (Negative - Light) =====
        Anxious,         // 초조/불안 (소폭 손실, ROE -5% ~ 0%)
        Frustrated,      // 좌절/짜증 (연속 손절, 타점 실패)
        Regretful,       // 후회/아쉬움 (놓친 기회, 조기 익절 후회)
        Jealous,         // 질투/시기 (마스터 관심 부족, 다른 곳에 집중 - 시스템 준비용, 현재 미사용)

        // ===== 부정 감정 (Negative - Heavy) =====
        Panicked,        // 패닉/공포 (급격한 손실, ROE < -20%, 청산 임박)
        Despairing,      // 절망/체념 (대형 손실, 멘탈 Danger 상태)
        Furious,         // 분노/격앙 (세력에 대한 증오, 강제 청산)
        Tearful,         // 울음/오열 (파산 직전, 마스터에게 애원)

        // ===== 극단/특수 감정 (Extreme/Special) =====
        Manic,           // 광기/폭주 (Overdose 상태, 통제 불능 쾌감)
        Obsessive,       // 병적 집착 (마스터 독점욕, 게임오버 직후)
        Exhausted,       // 탈진/기력 소진 (체력 0 근접, 의식 흐릿)
        Vengeful         // 복수심/저주 (세력에 대한 피맺힌 증오)
    }
}
