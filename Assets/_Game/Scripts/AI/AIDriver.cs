using UnityEngine;
using Apexline.Vehicle;

namespace Apexline.AI
{
    /// <summary>
    /// Pure-pursuit AI that writes the same CarInput a human does, so opponents
    /// obey identical physics. Skill varies per instance to keep races close.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class AIDriver : MonoBehaviour
    {
        [Header("Path")]
        public WaypointPath path;

        [Header("Skill (0 = backmarker, 1 = ace)")]
        [Range(0f, 1f)] public float skill = 0.75f;

        [Header("Pursuit")]
        [Tooltip("Lookahead distance = speed (m/s) * this, clamped below.")]
        public float lookaheadPerSpeed = 0.6f;
        public float minLookahead = 6f;
        public float maxLookahead = 40f;

        [Header("Speed")]
        public float cornerBrakeAggression = 1.4f;
        [Tooltip("Top speed as a fraction of the car's max, before skill variance.")]
        [Range(0.5f, 1f)] public float paceFraction = 0.96f;

        [Header("Avoidance")]
        public float avoidDistance = 14f;
        public float avoidStrength = 2.5f;
        public LayerMask carLayers = ~0;

        [Header("Recovery")]
        public float stuckSpeedKph = 4f;
        public float stuckSeconds = 2.5f;

        public int CurrentNode { get; private set; }
        public float LapProgress { get; private set; }

        CarController _car;
        float _lateralOffset;
        float _stuckTimer;
        float _skillJitter;

        void Awake()
        {
            _car = GetComponent<CarController>();
            // +/- 6% pace spread so the field isn't a train.
            _skillJitter = Random.Range(-0.06f, 0.06f);
        }

        void Start()
        {
            if (path != null) CurrentNode = path.NearestIndex(transform.position);
        }

        void FixedUpdate()
        {
            if (path == null || path.Count < 2) { _car.Input = CarInput.Idle; return; }

            AdvanceNode();
            LapProgress = path.Progress(transform.position);

            Vector3 target = ComputeTarget();
            float steer = ComputeSteer(target);
            ComputeThrottleBrake(out float throttle, out float brake);
            HandleStuck(ref throttle, ref brake, ref steer);

            _car.Input = new CarInput
            {
                Steer = steer,
                Throttle = throttle,
                Brake = brake,
                Handbrake = false
            };
        }

        void AdvanceNode()
        {
            // Consume nodes we've already driven past.
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 toNode = path.Position(CurrentNode + 1) - transform.position;
                toNode.y = 0f;

                if (Vector3.Dot(toNode, path.Forward(CurrentNode)) > 0f && toNode.magnitude > 3f)
                    break;

                CurrentNode = path.Wrap(CurrentNode + 1);
            }
        }

        Vector3 ComputeTarget()
        {
            float speed = _car.Body.linearVelocity.magnitude;
            float lookahead = Mathf.Clamp(speed * lookaheadPerSpeed, minLookahead, maxLookahead);

            // Walk forward along the path until we've covered the lookahead.
            int idx = CurrentNode;
            float covered = 0f;
            Vector3 prev = transform.position;

            while (covered < lookahead && idx < CurrentNode + path.Count)
            {
                idx++;
                Vector3 p = path.Position(idx);
                covered += Vector3.Distance(prev, p);
                prev = p;
            }

            Vector3 target = path.Position(idx);

            // Lateral offset for overtaking / avoidance, clamped to track width.
            UpdateAvoidance();
            float hw = Mathf.Max(0.5f, path.At(idx).halfWidth - 1.2f);
            Vector3 right = Vector3.Cross(Vector3.up, path.Forward(idx));
            target += right * Mathf.Clamp(_lateralOffset, -hw, hw);

            return target;
        }

        void UpdateAvoidance()
        {
            float desired = 0f;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.SphereCast(origin, 1.1f, transform.forward,
                                   out RaycastHit hit, avoidDistance, carLayers,
                                   QueryTriggerInteraction.Ignore))
            {
                if (hit.rigidbody != null && hit.rigidbody != _car.Body)
                {
                    // Pull toward whichever side the obstacle isn't on.
                    float side = Vector3.Dot(transform.right, hit.point - transform.position);
                    float urgency = 1f - Mathf.Clamp01(hit.distance / avoidDistance);
                    desired = -Mathf.Sign(side) * avoidStrength * urgency;
                }
            }

            _lateralOffset = Mathf.MoveTowards(_lateralOffset, desired, 6f * Time.fixedDeltaTime);
        }

        float ComputeSteer(Vector3 target)
        {
            Vector3 local = transform.InverseTransformPoint(target);
            local.y = 0f;

            float angle = Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z)) * Mathf.Rad2Deg;
            float gain = Mathf.Lerp(0.035f, 0.055f, skill);

            return Mathf.Clamp(angle * gain, -1f, 1f);
        }

        void ComputeThrottleBrake(out float throttle, out float brake)
        {
            float curvature = path.CurvatureAhead(CurrentNode, Mathf.RoundToInt(Mathf.Lerp(2f, 4f, skill)));
            float nodeLimit = path.At(CurrentNode + 2).speedLimit;

            float pace = Mathf.Clamp01(paceFraction + _skillJitter) * Mathf.Lerp(0.88f, 1f, skill);
            float targetKph = _car.maxSpeedKph * pace * nodeLimit
                              * Mathf.Lerp(1f, 0.30f, curvature * cornerBrakeAggression);

            float error = targetKph - _car.SpeedKph;

            if (error > 2f)      { throttle = Mathf.Clamp01(error / 25f); brake = 0f; }
            else if (error < -6f) { throttle = 0f; brake = Mathf.Clamp01(-error / 40f); }
            else                  { throttle = 0.35f; brake = 0f; }
        }

        void HandleStuck(ref float throttle, ref float brake, ref float steer)
        {
            bool slow = _car.SpeedKph < stuckSpeedKph;
            _stuckTimer = slow ? _stuckTimer + Time.fixedDeltaTime : 0f;

            if (_stuckTimer < stuckSeconds) return;

            // Reverse out, steering away from the path, then retry.
            throttle = 0f;
            brake = 1f;
            steer = -steer;

            if (_stuckTimer > stuckSeconds + 1.5f)
            {
                RespawnOnPath();
                _stuckTimer = 0f;
            }
        }

        public void RespawnOnPath()
        {
            int node = path.NearestIndex(transform.position);
            Vector3 pos = path.Position(node) + Vector3.up * 0.6f;
            Quaternion rot = Quaternion.LookRotation(path.Forward(node), Vector3.up);
            _car.ResetTo(pos, rot);
            CurrentNode = node;
            _lateralOffset = 0f;
        }
    }
}
