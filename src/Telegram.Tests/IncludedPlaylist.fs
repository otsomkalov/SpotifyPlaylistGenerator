module Telegram.Tests.IncludedPlaylist

open System.Threading.Tasks
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open FsUnit
open Xunit

[<Fact>]
let ``list should send included playlists`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Preset.presetMock.Id
      Preset.presetMock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> should not' (be Empty)
      Task.FromResult()

  let sut = IncludedPlaylist.list getPreset editMessageButtons

  sut Preset.presetMock.Id (Page 0)