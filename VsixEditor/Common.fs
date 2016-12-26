module R4nd0mApps.TddStud10.VsixEditor.Common

open Microsoft.VisualStudio.Zip
open System
open System.IO

let unzipTo vsix path = 
    printfn "... Unzipping %s -> %s" vsix path
    let decompressor = ZipFileDecompressor(vsix)
    decompressor.UncompressToFolder(path)
    decompressor.Close()
    path

let private makePathRelativeTo (folder : string) file = 
    let file = Uri(file, UriKind.Absolute)
    
    let folder = 
        if folder.EndsWith(@"\\") then folder
        else folder + @"\"
    
    let folder = Uri(folder, UriKind.Absolute)
    folder.MakeRelativeUri(file).ToString() |> Uri.UnescapeDataString

let zipTo vsix path = 
    printfn "... Zipping %s -> %s" path vsix
    let files = Directory.GetFiles(path, "*", SearchOption.AllDirectories) |> Array.map (makePathRelativeTo path)
    ZipFileCompressor(vsix, path, files, true) |> ignore

let getTddStud10Executables path = 
    Directory.GetFiles(path, "*tddstud10*", SearchOption.AllDirectories) |> Array.filter (fun f -> 
                                                                                let extn = Path.GetExtension(f)
                                                                                extn = ".dll" || extn = ".exe")
