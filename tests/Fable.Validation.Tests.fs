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
    let ret = all [
                People.age, 
                testAll allRightPeople.age 
                    |> ifNotGt 0 "Age should greater then 0" 
                    |> ifNotLt 20 "Age should less then 20" |> endTest
                People.name, testAll allRightPeople.name |> ifBlank "Name shouldn't be empty"  |> endTest
                People.mail, testAll allRightPeople.mail |> ifBlankAfterTrim "Mail shoudn't be empty" |> ifNotMail "Not valid mail" |> endTest
                People.phone, testAll allRightPeople.phone |> ifNotDegist "Not numbers" |> endTest
                People.url, testAll allRightPeople.url |> ifNotUrl "Not valid url" |> endTest
            ]   
    ret |> Result.map (
        fun (input) -> 
            let age = Map.find "age" input :?> int
            let name: string = Map.find "name" input :?> string
            let mail = Map.find "mail" input :?> string
            Assert.AreEqual(age, allRightPeople.age)
            Assert.AreEqual(name, allRightPeople.name)
            Assert.AreEqual(mail, allRightPeople.mail.Trim())
       ) |> printfn "%A"
    Assert.AreEqual(Result.isOk ret, true)


it "Adding failed" <| fun () ->
    let ageMsg = "Age should greater then 18"
    let nameMsg = "Name should be degist"
    let mailMsg = "Not valid Url"
    let phoneMsg = "Not valid mail"
    let urlMsg = "Not degist"
    let t = allRightPeople
    let ret = all [
                People.age, testAll t.age |> ifNotGt 18 ageMsg |> endTest
                People.name, testAll t.name |> ifNotDegist nameMsg |> endTest
                People.mail, testAll t.mail |> ifNotUrl mailMsg |> endTest
                People.phone, testAll t.phone |> ifNotMail phoneMsg |> endTest
                People.url, testAll t.url |> ifNotDegist urlMsg |> endTest
            ]
    ret |> Result.mapError (
        fun msgs -> 
            Assert.AreEqual(msgs.[People.age].[0], ageMsg)
            Assert.AreEqual(msgs.[People.name].[0], nameMsg)
            Assert.AreEqual(msgs.[People.mail].[0], mailMsg)
            Assert.AreEqual(msgs.[People.phone].[0], phoneMsg)
            Assert.AreEqual(msgs.[People.url].[0], urlMsg)
       ) |> printfn "%A"
    Assert.AreEqual(Result.isError ret, true)
