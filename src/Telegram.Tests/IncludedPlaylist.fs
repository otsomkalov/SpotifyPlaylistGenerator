module Telegram.Tests.IncludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open FsUnit
open Xunit
open Domain.Workflows
open Telegram.Workflows

[<Fact>]
let ``list should send included playlists`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Preset.mock.Id
      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let sut = IncludedPlaylist.list getPreset editMessageButtons

  sut Preset.mock.Id (Page 0)

[<Fact>]
let ``show should send included playlist`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Preset.mockId
      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (IncludedPlaylist.mock.Id |> ReadablePlaylistId.value)

      0L |> Task.FromResult

  let sut = IncludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Preset.mockId IncludedPlaylist.mock.Id

[<Fact>]
let ``remove should remove playlist and show the list`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Preset.mockId
      playlistId |> should equal IncludedPlaylist.mock.Id
      Task.FromResult()

  let answerCallbackQuery = fun _ -> Task.FromResult()

  let listExcludedPlaylists =
    fun presetId page ->
      presetId |> should equal Preset.mockId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut =
    IncludedPlaylist.remove removePlaylist answerCallbackQuery listExcludedPlaylists

  sut Preset.mockId IncludedPlaylist.mock.Id
