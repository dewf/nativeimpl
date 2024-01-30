module Generators.DepsResolver

type OpenGroup = {
    StopAt: string
    SoFar: string list
}

type RecResult = {
    Singles: string list
    Closed: string list list       // completed mutuals
    Open: Map<string, OpenGroup>
} with
    static member Init = { Singles = []; Closed = []; Open = Map.empty }

type SubResult =
    | StartGroup of stopAt: string
    | Normal of RecResult
    
type DepOrderItem =
    | Single of id: string
    | Mutual of ids: string list
    
let private mergeMaps (m1: Map<'K,'V>) (m2: Map<'K,'V>) (combineFunc: 'V -> 'V -> 'V) =
    (m1, m2 |> Map.toList)
    ||> List.fold (fun acc (k, v) ->
        if acc.ContainsKey k then
            let nextValue =
                combineFunc acc[k] v
            acc.Add(k, nextValue)
        else
            acc.Add(k, v))
    
let private subsFoldFunc (current: string) (acc: RecResult) (sub: SubResult) =
    match sub with
    | StartGroup stopAt ->
        assert (stopAt <> current)
        let nextOpen =
            match acc.Open.TryFind stopAt with
            | Some group ->
                let nextGroup =
                    { group with SoFar = current :: group.SoFar }
                acc.Open.Add(stopAt, nextGroup)
            | None ->
                acc.Open.Add(stopAt, { StopAt = stopAt; SoFar = [ current ] })
        { acc with Open = nextOpen }
    | Normal recResult ->
        let nextSingles =
            acc.Singles @ recResult.Singles
            |> List.sort
            |> List.distinct
        let nextClosed =
            acc.Closed @ recResult.Closed
            |> List.map List.sort
            |> List.distinct
        let nextOpen =
            let mergeFunc g1 g2 =
                { g1 with SoFar = (g1.SoFar @ g2.SoFar) |> List.sort |> List.distinct }
            mergeMaps acc.Open recResult.Open mergeFunc
        { Singles = nextSingles; Closed = nextClosed; Open = nextOpen }
        
let private indent (x: int) =
    String.replicate (x * 3) " "
    
let private tryOverlap (set1: string list) (set2: string list) =
    let s1set = set1 |> Set.ofList
    if set2 |> List.exists s1set.Contains then
        let combined =
            set1 @ set2
            |> List.sort
            |> List.distinct
        Some combined
    else
        None
    
type MergeResults = {
    NoOverlap: string list list
    Unchecked: string list list
}

let private mergeOverlapping (input: string list list) =
    let rec recInner (results: MergeResults) =
        match results.Unchecked with
        | [] ->
            results
        | toCheck :: etc ->
            // does 'toCheck' merge with any of 'etc' ?
            let checkedEtc =
                etc
                |> List.map (fun etcItem ->
                    match tryOverlap toCheck etcItem with
                    | Some combined ->
                        combined, true
                    | None ->
                        etcItem, false)
            let didMerge =
                checkedEtc
                |> List.exists snd
            if didMerge then
                let nextUnchecked =
                    checkedEtc
                    |> List.map fst
                recInner { results with Unchecked = nextUnchecked }
            else
                let nextNoOverlap =
                    toCheck :: results.NoOverlap
                recInner { NoOverlap = nextNoOverlap; Unchecked = etc }
    (recInner { NoOverlap = []; Unchecked = input })
        .NoOverlap
        
let private depOrderItemSort (depsMap: Map<string, string list>) (items: DepOrderItem list) =
    let aDependsOnB (a: DepOrderItem) (b: DepOrderItem) =
        // can we trace a path from any of A's items to any of B's items?
        let lookingFor =
            match b with
            | Single id -> [ id ]
            | Mutual idList -> idList
            |> Set.ofList
        let rec findInner (current: string) (stopSet: Set<string>) =
            let currentDeps =
                depsMap[current]
            if currentDeps |> List.exists (fun id -> lookingFor |> Set.contains id) then
                true
            else
                // recurse until we can't anymore
                currentDeps
                |> List.filter (fun id -> stopSet.Contains id |> not)
                |> List.exists (fun id -> findInner id (stopSet.Add current)) // no backtracking!
        let startFrom =
            match a with
            | Single id -> [ id ]
            | Mutual ids -> ids
        startFrom
        |> List.exists (fun id -> findInner id Set.empty)
    let compareFunc (x: DepOrderItem) (y: DepOrderItem) =
        if aDependsOnB x y then
            // printfn "%A depends on %A" x y
            1
        elif aDependsOnB y x then
            // printfn "%A depends on %A" y x
            -1
        else
            // printfn "no relation: %A/%A" x y
            0
    items
    |> List.sortWith compareFunc
    
let depsOrderSolver (things: 'a list) (idFunc: 'a -> string) (depsFunc: 'a -> string list) =
    let depsMap =
        things
        |> List.map (fun th ->
            let id =
                idFunc th
            let deps =
                depsFunc th
                |> List.filter (fun x -> x <> id) // ignore self-references for now, we trust those can be handled at least!
            id, deps)
        |> Map.ofList
    let rec depFunc (current: string) (inProgress: Set<string>) =
        // printfn "%s - depFunc %d" (indent depth) current
        let subs =
            depsMap[current]
            |> List.map (fun id ->
                if inProgress.Contains id then
                    // circular dependency, continue no further
                    // printfn "%s   = starting group [ends @ %d]" (indent depth) id
                    StartGroup id
                else
                    let nextInProgress =
                        inProgress.Add(current)
                    depFunc id nextInProgress
                    |> Normal )
        // merge sub results before we
        let subResults =
            (RecResult.Init, subs)
            ||> List.fold (subsFoldFunc current)
        // now perform handling that involves the current ID
        let toClose, stillOpen =
            subResults.Open
            |> Map.toList
            |> List.map snd
            |> List.partition (fun group -> group.StopAt = current)
        // anything that closed at the current node, is by definition part of the same group/island
        let newlyClosed =
            toClose
            |> List.collect (fun group -> current :: group.SoFar)
            |> List.distinct
        // otherwise capture ourselves into all open groups
        let stillOpen =
            stillOpen
            |> List.map (fun group -> { group with SoFar = current :: group.SoFar })
        // add self to singles? only if not captured
        let nextSingles =
            if newlyClosed.Length = 0 && stillOpen.Length = 0 then
                subResults.Singles @ [ current ]
            else
                subResults.Singles
        let nextClosed =
            if newlyClosed.IsEmpty then
                subResults.Closed
            else
                subResults.Closed @ [ newlyClosed |> List.sort ]
                |> List.distinct
            // merge overlapping closed groups!
            |> mergeOverlapping
        let nextOpen =
            stillOpen
            |> List.map (fun group -> group.StopAt, group)
            |> Map.ofList
        { Singles = nextSingles; Closed = nextClosed; Open = nextOpen }
    // run against all top-level things
    things
    |> List.collect (fun th ->
        let result =
            depFunc (idFunc th) Set.empty
        let singleParts =
            result.Singles
            |> List.map Single
        let mutualParts =
            result.Closed
            |> List.map Mutual
        singleParts @ mutualParts)
    |> List.distinct
    // use ID map to sort this list
    |> depOrderItemSort depsMap
  
