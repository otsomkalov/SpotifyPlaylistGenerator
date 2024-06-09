module Telegram.Tests.ExcludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open FsUnit
open Xunit

[<Fact>]
let ``list should send excluded playlists`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Preset.mock.Id

      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> Seq.length
      |> should equal 2

      Task.FromResult()

  let sut = ExcludedPlaylist.list getPreset editMessageButtons

  sut Preset.mock.Id (Page 0)