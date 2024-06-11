import { setup } from "xstate";

export const machine = setup({
    types: {
        context: {} as {},
        events: {} as
            | { type: "CANCEL" }
            | { type: "RIDER_LEFT" }
            | { type: "START_TRIP" }
            | { type: "ASSIGN_DRIVER" }
            | { type: "COMPLETE_TRIP" }
            | { type: "DRIVER_ARRIVED" }
            | { type: "CONFIRM_REQUEST" }
            | { type: "ARRIVED_AT_DESTINATION" },
    },
}).createMachine({
    context: {},
    id: "tripEntity",
    initial: "created",
    states: {
        created: {
            initial: "requestReceived",
            on: {
                CANCEL: {
                    target: "cancelled",
                },
            },
            states: {
                requestReceived: {
                    on: {
                        CONFIRM_REQUEST: {
                            target: "awaitingDriver",
                        },
                    },
                },
                awaitingDriver: {
                    on: {
                        ASSIGN_DRIVER: {
                            target: "#tripEntity.driverAssigned",
                        },
                    },
                },
            },
        },
        cancelled: {
            type: "final",
        },
        driverAssigned: {
            initial: "driverEnRoute",
            on: {
                CANCEL: {
                    target: "cancelled",
                },
            },
            states: {
                driverEnRoute: {
                    on: {
                        DRIVER_ARRIVED: {
                            target: "driverArrived",
                        },
                    },
                },
                driverArrived: {
                    on: {
                        START_TRIP: {
                            target: "#tripEntity.inProgress",
                        },
                    },
                },
            },
        },
        inProgress: {
            type: "parallel",
            on: {
                COMPLETE_TRIP: {
                    target: "completed",
                },
                CANCEL: {
                    target: "cancelled",
                },
            },
            states: {
                riderState: {
                    initial: "inCar",
                    states: {
                        inCar: {
                            on: {
                                RIDER_LEFT: {
                                    target: "leftCar",
                                },
                            },
                        },
                        leftCar: {
                            type: "final",
                        },
                    },
                },
                driverState: {
                    initial: "driving",
                    states: {
                        driving: {
                            on: {
                                ARRIVED_AT_DESTINATION: {
                                    target: "arrived",
                                },
                            },
                        },
                        arrived: {
                            type: "final",
                        },
                    },
                },
            },
        },
        completed: {
            type: "final",
        },
    },
});
