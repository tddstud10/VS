module R4nd0mApps.TddStud10.VsixEditor.Dogfoodizer

open Mono.Cecil
open System
open System.IO
open System.Text
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
    let newFileName = sprintf "%s.DF%s" (Path.GetFileNameWithoutExtension(file)) (Path.GetExtension(file))
    let newFile = Path.Combine(Path.GetDirectoryName(file), newFileName)
    File.Copy(file, newFile)
    File.Delete(file)
    newFile

let private readAssembly buildRoot file = 
    let r = DefaultAssemblyResolver()
    r.GetSearchDirectories() |> Array.iter r.RemoveSearchDirectory
    r.AddSearchDirectory(buildRoot)
    r.AddSearchDirectory(Path.GetDirectoryName(file))
    let rp = ReaderParameters(AssemblyResolver = r, ReadSymbols = true)
    let ad = AssemblyDefinition.ReadAssembly(file, rp)
    let hasSN = ad.Name.HasPublicKey 
    file, hasSN, ad

let private renameAssembly (f, hasSN, asm : AssemblyDefinition) = 
    asm.Name <- new AssemblyNameDefinition(asm.Name.Name + ".DF", asm.Name.Version)
    f, hasSN, asm

let private renameAssemblyRefs (f, hasSN, asm : AssemblyDefinition) = 
    let tddStud10Refs = 
        asm.MainModule.AssemblyReferences
        |> Seq.filter (fun ar -> Regex.IsMatch(ar.Name, ".*tddstud10.*", RegexOptions.IgnoreCase))
        |> Seq.toArray
    tddStud10Refs |> Seq.iter (fun r -> r.Name <- r.Name + ".DF")
    f, hasSN, asm

let private changeEtwProviderName (f, hasSN, asm : AssemblyDefinition) = 
    let t = asm.MainModule.Types |> Seq.tryFind (fun t -> t.FullName.Contains(".WindowsLogger"))
    match t with
    | Some t -> 
        let a = t.CustomAttributes |> Seq.find (fun a -> a.AttributeType.FullName.EndsWith(".EventSourceAttribute"))
        t.CustomAttributes.Remove(a) |> ignore
        let p = a.Properties |> Seq.find (fun p -> p.Name = "Name")
        let newArg = CustomAttributeArgument(p.Argument.Type, p.Argument.Value :?> string + "-DF")
        let newA = CustomAttribute(a.Constructor)
        newA.Properties.Add(CustomAttributeNamedArgument(p.Name, newArg))
        t.CustomAttributes.Add(newA)
    | None -> ()
    f, hasSN, asm

let private changeExtensibilityTags (f, hasSN, asm : AssemblyDefinition) = 
    asm.MainModule.Types
    |> Seq.filter (fun t -> t.FullName.EndsWith("TaggerProvider"))
    |> Seq.iter (fun t -> 
           let a = t.CustomAttributes |> Seq.find (fun a -> a.AttributeType.FullName.EndsWith(".TagTypeAttribute"))
           let tr = a.ConstructorArguments.[0].Value :?> TypeReference
           tr.Scope.Name <- tr.Scope.Name + ".DF")
    f, hasSN, asm

let private changeCommonUIResourceName (f, hasSN, asm : AssemblyDefinition) = 
    if asm.Name.Name = "R4nd0mApps.TddStud10.Hosts.CommonUI.DF" then 
        asm.MainModule.Resources
        |> Seq.find (fun r -> r.Name = "R4nd0mApps.TddStud10.Hosts.CommonUI.g.resources")
        |> fun r -> r.Name <- "R4nd0mApps.TddStud10.Hosts.CommonUI.DF.g.resources"
    else ()
    f, hasSN, asm

let private resignAndSaveAssembly buildRoot (file : string, hasSN, asm : AssemblyDefinition) = 
    let wp = 
        let snKeyPair = System.Reflection.StrongNameKeyPair(File.ReadAllBytes(Path.Combine(buildRoot, "tddstud10.snk")))
        WriterParameters(WriteSymbols = true, StrongNameKeyPair = if hasSN then snKeyPair else null)
    asm.Write(file, wp)

let private renameAssemblyFiles file = 
    [ file
      Path.ChangeExtension(file, "pdb") ]
    |> Seq.map renameFile
    |> Seq.toList
    |> List.head

let private editTddStud10Assembly buildRoot = 
    renameAssemblyFiles
    >> readAssembly buildRoot
    >> renameAssembly
    >> renameAssemblyRefs
    >> changeEtwProviderName
    >> changeExtensibilityTags
    >> changeCommonUIResourceName
    >> resignAndSaveAssembly buildRoot

let private editAllTddStud10Assemblies buildRoot path = 
    printfn "... Editing assemblies at %s" path
    path
    |> Common.getTddStud10Executables
    |> Seq.iter (editTddStud10Assembly buildRoot)
    path

let private editVsixManifest path = 
    let vsixManifest = Path.Combine(path, "extension.vsixmanifest")
    printfn "... Editing VSIX manifest at %s" vsixManifest
    let doc = XmlDocument()
    doc.Load(vsixManifest)
    let nsm = new XmlNamespaceManager(doc.NameTable)
    nsm.AddNamespace("v", "http://schemas.microsoft.com/developer/vsx-schema/2011")
    // Change DisplayName and Version
    let node = doc.SelectSingleNode("/v:PackageManifest/v:Metadata/v:DisplayName", nsm)
    node.InnerText <- node.InnerText + " (Dogfood)"
    let node = doc.SelectSingleNode("/v:PackageManifest/v:Metadata/v:Identity/@Version", nsm) :?> XmlAttribute
    let v = Version(node.Value)
    node.Value <- Version(v.Major, v.Minor, v.Build, 999).ToString()
    // Change Assets
    doc.SelectNodes("/v:PackageManifest/v:Assets/v:Asset/@Path", nsm)
    |> Seq.cast<XmlAttribute>
    |> Seq.iter 
           (fun a -> 
           a.Value <- sprintf "%s.DF%s" (Path.GetFileNameWithoutExtension(a.Value)) (Path.GetExtension(a.Value)))
    doc.Save(vsixManifest)
    path

let private editPkgdef path = 
    let pkgdef = Path.Combine(path, "R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.pkgdef")
    printfn "... Editing PKGDEF at %s" pkgdef
    let pkgdefContents = File.ReadAllText(pkgdef)
    
    let s = 
        if pkgdefContents.Contains("R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.dll") then 
            pkgdefContents.Replace
                ("R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.dll", 
                 "R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.DF.dll")
        else failwithf "R4nd0mApps.TddStud10.Hosts.VS.TddStudioPackage.dll not found in %s" pkgdef
    File.WriteAllText(pkgdef, s, Encoding.Unicode)
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
    |> editVsixManifest
    |> editPkgdef
    |> Common.zipTo vsixPath
    printfn "VsixEditor: Done!"
