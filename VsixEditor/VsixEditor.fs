module R4nd0mApps.TddStud10.VsixEditor.AppEntry

[<EntryPoint>]
let main argv = 
    match argv with
    | [| "injectpdb"; vsixPath |] -> PdbInjector.injectPdbs vsixPath
    | [| "dfize"; vsixPath |] -> Dogfoodizer.dogfoodize vsixPath
    | _ -> printfn "Usage: [injectpdb | dfize] vsixpath" 
    0
