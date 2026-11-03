using UnityEngine;
using UnityEngine.InputSystem;

namespace VoiceGame
{
    public enum SentinelState { Asleep, Waking, Watching, Stunned }

    public enum RoundState { Playing, Won, Lost }

    /// <summary>
    /// The rules: grab the key above the chest, reach the gate, and don't be seen moving
    /// while the Sentinel's eye is open. Crouching keeps the hero hidden.
    /// </summary>
    public sealed class SentinelGame : MonoBehaviour
    {
        const float WakingSeconds = 2.5f;
        const float WatchingSeconds = 3f;
        const float StunSeconds = 15f;
        const float SeenAfterSeconds = 0.3f;
        const float MovingSpeed = 0.4f;
        const float AirborneHeight = 0.25f;
        const float KeyGrabHeight = 0.6f;
        const float KeyGrabRadius = 3f;
        const float ReachRadius = 2.5f;
        const float MessageSeconds = 3.5f;
        const int StartingLives = 3;
        const int TargetFrameRate = 120;

        public HeroCommander hero;
        public Transform start;
        public Transform chest;
        public Transform tower;
        public Transform gate;
        public GameObject key;
        public GameObject gateDoor;
        public Renderer eye;
        public Light eyeLight;
        public Vector2 asleepSeconds = new Vector2(5f, 8f);

        Material eyeMaterial;
        Vector3 lastPosition;
        float stateEnds;
        float seenFor;
        float messageEnds;
        float startedAt;
        float finishedAt;
        bool towerUsed;
        bool gateWarned;

        public SentinelState Sentinel { get; private set; }
        public RoundState Round { get; private set; }
        public bool HasKey { get; private set; }
        public int Lives { get; private set; }
        public string Message { get; private set; } = "";
        public float StateSecondsLeft => Mathf.Max(0f, stateEnds - Time.time);
        public float ElapsedSeconds => (Round == RoundState.Playing ? Time.time : finishedAt) - startedAt;
        public bool AcceptsOrders => Round == RoundState.Playing;
        public string ShownMessage => Time.time < messageEnds ? Message : "";

        public string Goal
        {
            get
            {
                if (Round == RoundState.Won) return "You escaped!";
                if (Round == RoundState.Lost) return "The Sentinel got you";
                return HasKey ? "Reach the gate" : "Go to the chest, then jump to grab the key";
            }
        }

        void Start()
        {
            Application.targetFrameRate = TargetFrameRate;
            eyeMaterial = eye.material;
            Restart();
        }

        public void Restart()
        {
            Lives = StartingLives;
            HasKey = false;
            towerUsed = false;
            gateWarned = false;
            Round = RoundState.Playing;
            startedAt = Time.time;
            key.SetActive(true);
            gateDoor.SetActive(true);
            hero.Teleport(start.position);
            lastPosition = hero.Position;
            seenFor = 0f;
            Enter(SentinelState.Asleep);
            Say("Hold V and say \"Run to the chest\"");
        }

        public void Stun(float seconds)
        {
            Enter(SentinelState.Stunned);
            stateEnds = Time.time + seconds;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
            key.transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
            if (Round == RoundState.Playing)
            {
                AdvanceSentinel();
                CheckGoals();
                CheckSeen();
            }
            lastPosition = hero.Position;
            UpdateEye();
        }

        void AdvanceSentinel()
        {
            if (Time.time < stateEnds) return;
            switch (Sentinel)
            {
                case SentinelState.Asleep: Enter(SentinelState.Waking); break;
                case SentinelState.Waking: Enter(SentinelState.Watching); break;
                default: Enter(SentinelState.Asleep); break;
            }
        }

        void Enter(SentinelState state)
        {
            Sentinel = state;
            seenFor = 0f;
            switch (state)
            {
                case SentinelState.Asleep: stateEnds = Time.time + Random.Range(asleepSeconds.x, asleepSeconds.y); break;
                case SentinelState.Waking: stateEnds = Time.time + WakingSeconds; break;
                case SentinelState.Watching: stateEnds = Time.time + WatchingSeconds; break;
                case SentinelState.Stunned: stateEnds = Time.time + StunSeconds; break;
            }
        }

        void CheckGoals()
        {
            var position = hero.Position;
            if (!HasKey && position.y > KeyGrabHeight && Flat(position, chest.position) < KeyGrabRadius)
            {
                HasKey = true;
                key.SetActive(false);
                Say("You grabbed the key! Now reach the gate.");
            }
            if (!towerUsed && Flat(position, tower.position) < ReachRadius)
            {
                towerUsed = true;
                Stun(StunSeconds);
                Say("You jammed the Sentinel! It is blind for 15 seconds.");
            }
            if (Flat(position, gate.position) >= ReachRadius)
            {
                gateWarned = false;
                return;
            }
            if (HasKey)
            {
                Round = RoundState.Won;
                finishedAt = Time.time;
                gateDoor.SetActive(false);
                hero.Halt();
                Say($"You escaped in {ElapsedSeconds:F0} seconds! Say \"restart\" to play again.");
            }
            else if (!gateWarned)
            {
                gateWarned = true;
                Say("The gate is locked. Find the key first.");
            }
        }

        void CheckSeen()
        {
            if (Sentinel != SentinelState.Watching) return;
            var position = hero.Position;
            var speed = Flat(position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            var moving = speed > MovingSpeed || position.y > AirborneHeight;
            seenFor = moving && !hero.Crouching ? seenFor + Time.deltaTime : 0f;
            if (seenFor >= SeenAfterSeconds) Caught();
        }

        void Caught()
        {
            Lives--;
            hero.Teleport(start.position);
            lastPosition = hero.Position;
            Enter(SentinelState.Asleep);
            if (Lives > 0)
            {
                Say($"Seen! Back to the start. {Lives} {(Lives == 1 ? "life" : "lives")} left.");
                return;
            }
            Round = RoundState.Lost;
            finishedAt = Time.time;
            Say("Game over. Say \"restart\" to try again.");
        }

        void Say(string text)
        {
            Message = text;
            messageEnds = Time.time + (Round == RoundState.Playing ? MessageSeconds : float.MaxValue);
        }

        void UpdateEye()
        {
            Color color;
            float openness;
            switch (Round == RoundState.Playing ? Sentinel : SentinelState.Asleep)
            {
                case SentinelState.Waking:
                    color = new Color(1f, 0.75f, 0.1f);
                    openness = Mathf.Lerp(0.2f, 0.7f, 1f - StateSecondsLeft / WakingSeconds);
                    break;
                case SentinelState.Watching:
                    color = new Color(1f, 0.1f, 0.05f);
                    openness = 1f;
                    break;
                case SentinelState.Stunned:
                    color = new Color(0.3f, 0.6f, 1f);
                    openness = 0.2f;
                    break;
                default:
                    color = new Color(0.1f, 0.5f, 0.2f);
                    openness = 0.2f;
                    break;
            }
            eyeMaterial.SetColor("_BaseColor", color);
            eyeMaterial.SetColor("_EmissionColor", color * 3f);
            eye.transform.localScale = new Vector3(2f, 2f * openness, 2f);
            var watching = Round == RoundState.Playing && Sentinel == SentinelState.Watching;
            eyeLight.enabled = watching;
            eyeLight.color = color;
            if (watching) eyeLight.transform.LookAt(hero.Position + Vector3.up);
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
