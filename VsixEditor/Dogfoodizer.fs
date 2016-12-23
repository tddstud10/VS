module R4nd0mApps.TddStud10.VsixEditor.Dogfoodizer

open Mono.Cecil
open System.IO
open System.Text.RegularExpressions

let private renameFile file = 
    let newFile = sprintf "%s.DF%s" (Path.GetFileNameWithoutExtension(file)) (Path.GetExtension(file))
    File.Copy(file, Path.Combine(Path.GetDirectoryName(file), newFile))
    File.Delete(file)

let private readAssembly buildRoot file = 
    let r = DefaultAssemblyResolver()
    r.GetSearchDirectories() |> Array.iter r.RemoveSearchDirectory
    r.AddSearchDirectory(buildRoot)
    r.AddSearchDirectory(file)
    let rp = ReaderParameters(AssemblyResolver = r)
    AssemblyDefinition.ReadAssembly(file, rp)

let private renameAssembly (asm : AssemblyDefinition) = 
    asm.Name <- new AssemblyNameDefinition(asm.Name.Name + ".DF", asm.Name.Version)
    asm

let private renameAssemblyRefs (asm : AssemblyDefinition) = 
    let tddStud10Refs = 
        asm.MainModule.AssemblyReferences
        |> Seq.filter (fun ar -> Regex.IsMatch(ar.Name, ".*tddstud10.*", RegexOptions.IgnoreCase))
        |> Seq.toArray
    tddStud10Refs |> Seq.iter (fun r -> 
                         asm.MainModule.AssemblyReferences.Remove(r) |> ignore
                         r.Name <- r.Name + ".DF"
                         asm.MainModule.AssemblyReferences.Add(r))
    asm

let private resignAndSaveAssembly buildRoot (file : string) (asm : AssemblyDefinition) = 
    let snKeyPair = new System.Reflection.StrongNameKeyPair(File.ReadAllBytes(Path.Combine(buildRoot, "tddstud10.snk")))
    let wp = WriterParameters(WriteSymbols = true, StrongNameKeyPair = snKeyPair)
    asm.Write(file, wp)
    file

let private renameAssemblyFiles file = 
    [ file
      Path.ChangeExtension(file, "pdb") ]
    |> Seq.iter renameFile

let private editTddStud10Assembly buildRoot (file : string) = 
    readAssembly buildRoot file
    |> renameAssembly
    |> renameAssemblyRefs
    |> resignAndSaveAssembly buildRoot file
    |> renameAssemblyFiles

let private editAllTddStud10Assemblies buildRoot path = 
    printfn "... Changing assembly names at %s" path
    path
    |> Common.getTddStud10Executables
    |> Seq.iter (editTddStud10Assembly buildRoot)
    path

let private changeEtwChannelName = id
let private changeVsixManifest = id
let private renameTestHostConfigFile = id
let private changeTelemetryDeviceType = id
let private fixXLoader = id
let private fixBamlReferences = id
let private changeTestHostExeName = id
let private fixProductName = id

let dogfoodize vsixPath = 
    printfn "VsixEditor: Dogfoodizing %s..." vsixPath
    let buildRoot = vsixPath |> Path.GetDirectoryName
    let temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    Directory.CreateDirectory(temp) |> ignore
    temp
    |> Common.unzipTo vsixPath
    |> editAllTddStud10Assemblies buildRoot
    |> changeEtwChannelName
    //----
    |> renameTestHostConfigFile
    |> changeVsixManifest
    |> changeTestHostExeName
    |> changeTelemetryDeviceType
    |> fixXLoader
    |> fixProductName
    |> fixBamlReferences
    |> Common.zipTo vsixPath
    printfn "VsixEditor: Done!"
