module Telegram.Tests.IncludedPlaylist

open System.Threading.Tasks
open Domain.Core
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
      presetId |> should equal Preset.mock.Id
      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> should not' (be Empty)
      Task.FromResult()

  let sut = IncludedPlaylist.list getPreset editMessageButtons

  sut Preset.mock.Id (Page 0)

[<Fact>]
let ``show should send included playlist``() =
  let getPreset =
    fun presetId ->
      presetId |> should equal User.userPresetMock.Id
      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> should not' (be Empty)
      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId |> should equal IncludedPlaylist.mock.Id
      0L |> Task.FromResult

  let sut = IncludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut User.userPresetMock.Id IncludedPlaylist.mock.Id