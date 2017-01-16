namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open R4nd0mApps.TddStud10.Common
open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.EditorFrameworkExtensions
open System.Threading
open R4nd0mApps.TddStud10.Engine.Core
open System.Collections.Generic

type FailurePointTagger(buffer : ITextBuffer, dataStore : IXDataStore, dse : IXDataStoreEvents) as self = 
    inherit DisposableTagger()

    let logger = R4nd0mApps.TddStud10.Logger.LoggerFactory.logger
    let disposed : bool ref = ref false

    let syncContext = SynchronizationContext.Current
    let tagsChanged = Event<_, _>()
    let tfiCache : IDictionary<DocumentLocation, TestFailureInfo[]> ref = ref null
    
    let clearCache() =
        tfiCache := null
    
    let fireTagsChanged _ = 
        logger.logInfof "Firing FailurePointTagger.TagsChanged"
        clearCache()
        syncContext.Send
            (SendOrPostCallback
                 (fun _ -> 
                 Exec.safeExec 
                     (fun () -> 
                     tagsChanged.Trigger
                         (self, 
                          SnapshotSpanEventArgs(SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))))), 
             null)

    let tfiUpdatedSub = dse.TestFailureInfoUpdated.Subscribe fireTagsChanged

    override __.Dispose(disposing) = 
        if not disposed.Value then 
            if disposing then 
                tfiUpdatedSub.Dispose()
            disposed := true
            base.Dispose(disposing)

    interface ITagger<FailurePointTag> with
        
        (* NOTE: We are assuming that 
           (1) spans arg has only 1 item and it is a full line in the editor
           (2) Returned TagSpan.Span is the full span, i.e. it is not the set of intersection ranges of Span with failure sequence point. *)
        member __.GetTags(spans : _) : _ = 
            let getTags _ path = 
                if !tfiCache = null then
                    tfiCache := path |> dataStore.GetTestFailureInfosInFile |> Async.RunSynchronously

                spans
                |> Seq.map (fun s -> 
                       let dl = 
                            { document = path
                              line = (s.Start.GetContainingLine().LineNumber + 1) |> DocumentCoordinate }
                       s, (dl, !tfiCache) ||> Dict.tryGetValue [||] id)
                |> Seq.where (fun (_, tfis) -> 
                       tfis
                       |> Seq.isEmpty
                       |> not)
                |> Seq.map 
                       (fun (s, tfis) -> TagSpan<_>(SnapshotSpan(s.Start, s.Length), { Tfis = tfis }) :> ITagSpan<_>)
            buffer.FilePath |> Option.fold getTags Seq.empty
        
        [<CLIEvent>]
        member __.TagsChanged = tagsChanged.Publish
