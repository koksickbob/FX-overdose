namespace FXOverdose.Core
{
    /// <summary>
    /// 타이틀에서 선택하는 게임 진행 규칙입니다.
    /// Story를 0으로 유지하면 GameMode 필드가 없던 구버전 세이브도 스토리로 호환됩니다.
    /// </summary>
    public enum GameMode
    {
        Story = 0,
        Endless = 1,
        Challenge = 2
    }
}
