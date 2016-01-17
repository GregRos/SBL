module internal SBL.ListBindings
open SBL.Collections   
open System.Collections.Generic
open System.Collections.ObjectModel

type NthItemBindable<'T> internal
        (index : int, 
         inner : IList<'T>, 
         notifier : ISeqChangeNotifier<'T>, 
         parent : IBindable
         ) as x =
    inherit BindableBase<'T>()
    let onSeqChanged args = 
        let isInChangedRange start lst = start <= index && index < start + (lst |> List.length)
        let notify = 
            match args with
            | Replaced(changed, oldItems, _) -> isInChangedRange changed oldItems
            | Added(changed, items) -> index >= changed
            | Moved(oldIndex, newIndex, items) -> (index < oldIndex) = (index < newIndex)
            | Removed(changed, items) -> index < changed
            | Reset -> true
        if notify then x.NotifyChange(ContextualChangeInfo.Empty)
    let notifyToken = notifier.Changed.Subscribe(onSeqChanged)
    override x.CanRead = true
    override x.CanWrite = not inner.IsReadOnly
    override x.Parent = parent
    override x.Dispose() =
        notifyToken.Dispose()
        base.Dispose()
    override x.Value 
        with get() = inner.[index]
        and set v = inner.[index] <- v
                 
type ListItemBindable<'T>  
        (index : int, 
         inner : IList<'T>, 
         notifier : ISeqChangeNotifier<'T>, 
         parent : IBindable
         ) as x =
    inherit BindableBase<'T>()
    let mutable index = index
    let onSeqChanged args =
        let isInChangedRange start lst = start <= index && index < start + (lst |> List.length)
        let getShift start (lst :_ list) = start + lst.Length - index
        match args with
        | Replaced(changed, items, _) -> 
            if isInChangedRange changed items then
                x.NotifyChange(ContextualChangeInfo.Empty)

        | Added(changed, items) ->
            if index >= changed then
                index <- index + items.Length

        | Moved(oldIndex, newIndex, items) ->
            if isInChangedRange oldIndex items then //the index is part of the moved block
                index <- index - oldIndex + newIndex
            else 
                match index < oldIndex, index < newIndex with
                | true, false -> //the moved block used to be after the current index, but is now before
                    index <- index + items.Length
                | false, true -> //the moved block used to be before the current index, but is now after
                    index <- index - items.Length
                | _ -> () //the moved block hasn't shifted the index of the current block.  
                          
        | Removed(changed, items) ->
            if isInChangedRange changed items then //the item was removed
                x.Dispose() 
            elif index >= changed then //the removed items were before the current index
                index <- index - items.Length 

        | Reset -> x.Dispose() //the item was removed from the list and will need to be recreated
    let notifyToken = notifier.Changed.Subscribe(onSeqChanged)
    override x.CanRead = true
    override x.CanWrite = not inner.IsReadOnly
    override x.Parent = parent
    override x.Dispose() =
        notifyToken.Dispose()
        base.Dispose()
    override x.Value 
        with get() = inner.[index]
        and set v = inner.[index] <- v

type ProjectionBindable<'TIn, 'TOut> private 
        (inner : IBindable,
         convertOut : (int -> 'TIn -> 'TOut) option, 
         convertIn : (int -> 'TOut -> 'TIn) option) as x =
    inherit BindableBase<IList<'TOut>>()
    let outer = ObservableCollection()
    let expectInnerChange = ExpectEntry()
    let expectOuterChange = ExpectEntry()
    let innerList = inner.Value :?> IList<'TIn>
    //SelectBindable is implemented using synchornization code, not an IList implementation.
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
            x.NotifyChange(args)

    let onInnerChanged args = 
        if not expectInnerChange.Expect then
            if convertOut.IsNone then failwith "Cannot read"
            let convertOut = convertOut.Value
            let outerArgs = mapSeqChanged args outer convertOut
            expectOuterChange.ExpectExecute(fun() -> outer.SyncWithChange(outerArgs, fun() -> innerList |> Seq.mapi convertOut))
            x.NotifyChange(args)

    let notifier = SeqChangeInfo.GetNotifier outer
    let innerChangeToken = inner.Changed.Subscribe(onInnerChanged)
    let outerChangeToken = notifier.Changed.Subscribe(onOuterChanged)
    do onInnerChanged(Reset)

    override x.CanRead = convertOut.IsSome
    override x.CanWrite = innerList.IsReadOnly && convertIn.IsSome
    override x.Parent = inner
    override x.Value
        with get() = outer :> _
        and set v =
            expectInnerChange.ExpectExecute(fun () -> 
                outer.Clear()
                outer.AddRange(v)
                )
            onOuterChanged(Reset)

    override x.Dispose() =
        innerChangeToken.Dispose()
        outerChangeToken.Dispose()
        base.Dispose()

type ListBindable<'T> internal (inner : IList<'T>, notifier : ISeqChangeNotifier<'T>, parent : IBindable) as x=
    inherit BindableBase<IList<'T>>()
    let createBindable index _ = ListItemBindable(index, inner, notifier, x) :> IBindable<_>
    let bindingPoints = ProjectionList(inner, Some createBindable, None)
    let notifyToken = notifier.Changed.Subscribe(fun args -> x.NotifyChange(args))

    interface IListBindable<'T> with
        member x.Bindables = bindingPoints :> IList<_>

    override x.CanRead = true
    override x.CanWrite = not inner.IsReadOnly
    override x.Parent = parent
    override x.Value 
        with get() = inner
        and set v = 
            x.SetValueWithContext v (ContextualChangeInfo.Empty)
    override x.SetValueWithContext v info =
        match info with
        | SeqChanged(changed) -> inner.SyncWithChange(changed, fun () -> v)
        | _ -> inner.SyncWithChange(Reset, fun() -> v)
    override x.Dispose() =
        notifyToken.Dispose()
        base.Dispose()

type SelectBindable<'TIn, 'TOut> internal (inner : IListBindable<'TIn>, convert : IBindable<'TIn> -> IBindable<'TOut>) as x =
    inherit BindableBase<IList<'TOut>>()
    let bind i v = convert v
    let unbind i (b : IBindable<_>) = b.Value
    let projectionList = ProjectionList(ProjectionList(inner.Bindables, Some bind, None), )
    override x.CanRead = 

(*
type WhereBindable<'T> private (outer : WhereList<'T>, parent : IBindable) =
    //WhereBindable is implemented using a special IList implementation.
    inherit ListBindable<IBindable<'T>>(outer, SeqChangeInfo.GetNotifier outer, parent)
    new(inner : IBindable, filter : 'T -> bool) =
        let innerList = inner.Value :?> IList<IBindable<'T>>
        WhereBindable(WhereList(innerList, SeqChangeInfo.GetNotifier innerList, filter), inner)
        *)