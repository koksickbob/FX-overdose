using UnityEngine;

namespace FXOverdose.Core
{
    public static class SystemSettingsManager
    {
        public static readonly int[] FpsOptions = { 30, 60, 75, 144, 165, 240 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ApplyTargetFPS();
        }

        public static void ApplyTargetFPS()
        {
            // Default 60 for mobile/PC balance if not set
            int defaultFps = 60; 
            int target = PlayerPrefs.GetInt("TargetFPS", defaultFps);
            Application.targetFrameRate = target;
            Debug.Log($"[SystemSettingsManager] 프레임 레이트 제한이 {target} FPS로 적용되었습니다.");
        }

        public static int GetCurrentFPS()
        {
            return PlayerPrefs.GetInt("TargetFPS", 60); // Match defaultFps
        }

        public static void SetFPS(int fps)
        {
            PlayerPrefs.SetInt("TargetFPS", fps);
            PlayerPrefs.Save();
            ApplyTargetFPS();
        }
    }
}
