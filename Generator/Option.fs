module Option

open System.Threading.Tasks

let bindAsync binder optionTask =
    task {
        let! result = optionTask

        return!
            match result with
            | Some v -> binder v
            | None -> None |> Task.FromResult
    }

let (>==) func1 func2 = func1 >> bindAsync func2
