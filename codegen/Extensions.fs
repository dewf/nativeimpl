module Extensions

module List =
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
