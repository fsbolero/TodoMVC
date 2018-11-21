module Bolero.TodoMVC.Client.Main

open Elmish
open Bolero
open Bolero.Html

/// Parses the index.html file and provides types to fill it with dynamic content.
type MasterTemplate = Template<"template.html">

/// Our application has three URL endpoints.
type EndPoint =
    | [<EndPoint "/">] All
    | [<EndPoint "/active">] Active
    | [<EndPoint "/completed">] Completed

/// This module defines the model, the update and the view for a single entry.
module Entry =

    /// The unique identifier of a Todo entry.
    type Key = int

    /// The model for a Todo entry.
    type Model =
        {
            Id : Key
            Task : string
            IsCompleted : bool
            Editing : option<string>
        }

    let New (key: Key) (task: string) =
        {
            Id = key
            Task = task
            IsCompleted = false
            Editing = None
        }

    type Message =
        | Remove
        | StartEdit
        | Edit of text: string
        | CommitEdit
        | CancelEdit
        | SetCompleted of completed: bool

    /// Defines how a given Todo entry is updated based on a message.
    /// Returns Some to update the entry, or None to delete it.
    let Update (msg: Message) (e: Model) : option<Model> =
        match msg with
        | Remove ->
            None
        | StartEdit ->
            Some { e with Editing = Some e.Task }
        | Edit value ->
            Some { e with Editing = e.Editing |> Option.map (fun _ -> value) }
        | CommitEdit ->
            Some { e with
                    Task = e.Editing |> Option.defaultValue e.Task
                    Editing = None }
        | CancelEdit ->
            Some { e with Editing = None }
        | SetCompleted value ->
            Some { e with IsCompleted = value }

    /// Render a given Todo entry.
    let Render (endpoint, entry) dispatch =
        MasterTemplate.Entry()
            .Label(text entry.Task)
            .CssAttrs(
                attr.classes [
                    if entry.IsCompleted then yield "completed"
                    if entry.Editing.IsSome then yield "editing"
                    match endpoint, entry.IsCompleted with
                    | EndPoint.Completed, false
                    | EndPoint.Active, true -> yield "hidden"
                    | _ -> ()
                ]
            )
            .EditingTask(
                entry.Editing |> Option.defaultValue "",
                fun text -> dispatch (Message.Edit text)
            )
            .EditBlur(fun _ -> dispatch Message.CommitEdit)
            .EditKeyup(fun e ->
                match e.Key with
                | "Enter" -> dispatch Message.CommitEdit
                | "Escape" -> dispatch Message.CancelEdit
                | _ -> ()
            )
            .IsCompleted(
                entry.IsCompleted,
                fun x -> dispatch (Message.SetCompleted x)
            )
            .Remove(fun _ -> dispatch Message.Remove)
            .StartEdit(fun _ -> dispatch Message.StartEdit)
            .Elt()

    type Component() =
        inherit ElmishComponent<EndPoint * Model, Message>()

        override this.ShouldRender(oldModel, newModel) = oldModel <> newModel

        override this.View model dispatch = Render model dispatch

/// This module defines the model, the update and the view for a full todo list.
module TodoList =    

    /// The model for the full TodoList application.
    type Model =
        {
            EndPoint : EndPoint
            NewTask : string
            Entries : list<Entry.Model>
            NextKey : Entry.Key
        }

        static member Empty =
            {
                EndPoint = All
                NewTask = ""
                Entries = []
                NextKey = 0
            }

    type Message =
        | EditNewTask of text: string
        | AddEntry
        | ClearCompleted
        | SetAllCompleted of completed: bool
        | EntryMessage of key: Entry.Key * message: Entry.Message
        | SetEndPoint of EndPoint

    let Router = Router.infer SetEndPoint (fun m -> m.EndPoint)

    /// Defines how the Todo list is updated based on a message.
    let Update (msg: Message) (model: Model) =
        match msg with
        | EditNewTask value ->
            { model with NewTask = value }
        | AddEntry ->
            { model with
                NewTask = ""
                Entries = model.Entries @ [Entry.New model.NextKey model.NewTask]
                NextKey = model.NextKey + 1 }
        | ClearCompleted ->
            { model with Entries = List.filter (fun e -> not e.IsCompleted) model.Entries }
        | SetAllCompleted c ->
            { model with Entries = List.map (fun e -> { e with IsCompleted = c }) model.Entries }
        | EntryMessage (key, msg) ->
            let updateEntry (e: Entry.Model) =
                if e.Id = key then Entry.Update msg e else Some e
            { model with Entries = List.choose updateEntry model.Entries }
        | SetEndPoint ep ->
            { model with EndPoint = ep }

    /// Render the whole application.
    let Render (state: Model) (dispatch: Dispatch<Message>) =
        let countNotCompleted =
            state.Entries
            |> List.filter (fun e -> not e.IsCompleted)
            |> List.length
        MasterTemplate()
            .HiddenIfNoEntries(if List.isEmpty state.Entries then "hidden" else "")
            .Entries(
                forEach state.Entries <| fun entry ->
                    let entryDispatch msg = dispatch (EntryMessage (entry.Id, msg))
                    ecomp<Entry.Component,_,_> (state.EndPoint, entry) entryDispatch
            )
            .ClearCompleted(fun _ -> dispatch Message.ClearCompleted)
            .IsCompleted(
                (countNotCompleted = 0),
                fun c -> dispatch (Message.SetAllCompleted c)
            )
            .Task(
                state.NewTask,
                fun text -> dispatch (Message.EditNewTask text)
            )
            .Edit(fun e ->
                if e.Key = "Enter" && state.NewTask <> "" then
                    dispatch Message.AddEntry
            )
            .ItemsLeft(
                match countNotCompleted with
                | 1 -> "1 item left"
                | n -> string n + " items left"
            )
            .CssFilterAll(attr.``class`` (if state.EndPoint = EndPoint.All then "selected" else null))
            .CssFilterActive(attr.``class`` (if state.EndPoint = EndPoint.Active then "selected" else null))
            .CssFilterCompleted(attr.``class`` (if state.EndPoint = EndPoint.Completed then "selected" else null))
            .Elt()

    /// The entry point of our application, called on page load.
    type Component() =
        inherit ProgramComponent<Model, Message>()

        override this.Program =
            Program.mkSimple (fun _ -> Model.Empty) Update Render
            |> Program.withRouter Router
