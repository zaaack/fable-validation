#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Fake.YarnHelper
open System
open System.IO
open System.Text.RegularExpressions

#if MONO
// prevent incorrect output encoding (e.g. https://github.com/fsharp/FAKE/issues/1196)
System.Console.OutputEncoding <- System.Text.Encoding.UTF8
#endif

let dotnetcliVersion = "2.0.0"

let mutable dotnetExePath = "dotnet"

let release = LoadReleaseNotes "RELEASE_NOTES.md"
let srcGlob = "src/**/*.fsproj"
let testsGlob = "tests/**/*.fsproj"

module Util =

    let visitFile (visitor: string->string) (fileName : string) =
        File.ReadAllLines(fileName)
        |> Array.map (visitor)
        |> fun lines -> File.WriteAllLines(fileName, lines)

    let replaceLines (replacer: string->Match->string option) (reg: Regex) (fileName: string) =
        fileName |> visitFile (fun line ->
            let m = reg.Match(line)
            if not m.Success
            then line
            else
                match replacer line m with
                | None -> line
                | Some newLine -> newLine)

// Module to print colored message in the console
module Logger =
    let consoleColor (fc : ConsoleColor) =
        let current = Console.ForegroundColor
        Console.ForegroundColor <- fc
        { new IDisposable with
              member x.Dispose() = Console.ForegroundColor <- current }

    let warn str = Printf.kprintf (fun s -> use c = consoleColor ConsoleColor.DarkYellow in printf "%s" s) str
    let warnfn str = Printf.kprintf (fun s -> use c = consoleColor ConsoleColor.DarkYellow in printfn "%s" s) str
    let error str = Printf.kprintf (fun s -> use c = consoleColor ConsoleColor.Red in printf "%s" s) str
    let errorfn str = Printf.kprintf (fun s -> use c = consoleColor ConsoleColor.Red in printfn "%s" s) str


Target "Clean" (fun _ ->
    ["bin"]
    |> CleanDirs

    !! srcGlob
    |> Seq.collect(fun p ->
        ["bin";"obj"]
        |> Seq.map(fun sp ->
             Path.GetDirectoryName p @@ sp)
        )
    |> CleanDirs

    )

Target "InstallDotNetCore" (fun _ ->
   dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "YarnInstall"(fun _ ->
    Yarn (fun p ->
        { p with
            Command = Install Standard
        })
)

Target "DotnetRestore" (fun _ ->
    !! srcGlob
    ++ testsGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Restore (fun c ->
            { c with
                Project = proj
                ToolPath = dotnetExePath
                //This makes sure that Proj2 references the correct version of Proj1
                AdditionalArgs = [sprintf "/p:PackageVersion=%s" release.NugetVersion]
            })
))

Target "DotnetBuild" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Build (fun c ->
            { c with
                Project = proj
                ToolPath = dotnetExePath
            })
))


let fableSplitter workingDir =
    DotNetCli.RunCommand(fun c ->
        { c with WorkingDir = workingDir
                 ToolPath = dotnetExePath }
        ) "fable npm-run build:test"

let mocha args =
    Yarn(fun yarnParams ->
        { yarnParams with Command = args |> sprintf "run mocha -- %s" |> YarnCommand.Custom }
    )

Target "QuickTest" (fun _ ->
    !! testsGlob
    |> Seq.iter(fun proj ->
        let projDir = proj |> DirectoryName
        printfn "projDir: %A" projDir
        //Compile to JS
        fableSplitter projDir

        //Run mocha tests
        let projDirOutput = projDir </> "bin" </> "lib" </> "**/*.test.js"
        mocha projDirOutput
    )

)

Target "MochaTest" ignore


Target "Meta" (fun _ ->
    [ "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
      "<PropertyGroup>"
      "<Description>A Validation library for Fable</Description>"
      "<PackageProjectUrl>https://github.com/zaaack/fable-validation</PackageProjectUrl>"
      "<PackageLicenseUrl>https://raw.githubusercontent.com/zaaack/fable-validation/master/LICENSE.md</PackageLicenseUrl>"
    //   "<PackageIconUrl>https://raw.githubusercontent.com/zaaack/fable-validation/master/docs/files/img/logo.png</PackageIconUrl>"
      "<RepositoryUrl>https://github.com/zaaack/fable-validation.git</RepositoryUrl>"
      "<PackageTags>fable;elm;elmish;validation;fsharp</PackageTags>"
      "<Authors>Zack Young</Authors>"
      sprintf "<Version>%s</Version>" (string release.SemVer)
      "</PropertyGroup>"
      "</Project>"]
    |> WriteToFile false "Meta.props"
)

Target "DotnetPack" (fun _ ->
    !! srcGlob
    |> Seq.iter (fun proj ->
        DotNetCli.Pack (fun c ->
            { c with
                Project = proj
                Configuration = "Release"
                ToolPath = dotnetExePath
                AdditionalArgs =
                    [
                        sprintf "/p:PackageVersion=%s" release.NugetVersion
                        sprintf "/p:PackageReleaseNotes=\"%s\"" (String.Join("\n",release.Notes))
                    ]
            })
    )
)

let needsPublishing (versionRegex: Regex) (releaseNotes: ReleaseNotes) projFile =
    printfn "Project: %s" projFile
    if releaseNotes.NugetVersion.ToUpper().EndsWith("NEXT")
    then
        Logger.warnfn "Version in Release Notes ends with NEXT, don't publish yet."
        false
    else
        File.ReadLines(projFile)
        |> Seq.tryPick (fun line ->
            let m = versionRegex.Match(line)
            if m.Success then Some m else None)
        |> function
            | None -> failwith "Couldn't find version in project file"
            | Some m ->
                let sameVersion = m.Groups.[1].Value = releaseNotes.NugetVersion
                if sameVersion then
                    Logger.warnfn "Already version %s, no need to publish." releaseNotes.NugetVersion
                not sameVersion

Target "GenDocs" <| fun _ -> 
    // FSIHelper would cause errors, don't know why
    let out = ExecProcessAndReturnMessages (fun p ->
        p.FileName <- "fsharpi"
        p.Arguments <- "./tools/docgen.fsx"
        p.WorkingDirectory <- Environment.CurrentDirectory
        p.UseShellExecute <- false) TimeSpan.MaxValue
    for err in out.Errors do printfn "%s" err
    for msg in out.Messages do printfn "%s" msg
    if not out.OK then
        printfn "Warn: Generate docs has errors"

Target "Publish" (fun _ ->
    let versionRegex = Regex("<Version>(.*?)</Version>", RegexOptions.IgnoreCase)
    !! srcGlob
    |> Seq.filter(needsPublishing versionRegex release)
    |> Seq.iter(fun projFile ->
        let projDir = Path.GetDirectoryName(projFile)
        let nugetKey =
            match environVarOrNone "NUGET_KEY" with
            | Some nugetKey -> nugetKey
            | None -> failwith "The Nuget API key must be set in a NUGET_KEY environmental variable"
        Directory.GetFiles(projDir </> "bin" </> "Release", "*.nupkg")
        |> Array.find (fun nupkg -> nupkg.Contains(release.NugetVersion))
        |> (fun nupkg ->
            (Path.GetFullPath nupkg, nugetKey)
            ||> sprintf "nuget push %s -s nuget.org -k %s"
            |> DotNetCli.RunCommand (fun c ->
                                            { c with ToolPath = dotnetExePath }))

        // After successful publishing, update the project file
        (versionRegex, projFile) ||> Util.replaceLines (fun line _ ->
            versionRegex.Replace(line, "<Version>" + release.NugetVersion + "</Version>") |> Some)
    )
)

Target "Release" (fun _ ->

    if Git.Information.getBranchName "" <> "master" then failwith "Not on master"

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)


"Clean"
  ==> "Meta"
  ==> "InstallDotNetCore"
  ==> "YarnInstall"
  ==> "DotnetRestore"
  ==> "DotnetBuild"
  ==> "MochaTest"
  ==> "DotnetPack"
  ==> "GenDocs"
  ==> "Publish"
  ==> "Release"

"QuickTest"
  ==> "MochaTest"
  
RunTargetOrDefault "DotnetPack"
