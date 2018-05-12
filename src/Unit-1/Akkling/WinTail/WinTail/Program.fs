// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open Akkling
open System

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

type ErrorType =
    | NullInput
    | Validation

type InputResult =
    | InputSuccess of Reason : string
    | InputError of Reason : string * Type : ErrorType

/// <summary>
/// Actor responsible for serializing message writes to the console.
/// (write one message at a time, champ :)
/// </summary>
module ConsoleWriterActor =
    type Message = InputResult

    let create () =
        let rec behaviour message =
            match message with
            | InputError (reason, _) ->
                reason
                |> Console.printfnColor
                    ConsoleColor.Red
                    "%s"
            | InputSuccess reason ->
                reason
                |> Console.printfnColor
                    ConsoleColor.Green
                    "%s"
            become behaviour
        actorOf behaviour
        |> props

/// <summary>
/// Actor responsible for reading FROM the console.
/// Also responsible for calling <see cref="ActorSystem.Terminate"/>.
/// </summary>
module ConsoleReaderActor =
    type Message =
        | Start
        | Continue
        | InputResult of InputResult

    let [<Literal>] exitCommandLiteral = "exit"

    let (|IsNullOrEmpty|_|) str =
        if String.IsNullOrEmpty str
        then Some ()
        else None

    let (|OrdinalIgnoreCaseEquals|_|) pattern str =
        if String.Equals(str, pattern, StringComparison.OrdinalIgnoreCase)
        then Some ()
        else None

    let (|ExitCommand|_|) = function
        | OrdinalIgnoreCaseEquals exitCommandLiteral -> Some ()
        | _ -> None

    let printInstructions () =
        [   "Write whatever you want into the console!"
            "Some entries will pass validation, and some won't...\n\n"
            "Type 'exit' to quit this application at any time.\n" ]
        |> List.iter Console.WriteLine

    let (|Valid|Invalid|) str =
        if (String.length str) % 2 = 0
        then Valid
        else Invalid

    let create (writer : IActorRef<ConsoleWriterActor.Message>) =
        actorOf2 <| fun context ->
            let rec behaviour (message : Message) =
                match message with
                    | Start -> printInstructions()
                    | InputResult error -> writer <! error
                    | _ -> ()
                match Console.ReadLine() with
                | IsNullOrEmpty ->
                    context.Self <! InputResult (InputError ("Not imput received.", NullInput))
                | ExitCommand ->
                    context.System.Terminate() |> ignore
                | Valid ->
                    writer <! InputSuccess "Thank you! Message was valid."
                    context.Self <! Continue
                | Invalid ->
                    context.Self <! InputResult (InputError ("Invalid: input had odd number of characters.", Validation))
                become behaviour
            behaviour
        |> props

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

[<EntryPoint>]
let main argv =
    printInstructions()

    let writer =
        ConsoleWriterActor.create ()
        |> spawnAnonymous myActorSystem
    let reader =
        ConsoleReaderActor.create writer
        |> spawnAnonymous myActorSystem

    reader <! ConsoleReaderActor.Start

    // blocks the main thread from exiting until the actor system is shut down
    myActorSystem.WhenTerminated.Wait()
    0 // return an integer exit code