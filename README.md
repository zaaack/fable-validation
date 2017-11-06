# Fable.Validation

[![Build Status](https://travis-ci.org/zaaack/fable-validation.svg "Build Status")](https://travis-ci.org/zaaack/fable-validation)
[![NuGet version](https://badge.fury.io/nu/Fable.Validation.svg)](https://badge.fury.io/nu/Fable.Validation)

## Install
```sh
paket install Fable.Validation
```
OR

```sh
dotnet add package Fable.Validation
```

## Usage


```F#
open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.Testing
open Fable.Validation.Core

type People = {
    name: string
    age: int
}  with
    static member Name = "name"
    static member Age = "age"

let valid = { name="abcd"; age=10 }
let result = all <| fun t -> // validate all fields, if you want to return early after first error, you can use `fast`, there are also `allAsync`, `fastAsync` for async validate support

    { name = t.Test People.Name valid.name // call `t.Test fieldName value` to initialize state
                |> t.Trim // pipe the field state to validate rules
                |> t.NotBlank "name cannot be blank" // some rules could contain a generic error message
                |> t.MaxLen 20 "maxlen is 20"
                |> t.MinLen 4 "minlen is 4"
                |> t.End // call `t.End` to unwrap the validated and transformed value, you can use the transformed values to create a new model

      age = t.Test People.Age valid.age
                |> t.Gt 0 "cannot less then or equal 0"
                |> t.Lt 200 "cannot greater then or equal 200"
                |> t.End }

Assert.AreEqual (result, Ok(valid))

let result = all <| fun t ->

    { name = t.Test People.Name "abc"
                |> t.MaxLen 20 "maxlen is 20"
                |> t.MinLen 4 "minlen is 4"
                |> t.End
      age = t.Test People.Age 201
                |> t.Gt 0 "cannot less then or equal 0"
                |> t.Lt 200 "cannot greater then or equal 200"
                |> t.End }

Assert.AreEqual (result, Error(Map [ People.Name, ["minlen is 4"]
                                     People.Age, ["cannot greater then or equal 200"] ]))

// async validate example

async {
    let valid = { name=" abcd "; age=10 }

    let testNameAsync =
        IsValidOptAsync<string, string, string> <| fun name ->
            async { return Valid (name.Trim()) }

    // fast validation
    let! result = fastAsync <| fun t ->
        async {
            let! name = t.Test People.Name valid.name
                        |> t.ToAsync
                        |> testNameAsync "should be right"
                        |> t.EndAsync

            return { name = name
                     age = t.Test People.Age valid.age
                            |> t.Gt 0 "cannot less then or equal 0"
                            |> t.Lt 200 "cannot greater then or equal 200"
                            |> t.End }
        }

    Assert.AreEqual (result, Ok({name="abcd"; age=10}))
} |> Async.StartImmediately

// validate single value

// result is Ok(transformed input value) or Error(error list)
// test single value don't need call `t.End`, because it will return as result.
let result: Result<string, string list> = single <| fun t ->
    t.TestSingle "Some Input" |> t.MinLen 10 "minlen is 10" 

```

## [API Reference](https://zaaack.github.io/fable-validation)

## License
MIT
