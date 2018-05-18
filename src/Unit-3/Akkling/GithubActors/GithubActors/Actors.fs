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

[<AutoOpen>]
module private ActorUtils =
    let (|TaskCancelled|TaskFaulted|TaskSucceeded|) (task : System.Threading.Tasks.Task<_>) =
        match task.IsCanceled, task.IsFaulted with
        | true, _ -> TaskCancelled
        | false, true -> TaskFaulted
        | false, false -> TaskSucceeded task.Result

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

                    client.User.Current().ContinueWith (function
                        | TaskCancelled -> AuthenticationFailed
                        | TaskFaulted -> AuthenticationFailed
                        | TaskSucceeded _ ->
                            GithubClientFactory.setOauthToken token
                            AuthenticationSuccess
                        )
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
    let create (getGithubClient : _ -> Octokit.GitHubClient) =
        let splitIntoOwnerAndRepo repoUri =
            let results = Uri(repoUri, UriKind.Absolute).PathAndQuery.TrimEnd('/').Split('/') |> Array.rev
            results.[1], results.[0] // User, Repo

        props <| fun context ->
            let behaviour = function
                // outright invalid URLs
                | ValidateRepo uri when
                    String.IsNullOrEmpty uri
                    || not <| Uri.IsWellFormedUriString(uri, UriKind.Absolute) ->
                    context.Sender() <! InvalidRepo (uri, "Not a valid absolute URI")
                    ignored()
                // repos that at least have a valid absolute URL
                | ValidateRepo uri ->
                    let user, repo = splitIntoOwnerAndRepo uri

                    getGithubClient().Repository.Get(user, repo).ContinueWith (function
                        | TaskCancelled ->
                            InvalidRepo (uri, "Repo lookup timed out")
                        | TaskFaulted ->
                            InvalidRepo (uri, "Not a valid absolute URI")
                        | TaskSucceeded result ->
                            ValidRepo result)
                    |> Async.AwaitTask
                    // send the message back to ourselves but pass the real sender through
                    |> pipeToWithSender context.Self (context.Sender() |> untyped)
                    ignored()
                | InvalidRepo _ as invRepo ->
                    context.Sender() <<! invRepo
                    ignored()
                | ValidRepo repo ->
                    select context "akka://GithubActors/user/commander"
                        <! CanAcceptJob { Owner = repo.Owner.Login; Repo = repo.Name }
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
                    let sender_ = context.Sender()
                    githubClient.Value.Activity.Starring.GetAllForUser(login).ContinueWith (function
                        | TaskFaulted | TaskCancelled -> RetryableQuery (nextTry query)
                        | TaskSucceeded result -> StarredReposForUser (login, result))
                    |> Async.AwaitTask
                    |!> sender_
                    ignored()
                | RetryableQuery ({ Query = GithubActorMessage (QueryStarrers repoKey) } as query) ->
                    let sender_ = context.Sender()
                    githubClient.Value.Activity.Starring
                        .GetAllStargazers(repoKey.Owner, repoKey.Repo).ContinueWith (function
                            | TaskFaulted | TaskCancelled -> RetryableQuery (nextTry query)
                            | TaskSucceeded result -> result |> Array.ofSeq |> UsersToQuery)
                    |> Async.AwaitTask
                    |!> sender_
                    ignored()
                | _ -> unhandled()

            behaviour |> become

module GithubCoordinatorActor =
    let create () =
        let startWorking repoKey (scheduler: IScheduler) =
            {
                ReceivedInitialUsers = false
                CurrentRepo = repoKey
                Subscribers = System.Collections.Generic.HashSet<IActorRef> ()
                SimilarRepos = System.Collections.Generic.Dictionary<string, SimilarRepo> ()
                GithubProgressStats = getDefaultStats ()
                PublishTimer = new Cancelable (scheduler)
            }

        props <| fun context ->

            // pre-start
            let githubWorker =
                { GithubWorkerActor.create() with
                    Router =
                        Akka.Routing.RoundRobinPool 10
                        :> Akka.Routing.RouterConfig
                        |> Some }
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
                | StarredReposForUser (_, repos) ->
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
                | RetryableQuery { Query = GithubActorMessage (QueryStarrers _) } ->
                    settings.Subscribers
                    |> Seq.iter (fun subscriber -> typed subscriber <! JobFailed settings.CurrentRepo)

                    settings.PublishTimer.Cancel ()
                    become waiting
                // query failed, can't be retried, and it's a QueryStarrer operation - meaning that an individual operation failed
                | RetryableQuery { Query = GithubActorMessage (QueryStarrer _) } ->
                    become <| working {settings with GithubProgressStats = incrementFailures settings.GithubProgressStats 1 }
                | _ -> ignored()

            become waiting

module GithubCommanderActor =
    open Akka.Routing

    let create () =
        props <| fun (context : Actor<obj>) ->
            let coordinator =
                { GithubCoordinatorActor.create () with
                    Router = Some (FromConfig.Instance :> RouterConfig) }
                |> spawn context "coordinator"

            // Наверное не гуд.
            let mutable currentRepoKey = None

            // Зачем их передавать, если они все равно будут перезаписаны?
            let rec ready = function
                | GithubActorMessage (CanAcceptJob repoKey) ->
                    coordinator <! CanAcceptJob repoKey
                    currentRepoKey <- Some repoKey
                    // Ask how many coordinator instances were created (i.e. how many pending job replies are expected)
                    let routees : Routees = retype coordinator <? GetRoutees() |> Async.RunSynchronously

                    TimeSpan.FromSeconds 3.
                        |> Some
                        |> context.SetReceiveTimeout
                    routees.Members
                        |> Seq.length
                        |> asking (context.Sender())
                        |> become
                | msg -> checkDefer msg
            and (|ReceiveTimeout|_|) = tryUnbox<ReceiveTimeout>
            and asking canAcceptJobSender pendingJobReplies = function
                | ReceiveTimeout _ ->
                    currentRepoKey
                        |> Option.iter (fun p -> canAcceptJobSender <! UnableToAcceptJob p)
                    context.UnstashAll()
                    context.SetReceiveTimeout None
                    ready |> become
                | GithubActorMessage (CanAcceptJob _) ->
                    context.Stash()
                    ignored()
                | GithubActorMessage (UnableToAcceptJob repoKey) ->
                    let currentPendingJobReplies = pendingJobReplies - 1
                    if currentPendingJobReplies <= 0 then
                        canAcceptJobSender <! UnableToAcceptJob repoKey
                        context.UnstashAll()
                        context.SetReceiveTimeout None
                        become ready
                    else
                        become (asking canAcceptJobSender currentPendingJobReplies)
                | GithubActorMessage (AbleToAcceptJob repoKey) ->
                    canAcceptJobSender <! AbleToAcceptJob repoKey
                    // start processing mesages
                    context.Sender() <! BeginJob repoKey
                    // launch the new window to view results of the processing
                    select context "akka://GithubActors/user/mainform"
                        <! LaunchRepoResultsWindow (repoKey, untyped <| context.Sender())
                    context.UnstashAll()
                    context.SetReceiveTimeout None
                    ready |> become
                | msg -> checkDefer msg
            and checkDefer = function
                | LifecycleEvent PostStop ->
                    retype coordinator <! PoisonPill.Instance
                    ignored()
                | _ -> unhandled()

            ready |> become

module RepoResultsActor =
    let create (usersGrid : DataGridView) (statusLabel : ToolStripStatusLabel) (progressBar : ToolStripProgressBar) =
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

        props <| fun _ ->
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