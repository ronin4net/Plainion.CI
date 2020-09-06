﻿module Plainion.CI.Tasks.PNuGet

open System
open System.IO
open Fake.Core
open Fake.DotNet.NuGet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open PMsBuild

/// Creates a NuGet package with the given files and NuSpec at the packageOut folder.
/// Version is taken from changelog.md
let Pack projectRoot solutionPath projectName outputPath nuspec packageOut files =
    let release = projectRoot |> GetChangeLog 
        
    Directory.create packageOut
    Shell.cleanDir packageOut

    let assemblies = 
        files 
        |> Seq.map(fun (source,_,_) -> source)
        |> Seq.collect(fun pattern -> !! (outputPath </> pattern))
        |> Seq.map Path.GetFileName
        |> List.ofSeq

    assemblies
    |> Seq.iter( fun a -> Trace.trace (sprintf "Adding file %s to package" a))

    let dependencies =
        let getDependencies (project:VsProject) =
            let packagesConfig = project.Location |> Path.GetDirectoryName </> "packages.config"

            if packagesConfig |> File.exists then
                packagesConfig 
                |> Fake.DotNet.NuGet.NuGet.getDependencies
                |> List.map(fun d -> d.Id,d.Version.AsString)
            else
                project.PackageReferences
                |> List.map(fun d -> d.Name,d.Version)

        solutionPath
        |> PMsBuild.API.GetProjects
        |> Seq.filter(fun e -> assemblies |> List.exists ((=)e.Assembly))
        |> Seq.collect getDependencies
        |> Seq.distinct
        |> List.ofSeq

    dependencies
    |> Seq.iter( fun d -> Trace.trace (sprintf "Package dependency detected: %A" d))

    nuspec 
    |>  NuGet.NuGet (fun p ->  {p with OutputPath = packageOut
                                       WorkingDir = outputPath
                                       Project = projectName
                                       Dependencies = dependencies 
                                       Version = release |> Option.map(fun x -> x.AssemblyVersion) |? defaultAssemblyVersion
                                       ReleaseNotes = release |> Option.map(fun x -> x.Notes |> String.concat Environment.NewLine) |? ""
                                       Files = files }) 

/// Publishes the NuGet package specified by packageOut, projectName and current version of ChangeLog.md
/// to NuGet (https://www.nuget.org/api/v2/package)              
let PublishPackage projectRoot packageName packageOut =
    let release = projectRoot |> GetChangeLog 

    NuGet.NuGetPublish (fun p -> {p with OutputPath = packageOut
                                         WorkingDir = projectRoot
                                         Project = packageName
                                         Version = release |> Option.map(fun x -> x.AssemblyVersion) |? defaultAssemblyVersion
                                         PublishUrl = "https://www.nuget.org/api/v2/package"
                                         Publish = true }) 
