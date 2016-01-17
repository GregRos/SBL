namespace SBL.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open System.Collections
open SBL
open System.ComponentModel
open System.Collections.Specialized
open System

type internal WhereList<'T>  (inner : IList<IBindable<'T>>, notifier : ISeqChangeNotifier<'T>, filter : 'T -> bool) as x =
    let indexList = List<int>()
    let ilist = x :> IList<IBindable<'T>>
    let changedEvent = Event<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>()
    let mySubsribeKey = obj()
    let reset() = 
        indexList.Clear()
        indexList.AddRange(inner |> Seq.indexed |> Seq.choose (fun (i,v) -> if filter (v.Value) then Some i else None))
    
    let removeFromFiltered index =
        let mutable foundIndex = None
        for i in 0 .. indexList.Count - 1 do
            if indexList.[i] = index then
                indexList.RemoveAt(i)
                foundIndex <- Some i
            
            if indexList.[i] >= index then indexList.[i] <- indexList.[i] - 1

        foundIndex

    let insertIntoFiltered realIndex value assumeFiltered = 
        let isFiltered = defaultArg assumeFiltered (filter value)
        let mutable insertedPosition = None
        for i = 0 to indexList.Count do
            if isFiltered && insertedPosition.IsNone && indexList.[i] >= realIndex then
                indexList.Insert(i, realIndex)
                insertedPosition <- Some i
            elif indexList.[i] >= realIndex then
                indexList.[i] <- indexList.[i] + 1

        if insertedPosition.IsNone && isFiltered then
            indexList.Add(realIndex)
            Some <| indexList.Count - 1
        elif not isFiltered then
            None
        else
            Some <| insertedPosition.Value

    let updateFiltered realIndex oldValue value =
        let shouldExist = filter value
        let mutable insertedPosition = None
        let doesExist = indexList |> Seq.exists (fun i -> i = realIndex)
        let posToInsert = indexList |> Seq.tryFindIndex (fun i -> i >= realIndex) |> Option.orValue (indexList.Count)
        match doesExist, shouldExist with
        | false, false -> None //invisible item at this index was changed. Nothing happens.
        | true, true -> //visible item at this index has changed
            Replaced(posToInsert, [oldValue], [value]) |> Some
        | true, false -> //item at this index no longer matches the filter
            indexList.RemoveAt(posToInsert)
            Removed(posToInsert, [oldValue]) |> Some
        | false, true -> //item at this index now matches the filter
            indexList.Insert(posToInsert, realIndex)
            Added(posToInsert, [value]) |> Some
            

    let innerOnNotification(changed : SeqChangeInfo<_>) =
        let outerChanged = 
            let removeItems start vs = 
                let mutable fOldStartIndex = None
                let oldItems = List<_>()
                for i, v in vs |> List.indexed do
                    let curIndex = (removeFromFiltered (i + start))
                    fOldStartIndex <- fOldStartIndex |> Option.orOption curIndex
                    if curIndex.IsSome then oldItems.Add(v)
                fOldStartIndex, oldItems |> List.ofSeq    

            let addItems start vs assumeFiltered = 
                let mutable fNewStartIndex = None
                let newItems = List<_>()
                for i, v in vs |> List.indexed do
                    let curIndex = insertIntoFiltered (i + start) v assumeFiltered
                    fNewStartIndex <- fNewStartIndex |> Option.orOption curIndex
                    if curIndex.IsSome then newItems.Add(v)
                fNewStartIndex, newItems |> List.ofSeq                

            let updateItems start oldVs newVs =
                [ for i, (oldV, newV) in Seq.zip oldVs newVs |> Seq.indexed do
                    match updateFiltered i oldV newV with
                    | Some change -> yield change
                    | _ -> ()
                ]

            match changed with
            | Added(i, newVs) -> 
                let i, vs = addItems i newVs None
                if i.IsNone then [] else [Added(i.Value, vs)]
            | Removed(i, oldVs) ->
                let i, vs = removeItems i oldVs
                if i.IsNone then [] else [Removed(i.Value, oldVs)]
            | Replaced(i, oldVs, newVs) ->
                updateItems i oldVs newVs
            | Moved(oldI, newI, vs) ->
                let oldI, oldVs = removeItems oldI vs
                let newI, _ = addItems newI vs (Some true)
                match oldI, newI with
                | Some oldI, Some newI -> [Moved(oldI, newI, oldVs)]
                | Some oldI, None -> [Removed(oldI, oldVs)]
                | None, Some newI -> [Added(newI, oldVs)]
                | None, None -> []
            | Reset -> 
                reset()
                [Reset]
        
        outerChanged |> List.iter (fun change -> changedEvent.Trigger(x, change.ToArgs) )
    do 
        reset()
        notifier.Changed.Add(innerOnNotification)

    interface INotifyCollectionChanged with
        [<CLIEventAttribute>]
        member x.CollectionChanged = changedEvent.Publish

    interface IList<IBindable<'T>> with
        member x.IsReadOnly = inner.IsReadOnly
        member x.Count = indexList.Count
        member x.GetEnumerator() = 
            indexList |> Seq.map (fun i -> inner.[i]) |> Seq.getIter
        member x.GetEnumerator() : IEnumerator = ilist.GetEnumerator() :>_
        member x.Contains item = indexList |> Seq.exists (fun i -> obj.Equals(inner.[i], item))
        member x.Add item = inner.Add(item)
        member x.Clear() = inner.Clear()
        member x.CopyTo(arr, i) =
            for i = 0 to indexList.Count do
                arr.[i] <- inner.[indexList.[i]]
        member x.IndexOf item = indexList |> Seq.findIndex (fun i -> obj.Equals(inner.[i], item))
        member x.Remove item = 
            let indexOf = ilist |> Seq.tryFindIndex (fun v -> obj.Equals(v, item))
            if indexOf.IsNone then false
            else
                ilist.RemoveAt(indexOf.Value)
                true
        member x.Insert(index, item) =
            let realIndex = indexList.[index]
            ilist.Insert(realIndex, item)

        member x.RemoveAt(index) =
            let realIndex = indexList.[index]
            ilist.RemoveAt(realIndex)

        member x.Item
            with get i = inner.[indexList.[i]]
            and set i v = inner.[indexList.[i]] <- v