# Fable.Validation

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
let result = all <| fun t ->

    { name = t.Test People.Name valid.name
                |> t.Trim
                |> t.NotBlank "name cannot be blank"
                |> t.MaxLen 20 "maxlen 20"
                |> t.MinLen 4 "minlen 4"
                |> t.EndTest;

      age = t.Test People.Age valid.age
                |> t.Gt 0 "min 0"
                |> t.Lt 200 "min 200"
                |> t.EndTest }

Assert.AreEqual (result, Ok(valid))

let result = all <| fun t ->

    { name = t.Test People.Name "abc"
                |> t.MaxLen 20 "maxlen 20"
                |> t.MinLen 4 "minlen 4"
                |> t.EndTest;
      age = t.Test People.Age 201
                |> t.Gt 0 "min 0"
                |> t.Lt 200 "min 200"
                |> t.EndTest }

Assert.AreEqual (result, Error(Map [ People.Name, ["minlen 4"]
                                     People.Age, ["min 200"] ]))

// async

async {
    let valid = { name=" abcd "; age=10 }

    let testNameAsync =
        IsValidOptAsync<string, string, string> <| fun name ->
            async { return Valid (name.Trim()) }

    let! result = fastAsync <| fun t ->
        async {
            let! name = t.Test People.Name valid.name
                        |> t.ToAsync
                        |> testNameAsync "should be right"

            return { name = name |> t.EndTest;
                     age=t.Test People.Age valid.age
                            |> t.Gt 0 "min 0"
                            |> t.Lt 200 "min 200"
                            |> t.EndTest }
        }

    Assert.AreEqual (result, Ok({name="abcd"; age=10}))
}

```


## License
MIT
