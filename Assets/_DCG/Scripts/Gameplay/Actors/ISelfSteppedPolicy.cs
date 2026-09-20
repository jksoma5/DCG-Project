namespace DCG.Gameplay
{
    // A movement policy that advances its own actor, on its own loop, instead of being advanced by the
    // world. The fighting system is the case: a fight frame moves both fighters itself, inside
    // FighterAgent.Prepare, because the movement and the frame data are the same decision.
    //
    // The world therefore leaves such an actor alone. Stepping it as well would apply gravity and
    // movement twice in one tick, which is what used to make a fight impossible to run at the same time
    // as the other classes: the old way out was to switch the whole world off automatic ticks.
    public interface ISelfSteppedPolicy { }
}
