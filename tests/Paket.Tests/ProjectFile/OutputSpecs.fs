﻿module Paket.ProjectFile.OutputSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect lib output type for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Library

[<Test>]
let ``should detect exe output type for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect target framework for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should detect target framework for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_Client))