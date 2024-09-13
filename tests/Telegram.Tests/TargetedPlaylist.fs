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
      presetId |> should equal Mocks.preset.Id

      Mocks.preset |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard
      |> Seq.length
      |> should equal 2

      Task.FromResult()

  let sut = TargetedPlaylist.list getPreset editMessageButtons

  sut Mocks.presetId (Page 0)

[<Fact>]
let ``show should send targeted playlist details`` () =
  let getPreset =
    fun presetId ->
      presetId |> should equal User.userPresetMock.Id

      Mocks.preset |> Task.FromResult

  let editMessageButtons =
    fun text (replyMarkup: InlineKeyboardMarkup) ->
      replyMarkup.InlineKeyboard |> Seq.length |> should equal 3

      Task.FromResult()

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (Mocks.targetedPlaylist.Id |> WritablePlaylistId.value)

      0L |> Task.FromResult

  let sut = TargetedPlaylist.show editMessageButtons getPreset countPlaylistTracks

  sut Mocks.presetId Mocks.targetedPlaylist.Id

[<Fact>]
let ``remove should delete playlist and show targeted playlists`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetId
      playlistId |> should equal Mocks.targetedPlaylist.Id
      Task.FromResult()

  let showNotification =
    fun _ -> Task.FromResult()

  let listTargetedPlaylists =
    fun presetId  page ->
      presetId |> should equal Mocks.presetId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut = TargetedPlaylist.remove removePlaylist showNotification listTargetedPlaylists

  sut Mocks.presetId Mocks.targetedPlaylist.Id