namespace SBL
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System
open System.Collections.Generic
open System.Collections
open System.Collections.Specialized
open System.Collections.ObjectModel
open SBL.Utility
open SBL.SimpleBindings
open SBL.ListBindings
module Bindable = 
    let Var initial = VarBindable(initial) :> IBindable<_>
    let Const value = ConstBindable(value) :> IBindable<_>

[<ExtensionAttribute>]
type SpecExt private () = 

    [<ExtensionAttribute>]
    static member Link(this : IBindable<'T>) = 
        LinkingBindable(this) :> IBindable<'T>

    [<ExtensionAttribute>]
    static member Select(this : IBindable<IList<'T>>, ?convertOut, ?convertIn) = 
        SelectBindable(this, convertOut, convertIn) :> IBindable<_>
        
    [<ExtensionAttribute>]
    static member Where(this : IBindable<IList<IBindable<'T>>>, filter : Func<'T, bool>) =
        WhereBindable(this, filter.Invoke) :> IBindable<_>

    [<ExtensionAttribute>]
    static member SelectAsBindable(this : IBindable<IList<'T>>) = 
        SpecExt.Select(this, fun x -> Bindable.Const(x) :> IBindable<'T>) :> IBindable<_>

    [<ExtensionAttribute>]
    static member Convert(x : IBindable<'T>, ?convertOut, ?convertIn) =
        ConvertBindable(x, convertOut, convertIn) :> IBindable<_>

    [<ExtensionAttribute>]
    static member AsBindable(this : IList<'T>) =
        let notifier = this |> SeqChangeInfo.GetNotifier
        ListBindable(this, notifier, null) :> IBindable<_>

    [<ExtensionAttribute>]
    static member SelectLink(this : IBindable<IList<IBindable<'T>>>) : IBindable<IList<IBindable<'T>>>=
        SpecExt.Select(this, SpecExt.Link)








    

