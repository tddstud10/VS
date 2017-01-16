using R4nd0mApps.TddStud10.Engine.Core;
using R4nd0mApps.TddStud10.Logger;
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace R4nd0mApps.TddStud10.Hosts.VS
{
    public static class FSharpAsyncExtensions
    {
        public static T RunSynchronously<T>(this FSharpAsync<T> fsAsync)
        {
            return FSharpAsync.RunSynchronously(fsAsync, FSharpOption<int>.None, FSharpOption<CancellationToken>.None);
        }

    }

    public static class EngineLoader
    {
        private static readonly ILogger Logger = LoggerFactory.logger;
        private static readonly ITelemetryClient TelemetryClient = TelemetryClientFactory.telemetryClient;

        private static EngineFileSystemWatcher _efsWatcher;
        private static TddStud10Package _package;
        private static IEngine _engine;
        private static IEngineEvents _engineEvents;

        public static void Load(TddStud10Package package, EngineParams engineParams)
        {
            Logger.LogInfo("Loading Engine with solution {0}", engineParams.SolutionPath);

            _package = package;
            _efsWatcher = EngineFileSystemWatcher.Create(engineParams, () => RunEngine(engineParams));

            _engine = _package.TddStud10Host.GetEngine(); 
            _engineEvents = _package.TddStud10Host.GetEngineEvents();
            _engineEvents.RunStateChanged.AddHandler(_package.OnRunStateChanged);
            _engineEvents.RunStarting.AddHandler(_package.OnRunStarting);
            _engineEvents.RunStepStarting.AddHandler(_package.OnRunStepStarting);
            _engineEvents.RunStepError.AddHandler(_package.OnRunStepError);
            _engineEvents.RunStepEnded.AddHandler(_package.OnRunStepEnded);
            _engineEvents.RunError.AddHandler(_package.OnRunError);
            _engineEvents.RunEnded.AddHandler(_package.OnRunEnded);
        }

        public static bool IsEngineLoaded()
        {
            return _efsWatcher != null;
        }

        public static bool IsEngineEnabled()
        {
            var enabled = IsEngineLoaded() && _efsWatcher.IsEnabled() && _engine.IsEnabled().RunSynchronously();
            Logger.LogInfo("Engine is enabled:{0}", enabled);

            return enabled;
        }

        public static void EnableEngine()
        {
            Logger.LogInfo("Enabling Engine...");
            TelemetryClient.TrackEvent("EnableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _engine.EnableEngine().RunSynchronously();
            _efsWatcher.Enable();
        }

        public static void DisableEngine()
        {
            Logger.LogInfo("Disabling Engine...");
            TelemetryClient.TrackEvent("DisableEngine", new Dictionary<string, string>(), new Dictionary<string, double>());
            _efsWatcher.Disable();
            _engine.DisableEngine().RunSynchronously();
        }

        public static void Unload()
        {
            Logger.LogInfo("Unloading Engine...");

            _engineEvents.RunEnded.RemoveHandler(_package.OnRunEnded);
            _engineEvents.RunError.RemoveHandler(_package.OnRunError);
            _engineEvents.RunStepEnded.RemoveHandler(_package.OnRunStepEnded);
            _engineEvents.RunStepError.RemoveHandler(_package.OnRunStepError);
            _engineEvents.RunStepStarting.RemoveHandler(_package.OnRunStepStarting);
            _engineEvents.RunStarting.RemoveHandler(_package.OnRunStarting);
            _engineEvents.RunStateChanged.RemoveHandler(_package.OnRunStateChanged);

            _efsWatcher.Dispose();
            _efsWatcher = null;
        }

        public static bool IsRunInProgress()
        {
            return _engine.IsRunInProgress().RunSynchronously();
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
                _engine.RunEngine(engineParams).RunSynchronously();
            }
            catch (Exception e)
            {
                Logger.LogError("Exception thrown in InvokeEngine: {0}.", e);
            }
        }
    }
}
