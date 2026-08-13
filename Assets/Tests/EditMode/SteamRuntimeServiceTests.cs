using FXOverdose.P2P.Steam;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class SteamRuntimeServiceTests
    {
        [Test]
        public void Initialize_WithValidSteamUser_BecomesReady()
        {
            var backend = new FakeSteamBackend();
            var service = new SteamRuntimeService(backend);

            bool result = service.Initialize(480, false);

            Assert.That(result, Is.True);
            Assert.That(service.Status.State, Is.EqualTo(SteamRuntimeState.Ready));
            Assert.That(service.Status.AppId, Is.EqualTo(480));
            Assert.That(service.Status.SteamId, Is.EqualTo(76561198000000000UL));
            Assert.That(service.Status.PersonaName, Is.EqualTo("Test Trader"));
            Assert.That(service.CanUseP2P, Is.True);
            Assert.That(backend.RelayInitializeCount, Is.EqualTo(1));
        }

        [Test]
        public void Initialize_WhenSteamClientIsUnavailable_DisablesOnlyP2P()
        {
            var backend = new FakeSteamBackend { InitializeResult = false };
            var service = new SteamRuntimeService(backend);

            bool result = service.Initialize(480, false);

            Assert.That(result, Is.False);
            Assert.That(service.Status.State, Is.EqualTo(SteamRuntimeState.Failed));
            Assert.That(service.Status.Error, Is.EqualTo(SteamInitializationError.SteamClientUnavailable));
            Assert.That(service.CanUseP2P, Is.False);
            Assert.That(backend.ShutdownCount, Is.Zero);
        }

        [Test]
        public void Initialize_WhenUserIsInvalid_ShutsDownPartiallyInitializedApi()
        {
            var backend = new FakeSteamBackend { SteamId = 0 };
            var service = new SteamRuntimeService(backend);

            service.Initialize(480, false);

            Assert.That(service.Status.Error, Is.EqualTo(SteamInitializationError.InvalidSteamUser));
            Assert.That(backend.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void Initialize_WhenAlreadyReady_DoesNotInitializeTwice()
        {
            var backend = new FakeSteamBackend();
            var service = new SteamRuntimeService(backend);

            service.Initialize(480, false);
            service.Initialize(480, false);

            Assert.That(backend.InitializeCount, Is.EqualTo(1));
            Assert.That(backend.RelayInitializeCount, Is.EqualTo(1));
        }

        [Test]
        public void PumpCallbacks_OnlyRunsWhileReady()
        {
            var backend = new FakeSteamBackend();
            var service = new SteamRuntimeService(backend);

            service.PumpCallbacks();
            service.Initialize(480, false);
            service.PumpCallbacks();
            service.Shutdown();
            service.PumpCallbacks();

            Assert.That(backend.CallbackCount, Is.EqualTo(1));
        }

        [Test]
        public void PumpCallbacks_WhenBackendThrows_DisablesP2PAndShutsDown()
        {
            var backend = new FakeSteamBackend { ThrowOnCallback = true };
            var service = new SteamRuntimeService(backend);

            service.Initialize(480, false);
            service.PumpCallbacks();

            Assert.That(service.Status.State, Is.EqualTo(SteamRuntimeState.Failed));
            Assert.That(service.Status.Error, Is.EqualTo(SteamInitializationError.UnexpectedException));
            Assert.That(service.CanUseP2P, Is.False);
            Assert.That(backend.ShutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_IsIdempotentAndClearsIdentity()
        {
            var backend = new FakeSteamBackend();
            var service = new SteamRuntimeService(backend);

            service.Initialize(480, false);
            service.Shutdown();
            service.Shutdown();

            Assert.That(backend.ShutdownCount, Is.EqualTo(1));
            Assert.That(service.Status.State, Is.EqualTo(SteamRuntimeState.Shutdown));
            Assert.That(service.Status.SteamId, Is.Zero);
            Assert.That(service.CanUseP2P, Is.False);
        }

        [Test]
        public void Initialize_WhenRestartIsRequired_ReportsSpecificReason()
        {
            var backend = new FakeSteamBackend { RestartRequired = true };
            var service = new SteamRuntimeService(backend);

            service.Initialize(480, true);

            Assert.That(service.Status.Error, Is.EqualTo(SteamInitializationError.RestartRequired));
            Assert.That(backend.InitializeCount, Is.Zero);
        }

        private sealed class FakeSteamBackend : ISteamRuntimeBackend
        {
            public bool RestartRequired { get; set; }
            public bool PacksizeValid { get; set; } = true;
            public bool NativeLibraryValid { get; set; } = true;
            public bool InitializeResult { get; set; } = true;
            public uint AppId { get; set; } = 480;
            public ulong SteamId { get; set; } = 76561198000000000UL;
            public string PersonaName { get; set; } = "Test Trader";
            public bool ThrowOnCallback { get; set; }
            public int InitializeCount { get; private set; }
            public int RelayInitializeCount { get; private set; }
            public int CallbackCount { get; private set; }
            public int ShutdownCount { get; private set; }

            public bool RestartAppIfNecessary(uint appId) => RestartRequired;
            public bool IsPacksizeValid() => PacksizeValid;
            public bool IsNativeLibraryValid() => NativeLibraryValid;

            public bool Initialize()
            {
                InitializeCount++;
                return InitializeResult;
            }

            public uint GetAppId() => AppId;
            public ulong GetSteamId() => SteamId;
            public string GetPersonaName() => PersonaName;
            public void InitializeRelayNetworkAccess() => RelayInitializeCount++;
            public void RunCallbacks()
            {
                CallbackCount++;
                if (ThrowOnCallback)
                {
                    throw new System.InvalidOperationException("Callback failure");
                }
            }
            public void Shutdown() => ShutdownCount++;
        }
    }
}
