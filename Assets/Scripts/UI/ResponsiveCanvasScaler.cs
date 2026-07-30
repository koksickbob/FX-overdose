using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 16:9 기준의 루트 UI 캔버스를 현재 화면 비율에 맞춰 스케일합니다.
/// 울트라와이드는 높이, 16:9보다 좁은 화면은 너비를 기준으로 삼아 UI가 잘리지 않게 합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasScaler))]
public sealed class ResponsiveCanvasScaler : MonoBehaviour
{
    private static readonly Vector2 LandscapeReferenceResolution = new(1920f, 1080f);

    private CanvasScaler canvasScaler;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Awake()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        ApplyScalePolicy();
    }

    private void OnEnable()
    {
        ApplyScalePolicy();
    }

    private void Update()
    {
        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
        {
            ApplyScalePolicy();
        }
    }

    private void ApplyScalePolicy()
    {
        if (canvasScaler == null)
        {
            canvasScaler = GetComponent<CanvasScaler>();
        }

        if (canvasScaler == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = LandscapeReferenceResolution;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = LandscapeReferenceResolution.x / LandscapeReferenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect >= referenceAspect ? 1f : 0f;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
}

/// <summary>씬 및 런타임 생성 루트 캔버스에 반응형 스케일러를 자동 설치합니다.</summary>
internal static class ResponsiveCanvasScalerBootstrap
{
    private const string MonitorName = "[ResponsiveCanvasScaler]";
    private static ResponsiveCanvasScalerMonitor monitor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        monitor = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureMonitor();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMonitor();
        InstallOnLandscapeRootCanvases();
    }

    private static void EnsureMonitor()
    {
        if (monitor != null)
        {
            return;
        }

        GameObject monitorObject = new(MonitorName);
        Object.DontDestroyOnLoad(monitorObject);
        monitor = monitorObject.AddComponent<ResponsiveCanvasScalerMonitor>();
    }

    internal static void InstallOnLandscapeRootCanvases()
    {
        CanvasScaler[] scalers =
            Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include);

        foreach (CanvasScaler scaler in scalers)
        {
            if (scaler == null || scaler.TryGetComponent(out ResponsiveCanvasScaler _))
            {
                continue;
            }

            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas == null ||
                !canvas.isRootCanvas ||
                canvas.renderMode == RenderMode.WorldSpace ||
                scaler.referenceResolution.x < scaler.referenceResolution.y)
            {
                continue;
            }

            scaler.gameObject.AddComponent<ResponsiveCanvasScaler>();
        }
    }
}

internal sealed class ResponsiveCanvasScalerMonitor : MonoBehaviour
{
    private const float ScanInterval = 1f;
    private float nextScanTime;

    private void Start()
    {
        Scan();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScanTime)
        {
            Scan();
        }
    }

    private void Scan()
    {
        ResponsiveCanvasScalerBootstrap.InstallOnLandscapeRootCanvases();
        nextScanTime = Time.unscaledTime + ScanInterval;
    }
}
