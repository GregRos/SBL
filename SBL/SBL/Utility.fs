[<AutoOpen>]
module internal SBL.Utility
open System.Collections.ObjectModel
open System.Collections.Generic
open System
open System.Collections
open System.Collections.Specialized

type SeqChangeAction = NotifyCollectionChangedAction
type SeqChangeInfo = NotifyCollectionChangedEventArgs

let ofObjList (l : IList) =
    match l with
    | null -> []
    | l -> 
        let items = l |> Seq.cast<obj> |> List.ofSeq
        let items2 = items |> Seq.cast<'t> |> List.ofSeq
        items2 |> List.ofSeq

type SeqChangeInfo<'t> =
| Reset
| Added of index:int * items : 't list
| Removed of index:int * items : 't list
| Replaced of index:int * prevItems : 't list * newItems : 't list
| Moved of prevIndex : int * newIndex : int * items : 't list
with 
    interface IContextualChangeInfo
    static member OfArgs (args : NotifyCollectionChangedEventArgs) : SeqChangeInfo<'t> =
        let oldList = args.OldItems |> ofObjList
        let newList = args.NewItems |> ofObjList
        match args.Action with
        | SeqChangeAction.Reset -> Reset
        | SeqChangeAction.Add -> Added(args.NewStartingIndex, newList)
        | SeqChangeAction.Remove -> Removed(args.OldStartingIndex, oldList)
        | SeqChangeAction.Replace -> Replaced(args.OldStartingIndex, oldList, newList)
        | SeqChangeAction.Move -> Moved(args.OldStartingIndex, args.NewStartingIndex, oldList)
        | _ -> failwith "Unknown action"

    member changed.ToArgs  =
        let toMList (lst : _ seq) = List<_>(lst)
        match changed with
        | Added (index, items) -> SeqChangeInfo(SeqChangeAction.Add, items |> toMList, index)
        | Removed (index, items) -> SeqChangeInfo(SeqChangeAction.Remove, items |> toMList, index)
        | Replaced(index, oldItems, newItems) -> SeqChangeInfo(SeqChangeAction.Replace, newItems |> toMList, oldItems |> toMList)
        | Moved(oldIndex, newIndex, items) -> SeqChangeInfo(SeqChangeAction.Move, items |> toMList, newIndex, oldIndex)
        | Reset -> SeqChangeInfo(SeqChangeAction.Reset)

type IChangeNotifier<'t when 't :> IContextualChangeInfo> = 
    abstract Changed : IEvent<'t> 

type SeqChangeInfo<'t> with

    static member GetNotifier (o : obj) =
        match o with
        | :? IChangeNotifier<SeqChangeInfo<'t>> as notify -> notify
        | :? IChangeNotifier<IContextualChangeInfo> as notify ->
            let event = Event<_>()
            notify.Changed.Add(function :? SeqChangeInfo<'t> as changed -> event.Trigger(changed) | _ -> event.Trigger(Reset))
            {new IChangeNotifier<SeqChangeInfo<'t>> with member x.Changed = event.Publish}
        | :? INotifyCollectionChanged as notify ->
            let event = Event<_>()
            notify.CollectionChanged.Add(SeqChangeInfo.OfArgs >> event.Trigger)
            { new IChangeNotifier<SeqChangeInfo<'t>> with member x.Changed = event.Publish }
        | _ -> 
            let event = Event<_>()
            { new IChangeNotifier<SeqChangeInfo<'t>> with member x.Changed = event.Publish }

type ISeqChangeNotifier<'t> = IChangeNotifier<SeqChangeInfo<'t>>

type IList<'t> with
    member x.AddRange vs =
        vs |> Seq.iter (x.Add)

    member x.GetSlice(st,en) =
        let st = defaultArg st 0
        let en = defaultArg en (x.Count - 1)
        let newList = List<'t>()
        for i = st to en do
            newList.Add(x.[st + i])
        newList

    member x.InsertRange index vs = 
        if index = x.Count then x.AddRange(vs)
        else vs |> Seq.iteri (fun i v -> x.Insert(index + i, v))
        
    member x.RemoveRange index count = 
        for i = 0 to count do
            x.RemoveAt(index)
        
    member x.SyncWithChange(change : SeqChangeInfo<'t>, altSource : unit -> #seq<'t>) =
        match change with
        | Added(index, items) -> x.InsertRange index items
        | Removed(index, items) -> x.RemoveRange index (items.Length)
        | Replaced(index, oldItems, newItems) -> 
            for i = 0 to oldItems.Length do x.[i + index] <- newItems.[i]
        | Moved(oldIndex, newIndex, items) ->
            x.RemoveRange oldIndex (items.Length)
            x.InsertRange newIndex items
        | Reset -> 
            x.Clear()
            x.AddRange(altSource())
        
let (|SeqChanged|_|) (context : IContextualChangeInfo) =
    match context with
    | :? SeqChangeInfo<'t> as x -> Some(x)
    | _ -> None

open System.Threading
type ExpectEntry() =
    let mutable thread : Thread = null
    member x.Expect 
        with get() = thread = Thread.CurrentThread
        and set b = thread <- if b then Thread.CurrentThread else null

    member x.ExpectExecute act = 
        x.Expect <- true
        act()
        x.Expect <- false

module Seq = 
    open System.Linq
    let getIter (s : seq<_>) = s.GetEnumerator()
    let ofType<'t> (s : IEnumerable) = s.OfType<'t>()
