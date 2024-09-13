module Telegram.Tests.IncludedPlaylist

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
let ``list should send included playlists`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Mocks.preset.Id

      Mocks.preset |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2

      Task.FromResult()

  let sut = IncludedPlaylist.list getPreset editMessageButtons

  sut Mocks.preset.Id (Page 0)

[<Fact>]
let ``show should send included playlist details`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal Mocks.presetId
      Mocks.preset |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2

      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value)

      0L |> Task.FromResult

  let sut = IncludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Mocks.presetId Mocks.includedPlaylist.Id

[<Fact>]
let ``remove should delete playlist and show included playlists`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetId
      playlistId |> should equal Mocks.includedPlaylist.Id
      Task.FromResult()

  let showNotification = fun _ -> Task.FromResult()

  let listExcludedPlaylists =
    fun presetId page ->
      presetId |> should equal Mocks.presetId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut =
    IncludedPlaylist.remove removePlaylist showNotification listExcludedPlaylists

  sut Mocks.presetId Mocks.includedPlaylist.Id
