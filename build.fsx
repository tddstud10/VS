// include Fake libs
#r "./packages/FAKE/tools/FakeLib.dll"
#r "System.Management.Automation"

open Fake
open Fake.Testing
open System
open System.IO
open System.Management.Automation

MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some MSBuildVerbosity.Minimal }
let version = EnvironmentHelper.environVarOrDefault "GitVersion_NuGetVersion" "0.0.0-alpha00"
let vsixName = sprintf "TddStud10.%s.org.vsix" version
let vsixVersion = EnvironmentHelper.environVarOrDefault "GitVersion_AssemblySemVer" "0.0.0.0"

// Directories
let packagesDir = __SOURCE_DIRECTORY__ @@ "packages"
let buildDir  = __SOURCE_DIRECTORY__ @@ @"build"
let testDir  = __SOURCE_DIRECTORY__ @@ @"build"
let nugetDir = __SOURCE_DIRECTORY__ @@ @"NuGet"
ensureDirExists (directoryInfo nugetDir)

// Filesets
let solutionFile = "VS.sln"

let msbuildProps = [
    "Configuration", "Debug"
    "Platform", "Any CPU"
    "DeployExtension", "false"
    "CopyVsixExtensionFiles", "false"
    "CreateVsixContainer", "true"
    "TargetVsixContainerName", vsixName
]

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir]

    !! solutionFile
    |> MSBuild buildDir "Clean" msbuildProps
    |> ignore
)

Target "Rebuild" DoNothing

Target "UpdateTelemetryKey" (fun _ ->
    let keyFile = Path.Combine(__SOURCE_DIRECTORY__, "TddStudioPackage\Telemetry.Instrumentation.Key")
    if not <| File.Exists keyFile then failwithf "Key file not found at %s" keyFile
    let key = EnvironmentHelper.environVarOrDefault "TELEMETRY_INSTRUMENTATION_KEY" String.Empty
    File.WriteAllText(keyFile, key)
)

Target "UpdateVSIXVersion" (fun _ ->
    PowerShell
        .Create()
        .AddScript(__SOURCE_DIRECTORY__ @@ "tools" @@ (sprintf "updateVSIXVersion.ps1 -Version %s" vsixVersion))
        .Invoke()
        |> Seq.iter (printfn "%O")
)

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuild buildDir "Build" msbuildProps
    |> ignore

    // AppVeyor workaround
    !! @"packages\Newtonsoft.Json\lib\net45\Newtonsoft.Json.dll"
    |> CopyFiles buildDir
    
    let vsixWithPdb = buildDir @@ vsixName.Replace(".org", "")
    CopyFile vsixWithPdb (buildDir @@ vsixName) 
    let ret =
        ExecProcess (fun info ->
            info.FileName <- buildDir @@ "R4nd0mApps.TddStud10.VsixEditor.exe"
            info.Arguments <- "injectpdb " + vsixWithPdb) (TimeSpan.FromSeconds 30.0)
    if ret <> 0 then failwithf "VsixEdtor errored out"

    let dfVsix = buildDir @@ vsixName.Replace(".org", ".df")
    CopyFile dfVsix vsixWithPdb
    let ret =
        ExecProcess (fun info ->
            info.FileName <- buildDir @@ "R4nd0mApps.TddStud10.VsixEditor.exe"
            info.Arguments <- "dfize " + dfVsix) (TimeSpan.FromSeconds 30.0)
    if ret <> 0 then failwithf "VsixEdtor errored out"
)

Target "GitLink" (fun _ ->
    let gitLink = (packagesDir @@ @"gitlink" @@ "lib" @@ "net45" @@ "GitLink.exe")
    let args = sprintf "%s -f %s -d %s" __SOURCE_DIRECTORY__ solutionFile buildDir
    let ret =
        ExecProcessAndReturnMessages (fun info ->
            info.FileName <- gitLink
            info.Arguments <- args) (TimeSpan.FromSeconds 30.0)
    let consoleOutput =
        ret.Messages
        |> Seq.append ret.Errors
    consoleOutput
    |> Seq.iter (printfn "%s")
    let loadFailures =
        consoleOutput
        |> Seq.filter (fun m -> m.ToLowerInvariant().Contains("failed to load project"))
    if not ret.OK || not (Seq.isEmpty loadFailures) then failwith (sprintf "GitLink.exe \"%s\" task failed.\nErrors:\m %A" args loadFailures)
)

let runTest pattern =
    fun _ ->
        !! (buildDir + pattern)
        |> xUnit (fun p ->
            { p with
                ToolPath = findToolInSubPath "xunit.console.exe" (currentDirectory @@ "tools" @@ "xUnit")
                WorkingDir = Some testDir })

Target "Test" DoNothing
Target "UnitTests" (runTest "/*.UnitTests*.dll")

Target "Package" (fun _ ->
    printf "TBD: Inject pdbs into the VSIX. and generate dogfood vsix"
)

Target "Publish" (fun _ ->
    !! "build\*.vsix"
    |> AppVeyor.PushArtifacts
)

"Clean" ?=> "Build"
"Clean" ==> "Rebuild" 
"UpdateTelemetryKey" ==> "Build"
"UpdateVSIXVersion" ==> "Build"
"Build" ==> "Rebuild" 
"Build" ?=> "UnitTests" ==> "Test"
"Rebuild" ==> "Test"
"GitLink" ==> "Package"
"Test" ?=> "GitLink"
"Test" ==> "Package" ==> "Publish"

// start build
RunTargetOrDefault "Test"
