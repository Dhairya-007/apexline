using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Apexline.Vehicle;

namespace Apexline.Tests.PlayMode
{
    /// <summary>
    /// Objective handling benchmarks. Handling is ultimately subjective, but a
    /// great deal of it is not: acceleration, braking distance, peak lateral
    /// grip and spin resistance are numbers, and a car that is wrong on those
    /// cannot be rescued by tuning that only addresses feel.
    ///
    /// Every test logs its measurement with a BENCH| prefix so a run produces a
    /// comparable table. Assertions are wide sanity bounds, not targets - they
    /// exist to catch a regression that makes the car undrivable, while leaving
    /// the actual tuning decisions to a human who has driven it.
    ///
    /// Reference figures for a ~1300 kg rear-drive road car:
    ///   0-100 km/h        4-9 s
    ///   100-0 braking     35-55 m
    ///   peak lateral      0.9-1.3 g
    /// </summary>
    public class HandlingBenchmarkTests
    {
        CarController _car;
        const float Settle = 1.0f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TestTrack_Flat", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _car = Object.FindFirstObjectByType<CarController>();
            Assert.IsNotNull(_car, "no CarController in TestTrack_Flat");

            var driver = _car.GetComponent<PlayerDriver>();
            if (driver != null) driver.enabled = false;
            _car.Input = CarInput.Idle;

            yield return new WaitForSeconds(Settle);
        }

        static void Bench(string metric, float value, string unit)
            => Debug.Log($"BENCH|{metric}|{value:F2}|{unit}");

        /// <summary>
        /// Guards every benchmark against the car having left the ground plane.
        /// Free fall accelerates indefinitely, so an off-the-edge run silently
        /// produces plausible-looking but meaningless numbers.
        /// </summary>
        void AssertStillOnTrack()
        {
            Assert.Greater(_car.transform.position.y, -5f,
                $"car left the ground plane (y={_car.transform.position.y:F1}); " +
                "measurement is free fall, not handling");
        }

        /// <summary>Holds an input until the predicate is true or the timeout expires.</summary>
        IEnumerator HoldUntil(CarInput input, System.Func<bool> done, float timeout)
        {
            float t = 0f;
            while (t < timeout && !done())
            {
                _car.Input = input;
                t += Time.deltaTime;
                yield return null;
            }
            _car.Input = CarInput.Idle;
        }

        // ------------------------------------------------------------- straight
        [UnityTest]
        public IEnumerator Bench_ZeroToHundred()
        {
            float start = Time.time;
            yield return HoldUntil(new CarInput { Throttle = 1f },
                                   () => _car.SpeedKph >= 100f, 30f);

            if (_car.SpeedKph < 100f)
            {
                Bench("0-100kph", -1f, "s (NEVER REACHED)");
                Assert.Fail($"never reached 100 km/h in 30 s (peaked near {_car.SpeedKph:F1})");
            }

            AssertStillOnTrack();
            float elapsed = Time.time - start;
            Bench("0-100kph", elapsed, "s");

            Assert.Greater(elapsed, 2.0f, "0-100 under 2 s is unrealistically violent");
            Assert.Less(elapsed, 15.0f, "0-100 over 15 s feels broken, not slow");
        }

        [UnityTest]
        public IEnumerator Bench_TopSpeed()
        {
            yield return HoldUntil(new CarInput { Throttle = 1f }, () => false, 25f);
            AssertStillOnTrack();
            Bench("top-speed-25s", _car.SpeedKph, "kph");

            Assert.Greater(_car.SpeedKph, 120f, "car cannot reach a plausible speed");
            Assert.LessOrEqual(_car.SpeedKph, _car.maxSpeedKph + 5f,
                "exceeded its own declared maxSpeedKph");
        }

        [UnityTest]
        public IEnumerator Bench_BrakingDistanceFromHundred()
        {
            yield return HoldUntil(new CarInput { Throttle = 1f },
                                   () => _car.SpeedKph >= 100f, 30f);
            Assume.That(_car.SpeedKph, Is.GreaterThan(95f), "could not reach 100 km/h to brake from");

            Vector3 from = _car.transform.position;
            yield return HoldUntil(new CarInput { Brake = 1f },
                                   () => _car.SpeedKph < 5f, 15f);

            AssertStillOnTrack();
            float distance = Vector3.Distance(from, _car.transform.position);
            Bench("100-0-braking", distance, "m");

            Assert.Greater(distance, 10f, "stopping in under 10 m from 100 km/h is not physical");
            Assert.Less(distance, 120f, "takes over 120 m to stop; brakes are far too weak");
        }

        // -------------------------------------------------------------- lateral
        [UnityTest]
        public IEnumerator Bench_PeakLateralGrip()
        {
            // Get up to a steady speed, then hold full lock and watch the
            // lateral acceleration the tyres can actually sustain.
            yield return HoldUntil(new CarInput { Throttle = 1f },
                                   () => _car.SpeedKph >= 70f, 30f);
            Assume.That(_car.SpeedKph, Is.GreaterThan(60f));

            float peak = 0f;
            Vector3 prevVel = _car.Body.linearVelocity;
            float t = 0f;

            while (t < 6f)
            {
                _car.Input = new CarInput { Throttle = 0.35f, Steer = 1f };
                yield return new WaitForFixedUpdate();

                Vector3 vel = _car.Body.linearVelocity;
                Vector3 accel = (vel - prevVel) / Time.fixedDeltaTime;
                // Lateral component in the car's own frame.
                float lat = Mathf.Abs(Vector3.Dot(accel, _car.transform.right));
                if (lat > peak) peak = lat;

                prevVel = vel;
                t += Time.fixedDeltaTime;
            }
            _car.Input = CarInput.Idle;

            AssertStillOnTrack();
            float g = peak / 9.81f;
            Bench("peak-lateral", g, "g");

            Assert.Greater(g, 0.4f, "under 0.4 g of lateral grip: the car cannot corner");
            Assert.Less(g, 3.0f, "over 3 g is not a road car, it is a glitch");
        }

        [UnityTest]
        public IEnumerator Bench_TurningCircleAtLowSpeed()
        {
            // Full lock at a crawl: measures the geometric turning circle, which
            // is set by maxSteerAngleLowSpeed and the wheelbase.
            yield return HoldUntil(new CarInput { Throttle = 0.4f },
                                   () => _car.SpeedKph >= 18f, 20f);
            Assume.That(_car.SpeedKph, Is.GreaterThan(12f));

            float heading0 = _car.transform.eulerAngles.y;
            Vector3 p0 = _car.transform.position;
            float turned = 0f;
            float prev = heading0;
            float t = 0f;

            while (t < 12f && turned < 170f)
            {
                _car.Input = new CarInput { Throttle = 0.25f, Steer = 1f };
                yield return null;
                float now = _car.transform.eulerAngles.y;
                turned += Mathf.Abs(Mathf.DeltaAngle(prev, now));
                prev = now;
                t += Time.deltaTime;
            }
            _car.Input = CarInput.Idle;

            // Half a turn means the car is now a diameter away from where it began.
            float diameter = Vector3.Distance(p0, _car.transform.position);
            Bench("turn-degrees-in-12s", turned, "deg");
            if (turned >= 170f) Bench("turning-circle-diameter", diameter, "m");

            Assert.Greater(turned, 60f,
                $"only turned {turned:F0} degrees at full lock in 12 s; steering is far too slow");
        }

        // ------------------------------------------------------------- stability
        [UnityTest]
        public IEnumerator Stability_DoesNotSpinOnPowerFromStandstill()
        {
            // Accumulated, not DeltaAngle against the start: DeltaAngle saturates
            // at 180, so a deliberate half-turn and a full spin read identically.
            float prev = _car.transform.eulerAngles.y;
            float total = 0f;
            float t = 0f;

            while (t < 5f)
            {
                _car.Input = new CarInput { Throttle = 1f, Steer = 0.6f };
                yield return null;
                float now = _car.transform.eulerAngles.y;
                total += Mathf.Abs(Mathf.DeltaAngle(prev, now));
                prev = now;
                t += Time.deltaTime;
            }
            _car.Input = CarInput.Idle;

            float worst = total;
            Bench("yaw-on-power-5s", worst, "deg");

            // A deliberate turn is expected; a snap spin is not.
            Assert.Less(worst, 330f,
                $"car spun {worst:F0} degrees under power with partial steer - traction control or counter-steer assist is too weak");
        }

        [UnityTest]
        public IEnumerator Stability_DoesNotRollOverUnderHardSteer()
        {
            yield return HoldUntil(new CarInput { Throttle = 1f },
                                   () => _car.SpeedKph >= 90f, 30f);
            Assume.That(_car.SpeedKph, Is.GreaterThan(80f));

            float worstTilt = 0f;
            float t = 0f;
            while (t < 5f)
            {
                _car.Input = new CarInput { Throttle = 0.5f, Steer = 1f };
                yield return null;
                float tilt = Vector3.Angle(_car.transform.up, Vector3.up);
                if (tilt > worstTilt) worstTilt = tilt;
                t += Time.deltaTime;
            }
            _car.Input = CarInput.Idle;

            Bench("peak-body-tilt", worstTilt, "deg");

            Assert.Less(worstTilt, 45f,
                $"body reached {worstTilt:F0} degrees of tilt - anti-roll bars are too soft or the centre of mass is too high");
        }

        [UnityTest]
        public IEnumerator Stability_RecoversFromHandbrakeSlide()
        {
            yield return HoldUntil(new CarInput { Throttle = 1f },
                                   () => _car.SpeedKph >= 70f, 30f);
            Assume.That(_car.SpeedKph, Is.GreaterThan(60f));

            // Provoke a slide, then ask whether it is catchable.
            yield return HoldUntil(new CarInput { Throttle = 0.3f, Steer = 0.8f, Handbrake = true },
                                   () => false, 1.5f);
            yield return HoldUntil(new CarInput { Throttle = 0.4f }, () => false, 3f);

            float slip = _car.MaxLateralSlip;
            Bench("slip-after-recovery", slip, "ratio");

            Assert.IsTrue(_car.IsGrounded, "car left the ground during a handbrake slide");
            Assert.Less(Vector3.Angle(_car.transform.up, Vector3.up), 30f,
                "car did not stay upright through a handbrake slide");
            Assert.Less(slip, 0.9f,
                $"still sliding at {slip:F2} three seconds after releasing the handbrake - the car is not recoverable");
        }
    }
}
