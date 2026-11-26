using UnityEngine;
using Apexline.Vehicle;

namespace Apexline.Util
{
    /// <summary>
    /// Chase camera for the target car. Deliberately hand-written rather than a
    /// Cinemachine rig: the camera is half of how a racing game feels, and the
    /// three things that matter most here - positional lag, speed-based FOV and
    /// a kerb-triggered shake - are easier to tune directly than through a rig.
    /// Cinemachine remains available if a richer setup is wanted later.
    ///
    /// Runs in LateUpdate so it reads the car's already-interpolated transform
    /// and renders at display rate, not at the 50 Hz physics rate.
    /// </summary>
    public class ChaseCamera : MonoBehaviour
    {
        [Header("Target")]
        public CarController target;

        [Header("Placement (local to the car)")]
        public Vector3 offset = new Vector3(0f, 2.2f, -6.0f);
        [Tooltip("Look at a point this far above the car's origin.")]
        public float lookAtHeight = 1.0f;
        [Tooltip("Aim this far ahead along the car's velocity, in seconds.")]
        public float leadSeconds = 0.25f;

        [Header("Damping (seconds to catch up)")]
        public float positionDamping = 0.18f;
        public float rotationDamping = 0.10f;

        [Header("Field of view")]
        public float fovAtRest = 60f;
        public float fovAtTopSpeed = 75f;

        [Header("Shake")]
        [Tooltip("Shake amplitude when a wheel is sliding hard.")]
        public float slipShake = 0.06f;
        [Tooltip("Shake amplitude while airborne.")]
        public float airborneShake = 0.10f;
        public float shakeFrequency = 22f;

        Camera _cam;
        Vector3 _posVelocity;
        float _currentFov;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = gameObject.AddComponent<Camera>();
            _currentFov = fovAtRest;
        }

        void Start()
        {
            if (target != null) SnapToTarget();
        }

        /// <summary>Jumps straight to the framed position, with no damping.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.transform.TransformPoint(offset);
            transform.rotation = Quaternion.LookRotation(LookPoint() - transform.position, Vector3.up);
            _posVelocity = Vector3.zero;
        }

        Vector3 LookPoint()
        {
            Vector3 p = target.transform.position + Vector3.up * lookAtHeight;
            if (target.Body != null) p += target.Body.linearVelocity * leadSeconds;
            return p;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // --- position -------------------------------------------------
            Vector3 desired = target.transform.TransformPoint(offset);
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _posVelocity, positionDamping);

            // --- rotation -------------------------------------------------
            Quaternion want = Quaternion.LookRotation(
                LookPoint() - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, want,
                1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, rotationDamping)));

            // --- field of view --------------------------------------------
            float speed01 = Mathf.Clamp01(target.SpeedKph / Mathf.Max(1f, target.maxSpeedKph));
            _currentFov = Mathf.Lerp(_currentFov,
                Mathf.Lerp(fovAtRest, fovAtTopSpeed, speed01),
                1f - Mathf.Exp(-dt / 0.25f));
            if (_cam != null) _cam.fieldOfView = _currentFov;

            // --- shake ----------------------------------------------------
            float amp = 0f;
            if (!target.IsGrounded) amp = airborneShake;
            else if (target.MaxLateralSlip > 0.4f) amp = slipShake * target.MaxLateralSlip;

            if (amp > 0.0001f)
            {
                float t = Time.time * shakeFrequency;
                // Perlin noise rather than Random: continuous, so the shake
                // reads as vibration instead of per-frame jitter.
                Vector3 jitter = new Vector3(
                    Mathf.PerlinNoise(t, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, t) - 0.5f,
                    0f) * (amp * 2f);
                transform.position += transform.TransformDirection(jitter);
            }
        }
    }
}
