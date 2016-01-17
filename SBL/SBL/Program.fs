// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
module SBL.Program
open System.Collections.ObjectModel
open SBL.Utility
[<EntryPoint>]
let main argv = 
    let lst = ObservableCollection()
    lst.Add(1)
    
    lst.AddRange([1; 2; 3])
    let bindable = lst.AsBindable().SelectAsBindable().Where(fun x -> x % 2 = 0)
    lst.[0] <- 4
    let l2 = bindable.Value
    printfn "%A" lst
    0 // return an integer exit code
