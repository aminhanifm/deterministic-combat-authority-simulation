# Deterministic Combat Authority Simulation

This is a small C# console project I put together to work through the mechanics of server-authoritative combat with client-side prediction. The idea was to keep things simple - two players, a couple of abilities, a fixed tick loop - and focus on getting the authority model, prediction, and reconciliation right rather than building anything large.

## Building and Running

```
dotnet build
dotnet run
```

You'll need the .NET 8.0 SDK.

---

## How It's Structured

The codebase intentionally keeps a flat structure to avoid over-engineering
and keep the deterministic simulation easy to inspect.

```
┌──────────────────────────────────────────────────────────────┐
│                        Program.cs                            │
│                   Wires everything together                  │
├──────────┬───────────┬──────────────┬────────────────────────┤
│  Client  │  Client   │    Server    │     ReplayRunner       │
│  (P0)    │  (P1)     │ (authority)  │  (replays input log)   │
├──────────┴───────────┴──────────────┴────────────────────────┤
│                     Simulation.Step()                        │
│            Pure function - same inputs, same outputs         │
├──────────────────────────────────────────────────────────────┤
│     PlayerState / GameState / PlayerInput / Constants        │
│                      Value types                             │
└──────────────────────────────────────────────────────────────┘
```

`SimulationConstants.cs` has all the timing values and tuning knobs. `PlayerState.cs` is a struct that holds position, velocity, stamina, and the combat state machine. `GameState.cs` wraps two player states and a tick counter. `PlayerInput.cs` is just a tagged input with a player ID and tick number.

`Simulation.cs` is the core - a pure static function that takes a game state and some inputs and returns the next state. No side effects, no randomness. That's what makes replay work.

`Server.cs` runs the authoritative simulation. It queues up inputs, handles late arrivals by restamping them to the current tick, and keeps a history of past states for reconciliation lookups.

`Client.cs` does prediction. It runs the same `Simulation.Step()` locally when you press a button so things feel responsive, then compares against the server later and corrects if needed.

`ReplayRunner.cs` takes a recorded input log, runs it through a fresh simulation, and checks if the final state matches the original run.

`Program.cs` scripts a specific conflict scenario, runs the tick loop, and prints a tick-by-tick table with columns for server state, client predictions, and an Event column that flags noteworthy moments - input actions, state transitions, late arrivals, and client corrections.

---

## The Authority Model

The server is the single source of truth. Every tick it pulls pending inputs out of a queue, feeds them into the simulation step, and stores the result. If an input arrives late (because of network delay), it gets restamped to the current server tick. If the player is already in a state that doesn't accept that input (say, they're in hitstun), the state machine just ignores it naturally.

Clients run their own copy of the simulation for prediction. When you press dodge, the client applies it immediately - you don't want to wait 130ms for the server to confirm. But the client also keeps a history of what it predicted at each tick. When the server state comes back, it compares. If they match, great. If not, it corrects.

---

## Combat States

Each player has a finite state machine:

```
              ┌───────────────────────────────────────┐
              │                                       │
              ▼                                       │
  ┌────────────────┐   LightAttack   ┌─────────┐      │
  │      Idle      │───────────────► │ Windup  │      │
  │                │                 │  12t    │      │
  └────────────────┘                 └────┬────┘      │
       │                                  │ expires   │
       │  Dodge                           ▼           │
       │              ┌─────────┐   ┌─────────┐       │
       └────────────► │  Dodge  │   │ Active  │       │
                      │  21t    │   │   6t    │       │
                      │ S=3     │   └────┬────┘       │
                      │ I=6     │        │ expires    │
                      │ R=12    │        ▼            │
                      └────┬────┘   ┌──────────┐      │
                           │        │ Recovery │      │
                           │        │   18t    │      │
                           │        └────┬─────┘      │
                           │             │            │
                           └─────────────┴────────────┘
                                 expires → Idle

  Hit during Active → opponent enters Hitstun (18t) → Idle
```

Dodge has three sub-phases: startup (3 ticks), invulnerability (6 ticks), recovery (12 ticks). You can only avoid a hit during the invulnerability window. Everything else is straightforward - light attack goes through windup, active (the hit window), and recovery.

All timing is in ticks, not real time. At 60Hz, 12 ticks is 200ms, 6 ticks is 100ms, etc. This is what keeps things deterministic.

|    Ability   |      Phase      |  ms  | Ticks |
|--------------|-----------------|------|-------|
| Light Attack |      Windup     |  200 |   12  |
| Light Attack |      Active     |  100 |   6   |
| Light Attack |     Recovery    |  300 |   18  |
| Dodge        |     Startup     |   50 |   3   |
| Dodge        | Invulnerability |  100 |   6   |
| Dodge        |     Recovery    |  200 |   12  |
|      -       |     Hitstun     |  300 |   18  |

---

## Reconciliation

When the server state comes in, the client checks what it predicted at that tick. There are a few possible outcomes:

**Match** - predicted state equals server state. Nothing to do.

**Snap** - the combat states are different (e.g., client thinks we're dodging but server says we're in hitstun). This is a hard divergence. The client overwrites its state with the server's and replays any inputs it had buffered after that point.

**Rollback** - the combat state is the same but something else is off, like stamina or a hit flag. Client rewinds to the server state and replays forward.

**Blend** - only the position is slightly off (under 0.5 units). The client interpolates toward the correct position instead of snapping, so it doesn't look jarring.

For anything else, it falls back to snap.

---

## The Conflict Scenario

The whole point of the demo is to show what happens when latency causes a disagreement.

Player A throws a light attack at tick 10. That arrives at the server immediately. Player B tries to dodge at tick 15, but because of ~133ms of simulated latency, the server doesn't see that input until tick 24.

Here's what plays out:

- Ticks 10–21: Player A is in windup on the server. 
- Tick 15: Client B predicts the dodge locally - the client shows dodge happening.
- Tick 16: Server state comes back. Server says Player B is still idle (the dodge input hasn't arrived yet). Client B gets a SNAP correction - combat state mismatch.
- Tick 22: Player A enters the active phase. Player B is idle on the server. They're in range. The attack connects. Player B enters hitstun.
- Tick 24: Player B's dodge input finally reaches the server, but Player B is in hitstun now, so the state machine ignores it.

The server decided the attack landed. The client's optimistic dodge prediction got corrected. That's the whole authority model in action.

---

## Replay

After the scenario finishes, the program takes every input that the server actually processed (with their actual processing ticks, including restamped ones) and runs them through a clean simulation from the initial state. If the final state matches exactly, it prints `REPLAY PASS`. That's the determinism proof - same inputs, same order, same result.

---

## Why Certain Things Are the Way They Are

I used a struct for `PlayerState` so copying is cheap and comparison is straightforward. The simulation step is a pure function with no side effects, which is the whole basis for replay working. Position is 1D because that's all you need to show range-based hit detection without getting into floating-point headaches from 2D/3D math. Inputs during non-idle states just get dropped - no input buffering - which keeps the state machine simple.
