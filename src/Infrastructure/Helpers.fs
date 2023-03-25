module Infrastructure.Helpers

[<RequireQualifiedAccess>]
module Task =
  let map mapping task' =
    task {
      let! value = task'

      return mapping value
    }

[<RequireQualifiedAccess>]
module TaskOption =
  let map mapping taskOption =
    task {
      let! option = taskOption

      return
        match option with
        | Some v -> mapping v |> Some
        | None -> None
    }
