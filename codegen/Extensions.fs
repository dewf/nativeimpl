module Extensions

module String =
    let tryRemovePrefix (prefix: string) (str: string) =
        if str.StartsWith(prefix) then
            str.Substring(prefix.Length)
            |> Some
        else
            None
            
module List =
    let extractFirstMatching (pred: 'a -> bool) (xs: 'a list) =
        let rec recInner (revSkipped: 'a list) (etc: 'a list) =
            match etc with
            | [] ->
                None, xs // original list, since it wasn't found, no need to reverse revSkipped which is the same
            | h :: etc ->
                if pred h then
                    Some h, (List.rev revSkipped) @ etc
                else
                    recInner (h :: revSkipped) etc
        recInner [] xs
        
    let zipWithIndex (xs: 'a list) =
        xs |> List.mapi (fun i x -> (i, x))
//    let safeTake (n: int) (xs: 'a list) =
//        if xs.Length >= n then
//            List.take n xs
//        else
//            []
    let safeTake (count: int) (xs: 'a list) =
        // this isn't tail recursive so don't use too high a count :)
        let rec takeMore (n: int) (current: 'a list) =
            match current with
            | x :: etc when n > 0 ->
                x :: takeMore (n - 1) etc
            | _ ->
                // either n = 0, or no head element available
                []
        takeMore count xs
    let safeSplit (count: int) (xs: 'a list) =
        let rec takeMore (n: int) (current: 'a list) (revFront: 'a list) =
            match current with
            | h :: tail when n > 0 ->
                takeMore (n - 1) tail (h :: revFront)
            | _ ->
                (List.rev revFront), current
        takeMore count xs []
    let splitWhile (pred: 'a -> bool) (xs: 'a list) =
        // basically a combined take/skip
        let front =
            xs |> List.takeWhile pred
        let etc =
            xs |> List.skip front.Length
        front, etc
    
    let skipWhile2 (pred: 'a -> 'b -> bool) (xa: 'a list) (xb: 'b list) =
        let rec inner (arem: 'a list) (brem: 'b list) =
            match arem, brem with
            | a :: aetc, b :: betc ->
                if pred a b then
                    inner aetc betc
                else
                    arem, brem
            | _ ->
                arem, brem
        inner xa xb
        
    let splitAtLast (xs: 'a list) =
        match List.rev xs with
        | h :: etc ->
            List.rev etc, h
        | [] ->
            failwith "List.splitAtLast - list was empty"
            
    let removePrefix (prefix: 'a list) (xs: 'a list) =
        let rec removeWhileEqual (pRemain: 'a list) (xRemain: 'a list) =
            match pRemain, xRemain with
            | pHead :: pEtc, xHead :: xEtc when pHead = xHead ->
                removeWhileEqual pEtc xEtc
            | [], xEtc ->
                // xEtc might be empty, but we did exhaust the prefix as asked
                Some xEtc
            | _ :: _, [] ->
                // ran out of target list before consuming prefix
                None
            | _ ->
                // heads exist but not equal, fail
                None
        removeWhileEqual prefix xs

    let partitionAndMap (partFunc: 'a -> bool * 'b) (xs: 'a list): 'b list * 'b list =
        xs
        |> List.map partFunc
        |> List.partition fst
        |> (fun (left, right) ->
            left |> List.map snd, right |> List.map snd)
    
    let startsWith (prefix: 'a list) (xs: 'a list) =
        let rec recInner (pRemain: 'a list) (xRemain: 'a list) =
            match pRemain, xRemain with
            | pHead :: pEtc, xHead :: xEtc when pHead = xHead ->
                recInner pEtc xEtc
            | [], xEtc ->
                // xEtc might be empty, but we did exhaust the prefix as asked
                true
            | _ :: _, [] ->
                // ran out of target list before consuming prefix
                false
            | _ ->
                // heads exist but not equal, fail
                false
        recInner prefix xs

    let mapPrefixes (f: 'a list -> 'b) (xs: 'a list) =
        // I'm sure there's a better name for this operation, no idea what
        // [1; 2; 3; 4; 5] -> [[1]; [1; 2]; [1; 2; 3]; [1; 2; 3; 4]; [1; 2; 3; 4; 5]] -> List.map f
        ([], xs)
        ||> List.scan (fun acc i ->
            i :: acc)
        |> List.map List.rev
        |> List.tail
        |> List.map f
        
    let allButLast (xs: 'a list) =
        xs
        |> List.rev
        |> List.tail
        |> List.rev

    type MapWithContextAcc<'context, 'b> = {
        Context: 'context
        RevResult: 'b list
    }
    let mapWithContext (f: 'context -> 'a -> 'context * 'b) (initContext: 'context) (xs: 'a list) =
        let result =
            ({ Context = initContext; RevResult = [] }, xs)
            ||> List.fold (fun acc item ->
                let nextContext, value =
                    f acc.Context item
                { Context = nextContext; RevResult = value :: acc.RevResult })
        result.RevResult
        |> List.rev
        