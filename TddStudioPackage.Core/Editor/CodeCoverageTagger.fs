namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.EditorFrameworkExtensions
open System.Threading
open R4nd0mApps.TddStud10.Engine.Core
open System.Collections.Generic

type CodeCoverageTagger(buffer : ITextBuffer, ta : TagAggregator<_>, dataStore : IXDataStore, dse : XDataStoreEvents) as self = 
    inherit DisposableTagger()
    
    let logger = R4nd0mApps.TddStud10.Logger.LoggerFactory.logger
    let disposed : bool ref = ref false
    let syncContext = SynchronizationContext.Current
    let tagsChanged = Event<_, _>()
    let spTrCache : IDictionary<SequencePointId, DTestResult[]> ref = ref (dict[])
    
    let clearCache() =
        spTrCache := dict[]
    
    let fireTagsChanged _ = 
        logger.logInfof "Firing CodeCoverageTagger.TagsChanged"
        clearCache()
        syncContext.Send
            (SendOrPostCallback
                 (fun _ -> 
                 Common.safeExec 
                     (fun () -> 
                     tagsChanged.Trigger
                         (self, 
                          SnapshotSpanEventArgs(SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))))), 
             null)

    let spTagsChangedSub = TagAggregator.subscribeToTagsChanged fireTagsChanged ta 
    let ciUpdatedSub = dse.CoverageInfoUpdated.Subscribe fireTagsChanged
    
    override __.Dispose(disposing) = 
        if not disposed.Value then 
            if disposing then 
                TagAggregator.dispose ta
                ciUpdatedSub.Dispose()
                spTagsChangedSub.Dispose()
            disposed := true
            base.Dispose(disposing)
        
    interface ITagger<CodeCoverageTag> with
        
        (* NOTE: We are assuming that 
           (1) spans arg has only 1 item and it is a full line in the editor
           (2) Returned TagSpan.Span is the full span, i.e. it is not the set of intersection ranges of Span with failure sequence point. *)
        member __.GetTags(spans : _) : _ = 
            let getTags _ _ = 
                let tspans =
                    spans
                    |> TagAggregator.snapshotSnapsToTagSpan ta
                    |> Seq.toList

                let cacheMiss = 
                    tspans 
                    |> List.filter (fun t -> t.Tag.SptSequencePoint.id |> spTrCache.Value.ContainsKey |> not)
            
                if cacheMiss |> Seq.isEmpty |> not then
                    let fromDataStore = 
                        cacheMiss
                        |> Seq.map (fun tsp -> tsp.Tag.SptSequencePoint.id)
                        |> dataStore.GetTestResultsForSequencepointsIds

                    spTrCache := 
                        spTrCache.Value 
                        |> Seq.append fromDataStore
                        |> Seq.map (fun kv -> kv.Key, kv.Value)
                        |> dict

                tspans
                |> Seq.map (fun tsp -> 
                       let trs = (tsp.Tag.SptSequencePoint.id, !spTrCache) ||> Dict.tryGetValue [||] id
                       TagSpan<_>(tsp.Span, 
                                  { CctSeqPoint = tsp.Tag.SptSequencePoint
                                    CctTestResults = trs }) :> ITagSpan<_>)
            buffer.FilePath |> Option.fold getTags Seq.empty
        
        [<CLIEvent>]
        member __.TagsChanged = tagsChanged.Publish
