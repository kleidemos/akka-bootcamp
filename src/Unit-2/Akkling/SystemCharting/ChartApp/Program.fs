module Program

open System
open System.Windows.Forms
open Akkling
open Akka.Actor
open Akka.Configuration.Hocon
open System.Configuration
open ChartApp

let chartActors =
    Configuration.load ()
    |> System.create "ChartActors"

Application.EnableVisualStyles ()
Application.SetCompatibleTextRenderingDefault false

[<STAThread>]
do Application.Run (Form.load chartActors)