using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.Collections.Generic;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    // NOTE: This entity will continue to be alive till we figure out the final trigger mechanism(s)
    // Till then we will just have to carefully do/undo the pairs of functionality at appropriate places
    public static class EngineLoader
    {
        private static readonly ILogger Logger = LoggerFactory.logger;
        private static readonly ITelemetryClient TelemetryClient = TelemetryClientFactory.telemetryClient;

        private static EngineFileSystemWatcher _efsWatcher;

        private static IEngine _engine;

        public static void Load(IEngineCallback callback, EngineParams engineParams)
        {
            Logger.LogInfo("Loading Engine with solution {0}", engineParams.SolutionPath);

            _engine = new Engine.Core.Engine();
            _engine.Load(callback, engineParams);

            _efsWatcher = EngineFileSystemWatcher.Create(engineParams, RunEngine);
        }

        public static bool IsEngineLoaded()
        {
            return _efsWatcher != null;
        }

        public static bool IsEngineEnabled()
        {
            var enabled = IsEngineLoaded() && _efsWatcher.IsEnabled() && _engine.IsEnabled();
            Logger.LogInfo("Engine is enabled:{0}", enabled);

            return enabled;
        }

        public static void EnableEngine()
        {
            Logger.LogInfo("Enabling Engine...");
            TelemetryClient.TrackEvent("EnableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _engine.EnableEngine();
            _efsWatcher.Enable();
        }

        public static void DisableEngine()
        {
            Logger.LogInfo("Disabling Engine...");
            TelemetryClient.TrackEvent("DisableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _efsWatcher.Disable();
            _engine.DisableEngine();
        }

        public static void Unload()
        {
            Logger.LogInfo("Unloading Engine...");

            _efsWatcher.Dispose();
            _efsWatcher = null;

            _engine.Unload();
        }

        public static bool IsRunInProgress()
        {
            return _engine.IsRunInProgress();
        }

        private static void RunEngine()
        {
            try
            {
                if (IsRunInProgress())
                {
                    Logger.LogInfo("Cannot start engine. A run is already in progress.");
                    return;
                }

                Logger.LogInfo("--------------------------------------------------------------------------------");
                Logger.LogInfo("EngineLoader: Going to trigger a run.");
                _engine.RunEngine();
            }
            catch (Exception e)
            {
                Logger.LogError("Exception thrown in InvokeEngine: {0}.", e);
            }
        }
    }
}
