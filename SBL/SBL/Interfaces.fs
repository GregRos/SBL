namespace SBL
open System
type IContextualChangeInfo = interface end

type IMetaProvider = interface end

type ValidationResult = class end

type ContextualChangeInfo internal () =
    interface IContextualChangeInfo
    static member Empty = ContextualChangeInfo()

type NotifyBindableChanged = delegate of obj * IContextualChangeInfo -> unit
type NotifyBindableDisposed = delegate of obj * EventArgs -> unit

open System.Collections.Generic



[<FlagsAttribute>]
type BindingMode =
| Default = 0
| Disabled = 0x1
| IntoTarget = 0x2
| FromTarget = 0x4
| TwoWay = 6
| TwoWayPrioritizeSource = 14

and [<AllowNullLiteral>] IBindable =
    inherit IDisposable
    abstract IsDisposed : bool
    abstract NotifyChange : IContextualChangeInfo -> unit
    abstract Value : obj with get, set
    abstract CanRead : bool
    abstract CanWrite : bool
    abstract Meta : IMetaProvider
    abstract Parent : IBindable
    abstract IsValid : IBindable<ValidationResult>
    [<CLIEventAttribute>]
    abstract Changed : IEvent<NotifyBindableChanged, IContextualChangeInfo>
    [<CLIEventAttribute>]
    abstract Disposed : IEvent<NotifyBindableDisposed, EventArgs>

and [<AllowNullLiteral>] IBindable<'T> =
    inherit IBindable
    abstract Value : 'T with get, set
    abstract SetValueWithContext : 'T -> IContextualChangeInfo -> unit
    abstract Binding : IBinding<'T> with get, set

and INotifier<'T> =
    abstract Bindable : IBindable<'T>
    abstract Add : INotificationMode -> unit
    abstract Modes : seq<INotificationMode>
    abstract Remove : INotificationMode -> unit

and INotificationModeToken = 
    inherit IDisposable
    abstract Mode : INotificationMode

and INotificationMode =
    abstract Subscribe : IBindable -> INotificationModeToken

and [<AllowNullLiteral>] IBinding =
    inherit IDisposable
    abstract Source : IBindable
    abstract Target : IBindable
    abstract IsDisposed : bool
    abstract IsInitialized : bool
    abstract Mode : BindingMode

and [<AllowNullLiteral>] IBinding<'T> =
    inherit IBinding
    abstract Source : IBindable<'T>
    abstract Target : IBindable<'T>
    abstract Initialize : IBindable<'T> -> unit

type IListBindable<'T> =
    inherit IBindable<IList<'T>>
    abstract Bindables : IList<IBindable<'T>>
