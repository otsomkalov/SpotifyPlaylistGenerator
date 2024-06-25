module Telegram.Tests.TargetedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open FsUnit.Xunit
open Xunit
open Domain.Workflows
open Telegram.Workflows

[<Fact>]
let ``list should send targeted playlists`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMock.Id

      Mocks.presetMock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> Seq.length
      |> should equal 2

      Task.FromResult()

  let sut = TargetedPlaylist.list getPreset editMessageButtons

  sut Mocks.presetMockId (Page 0)

[<Fact>]
let ``show should send targeted playlist`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal User.userPresetMock.Id

      Mocks.presetMock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 3

      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (TargetedPlaylist.targetedPlaylistMock.Id |> WritablePlaylistId.value)

      0L |> Task.FromResult

  let sut = TargetedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Mocks.presetMockId TargetedPlaylist.targetedPlaylistMock.Id

[<Fact>]
let ``remove should remove playlist and show the list`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetMockId
      playlistId |> should equal TargetedPlaylist.targetedPlaylistMock.Id
      Task.FromResult()

  let answerCallbackQuery =
    fun _ -> Task.FromResult()

  let listTargetedPlaylists =
    fun presetId  page ->
      presetId |> should equal Mocks.presetMockId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut = Workflows.TargetedPlaylist.remove removePlaylist answerCallbackQuery listTargetedPlaylists

  sut Mocks.presetMockId TargetedPlaylist.targetedPlaylistMock.Id