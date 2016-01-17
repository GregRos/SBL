[<AutoOpen>]
module internal SBL.SimpleBindings
open SBL

type VarBindable<'T>(value : 'T) =
    inherit BindableBase<'T>()
    let mutable value = value
    override x.CanRead = true
    override x.CanWrite = true
    override x.Parent = null
    override x.Value
        with get() = value
        and set v = value <- v
    override x.SetValueWithContext v info = 
        value <- v
    
type ConstBindable<'T>(value : 'T) =
    inherit VarBindable<'T>(value)
    override x.CanWrite = false
    override x.Value
        with get() = base.Value
        and set v = failwith "Bindable read-only"

type ConvertBindable<'TIn, 'TOut> internal (inner : IBindable<'TIn>, convertOut : ('TIn -> 'TOut) option , convertIn : ('TOut -> 'TIn) option) as x =
    inherit BindableBase<'TOut>()
    let notifyToken = inner.Changed.Subscribe(fun arg -> x.NotifyChange(ContextualChangeInfo.Empty))
    override x.CanRead = convertOut.IsSome && inner.CanRead
    override x.CanWrite = convertIn.IsSome && inner.CanWrite
    override x.Parent = inner :> IBindable
    override x.Value
        with get() = 
            if not x.CanRead then failwith "Does not support read"
            convertOut.Value(inner.Value)
        and set v = 
            if not x.CanWrite then failwith "Does not support write"
            inner.Value <- convertIn.Value v
    override x.Dispose() =
        notifyToken.Dispose()
        base.Dispose()
        
type LinkingBindable<'T> internal (inner : IBindable<'T>) as x=
    inherit BindableBase<'T>()
    let onInnerChanged arg = x.NotifyChange(arg)
    let notifyToken = inner.Changed.Subscribe(onInnerChanged)
    override x.Value with get() = inner.Value and set v = inner.Value <- v
    override x.CanRead = inner.CanRead
    override x.CanWrite = inner.CanWrite
    override x.Parent = inner :> _
    override x.Dispose() =
        notifyToken.Dispose()
        base.Dispose()