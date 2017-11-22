# Fable.Validation

[![Build Status](https://travis-ci.org/zaaack/fable-validation.svg "Build Status")](https://travis-ci.org/zaaack/fable-validation)
[![NuGet version](https://badge.fury.io/nu/Fable.Validation.svg)](https://badge.fury.io/nu/Fable.Validation)

## Install
```sh
paket add Fable.Validation
```
OR

```sh
dotnet add package Fable.Validation
```

## Usage

### Example

`all` is a validate function, it has a parameter to pass a callback with the only argument of a validator instance, you can write your validate rules and return the Ok value. `all` means validate all fields, if you want to return early after first error, you can use `fast`, there are also `allAsync`, `fastAsync` for async validate support.

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
let result = all <| fun t ->

    { name = t.Test People.Name valid.name // call `t.Test fieldName value` to initialize field state
                |> t.Trim // pipe the field state to rules
                |> t.NotBlank "name cannot be blank" // rules can contain params and a generic error message
                |> t.MaxLen 20 "maxlen is {len}"
                |> t.MinLen 4 "minlen is {len}"
                |> t.End // call `t.End` to unwrap the validated
                         // and transformed value,
                         // you can use the transformed values to create a new model

      age = t.Test People.Age valid.age
                |> t.Gt 0 "should greater then {min}"
                |> t.Lt 200 "shoudld less then {max}"
                |> t.End }

Assert.AreEqual (result, Ok(valid))

// the result type is Result<'T, 'E list>
let result: Result<People, string list> = all <| fun t ->

    { name = t.Test People.Name "abc"
                |> t.MaxLen 20 "maxlen is 20"
                |> t.MinLen 4 "minlen is 4"
                |> t.End
      age = t.Test People.Age 201
                |> t.Gt 0 "should greater then 0"
                |> t.Lt 200 "should less then 200"
                |> t.End }

Assert.AreEqual (result, Error(Map [ People.Name, ["minlen is 4"]
                                     People.Age, ["should less then 200"] ]))

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
                            |> t.Gt 0 "shoud greater then 0"
                            |> t.Lt 200 "should less then 200"
                            |> t.End }
        }

    Assert.AreEqual (result, Ok({name="abcd"; age=10}))
} |> Async.StartImmediately

// validate single value

// result is Ok(transformed input value) or Error(error list)
// test single value don't need call `t.End`, because it will return as result.
let result: Result<string, string list> = single <| fun t ->
    t.TestOne "Some Input" |> t.MinLen 10 "minlen is 10" |> t.End

```

### Option/Result support

This library comes with Option/Result support in mind, you can unwrap it by validate rules like IsSome/IsOK, or skip following validation it if it's None/Error.

```F#
let result: Result<string, string list> = single <| fun t ->
    t.TestOne (Some 1) |> t.IsSome  "should be some" |> t.Gt 0 "should greater then 0" |> t.End

// In this case you don't need to call t.End and rules are a list of rule,
// it will return None if it's None and don't cause any error.
// there are TestOneOnlySome/TestOneOnlyOk/TestOneOnlySomeAsync/TestOneOnlyOkAsync for single field test
// and TestOnlySome/TestOnlyOk/TestOnlySomeAsync/TestOnlyOkAsync for multi fields test
let result: Result<string, string list> = single <| fun t ->
    t.TestOneOnlySome None [ t.Gt 0 "should greater then 0" ]
```

### Map/To

It's very easy to transform the input value to another type:

* `t.Map` Lift the input value's type, it shouldn't throw error
* `t.To` Parse the input value to another type, it could throw error.

```F#
let result: Result<int option, string list> =
    single (fun t -> t.TestOne 1 |> t.Map Some |> t.End)

let result: Result<int, string list> =
    single (fun t -> t.TestOne "123" |> t.To int "cann't parse to int" |> t.End)
```

For more you can see [API Reference](https://zaaack.github.io/fable-validation).

## License

MIT
