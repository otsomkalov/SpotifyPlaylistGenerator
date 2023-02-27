module Infrastructure.Core

open Domain.Core

[<RequireQualifiedAccess>]
module UserId =

  let value (UserId id) = id

