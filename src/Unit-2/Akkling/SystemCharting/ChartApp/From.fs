namespace ChartApp

open Akkling
open System.Drawing
open System.Windows.Forms
open System.Windows.Forms.DataVisualization.Charting
open Akka.Util.Internal

module Form =
    let sysChart = new Chart(Name = "sysChart", Text = "sysChart", Dock = DockStyle.Fill, Location = Point(0, 0), Size = Size(684, 446), TabIndex = 0)
    let form = new Form(Name = "Main", Visible = true, Text = "System Metrics", AutoScaleDimensions = SizeF(6.F, 13.F), AutoScaleMode = AutoScaleMode.Font, ClientSize = Size(684, 446))
    let chartArea1 = new ChartArea(Name = "ChartArea1")
    let legend1 = new Legend(Name = "Legend1")

    let btnPauseResume = new Button(Name = "btnPauseResume", Text = "PAUSE ||", Location = Point(570, 200), Size = Size(110, 40), TabIndex = 4, UseVisualStyleBackColor = true)
    // create the buttons
    let btnCpu = new Button(Name = "btnCpu", Text = "CPU (ON)", Location = Point(560, 275), Size = Size(110, 40), TabIndex = 1, UseVisualStyleBackColor = true)
    let btnMemory = new Button(Name = "btnMemory", Text = "MEMORY (OFF)", Location = Point(560, 320), Size = Size(110, 40), TabIndex = 2, UseVisualStyleBackColor = true)
    let btnDisk = new Button(Name = "btnDisk", Text = "DISK (OFF)", Location = Point(560, 365), Size = Size(110, 40), TabIndex = 3, UseVisualStyleBackColor = true)

    sysChart.BeginInit ()
    form.SuspendLayout ()
    sysChart.ChartAreas.Add chartArea1
    sysChart.Legends.Add legend1

    form.Controls.Add btnPauseResume

    // and add them to the form
    form.Controls.Add btnCpu
    form.Controls.Add btnMemory
    form.Controls.Add btnDisk

    form.Controls.Add sysChart
    sysChart.EndInit ()
    form.ResumeLayout false

    let load (myActorSystem : Akka.Actor.ActorSystem) =
        let chartActor =
            ChartingActor.create sysChart btnPauseResume
            |> spawn myActorSystem "charting"
        let coordinatorActor =
            chartActor
            |> PerformanceCounterCoordinatorActor.create
            |> spawn myActorSystem "counters"
        let toggleActors =
            [   CounterType.Cpu, btnCpu, "cpuCounter"
                CounterType.Memory, btnMemory, "memoryCounter"
                CounterType.Disk, btnDisk, "diskCounter" ]
            |> List.map (fun (counterType, btn, name) ->
                counterType,
                    { ButtonToggleActor.create coordinatorActor btn counterType false with
                        Dispatcher = Some "akka.actor.synchronized-dispatcher" }
                    |> spawn myActorSystem name)
            |> Map.ofList

        toggleActors.[CounterType.Cpu] <! Toggle

        [   CounterType.Cpu, btnCpu, "cpuCounter"
            CounterType.Memory, btnMemory, "memoryCounter"
            CounterType.Disk, btnDisk, "diskCounter" ]
        |> List.iter (fun (counterType, btn, _) ->
            btn.Click.Add (fun _ -> toggleActors.[counterType] <! Toggle))

        btnPauseResume.Click.Add (fun _ ->
            chartActor <! TogglePause)

        form