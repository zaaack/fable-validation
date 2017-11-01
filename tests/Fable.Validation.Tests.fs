module Fable.Validation.Tests

open Fable.Core
open Fable.Validation
open Fable.Core.Testing
open Fable.Validation.Core
open Fable.Import

[<Global>]
let it (msg: string) (f: unit->unit): unit = jsNative

type Result<'T, 'TError> with
    static member isOk (res: Result<'T, 'TError>) =
        match res with
        | Ok _ -> true
        | Error _ -> false
    static member isError (res: Result<'T, 'TError>) =
        Result.isOk res |> not


type People = {
    username: string
    age: int
}  with
    static member Username = "username"
    static member Age = "age"

let allRightPeople = {
    username = "aaa"
    age = 10
}

it "Adding valid" <| fun () ->

    let ret = all (fun t ->
        { username = t.Test People.Username "aaa"
              |> t.NotBlank "Cannot be empty" |> t.MinLen 4 "Max length is 4"
              |> t.MaxLen 40 "Max length is 40" |> t.EndTest
          age = t.Test People.Age "16" |> t.To int "Age requires a number" |> t.EndTest })

    match ret with
    | Ok people -> people |> printfn "Valid people: %A"
    | Error msgs -> msgs |> printfn "Messages: %A"

    let ret2 = single (fun t -> t.TestSingle "aa" |> t.NotBlank "Cannot be empty")
    Assert.AreEqual(Result.isOk ret, true)


