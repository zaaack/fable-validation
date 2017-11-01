module Fable.Validation.Core

open System
open System.Text.RegularExpressions

let private singleKey = "s"

module Regexs =
    let mail = Regex (@"^(([^<>()\[\]\\.,;:\s@""]+(\.[^<>()\[\]\\.,;:\s@""]+)*)|("".+""))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)
    let url = Regex (@"^((([A-Za-z]{3,9}:(?:\/\/)?)(?:[\-;:&=\+\$,\w]+@)?[A-Za-z0-9\.\-]+|(?:www\.|[\-;:&=\+\$,\w]+@)[A-Za-z0-9\.\-]+)((?:\/[\+~%\/\.\w\-_]*)?\??(?:[\-\+=&;%@\.\w_]*)#?(?:[\.\!\/\\\w]*))?)$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

type ValidateResult<'T> =
    | Valid of 'T
    | Invalid

type FieldInfo<'T, 'E> = (string * 'T * Validator<'E>) option

and Validator<'E>(all) =
    let mutable errors: Map<string, 'E list> = Map.empty
    let mutable hasError = false
    member __.HasError with get() = hasError
    member __.Errors with get() = errors

    member __.PushError name error =
        if not hasError then hasError <- true
        errors <- Map.add name [error] errors
    member x.Test<'T> name (value: 'T) =
        errors <- Map.add name [] errors
        if not all && hasError then None
        else Some (name, value, x)
    member x.TestSingle value =
        x.Test singleKey value
    member __.EndTest (input: FieldInfo<'T, 'E>) =
        match input with
        | Some (_, value, _) -> value
        | None -> Unchecked.defaultof<'T>

    member __.IsValidOpt<'T, 'T0, 'E> (tester: 'T -> ValidateResult<'T0>) (error: 'E) (input: FieldInfo<'T, 'E>) =
        match input with
        | Some (name, value, validator) ->
            match tester value with
            | Valid value' -> Some (name, value', validator)
            | Invalid ->
                validator.PushError name error
                None
        | None -> None
    member x.IsValid<'T, 'E> (tester: 'T -> bool) =
        x.IsValidOpt<'T, 'T, 'E> (fun v -> if tester v then Valid v else Invalid)

    member __.IsValidOptAsync<'T, 'T0, 'E> (tester: 'T -> Async<ValidateResult<'T0>>) (error: 'E) (input: Async<FieldInfo<'T, 'E>>) =
        async {
            let! input = input
            match input with
            | Some (name, value, validator) ->
                let! ret = tester value
                return match ret with
                        | Valid value' -> Some (name, value', validator)
                        | Invalid ->
                        validator.PushError name error
                        None
            | None -> return None
        }

    member x.IsValidAsync (tester: 'T -> Async<bool>) =
        x.IsValidOptAsync<'T, 'T, 'E> (
            fun v ->
                async {
                    let! ret = tester v
                    return if ret then Valid v else Invalid
                })

    member __.Trim (input: (string * string * Validator<'Error>) option) =
        match input with
        | Some (key, value, vali) ->
            Some(key, value.Trim(), vali)
        | None -> None

    member x.NotBlank =
        x.IsValid<string, 'E> (String.IsNullOrEmpty)

    member __.SkipNone input =
        match input with
        | Some (key, value, vali) ->
            match value with
            | Some value -> Some (key, value, vali)
            | None -> None
        | None -> None

    member x.IsSome error =
        x.IsValid<'T option, 'E> (fun t -> t.IsSome) error >> x.SkipNone

    member __.SkipError input =
        match input with
        | Some (_, value, _) ->
            match value with
            | Ok _ -> input
            | Error _ -> None
        | None -> None

    member x.Map fn =
        x.IsValidOpt<'T, 'T0, 'E> (fn >> Valid) Unchecked.defaultof<'E>

    member x.To fn =
        x.IsValidOpt<'T, 'T0, 'E> (
            fun t ->
                try fn t |> Valid
                with
                | exn ->
                    printfn "Validation Map error: fn: %A value: %A exn: %A" fn t exn
                    Invalid
        )

    member x.IsOk<'T, 'TError> error =
        let isOk = fun t -> match t with Ok _ -> true | Error _ -> false
        x.IsValid<Result<'T, 'TError>, 'E> isOk error >> x.SkipError 

    member x.Gt min =
        x.IsValid (fun input -> input > min)

    member x.Gte min =
        x.IsValid (fun input -> input >= min)

    member x.Lt max =
        (fun input -> input < max) |> x.IsValid

    member x.Lte max =
        (fun input -> input <= max) |> x.IsValid

    member x.MaxLen len =
        (fun input -> Seq.length input <= len) |> x.IsValid

    member x.MinLen len =
        (fun input -> Seq.length input >= len) |> x.IsValid


    member x.IsMail<'TError> error input =
        x.IsValid<string, 'TError> Regexs.mail.IsMatch error input


    member x.IsUrl<'TError> error input =
        x.IsValid<string, 'TError> Regexs.url.IsMatch error input

    #if FABLE_COMPILER
    member x.IsDegist<'TError> error input =
        x.IsValid<string, 'TError> (String.forall(fun c -> c >= '0' && c <= '9')) error input
    #else
    member x.IfNotDegist<'TError> error input =
        x.IsValid<string, 'TError> (String.forall(Char.IsDigit)) error input
    #endif

let validateSync all tester =
    let validator = Validator(all)
    let ret = tester validator
    if validator.HasError then
        Error validator.Errors
    else
        Ok ret
let validateAsync all tester =
    async {
        let validator = Validator(all)
        let! ret = tester validator
        if validator.HasError then
            return Error validator.Errors
        else
            return Ok ret
    }

let inline all tester = validateSync true tester
let inline race tester = validateSync false tester
let inline allAsync tester = validateAsync true tester
let inline raceAsync tester = validateAsync false tester


let single tester =
    let validator = Validator(true)
    let ret = tester validator
    match ret with
    | Some (_, value, _) -> Ok value
    | None -> Error validator.Errors.[singleKey]

let singleAsync tester =
    async {
        let validator = Validator(true)
        let! ret = tester validator
        return match ret with
                | Some (_, value, _) -> Ok value
                | None -> Error validator.Errors.[singleKey]
    }

