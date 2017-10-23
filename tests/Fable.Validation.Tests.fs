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
    name: string
    age: int
    phone: string
    mail: string
    url: string
} 

module People = 
    let name = "name"
    let age = "age"
    let phone = "phone"
    let mail = "mail"
    let url = "url"


let allRightPeople = {
    name = "aaa"
    age = 10
    phone = "132721"
    mail = "  aa@bb.com  "
    url = "https://www.google.com"
}

it "Adding valid" <| fun () ->
    let ret =
        allRightPeople
        |> (all (fun t ->
                    [
                        People.age, 
                        testAll t.age 
                            |> ifNotGt 0 "Age should greater then 0" 
                            |> ifNotLt 20 "Age should less then 20" |> endTest
                        People.name, testAll t.name |> ifBlank "Name shouldn't be empty"  |> endTest
                        People.mail, testAll t.mail |> ifBlankAfterTrim "Mail shoudn't be empty" |> ifNotMail "Not valid mail" |> endTest
                        People.phone, testAll t.phone |> ifNotDegist "Not numbers" |> endTest
                        People.url, testAll t.url |> ifNotUrl "Not valid url" |> endTest
                    ]))
    ret |> Result.map (
        fun input -> 
            Assert.AreEqual(input.age, allRightPeople.age)
            Assert.AreEqual(input.name, allRightPeople.name)
            Assert.AreEqual(input.mail, allRightPeople.mail.Trim())
       ) |> printfn "%A"
    Assert.AreEqual(Result.isOk ret, true)


it "Adding failed" <| fun () ->
    let ageMsg = "Age should greater then 18"
    let nameMsg = "Name should be degist"
    let mailMsg = "Not valid Url"
    let phoneMsg = "Not valid mail"
    let urlMsg = "Not degist"
    let ret =
        allRightPeople
        |> (all (fun t ->
                    [
                        People.age, testAll t.age |> ifNotGt 18 ageMsg |> endTest
                        People.name, testAll t.name |> ifNotDegist nameMsg |> endTest
                        People.mail, testAll t.mail |> ifNotUrl mailMsg |> endTest
                        People.phone, testAll t.phone |> ifNotMail phoneMsg |> endTest
                        People.url, testAll t.url |> ifNotDegist urlMsg |> endTest
                    ]))
    ret |> Result.mapError (
        fun msgs -> 
            Assert.AreEqual(msgs.[People.age].[0], ageMsg)
            Assert.AreEqual(msgs.[People.name].[0], nameMsg)
            Assert.AreEqual(msgs.[People.mail].[0], mailMsg)
            Assert.AreEqual(msgs.[People.phone].[0], phoneMsg)
            Assert.AreEqual(msgs.[People.url].[0], urlMsg)
       ) |> printfn "%A"
    Assert.AreEqual(Result.isError ret, true)
// |> ifInvalidMail "Not valid mail sync" |> ifNotMinLen 12 "Name's minimal length is 12"

// JS.console.time "sync"
// let internal result =
//     all (fun t ->
//         [
//             ("age", testAll t.age |> ifNotGt 18 "Age should greater then 18" |> endTest)
//             ("name", testAll t.name |> ifBlank "Name shouldn't be empty" |> ifInvalidMail "Not valid mail sync" |> ifNotMinLen 12 "Name's minimal length is 12" |> endTest)
//         ]) {name = ""; age = 15}
// JS.console.timeEnd "sync"

// printfn "all sync result: %A" result
// printfn "mail: %A" (ifInvalidMail "Not valid mail" (Input ({race= false; skip= false}, "")))
// let asyncResult =
//     let rules t =
//         [("age", testAll t.age |> ifNotGt 18 "Age should greater then 18" |> endTest);
//         ("name", testAll t.name |> ifBlank "Name shouldn't be empty" |> ifInvalidMail "Not valid mail" |> ifNotMinLen 12 "Name's minimal length is 12" |> endTest)]
//     async {
//         JS.console.time "async"
//         let! result' = allAsync rules {name = ""; age = 15}
//         JS.console.timeEnd "async"
//         printfn "all async result: %A" result'
//     }
//     |> Async.StartImmediate

// let p = {name = ""; age = 15}
// JS.console.time "raw"
// let r' = String.IsNullOrWhiteSpace(p.name) || (Seq.length p.name) > 12
// let r1' = p.age > 18
// JS.console.timeEnd "raw"
// // let asyncTest =
// //     async {
// //       return 1
// //     } |> Async.RunSynchronously
// // printfn "%A" asyncTest
// // printfn "async result: %A" asyncResult
