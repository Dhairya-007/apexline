---
name: mobile-perf-audit
description: Investigate frame rate, stutter, draw calls, triangle count, GC spikes or physics cost in Apexline, and decide what to optimise on an Android device. Use when asked why the game is slow, janky, or dropping frames, or asked to optimise, profile, or hit 60 fps.
---

# Mobile performance audit

## The rule that overrides everything

**The editor on this machine lies.** An M5 Pro runs this at hundreds of fps with
every budget blown. An editor frame rate is not evidence. If there is no number
from a phone, the first step is getting one — not reading code.

```bash
adb devices
adb shell dumpsys gfxinfo <package> framestats
adb shell dumpsys gfxinfo <package> | head -40
```

Read the **90th and 99th percentile**, never the average. 60 fps average with a
99th percentile of 40 ms is a game that stutters in every corner, and that is
what players feel.

## Never optimise from inspection

The loop is: measure → change one thing → measure again. Guessing from source
reliably optimises the wrong thing, because mobile bottlenecks are almost always
draw calls and shadows rather than the C# that looks expensive.

## Order of attack

Full budget and detail in `docs/PERFORMANCE.md`. Summary, cheapest first:

1. **Bake the lighting** — larger than everything else combined
2. Occlusion culling and LODs (cull ~150 m)
3. Draw calls: SRP Batcher compatibility, GPU instancing, atlas materials
4. ASTC 6x6 textures, 1024 props / 2048 hero car
5. Simple Lit for props; keep clearcoat for cars only
6. Post-processing: keep bloom and motion blur, cut the rest
7. Physics — only after all of the above
8. GC: no per-frame allocation

## Known hot paths in this codebase

Recognise these rather than rediscovering them:

- **`CarController` calls `GetGroundHit` three times per wheel per step**
  (`ApplyAntiRoll`, `ApplyTractionControl`, `ReadSlipAndGrounding`). If physics
  shows up, cache one `WheelHit` per wheel per step first.
- **`WaypointPath.NearestIndex` is O(nodes)** and `Progress` calls it;
  `RaceManager.UpdateStandings` runs it per racer per frame. Six cars × 200 nodes
  = 1200 distance checks a frame. Fix by searching outward from the racer's last
  known node.
- **`AIDriver.ComputeTarget` walks the path** each physics step.
- **Realtime reflection probes on cars** will not fit the budget. Bake per region.

## Reporting

Never claim an improvement without: the device, the build type, and before/after
90th-percentile frame time. "Should be faster" and "feels smoother" are not
results. If you only reasoned about the code, say that is what you did.
