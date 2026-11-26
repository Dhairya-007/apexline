namespace Apexline.Vehicle
{
    /// <summary>
    /// Everything that can drive a car. Player touch/gamepad input and the AI
    /// driver both write one of these, so they share identical physics.
    /// </summary>
    public struct CarInput
    {
        public float Throttle;   // 0..1
        public float Brake;      // 0..1
        public float Steer;      // -1..1
        public bool  Handbrake;

        public static CarInput Idle => new CarInput();
    }
}
