namespace R4nd0mApps.TddStud10.Engine.Core

open R4nd0mApps.TddStud10.Common.Domain
open System

[<AllowNullLiteral>]
type IXDataStoreCallback = 
    abstract OnTestCasesUpdated : PerDocumentLocationDTestCases -> unit
    abstract OnSequencePointsUpdated : PerDocumentSequencePoints -> unit
    abstract OnTestResultsUpdated : PerTestIdDResults -> unit
    abstract OnTestFailureInfoUpdated : PerDocumentLocationTestFailureInfo -> unit
    abstract OnCoverageInfoUpdated : PerSequencePointIdTestRunId -> unit

type XDataStoreEvents() = 
    let testCasesUpdated = new Event<_>()
    let sequencePointsUpdated = new Event<_>()
    let testResultsUpdated = new Event<_>()
    let testFailureInfoUpdated = new Event<_>()
    let coverageInfoUpdated = new Event<_>()
    member __.TestCasesUpdated = testCasesUpdated.Publish
    member __.SequencePointsUpdated = sequencePointsUpdated.Publish
    member __.TestResultsUpdated = testResultsUpdated.Publish
    member __.TestFailureInfoUpdated = testFailureInfoUpdated.Publish
    member __.CoverageInfoUpdated = coverageInfoUpdated.Publish
    interface IXDataStoreCallback with
        member __.OnCoverageInfoUpdated(pspidtrid) = coverageInfoUpdated.Trigger(pspidtrid)
        member __.OnSequencePointsUpdated(pdsp) = sequencePointsUpdated.Trigger(pdsp)
        member __.OnTestCasesUpdated(pdltc) = testCasesUpdated.Trigger(pdltc)
        member __.OnTestFailureInfoUpdated(pdltfi) = testFailureInfoUpdated.Trigger(pdltfi)
        member __.OnTestResultsUpdated(ptidr) = testResultsUpdated.Trigger(ptidr)

type IXDataStore = 
    abstract Connect : IXDataStoreCallback -> unit
    abstract Disconnect : unit -> unit
    abstract UpdateRunStartParams : RunStartParams -> unit
    abstract UpdateData : RunData -> unit
    abstract ResetData : unit -> unit
    abstract FindTest : DocumentLocation -> seq<DTestCase>
    abstract GetSequencePointsForFile : FilePath -> seq<SequencePoint>
    abstract FindTestFailureInfo : DocumentLocation -> seq<TestFailureInfo>
    abstract GetRunIdsForTestsCoveringSequencePointId : SequencePointId -> seq<TestRunId>
    abstract GetResultsForTestId : TestId -> seq<DTestResult>

type XDataStore(dataStore : IDataStore) = 
    let testCasesUpdatedSub : IDisposable ref = ref null
    let sequencePointsUpdatedSub : IDisposable ref = ref null
    let testResultsUpdatedSub : IDisposable ref = ref null
    let testFailureInfoUpdatedSub : IDisposable ref = ref null
    let coverageInfoUpdatedSub : IDisposable ref = ref null
    new() = new XDataStore(DataStore.Instance)
    interface IXDataStore with
        
        member __.Connect(cb : IXDataStoreCallback) : unit = 
            testCasesUpdatedSub := dataStore.TestCasesUpdated.Subscribe cb.OnTestCasesUpdated
            sequencePointsUpdatedSub := dataStore.SequencePointsUpdated.Subscribe cb.OnSequencePointsUpdated
            testResultsUpdatedSub := dataStore.TestResultsUpdated.Subscribe cb.OnTestResultsUpdated
            testFailureInfoUpdatedSub := dataStore.TestFailureInfoUpdated.Subscribe cb.OnTestFailureInfoUpdated
            coverageInfoUpdatedSub := dataStore.CoverageInfoUpdated.Subscribe cb.OnCoverageInfoUpdated
        
        member __.Disconnect() : unit = 
            coverageInfoUpdatedSub.Value.Dispose()
            testFailureInfoUpdatedSub.Value.Dispose()
            testResultsUpdatedSub.Value.Dispose()
            sequencePointsUpdatedSub.Value.Dispose()
            testCasesUpdatedSub.Value.Dispose()
        
        member __.FindTest(dl : DocumentLocation) : seq<DTestCase> = dataStore.FindTest dl
        member __.FindTestFailureInfo(dl : DocumentLocation) : seq<TestFailureInfo> = dataStore.FindTestFailureInfo dl
        member __.GetResultsForTestId(tid : TestId) : seq<DTestResult> = dataStore.GetResultsForTestId tid
        member __.GetRunIdsForTestsCoveringSequencePointId(spid : SequencePointId) : seq<TestRunId> = 
            dataStore.GetRunIdsForTestsCoveringSequencePointId spid
        member __.GetSequencePointsForFile(path : FilePath) : seq<SequencePoint> = 
            dataStore.GetSequencePointsForFile path
        member __.ResetData() : unit = dataStore.ResetData()
        member __.UpdateData(rd : RunData) : unit = dataStore.UpdateData rd
        member __.UpdateRunStartParams(rsp : RunStartParams) : unit = dataStore.UpdateRunStartParams rsp
