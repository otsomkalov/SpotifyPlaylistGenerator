module Shared.Log

open System
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core

[<Interface>]
type ILog =
  abstract Logger: ILogger

let info (env: #ILog) (message, [<ParamArray>] args: obj list) =
  env.Logger.LogInformation(message, args)
