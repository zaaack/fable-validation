module Fable.Validation.Core

open System
open System.Text.RegularExpressions

type ValidatorFlags = {
    race: bool
    skip: bool
}
type ValidatorInfo<'T, 'TError> =
    | Input of (ValidatorFlags * 'T)
    | Validator of (ValidatorFlags * 'T* Result<'T, 'TError list>)
    | AsyncValidator of (ValidatorFlags * 'T * Async<Result<'T, 'TError list>>)

type ValidationTester<'T, 'TError> =
    | Test of ('T -> 'T option)
    | AsyncTest of ('T -> Async<Result<'T option, 'TError>>)

let inline internal castAny<'a, 'b> (a: 'a) = (a :> obj) :?> 'b

let getInfoFlagsAndInput info =
    match info with
    | Input (race, input) -> (race, input)
    | Validator (race, input, _) -> (race, input)
    | AsyncValidator (race, input, _) -> (race, input)


let ifInvalid<'T, 'TError> (test: ValidationTester<'T, 'TError>) (error: 'TError) (info: ValidatorInfo<'T, 'TError>): ValidatorInfo<'T, 'TError> =
    let (flags, input) =  getInfoFlagsAndInput info
    let {race = race; skip = skip} = flags
    if skip then info
    else
        let inline mkResult input errors =
            if List.isEmpty errors then Ok input
            else Error errors
        let inline mkResultWithLast lastResult input errors =
            match lastResult with
            | Ok input -> mkResult input errors
            | Error errors' -> mkResult input (errors @ errors')


        let inline asyncValidate () =
            async {
                let mutable newInput = input
                let mutable testErrors = []
                match test with
                | Test test ->
                    match test input with
                    | None -> testErrors <- error::testErrors
                    | Some input' -> newInput <- input'
                | AsyncTest test ->
                    let! result' = test input
                    match result' with
                    | Ok None -> testErrors <- error::testErrors
                    | Ok(Some input') -> newInput <- input'
                    | Error error' -> testErrors <- error'::testErrors

                if race && List.isEmpty testErrors |> not then
                    return mkResult newInput testErrors
                else
                    let mutable result = Ok newInput
                    match info with
                    | Input (race, input') ->
                        failwithf "this should never happen for asyncValidator race:%A input:%A" race input'
                    | Validator (_, input, result') ->
                        result <- mkResultWithLast result' input testErrors
                    | AsyncValidator (_, input, result') ->
                        let! asyncInfoResult = result'
                        result <- mkResultWithLast asyncInfoResult input testErrors
                    return result
            }

        let mutable newInput = input
        match test, info with
        | AsyncTest _, _ | _, AsyncValidator _ ->
            AsyncValidator (flags, input, asyncValidate ())
        | Test test, Input (_, input) ->
            let testErrors = 
                match test input with
                | Some input -> newInput <- input; []
                | None -> [error]
            Validator (flags, newInput, (mkResult input testErrors))
        | Test test, Validator (_, input, result) ->
            let testErrors = 
                match test input with
                | Some input -> newInput <- input; []
                | None -> [error]
            let result =
                if race && List.isEmpty testErrors |> not then mkResult input testErrors
                else mkResultWithLast result input testErrors
            Validator (flags, input, result)

let inline ifInvalidSync<'T, 'TError> (test: 'T -> 'T option) =
    ifInvalid<'T, 'TError> (Test test)


let inline ifInvalidAsync<'T, 'TError> (test: 'T -> Async<Result<'T option, 'TError>>) =
    ifInvalid<'T, 'TError> (AsyncTest test)

let baseValidateAsync<'T, 'TError, 'L when 'L : comparison>
    raceField (rules: ('L * ValidatorInfo<obj, 'TError>) list): Async<Result<Map<'L,'E>, Map<'L, 'TError list>>> =
        async {
            let mutable msgMap = Map<'L, 'TError list> Seq.empty
            let mutable dataMap = Map<'L, 'E> Seq.empty
            let mutable tail = rules
            let mutable isBreak = false
            let consumeResult key result =
                match result with
                | Ok input -> dataMap <- Map.add key input dataMap
                | Error errors ->
                    msgMap <- Map.add key errors msgMap
            while not isBreak && List.isEmpty tail |> not do
                let (key, info) = tail.Head
                tail <- tail.Tail
                match info with
                | Validator (_, _, result) ->
                    consumeResult key result
                | AsyncValidator (_, _, result) ->
                    let! result = result
                    consumeResult key result
                | Input input -> failwithf "Validation rules must be a function, but found input: %A" input
                if raceField && Map.isEmpty msgMap |> not then
                    isBreak <- true

            return
                if Map.isEmpty msgMap then Ok dataMap
                else Error msgMap
        }

let inline allAsync<'T, 'TError, 'L when 'L : comparison> = baseValidateAsync<'T, 'TError, 'L> false
let inline raceAsync<'T, 'TError, 'L when 'L : comparison> = baseValidateAsync<'T, 'TError, 'L> true

// let internal createNewRecord (input: 'T) fields =
//     let type' = typedefof<'T>
//     FSharp.Reflection.FSharpValue.MakeRecord()


let baseValidateSync<'T, 'TError, 'L when 'L : comparison>
    raceField (rules: ('L * ValidatorInfo<obj, 'TError>) list): Result<Map<'L,'E>, Map<'L, 'TError list>> = 
            let mutable msgMap = Map<'L, 'TError list> Seq.empty
            let mutable dataMap = Map<'L, 'E> Seq.empty
            let mutable tail = rules
            let mutable isBreak = false
            while not isBreak && List.isEmpty tail |> not do
                let (key, info) = tail.Head
                tail <- tail.Tail
                match info with
                | Validator (_, _, result) ->
                    match result with
                    | Ok input -> dataMap <- Map.add key input dataMap
                    | Error errors ->
                        (msgMap <- Map.add key errors msgMap)
                | AsyncValidator (_, input, vali) ->
                    failwithf "Sync validation cannot contain async rules: %A, whole input: %A" (input, vali) input
                | Input input -> failwithf "Validation rules must be a function, but found input: %A, whole input: %A" input input
                if raceField && Map.isEmpty msgMap |> not then
                    isBreak <- true
            if Map.isEmpty msgMap then Ok dataMap
            else Error msgMap


let inline all<'T, 'TError, 'L when 'L : comparison> = baseValidateSync<'T, 'TError, 'L> false
let inline race<'T, 'TError, 'L when 'L : comparison> = baseValidateSync<'T, 'TError, 'L> true

let inline allSync<'T, 'TError, 'L when 'L : comparison> = all
let inline raceSync<'T, 'TError, 'L when 'L : comparison> = race

let inline test input = Input ({race=true;skip=false}, input)
let inline testAll input = Input ({race=false;skip=false}, input)

let inline endTest<'T, 'TError> = castAny<ValidatorInfo<'T, 'TError>, ValidatorInfo<obj, 'TError>>


let skipNone<'T, 'TError> (info: ValidatorInfo<'T option, 'TError>): ValidatorInfo<'T, 'TError> =
    let (flags, input) = getInfoFlagsAndInput info
    let dummy = castAny<obj, 'T> null
    match input with
    | Some i -> Validator (flags, i, Ok(i))
    | None -> Validator ({flags with skip = true}, dummy, Ok(dummy))


let skipError<'T, 'TError, 'Error> (info: ValidatorInfo<Result<'T, 'TError>, 'Error>) : ValidatorInfo<'T, 'Error> =
    let (flags, input) = getInfoFlagsAndInput info
    let dummy = castAny<obj, 'T> null
    match input with
    | Ok i -> Validator (flags, i, Ok(i))
    | Error _ -> Validator ({flags with skip = true}, dummy, Ok(dummy))

let inline ifInvalidByBool<'T, 'TError>(tester: 'T -> bool) =
    (fun input -> if tester input then Some input else None)  |> Test |> ifInvalid<'T, 'TError>

let ifNone<'T, 'TError> error info =
    let _ifNone = Option.isSome |> ifInvalidByBool
    _ifNone error info |> skipNone<'T, 'TError>

let ifError<'T, 'TError, 'Error> error info =
    let inline isOk (input: Result<'T, 'TError>) =
        match input with
        | Ok _ -> true
        | Error _ -> false
    let _ifError = isOk |> ifInvalidByBool
    _ifError error info |> skipError<'T, 'TError, 'Error>

let ifBlank<'TError> = String.IsNullOrWhiteSpace |> ifInvalidByBool<string, 'TError>

let ifBlankAfterTrim<'TError> =
    (fun (input: string) -> 
        let input = input.Trim()
        if String.IsNullOrWhiteSpace input 
        then None 
        else Some input)
    |> Test |> ifInvalid<string, 'TError>

let ifNotGt min =
    (fun input -> input > min) |> ifInvalidByBool

let ifNotGte min =
    (fun input -> input >= min) |> ifInvalidByBool

let ifNotLt max =
    (fun input -> input < max) |> ifInvalidByBool

let ifNotLte max =
    (fun input -> input <= max) |> ifInvalidByBool

let ifNotMaxLen len =
    (fun input -> Seq.length input <= len) |> ifInvalidByBool 

let ifNotMinLen len =
    (fun input -> Seq.length input >= len) |> ifInvalidByBool 

module Regexs = 
    let mail = Regex (@"^(([^<>()\[\]\\.,;:\s@""]+(\.[^<>()\[\]\\.,;:\s@""]+)*)|("".+""))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)
    let url = Regex (@"^((([A-Za-z]{3,9}:(?:\/\/)?)(?:[\-;:&=\+\$,\w]+@)?[A-Za-z0-9\.\-]+|(?:www\.|[\-;:&=\+\$,\w]+@)[A-Za-z0-9\.\-]+)((?:\/[\+~%\/\.\w\-_]*)?\??(?:[\-\+=&;%@\.\w_]*)#?(?:[\.\!\/\\\w]*))?)$", RegexOptions.Compiled ||| RegexOptions.ECMAScript)



let ifNotMail<'TError> = Regexs.mail.IsMatch |> ifInvalidByBool<string, 'TError>


let ifNotUrl<'TError> = Regexs.url.IsMatch |> ifInvalidByBool<string, 'TError>

#if FABLE_COMPILER 
let ifNotDegist<'TError> = (String.forall(fun c -> c >= '0' && c <= '9')) |> ifInvalidByBool<string, 'TError>
#else
let ifNotDegist<'TError> = (String.forall(Char.IsDigit)) |> ifInvalidByBool<string, 'TError>
#endif
