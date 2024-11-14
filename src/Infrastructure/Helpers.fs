module Infrastructure.Helpers

open System.Text.Json
open System.Text.Json.Serialization

module JSON =
  let options =
    JsonFSharpOptions.Default().WithUnionExternalTag().WithUnionUnwrapRecordCases().ToJsonSerializerOptions()

  let serialize value =
    JsonSerializer.Serialize(value, options)

  let deserialize<'a> (json: string) =
    JsonSerializer.Deserialize<'a>(json, options)