using System;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

namespace VoiceGame
{
    public enum HeroOrder { WalkTo, RunTo, Jump, Crouch, Stand, Stop }

    public readonly struct HeroCommand
    {
        public readonly HeroOrder Order;
        public readonly string Place;

        public HeroCommand(HeroOrder order, string place = null)
        {
            Order = order;
            Place = place;
        }

        public bool IsMove => Order == HeroOrder.WalkTo || Order == HeroOrder.RunTo;
    }

    [Serializable]
    public struct Landmark
    {
        public string name;
        public Transform point;
    }

    /// <summary>
    /// Turns spoken orders into the same inputs the keyboard would give the Synty controller,
    /// by steering its InputReader. Chained orders run one after another.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class HeroCommander : MonoBehaviour
    {
        const float ArrivalRadius = 2.3f;
        const float WaypointRadius = 2f;
        const float WalkInput = 0.6f;
        const float CrouchInput = 0.8f;

        public InputReader input;
        public SampleCameraController cameraController;
        public CharacterController controller;
        public Landmark[] landmarks;
        public Transform bridge;
        public float riverZ;

        readonly Queue<HeroCommand> queued = new Queue<HeroCommand>();
        readonly Queue<Vector3> route = new Queue<Vector3>();
        float gait;
        bool sprinting;

        public bool Crouching { get; private set; }
        public bool Moving => route.Count > 0;
        public string Destination { get; private set; }
        public Vector3 Position => controller.transform.position;

        public bool Knows(string place) => FindPlace(place) != null;

        public void Give(IReadOnlyList<HeroCommand> commands)
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                if (command.Order == HeroOrder.Stop)
                {
                    Halt();
                    continue;
                }
                if (i > 0)
                {
                    queued.Enqueue(command);
                    continue;
                }
                if (command.IsMove) queued.Clear();
                Execute(command);
            }
        }

        public void Halt()
        {
            queued.Clear();
            route.Clear();
            Destination = null;
            ReleaseInput();
        }

        public void Teleport(Vector3 position)
        {
            Halt();
            if (Crouching) Execute(new HeroCommand(HeroOrder.Stand));
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = true;
        }

        void Execute(HeroCommand command)
        {
            switch (command.Order)
            {
                case HeroOrder.WalkTo:
                case HeroOrder.RunTo:
                    BeginMove(command);
                    break;
                case HeroOrder.Jump:
                    Crouching = false;
                    input.onJumpPerformed?.Invoke();
                    break;
                case HeroOrder.Crouch:
                    SetSprint(false);
                    Crouching = true;
                    input.onCrouchActivated?.Invoke();
                    break;
                case HeroOrder.Stand:
                    Crouching = false;
                    input.onCrouchDeactivated?.Invoke();
                    break;
            }
        }

        void BeginMove(HeroCommand command)
        {
            var place = FindPlace(command.Place);
            if (place == null) return;
            var running = command.Order == HeroOrder.RunTo;
            if (running && Crouching) Execute(new HeroCommand(HeroOrder.Stand));
            route.Clear();
            var crossesRiver = (Position.z < riverZ) != (place.position.z < riverZ);
            if (crossesRiver && place != bridge) route.Enqueue(bridge.position);
            route.Enqueue(place.position);
            Destination = command.Place;
            gait = running ? 1f : WalkInput;
            SetSprint(running);
        }

        Transform FindPlace(string place)
        {
            foreach (var landmark in landmarks)
                if (landmark.name == place) return landmark.point;
            return null;
        }

        void Update()
        {
            if (route.Count > 0) Steer();
            else if (queued.Count > 0) Execute(queued.Dequeue());
        }

        void Steer()
        {
            var next = route.Peek();
            var offset = next - Position;
            offset.y = 0f;
            var radius = route.Count > 1 ? WaypointRadius : ArrivalRadius;
            if (offset.magnitude <= radius)
            {
                route.Dequeue();
                if (route.Count > 0) return;
                Destination = null;
                ReleaseInput();
                return;
            }
            var direction = offset.normalized;
            var scale = Crouching ? CrouchInput : gait;
            input._moveComposite = scale * new Vector2(
                Vector3.Dot(direction, cameraController.GetCameraRightZeroedYNormalised()),
                Vector3.Dot(direction, cameraController.GetCameraForwardZeroedYNormalised()));
            input._movementInputDetected = true;
        }

        void ReleaseInput()
        {
            SetSprint(false);
            input._moveComposite = Vector2.zero;
            input._movementInputDetected = false;
        }

        void SetSprint(bool on)
        {
            if (on == sprinting) return;
            sprinting = on;
            if (on) input.onSprintActivated?.Invoke();
            else input.onSprintDeactivated?.Invoke();
        }
    }
}
