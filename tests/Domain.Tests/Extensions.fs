module Domain.Tests.Extensions

open FsUnit
open Xunit

let inline equivalent expected =
  CustomMatchers.equivalent (fun a b -> Assert.Equivalent(a, b)) expected