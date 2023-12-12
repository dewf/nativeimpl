module Extensions

module List =
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
        
    let allButLast (xs: 'a list) =
        if xs.IsEmpty then
            failwith "List.allButLast - list was empty!"
        else
            xs
            |> List.rev
            |> List.skip 1
            |> List.rev
