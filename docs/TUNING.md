# Tuning guide

Handling is the part of a racing game that decides whether people keep playing.
It is also subjective, so this guide exists to turn "feels wrong" into "this
parameter is wrong".

Every parameter below is on `CarController`
([`CarController.cs`](../Assets/_Game/Scripts/Vehicle/CarController.cs)) unless
stated otherwise.

## Method

1. **Reproduce it first.** Note speed, corner and input method. A problem that
   only happens above 180 km/h is a downforce or steering-falloff problem; the
   same symptom under 60 km/h is a friction or anti-roll problem.
2. **Change one parameter.** Two at once and you learn nothing.
3. **Move in meaningful steps.** ±20%, not ±2%. If 20% changes nothing, that is
   not the parameter.
4. **Drive the same reference lap** and record the time. A change that improves
   feel but costs two seconds a lap needs a conscious decision, not a shrug.
5. **Write the before/after in the commit.** See `CONTRIBUTING.md`.

## Symptom → parameter

| Symptom | Most likely cause | Try |
|---|---|---|
| Will not turn in (understeer) at all speeds | Front grip too low | Raise front `sidewaysFriction.stiffness` to 2.4–2.6 |
| Understeers only at high speed | Steering limit falls off too aggressively | Raise `maxSteerAngleHighSpeed` toward 10–12, or raise `steerFalloffSpeedKph` |
| Understeers only under power | Rear traction dominating | Lower `tractionControl` toward 0.25 |
| Rear steps out and cannot be caught | Counter-steer assist too weak | Raise `counterSteerAssist` toward 0.5 |
| Rear steps out suddenly, no warning | Rear friction curve too peaky | Raise rear `asymptoteValue` to ~0.85 so grip decays gently |
| Spins on throttle out of slow corners | Too much torque at low speed | Lower the first `powerCurve` keyframe below 0.75 |
| Rolls over or lifts an inside wheel | Anti-roll too soft, or CoM too high | Raise `antiRollFront`/`antiRollRear` by 30%; check `centerOfMassOffset.y` is ≈ -0.30 |
| Floaty and nervous at top speed | Not enough downforce | Raise `downforceAtTopSpeed` toward 8000–9000 |
| Bottoms out or scrapes over kerbs | Suspension too soft or too short | Raise `WheelCollider` spring to ~40000, or `suspensionDistance` to 0.30 |
| Bounces after kerbs | Damper too low for the spring | Raise damper to ~5500 |
| Steering feels laggy or vague | Slew rate too low, or input smoothing too high | Raise `steerRateDegPerSec` toward 200; lower `PlayerDriver.steerSmoothing` toward 0.08 |
| Steering feels twitchy on a touchscreen | Not enough smoothing, or deadzone too small | Raise `PlayerDriver.steerSmoothing` to 0.16; raise `tiltDeadzoneDegrees` to 3 |
| Brakes lock and the car will not rotate | Brake bias too far forward | Lower `brakeBiasFront` toward 0.55 |
| Car will not slow down in time | Brake torque too low | Raise `brakeTorque` toward 5000 |
| Top speed reached far too quickly | Power curve too flat at speed | Lower the last `powerCurve` keyframe below 0.25 |

## Reference values

The shipped defaults, for reverting after an experiment goes sideways.

```
mass 1300                         centerOfMassOffset (0, -0.30, 0)
maxMotorTorque 2500               maxSpeedKph 260
brakeTorque 4000                  handbrakeTorque 6000
brakeBiasFront 0.62
maxSteerAngleLowSpeed 28          maxSteerAngleHighSpeed 8
steerFalloffSpeedKph 220          steerRateDegPerSec 160
counterSteerAssist 0.35           tractionControl 0.40
antiRollFront 9000                antiRollRear 7000
downforceAtTopSpeed 6500

WheelCollider:
  suspensionDistance 0.25  spring 35000  damper 4500  targetPosition 0.5
  forwardFriction   extremumSlip 0.40 value 1.00  asymptoteSlip 0.80 value 0.75  stiffness 2.0
  sidewaysFriction  extremumSlip 0.25 value 1.00  asymptoteSlip 0.50 value 0.75  stiffness 2.2

PlayerDriver:
  steerSmoothing 0.12  tiltFullLockDegrees 22  tiltDeadzoneDegrees 2  autoThrottle true
```

## Design intent

This is **arcade with weight**, not simulation. Decisions that follow from that:

- **Assists are not cheating, they are the design.** On a touchscreen the player
  has no force feedback and roughly two bits of steering precision. The
  counter-steer assist and the speed-sensitive limit are what make a slide
  catchable at all.
- **Auto-throttle is the default** because a phone has no analogue trigger, and
  holding a virtual pedal while steering by tilt is not a thing people can do.
- **The gearbox is fake.** `Gear` and `EngineRpm01` exist to drive audio and the
  HUD. Do not build shift logic on them expecting physical meaning.
- **Faster is not better.** If a change makes the car quicker but less
  controllable, it is a worse change. Lap time is a tiebreaker, not the goal.

## AI tuning

`AIDriver` has its own parameters, and AI that feels wrong is usually one of two
things:

| Symptom | Try |
|---|---|
| AI brakes too late and runs wide | Raise `cornerBrakeAggression` toward 1.7 |
| AI crawls through corners | Lower `cornerBrakeAggression` toward 1.1, or raise per-node `speedLimit` |
| AI weaves on straights | Raise `minLookahead` toward 9 |
| AI cuts corners | Lower `lookaheadPerSpeed` toward 0.5, or add nodes to tighten the path |
| AI train, nobody overtakes | Widen pace jitter in `Awake`, or raise `avoidStrength` |
| AI rams the player | Raise `avoidDistance` toward 18 |

Per-node `halfWidth` and `speedLimit` on `WaypointPath` are usually the better
fix for a corner-specific problem. Global parameters are for global problems.
