using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Apexline.Vehicle
{
    /// <summary>
    /// Turns human input into CarInput. Supports gamepad/keyboard for fast
    /// desktop iteration and tilt or on-screen steering for touch, with
    /// auto-throttle on by default (the Real Racing / F1 Mobile convention -
    /// a virtual stick is a bad default on a phone).
    ///
    /// Compiles with or without the Input System package installed.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class PlayerDriver : MonoBehaviour
    {
        public enum TouchSteering { Tilt, OnScreenWheel }

        [Header("Mode")]
        public bool useTouchControls = Application.isMobilePlatform;
        public TouchSteering touchSteering = TouchSteering.Tilt;

        [Header("Assists")]
        [Tooltip("Throttle is held down automatically; the player only brakes.")]
        public bool autoThrottle = true;

        [Header("Tilt")]
        [Tooltip("Device roll in degrees that equals full lock.")]
        public float tiltFullLockDegrees = 22f;
        public float tiltDeadzoneDegrees = 2f;

        [Header("Smoothing")]
        [Tooltip("Seconds to reach full steering lock. Higher feels heavier.")]
        public float steerSmoothing = 0.12f;

        /// <summary>Set by the on-screen steering widget, range -1..1.</summary>
        [HideInInspector] public float onScreenSteer;
        /// <summary>Set by the on-screen brake button, range 0..1.</summary>
        [HideInInspector] public float onScreenBrake;
        [HideInInspector] public bool onScreenHandbrake;

        CarController _car;
        float _smoothedSteer;
        float _steerVelocity;

        void Awake() => _car = GetComponent<CarController>();

        void Update()
        {
            float rawSteer = useTouchControls ? ReadTouchSteer() : ReadDeviceSteer();

            _smoothedSteer = Mathf.SmoothDamp(
                _smoothedSteer, Mathf.Clamp(rawSteer, -1f, 1f),
                ref _steerVelocity, steerSmoothing);

            ReadPedals(out float throttle, out float brake, out bool handbrake);

            _car.Input = new CarInput
            {
                Steer = _smoothedSteer,
                Throttle = throttle,
                Brake = brake,
                Handbrake = handbrake
            };
        }

        float ReadTouchSteer()
        {
            if (touchSteering == TouchSteering.OnScreenWheel) return onScreenSteer;

            // Tilt: use gravity's x component, which is orientation-independent
            // enough for landscape play and needs no calibration step.
            Vector3 gravity = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Accelerometer.current != null)
            {
                UnityEngine.InputSystem.InputSystem.EnableDevice(
                    UnityEngine.InputSystem.Accelerometer.current);
                gravity = UnityEngine.InputSystem.Accelerometer.current.acceleration.ReadValue();
            }
#else
            gravity = UnityEngine.Input.acceleration;
#endif

            float degrees = Mathf.Asin(Mathf.Clamp(-gravity.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (Mathf.Abs(degrees) < tiltDeadzoneDegrees) return 0f;

            float sign = Mathf.Sign(degrees);
            float magnitude = (Mathf.Abs(degrees) - tiltDeadzoneDegrees)
                              / Mathf.Max(1f, tiltFullLockDegrees - tiltDeadzoneDegrees);

            return sign * Mathf.Clamp01(magnitude);
        }

        float ReadDeviceSteer()
        {
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                float stick = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(stick) > 0.08f) return stick;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                float k = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k += 1f;
                return k;
            }
            return 0f;
#else
            return UnityEngine.Input.GetAxis("Horizontal");
#endif
        }

        void ReadPedals(out float throttle, out float brake, out bool handbrake)
        {
            throttle = autoThrottle ? 1f : 0f;
            brake = 0f;
            handbrake = false;

            if (useTouchControls)
            {
                brake = Mathf.Clamp01(onScreenBrake);
                handbrake = onScreenHandbrake;
                if (!autoThrottle) throttle = brake > 0.01f ? 0f : 1f;
                if (brake > 0.01f) throttle = 0f;
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                float t = pad.rightTrigger.ReadValue();
                float b = pad.leftTrigger.ReadValue();
                if (!autoThrottle) throttle = t;
                else if (b > 0.02f) throttle = 0f;
                brake = b;
                handbrake = pad.buttonEast.isPressed;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (!autoThrottle && kb.wKey.isPressed) throttle = 1f;
                if (kb.sKey.isPressed) { brake = 1f; throttle = 0f; }
                if (kb.spaceKey.isPressed) handbrake = true;
            }
#else
            float axis = UnityEngine.Input.GetAxis("Vertical");
            if (!autoThrottle) throttle = Mathf.Max(0f, axis);
            brake = Mathf.Max(0f, -axis);
            if (brake > 0.01f) throttle = 0f;
            handbrake = UnityEngine.Input.GetKey(KeyCode.Space);
#endif
        }
    }
}
