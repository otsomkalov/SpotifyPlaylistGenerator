module Generator.Worker.Log

open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core

[<Interface>]
type ILog =
  abstract Logger: ILogger

let info (env: #ILog) message = env.Logger.LogInformation(message)

let infoWithArg (env: #ILog) message arg =
  env.Logger.LogInformation(message, [ arg ])

let infoWithArgs (env: #ILog) message arg1 arg2 =
  env.Logger.LogInformation(message, [ arg1; arg2 ])
