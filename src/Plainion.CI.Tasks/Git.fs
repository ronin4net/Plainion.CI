﻿module Plainion.CI.Tasks.PGit

open System
open System.IO
open LibGit2Sharp
open Plainion.CI
open Fake.Core

/// Commits the given files to git repository
let Commit workspaceRoot ((files:string list), comment, name, email) =
    use repo = new Repository( workspaceRoot ) 

    files
    |> Seq.iter(fun file -> Commands.Stage(repo, file ) )

    let author = new Signature( name, email, DateTimeOffset.Now )

    repo.Commit( comment, author, author ) |> ignore

/// Pushes the local repository to the default remote one
let Push workspaceRoot (name, password) =
    // there are currently 2 blocking issues open with libgit2sharp and push related to windows and network:
    // - https://github.com/libgit2/libgit2sharp/issues/1429
    // - https://github.com/libgit2/libgit2/issues/4546
    // therefore we use a the command line "git" if found
    let cmdLineGit =
        Environment.GetEnvironmentVariable("PATH").Split([|';'|])
        |> Seq.map(fun path -> path.Trim())
        |> Seq.map(fun path -> Path.Combine(path, "git.exe"))
        |> Seq.tryFind File.Exists

    use repo = new Repository( workspaceRoot )
    let origin = repo.Network.Remotes.[ "origin" ]

    match cmdLineGit with
    | Some exe -> 
        // "https://github.com/plainionist/Plainion.CI.git"
        // https://stackoverflow.com/questions/29776439/username-and-password-in-command-for-git-push
        let uri = new Uri(origin.Url)
        let builder = new UriBuilder(uri)
        builder.UserName <- name
        builder.Password <- password
        
        let ret =
            { Program = exe
              Args = []
              WorkingDir = workspaceRoot
              CommandLine = (sprintf "%s %s" "push" (builder.Uri.ToString())) }
            |> Process.shellExec 

        if ret <> 0 then
            failwith "Failed to push using command line git.exe"
    | None ->
        let options = new PushOptions()
        options.CredentialsProvider <- (fun url usernameFromUrl types -> let credentials = new UsernamePasswordCredentials()
                                                                         credentials.Username <- name
                                                                         credentials.Password <- password
                                                                         credentials :> Credentials)

        repo.Network.Push( origin, @"refs/heads/master", options )

/// Returns all non-ignored pending changes
let PendingChanges workspaceRoot =
    use repo = new Repository( workspaceRoot )

    repo.RetrieveStatus()
    |> Seq.filter(fun e -> e.State.HasFlag( FileStatus.Ignored ) |> not )
    |> Seq.map(fun e -> e.FilePath)
    |> List.ofSeq
