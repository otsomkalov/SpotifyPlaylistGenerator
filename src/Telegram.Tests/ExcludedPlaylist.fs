module Telegram.Tests.ExcludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open FsUnit.Xunit
open Xunit
open Domain.Workflows
open Telegram.Workflows

[<Fact>]
let ``list should send excluded playlists`` () =
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

  let sut = ExcludedPlaylist.list getPreset editMessageButtons

  sut Mocks.presetMock.Id (Page 0)

[<Fact>]
let ``show should send excluded playlist`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetMockId
      Mocks.presetMock |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (ExcludedPlaylist.excludedPlaylistMock.Id |> ReadablePlaylistId.value)

      0L |> Task.FromResult

  let sut = ExcludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Mocks.presetMockId ExcludedPlaylist.excludedPlaylistMock.Id

[<Fact>]
let ``remove should remove playlist and show the list`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetMockId
      playlistId |> should equal ExcludedPlaylist.excludedPlaylistMock.Id
      Task.FromResult()

  let answerCallbackQuery =
    fun _ -> Task.FromResult()

  let listExcludedPlaylists =
    fun presetId  page ->
      presetId |> should equal Mocks.presetMockId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut = ExcludedPlaylist.remove removePlaylist answerCallbackQuery listExcludedPlaylists

  sut Mocks.presetMockId ExcludedPlaylist.excludedPlaylistMock.Id