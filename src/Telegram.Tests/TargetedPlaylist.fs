module Telegram.Tests.TargetedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open FsUnit
open Xunit
open Domain.Workflows

[<Fact>]
let ``list should send targeted playlists`` () =
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

  let sut = TargetedPlaylist.list getPreset editMessageButtons

  sut Preset.mockId (Page 0)

[<Fact>]
let ``show should send targeted playlist`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal User.userPresetMock.Id

      Preset.mock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 3

      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (TargetedPlaylist.mock.Id |> WritablePlaylistId.value)

      0L |> Task.FromResult

  let sut = TargetedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Preset.mockId TargetedPlaylist.mock.Id

[<Fact>]
let ``remove should remove playlist and show the list`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Preset.mockId
      playlistId |> should equal TargetedPlaylist.mock.Id
      Task.FromResult()

  let answerCallbackQuery =
    fun _ -> Task.FromResult()

  let listTargetedPlaylists =
    fun presetId  page ->
      presetId |> should equal Preset.mockId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut = Workflows.TargetedPlaylist.remove removePlaylist answerCallbackQuery listTargetedPlaylists

  sut Preset.mockId TargetedPlaylist.mock.Id