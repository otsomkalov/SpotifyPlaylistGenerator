module Telegram.Tests.IncludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Moq
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open FsUnit.Xunit
open Xunit
open Domain.Workflows
open Telegram.Workflows
open otsom.fs.Bot

let getPreset =
  fun presetId ->
    presetId |> should equal Mocks.preset.Id

    Mocks.preset |> Task.FromResult

[<Fact>]
let ``list should send included playlists`` () =
  let botMessageCtx =
    { new IEditMessageButtons with
        member this.EditMessageButtons =
          fun text buttons ->
            buttons |> Seq.length |> should equal 2

            Task.FromResult() }

  let sut = IncludedPlaylist.list getPreset botMessageCtx

  sut Mocks.preset.Id (Page 0)

[<Fact>]
let ``show should send included playlist details`` () =

  let botMessageCtx =
    { new IEditMessageButtons with
        member this.EditMessageButtons =
          fun text buttons ->
            buttons |> Seq.length |> should equal 3

            Task.FromResult() }

  let countPlaylistTracks =
    fun playlistId ->
      playlistId
      |> should equal (Mocks.includedPlaylist.Id |> ReadablePlaylistId.value)

      0L |> Task.FromResult

  let sut = IncludedPlaylist.show botMessageCtx getPreset countPlaylistTracks

  sut Mocks.presetId Mocks.includedPlaylist.Id

[<Fact>]
let ``remove should delete playlist and show included playlists`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Mocks.presetId
      playlistId |> should equal Mocks.includedPlaylist.Id
      Task.FromResult()

  let botMessageCtx =
    { new IEditMessageButtons with
        member this.EditMessageButtons =
          fun text buttons ->
            buttons |> Seq.length |> should equal 2

            Task.FromResult() }

  let showNotification = fun _ -> Task.FromResult()

  let sut =
    IncludedPlaylist.remove getPreset botMessageCtx removePlaylist showNotification

  sut Mocks.presetId Mocks.includedPlaylist.Id
