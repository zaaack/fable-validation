#load "../packages/build/FSharp.Formatting/FSharp.Formatting.fsx"
#I "../packages/build/FAKE/tools/"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat

type BinaryInfo = {
  file: string
  folder: string
}

let root = __SOURCE_DIRECTORY__ @@ "../"

let formatting = root @@ "packages/build/FSharp.Formatting/"

let binaries = [
    "src/bin/Release/netstandard1.6/Fable.Validation.dll"
]

let output = root @@ "/docs"
let templates = __SOURCE_DIRECTORY__ @@ "templates"
let githubLink = "https://github.com/zaaack/fable-validation"


let info =
  [ "project-name", "fable-validation"
    "project-author", "Zack Young"
    "project-summary", ""
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/Fable.Validation" ]

let sourceVersion =
  ExecProcessAndReturnMessages (
    fun p ->
      p.Arguments <- "log -n 1"
      p.FileName <- "git"
      p.UseShellExecute <- false
      p.WorkingDirectory <- root)
    System.TimeSpan.MaxValue
  |> (fun r ->
        if r.ExitCode <> 0 then
          failwithf "Error occurred when get git version: %A" r.Errors

        r.Messages.Find (
          fun s ->
            s.Trim().StartsWith("commit "))
        |> replace "commit " "")

let buildReferences () =
  binaries
  |> List.map (
    fun bin ->
      async {
        let dllFile = root @@ bin
        let outputFolder = output
        ensureDirectory outputFolder
        let libDirs = [
          dllFile |> directory
        ]
        let () = MetadataFormat.Generate (
                    dllFile,
                    outputFolder,
                    [templates
                     formatting @@ "templates"
                     formatting @@ "templates/reference" ],
                    parameters = ("root", ".")::info,
                    sourceRepo = githubLink @@ "tree/master/",
                    moduleTemplate = __SOURCE_DIRECTORY__ @@ "templates/module.cshtml",
                    sourceFolder = root,
                    publicOnly = true, libDirs = libDirs)
        ()
      }
  )
  |> Async.Parallel
  |> Async.RunSynchronously
  |> ignore

buildReferences()

