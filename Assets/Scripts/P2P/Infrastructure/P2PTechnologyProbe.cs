using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>
    /// Steam P2P 구현에 필요한 선택 패키지가 실제 런타임에 로드됐는지 확인합니다.
    /// 패키지 타입을 직접 참조하지 않으므로 설치 전에도 기존 싱글플레이 빌드를 깨지 않습니다.
    /// </summary>
    public static class P2PTechnologyProbe
    {
        // 패키지 타입을 직접 참조하는 대신 로드된 어셈블리 이름으로 설치 상태를 판별합니다.
        // 덕분에 선택 패키지가 빠진 환경에서도 이 진단 코드 자체는 컴파일될 수 있습니다.
        private const string NetcodeAssemblyPrefix = "Unity.Netcode";
        private const string SteamworksAssemblyPrefix = "com.rlabrecque.steamworks.net";
        private const string SteamworksFallbackAssemblyPrefix = "Steamworks.NET";

        /// <summary>
        /// 한 번의 기술 스택 검사 결과를 변경 불가능한 값으로 묶어 전달합니다.
        /// 실제 Steam 로그인 성공 여부가 아니라 필요한 어셈블리의 로드 여부만 나타냅니다.
        /// </summary>
        public readonly struct Result
        {
            public Result(
                string unityVersion,
                RuntimePlatform platform,
                bool is64BitProcess,
                bool hasNetcodeForGameObjects,
                bool hasSteamworksNet,
                bool hasSteamTransport)
            {
                UnityVersion = unityVersion;
                Platform = platform;
                Is64BitProcess = is64BitProcess;
                HasNetcodeForGameObjects = hasNetcodeForGameObjects;
                HasSteamworksNet = hasSteamworksNet;
                HasSteamTransport = hasSteamTransport;
            }

            public string UnityVersion { get; }
            public RuntimePlatform Platform { get; }
            public bool Is64BitProcess { get; }
            public bool HasNetcodeForGameObjects { get; }
            public bool HasSteamworksNet { get; }
            public bool HasSteamTransport { get; }

            // NGO와 Steamworks.NET은 P2P 코드가 컴파일되기 위한 최소 구성입니다.
            public bool HasCorePackages => HasNetcodeForGameObjects && HasSteamworksNet;

            // Transport까지 로드돼야 Steam 연결 프로토타입을 실행할 수 있습니다.
            public bool IsReadyForSteamConnectionPrototype => HasCorePackages && HasSteamTransport;
        }

        /// <summary>
        /// 현재 AppDomain에 로드된 어셈블리를 조사하여 P2P 기술 준비 상태를 반환합니다.
        /// </summary>
        public static Result Evaluate()
        {
            string[] assemblyNames = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetName().Name ?? string.Empty)
                .ToArray();

            bool hasNgo = assemblyNames.Any(name =>
                name.StartsWith(NetcodeAssemblyPrefix, StringComparison.OrdinalIgnoreCase));

            bool hasSteamworks = assemblyNames.Any(name =>
                name.StartsWith(SteamworksAssemblyPrefix, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(SteamworksFallbackAssemblyPrefix, StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Steamworks", StringComparison.OrdinalIgnoreCase));

            // 채택한 패키지의 실제 어셈블리 이름은
            // "SteamNetworkingSockets Transport for Netcode for GameObjects"입니다.
            // 특정 구현명에 종속되지 않도록 Steam과 Transport가 함께 포함됐는지 검사합니다.
            bool hasSteamTransport = assemblyNames.Any(name =>
                name.Contains("Steam", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("Transport", StringComparison.OrdinalIgnoreCase));

            return new Result(
                Application.unityVersion,
                Application.platform,
                Environment.Is64BitProcess,
                hasNgo,
                hasSteamworks,
                hasSteamTransport);
        }

        public static string BuildReport(Result result)
        {
            var builder = new StringBuilder(320);
            builder.AppendLine("[P2P Technology Probe]");
            builder.AppendLine($"Unity: {result.UnityVersion}");
            builder.AppendLine($"Platform: {result.Platform} / 64-bit: {result.Is64BitProcess}");
            builder.AppendLine($"Netcode for GameObjects: {Format(result.HasNetcodeForGameObjects)}");
            builder.AppendLine($"Steamworks.NET: {Format(result.HasSteamworksNet)}");
            builder.AppendLine($"Steam Transport: {Format(result.HasSteamTransport)}");
            builder.AppendLine($"Core packages ready: {Format(result.HasCorePackages)}");
            builder.Append($"Steam connection prototype ready: {Format(result.IsReadyForSteamConnectionPrototype)}");
            return builder.ToString();
        }

        private static string Format(bool value)
        {
            return value ? "READY" : "MISSING";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 릴리스 빌드에서는 진단 로그를 남기지 않아 불필요한 환경 정보 노출을 피합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void LogDevelopmentReport()
        {
            Debug.Log(BuildReport(Evaluate()));
        }
#endif
    }
}
