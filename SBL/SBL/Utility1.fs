[<AutoOpen>]
module internal SBL.Utility2

module Option =
    let orValue v (opt : _ option) =
        match opt with
        | Some u -> u
        | None -> v

    let orOption alt opt = 
        match opt with
        | Some u -> Some u
        | None -> alt

module Event = 
    open System.Collections.Concurrent
    let dict = ConcurrentDictionary<obj * obj,ConcurrentStack<_>>()
    let unsubLock = obj()

    let subscribeWithKey (event : IEvent<_,_>) key (f : _ -> unit) =
        let r = event.Subscribe f
        let stack = dict.GetOrAdd((event :> obj,key), ConcurrentStack())
        stack.Push(r)

    let unsubscribeWithKey (event : IEvent<_,_>) key (f : _ -> unit) =
        let success, handlers = dict.TryRemove((event :> obj, key))
        if not success then false
        else
            handlers |> Seq.iter (fun disp -> disp.Dispose())
            true
