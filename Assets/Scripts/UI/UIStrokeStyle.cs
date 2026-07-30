using UnityEngine;

/// <summary>TopBar를 제외한 게임 UI에서 공유하는 외곽선 규칙입니다.</summary>
public static class UIStrokeStyle
{
    public const float Width = 3f;
    public const float CompactHudHeight = 69f;
    public const float ScreenEdgeMargin = 24f;
    public const float CenterGutterHalf = 12f;
    public static readonly Vector2 EffectDistance = new(Width, -Width);
    public static readonly Color DefaultColor = new Color32(59, 75, 102, 255);
}
