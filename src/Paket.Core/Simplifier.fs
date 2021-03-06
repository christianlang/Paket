﻿module Paket.Simplifier

open System.IO
open Logging
open System
open Paket.Domain
open Paket.PackageResolver

let private formatDiff (before : string) (after : string) =
    let nl = Environment.NewLine
    nl + "Before:" + nl + nl + before + nl + nl + nl + "After:" + nl + nl + after + nl + nl

let private simplify file before after =
    if before <> after then
        File.WriteAllText(file, after)
        tracefn "Simplified %s" file
        traceVerbose (formatDiff before after)
    else
        tracefn "%s is already simplified" file

let private interactiveConfirm fileName (PackageName package) = 
        Utils.askYesNo(sprintf "Do you want to remove indirect dependency %s from file %s ?" package fileName)

let Analyze(allPackages : list<ResolvedPackage>, depFile : DependenciesFile, refFiles : ReferencesFile list, interactive) = 
    
    let depsLookup =
        allPackages
        |> Seq.map (fun package -> NormalizedPackageName package.Name,
                                               package.Dependencies 
                                               |> Set.map (fun (name,_,_) -> name))
        |> Map.ofSeq

    let rec getAllDeps (package : NormalizedPackageName) =
        Set.union depsLookup.[package]
                  (Set.unionMany (depsLookup.[package] |> Set.map NormalizedPackageName |> Set.map getAllDeps))

    let flattenedLookup = depsLookup |> Map.map (fun key _ -> getAllDeps key)

    let getSimplifiedDeps (depNameFun : 'a -> PackageName) fileName allDeps =
        let indirectDeps = 
            allDeps 
            |> List.map depNameFun 
            |> List.fold (fun set directDep -> Set.union set (flattenedLookup.[ NormalizedPackageName directDep ])) Set.empty
        let depsToRemove =
            if interactive then indirectDeps |> Set.filter (interactiveConfirm fileName) else indirectDeps
            |> Set.map NormalizedPackageName
        allDeps |> List.filter (fun dep -> not <| Set.contains (NormalizedPackageName (depNameFun dep)) depsToRemove)

    let simplifiedDeps = depFile.Packages |> getSimplifiedDeps (fun p -> p.Name) depFile.FileName |> Seq.toList
    let refFiles' = if depFile.Options.Strict 
                    then refFiles 
                    else refFiles |> List.map (fun refFile -> {refFile with NugetPackages = 
                                                                            refFile.NugetPackages |> getSimplifiedDeps id refFile.FileName})

    DependenciesFile(depFile.FileName, depFile.Options, depFile.Sources, simplifiedDeps, depFile.RemoteFiles), refFiles'

let Simplify (dependenciesFileName,interactive) = 
    if not <| File.Exists dependenciesFileName then
        failwithf "%s file not found." dependenciesFileName
    let depFile = DependenciesFile.ReadFromFile dependenciesFileName
    let lockFilePath = depFile.FindLockfile()
    if not <| File.Exists(lockFilePath.FullName) then 
        failwith "lock file not found. Create lock file by running paket install."

    let lockFile = LockFile.LoadFrom(lockFilePath.FullName)
    let packages = lockFile.ResolvedPackages |> Seq.map (fun kv -> kv.Value) |> List.ofSeq
    let refFiles = 
        ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) 
        |> Array.choose (fun p -> ProjectFile.FindReferencesFile <| FileInfo(p.FileName))
        |> Array.map ReferencesFile.FromFile
    let refFilesBefore = refFiles |> Array.map (fun refFile -> refFile.FileName, refFile) |> Map.ofArray

    let simplifiedDepFile, simplifiedRefFiles = Analyze(packages, depFile, Array.toList refFiles, interactive)
    
    printfn ""
    simplify depFile.FileName <| depFile.ToString() <| simplifiedDepFile.ToString()

    if depFile.Options.Strict then
        traceWarn ("Strict mode detected. Will not attempt to simplify " + Constants.ReferencesFile + " files.")
    else
        for refFile in simplifiedRefFiles do
            simplify refFile.FileName <| refFilesBefore.[refFile.FileName].ToString() <| refFile.ToString()