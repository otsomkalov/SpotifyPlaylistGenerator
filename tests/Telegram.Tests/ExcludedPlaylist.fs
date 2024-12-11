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

let getPreset =
  fun presetId ->
    presetId |> should equal Mocks.preset.Id

    Mocks.preset |> Task.FromResult

[<Fact>]
let ``list should send excluded playlists`` () =
  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> Seq.length
      |> should equal 2

      Task.FromResult()

  let sut = ExcludedPlaylist.list getPreset editMessageButtons

  sut Mocks.preset.Id (Page 0)

[<Fact>]
let ``show should send excluded playlist details`` () =
  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 3
      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (Mocks.excludedPlaylist.Id |> ReadablePlaylistId.value)

      0L |> Task.FromResult

  let sut = ExcludedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Mocks.presetId Mocks.excludedPlaylist.Id

[<Fact>]
let ``remove should delete playlist and show excluded playlists`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetId
      playlistId |> should equal Mocks.excludedPlaylist.Id
      Task.FromResult()

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 2
      Task.FromResult()

  let showNotification =
    fun _ -> Task.FromResult()

  let sut = ExcludedPlaylist.remove getPreset editMessageButtons removePlaylist showNotification

  sut Mocks.presetId Mocks.excludedPlaylist.Id