namespace WinTail

open System
open Akkling

/// <summary>
/// Actor responsible for reading FROM the console.
/// Also responsible for calling <see cref="ActorSystem.Terminate"/>.
/// </summary>
module ConsoleReaderActor =
    type Message = Command

    let [<Literal>] exitCommandLiteral = "exit"

    let (|OrdinalIgnoreCaseEquals|_|) pattern str =
        if String.Equals(str, pattern, StringComparison.OrdinalIgnoreCase)
        then Some ()
        else None

    let (|ExitCommand|_|) = function
        | OrdinalIgnoreCaseEquals exitCommandLiteral -> Some ()
        | _ -> None

    let printInstructions () =
        Console.WriteLine "Please provide the URI of a log file on disk.\n"

    let create (validator : IActorRef<string>) =
        actorOf2 <| fun context ->
            let rec behaviour (message : Message) =
                match message with
                    | Start -> printInstructions()
                    | _ -> ()
                match Console.ReadLine() with
                    | ExitCommand ->
                        context.System.Terminate() |> ignore
                    | p -> validator <! p
                ignored()
            behaviour
        |> props

/// <summary>
/// Actor responsible for serializing message writes to the console.
/// (write one message at a time, champ :)
/// </summary>
module ConsoleWriterActor =
    type Message = InputResult

    let create () =
        let rec behaviour (message : obj) : obj Effect=
            match message with
            | InputResult (InputError (reason, _)) ->
                reason
                |> Console.printfnColor
                    ConsoleColor.Red
                    "%s"
            | InputResult (InputSuccess reason) ->
                reason
                |> Console.printfnColor
                    ConsoleColor.Green
                    "%s"
            | p -> Console.printfnColor ConsoleColor.Yellow "%A" p
            ignored()
        actorOf behaviour
        |> props

[<Obsolete>]
module ValidationActor =
    let (|Valid|Invalid|) str =
        if (String.length str) % 2 = 0
        then Valid
        else Invalid

    let create writer =
        actorOf2 <| fun context ->
            let rec behaviour message =
                match message with
                | IsNullOrEmpty ->
                    writer <! InputError ("Not imput received.", NullInput)
                | Valid ->
                    writer <! InputSuccess "Thank you! Message was valid."
                | Invalid ->
                    writer <! InputError ("Invalid: input had odd number of characters.", Validation)
                context.Sender() <! Continue
                ignored()
            behaviour
        |> props

module FileValidationActor =
    let (|IsFileUrl|_|) path = Some path |> Option.filter System.IO.File.Exists

    let create (writer : IActorRef<_>) tailCoordinator =
        actorOf2 <| fun context ->
            let rec behaviour message =
                match message with
                | IsNullOrEmpty ->
                    writer <! InputError ("Input was blank. Please try again.\n", ErrorType.NullInput)
                    context.Sender() <! Continue
                | IsFileUrl _ ->
                    writer <! InputSuccess (sprintf "Starting processing for %s" message)
                    tailCoordinator <! StartTail (message, retype writer)
                | _ ->
                    writer <! InputError (sprintf "%s is not an existing URI on disk." message, ErrorType.Validation)
                    context.Sender() <! Continue
                ignored ()
            behaviour
        |> props

module TailActor =
    open System.IO

    let create filePath reporter =
        props <| fun context ->
            let observer = new FileObserver(context.Self, System.IO.Path.GetFullPath filePath)
            do observer.Start()

            let fileStream =
                new FileStream(
                    Path.GetFullPath filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite)
            let fileStreamReader = new StreamReader(fileStream, System.Text.Encoding.UTF8)
            let text = fileStreamReader.ReadToEnd()
            do context.Self <! InitialRead(filePath, text)

            let rec behaviour message : FileCommand Effect =
                match message with
                | FileWrite _ ->
                    let text = fileStreamReader.ReadToEnd()
                    if not <| String.IsNullOrEmpty text then
                        reporter <! text
                | FileError (_, reason) ->
                    reporter <! sprintf "Tail error: %s" reason
                | InitialRead (_, text) ->
                    reporter <! text
                ignored()
            become behaviour

module TailCoordinatorActor =
    let create () =
        props <| fun context ->
            let rec behaviour message =
                match message with
                | StartTail (filePath, reporter) ->
                    TailActor.create filePath reporter
                    |> spawn context "tailActor"
                    |> ignore
                    ignored()
                | _ ->
                    ignored()
            become behaviour