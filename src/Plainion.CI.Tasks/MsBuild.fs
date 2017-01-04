﻿module Plainion.CI.Tasks.PMsBuild

open System
open System.IO
open System.Xml.Linq

let private xn n = XName.Get(n,"http://schemas.microsoft.com/developer/msbuild/2003")

/// Retruns all project files referenced by the given solution
let GetProjectFiles solution =
    let solutionDir = Path.GetDirectoryName(solution)

    // Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Plainion.CI", "src\Plainion.CI\Plainion.CI.csproj", "{E81B5CDC-72D9-4DEB-AF55-9BA7409C7CBF}"
    File.ReadLines(solution)
    |> Seq.filter (fun line -> line.StartsWith("Project"))
    |> Seq.map (fun line -> line.Split([|','|]).[1])
    |> Seq.map(fun file -> file.Trim().Trim('"'))
    |> Seq.map(fun file -> Path.Combine(solutionDir, file))
    |> Seq.filter File.Exists
    |> List.ofSeq 

type VsProject = { Location : string
                   Assembly : string }

/// Loads the visual studio project from given location
let LoadProject (projectFile:string) =
    let root = XElement.Load(projectFile)

    let allProperties = 
        root.Elements(xn "PropertyGroup") 
        |> Seq.collect(fun e -> e.Elements())   
        |> List.ofSeq

    let assembly = 
        allProperties 
        |> Seq.find(fun e -> e.Name = xn "AssemblyName")
        |> (fun e -> e.Value)

    let outputType = 
        allProperties 
        |> Seq.find(fun e -> e.Name = xn "OutputType")
        |> (fun e -> e.Value)

    let assemblyExtension = if outputType = "WinExe" || outputType = "Exe" then "exe" else "dll"

    { Location = projectFile
      Assembly = (sprintf "%s.%s" assembly assemblyExtension) }