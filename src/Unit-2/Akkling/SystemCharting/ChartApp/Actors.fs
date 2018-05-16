namespace ChartApp

open System.Collections.Generic
open System.Windows.Forms.DataVisualization.Charting
open Akka.Actor
open Akkling

[<AutoOpen>]
module Messages =
    type ChartMessage =
        | InitializeChart of initialSeries : Map<string, Series>
        | AddSeries of series : Series
        | RemoveSeries of seriesName : string
        | Metric of series : string * counterValue : float
        | TogglePause

    let (|ChartMessage|_|) = tryUnbox<ChartMessage>

    type CounterType =
        | Cpu = 1
        | Memory = 2
        | Disk = 3

    type CounterMessage =
        | GatherMetrics
        | SubscribeCounter of subscriber : IActorRef
        | UnsubscribeCounter of subscriber : IActorRef

    let (|CounterMessage|_|) = tryUnbox<CounterMessage>

    type CoordinationMessage =
        | Watch of counter : CounterType
        | Unwatch of counter : CounterType

    type ButtonMessage = Toggle

/// Actors used to intialize chart data
module ChartingActor =
    let create (chart : Chart) (pauseButton : System.Windows.Forms.Button) =
        props <| fun context ->
            let maxPoints = 250

            let setChartBoundaries mapping numberOfPoints =
                let allPoints =
                    mapping
                    |> Map.toSeq
                    |> Seq.collect (fun (_, series : Series) -> series.Points)
                    |> HashSet<DataPoint>
                if allPoints.Count > 2 then
                    let yValues = allPoints |> Seq.collect (fun p -> p.YValues) |> Seq.toList
                    let area = chart.ChartAreas.[0]
                    area.AxisX.Maximum <- float numberOfPoints
                    area.AxisX.Minimum <- float (numberOfPoints - maxPoints)
                    area.AxisY.Maximum <- yValues |> List.fold max 1. |> System.Math.Ceiling
                    area.AxisY.Minimum <- yValues |> List.fold min 0. |> System.Math.Floor

            let (|SeriesName|_|) = function
                | InitializeChart _ | TogglePause -> None
                | AddSeries series -> Some series.Name
                | RemoveSeries seriesName -> Some seriesName
                | Metric (seriesName, _) -> Some seriesName

            let setPauseButtonText paused =
                pauseButton.Text <-
                    if paused
                    then "RESUME ->"
                    else "PAUSE ||"

            let rec behaviour mapping numberOfPoints = function
                | InitializeChart series ->
                    chart.Series.Clear()
                    chart.ChartAreas.[0].AxisX.IntervalType <- DateTimeIntervalType.Number
                    chart.ChartAreas.[0].AxisY.IntervalType <- DateTimeIntervalType.Number
                    series |> Map.iter (fun k v ->
                        v.Name <- k
                        chart.Series.Add v)
                    ignored()
                | SeriesName name
                    when System.String.IsNullOrEmpty name ->
                        unhandled()
                | AddSeries series when
                    not <| (mapping |> Map.containsKey series.Name) ->
                        let mapping = mapping.Add (series.Name, series)
                        chart.Series.Add series
                        setChartAndBecomeBehaviour mapping numberOfPoints
                | RemoveSeries seriesName when
                    mapping.ContainsKey seriesName ->
                        chart.Series.Remove mapping.[seriesName] |> ignore
                        let mapping = mapping.Remove seriesName
                        setChartAndBecomeBehaviour mapping numberOfPoints
                | Metric (seriesName, counterValue) when
                    mapping.ContainsKey seriesName ->
                        let numberOfPoints = numberOfPoints + 1
                        let series = mapping.[seriesName]
                        series.Points.AddXY (numberOfPoints, counterValue) |> ignore
                        while (series.Points.Count > maxPoints) do series.Points.RemoveAt 0
                        setChartAndBecomeBehaviour mapping numberOfPoints
                | TogglePause ->
                    setPauseButtonText true
                    paused mapping numberOfPoints |> become
                | _ -> unhandled()
            and setChartAndBecomeBehaviour mapping numberOfPoints =
                setChartBoundaries mapping numberOfPoints
                behaviour mapping numberOfPoints |> become
            and paused mapping numberOfPoints = function
                | TogglePause ->
                    setPauseButtonText false
                    behaviour mapping numberOfPoints |> become
                | SeriesName name when
                    System.String.IsNullOrEmpty name ->
                        unhandled()
                | Metric (seriesName, _) when
                    mapping.ContainsKey seriesName ->
                        let numberOfPoints = numberOfPoints + 1
                        let series = mapping.[seriesName]
                        series.Points.AddXY (numberOfPoints, 0.) |> ignore
                        while (series.Points.Count > maxPoints) do series.Points.RemoveAt 0
                        setChartBoundaries mapping numberOfPoints
                        paused mapping numberOfPoints |> become
                | _ -> unhandled()
            behaviour Map.empty 0
            |> become

module PerformanceCounterActor =
    open System.Diagnostics

    let create seriesName perfCounterGenerator =
        props <| fun context ->
            let counter : PerformanceCounter = perfCounterGenerator()
            let cancelled =
                context.ScheduleRepeatedly
                    (System.TimeSpan.FromMilliseconds 250.)
                    (System.TimeSpan.FromMilliseconds 250.)
                    context.Self
                    GatherMetrics

            let rec behaviour subscriptions = function
                | LifecycleEvent PostStop ->
                    cancelled.Cancel()
                    counter.Dispose()
                    ignored()
                | CounterMessage GatherMetrics ->
                    subscriptions |> Seq.iter (
                        let msg = Metric(seriesName, counter.NextValue() |> float)
                        fun p -> typed p <! msg)
                    ignored()
                | CounterMessage (SubscribeCounter sub) ->
                    let subscriptionsWithoutSubscriber =
                        subscriptions |> List.filter (fun actor -> actor <> sub)
                    sub::subscriptionsWithoutSubscriber
                    |> behaviour
                    |> become
                | CounterMessage (UnsubscribeCounter sub) ->
                    subscriptions
                    |> List.filter (fun actor -> actor <> sub)
                    |> behaviour
                    |> become
                | _ -> unhandled()

            behaviour [] |> become

module PerformanceCounterCoordinatorActor =
    open System.Diagnostics
    open System.Drawing

    let create (chartingActor : IActorRef<ChartMessage>) =
        props <| fun context ->
            let counterGenerators =
                [   CounterType.Cpu, fun _ ->
                        new PerformanceCounter("Processor", "% Processor Time", "_Total", true)
                    CounterType.Memory, fun _ ->
                        new PerformanceCounter("Memory", "% Committed Bytes In Use", true)
                    CounterType.Disk, fun _ ->
                        new PerformanceCounter("LogicalDisk", "% Disk Time", "_Total", true) ]
                |> Map.ofList
            let counterSeries =
                [   CounterType.Cpu, SeriesChartType.SplineArea, Color.DarkGreen
                    CounterType.Memory, SeriesChartType.FastLine, Color.MediumBlue
                    CounterType.Disk, SeriesChartType.SplineArea, Color.DarkRed ]
                |> List.map (fun (counterType, chartType, color) ->
                    counterType, fun _ -> new Series(string counterType, ChartType = chartType, Color = color))
                |> Map.ofList

            let rec behaviour counterActors = function
                | Watch counter when counterActors |> Map.containsKey counter |> not ->
                    let counterName = string counter
                    let actor =
                        PerformanceCounterActor.create counterName counterGenerators.[counter]
                        |> spawn context (sprintf "counterActor-%s" counterName)
                    let counterActors = counterActors.Add (counter, actor)
                    chartingActor <! AddSeries (counterSeries.[counter] ())
                    actor <! SubscribeCounter (untyped chartingActor)
                    behaviour counterActors |> become
                | Watch counter ->
                    chartingActor <! AddSeries (counterSeries.[counter] ())
                    counterActors.[counter] <! SubscribeCounter (untyped chartingActor)
                    ignored()
                | Unwatch counter when counterActors |> Map.containsKey counter ->
                    chartingActor <! RemoveSeries ((counterSeries.[counter] ()).Name)
                    counterActors.[counter] <! UnsubscribeCounter (untyped chartingActor)
                    ignored()
                | _ -> unhandled()
            become (behaviour Map.empty)

module ButtonToggleActor =
    let create coordinatorActor (button : System.Windows.Forms.Button) counterType isToggled  =
        props <| fun context ->
            let flipToggle isOn =
                let isToggledOn = not isOn
                button.Text <-
                    if isToggledOn then "ON" else "OFF"
                    |> sprintf "%s (%s)" (counterType.ToString().ToUpperInvariant())
                isToggledOn
            let rec behaviour isToggledOn = function
                | Toggle when isToggledOn ->
                    coordinatorActor <! Unwatch counterType
                    behaviour (flipToggle isToggledOn) |> become
                | Toggle ->
                    coordinatorActor <! Watch counterType
                    behaviour (flipToggle isToggledOn) |> become
                | _ -> unhandled()

            behaviour isToggled
            |> become