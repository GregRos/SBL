namespace SBL
open SBL
open System.Collections.Generic
open System.Collections.ObjectModel
open SBL.Utility
[<AbstractClass>]
type BindableBase<'T>() = 
    let mutable eventChanged = Event<NotifyBindableChanged,IContextualChangeInfo>()
    let mutable eventDisposed = Event<NotifyBindableDisposed, System.EventArgs>()
    let handlerDictionary = Dictionary()
    let mutable _binding : IBinding<'T> = null
    interface IBindable with
        member x.IsDisposed = x.IsDisposed
        member x.IsValid = failwith "Not implemented"
        member x.Meta = failwith "Not implemented"
        member x.Value 
            with get() = x.Value :> obj
            and set o = x.Value <- o :?> 'T
        member x.CanRead = x.CanRead
        member x.CanWrite = x.CanWrite
        member x.Parent = x.Parent
        member x.NotifyChange info = x.NotifyChange info
        [<CLIEvent>]
        member x.Changed = eventChanged.Publish
        [<CLIEvent>]
        member x.Disposed = eventDisposed.Publish
        member x.Dispose() = x.Dispose()
            
    interface IBindable<'T> with
        member x.Value 
            with get() = x.Value
            and set v = x.Value <- v
        member x.SetValueWithContext v info = x.SetValueWithContext v info
        member x.Binding 
            with get() = _binding
            and set b = 
                _binding <- b
                match b with
                | null -> ()
                | b -> b.Initialize(x)
                
    member val IsDisposed = false with get, set
    abstract Value : 'T with get, set
    abstract SetValueWithContext : 'T -> IContextualChangeInfo -> unit
    default x.SetValueWithContext v info = x.Value <- v
    abstract CanRead : bool
    abstract CanWrite : bool
    abstract Parent : IBindable
    abstract NotifyChange : IContextualChangeInfo -> unit
    abstract Dispose : unit -> unit
    default x.NotifyChange info = eventChanged.Trigger(x, info)
    default x.Dispose() =
        if _binding <> null then _binding.Dispose()
        eventChanged <- Event<_,_>()
        eventDisposed <- Event<_,_>()


