using UnityEngine;
using Apexline.Vehicle;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Apexline.Util
{
    /// <summary>
    /// On-screen driving telemetry. Exists because handling cannot be tuned by
    /// feel alone: "it understeers" is not actionable, but "1.4 g at 38 degrees
    /// of steer with 0.6 front slip" names the parameter to change.
    ///
    /// Deliberately IMGUI (OnGUI): zero setup, no canvas, no prefab wiring, and
    /// it cannot be confused for the real HUD that Day 6 builds. Toggle with F1.
    /// </summary>
    public class VehicleTelemetry : MonoBehaviour
    {
        public CarController car;
        [Tooltip("Only used on the legacy input backend; the Input System path is hard-wired to F1.")]
        public KeyCode toggleKey = KeyCode.F1;
        // Off by default: it is a debug overlay and it sat on top of the race
        // HUD in the first playable build. F1 brings it back.
        public bool visible = false;

        [Tooltip("Peak values are held this long so a transient is readable.")]
        public float peakHoldSeconds = 2f;

        float _lateralG, _longitudinalG;
        float _peakLateralG, _peakLateralAt;
        float _peakSpeed;
        Vector3 _prevVel;
        GUIStyle _style;

        void Awake()
        {
            if (car == null) car = FindFirstObjectByType<CarController>();
        }

        void FixedUpdate()
        {
            if (car == null || car.Body == null) return;

            Vector3 vel = car.Body.linearVelocity;
            Vector3 accel = (vel - _prevVel) / Time.fixedDeltaTime;
            _prevVel = vel;

            // Measured in the car's own frame, which is what a driver feels.
            _lateralG = Vector3.Dot(accel, car.transform.right) / 9.81f;
            _longitudinalG = Vector3.Dot(accel, car.transform.forward) / 9.81f;

            float absLat = Mathf.Abs(_lateralG);
            if (absLat > _peakLateralG || Time.time - _peakLateralAt > peakHoldSeconds)
            {
                _peakLateralG = absLat;
                _peakLateralAt = Time.time;
            }
            if (car.SpeedKph > _peakSpeed) _peakSpeed = car.SpeedKph;
        }

        void Update()
        {
            // UnityEngine.Input THROWS when activeInputHandler is set to the new
            // Input System, which is this project's configuration. Reading the
            // legacy class is not a fallback here, it is a crash every frame.
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) visible = !visible;
#else
            if (UnityEngine.Input.GetKeyDown(toggleKey)) visible = !visible;
#endif
        }

        void OnGUI()
        {
            if (!visible || car == null) return;

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8),
            };

            float tilt = Vector3.Angle(car.transform.up, Vector3.up);

            string text =
                $"<b>{car.SpeedKph:F0}</b> km/h    gear <b>{car.Gear}</b>    rpm {car.EngineRpm01 * 100f:F0}%\n" +
                $"peak speed      {_peakSpeed:F0} km/h\n" +
                $"lateral         {_lateralG:+0.00;-0.00} g   (peak {_peakLateralG:F2})\n" +
                $"longitudinal    {_longitudinalG:+0.00;-0.00} g\n" +
                $"slip  lat {car.MaxLateralSlip:F2}   fwd {car.MaxForwardSlip:F2}\n" +
                $"body tilt       {tilt:F1}°\n" +
                $"grounded        {(car.IsGrounded ? "yes" : "<color=#ff6666>NO</color>")}\n" +
                $"input  thr {car.Input.Throttle:F2}  brk {car.Input.Brake:F2}  steer {car.Input.Steer:+0.00;-0.00}" +
                (car.Input.Handbrake ? "  <color=#ffcc44>HANDBRAKE</color>" : "");

            var rect = new Rect(12f, 12f, 330f, 170f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, text, _style);

            GUI.Label(new Rect(12f, 186f, 330f, 20f),
                      $"<i>F1 hides this</i>", _style);
        }

        /// <summary>Clears the held peaks, for starting a fresh measurement run.</summary>
        public void ResetPeaks()
        {
            _peakLateralG = 0f;
            _peakSpeed = 0f;
        }
    }
}
