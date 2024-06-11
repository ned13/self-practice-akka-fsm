namespace SelfPractice.AkkaFsm

open Akka.Actor
open Akka.Event

module TripState =
    type TripStateTrigger =
                | ConfirmRequest
                | AssignDriver
                | DriverArrived
                | StartTrip
                | RiderLeft
                | ArrivedAtDestination
                | CompleteTrip
                | Cancel

    module CreatedState =
        type State = RequestReceived | AwaitingDriver | Completed
        type IStateData = interface end
        type NoStateData private () =
            interface IStateData
            static member val Instance = NoStateData() with get

        type CreatedStateActor(tripActor: IActorRef) as this =
            inherit FSM<State, IStateData>()
            let _logger: ILoggingAdapter = CreatedStateActor.Context.GetLogger()

            do
                let self = this.Self
                this.StartWith(RequestReceived, NoStateData.Instance)
                this.When(RequestReceived,
                            fun state ->
                                match state.FsmEvent with
                                | :? TripStateTrigger as tt ->
                                    match tt with
                                    | ConfirmRequest -> this.GoTo(AwaitingDriver)
                                    | Cancel -> this.GoTo(Completed)
                                    | _ -> this.Stay()
                                | _ -> this.Stay())
                this.When(AwaitingDriver,
                           fun state ->
                               match state.FsmEvent with
                               | :? TripStateTrigger as tt ->
                                   match tt with
                                   | AssignDriver ->
                                       tripActor.Tell(AssignDriver, self)
                                       this.GoTo(Completed)
                                   | Cancel -> this.GoTo(Completed)
                                   | _ -> this.Stay()
                               | _ -> this.Stay())
                this.When(Completed,
                          fun state ->
                              _logger.Info($"CreatedStateActor {Completed}")
                              this.Stop()
                          )
                this.WhenUnhandled(
                        fun state ->
                            _logger.Warning($"Received unhandled request {state.FsmEvent} {this.StateName} {this.StateData}")
                            this.Stay()
                        )

                this.Initialize()

            static member Props(tripActor: IActorRef) =
                Props.Create<CreatedStateActor>(fun () -> CreatedStateActor(tripActor))

    module DriverAssignedState =
        type State = DriverEnRoute | DriverArrived | Completed
        type StateData = private | NoStateData

        type DriverStateActor(tripActor: IActorRef) as this =
            inherit FSM<State, StateData>()
            let _logger: ILoggingAdapter = DriverStateActor.Context.GetLogger()

            do
                let self = this.Self
                this.StartWith(DriverEnRoute, NoStateData)
                this.When(DriverEnRoute,
                          fun state ->
                                match state.FsmEvent with
                                | :? TripStateTrigger as tt ->
                                    match tt with
                                    | TripStateTrigger.DriverArrived -> this.GoTo(DriverArrived)
                                    | Cancel -> this.GoTo(Completed)
                                    | _ -> this.Stay()
                                | _ -> this.Stay())
                this.When(DriverArrived,
                          fun state ->
                               match state.FsmEvent with
                               | :? TripStateTrigger as tt ->
                                   match tt with
                                   | StartTrip ->
                                       tripActor.Tell(StartTrip, self)
                                       this.GoTo(Completed)
                                   | Cancel -> this.GoTo(Completed)
                                   | _ -> this.Stay()
                               | _ -> this.Stay())
                this.When(Completed,
                          fun state ->
                              _logger.Info($"DriverStateActor {Completed}")
                              this.Stop()
                          )
                this.WhenUnhandled(
                        fun state ->
                            _logger.Warning($"Received unhandled request {state.FsmEvent} {this.StateName} {this.StateData}")
                            this.Stay()
                        )
                this.Initialize()

            static member Props(tripActor: IActorRef) =
                Props.Create<DriverStateActor>(fun () -> DriverStateActor(tripActor))

    module InProgressState =

        module DriverState =
            type State = Driving | Arrived | Completed
            type StateData = private | NoStateData

            type DriverStateActor(inProgressStateActor: IActorRef) as this =
                inherit FSM<State, StateData>()
                let _logger: ILoggingAdapter = DriverStateActor.Context.GetLogger()

                do
                    let self = this.Self
                    this.StartWith(Driving, NoStateData)
                    this.When(Driving,
                              fun state ->
                                    match state.FsmEvent with
                                    | :? TripStateTrigger as tt ->
                                        match tt with
                                        | ArrivedAtDestination -> this.GoTo(Arrived)
                                        | Cancel -> this.GoTo(Completed)
                                        | _ -> this.Stay()
                                    | _ -> this.Stay())
                    this.When(Arrived,
                              fun state ->
                                    match state.FsmEvent with
                                    | :? TripStateTrigger as tt ->
                                        match tt with
                                        | CompleteTrip ->
                                            inProgressStateActor.Tell(CompleteTrip, self)
                                            this.GoTo(Completed)
                                        | Cancel -> this.GoTo(Completed)
                                        | _ -> this.Stay()
                                    | _ -> this.Stay())
                    this.When(Completed,
                              fun state ->
                                  _logger.Info($"DriverStateActor {Completed}")
                                  this.Stop()
                              )
                    this.WhenUnhandled(
                            fun state ->
                                _logger.Warning($"Received unhandled request {state.FsmEvent} {this.StateName} {this.StateData}")
                                this.Stay()
                            )
                    this.Initialize()

                static member Props(inProgressStateActor: IActorRef) =
                    Props.Create<DriverStateActor>(fun () -> DriverStateActor(inProgressStateActor))


        module RiderState =
            type State = InCar | LeftCar | Completed
            type StateData = private | NoStateData

            type RiderStateActor(inProgressStateActor: IActorRef) as this =
                inherit FSM<State, StateData>()
                let _logger: ILoggingAdapter = RiderStateActor.Context.GetLogger()

                do
                    let self = this.Self
                    this.StartWith(InCar, NoStateData)
                    this.When(InCar,
                              fun state ->
                                    match state.FsmEvent with
                                    | :? TripStateTrigger as tt ->
                                        match tt with
                                        | RiderLeft -> this.GoTo(LeftCar)
                                        | Cancel -> this.GoTo(Completed)
                                        | _ -> this.Stay()
                                    | _ -> this.Stay())
                    this.When(LeftCar,
                              fun state ->
                                    match state.FsmEvent with
                                    | :? TripStateTrigger as tt ->
                                        match tt with
                                        | CompleteTrip ->
                                            inProgressStateActor.Tell(CompleteTrip, self)
                                            this.GoTo(Completed)
                                        | Cancel -> this.GoTo(Completed)
                                        | _ -> this.Stay()
                                    | _ -> this.Stay())
                    this.When(Completed,
                              fun state ->
                                  _logger.Info($"RiderStateActor {Completed}")
                                  this.Stop()
                              )
                    this.WhenUnhandled(
                            fun state ->
                                _logger.Warning($"Received unhandled request {state.FsmEvent} {this.StateName} {this.StateData}")
                                this.Stay()
                            )
                    this.Initialize()

                static member Props(inProgressStateActor: IActorRef) =
                    Props.Create<RiderStateActor>(fun () -> RiderStateActor(inProgressStateActor))

        type State = | InProgress | Completed
        type StateData = {
            DriverStateActorCompleted: bool
            RiderStateActorCompleted: bool
        }
        with
            member this.IsAllCompleted = this.DriverStateActorCompleted && this.RiderStateActorCompleted

        type InprogressStateActor(tripActor: IActorRef) as this =
            inherit FSM<State, StateData>()
            let _logger: ILoggingAdapter = InprogressStateActor.Context.GetLogger()

            do
                let self = this.Self
                let context = InprogressStateActor.Context
                let driverStateActor = context.ActorOf(DriverState.DriverStateActor.Props(self))
                let riderStateActor = context.ActorOf(RiderState.RiderStateActor.Props(self))
                let innerCompletionStateData = { DriverStateActorCompleted = false; RiderStateActorCompleted = false }

                this.StartWith(InProgress, innerCompletionStateData)
                this.When(InProgress,
                          fun state ->
                                match state.FsmEvent with
                                | :? TripStateTrigger as tt ->
                                    match tt with
                                    | ArrivedAtDestination ->
                                        driverStateActor.Tell(tt)
                                        this.Stay()
                                    | RiderLeft ->
                                        riderStateActor.Tell(tt)
                                        this.Stay()
                                    | CompleteTrip ->
                                        if context.Sender.Equals(tripActor) then
                                            // 從 TripActor 來就往下送
                                            driverStateActor.Tell(tt)
                                            riderStateActor.Tell(tt)
                                            this.Stay()
                                        else if context.Sender.Equals(driverStateActor) then
                                            let nextStateData = { state.StateData with DriverStateActorCompleted = true }
                                            if nextStateData.IsAllCompleted then
                                                tripActor.Tell(CompleteTrip, self)
                                                this.GoTo(Completed)
                                            else
                                                this.Stay().Using(nextStateData)
                                        else if context.Sender.Equals(riderStateActor) then
                                            let nextStateData = { state.StateData with RiderStateActorCompleted = true }
                                            if nextStateData.IsAllCompleted then
                                                tripActor.Tell(CompleteTrip, self)
                                                this.GoTo(Completed)
                                            else
                                                this.Stay().Using(nextStateData)
                                        else
                                            // 不知道從哪裡來就不理它
                                            this.Stay()

                                    | Cancel ->
                                        driverStateActor.Tell(Cancel)
                                        riderStateActor.Tell(Cancel)
                                        this.GoTo(Completed)
                                    | _ -> this.Stay()
                                | _ -> this.Stay())
                this.When(Completed,
                          fun state ->
                              _logger.Info($"InprogressStateActor {Completed}")
                              this.Stop()
                          )
                this.WhenUnhandled(
                        fun state ->
                            _logger.Warning($"Received unhandled request {state.FsmEvent} {this.StateName} {this.StateData}")
                            this.Stay()
                        )
                this.Initialize()

            static member Props(tripActor: IActorRef) =
                Props.Create<InprogressStateActor>(fun () -> InprogressStateActor(tripActor))

    type State = Created | InProgress | DriverAssigned | Canceled | Completed

    type NoStateData = private | NoStateData
    type CreatedStateData = {
        CreatedStateActor: IActorRef
    }
    type DriverAssignedStateData = {
        DriverAssignedStateActor: IActorRef
    }
    type InProgressStateData = {
        InprogressStateActor: IActorRef
    }


    type StateData = private
                     | NoStateData
                     | CreatedStateData of CreatedStateData
                     | DriverAssignedStateData of DriverAssignedStateData
                     | InProgressStateData of InProgressStateData

    type TripStateActor() as this =
        inherit FSM<State, StateData>()

        let _logger: ILoggingAdapter = TripStateActor.Context.GetLogger()

        let onCreated (this: TripStateActor) (context: IActorContext) (csd: CreatedStateData) (tt: TripStateTrigger) =
            let createdStateActor = csd.CreatedStateActor
            match tt with
            | ConfirmRequest ->
                createdStateActor.Tell(tt)
                this.Stay().Using(CreatedStateData csd)
            | AssignDriver ->
                if context.Sender.Equals(createdStateActor) then
                    this.GoTo(DriverAssigned).Using(NoStateData)
                else
                    createdStateActor.Tell(tt)
                    this.Stay().Using(CreatedStateData csd)
            | Cancel ->
                createdStateActor.Tell(Cancel)
                this.GoTo(Canceled).Using(NoStateData)
            | _ -> this.Stay()

        let onDriverAssigned (this: TripStateActor) (context: IActorContext) (dasd: DriverAssignedStateData) (tt: TripStateTrigger) =
            let driverAssignedStateActor = dasd.DriverAssignedStateActor
            match tt with
            | DriverArrived ->
                driverAssignedStateActor.Tell(tt)
                this.Stay().Using(DriverAssignedStateData dasd)
            | StartTrip ->
                if context.Sender.Equals(driverAssignedStateActor) then
                    this.GoTo(InProgress).Using(NoStateData)
                else
                    driverAssignedStateActor.Tell(tt)
                    this.Stay().Using(DriverAssignedStateData dasd)
            | Cancel ->
                driverAssignedStateActor.Tell(Cancel)
                this.GoTo(Canceled).Using(NoStateData)
            | _ -> this.Stay()

        let onInprogress (this: TripStateActor) (context: IActorContext) (ipsd: InProgressStateData) (tt: TripStateTrigger) =
            let inprogressStateActor = ipsd.InprogressStateActor
            match tt with
            | ArrivedAtDestination
            | RiderLeft ->
                inprogressStateActor.Tell(tt)
                this.Stay().Using(InProgressStateData ipsd)
            | CompleteTrip ->
                if context.Sender.Equals(inprogressStateActor) then
                    this.GoTo(Completed).Using(NoStateData)
                else
                    inprogressStateActor.Tell(tt)
                    this.Stay().Using(InProgressStateData ipsd)
            | Cancel ->
                inprogressStateActor.Tell(Cancel)
                this.GoTo(Canceled).Using(NoStateData)
            | _ -> this.Stay()

        do
            let self = this.Self
            let context = TripStateActor.Context
            this.StartWith(Created, NoStateData)
            this.When(Created,
                      fun state ->
                            match state.StateData, state.FsmEvent with
                            | NoStateData, (:? TripStateTrigger as tt) ->
                                let newActor = context.ActorOf(CreatedState.CreatedStateActor.Props(self))
                                let createdStateData = { CreatedStateActor = newActor }
                                let nextState = onCreated this context createdStateData tt
                                nextState
                            | CreatedStateData csd, (:? TripStateTrigger as tt) ->
                                let nextState = onCreated this context csd tt
                                nextState
                            | _, _ -> this.Stay())
            this.When(DriverAssigned,
                      fun state ->
                            match state.StateData, state.FsmEvent with
                            | NoStateData, (:? TripStateTrigger as tt) ->
                                let newActor = context.ActorOf(DriverAssignedState.DriverStateActor.Props(self))
                                let driverAssignedStateData = { DriverAssignedStateActor = newActor }
                                onDriverAssigned this context driverAssignedStateData tt
                            | DriverAssignedStateData dasd, (:? TripStateTrigger as tt) ->
                                onDriverAssigned this context dasd tt
                            | _, _ -> this.Stay())
            this.When(InProgress,
                      fun state ->
                            match state.StateData, state.FsmEvent with
                            | NoStateData, (:? TripStateTrigger as tt) ->
                                let newActor = context.ActorOf(InProgressState.InprogressStateActor.Props(self))
                                let driverAssignedStateData = { InprogressStateActor = newActor }
                                onInprogress this context driverAssignedStateData tt
                            | InProgressStateData ipsd, (:? TripStateTrigger as tt) ->
                                onInprogress this context ipsd tt
                            | _, _ -> this.Stay())
            this.When(Completed, fun state -> this.Stop())
            this.When(Canceled, fun state -> this.Stop())

            this.Initialize()


        static member Props() =
            Props.Create<TripStateActor>(fun () -> TripStateActor())