# AlphaTown — time and anti-cheat

Every gate in this game is a comparison against a clock. Crops, construction, upgrades,
production, order expiry, order slot cooldowns — all of them store an absolute completion
timestamp and ask "is it later than that yet?".

That design is what makes offline progression free. It is also what makes the clock the single
most valuable thing in the game to lie about.

---

## The threat

A player opens Settings, moves the device clock forward a year, and reopens the game. Every crop
is grown, every building is built, every production queue is done, every order slot has refilled.
The deed faucet — which paces land — empties as fast as they can tap.

This needs no tools, no rooted device and no technical knowledge. It is the single most common
exploit in the genre, and until this phase AlphaTown was completely open to it.

What we are *not* defending against here: a rooted device with a memory editor, or a modified
client. A client can always lie about its own state; that class of cheat is answered by
server-authoritative simulation, which is a different and much larger project.

---

## The mechanism

One sentence: **ask the device what time it is as rarely as possible.**

```
sync:   serverNow  ←──── backend
        monotonic₀ ←──── Stopwatch

later:  now = serverNow + (monotonic − monotonic₀)
```

A monotonic counter measures *elapsed* time without reference to the wall clock. Once a session
knows one authoritative instant, it can carry that instant forward indefinitely without asking
the device the time again.

So after a successful sync, changing the device clock does **nothing at all**. Not "is detected" —
does nothing, because the number that was moved is no longer an input.
`WhileSynchronised_MovingTheDeviceClockChangesNothing` is the test that pins it.

### Latency

The sample is corrected by half the round trip, on the standard assumption that the trip is
roughly symmetric. Crude, and good to well within a second — far tighter than any timer the game
gates on.

---

## Trust levels

`TimeTrust` is surfaced all the way up to `IGameClock`, so any system handing out real value on a
timer can ask.

| | When | Can the player move time? |
| --- | --- | --- |
| `Synchronized` | Synced this session | **No.** Time comes from the monotonic counter. |
| `Stale` | A previous session synced; running on the device clock plus the stored offset | Between sessions, yes |
| `Untrusted` | Never synced, or caught mid-session tampering | Between sessions, yes |

The stored offset is worth keeping even though it is spoofable: it corrects a device whose clock
is simply *wrong* — a flat battery, a factory reset, a phone bought in another timezone — which
is far more common than deliberate cheating.

---

## Offline behaviour

Being offline is a **supported state, not an error**. The game is playable on a plane.

1. **Never synced, offline.** Runs on the raw device clock. `Untrusted`.
2. **Synced before, offline now.** Device clock plus the stored offset. `Stale`.
3. **Offline mid-session.** Nothing changes — the monotonic counter needs no network, so a session
   that synced at launch stays `Synchronized` for as long as it runs.

In every case the session still advances on the monotonic counter after its first reading, so
**the device clock cannot be moved during play regardless of trust level.** The exposure is only
across a restart.

Falling back is logged plainly, at warning level, every time.

---

## The floor

The last time the game believed is persisted and restored as a lower bound.

This is as much a correctness guard as an anti-cheat one: without it, winding the clock backwards
would make already-finished timers un-finish, and a player could watch an order they completed
reappear.

A **server sample overrides the floor**. Without that, a session poisoned by a clock set to 2099
would keep that inflated floor forever and never recover, even once back online.
`AServerSampleClearsAPoisonedFloor` covers it.

---

## Suspend

The monotonic counter can stop while a device sleeps, depending on platform. On resume the clock
is re-based against the device clock — allowed to jump **forward** only, never backward — and a
sync is requested immediately.

That is the one place the device clock can still push time on during a session, so trust drops to
`Stale` until the sync lands. Honest, and visible in the logs.

---

## Clock jump detection

`PollDeviceDrift` compares the device clock against our own reckoning about once a second. Beyond
a five-minute tolerance — generous, because a legitimate NTP step can be minutes — it raises
`ClockJumpDetected`.

While `Synchronized` this is **pure signal**: the drift changes nothing because the device clock
is not what time is derived from. It is still worth knowing. Unsynchronised it is more than
signal, because the baseline came from that clock, so trust drops to `Untrusted`.

TODO(live-ops): forward this to analytics. A device clock that leaps mid-session is the clearest
tampering signal a client can produce about itself.

---

## What is still vulnerable

Stated plainly, because a security doc that only lists wins is worse than none.

1. **Fully offline long-term play.** A player who never connects can quit, move the clock forward,
   and relaunch. The session re-baselines from the device clock, and there is no way for a client
   with no network to know better. This is the residual hole, and it is inherent: elapsed time
   while a process is dead cannot be measured locally.

   It is bounded, though: the exploit costs an app restart per jump, and the moment they connect,
   the server sample corrects the clock and the floor.

2. **The `Date` header is not authoritative.** `HttpDateHeaderTimeProvider` reads the time from an
   ordinary HTTP response header. That defeats the actual threat — a player in Settings — but it
   is unsigned, so anyone able to redirect the request (a proxy, a hosts entry, a rooted device)
   can answer with any time they like.

3. **The client still owns its own state.** Nothing here stops a memory editor from writing a
   completion timestamp directly. Trusted time makes the *clock* honest, not the save file.

4. **Nothing is enforced on trust level yet.** `TimeTrust` is exposed but no system refuses to pay
   out on untrusted time. That is deliberate — refusing to run offline would be a worse game — but
   it means the flag is currently telemetry rather than a gate.

---

## Next hardening steps, in order of value

1. **A signed timestamp endpoint.** Your own backend returns the time plus an HMAC over it; the
   client verifies before accepting. Kills threat 2 outright, and only
   `HttpDateHeaderTimeProvider` changes — nothing else in the codebase knows how time arrives.
2. **Server-side validation of claimed progress.** On sync, send the last known time and the
   elapsed time the client believes; the backend rejects impossible jumps. Bounds threat 1 without
   requiring the player to be online to play.
3. **Refuse hard-currency and deed payouts on `Untrusted` time**, or hold them until the next
   sync. Turns the flag from telemetry into a gate, and is a small change once the flag is
   trusted.
4. **Server-authoritative timers for the things that matter.** Land deeds and hard currency only;
   crops and production can stay client-side, because faking them is bounded by the barn.
