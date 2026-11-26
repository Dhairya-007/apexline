using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Apexline.AI;
using Apexline.Vehicle;

namespace Apexline.Race
{
    /// <summary>
    /// Owns the race: grid, countdown, lap counting, live positions, finish.
    /// Lap validity requires every sector to be hit, which stops corner cutting.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public enum State { Grid, Countdown, Racing, Finished }

        [Header("Setup")]
        public WaypointPath path;
        public int totalLaps = 3;
        public Transform[] gridSlots;
        [Tooltip("Sector triggers in lap order. The first one is the start/finish line.")]
        public RaceSector[] sectors;

        [Header("Countdown")]
        public float countdownSeconds = 3f;

        public State Current { get; private set; } = State.Grid;
        public float RaceTime { get; private set; }
        public int CountdownRemaining { get; private set; }
        public IReadOnlyList<Racer> Standings => _standings;

        public event System.Action<State> StateChanged;
        public event System.Action<Racer> LapCompleted;
        public event System.Action<Racer> RacerFinished;

        readonly List<Racer> _racers = new List<Racer>();
        readonly List<Racer> _standings = new List<Racer>();

        public class Racer
        {
            public CarController car;
            public string displayName;
            public bool isPlayer;

            public int lap;                  // completed laps
            public int position = 1;
            public float progress;           // node units within the current lap
            public float totalProgress;      // lap * nodeCount + progress
            public float lastLapTime;
            public float bestLapTime = float.MaxValue;
            public float lapStartTime;
            public bool finished;
            public float finishTime;

            public readonly HashSet<int> sectorsHit = new HashSet<int>();
        }

        void Awake()
        {
            AutoRegisterCars();
        }

        void Start()
        {
            // Grid placement happens in Start, not Awake: it writes to each
            // car's Rigidbody, and Awake order between components is not
            // guaranteed. Start runs only after every Awake has completed.
            PlaceOnGrid();
            StartCoroutine(RunCountdown());
        }

        void AutoRegisterCars()
        {
            foreach (var car in FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                bool isAi = car.GetComponent<AIDriver>() != null;
                _racers.Add(new Racer
                {
                    car = car,
                    isPlayer = !isAi,
                    displayName = isAi ? car.name : "You"
                });
            }
            _standings.AddRange(_racers);
        }

        void PlaceOnGrid()
        {
            for (int i = 0; i < _racers.Count && gridSlots != null && i < gridSlots.Length; i++)
            {
                if (gridSlots[i] == null) continue;
                _racers[i].car.ResetTo(gridSlots[i].position, gridSlots[i].rotation);
            }
        }

        IEnumerator RunCountdown()
        {
            SetState(State.Countdown);
            FreezeAll(true);

            for (int n = Mathf.CeilToInt(countdownSeconds); n > 0; n--)
            {
                CountdownRemaining = n;
                yield return new WaitForSeconds(1f);
            }

            CountdownRemaining = 0;
            FreezeAll(false);
            RaceTime = 0f;

            foreach (var r in _racers) r.lapStartTime = 0f;

            SetState(State.Racing);
        }

        void FreezeAll(bool frozen)
        {
            foreach (var r in _racers)
            {
                if (r.car == null || r.car.Body == null) continue;
                r.car.Body.isKinematic = frozen;
                if (frozen) r.car.Input = CarInput.Idle;

                var ai = r.car.GetComponent<AIDriver>();
                if (ai != null) ai.enabled = !frozen;
            }
        }

        void Update()
        {
            if (Current != State.Racing) return;

            RaceTime += Time.deltaTime;
            UpdateStandings();
        }

        void UpdateStandings()
        {
            if (path == null || path.Count == 0) return;

            foreach (var r in _racers)
            {
                if (r.finished) continue;
                r.progress = path.Progress(r.car.transform.position);
                r.totalProgress = r.lap * path.Count + r.progress;
            }

            _standings.Sort((a, b) =>
            {
                if (a.finished != b.finished) return a.finished ? -1 : 1;
                if (a.finished && b.finished) return a.finishTime.CompareTo(b.finishTime);
                return b.totalProgress.CompareTo(a.totalProgress);
            });

            for (int i = 0; i < _standings.Count; i++) _standings[i].position = i + 1;
        }

        /// <summary>Called by RaceSector triggers.</summary>
        public void OnSectorEntered(int sectorIndex, CarController car)
        {
            if (Current != State.Racing) return;

            Racer r = _racers.Find(x => x.car == car);
            if (r == null || r.finished) return;

            bool isStartLine = sectorIndex == 0;

            if (!isStartLine)
            {
                r.sectorsHit.Add(sectorIndex);
                return;
            }

            // Start/finish line: only count the lap if the whole track was driven.
            int required = Mathf.Max(0, (sectors?.Length ?? 1) - 1);
            if (r.sectorsHit.Count < required) return;

            r.sectorsHit.Clear();
            r.lap++;

            r.lastLapTime = RaceTime - r.lapStartTime;
            r.lapStartTime = RaceTime;
            if (r.lastLapTime < r.bestLapTime) r.bestLapTime = r.lastLapTime;

            LapCompleted?.Invoke(r);

            if (r.lap >= totalLaps) FinishRacer(r);
        }

        void FinishRacer(Racer r)
        {
            r.finished = true;
            r.finishTime = RaceTime;
            RacerFinished?.Invoke(r);

            var ai = r.car.GetComponent<AIDriver>();
            if (ai != null) ai.paceFraction *= 0.6f;   // cool down, don't hard stop

            if (r.isPlayer) SetState(State.Finished);
        }

        void SetState(State s)
        {
            Current = s;
            StateChanged?.Invoke(s);
        }

        public Racer PlayerRacer => _racers.Find(r => r.isPlayer);

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f || float.IsInfinity(seconds) || seconds >= float.MaxValue * 0.5f)
                return "--:--.---";

            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.000}";
        }
    }
}
