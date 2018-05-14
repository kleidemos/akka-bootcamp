// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open Akkling
open System
open WinTail

let printInstructions () =
    Console.Write """Write whatever you want into the console!
Some lines will appear as"""
    Console.printfColor ConsoleColor.DarkRed " red"
    Console.Write " and others will appear as"
    Console.printfColor ConsoleColor.Green " green! "
    Console.WriteLine()
    Console.WriteLine()
    Console.WriteLine "Type 'exit' to quit this application at any time.\n"

let myActorSystem =
    Configuration.defaultConfig()
    |> System.create "MyActorSystem"

open Akka.Actor

[<EntryPoint>]
let main argv =
    printInstructions()

    let writer =
        ConsoleWriterActor.create()
        |> spawn myActorSystem "consoleWriterActor"

    let tailCoordinator =
        { TailCoordinatorActor.create () with
            SupervisionStrategy = Strategy.OneForOne((function
                | :? ArithmeticException -> Directive.Resume
                | :? NotSupportedException -> Directive.Stop
                | _ -> Directive.Restart), 10, TimeSpan.FromSeconds 30.)
                |> Some
        }   |> spawn myActorSystem "tailCoordinatorActor"

    let fileValidator =
        FileValidationActor.create (retype writer)
        |> spawn myActorSystem "fileValidatorActor"

    let reader =
        ConsoleReaderActor.create ()
        |> spawn myActorSystem "consoleReaderActor"

    reader <! Start

    // blocks the main thread from exiting until the actor system is shut down
    myActorSystem.WhenTerminated.Wait()
    0 // return an integer exit code