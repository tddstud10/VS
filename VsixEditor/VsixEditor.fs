module R4nd0mApps.TddStud10.VsixEditor

open Microsoft.VisualStudio.Zip
open System
open System.IO

let unzipTo vsix dst = 
    let decompressor = ZipFileDecompressor(vsix)
    decompressor.UncompressToFolder(dst)
    decompressor.Close()
    dst

let cleanFiles path = 
    let excl = [ "Microsoft.VisualStudio."; "FSharp.Core"; "System.Xml"; "Newtonsoft.Json" ]
    let incl = [ "Microsoft.VisualStudio.TestPlatform" ]
    path
    |> fun d -> Directory.GetFiles(d, "*", SearchOption.AllDirectories)
    |> Seq.filter (fun f -> 
           let fn = Path.GetFileName(f)
           excl
           |> List.exists (fun f -> fn.StartsWith(f, StringComparison.OrdinalIgnoreCase))
           && not (incl |> List.exists (fun f -> fn.StartsWith(f, StringComparison.OrdinalIgnoreCase))))
    |> Seq.iter File.Delete
    path

let makePathRelativeTo (folder : string) file = 
    let file = Uri(file, UriKind.Absolute)
    
    let folder = 
        if folder.EndsWith(@"\\") then folder
        else folder + @"\"
    
    let folder = Uri(folder, UriKind.Absolute)
    folder.MakeRelativeUri(file).ToString() |> Uri.UnescapeDataString

let copyPdbs src dst = 
    Directory.GetFiles(dst, "*tddstud10*", SearchOption.AllDirectories)
    |> Array.map Path.GetFileName
    |> Array.filter (fun f -> 
           let extn = Path.GetExtension(f)
           extn = ".dll" || extn = ".exe")
    |> Array.map (fun f -> Path.ChangeExtension(f, "pdb"))
    |> Array.iter (fun f -> FileInfo(Path.Combine(src, f)).CopyTo(Path.Combine(dst, f), true) |> ignore)
    dst

let zipTo dst src = 
    let files = Directory.GetFiles(src, "*", SearchOption.AllDirectories) |> Array.map (makePathRelativeTo src)
    ZipFileCompressor(dst, src, files, true) |> ignore

[<EntryPoint>]
let main argv = 
    let vsixPath = argv.[0]
    let temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    Directory.CreateDirectory(temp) |> ignore
    temp
    |> unzipTo vsixPath
    |> cleanFiles
    |> copyPdbs (Path.GetDirectoryName vsixPath)
    |> zipTo vsixPath
    0
