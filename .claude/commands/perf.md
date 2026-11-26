---
description: Run a mobile performance audit against real device numbers
argument-hint: [optional: the symptom, e.g. "stutters at the start line"]
---

Performance concern: **$ARGUMENTS**

Use the `mobile-perf-audit` skill.

Start by establishing whether there is a measurement from a real Android device.
If there is not, getting one is step one — the editor on this M5 Pro will report
everything as fine regardless.

```bash
adb devices
adb shell dumpsys gfxinfo <package> framestats
```

Then work the order in `docs/PERFORMANCE.md` (baked lighting first, physics
almost last), check the known hot paths listed in the skill, and change one
thing at a time.

Report the device, build type, and before/after 90th-percentile frame time.
If you only reasoned about the source, say so explicitly.
