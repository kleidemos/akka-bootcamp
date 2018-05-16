namespace ChartApp

open System.Collections.Generic
open System.Windows.Forms.DataVisualization.Charting
open Akka.Actor
open Akkling

[<AutoOpen>]
module Messages =
    type ChartMessage =
        | InitializeChart of initialSeries: Map<string, Series>
        | AddSeries of series: Series

/// Actors used to intialize chart data
module ChartingActor =
    let create (chart : Chart) =
        actorOf2 <| fun context ->
            let rec behaviour mapping = function
                | InitializeChart series ->
                    chart.Series.Clear()
                    series |> Map.iter (fun k v ->
                        v.Name <- k
                        chart.Series.Add v)
                    ignored()
                | AddSeries series when
                    not <| System.String.IsNullOrEmpty series.Name
                    && not <| (mapping |> Map.containsKey series.Name) ->
                    let mapping = mapping.Add (series.Name, series)
                    chart.Series.Add series
                    become (behaviour mapping)
                | _ -> ignored()
            behaviour Map.empty
        |> props