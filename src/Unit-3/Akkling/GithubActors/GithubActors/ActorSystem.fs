namespace GithubActors

open Akkling

module ActorSystem =
    let githubActors = System.create "GithubActors" (Configuration.load())