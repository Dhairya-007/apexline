# Performance

Target: **60 fps on a mid-range Android phone**, which means a 16.6 ms frame.

## The rule that matters most

**Your development machine lies to you.** An M5 Pro will run this at hundreds of
frames per second with every budget below blown. The editor frame rate carries
no information about the target device. Every number in this document must come
from a phone.

## Budget

| Resource | Budget | Why |
|---|---|---|
| Frame time | < 16.6 ms | 60 fps |
| Visible triangles | < 150k | Mid-range mobile GPUs fall off a cliff above this |
| Draw calls / SetPass | < 120 | Mobile drivers are call-rate bound before fill bound |
| Realtime shadow cascades | 1 | Each cascade is a full extra scene render |
| Realtime lights | 1 directional | Everything else baked |
| Realtime reflection probes | 0 | Bake them; box-project for the car |
| Texture compression | ASTC 6x6 | Half the memory of 4x4, no visible loss at phone DPI |
| Physics fixed timestep | 0.02 (50 Hz) | 0.01 doubles physics cost for no felt benefit |
| Active rigidbodies | ≤ 8 | Six cars plus headroom |

## Measuring on device

```bash
adb devices                                      # confirm the device is attached
adb logcat -c                                    # clear, so the log is readable
adb logcat -s Unity                              # Unity's own log

# Frame timing. Run a lap, then read it:
adb shell dumpsys gfxinfo <package> framestats
adb shell dumpsys gfxinfo <package> | head -40   # percentiles and jank count
```

Read the **90th and 99th percentile**, not the average. A 60 fps average with a
99th percentile of 40 ms is a game that stutters every corner, and players feel
the stutter rather than the average.

For attribution rather than totals, use the Unity Profiler over USB with
**Development Build + Autoconnect Profiler**, and the Frame Debugger for draw
call breakdown. Note that a development build is itself slower — use it to find
*what* is expensive, and a release build to decide *whether* it still is.

## Order of attack

Cheapest wins first. Do not start at the bottom.

1. **Bake the lighting.** Nothing else comes close. Realtime shadows on a phone
   cost more than every other item here combined.
2. **Occlusion culling + LODs.** Bake occlusion; LOD0/1/2 on every prop with a
   cull distance around 150 m.
3. **Cut draw calls.** SRP Batcher compatibility (no per-renderer material
   property divergence), GPU instancing on repeated props, one material per
   atlas rather than one per prop.
4. **Texture settings.** ASTC 6x6, mip maps on, max size 1024 for props and 2048
   only for the hero car.
5. **Shader cost.** URP Lit is not free. Use Simple Lit for props and keep the
   expensive clearcoat path for the cars.
6. **Post-processing.** Bloom and motion blur are the costly two. Keep them; cut
   depth of field, SSAO quality to Medium, and never add temporal AA on mobile.
7. **Physics.** Only after the above. Keep the timestep at 0.02 and keep
   `WheelCollider` count at four per car.
8. **GC pressure.** No per-frame allocation. `FindObjectsByType` belongs in
   `Awake`, never in `Update` — `RaceManager` calls it once, deliberately.

## Traps specific to this project

- **`WheelCollider.GetGroundHit` is not free.** `CarController` calls it in
  `ApplyAntiRoll`, `ApplyTractionControl` and `ReadSlipAndGrounding` — three
  times per wheel per physics step. If physics shows up in a profile, cache one
  `WheelHit` per wheel per step before anything else.
- **`WaypointPath.NearestIndex` is O(n) over every node** and `Progress` calls
  it. `RaceManager.UpdateStandings` runs it per racer per frame. With six cars
  and 200 nodes that is 1200 distance checks a frame. If it appears in a
  profile, start the search from the racer's last known node instead.
- **`AIDriver.ComputeTarget` walks the path** each physics step. Bounded by node
  count, so a very dense path makes AI expensive.
- **Reflection probes on cars** are tempting and will not fit the budget as
  realtime. Bake them per track region.

## Before claiming an improvement

State the device, the build type, and the before/after 90th-percentile frame
time. "It feels smoother" is not a measurement, and on a machine this fast it is
not even an observation.
