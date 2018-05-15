namespace ChartApp

open System.Collections.Generic
open System.Windows.Forms.DataVisualization.Charting
open Akka.Actor
open Akkling

[<AutoOpen>]
module Messages =
    type InitializeChart =
        | InitializeChart of initialSeries: Map<string, Series>

/// Actors used to intialize chart data
module ChartingActor =
    let create (chart : Chart) =
        actorOf <| function
            | InitializeChart series ->
                chart.Series.Clear()
                series |> Map.iter (fun k v ->
                    v.Name <- k
                    chart.Series.Add v)
                ignored()
        |> props