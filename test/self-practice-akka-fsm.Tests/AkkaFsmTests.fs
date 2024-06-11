module SelfPractice.AkkaFsmTests

open Akka.Actor
open Akka.TestKit.Xunit2

open Xunit
open FluentAssertions
open SelfPractice.AkkaFsm.TripState
open System.Collections.Generic

let tripTriggerNone = Option<TripStateTrigger>.None

let testTransition (this: TestKit) (props: Props) (initState: 'TState) (triggerAndExpectedStates: ('TState*TripStateTrigger*'TState*(TripStateTrigger option)) []) =
            let stateActor = this.Sys.ActorOf(props)
            let probe = this.CreateTestProbe();
            probe.Watch(stateActor) |> ignore

            stateActor.Tell(FSMBase.SubscribeTransitionCallBack(probe))
            probe.AwaitCondition(fun () -> probe.HasMessages)
            probe.ExpectMsg<FSMBase.CurrentState<'TState>>().Should().Equals(FSMBase.CurrentState(stateActor, initState)) |> ignore

            for tAndE in triggerAndExpectedStates do
                let orgState, trigger, expectedState, outTriggerOpt = tAndE
                stateActor.Tell(trigger)

                if orgState <> expectedState then
                    probe.AwaitCondition(fun () -> probe.HasMessages)
                    probe.ExpectMsg<FSMBase.Transition<'TState>>().Should().Equals(FSMBase.Transition(stateActor, orgState, expectedState)) |> ignore

                    // No termination for stop, tried following way. there may have another way to verify it.
                    // if expectedState = Completed then
                        //probe.AwaitAssert(fun () -> probe.ExpectMsg<Terminated>().ActorRef.Should().Equals(createdStateActor) |> ignore)
                        //probe.AwaitCondition(fun () -> probe.HasMessages)
                        //probe.ExpectMsg<Terminated>().ActorRef.Should().Equals(createdStateActor) |> ignore
                else
                    probe.ExpectNoMsg()

                match outTriggerOpt with
                | Some t -> this.ExpectMsg<TripStateTrigger>().Should().Equals(t) |> ignore
                | None -> ()

            stateActor.Tell(FSMBase.UnsubscribeTransitionCallBack(probe))

module TripState =

    module CreatedState =
        open SelfPractice.AkkaFsm.TripState.CreatedState
        type CreateStateActorTests() =
            inherit TestKit()

            [<Fact>]
            member _.``Normal state should send AssignDriver to TripActor``() =
                let tripActor = base.TestActor
                let createdStateActor = base.Sys.ActorOf(CreatedStateActor.Props(tripActor))
                createdStateActor.Tell(ConfirmRequest)
                createdStateActor.Tell(AssignDriver)
                base.ExpectMsg<TripStateTrigger>().Should().Equals(AssignDriver)

            static member private StateTransitionData() : IEnumerable<obj[]> =
                seq {
                    // Normal case
                    yield [| [| RequestReceived, ConfirmRequest, AwaitingDriver, tripTriggerNone;
                                AwaitingDriver, AssignDriver, Completed, Some AssignDriver |] |]

                    // RequestReceived
                    yield [| [| RequestReceived, Cancel, Completed, tripTriggerNone |] |]
                    yield [| [| RequestReceived, DriverArrived , RequestReceived, tripTriggerNone |] |]

                    // AwaitingDriver
                    yield [| [| RequestReceived, ConfirmRequest, AwaitingDriver, tripTriggerNone;
                                AwaitingDriver, Cancel, Completed, tripTriggerNone; |] |]
                    yield [| [| RequestReceived, ConfirmRequest, AwaitingDriver, tripTriggerNone;
                                AwaitingDriver, DriverArrived, AwaitingDriver, tripTriggerNone; |] |]

                }
            [<Theory>]
            [<MemberData("StateTransitionData", MemberType=typeof<CreateStateActorTests>)>]
            member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
                let props = CreatedStateActor.Props(this.TestActor)
                testTransition this props RequestReceived triggerAndExpectedStates

    module DriverAssignedState =
        open SelfPractice.AkkaFsm.TripState.DriverAssignedState

        type DriverStateActorTests() =
            inherit TestKit()

            static member private StateTransitionData() : IEnumerable<obj[]> =
                seq {
                    // Normal case
                    yield [| [| DriverEnRoute, TripStateTrigger.DriverArrived, State.DriverArrived, tripTriggerNone;
                                DriverArrived, StartTrip, Completed, Some StartTrip |] |]

                    // DriverEnRoute
                    yield [| [| DriverEnRoute, Cancel, Completed, tripTriggerNone |] |]
                    yield [| [| DriverEnRoute, ConfirmRequest , DriverEnRoute, tripTriggerNone |] |]

                    // AwaitingDriver
                    yield [| [| DriverEnRoute, TripStateTrigger.DriverArrived, State.DriverArrived, tripTriggerNone;
                                DriverArrived, Cancel, Completed, tripTriggerNone; |] |]
                    yield [| [| DriverEnRoute, TripStateTrigger.DriverArrived, State.DriverArrived, tripTriggerNone;
                                DriverArrived, ConfirmRequest, DriverArrived, tripTriggerNone; |] |]

                }
            [<Theory>]
            [<MemberData("StateTransitionData", MemberType=typeof<DriverStateActorTests>)>]
            member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
                let props = DriverStateActor.Props(this.TestActor)
                testTransition this props DriverEnRoute triggerAndExpectedStates

    module InProgressState =

        module DriverState =
            open SelfPractice.AkkaFsm.TripState.InProgressState.DriverState

            type DriverStateActorTests() =
                inherit TestKit()

                static member private StateTransitionData() : IEnumerable<obj[]> =
                    seq {
                        // Normal case
                        yield [| [| Driving, ArrivedAtDestination, Arrived, tripTriggerNone;
                                    Arrived, CompleteTrip, Completed, Some CompleteTrip |] |]

                        // Driving
                        yield [| [| Driving, Cancel, Completed, tripTriggerNone |] |]
                        yield [| [| Driving, ConfirmRequest , Driving, tripTriggerNone |] |]

                        // Arrived
                        yield [| [| Driving, ArrivedAtDestination, Arrived, tripTriggerNone;
                                    Arrived, Cancel, Completed, tripTriggerNone; |] |]
                        yield [| [| Driving, ArrivedAtDestination, Arrived, tripTriggerNone;
                                    Arrived, ConfirmRequest, Arrived, tripTriggerNone; |] |]

                    }
                [<Theory>]
                [<MemberData("StateTransitionData", MemberType=typeof<DriverStateActorTests>)>]
                member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
                    let props = DriverStateActor.Props(this.TestActor)
                    testTransition this props Driving triggerAndExpectedStates

        module RiderState =
            open SelfPractice.AkkaFsm.TripState.InProgressState.RiderState

            type RiderStateActorTests() =
                inherit TestKit()

                static member private StateTransitionData() : IEnumerable<obj[]> =
                    seq {
                        // Normal case
                        yield [| [| InCar, RiderLeft, LeftCar, tripTriggerNone;
                                    LeftCar, CompleteTrip, Completed, Some CompleteTrip |] |]

                        // Driving
                        yield [| [| InCar, Cancel, Completed, tripTriggerNone |] |]
                        yield [| [| InCar, ConfirmRequest , InCar, tripTriggerNone |] |]

                        // Arrived
                        yield [| [| InCar, RiderLeft, LeftCar, tripTriggerNone;
                                    LeftCar, Cancel, Completed, tripTriggerNone; |] |]
                        yield [| [| InCar, RiderLeft, LeftCar, tripTriggerNone;
                                    LeftCar, ConfirmRequest, LeftCar, tripTriggerNone; |] |]

                    }
                [<Theory>]
                [<MemberData("StateTransitionData", MemberType=typeof<RiderStateActorTests>)>]
                member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
                    let props = RiderStateActor.Props(this.TestActor)
                    testTransition this props InCar triggerAndExpectedStates

        open SelfPractice.AkkaFsm.TripState.InProgressState

        type InProgressStateStateActorTests() =
            inherit TestKit()

            static member private StateTransitionData() : IEnumerable<obj[]> =
                seq {
                    // Normal case
                    yield [| [| InProgress, ArrivedAtDestination, InProgress, tripTriggerNone;
                                InProgress, RiderLeft, InProgress,tripTriggerNone;
                                InProgress, CompleteTrip, Completed, Some CompleteTrip |] |]

                    // InProgress
                    yield [| [| InProgress, Cancel, Completed, tripTriggerNone |] |]
                    yield [| [| InProgress, ConfirmRequest , InProgress, tripTriggerNone |] |]
                }
            [<Theory>]
            [<MemberData("StateTransitionData", MemberType=typeof<InProgressStateStateActorTests>)>]
            member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
                let props = InprogressStateActor.Props(this.TestActor)
                testTransition this props InProgress triggerAndExpectedStates

    type TripStateActorTests() =
        inherit TestKit()

        static member private StateTransitionData() : IEnumerable<obj[]> =
            seq {
                    // Normal case
                    yield [| [| Created,        ConfirmRequest, Created,            tripTriggerNone;
                                Created,        AssignDriver,   DriverAssigned,     tripTriggerNone;
                                DriverAssigned, DriverArrived,  DriverAssigned,     tripTriggerNone;
                                DriverAssigned, StartTrip,      InProgress,         tripTriggerNone;
                                InProgress,     ArrivedAtDestination, InProgress,   tripTriggerNone;
                                InProgress,     RiderLeft,      InProgress,         tripTriggerNone;
                                InProgress,     CompleteTrip,   Completed,          tripTriggerNone |] |]

                    // Created
                    yield [| [| Created, Cancel, Completed, tripTriggerNone |] |]
                    yield [| [| Created, DriverArrived, Created, tripTriggerNone |] |] // 不相關 Trigger
                    yield [| [| Created, ConfirmRequest , Created, tripTriggerNone;
                                Created, Cancel, Completed, tripTriggerNone; |] |]
                    yield [| [| Created, ConfirmRequest , Created, tripTriggerNone;
                                Created, StartTrip, Created, tripTriggerNone; |] |] // 不相關 Trigger

                    // DriverAssigned
                    yield [| [| Created, ConfirmRequest, Created, tripTriggerNone
                                Created, AssignDriver, DriverAssigned, tripTriggerNone
                                DriverAssigned, Cancel, Completed, tripTriggerNone |] |]
                    yield [| [| Created, ConfirmRequest, Created, tripTriggerNone
                                Created, AssignDriver, DriverAssigned, tripTriggerNone
                                DriverAssigned, RiderLeft, DriverAssigned, tripTriggerNone |] |] // 不相關 Trigger

                    // InProgress
                    yield [| [| Created, ConfirmRequest, Created, tripTriggerNone;
                                Created, AssignDriver, DriverAssigned, tripTriggerNone;
                                DriverAssigned, DriverArrived, DriverAssigned, tripTriggerNone
                                DriverAssigned, StartTrip, InProgress, tripTriggerNone;
                                InProgress, Cancel, Completed, tripTriggerNone; |] |]
            }
        [<Theory>]
        [<MemberData("StateTransitionData", MemberType=typeof<TripStateActorTests>)>]
        member this.``test state transitions for various triggers`` (triggerAndExpectedStates: (State*TripStateTrigger*State*(TripStateTrigger option)) []) =
            let props = TripStateActor.Props()
            testTransition this props InProgress triggerAndExpectedStates



