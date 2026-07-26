using UnityEngine;

namespace LumenKart
{
    public readonly struct KartInputFrame
    {
        public KartInputFrame(
            float steering,
            float throttle,
            float brake,
            bool driftHeld,
            bool itemPressed,
            bool pausePressed)
        {
            Steering = Mathf.Clamp(steering, -1f, 1f);
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            DriftHeld = driftHeld;
            ItemPressed = itemPressed;
            PausePressed = pausePressed;
        }

        public float Steering { get; }
        public float Throttle { get; }
        public float Brake { get; }
        public bool DriftHeld { get; }
        public bool ItemPressed { get; }
        public bool PausePressed { get; }

        public static KartInputFrame Neutral => new(0f, 0f, 0f, false, false, false);
    }

    public abstract class KartInputSource : MonoBehaviour
    {
        public virtual bool IsHuman => false;
        public abstract KartInputFrame ReadInput();
    }

    public sealed class PlayerKartInput : KartInputSource
    {
        [SerializeField] private float steeringResponse = 9f;
        [SerializeField] private float steeringReturn = 12f;

        private float smoothedSteering;
        private bool previousItemHeld;
        private bool previousPauseHeld;

        public override bool IsHuman => true;

        public override KartInputFrame ReadInput()
        {
            float rawSteering = ReadSteering();
            float response = Mathf.Abs(rawSteering) > Mathf.Abs(smoothedSteering)
                ? steeringResponse
                : steeringReturn;
            smoothedSteering = Mathf.MoveTowards(
                smoothedSteering,
                rawSteering,
                response * Time.unscaledDeltaTime);

            float throttle = 0f;
            float brake = 0f;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                throttle = 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                brake = 1f;
            }

            float vertical = SafeGetAxisRaw("Vertical");
            if (vertical > 0.15f)
            {
                throttle = Mathf.Max(throttle, vertical);
            }
            else if (vertical < -0.15f)
            {
                brake = Mathf.Max(brake, -vertical);
            }

            bool driftHeld =
                Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.JoystickButton0) ||
                Input.GetKey(KeyCode.JoystickButton5);

            bool itemHeld =
                Input.GetKey(KeyCode.E) ||
                Input.GetKey(KeyCode.RightControl) ||
                Input.GetKey(KeyCode.JoystickButton1) ||
                Input.GetKey(KeyCode.JoystickButton4);

            bool pauseHeld =
                Input.GetKey(KeyCode.Escape) ||
                Input.GetKey(KeyCode.JoystickButton7);

            bool itemPressed = itemHeld && !previousItemHeld;
            bool pausePressed = pauseHeld && !previousPauseHeld;
            previousItemHeld = itemHeld;
            previousPauseHeld = pauseHeld;

            return new KartInputFrame(
                smoothedSteering,
                throttle,
                brake,
                driftHeld,
                itemPressed,
                pausePressed);
        }

        private static float ReadSteering()
        {
            float keyboard = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                keyboard -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                keyboard += 1f;
            }

            float axis = SafeGetAxisRaw("Horizontal");
            return Mathf.Abs(keyboard) > Mathf.Abs(axis) ? keyboard : axis;
        }

        private static float SafeGetAxisRaw(string axisName)
        {
            // The project uses Unity's standard Input Manager axes. The try/catch keeps the
            // prototype usable if a custom project settings file removes an axis later.
            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch
            {
                return 0f;
            }
        }
    }
}
