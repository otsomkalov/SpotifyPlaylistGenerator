namespace Generator.Extensions

open Microsoft.Extensions.Primitives

module IQueryCollection =

  let (|QueryParam|_|) (stringValues: StringValues) =
    if stringValues = StringValues.Empty then
      None
    else
      Some(stringValues.ToString())
