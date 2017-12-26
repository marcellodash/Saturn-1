module Sample

open Saturn.Router
open Saturn.Controler

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Microsoft.AspNetCore

//Saturn is using standard HttpHandlers from Giraffe

let apiHelloWorld = text "hello world from API"
let apiHelloWorld2 = text "hello world from API 2"
let otherHelloWorld = text "hello world from OTHER"
let otherHelloWorld2 = text "hello world from OTHER 2"
let helloWorld = text "hello world"
let helloWorld2 = text "hello world2"
let helloWorldName str = text ("hello world, " + str)
let helloWorldNameAge (str, age) = text (sprintf "hello world, %s, You're %i" str age)

//Pipeline CE is used to compose HttpHandlers together in more declarative way.
//At the moment only low level helpers in form of custom CE keywords are prvovded.
//But I hope to add some more high level helpers (for example `accept_json` instead of `must_accept "application/json" )
//Phoenix default set of plugs can be good source of ideas.
//Pipelines are composed together with `plug` keyowrd

let apiHeaderPipe = pipeline {
    set_header "myCustomHeaderApi" "api"
}

let otherHeaderPipe = pipeline {
    set_header "myCustomHeaderOther" "other"
}


let headerPipe = pipeline {
    set_header "myCustomHeader" "abcd"
    set_header "myCustomHeader2" "zxcv"
}

//`scope` CE is used to declare (sub)routers (using TokenRouter). It provides custom keywords for all HTTP methods
// supported by TokenRouter which supports type-safe formatting of routes. It's composed together with pipelines
// with `pipe_through` method which means that all handlers registed in router will be piped through given pipeline
// It enables composition with other routers (and any HttpHandlers) with `forward` keyword - it will behave
// like `subRoute`, modify value of `Path` on HttpContext. It also enables setting custom error/not found handler
// with `error_handler` keyword.
// It automatically supports grouping handlers registered for same path into `choose` combinator, what is
// not supported out of the box in TokenRouter - if you have multiple handlers registerd for `get "/"` they will be grouped,
// on the TokenRouter matching we will create single `route "/"` call, but the HttpHandler passed to it will be `choose` build
// from all registed handlers for this route.

let apiRouter = scope {
    pipe_through apiHeaderPipe
    error_handler (text "Api 404")


    get "/" apiHelloWorld
    get "/a" apiHelloWorld2
}

let otherRouter = scope {
    pipe_through otherHeaderPipe
    error_handler (text "Other 404")

    get "/" otherHelloWorld
    get "/a" otherHelloWorld2
}

//`controler<'Key>` CE is higher level abstraction following convention of Phoenix Controllers and `resources` macro. It will create
// complex routing for predefined set of operations which looks like this:
// [
//     GET [
//         route "/" index
//         route "/add" add
//         routef "/%?" show
//         routef "/%?/edit" edit
//     ]
//     POST [
//         route "/" create
//     ]
//     PUT [
//         route "/%?" update
//     ]
//     PATCH [
//         route "/%?" update
//     ]
//     DELETE [
//         route "/%?" delete
//     ]
// ]
// The exact format argument of `routef` routes is created based on generic type passed to CE - it supports same types what Giraffe `routef`
// If any of the actions is not provided in CE it won't be added to routing table.
// By convention given handlers should do following actions:
// index -render list of all items
// add - render form for adding new item
// show - render single item
// edit - render form for editing item
// create - add item
// update - update item
// delete - delete item

let userControler = controler<string> {
    error_handler (text "Users 404")

    index (fun (_nxt, ctx) -> ctx.WriteTextAsync "Index handler")
    add (fun (_nxt, ctx) -> ctx.WriteTextAsync "Add handler")
    show (fun (_nxt, ctx, id) -> ctx.WriteTextAsync (sprintf "Show handler - %s" id))
    edit (fun (_nxt, ctx, id) -> ctx.WriteTextAsync (sprintf "Edit handler - %s" id))
}

//Since all computation expressions produces `HttpHandler` everything can be easily composed together in nice declarative way.
//I belive that aim of the Saturn should be providing a more streamlined, higher level developer experiance on top of the great
//Giraffe's model. It's bit like Phoenix using Plug model under the hood.

let router = scope {
    pipe_through headerPipe
    error_handler (text "404")


    get "/" helloWorld
    get "/a" helloWorld2
    getf "/name/%s" helloWorldName
    getf "/name/%s/%i" helloWorldNameAge

    forward "/other" otherRouter
    forward "/api" apiRouter
    forward "/users" userControler
}


[<EntryPoint>]
let main _ =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> Giraffe.HttpStatusCodeHandlers.ServerErrors.INTERNAL_ERROR ex.Message

    let configureApp (app : IApplicationBuilder) =
        app.UseGiraffeErrorHandler(errorHandler)
            .UseGiraffe router


    WebHost.CreateDefaultBuilder()
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .UseUrls("http://0.0.0.0:8085/")
        .Build()
        .Run()

    0 // return an integer exit code

