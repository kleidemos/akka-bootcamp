namespace WinTail
open System

[<AutoOpen>]
module Utils =
    let (|IsNullOrEmpty|_|) str =
        if String.IsNullOrEmpty str
        then Some ()
        else None

module Console =
    let private withColor fn color format =
        format
        |> Printf.kprintf (fun str ->
            Console.ForegroundColor <- color
            fn str
            Console.ResetColor())

    let printfColor color format =
        withColor Console.Write color format

    let printfnColor color format =
        withColor Console.WriteLine color format