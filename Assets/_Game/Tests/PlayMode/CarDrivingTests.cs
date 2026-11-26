using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Apexline.Vehicle;

namespace Apexline.Tests.PlayMode
{
    /// <summary>
    /// Does the car actually drive? Everything else about the vehicle can be
    /// verified statically, but "throttle makes it move forward and stay upright"
    /// only holds once physics has run, and it is the single most important
    /// property in the project.
    ///
    /// The tests drive CarInput directly with PlayerDriver disabled, so a
    /// failure means the physics is wrong rather than that input was misread.
    /// </summary>
    public class CarDrivingTests
    {
        CarController _car;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("TestTrack_Flat", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _car = Object.FindFirstObjectByType<CarController>();
            Assert.IsNotNull(_car, "no CarController in TestTrack_Flat");

            // Input comes from the test, not from a keyboard that is not there.
            var driver = _car.GetComponent<PlayerDriver>();
            if (driver != null) driver.enabled = false;
            _car.Input = CarInput.Idle;

            // Let the suspension settle before measuring anything.
            yield return new WaitForSeconds(1.0f);
        }

        IEnumerator Hold(CarInput input, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                _car.Input = input;
                t += Time.deltaTime;
                yield return null;
            }
            _car.Input = CarInput.Idle;
        }

        [UnityTest]
        public IEnumerator Car_SettlesOnItsWheels_AndDoesNotFallThrough()
        {
            yield return null;

            Assert.Greater(_car.transform.position.y, -1f,
                "car fell through the ground");
            Assert.Less(_car.transform.position.y, 2f,
                "car was launched into the air by the suspension");
            Assert.IsTrue(_car.IsGrounded,
                "car is not reporting contact with the ground");
            Assert.Less(Vector3.Angle(_car.transform.up, Vector3.up), 10f,
                "car did not settle upright");
        }

        [UnityTest]
        public IEnumerator Throttle_MovesTheCarForward()
        {
            Vector3 start = _car.transform.position;
            Vector3 forward = _car.transform.forward;

            yield return Hold(new CarInput { Throttle = 1f }, 3f);

            Vector3 travel = _car.transform.position - start;
            float alongForward = Vector3.Dot(travel, forward);

            Assert.Greater(alongForward, 5f,
                $"only travelled {alongForward:F2} m forward in 3 s under full throttle");
            Assert.Greater(_car.SpeedKph, 15f,
                $"speed only reached {_car.SpeedKph:F1} km/h");
            Assert.Less(Vector3.Angle(_car.transform.up, Vector3.up), 20f,
                "car rolled over while accelerating in a straight line");
        }

        [UnityTest]
        public IEnumerator Brake_SlowsTheCarDown()
        {
            yield return Hold(new CarInput { Throttle = 1f }, 3f);
            float before = _car.SpeedKph;
            Assert.Greater(before, 15f, "car never got up to speed, cannot test braking");

            yield return Hold(new CarInput { Brake = 1f }, 1.5f);

            Assert.Less(_car.SpeedKph, before * 0.7f,
                $"braking only took {before:F1} to {_car.SpeedKph:F1} km/h");
        }

        [UnityTest]
        public IEnumerator Steering_ChangesHeading()
        {
            yield return Hold(new CarInput { Throttle = 1f }, 2f);

            float headingBefore = _car.transform.eulerAngles.y;
            yield return Hold(new CarInput { Throttle = 0.5f, Steer = 1f }, 2f);
            float delta = Mathf.DeltaAngle(headingBefore, _car.transform.eulerAngles.y);

            Assert.Greater(delta, 10f,
                $"steering right only changed heading by {delta:F1} degrees");
        }

        [UnityTest]
        public IEnumerator Steering_IsNotInverted()
        {
            yield return Hold(new CarInput { Throttle = 1f }, 2f);

            float h0 = _car.transform.eulerAngles.y;
            yield return Hold(new CarInput { Throttle = 0.5f, Steer = -1f }, 2f);
            float delta = Mathf.DeltaAngle(h0, _car.transform.eulerAngles.y);

            // Negative steer must yaw left, i.e. a negative change in Y euler.
            Assert.Less(delta, -10f,
                $"steering left changed heading by {delta:F1} degrees - wheel order or sign is inverted");
        }

        [UnityTest]
        public IEnumerator EngineTelemetry_RespondsToSpeed()
        {
            yield return Hold(new CarInput { Throttle = 1f }, 3f);

            Assert.Greater(_car.EngineRpm01, 0.1f, "RPM did not rise under throttle");
            Assert.LessOrEqual(_car.EngineRpm01, 1f, "RPM exceeded its normalised range");
            Assert.Greater(_car.Gear, 0, "gear should be at least 1");
            Assert.LessOrEqual(_car.Gear, 6, "gear exceeded the 6-speed range");
        }
    }
}
