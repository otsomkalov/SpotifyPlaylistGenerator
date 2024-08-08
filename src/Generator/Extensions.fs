namespace Generator.Extensions

open System.Diagnostics
open Microsoft.Extensions.Primitives

module Activity =
  let rec private getParentmost (activity: Activity) =
    if activity.Parent = null then
      activity
    else
      getParentmost activity.Parent

  let getCurrentParentmost () =
    getParentmost Activity.Current

module IQueryCollection =

  let (|QueryParam|_|) (stringValues: StringValues) =
    if stringValues = StringValues.Empty then
      None
    else
      Some(stringValues.ToString())
