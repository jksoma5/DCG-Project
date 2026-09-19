namespace DCG.Classes.Rifle
{
    // Pure input state: a hold never turns into ADS when released.
    public sealed class RifleAimGesture
    {
        float downAt;
        bool down, held;
        public bool Ads { get; private set; }
        public bool Shoulder => down && held;
        public void Step(bool pressed, bool released, bool isDown, float time, float threshold)
        {
            if (pressed) { down = true; held = false; downAt = time; }
            if (down && (isDown || released) && time - downAt >= threshold) { held = true; Ads = false; }
            if (released && down)
            {
                if (!held) Ads = !Ads;
                down = false; held = false;
            }
        }
        public void Reset() { down = held = Ads = false; }
    }
}
