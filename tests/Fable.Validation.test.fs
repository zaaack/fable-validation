module Fable.Validation.Tests

open System
open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.Testing

open Fable.Validation.Core

open Fable.Import
open Fable.Import.Node
open Fable.Import
open Fable.PowerPack

[<Emit("describe($0, $1)")>]
let describe (str: string) (fn: unit -> unit) = jsNative

[<Emit("it($0, $1)")>]
let it (str: string) (fn: unit -> unit) = jsNative

[<Emit("it($0, $1)")>]
let itAsync (str: string) (fn: unit -> JS.Promise<unit>) = jsNative


exception ShouldThrowException of string

describe "test rules" <| fun () ->
  it "should Trim work" <| fun () ->
    let valid = "aaa"
    let invalid = " aaa  "
    let result = single <| fun t -> t.TestOne valid |> t.Trim |> t.End
    Assert.AreEqual (result, Ok(valid), "valid is Ok")
    let result = single <| fun t -> t.TestOne invalid |> t.Trim |> t.End
    Assert.AreEqual (result, Ok(valid), "Invalid with trim is Ok")

  it "should IsBlank work" <| fun () ->
    let valid = "aaa"
    let invalid = "   "
    let msg = "Cannot be blank"
    let result =  single <| fun t -> t.TestOne valid |> t.NotBlank msg |> t.End
    Assert.AreEqual (result, Ok(valid), "1")
    let result =  single <| fun t -> t.TestOne invalid |> t.NotBlank msg |> t.End
    Assert.AreEqual (result, Error([msg]), "2")

  it "should IsBlank and Trim work" <| fun () ->
    let valid = "aaa"
    let invalid = " aaa  "
    let msg = "Cannot be blank"
    let result =  single <| fun t -> t.TestOne invalid |> t.Trim |> t.MaxLen 3 msg |> t.End
    Assert.AreEqual (result, Ok(valid))

  it "should IsOk work" <| fun () ->
    let ok = Ok(1)
    let okMsg = "should be ok"
    let err = Error(2)
    let result =  single <| fun t -> t.TestOne ok |> t.IsOk okMsg |> t.End
    Assert.AreEqual (result, ok, "1")
    let result =  single <| fun t -> t.TestOne err |> t.IsOk okMsg
    Assert.AreEqual (result, Error([okMsg]), "2")

  it "should SkipError work" <| fun () ->
    let err: Result<int, int> = Error(2)
    let result =  single <| fun t -> t.TestOneOnlyOk err [t.Gt 0 "Should > 0"]
    Assert.AreEqual (result, Ok(err))

  it "should DefaultOfError work" <| fun () ->
    let err: Result<int, int> = Error(2)
    let result =  single <| fun t -> t.TestOne err |> t.DefaultOfError 1 |> t.Gt 0 "Should > 0" |> t.End
    Assert.AreEqual (result, Ok(1))


  it "should IsSome work" <| fun () ->
    let some = Some(1)
    let someMsg = "should be some"
    let result =  single <| fun t -> t.TestOne some |> t.IsSome someMsg |> t.End
    Assert.AreEqual (result, Ok(some.Value), "1")
    let result =  single <| fun t -> t.TestOne None |> t.IsSome someMsg |> t.End
    Assert.AreEqual (result, Error([someMsg]), "2")

  it "should SkipNone work" <| fun () ->
    let none: int option = None
    let result =  single <| fun t -> t.TestOneOnlySome none [t.Gt 0 "should > 0"]
    // Unchecked.defaultof<'T> is always null in Fable
    // but it will be 0 when 'T is int in F#
    // Warnning: `Ok(null :> obj :?> int)` will throw runtime error in F#
    Assert.AreEqual (result, Ok(None))


  it "should DefaultOfNone work" <| fun () ->
    let none: int option = None
    let result =  single <| fun t -> t.TestOne none |> t.DefaultOfNone 1 |> t.Gt 0 "should > 0" |> t.End
    Assert.AreEqual (result, Ok(1))

  it "should Gt/Gte/Lt/Lte work" <| fun () ->
    let n1 = 1
    let result =  single <| fun t -> t.TestOne n1 |> t.Gt 0 "should > {min}" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Gt 1 "should > 0" |> t.End
    Assert.AreEqual (result, Error(["should > 0"]))
    let result =  single <| fun t -> t.TestOne n1 |> t.Lt 2 "should < {max}" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Lt 0 "should < 0" |> t.End
    Assert.AreEqual (result, Error(["should < 0"]))

    let result =  single <| fun t -> t.TestOne n1 |> t.Gte 0 "should >= 0" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Gte 1 "should >= {min}" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Gt 2 "should >= 2" |> t.End
    Assert.AreEqual (result, Error(["should >= 2"]))
    let result =  single <| fun t -> t.TestOne n1 |> t.Lte 2 "should <= {max}" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Lte 1 "should <= 1" |> t.End
    Assert.AreEqual (result, Ok(n1))
    let result =  single <| fun t -> t.TestOne n1 |> t.Lte 0 "should <= 0" |> t.End
    Assert.AreEqual (result, Error(["should <= 0"]))

  it "should Map/To work" <| fun () ->
    let n1 = 1
    let result =  single <| fun t -> t.TestOne n1 |> t.Map Ok |> t.IsOk "should be Ok" |> t.End
    Assert.AreEqual (result, Ok(n1))
    try
        (single <| fun t -> t.TestOne "abc" |> t.Map int |> t.End) |> ignore
        raise (ShouldThrowException "Map should throw error")
    with
    | :? ShouldThrowException as exp -> raise exp
    | _ -> ()

    let msg = "should be int"
    let result =  single <| fun t -> t.TestOne "123" |> t.To int msg |> t.End
    Assert.AreEqual (result, Ok(123))
    let result =  single <| fun t -> t.TestOne "abc" |> t.To int msg |> t.End
    Assert.AreEqual (result, Error([msg]))

  it "should MaxLen/MinLen work" <| fun () ->
    let str = "abcd"

    let minLen2 = "should min len 2"
    let minLen4 = "should min len 4"
    let minLen6 = "should min len 6"
    let result =  single <| fun t -> t.TestOne str |> t.MinLen 2 minLen2 |> t.End
    Assert.AreEqual (result, Ok(str))
    let result =  single <| fun t -> t.TestOne str |> t.MinLen 4 minLen4 |> t.End
    Assert.AreEqual (result, Ok(str))
    let result =  single <| fun t -> t.TestOne str |> t.MinLen 6 "should min len {len}" |> t.End
    Assert.AreEqual (result, Error([minLen6]))

    let maxLen2 = "should max len 2"
    let maxLen4 = "should max len 4"
    let maxLen6 = "should max len 6"
    let result =  single <| fun t -> t.TestOne str |> t.MaxLen 2 "should max len {len}" |> t.End
    Assert.AreEqual (result, Error([maxLen2]))
    let result =  single <| fun t -> t.TestOne str |> t.MaxLen 4 maxLen4 |> t.End
    Assert.AreEqual (result, Ok(str))
    let result =  single <| fun t -> t.TestOne str |> t.MaxLen 6 maxLen6 |> t.End
    Assert.AreEqual (result, Ok(str))

  it "should match work" <| fun () ->
    let valid = "123"
    let invalid = "abc"
    let msg = "should match number"
    let result =  single <| fun t -> t.TestOne valid |> t.Match (Regex ("^\d+$", RegexOptions.ECMAScript)) msg |> t.End
    Assert.AreEqual (result, Ok(valid))
    let result =  single <| fun t -> t.TestOne invalid |> t.Match (Regex ("^\d+$", RegexOptions.ECMAScript)) msg |> t.End
    Assert.AreEqual (result, Error([msg]))

  it "should url work" <| fun () ->
    let valid = "https://www.google.com"
    let invalid = "abc"
    let msg = "should be valid url"
    let result =  single <| fun t -> t.TestOne valid |> t.IsUrl msg |> t.End
    Assert.AreEqual (result, Ok(valid))
    let result =  single <| fun t -> t.TestOne invalid |> t.IsUrl msg |> t.End
    Assert.AreEqual (result, Error([msg]))

  it "should mail work" <| fun () ->
    let valid = "aa@bb.com"
    let invalid = "ab@bb"
    let msg = "should be valid mail"
    let result =  single <| fun t -> t.TestOne valid |> t.IsMail msg |> t.End
    Assert.AreEqual (result, Ok(valid))
    let result =  single <| fun t -> t.TestOne invalid |> t.IsMail msg |> t.End
    Assert.AreEqual (result, Error([msg]))

  it "should IsDegist work" <| fun () ->
    let valid = "12345"
    let invalid = "abcd"
    let msg = "should be degist"
    let result =  single <| fun t -> t.TestOne valid |> t.IsDegist msg |> t.End
    Assert.AreEqual (result, Ok(valid))
    let result =  single <| fun t -> t.TestOne invalid |> t.IsDegist msg |> t.End
    Assert.AreEqual (result, Error([msg]))



type People = {
    name: string
    age: int
}  with
    static member Name = "name"
    static member Age = "age"

describe "test all" <| fun () ->
  it "should all work" <| fun () ->
    let valid = {name="abcd"; age=10}
    let result =  all <| fun t ->
        { name = t.Test People.Name valid.name
                    |> t.MaxLen 20 "maxlen 20"
                    |> t.MinLen 4 "minlen 4"
                    |> t.End;
          age = t.Test People.Age valid.age
                    |> t.Gt 0 "min 0"
                    |> t.Lt 200 "min 200"
                    |> t.End }

    Assert.AreEqual (result, Ok(valid), "0")

    let result =  all <| fun t ->
        let name = t.TestOnlySome People.Name (Some "abc")
                    [ t.MaxLen 20 "maxlen 20"
                      t.MinLen 4 "minlen 4" ]
        { name = name.Value;
          age = t.Test People.Age 201
                    |> t.Gt 0 "min 0"
                    |> t.Lt 200 "min 200"
                    |> t.End }

    Assert.AreEqual (result, Error(Map [ People.Name, ["minlen 4"]
                                         People.Age, ["min 200"] ]), "1")

  it "should fast work" <| fun () ->
    let valid = { name = "abcd"; age = 10 }
    let result = fast <| fun t ->
        { name = t.Test People.Name valid.name
                    |> t.MaxLen 20 "maxlen 20"
                    |> t.MinLen 4 "minlen 4"
                    |> t.End

          age = t.Test People.Age valid.age
                    |> t.Gt 0 "min 0"
                    |> t.Lt 200 "min 200"
                    |> t.End }
    Assert.AreEqual (result, Ok(valid), "should be ok")

    let result = fast <| fun t ->
        { name = t.Test People.Name "abc"
                    |> t.MaxLen 20 "maxlen 20"
                    |> t.MinLen 4 "minlen 4"
                    |> t.End

          age = t.Test People.Age 201
                    |> t.Gt 0 "min 0"
                    |> t.Lt 200 "min 200"
                    |> t.End }
    Assert.AreEqual (result, Error(Map [ People.Age, []
                                         People.Name, ["minlen 4"]]), "should be error")


  itAsync "should async work" <| fun () ->
    promise {
        let valid = { name = " abcd "; age = 10 }
        let testNameAsync err =
            isValidOptAsync (fun (name: string) ->
                async { return Valid (name.Trim()) }) err

        let asyncResult = fastAsync <| fun t ->
            async {
                let! name = t.Test People.Name valid.name
                                |> t.ToAsync
                                |> testNameAsync "valid"
                                |> t.EndAsync

                return { name = name;
                         age  = t.Test People.Age valid.age
                                    |> t.Gt 0 "min 0"
                                    |> t.Lt 200 "min 200"
                                    |> t.End }
            }

        let! result = asyncResult |> Async.StartAsPromise
        Assert.AreEqual (result, Ok({name="abcd"; age=10}))
    }




