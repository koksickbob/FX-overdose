namespace FXOverdose.DatingSim.Dialogue
{
    /// <summary>
    /// 요미의 응답 대사를 만들어 주는 공급자입니다.
    /// 자유 채팅(LLM) 제거 후, 향후 대체 대화 시스템이 이 인터페이스를 구현합니다.
    /// </summary>
    public interface IYomiDialogueProvider
    {
        /// <summary>대화 기능이 실제로 동작 가능한지 여부입니다. false면 자리 표시자 응답만 나옵니다.</summary>
        bool IsAvailable { get; }

        /// <summary>유저 입력에 대한 요미의 응답 한 줄을 반환합니다.</summary>
        string GetResponse(string userMessage);
    }
}
