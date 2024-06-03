module Telegram.Tests.ExcludedPlaylist

open System.Threading.Tasks
open Domain.Core
open Domain.Tests
open Telegram
open Telegram.Bot.Types.ReplyMarkups
open Telegram.Core
open Telegram.Workflows
open FsUnit
open Xunit

[<Fact>]
let ``list should send excluded playlists`` () =
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

  let sut = ExcludedPlaylist.list getPreset editMessageButtons

  sut Preset.mock.Id (Page 0)

[<Fact>]
let ``remove should remove playlist and show the list`` () =
  let removePlaylist =
    fun presetId playlistId ->
      presetId |> should equal Preset.mockId
      playlistId |> should equal ExcludedPlaylist.mock.Id
      Task.FromResult()

  let answerCallbackQuery =
    fun _ -> Task.FromResult()

  let listExcludedPlaylists =
    fun presetId  page ->
      presetId |> should equal Preset.mockId
      page |> should equal (Page 0)
      Task.FromResult()

  let sut = ExcludedPlaylist.remove removePlaylist answerCallbackQuery listExcludedPlaylists

  sut Preset.mockId ExcludedPlaylist.mock.Id