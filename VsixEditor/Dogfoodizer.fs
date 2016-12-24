module R4nd0mApps.TddStud10.VsixEditor.Dogfoodizer

open Mono.Cecil
open System
open System.IO
open System.Text.RegularExpressions
open System.Xml

let private renameExeConfigFiles path = 
    printfn "... Renaming exe configs at %s" path
    path
    |> Common.getTddStud10Executables
    |> Seq.filter (fun e -> e.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
    |> Seq.iter (fun e -> File.Move(e + ".config", e.Replace(".exe", ".DF.exe.config")))
    path

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

let private changeEtwProviderName (asm : AssemblyDefinition) = 
    let t = asm.MainModule.Types |> Seq.tryFind (fun t -> t.FullName.Contains(".WindowsLogger"))
    match t with
    | Some t -> 
        let a = t.CustomAttributes |> Seq.find (fun a -> a.AttributeType.FullName.Contains(".EventSourceAttribute"))
        t.CustomAttributes.Remove(a) |> ignore
        let p = a.Properties |> Seq.find (fun p -> p.Name = "Name")
        let newArg = CustomAttributeArgument(p.Argument.Type, p.Argument.Value :?> string + "-DF")
        let newA = CustomAttribute(a.Constructor)
        newA.Properties.Add(CustomAttributeNamedArgument(p.Name, newArg))
        t.CustomAttributes.Add(newA)
    | None -> ()
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
    |> changeEtwProviderName
    |> resignAndSaveAssembly buildRoot file
    |> renameAssemblyFiles

let private editAllTddStud10Assemblies buildRoot path = 
    printfn "... Editing assemblies at %s" path
    path
    |> Common.getTddStud10Executables
    |> Seq.iter (editTddStud10Assembly buildRoot)
    path

let private changeVsixManifest path = 
    let vsixManifest = Path.Combine(path, "extension.vsixmanifest")
    printfn "... Editing VSIX manifest at %s" vsixManifest
    let doc = XmlDocument()
    doc.Load(vsixManifest)
    let nsm = new XmlNamespaceManager(doc.NameTable)
    nsm.AddNamespace("v", "http://schemas.microsoft.com/developer/vsx-schema/2011")
    let node = doc.SelectSingleNode("/v:PackageManifest/v:Metadata/v:DisplayName", nsm)
    node.InnerText <- node.InnerText + " (Dogfood)"
    let node = doc.SelectSingleNode("/v:PackageManifest/v:Metadata/v:Identity/@Version", nsm) :?> XmlAttribute
    let v = Version(node.Value)
    node.Value <- Version(v.Major, v.Minor, v.Build, 9).ToString()
    doc.Save(vsixManifest)
    path

let dogfoodize vsixPath = 
    printfn "VsixEditor: Dogfoodizing %s..." vsixPath
    let buildRoot = vsixPath |> Path.GetDirectoryName
    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    |> Directory.CreateDirectory
    |> fun di -> di.FullName
    |> Common.unzipTo vsixPath
    |> renameExeConfigFiles
    |> editAllTddStud10Assemblies buildRoot
    |> changeVsixManifest
    |> Common.zipTo vsixPath
    printfn "VsixEditor: Done!"
