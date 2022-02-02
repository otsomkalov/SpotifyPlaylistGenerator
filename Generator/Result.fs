module Result

open System.Threading.Tasks

let bindAsync binder resultTask =
    task {
        let! result = resultTask

        return!
            match result with
            | Ok v -> binder v
            | Error e -> Error e |> Task.FromResult
    }

let (>>=) func1 func2 = func1 >> bindAsync func2
