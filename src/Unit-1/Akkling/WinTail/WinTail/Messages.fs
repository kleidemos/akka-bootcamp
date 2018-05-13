namespace WinTail

[<AutoOpen>]
module Messages =

    type ErrorType =
        | NullInput
        | Validation

    type InputResult =
        | InputSuccess of Reason : string
        | InputError of Reason : string * Type : ErrorType

    let (|InputResult|_|) = tryUnbox<InputResult>

    type Command =
        | Continue
        | Exit
        | Start
        | Message of string

    //Messages to start and stop observing file content for any changes
    type TailCommand =
        | StartTail of filePath: string * reporterActor: Akkling.ActorRefs.IActorRef<string>  //File to observe, actor to display contents
        | StopTail of filePath: string

    type FileCommand =
        | FileWrite of fileName: string
        | FileError of fileName: string * reason: string
        | InitialRead of fileName: string * text: string