namespace R4nd0mApps.TddStud10.Engine.Core

open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Logger
open System
open System.Threading
open System.Threading.Tasks

type EngineParams = 
    { HostVersion : HostVersion
      EngineConfig : EngineConfig
      SolutionPath : FilePath
      SessionStartTime : DateTime }

type IEngineCallback = 
    abstract OnRunStateChanged : RunState -> unit
    abstract OnRunStarting : RunStartParams -> unit
    abstract OnRunStepStarting : RunStepStartingEventArg -> unit
    abstract OnRunStepError : RunStepErrorEventArg -> unit
    abstract OnRunStepEnded : RunStepEndedEventArg -> unit
    abstract OnRunError : Exception -> unit
    abstract OnRunEnded : RunStartParams -> unit

type IEngine = 
    abstract Load : IEngineCallback -> EngineParams -> unit
    abstract Unload : unit -> unit
    abstract DisableEngine : unit -> unit
    abstract IsEnabled : unit -> bool
    abstract EnableEngine : unit -> unit
    abstract RunEngine : unit -> unit
    abstract IsRunInProgress : unit -> bool

type Engine() as x = 
    let logger = LoggerFactory.logger
    let telemetryClient = TelemetryClientFactory.telemetryClient
    let dataStore = DataStore.Instance
    let runner = 
        TddStud10Runner.Create x (R4nd0mApps.TddStud10.Engine.Engine.CreateRunSteps(Func<_, _>(dataStore.FindTest)))
    let currentRun : Task<_> ref = ref (null :> _)
    let currentRunCts : CancellationTokenSource ref = ref (null :> _)
    let isEnabled : bool ref = ref false
    let engineParams : EngineParams option ref = ref None
    let runStateChangedHandler : Handler<RunState> ref = ref null
    let runStartingHandler : Handler<RunStartParams> ref = ref null
    let runStepStartingHandler : Handler<RunStepStartingEventArg> ref = ref null
    let runStepErrorHandler : Handler<RunStepErrorEventArg> ref = ref null
    let runStepEndedHandler : Handler<RunStepEndedEventArg> ref = ref null
    let runErrorHandler : Handler<Exception> ref = ref null
    let runEndedHandler : Handler<RunStartParams> ref = ref null
    
    interface IEngine with
        
        member __.Load cb ep = 
            logger.logInfof "Loading Engine with solution %O" ep.SolutionPath
            engineParams := Some ep
            runStateChangedHandler := Handler<_>(fun _ ea -> cb.OnRunStateChanged(ea))
            runStartingHandler := Handler<_>(fun _ ea -> 
                                      cb.OnRunStarting(ea)
                                      dataStore.UpdateRunStartParams(ea))
            runStepStartingHandler := Handler<_>(fun _ ea -> cb.OnRunStepStarting(ea))
            runStepErrorHandler := Handler<_>(fun _ ea -> cb.OnRunStepError(ea))
            runStepEndedHandler := Handler<_>(fun _ ea -> 
                                       cb.OnRunStepEnded(ea)
                                       dataStore.UpdateData(ea.rsr.runData))
            runErrorHandler := Handler<_>(fun _ ea -> cb.OnRunError(ea))
            runEndedHandler := Handler<_>(fun _ ea -> cb.OnRunEnded(ea))
            runner.AttachHandlers !runStateChangedHandler !runStartingHandler !runStepStartingHandler 
                !runStepErrorHandler !runStepEndedHandler !runErrorHandler !runEndedHandler
        
        member __.EnableEngine() = isEnabled := true
        member __.IsEnabled() = !isEnabled
        
        member __.DisableEngine() = 
            isEnabled := false
            dataStore.ResetData()
        
        member __.Unload() = 
            logger.LogInfo("Unloading Engine...")
            runner.DetachHandlers !runEndedHandler !runErrorHandler !runStepEndedHandler !runStepErrorHandler 
                !runStepStartingHandler !runStartingHandler !runStateChangedHandler
            runEndedHandler := null
            runErrorHandler := null
            runStepEndedHandler := null
            runStepErrorHandler := null
            runStepStartingHandler := null
            runStartingHandler := null
            runStateChangedHandler := null
        
        member __.IsRunInProgress() = 
            not 
                (currentRun.Value = null 
                 || (currentRun.Value.Status = TaskStatus.Canceled || currentRun.Value.Status = TaskStatus.Faulted 
                     || currentRun.Value.Status = TaskStatus.RanToCompletion))
        member x.RunEngine() = 
            try 
                if not isEnabled.Value then logger.logInfof "Cannot start engine. Host has denied request."
                else if (x :> IEngine).IsRunInProgress() then 
                    logger.logInfof "Cannot start engine. A run is already in progress."
                else 
                    logger.logInfof "--------------------------------------------------------------------------------"
                    logger.logInfof "EngineLoader: Going to trigger a run."
                    // NOTE: Note fix the CT design once we wire up.
                    if (currentRunCts.Value <> null) then currentRunCts.Value.Dispose()
                    currentRunCts := new CancellationTokenSource()
                    currentRun 
                    := runner.StartAsync engineParams.Value.Value.EngineConfig engineParams.Value.Value.SessionStartTime 
                           engineParams.Value.Value.SolutionPath currentRunCts.Value.Token
            with e -> logger.logErrorf "Exception thrown in InvokeEngine: %O." e
    
    interface IRunExecutorHost with
        member __.CanContinue() = !isEnabled
        member __.HostVersion = engineParams.Value.Value.HostVersion
        member __.RunStateChanged(_) = ()
