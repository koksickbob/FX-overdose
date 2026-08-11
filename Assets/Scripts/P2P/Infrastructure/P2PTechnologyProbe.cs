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
        private const string NetcodeAssemblyPrefix = "Unity.Netcode";
        private const string SteamworksAssemblyPrefix = "com.rlabrecque.steamworks.net";
        private const string SteamworksFallbackAssemblyPrefix = "Steamworks.NET";

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
            public bool HasCorePackages => HasNetcodeForGameObjects && HasSteamworksNet;
            public bool IsReadyForSteamConnectionPrototype => HasCorePackages && HasSteamTransport;
        }

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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void LogDevelopmentReport()
        {
            Debug.Log(BuildReport(Evaluate()));
        }
#endif
    }
}
