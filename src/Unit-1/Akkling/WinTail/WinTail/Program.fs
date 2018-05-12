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

/// <summary>
/// Actor responsible for serializing message writes to the console.
/// (write one message at a time, champ :)
/// </summary>
module ConsoleWriterActor =

    type Message = Message of string

    let create () =
        let rec behaviour (Message message) =
            // make sure we got a message
            match String.IsNullOrEmpty message with
            | true ->
                Console.printfnColor
                    ConsoleColor.DarkYellow
                    "Please provide an input.\n"
            // if message has even # characters, display in red; else, green
            | false when message.Length % 2 = 0 ->
                Console.printfnColor
                    ConsoleColor.Red
                    "Your string had an even # of characters.\n"
            | false ->
                Console.printfnColor
                    ConsoleColor.Green
                    "Your string had an odd # of characters.\n"
            become behaviour
        actorOf behaviour
        |> props

/// <summary>
/// Actor responsible for reading FROM the console.
/// Also responsible for calling <see cref="ActorSystem.Terminate"/>.
/// </summary>
module ConsoleReaderActor =
    let [<Literal>] exitCommand = "exit"

    type Message = Message of string

    let create (writer : IActorRef<ConsoleWriterActor.Message>) =
        actorOf2 <| fun context ->
            let rec behaviour (Message message) =
                let read = Console.ReadLine()
                if not (String.IsNullOrEmpty read)
                    && String.Equals(read, exitCommand, StringComparison.OrdinalIgnoreCase)
                then
                    // shut down the system (acquire handle to system via
                    // this actors context)
                    Stop :> Effect<_>
                else
                    writer <! ConsoleWriterActor.Message message
                    context.Self <! Message "continue"
                    become behaviour
            behaviour
        |> props

let printInstructions () =
    Console.WriteLine """Write whatever you want into the console!
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

    reader <! ConsoleReaderActor.Message "start"

    // blocks the main thread from exiting until the actor system is shut down
    myActorSystem.WhenTerminated.Wait()
    0 // return an integer exit code