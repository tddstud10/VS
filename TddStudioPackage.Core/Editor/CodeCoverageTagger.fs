namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.EditorFrameworkExtensions
open System.Threading
open R4nd0mApps.TddStud10.Engine.Core

[<AutoOpen>]
module TagAggregator =
    type TagAggregator<'T> when 'T :> ITag =
        | SnapshotSnapsToTagSpan of SnapshotSnapsToTagSpan<'T>
        | ITagAggregator of ITagAggregator<'T>

    let dispose =
        function
        | SnapshotSnapsToTagSpan _ -> ()
        | ITagAggregator i -> i.Dispose()

    let snapshotSnapsToTagSpan =
        function
        | SnapshotSnapsToTagSpan s -> s
        | ITagAggregator i -> i.getTagSpans

type CodeCoverageTagger(buffer : ITextBuffer, ta : TagAggregator<_>, dataStore : IXDataStore, dse : XDataStoreEvents) as self = 
    inherit DisposableTagger()
    
    let logger = R4nd0mApps.TddStud10.Logger.LoggerFactory.logger
    let disposed : bool ref = ref false

    let syncContext = SynchronizationContext.Current
    let tagsChanged = Event<_, _>()
    
    let fireTagsChanged _ = 
        logger.logInfof "Firing CodeCoverageTagger.TagsChanged"
        syncContext.Send
            (SendOrPostCallback
                 (fun _ -> 
                 Common.safeExec 
                     (fun () -> 
                     tagsChanged.Trigger
                         (self, 
                          SnapshotSpanEventArgs(SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))))), 
             null)
    
    let spUpdatedSub = dse.SequencePointsUpdated.Subscribe fireTagsChanged
    let ciUpdatedSub = dse.CoverageInfoUpdated.Subscribe fireTagsChanged
    
    override __.Dispose(disposing) = 
        if not disposed.Value then 
            if disposing then 
                TagAggregator.dispose ta
                ciUpdatedSub.Dispose()
                spUpdatedSub.Dispose()
            disposed := true
            base.Dispose(disposing)
        
    interface ITagger<CodeCoverageTag> with
        
        (* NOTE: We are assuming that 
           (1) spans arg has only 1 item and it is a full line in the editor
           (2) Returned TagSpan.Span is the full span, i.e. it is not the set of intersection ranges of Span with failure sequence point. *)
        member __.GetTags(spans : _) : _ = 
            let getTags _ _ = 
                spans
                |> TagAggregator.snapshotSnapsToTagSpan ta
                |> Seq.map (fun tsp -> tsp, tsp.Tag.SptSequencePoint.id |> dataStore.GetRunIdsForTestsCoveringSequencePointId)
                |> Seq.map (fun (tsp, rids) -> 
                       tsp, 
                       rids
                       |> Seq.map (fun rid -> rid.testId)
                       |> Seq.distinct
                       |> Seq.map dataStore.GetResultsForTestId
                       |> Seq.collect id)
                |> Seq.map (fun (tsp, trs) -> 
                       TagSpan<_>(tsp.Span, 
                                  { CodeCoverageTag.CctSeqPoint = tsp.Tag.SptSequencePoint
                                    CctTestResults = trs }) :> ITagSpan<_>)
            buffer.FilePath |> Option.fold getTags Seq.empty
        
        [<CLIEvent>]
        member __.TagsChanged = tagsChanged.Publish
