using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.Collections.Generic;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    public static class EngineLoader
    {
        private static readonly ILogger Logger = LoggerFactory.logger;
        private static readonly ITelemetryClient TelemetryClient = TelemetryClientFactory.telemetryClient;

        private static EngineFileSystemWatcher _efsWatcher;
        private static TddStud10Package _package;

        public static void Load(TddStud10Package package, EngineParams engineParams)
        {
            Logger.LogInfo("Loading Engine with solution {0}", engineParams.SolutionPath);

            _package = package;
            _efsWatcher = EngineFileSystemWatcher.Create(engineParams, () => RunEngine(engineParams));

            _package.Engine.Events.RunStateChanged.AddHandler(_package.OnRunStateChanged);
            _package.Engine.Events.RunStarting.AddHandler(_package.OnRunStarting);
            _package.Engine.Events.RunStepStarting.AddHandler(_package.OnRunStepStarting);
            _package.Engine.Events.RunStepError.AddHandler(_package.OnRunStepError);
            _package.Engine.Events.RunStepEnded.AddHandler(_package.OnRunStepEnded);
            _package.Engine.Events.RunError.AddHandler(_package.OnRunError);
            _package.Engine.Events.RunEnded.AddHandler(_package.OnRunEnded);
        }

        public static bool IsEngineLoaded()
        {
            return _efsWatcher != null;
        }

        public static bool IsEngineEnabled()
        {
            var enabled = IsEngineLoaded() && _efsWatcher.IsEnabled() && _package.Engine.Server.IsEnabled();
            Logger.LogInfo("Engine is enabled:{0}", enabled);

            return enabled;
        }

        public static void EnableEngine()
        {
            Logger.LogInfo("Enabling Engine...");
            TelemetryClient.TrackEvent("EnableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _package.Engine.Server.EnableEngine();
            _efsWatcher.Enable();
        }

        public static void DisableEngine()
        {
            Logger.LogInfo("Disabling Engine...");
            TelemetryClient.TrackEvent("DisableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _efsWatcher.Disable();
            _package.Engine.Server.DisableEngine();
        }

        public static void Unload()
        {
            Logger.LogInfo("Unloading Engine...");

            _package.Engine.Events.RunEnded.RemoveHandler(_package.OnRunEnded);
            _package.Engine.Events.RunError.RemoveHandler(_package.OnRunError);
            _package.Engine.Events.RunStepEnded.RemoveHandler(_package.OnRunStepEnded);
            _package.Engine.Events.RunStepError.RemoveHandler(_package.OnRunStepError);
            _package.Engine.Events.RunStepStarting.RemoveHandler(_package.OnRunStepStarting);
            _package.Engine.Events.RunStarting.RemoveHandler(_package.OnRunStarting);
            _package.Engine.Events.RunStateChanged.RemoveHandler(_package.OnRunStateChanged);

            _efsWatcher.Dispose();
            _efsWatcher = null;

            _package.Engine.Server.Disconnect();
        }

        public static bool IsRunInProgress()
        {
            return _package.Engine.Server.IsRunInProgress();
        }

        private static void RunEngine(EngineParams engineParams)
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
                _package.Engine.Server.RunEngine(engineParams);
            }
            catch (Exception e)
            {
                Logger.LogError("Exception thrown in InvokeEngine: {0}.", e);
            }
        }
    }
}
