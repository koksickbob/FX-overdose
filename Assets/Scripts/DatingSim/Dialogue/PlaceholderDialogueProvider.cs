namespace FXOverdose.DatingSim.Dialogue
{
    /// <summary>
    /// 대화 시스템 기획이 확정되기 전까지 사용하는 자리 표시자입니다.
    /// UI 흐름(입력 → 응답 표시)은 그대로 유지하되, 준비 중임을 알리는 고정 대사만 돌려줍니다.
    /// 새 대화 시스템이 정해지면 이 클래스 대신 새 구현체를 주입하면 됩니다.
    /// </summary>
    public sealed class PlaceholderDialogueProvider : IYomiDialogueProvider
    {
        public static readonly PlaceholderDialogueProvider Default = new PlaceholderDialogueProvider();

        /// <summary>자리 표시자이므로 항상 false입니다. 호출부는 이 값으로 보상 지급 여부를 판단합니다.</summary>
        public bool IsAvailable => false;

        public string GetResponse(string userMessage)
        {
            return "지금은 대화 기능을 준비하고 있어... 조금만 기다려 줘.";
        }
    }
}
