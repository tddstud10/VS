namespace R4nd0mApps.TddStud10.Engine.Core

open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Logger
open System
open System.Threading
open System.Threading.Tasks
open System.ServiceModel

[<CLIMutable>]
type EngineParams = 
    { HostVersion : HostVersion
      EngineConfig : EngineConfig
      SolutionPath : FilePath
      SessionStartTime : DateTime }

type IEngineCallback = 
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunStateChanged : rs:RunState -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunStarting : rsp:RunStartParams -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunStepStarting : rssea:RunStepStartingEventArg -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunStepError : rseea:RunStepErrorEventArg -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunStepEnded : rseea:RunStepEndedEventArg -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunError : rfi:RunFailureInfo -> unit
    [<OperationContract(IsOneWay = true)>]
    abstract OnRunEnded : rsp:RunStartParams -> unit

[<CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)>]
type EngineEvents() = 
    let runStateChanged = new Event<_>()
    let runStarting = new Event<_>()
    let runStepStarting = new Event<_>()
    let runStepError = new Event<_>()
    let runStepEnded = new Event<_>()
    let runError = new Event<_>()
    let runEnded = new Event<_>()
    member __.RunStateChanged = runStateChanged.Publish
    member __.RunStarting = runStarting.Publish
    member __.RunStepStarting = runStepStarting.Publish
    member __.RunStepError = runStepError.Publish
    member __.RunStepEnded = runStepEnded.Publish
    member __.RunError = runError.Publish
    member __.RunEnded = runEnded.Publish
    interface IEngineCallback with
        member __.OnRunStateChanged(rs) = runStateChanged.Trigger(rs)
        member __.OnRunStarting(rsp) = runStarting.Trigger(rsp)
        member __.OnRunStepStarting(rssea) = runStepStarting.Trigger(rssea)
        member __.OnRunStepError(rseea) = runStepError.Trigger(rseea)
        member __.OnRunStepEnded(rseea) = runStepEnded.Trigger(rseea)
        member __.OnRunError(e) = runError.Trigger(e)
        member __.OnRunEnded(rsp) = runEnded.Trigger(rsp)

[<ServiceContract(CallbackContract = typeof<IEngineCallback>)>]
type IEngine = 
    [<OperationContract>]
    abstract Connect : unit -> unit
    [<OperationContract>]
    abstract Disconnect : unit -> unit
    [<OperationContract>]
    abstract DisableEngine : unit -> unit
    [<OperationContract>]
    abstract IsEnabled : unit -> bool
    [<OperationContract>]
    abstract EnableEngine : unit -> unit
    [<OperationContract>]
    abstract RunEngine : ep:EngineParams -> unit
    [<OperationContract>]
    abstract IsRunInProgress : unit -> bool

[<ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)>]
type Engine(dataStore : IDataStore, cb : IEngineCallback option) as x = 
    let logger = LoggerFactory.logger
    let engineParams : EngineParams option ref = ref None

    let cbs : IEngineCallback list ref = ref (cb |> Option.fold (fun _ e -> [ e ]) [])
    let invokeCbs f = !cbs |> List.iter (fun cb -> Common.safeExec (fun () -> f cb))

    let runner = 
        TddStud10Runner.Create x (R4nd0mApps.TddStud10.Engine.Engine.CreateRunSteps(Func<_, _>(dataStore.FindTest)))
    let currentRun : Task<_> ref = ref (null :> _)
    let currentRunCts : CancellationTokenSource ref = ref (null :> _)
    let isEnabled : bool ref = ref false
    
    new() = new Engine(DataStore.Instance, None)
    interface IEngine with
        
        member __.Connect() = 
            logger.logInfof "|ENGINE ACCESS| =====> Connect"
            let cb = OperationContext.Current.GetCallbackChannel<IEngineCallback>()
            if (!cbs
                |> List.exists ((=) cb)
                |> not)
            then cbs := OperationContext.Current.GetCallbackChannel<IEngineCallback>() :: !cbs
            let rsc = fun ea -> (invokeCbs (fun cb -> cb.OnRunStateChanged(ea)))
            let rs = fun ea -> 
                        (invokeCbs (fun cb -> 
                            cb.OnRunStarting(ea)
                            dataStore.UpdateRunStartParams(ea)))
            let rss = fun ea -> (invokeCbs (fun cb -> cb.OnRunStepStarting(ea)))
            let rser = fun ea -> (invokeCbs (fun cb -> cb.OnRunStepError(ea)))
            let rse = fun ea -> 
                        (invokeCbs (fun cb -> 
                            cb.OnRunStepEnded(ea)
                            dataStore.UpdateData(ea.rsr.runData)))
            let rer = fun ea -> (invokeCbs (fun cb -> cb.OnRunError(ea)))
            let re = fun ea -> (invokeCbs (fun cb -> cb.OnRunEnded(ea)))
            runner.AttachHandlers rsc rs rss rser rse rer re

        member __.EnableEngine() = 
            logger.logInfof "|ENGINE ACCESS| =====> EnableEngine"
            isEnabled := true
        member __.IsEnabled() = 
            logger.logInfof "|ENGINE ACCESS| =====> IsEnabled"
            !isEnabled
        
        member __.DisableEngine() = 
            logger.logInfof "|ENGINE ACCESS| =====> DisableEngine"
            isEnabled := false
            dataStore.ResetData()
        
        member __.Disconnect() = 
            logger.logInfof "|ENGINE ACCESS| =====> Disconnect"
            runner.DetachHandlers()
            let cb = OperationContext.Current.GetCallbackChannel<IEngineCallback>()
            cbs := !cbs |> List.filter ((<>) cb)
        
        member __.IsRunInProgress() = 
            logger.logInfof "|ENGINE ACCESS| =====> IsRunInProgress"
            not 
                (currentRun.Value = null 
                 || (currentRun.Value.Status = TaskStatus.Canceled || currentRun.Value.Status = TaskStatus.Faulted 
                     || currentRun.Value.Status = TaskStatus.RanToCompletion))
        member x.RunEngine ep = 
            engineParams := Some ep
            logger.logInfof "|ENGINE ACCESS| =====> RunEngine with EngineParams: %A" engineParams
            try 
                if not isEnabled.Value then logger.logInfof "Cannot start engine. Host has denied request."
                else if (x :> IEngine).IsRunInProgress() then 
                    logger.logInfof "Cannot start engine. A run is already in progress."
                else 
                    logger.logInfof "Engine: Going to trigger a run."
                    // NOTE: Note fix the CT design once we wire up.
                    if (currentRunCts.Value <> null) then currentRunCts.Value.Dispose()
                    currentRunCts := new CancellationTokenSource()
                    currentRun 
                    := runner.StartAsync ep.EngineConfig ep.SessionStartTime 
                           ep.SolutionPath currentRunCts.Value.Token
            with e -> logger.logErrorf "Exception thrown in InvokeEngine: %O." e
    
    interface IRunExecutorHost with
        member __.CanContinue() = !isEnabled
        member __.HostVersion =
            match !engineParams with
            | Some ep -> ep.HostVersion
            | _ -> failwithf "IRunExecutorHost called outside the context of a run!"
        member __.RunStateChanged(_) = ()
