namespace SBL.Collections
open System.Collections.Generic
open System
open SBL
type DisposingList<'T>(inner : IList<'T>) as x =
    let dispose (o : obj) = 
        match o with
        | :? IDisposable as disposable -> disposable.Dispose()
        | _ -> ()
    let ilist = x :> IList<'T>
    interface IList<'T> with
        member x.Add(item: 'T): unit = inner.Add(item)
        member x.Contains(item: 'T): bool = inner.Contains(item)
        member x.CopyTo(array: 'T [], arrayIndex: int): unit = inner.CopyTo(array, arrayIndex)
        member x.Count: int = inner.Count
        member x.GetEnumerator(): IEnumerator<'T> = inner.GetEnumerator()
        member x.GetEnumerator(): System.Collections.IEnumerator = inner.GetEnumerator() :> _
        member x.IndexOf(item: 'T): int = inner.IndexOf(item)
        member x.Insert(index: int, item: 'T): unit = inner.Insert(index, item)
        member x.IsReadOnly: bool = inner.IsReadOnly
        member x.Clear() : unit =
            let toDispose = inner |> Seq.ofType<IDisposable> |> Seq.toArray
            inner.Clear()
            toDispose |> Seq.iter (dispose)
        member x.Item
            with get (index: int): 'T = inner.[index]
            and set (index: int) (v: 'T): unit = 
                let oldValue = inner.[index]
                inner.[index] <- v
                dispose oldValue
        member x.Remove(item: 'T): bool = 
            let ix = ilist.IndexOf(item)
            if ix < 0 then false else 
                ilist.RemoveAt(ix)
                true
        member x.RemoveAt(index: int): unit = 
            let atIndex = inner.[index]
            inner.RemoveAt(index)
            dispose atIndex
