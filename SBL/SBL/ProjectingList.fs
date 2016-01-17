namespace SBL.Collections
open System.Collections.Generic
open System.Collections.ObjectModel
open SBL
open System

type internal ProjectionList<'a, 'b>(inner : IList<'a>, convertOut : (int -> 'a -> 'b) option, convertIn : (int -> 'b -> 'a) option) =
    let notifier = SeqChangeInfo.GetNotifier inner
    let outer = ObservableCollection()
    let expectInnerChange = ExpectEntry()
    let expectOuterChange = ExpectEntry()
    let innerList = inner
    let mutable changedEvent = Event<_>()
 
    static let mapSeqChanged changed (toTarget : IList<_>) f = 
        let getInnerRange index count = 
            toTarget.[index .. index + count] |> List.ofSeq
        let mapf index items =
            items |> List.mapi (fun i v -> f (i + index) v)
        match changed with
        | SeqChanged(Added(index, items)) -> Added(index, items |> mapf index)
        | SeqChanged(Removed(index, oldItems)) -> Removed(index, getInnerRange index (oldItems.Length))
        | SeqChanged(Replaced(index, oldItems, newItems)) -> Replaced(index, getInnerRange index (oldItems.Length), newItems |> mapf index)
        | SeqChanged(Moved(oldIndex, newIndex, items)) -> Moved(oldIndex, newIndex, getInnerRange oldIndex (items.Length))
        | SeqChanged(Reset) | _ -> Reset

    let onOuterChanged args =
        if not expectOuterChange.Expect then
            if convertIn.IsNone then failwith "Cannot write"
            let convertIn = convertIn.Value
            let innerArgs = mapSeqChanged args (innerList) (convertIn)
            expectInnerChange.ExpectExecute(fun() -> innerList.SyncWithChange(innerArgs, fun () -> outer |> Seq.mapi convertIn))
            changedEvent.Trigger(args)

    let innerItemChanged i args =
        let item = inner.[i]
        outer.[i] <- convertOut.Value i item
        ()
        
    let onInnerChanged args = 
        if not expectInnerChange.Expect then
            if convertOut.IsNone then failwith "Cannot read"
            let convertOut = convertOut.Value
            let outerArgs = mapSeqChanged args outer convertOut
            expectOuterChange.ExpectExecute(fun() -> outer.SyncWithChange(outerArgs, fun() -> innerList |> Seq.mapi convertOut))
            changedEvent.Trigger(outerArgs)

    let outerNotifier = SeqChangeInfo.GetNotifier outer
    let innerNotifier = SeqChangeInfo.GetNotifier inner
    let innerChangeToken = innerNotifier.Changed.Subscribe(onInnerChanged)
    let outerChangeToken = outerNotifier.Changed.Subscribe(onOuterChanged)
    
    do onInnerChanged(Reset)
    
    interface IDisposable with
        member x.Dispose() = 
            innerChangeToken.Dispose()
            outerChangeToken.Dispose()
            changedEvent <- Event<_>()

    interface ISeqChangeNotifier<'b> with
        member x.Changed = changedEvent.Publish

    interface IList<'b> with
        member x.Add(item: 'b): unit = 
            outer.Add(item)
        member x.Clear(): unit = 
            outer.Clear()
        member x.Contains(item: 'b): bool = 
            outer.Contains(item)
        member x.CopyTo(array: 'b [], arrayIndex: int): unit = 
            outer.CopyTo(array, arrayIndex)
        member x.Count: int = 
            outer.Count
        member x.GetEnumerator(): IEnumerator<'b> = 
            outer.GetEnumerator()
        member x.GetEnumerator(): System.Collections.IEnumerator = 
            outer.GetEnumerator() :> _
        member x.IndexOf(item: 'b): int = 
            outer.IndexOf(item)
        member x.Insert(index: int, item: 'b): unit = 
            outer.Insert(index, item)
        member x.IsReadOnly: bool = 
            inner.IsReadOnly
        member x.Item
            with get (index: int): 'b = 
                outer.[index]
            and set (index: int) (v: 'b): unit = 
                outer.[index] <- v
        member x.Remove(item: 'b): bool = 
            outer.Remove(item)
        member x.RemoveAt(index: int): unit = 
            outer.RemoveAt(index)
    member x.Changed = changedEvent.Publish

