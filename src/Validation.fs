module Fable.Validation.Core

open System
open System.Text.RegularExpressions

let private singleKey = "s"

module ValidateRegexs =
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

    member x.TestSingle value =
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
    member __.SkipNone input =
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
    member __.SkipError input =
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
                    printfn "Validation Map error: fn: %A value: %A exn: %A" fn t exn
                    Invalid
        )

    /// Convert a synchronize validate pipe to asynchronize
    member __.ToAsync<'T> (input: FieldInfo<'T, 'E>) =
        async { return input }

    member x.Gt min err =
        x.IsValid (fun input -> input > min) err

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

    member x.Enum<'T, 'E when 'T: equality> (enums: 'T list) =
        (fun input -> enums |> List.contains input) |> x.IsValid<'T>

    member x.IsMail error input =
        x.IsValid<string> ValidateRegexs.mail.IsMatch error input

    member x.IsUrl<'TError> error input =
        x.IsValid<string> ValidateRegexs.url.IsMatch error input

    #if FABLE_COMPILER

    member x.IsDegist error input =
        x.IsValid<string> (String.forall(fun c -> c >= '0' && c <= '9')) error input

    #else

    member x.IsDegist error input =
        x.IsValid<string> (String.forall(Char.IsDigit)) error input

    #endif

let private instance<'E> = Validator<'E>(true)

/// IsValid helper from Validator method for custom rule functions, you can also extends Validator class directly.
let IsValid<'T, 'E> = instance<'E>.IsValid<'T>

/// IsValidOpt helper from Validator method for custom rule functions, you can also extends Validator class directly.
let IsValidOpt<'T, 'T0, 'E> = instance<'E>.IsValidOpt<'T, 'T0>

/// IsValidAsync helper from Validator method for custom rule functions, you can also extends Validator class directly.
let IsValidAsync<'T, 'E> = instance<'E>.IsValidAsync<'T>

/// IsValidOptAsync helper from Validator method for custom rule functions, you can also extends Validator class directly.
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


