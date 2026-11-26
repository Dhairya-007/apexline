using UnityEngine;

namespace Apexline.Vehicle
{
    /// <summary>
    /// Semi-arcade vehicle controller built on WheelCollider.
    /// Tuned for "arcade with weight" feel: assists do the heavy lifting,
    /// not tyre-model accuracy. Default values are race-ready; see docs/TUNING.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [System.Serializable]
        public class Wheel
        {
            public WheelCollider collider;
            public Transform mesh;
            public bool steer;
            public bool drive;
            public bool handbrake;
            [HideInInspector] public float visualSpin;
        }

        [Header("Wheels (FL, FR, RL, RR)")]
        public Wheel[] wheels = new Wheel[4];

        [Header("Body")]
        [Tooltip("Lower = less likely to roll. Applied as a local offset.")]
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.30f, 0f);
        public float mass = 1300f;

        [Header("Engine / Drivetrain")]
        public float maxMotorTorque = 2500f;
        public float maxSpeedKph = 260f;
        public float reverseSpeedKph = 40f;
        [Tooltip("Torque multiplier across the speed range (x = speed/maxSpeed).")]
        public AnimationCurve powerCurve = new AnimationCurve(
            new Keyframe(0.00f, 0.75f),
            new Keyframe(0.25f, 1.00f),
            new Keyframe(0.70f, 0.85f),
            new Keyframe(1.00f, 0.25f));

        [Header("Braking")]
        public float brakeTorque = 4000f;
        public float handbrakeTorque = 6000f;
        [Range(0f, 1f)] public float brakeBiasFront = 0.62f;

        [Header("Steering")]
        public float maxSteerAngleLowSpeed = 28f;
        public float maxSteerAngleHighSpeed = 8f;
        [Tooltip("Speed (kph) at which steering reaches its tightest limit.")]
        public float steerFalloffSpeedKph = 220f;
        [Tooltip("Degrees per second the wheels can turn. Lower = heavier.")]
        public float steerRateDegPerSec = 160f;
        [Range(0f, 1f)] public float counterSteerAssist = 0.35f;

        [Header("Stability Assists")]
        public float antiRollFront = 9000f;
        public float antiRollRear  = 7000f;
        [Tooltip("Downforce in Newtons at maxSpeedKph. Scales with speed squared.")]
        public float downforceAtTopSpeed = 6500f;
        [Tooltip("Extra grip added to rear tyres under power to stop snap spins.")]
        [Range(0f, 1f)] public float tractionControl = 0.4f;

        // ---- Runtime state (read by audio, VFX, HUD, AI) ----
        public CarInput Input { get; set; } = CarInput.Idle;
        public float SpeedKph { get; private set; }
        public float EngineRpm01 { get; private set; }
        public int Gear { get; private set; } = 1;
        public float MaxLateralSlip { get; private set; }
        public float MaxForwardSlip { get; private set; }
        public bool IsGrounded { get; private set; }
        public Rigidbody Body => _rb;

        const int GearCount = 6;

        Rigidbody _rb;
        float _currentSteerAngle;
        float[] _defaultSidewaysStiffness;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = mass;
            _rb.centerOfMass += centerOfMassOffset;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _defaultSidewaysStiffness = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i]?.collider == null) continue;
                _defaultSidewaysStiffness[i] = wheels[i].collider.sidewaysFriction.stiffness;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            SpeedKph = _rb.linearVelocity.magnitude * 3.6f;
            float speed01 = Mathf.Clamp01(SpeedKph / Mathf.Max(1f, maxSpeedKph));

            ApplySteering(dt);
            ApplyDrive(speed01);
            ApplyBrakes();
            ApplyDownforce();
            ApplyAntiRoll(0, 1, antiRollFront);
            ApplyAntiRoll(2, 3, antiRollRear);
            ApplyTractionControl();
            ReadSlipAndGrounding();
            UpdateFakeGearbox(speed01);
        }

        void Update() => UpdateWheelMeshes();

        // -------------------------------------------------------------- steering
        void ApplySteering(float dt)
        {
            float t = Mathf.Clamp01(SpeedKph / Mathf.Max(1f, steerFalloffSpeedKph));
            float limit = Mathf.Lerp(maxSteerAngleLowSpeed, maxSteerAngleHighSpeed, t);

            float target = Input.Steer * limit;

            // Counter-steer assist: if the car is already rotating into the turn,
            // ease off so the player doesn't have to catch every slide themselves.
            if (counterSteerAssist > 0f && SpeedKph > 25f)
            {
                Vector3 localVel = transform.InverseTransformDirection(_rb.linearVelocity);
                float slipAngle = Mathf.Atan2(localVel.x, Mathf.Abs(localVel.z)) * Mathf.Rad2Deg;
                target += Mathf.Clamp(-slipAngle, -limit, limit) * counterSteerAssist;
                target = Mathf.Clamp(target, -limit, limit);
            }

            _currentSteerAngle = Mathf.MoveTowards(
                _currentSteerAngle, target, steerRateDegPerSec * dt);

            foreach (var w in wheels)
                if (w?.collider != null && w.steer) w.collider.steerAngle = _currentSteerAngle;
        }

        // ----------------------------------------------------------------- drive
        void ApplyDrive(float speed01)
        {
            bool reversing = Input.Brake > 0.1f && SpeedKph < 3f;
            float throttle = reversing ? -Input.Brake : Input.Throttle;

            float limitKph = reversing ? reverseSpeedKph : maxSpeedKph;
            if (SpeedKph >= limitKph) throttle = 0f;

            float torque = throttle * maxMotorTorque * powerCurve.Evaluate(speed01);

            int driven = 0;
            foreach (var w in wheels) if (w?.collider != null && w.drive) driven++;
            if (driven == 0) return;

            foreach (var w in wheels)
                if (w?.collider != null && w.drive) w.collider.motorTorque = torque / driven;
        }

        // ---------------------------------------------------------------- brakes
        void ApplyBrakes()
        {
            bool reversing = Input.Brake > 0.1f && SpeedKph < 3f;
            float brake = reversing ? 0f : Input.Brake * brakeTorque;

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w?.collider == null) continue;

                float bias = w.steer ? brakeBiasFront : (1f - brakeBiasFront);
                float t = brake * bias * 2f;

                if (Input.Handbrake && w.handbrake)
                {
                    t = Mathf.Max(t, handbrakeTorque);
                    w.collider.motorTorque = 0f;
                }
                w.collider.brakeTorque = t;
            }
        }

        // ------------------------------------------------------------- stability
        void ApplyDownforce()
        {
            float f = Mathf.Pow(Mathf.Clamp01(SpeedKph / Mathf.Max(1f, maxSpeedKph)), 2f);
            _rb.AddForce(-transform.up * (downforceAtTopSpeed * f));
        }

        /// <summary>Anti-roll bar between two wheels - the cheapest way to stop flipping.</summary>
        void ApplyAntiRoll(int leftIdx, int rightIdx, float stiffness)
        {
            if (leftIdx >= wheels.Length || rightIdx >= wheels.Length) return;
            var left = wheels[leftIdx]?.collider;
            var right = wheels[rightIdx]?.collider;
            if (left == null || right == null) return;

            float travelL = 1f, travelR = 1f;

            bool groundedL = left.GetGroundHit(out WheelHit hitL);
            if (groundedL)
                travelL = (-left.transform.InverseTransformPoint(hitL.point).y
                           - left.radius) / left.suspensionDistance;

            bool groundedR = right.GetGroundHit(out WheelHit hitR);
            if (groundedR)
                travelR = (-right.transform.InverseTransformPoint(hitR.point).y
                           - right.radius) / right.suspensionDistance;

            float force = (travelL - travelR) * stiffness;

            if (groundedL)
                _rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (groundedR)
                _rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        /// <summary>Temporarily stiffens rear tyres under power so the car doesn't snap-spin.</summary>
        void ApplyTractionControl()
        {
            if (tractionControl <= 0f) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                if (w?.collider == null || !w.drive) continue;
                if (Input.Handbrake) { Restore(i, w); continue; }

                if (!w.collider.GetGroundHit(out WheelHit hit)) { Restore(i, w); continue; }

                float excess = Mathf.Abs(hit.forwardSlip) - 0.35f;
                var f = w.collider.sidewaysFriction;
                f.stiffness = _defaultSidewaysStiffness[i] *
                              (1f + Mathf.Max(0f, excess) * tractionControl);
                w.collider.sidewaysFriction = f;
            }

            void Restore(int i, Wheel w)
            {
                var f = w.collider.sidewaysFriction;
                f.stiffness = _defaultSidewaysStiffness[i];
                w.collider.sidewaysFriction = f;
            }
        }

        // ------------------------------------------------------------- telemetry
        void ReadSlipAndGrounding()
        {
            float lat = 0f, fwd = 0f;
            int grounded = 0;

            foreach (var w in wheels)
            {
                if (w?.collider == null) continue;
                if (!w.collider.GetGroundHit(out WheelHit hit)) continue;
                grounded++;
                lat = Mathf.Max(lat, Mathf.Abs(hit.sidewaysSlip));
                fwd = Mathf.Max(fwd, Mathf.Abs(hit.forwardSlip));
            }

            MaxLateralSlip = lat;
            MaxForwardSlip = fwd;
            IsGrounded = grounded >= 2;
        }

        /// <summary>No real gearbox - this exists purely to drive engine audio and the HUD.</summary>
        void UpdateFakeGearbox(float speed01)
        {
            float perGear = 1f / GearCount;
            Gear = Mathf.Clamp(Mathf.FloorToInt(speed01 / perGear) + 1, 1, GearCount);

            float within = (speed01 - (Gear - 1) * perGear) / perGear;
            float load = Mathf.Max(Input.Throttle, 0.18f);
            EngineRpm01 = Mathf.Clamp01(Mathf.Lerp(0.20f, 1f, within) * load
                                        + MaxForwardSlip * 0.25f);
        }

        // ---------------------------------------------------------------- visual
        void UpdateWheelMeshes()
        {
            foreach (var w in wheels)
            {
                if (w?.collider == null || w.mesh == null) continue;

                w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
                w.mesh.SetPositionAndRotation(pos, rot);
            }
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(position, rotation);
            _currentSteerAngle = 0f;
            Input = CarInput.Idle;
        }
    }
}
