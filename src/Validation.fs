module Fable.Validation.Core

open System
open System.Text.RegularExpressions

#if FABLE_COMPILER

open Fable.Core

#endif

let private singleKey = "s"

module ValidateRegexes =
    let mail = Regex (@"^(([^<>()\[\]\\.,;:\s@""]+(\.[^<>()\[\]\\.,;:\s@""]+)*)|("".+""))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)
    let url = Regex (@"^((([A-Za-z]{3,9}:(?:\/\/)?)(?:[\-;:&=\+\$,\w]+@)?[A-Za-z0-9\.\-]+|(?:www\.|[\-;:&=\+\$,\w]+@)[A-Za-z0-9\.\-]+)((?:\/[\+~%\/\.\w\-_]*)?\??(?:[\-\+=&;%@\.\w_]*)#?(?:[\.\!\/\\\w]*))?)$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)

type ValidateResult<'T> =
    | Valid of 'T
    | Invalid
with
    member x.IsValid_
        with get() = match x with Valid _ -> true | _ -> false
    member x.IsInvalid_ with get() = x.IsValid_ |> not

type FieldInfo<'T, 'T0, 'E> = {
    key: string
    original: 'T
    result: ValidateResult<'T0>
    validator: Validator<'E>
} with
    member x.Replace<'T1> (result: ValidateResult<'T1>): FieldInfo<'T, 'T1, 'E> =
        { key=x.key; original=x.original; result=result; validator=x.validator }

and Validator<'E>(all) =
    let mutable errors: Map<string, 'E list> = Map.empty
    let mutable hasError = false
    member __.HasError with get() = hasError

    member __.Errors with get() = errors

    member __.PushError name error =
        if not hasError then hasError <- true
        errors <- Map.add name [error] errors

    member x.Test name (value: 'T): FieldInfo<'T, 'T, 'E> =
        errors <- Map.add name [] errors
        if not all && hasError then
            { key=name; original=value
              result=Invalid; validator=x }
        else
            { key=name; original=value
              result=Valid value; validator=x }

    member x.TestAsync name (value: 'T) =
        async { return x.Test name value }

    member x.TestSingle (value: 'T0) =
        x.Test singleKey value

#if FABLE_COMPILER
    [<PassGenerics>]
#endif
    member __.End<'T, 'T0> (input: FieldInfo<'T, 'T0, 'E>) =
        match input.result with
        | Valid value -> value
        | _ -> Unchecked.defaultof<'T0>

#if FABLE_COMPILER
    [<PassGenerics>]
#endif
    member x.EndAsync (input: Async<FieldInfo<'T, 'T0, 'E>>) =
        async {
            let! input = input
            return x.End input
        }

    /// Validate with a custom tester, return ValidateResult DU to modify input value
    member __.IsValidOpt<'T, 'T0, 'T1> (tester: 'T0 -> ValidateResult<'T1>) (error: 'E) (input: FieldInfo<'T, 'T0, 'E>) =
        match input.result with
        | Valid value ->
            let result = tester value
            if result.IsInvalid_ then
                input.validator.PushError input.key error
            input.Replace result
        | _ -> input.Replace Invalid

    /// Validate with a custom tester, return bool
    member x.IsValid<'T, 'T0> (tester: 'T0 -> bool) =
        x.IsValidOpt<'T, 'T0, 'T0> (fun v -> if tester v then Valid v else Invalid)

    member __.IsValidOptAsync<'T, 'T0, 'T1> (tester: 'T0 -> Async<ValidateResult<'T1>>) (error: 'E) (input: Async<FieldInfo<'T, 'T0, 'E>>) =
        async {
            let! input = input
            match input.result with
            | Valid value ->
                let! result = tester value
                if result.IsInvalid_ then
                    input.validator.PushError input.key error
                return input.Replace result
            | Invalid -> return input.Replace Invalid
        }

    member x.IsValidAsync (tester: 'T0 -> Async<bool>) =
        x.IsValidOptAsync<'T, 'T0, 'T0> (
            fun v ->
                async {
                    let! ret = tester v
                    return if ret then Valid v else Invalid
                })

    // Trim input value
    member __.Trim (input: FieldInfo<'T, string, string>) =
        match input.result with
        | Valid value ->
            input.Replace (Valid <| value.Trim())
        | Invalid -> input

    /// Validate with `String.IsNullOrWhiteSpace`
    member x.NotBlank<'T> err =
        x.IsValid<'T, string> (String.IsNullOrWhiteSpace >> not) err

    /// Test an option value is some and unwrap it
    /// it will collect error
#if FABLE_COMPILER
    [<PassGenerics>]
#endif
    member x.IsSome error =
        x.IsValid<'T, 'T0 option> (fun t -> t.IsSome) error >> x.SkipNone

    /// Skip following rules if value is None,
    /// it won't collect error
    member __.SkipNone (input: FieldInfo<'T, 'T0 option, 'E>) =
        match input.result with
        | Valid (Some value) ->
            input.Replace <| Valid value
        | _ -> input.Replace <| Invalid

    /// Defaults of None value, it won't collect error
    member __.DefaultOfNone defaults (input: FieldInfo<'T, 'T0 option, 'E>) =
        match input.result with
        | Valid (Some value) ->
            input.Replace <| Valid value
        | _ -> input.Replace <| Valid defaults

    /// Test a Result value is Ok and unwrap it
    /// it will collect error
#if FABLE_COMPILER
    [<PassGenerics>]
#endif
    member x.IsOk<'T, 'T0, 'TError> error =
        let isOk = fun t -> match t with Ok _ -> true | Error _ -> false
        x.IsValid<'T, Result<'T0, 'TError>> isOk error >> x.SkipError

    /// Skip following rules if value is None,
    /// it won't collect error
    member __.SkipError (input: FieldInfo<'T, Result<'T0, 'TError>, 'E>)  =
        match input.result with
        | Valid (Ok value) ->
            input.Replace <| Valid value
        | _ -> input.Replace <| Invalid

    /// Defaults of Error value, it won't collect error
    member __.DefaultOfError defaults (input: FieldInfo<'T, Result<'T0, 'TError>, 'E>)  =
        match input.result with
        | Valid (Ok value) ->
            input.Replace <| Valid value
        | _ -> input.Replace <| Valid defaults

    /// Map a function or constructor to the value, aka lift
    /// fn shouldn't throw error, if it would, please using `t.To fn error`
    member x.Map fn =
        x.IsValidOpt<'T, 'T0, 'T1> (fn >> Valid) Unchecked.defaultof<'E>

    /// Convert the input value by fn
    /// if fn throws error then it will collect error
    member x.To fn =
        x.IsValidOpt<'T, 'T0, 'T1> (
            fun t ->
                try fn t |> Valid
                with
                | exn ->
                    printfn "Validation Map error: fn: %A value: %A exn: %s %s" fn t exn.Message exn.StackTrace
                    Invalid
        )

    /// Convert a synchronize validate pipe to asynchronize
    member __.ToAsync (input: FieldInfo<'T, 'T0, 'E>) =
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
    member x.MaxLen len err (input: FieldInfo<'T, 'T0, 'E>) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{len}", len.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> Seq.length input <= len) err input

    /// Min length, if err is a string, it can contains `{len}` to reuse first param
    member x.MinLen len err (input: FieldInfo<'T, 'T0, 'E>) =
        let err =
            match (err :> obj) with
            | :? string as strErr -> strErr.Replace("{len}", len.ToString()) :> obj :?> 'E
            | _ -> err
        x.IsValid (fun input -> Seq.length input >= len) err input

    member x.Enum<'T, 'T0, 'E when 'T0: equality> (enums: 'T0 list) =
        (fun input -> enums |> List.contains input) |> x.IsValid<'T, 'T0>

    member x.IsMail (error: 'E) input =
        x.IsValid<'T, string> ValidateRegexes.mail.IsMatch error input

    member x.IsUrl (error: 'E) input =
        x.IsValid<'T, string> ValidateRegexes.url.IsMatch error input

    member x.Match (regex: Regex) (error: 'E) input =
        x.IsValid<'T, string> (regex.IsMatch) error input

    #if FABLE_COMPILER

    member x.IsDegist error input =
        x.IsValid<'T, string> (String.forall(fun c -> c >= '0' && c <= '9')) error input

    #else

    member x.IsDegist error input =
        x.IsValid<'T, string> (String.forall(Char.IsDigit)) error input

    #endif

let private instance<'E> = Validator<'E>(true)

/// IsValid helper from Validator method for custom rule functions, you can also extend Validator class directly.
let isValid<'T, 'T0, 'E> = instance<'E>.IsValid<'T, 'T0>

/// IsValidOpt helper from Validator method for custom rule functions, you can also extend Validator class directly.
let isValidOpt<'T, 'T0, 'T1, 'E> = instance<'E>.IsValidOpt<'T, 'T0, 'T1>

/// IsValidAsync helper from Validator method for custom rule functions, you can also extend Validator class directly.
let isValidAsync<'T, 'T0, 'E> = instance<'E>.IsValidAsync<'T, 'T0>

/// IsValidOptAsync helper from Validator method for custom rule functions, you can also extend Validator class directly.
let isValidOptAsync<'T, 'T0, 'T1, 'E> = instance<'E>.IsValidOptAsync<'T, 'T0, 'T1>

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
#if FABLE_COMPILER
[<PassGenerics>]
#endif
let single (tester: Validator<'E> -> FieldInfo<'T, 'T0, 'E>)  =
    let t = Validator(true)
    let ret = tester t |> t.End
    if t.HasError
    then Error t.Errors.[singleKey]
    else Ok ret

/// Validate single value asynchronize
#if FABLE_COMPILER
[<PassGenerics>]
#endif
let singleAsync (tester: Validator<'E> -> Async<FieldInfo<'T, 'T0, 'E>>) =
    async {
        let t = Validator(true)
        let! ret = tester t |> t.EndAsync
        if t.HasError
        then return Error t.Errors.[singleKey]
        else return Ok ret
    }


