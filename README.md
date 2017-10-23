# Fable.Validation

```F#

module People = 
    let name = "name"
    let age = "age"
    let phone = "phone"
    let mail = "mail"
    let url = "url"

type People = {
    name: string
    age: int
    phone: string
    mail: string
    url: string
} with
  static member Validate (p: People) =
      [
        People.age, testAll t.age 
                        |> ifNotGt 0 "Age should greater then 0" 
                        |> ifNotLt 20 "Age should less then 20" |> endTest
        People.name, testAll t.name |> ifBlank "Name shouldn't be empty"  |> endTest
        People.mail, testAll t.mail |> ifBlankAfterTrim "Mail shoudn't be empty" |> ifNotMail "Not valid mail" |> endTest
        People.phone, testAll t.phone |> ifNotDegist "Not numbers" |> endTest
        People.url, testAll t.url |> ifNotUrl "Not valid url" |> endTest
      ]



let allRightPeople = {
    name = "aaa"
    age = 10
    phone = "132721"
    mail = "  aa@bb.com  "
    url = "https://www.google.com"
}


let ret =
    allRightPeople
    |> (all People.Validate)
match ret with
| Ok input -> printfn "Original input: %A" input
| Error msgs -> printfn "Messages: %A" msgs
```


## TODO

* Support return modified input record/class