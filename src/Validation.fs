module Fable.Validation.Core

open System
open System.Text.RegularExpressions

let private singleKey = "s"

module ValidateRegexes =
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

    member x.TestAsync<'T> name (value: 'T) =
        async { return x.Test name value }

    member x.TestSingle (value: 'T) =
        x.Test singleKey value

    member __.End (input: FieldInfo<'T, 'E>) =
        match input with
        | Some (_, value, _) -> value
        | None -> Unchecked.defaultof<'T>


    member __.EndAsync (input: Async<FieldInfo<'T, 'E>>) =
        async {
            let! input = input
            match input with
            | Some (_, value, _) -> return value
            | None -> return Unchecked.defaultof<'T>
        }

    /// Validate with a custom tester, return ValidateResult DU to modify input value
    member __.IsValidOpt<'T, 'T0> (tester: 'T -> ValidateResult<'T0>) (error: 'E) (input: FieldInfo<'T, 'E>) =
        match input with
        | Some (name, value, validator) ->
            match tester value with
            | Valid value' -> Some (name, value', validator)
            | Invalid ->
                validator.PushError name error
                None
        | None -> None

    /// Validate with a custom tester, return bool
    member x.IsValid<'T> (tester: 'T -> bool) =
        x.IsValidOpt<'T, 'T> (fun v -> if tester v then Valid v else Invalid)

    member __.IsValidOptAsync<'T, 'T0> (tester: 'T -> Async<ValidateResult<'T0>>) (error: 'E) (input: Async<FieldInfo<'T, 'E>>) =
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
        x.IsValidOptAsync<'T, 'T> (
            fun v ->
                async {
                    let! ret = tester v
                    return if ret then Valid v else Invalid
                })

    // Trim input value
    member __.Trim (input: (string * string * Validator<'Error>) option) =
        match input with
        | Some (key, value, vali) ->
            Some(key, value.Trim(), vali)
        | None -> None

    /// Validate with `String.IsNullOrWhiteSpace`
    member x.NotBlank =
        x.IsValid<string> (String.IsNullOrWhiteSpace >> not)

    /// Skip following rules if the option value is None
    /// it won't collect error
    member __.SkipNone (input: FieldInfo<'T option, 'E>) =
        match input with
        | Some (key, value, vali) ->
            match value with
            | Some value -> Some (key, value, vali)
            | None -> None
        | None -> None

    /// Test an option value is some and unwrap it
    /// it will collect error
    member x.IsSome error =
        x.IsValid<'T option> (fun t -> t.IsSome) error >> x.SkipNone

    // Skip following rules if the Result value is an Error
    // it won't collect error
    member __.SkipError (input: FieldInfo<Result<'T, 'TError>, 'E>) =
        match input with
        | Some (key, value, vali) ->
            match value with
            | Ok value -> Some (key, value, vali)
            | Error _ -> None
        | None -> None

    /// Test a Result value is Ok and unwrap it
    /// it will collect error
    member x.IsOk<'T, 'TError> error =
        let isOk = fun t -> match t with Ok _ -> true | Error _ -> false
        x.IsValid<Result<'T, 'TError>> isOk error >> x.SkipError

    /// Map a function or constructor to the value, aka lift
    /// fn shouldn't throw error, if it would, please using `t.To fn error`
    member x.Map fn =
        x.IsValidOpt<'T, 'T0> (fn >> Valid) Unchecked.defaultof<'E>

    /// Convert the input value by fn
    /// if fn throws error then it will collect error
    member x.To fn =
        x.IsValidOpt<'T, 'T0> (
            fun t ->
                try fn t |> Valid
                with
                | exn ->
                    printfn "Validation Map error: fn: %A value: %A exn: %s %s" fn t exn.Message exn.StackTrace
                    Invalid
        )

    /// Convert a synchronize validate pipe to asynchronize
    member __.ToAsync<'T> (input: FieldInfo<'T, 'E>) =
        async { return input }

    /// Greater then a value, if err is a string, it can contains `{min}` to reuse first param
    member x.Gt (min: 'a) (err : 'E) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{min}", min.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> input > min) err

    /// Greater and equal then a value, if err is a string, it can contains `{min}` to reuse first param
    member x.Gte (min: 'a) (err : 'E) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{min}", min.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> input >= min) err

    /// Less then a value, if err is a string, it can contains `{max}` to reuse first param
    member x.Lt (max: 'a) (err : 'E) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{max}", max.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> input < max) err

    /// Less and equal then a value, if err is a string, it can contains `{max}` to reuse first param
    member x.Lte (max: 'a) (err : 'E) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{max}", max.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> input <= max) err

    /// Max length, if err is a string, it can contains `{len}` to reuse first param
    member x.MaxLen len err (input: FieldInfo<'T, 'E>) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{len}", len.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> Seq.length input <= len) err input

    /// Min length, if err is a string, it can contains `{len}` to reuse first param
    member x.MinLen len err (input: FieldInfo<'T, 'E>) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{len}", len.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> Seq.length input >= len) err input

    member x.Enum<'T, 'E when 'T: equality> (enums: 'T list) =
        (fun input -> enums |> List.contains input) |> x.IsValid<'T>

    member x.IsMail error input =
        x.IsValid<string> ValidateRegexes.mail.IsMatch error input

    member x.IsUrl<'TError> error input =
        x.IsValid<string> ValidateRegexes.url.IsMatch error input

    #if FABLE_COMPILER

    member x.IsDegist error input =
        x.IsValid<string> (String.forall(fun c -> c >= '0' && c <= '9')) error input

    #else

    member x.IsDegist error input =
        x.IsValid<string> (String.forall(Char.IsDigit)) error input

    #endif

let private instance<'E> = Validator<'E>(true)

/// IsValid helper from Validator method for custom rule functions, you can also extend Validator class directly.
let IsValid<'T, 'E> = instance<'E>.IsValid<'T>

/// IsValidOpt helper from Validator method for custom rule functions, you can also extend Validator class directly.
let IsValidOpt<'T, 'T0, 'E> = instance<'E>.IsValidOpt<'T, 'T0>

/// IsValidAsync helper from Validator method for custom rule functions, you can also extend Validator class directly.
let IsValidAsync<'T, 'E> = instance<'E>.IsValidAsync<'T>

/// IsValidOptAsync helper from Validator method for custom rule functions, you can also extend Validator class directly.
let IsValidOptAsync<'T, 'T0, 'E> = instance<'E>.IsValidOptAsync<'T, 'T0>

let validateSync all (tester: Validator<'E> -> 'T) =
    let validator = Validator(all)
    let ret = tester validator
    if validator.HasError then
        Error validator.Errors
    else
        Ok ret

let validateAsync all (tester: Validator<'E> -> Async<'T>) =
    async {
        let validator = Validator(all)
        let! ret = tester validator
        if validator.HasError then
            return Error validator.Errors
        else
            return Ok ret
    }

/// validate all fields and return a custom type,
let inline all (tester: Validator<'E> -> 'T) = validateSync true tester

/// Exit after first error occurred and return a custom type
let inline fast (tester: Validator<'E> -> 'T) = validateSync false tester

let inline allAsync (tester: Validator<'E> -> Async<'T>) = validateAsync true tester

let inline fastAsync (tester: Validator<'E> -> Async<'T>) = validateAsync false tester

/// Validate single value
let single (tester: Validator<'E> -> FieldInfo<'T, 'E>)  =
    let validator = Validator(true)
    let ret = tester validator
    match ret with
    | Some (_, value, _) -> Ok value
    | None -> Error validator.Errors.[singleKey]

/// Validate single value asynchronize
let singleAsync (tester: Validator<'E> -> Async<FieldInfo<'T, 'E>>) =
    async {
        let validator = Validator(true)
        let! ret = tester validator
        return match ret with
                | Some (_, value, _) -> Ok value
                | None -> Error validator.Errors.[singleKey]
    }


