namespace R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.Core.Editor

open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Tagging
open R4nd0mApps.TddStud10.Common.Domain
open R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.EditorFrameworkExtensions
open System.Threading
open R4nd0mApps.TddStud10.Engine.Core

type TestStartTagger(buffer : ITextBuffer, dataStore : IXDataStore, dse : XDataStoreEvents) as self = 
    inherit DisposableTagger()

    let disposed : bool ref = ref false
    let syncContext = SynchronizationContext.Current
    let tagsChanged = Event<_, _>()
    
    let fireTagsChanged _ = 
        logger.logInfof "Firing TestStartTagger.TagsChanged"
        syncContext.Send
            (SendOrPostCallback
                 (fun _ -> 
                 Common.safeExec 
                     (fun () -> 
                     tagsChanged.Trigger
                         (self, 
                          SnapshotSpanEventArgs(SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))))), 
             null)
    
    let tcUpdatedSub = dse.TestCasesUpdated.Subscribe fireTagsChanged
    
    override __.Dispose(disposing) = 
        if not disposed.Value then 
            if disposing then 
                tcUpdatedSub.Dispose()
            disposed := true
            base.Dispose(disposing)

    interface ITagger<TestStartTag> with
        
        member __.GetTags(spans : _) : _ = 
            let getTags _ path = 
                spans
                |> Seq.map (fun s -> 
                       s, 
                       { document = path
                         line = DocumentCoordinate(s.Start.GetContainingLine().LineNumber + 1) })
                |> Seq.map (fun (s, dl) -> s, dataStore.FindTest dl)
                |> Seq.filter (fun (_, ts) -> 
                       ts
                       |> Seq.isEmpty
                       |> not)
                |> Seq.map 
                       (fun (s, ts) -> 
                       TagSpan<_>(SnapshotSpan(s.Start, s.Length), 
                                  { TstTestCases = ts
                                    TstLocation = 
                                        { document = path
                                          line = DocumentCoordinate(s.Start.GetContainingLine().LineNumber + 1) } }) :> ITagSpan<_>)
            buffer.FilePath |> Option.fold getTags Seq.empty
        
        [<CLIEvent>]
        member __.TagsChanged = tagsChanged.Publish
