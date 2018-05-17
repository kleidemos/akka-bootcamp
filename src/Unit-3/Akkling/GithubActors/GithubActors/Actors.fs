namespace GithubActors

open System
open System.Windows.Forms
open System.Drawing
open Akkling
open Akka.Actor

[<AutoOpen>]
module Actors =

    // make a pipe-friendly version of Akka.NET PipeTo for handling async computations
    let pipeToWithSender recipient sender asyncComp = pipeTo sender recipient asyncComp

    // Helper functions to check the type of query received
    let isWorkerMessage (someType: obj) = someType.GetType().IsSubclassOf(typeof<GithubActorMessage>)

    let isQueryStarrers (someType: obj) =
        if isWorkerMessage someType then
            match someType :?> GithubActorMessage with
            | QueryStarrers _ -> true
            | _ -> false
        else
            false

    let isQueryStarrer (someType: obj) =
        if isWorkerMessage someType then
            match someType :?> GithubActorMessage with
            | QueryStarrer _ -> true
            | _ -> false
        else
            false

module GithubAuthenticationActor =
    let create (statusLabel : Label) (githubAuthForm : Form) (launcherForm : Form) =
        let cannotAuthenticate reason =
            statusLabel.ForeColor <- Color.Red
            statusLabel.Text <- reason
        let showAuthenticatingStatus () =
            statusLabel.Visible <- true
            statusLabel.ForeColor <- Color.Orange
            statusLabel.Text <- "Authenticating..."
        props <| fun context ->
            let rec unauthenticated = function
                | Authenticate token ->
                    showAuthenticatingStatus ()
                    let client = GithubClientFactory.getUnauthenticatedClient()
                    client.Credentials <- Octokit.Credentials token

                    fun (task : System.Threading.Tasks.Task<_>) ->
                        match task.IsFaulted, task.IsCanceled with
                        | true, _ -> AuthenticationFailed
                        | false, true -> AuthenticationCancelled
                        | false, false ->
                            GithubClientFactory.setOauthToken token
                            AuthenticationSuccess
                    |> client.User.Current().ContinueWith
                    |> Async.AwaitTask
                    |!> context.Self

                    become authenticating
                | _ -> unhandled()
            and authenticating = function
                | AuthenticationFailed ->
                    cannotAuthenticate "Authentication failed."
                    become unauthenticated
                | AuthenticationCancelled ->
                    cannotAuthenticate "Authenticatation timed out"
                    become unauthenticated
                | AuthenticationSuccess ->
                    githubAuthForm.Hide ()
                    launcherForm.Show()
                    ignored()
                | _ -> unhandled()
            become unauthenticated

module MainFormActor =
    let create (isValidLabel : Label) createRepoResultsForm =
        props <| fun context ->
            let updateLabel isValid message =
                isValidLabel.Text <- message
                isValidLabel.ForeColor <- if isValid then Color.Green else Color.Red
                context.UnstashAll()

            let rec ready = function
                | ProcessRepo uri ->
                    select context "akka://GithubActors/user/validator" <! ValidateRepo uri
                    isValidLabel.Visible <- true
                    isValidLabel.Text <- sprintf "Validating %s..." uri
                    isValidLabel.ForeColor <- Color.Orange
                    become busy
                | LaunchRepoResultsWindow (repoKey, coordinator) ->
                    let repoResultsForm : Form = createRepoResultsForm repoKey coordinator
                    repoResultsForm.Show()
                    ignored()
                | _ -> unhandled()
            and busy = function
                | ValidRepo _ ->
                    updateLabel true "Valid!"
                    become ready
                | InvalidRepo (_, reason) ->
                    updateLabel false reason
                    become ready
                | UnableToAcceptJob job ->
                    sprintf "%s/%s is a valid repo, but the system cannot accept additional jobs" job.Owner job.Repo
                    |> updateLabel false
                    become ready
                | AbleToAcceptJob job ->
                    sprintf "%s/%s is a valid repo - starting job" job.Owner job.Repo
                    |> updateLabel true
                    become ready
                | LaunchRepoResultsWindow _ ->
                    context.Stash()
                    ignored()
                | _ -> unhandled()

            become ready

module GithubValidatorActor =
    let create getGithubClient =
        props <| fun context ->
            let splitIntoOwnerAndRepo repoUri =
                let results = Uri(repoUri, UriKind.Absolute).PathAndQuery.TrimEnd('/').Split('/') |> Array.rev
                results.[1], results.[0] // User, Repo

            let behaviour = function
                // outright invalid URLs
                | ValidateRepo uri when uri |> String.IsNullOrEmpty || not (Uri.IsWellFormedUriString(uri, UriKind.Absolute)) ->
                    context.Sender() <! InvalidRepo (uri, "Not a valid absolute URI")
                    ignored()
                // Repo that at least have a alid absolute URL
                | ValidateRepo uri ->
                    let continuation (task : System.Threading.Tasks.Task<_>) =
                        match task.IsCanceled, task.IsFaulted with
                        | true, _ ->
                            InvalidRepo (uri, "Repo lookup timed out")
                        | false, true ->
                            InvalidRepo (uri, "Not a valid absolute URI")
                        | false, false ->
                            ValidRepo task.Result
                    let user, repo = splitIntoOwnerAndRepo uri
                    let githubClient : Octokit.GitHubClient = getGithubClient()
                    githubClient.Repository.Get(user, repo).ContinueWith continuation
                    |> Async.AwaitTask
                    |> pipeToWithSender context.Self (context.Sender() |> untyped) // send the message back to ourselves but pass the real sender through
                    ignored()
                | InvalidRepo _ as invRepo ->
                    context.Sender() <<! invRepo
                    ignored()
                | ValidRepo repo ->
                    select context "akka://GithubActors/user/commander" <! CanAcceptJob { Owner = repo.Owner.Location; Repo = repo.Name }
                    ignored()
                | (UnableToAcceptJob _ as msg)
                | (AbleToAcceptJob _ as msg) ->
                    select context "akka://GithubActors/user/mainform" <! msg
                    ignored()
                | _ -> unhandled()

            behaviour |> become

module GithubWorkerActor =
    let create () =
        props <| fun context ->
            let githubClient = lazy (GithubClientFactory.getClient ())

            let rec behaviour = function
                | RetryableQuery ({ Query = GithubActorMessage (QueryStarrer login) } as query) ->
                    try
                        let starredRepos =
                            githubClient.Value.Activity.Starring.GetAllForUser login
                            |> Async.AwaitTask
                            |> Async.RunSynchronously
                        context.Sender() <! StarredReposForUser (login, starredRepos)
                    with
                    | ex -> context.Sender() <! nextTry query // operation failed - let the parent know
                    ignored()
                | RetryableQuery ({ Query = GithubActorMessage (QueryStarrers repoKey) } as query) ->
                    try
                        let users =
                            githubClient.Value.Activity.Starring.GetAllStargazers (repoKey.Owner, repoKey.Repo)
                            |> Async.AwaitTask
                            |> Async.RunSynchronously
                            |> Array.ofSeq

                        context.Sender() <! UsersToQuery users
                    with
                    | ex -> context.Sender() <! nextTry query // operation failed - let the parent know
                    ignored()
                | _ -> unhandled()

            behaviour |> become

module GithubCoordinatorActor =
    let create () =
        props <| fun context ->
            let startWorking repoKey (scheduler: IScheduler) =
                {
                    ReceivedInitialUsers = false
                    CurrentRepo = repoKey
                    Subscribers = System.Collections.Generic.HashSet<IActorRef> ()
                    SimilarRepos = System.Collections.Generic.Dictionary<string, SimilarRepo> ()
                    GithubProgressStats = getDefaultStats ()
                    PublishTimer = new Cancelable (scheduler)
                }

            // pre-start
            let githubWorker =
                GithubWorkerActor.create()
                |> spawn context "worker"

            let rec waiting = function
                | CanAcceptJob repoKey ->
                    context.Sender() <! AbleToAcceptJob repoKey
                    ignored()
                | BeginJob repoKey ->
                    githubWorker <! RetryableQuery { Query = QueryStarrers repoKey; AllowableTries = 4; CurrentAttempt = 0 }
                    let newSettings = startWorking repoKey context.System.Scheduler
                    working newSettings |> become
                | _ -> ignored()
            and working (settings: WorkerSettings) = function
                // received a downloaded user back from the github worker
                | StarredReposForUser (login, repos) ->
                    repos
                    |> Seq.iter (fun repo ->
                        if not <| settings.SimilarRepos.ContainsKey repo.HtmlUrl then
                            settings.SimilarRepos.[repo.HtmlUrl] <- { SimilarRepo.Repo = repo; SharedStarrers = 1 }
                        else
                            settings.SimilarRepos.[repo.HtmlUrl] <- increaseSharedStarrers settings.SimilarRepos.[repo.HtmlUrl]
                    )
                    working {settings with GithubProgressStats = userQueriesFinished settings.GithubProgressStats 1 }
                    |> become
                | PublishUpdate ->
                    // Check to see if the job has fully completed
                    match settings.ReceivedInitialUsers && settings.GithubProgressStats.IsFinished with
                    | true ->
                        let finishStats = finish settings.GithubProgressStats

                        // All repos minus forks of the current one
                        let sortedSimilarRepos =
                            settings.SimilarRepos.Values
                            |> Seq.filter (fun repo -> repo.Repo.Name <> settings.CurrentRepo.Repo)
                            |> Seq.sortBy (fun repo -> -repo.SharedStarrers)

                        // Update progress (both repos and users)
                        settings.Subscribers
                        |> Seq.iter (fun subscriber ->
                            typed subscriber <! SimilarRepos sortedSimilarRepos
                            typed subscriber <! GithubProgressStats finishStats)

                        settings.PublishTimer.Cancel ()
                        become waiting
                    | false ->
                        settings.Subscribers
                        |> Seq.iter (fun subscriber -> typed subscriber <! GithubProgressStats settings.GithubProgressStats)
                        ignored()
                | UsersToQuery users ->
                    // queue all the jobs
                    users |> Seq.iter (fun user -> githubWorker <! RetryableQuery { Query = QueryStarrer user.Login; AllowableTries = 3; CurrentAttempt = 0 })
                    become <| working {settings with GithubProgressStats = setExpectedUserCount settings.GithubProgressStats users.Length; ReceivedInitialUsers = true }
                // the actor is currently busy, cannot handle the job now
                | CanAcceptJob repoKey ->
                    context.Sender() <! UnableToAcceptJob repoKey
                    ignored()
                | SubscribeToProgressUpdates subscriber ->
                    // this is our first subscriber, which means we need to turn publishing on
                    if settings.Subscribers.Count = 0 then
                        context.System.Scheduler.ScheduleTellRepeatedly(
                            TimeSpan.FromMilliseconds 100., TimeSpan.FromMilliseconds 30.,
                            untyped context.Self, PublishUpdate, untyped context.Self, settings.PublishTimer)
                    settings.Subscribers.Add subscriber
                    |> ignored

                // query failed, but can be retried
                | RetryableQuery query when query.CanRetry ->
                    githubWorker <! RetryableQuery query
                    ignored()
                // query failed, can't be retried, and it's a QueryStarrers operation - meaning that the entire job failed
                | RetryableQuery query when not query.CanRetry && isQueryStarrers query.Query ->
                    settings.Subscribers
                    |> Seq.iter (fun subscriber -> typed subscriber <! JobFailed settings.CurrentRepo)

                    settings.PublishTimer.Cancel ()
                    become waiting
                // query failed, can't be retried, and it's a QueryStarrer operation - meaning that an individual operation failed
                | RetryableQuery query when not query.CanRetry && isQueryStarrer query.Query ->
                    become <| working {settings with GithubProgressStats = incrementFailures settings.GithubProgressStats 1 }
                | _ -> ignored()

            become waiting

module GithubCommanderActor =
    let create () =
        props <| fun context ->
            let coordinator =
                GithubCoordinatorActor.create()
                |> spawn context "coordinator"
                |> retype

            let rec behaviour canAcceptJobSender = function
                | GithubActorMessage (CanAcceptJob repoKey) ->
                    coordinator <! CanAcceptJob repoKey
                    context.Sender() |> behaviour |> become
                | GithubActorMessage (UnableToAcceptJob repoKey) ->
                    canAcceptJobSender <! UnableToAcceptJob repoKey
                    ignored()
                | AbleToAcceptJob repoKey ->
                    canAcceptJobSender <! AbleToAcceptJob repoKey
                    coordinator <! BeginJob repoKey // start processing mesages
                    select context "akka://GithubActors/user/mainform"
                        <! LaunchRepoResultsWindow (repoKey, untyped coordinator) // launch the new window to view results of the processing
                    ignored()
                | LifecycleEvent PostStop ->
                    retype coordinator <! PoisonPill.Instance
                    ignored()
                | _ -> unhandled()

            // ОПАСНО!
            behaviour (typed null) |> become

module RepoResultsActor =
    let create (usersGrid : DataGridView) (statusLabel : ToolStripStatusLabel) (progressBar : ToolStripProgressBar) =
        props <| fun context ->
            let startProgress stats =
                progressBar.Minimum <- 0
                progressBar.Step <- 1
                progressBar.Maximum <- stats.ExpectedUsers
                progressBar.Value <- stats.UsersThusFar
                progressBar.Visible <- true
                statusLabel.Visible <- true

            let displayProgress stats =
                statusLabel.Text <- sprintf "%i out of %i users (%i failures) [%A elapsed]" stats.UsersThusFar stats.ExpectedUsers stats.QueryFailures stats.Elapsed

            let stopProgress repo =
                progressBar.Visible <- true
                progressBar.ForeColor <- Color.Red
                progressBar.Maximum <- 1
                progressBar.Value <- 1
                statusLabel.Visible <- true
                statusLabel.Text <- sprintf "Failed to gather date for GitHub repository %s / %s" repo.Owner repo.Repo

            let displayRepo similarRepo =
                let repo = similarRepo.Repo
                let row = new DataGridViewRow()
                row.CreateCells usersGrid
                row.Cells.[0].Value <- repo.Owner.Login
                row.Cells.[1].Value <- repo.Owner.Name
                row.Cells.[2].Value <- repo.Owner.HtmlUrl
                row.Cells.[3].Value <- similarRepo.SharedStarrers
                row.Cells.[4].Value <- repo.OpenIssuesCount
                row.Cells.[5].Value <- repo.StargazersCount
                row.Cells.[6].Value <- repo.ForksCount
                usersGrid.Rows.Add row |> ignore

            let rec behaviour hasSetProgress = function
                | GithubProgressStats stats -> // progress update
                    let hasSetProgress =
                        not hasSetProgress && stats.ExpectedUsers > 0
                    if hasSetProgress then startProgress stats
                    displayProgress stats
                    progressBar.Value <- stats.UsersThusFar + stats.QueryFailures
                    behaviour hasSetProgress |> become
                | SimilarRepos repos -> // user update
                    repos |> Seq.iter displayRepo
                    ignored()
                | JobFailed repoKey -> // critical failure, like not being able to connect to Github
                    stopProgress repoKey
                    ignored()
                | _ -> unhandled()

            behaviour false |> become