using DCG.Core;
namespace DCG.Gameplay
{
    // Optional direct-control/ability integration; Gameplay never depends on a concrete class.
    public interface IActorActionPolicy : IMovementPolicy
    {
        bool Accepts(PlayerCommand command);
        ActionState ActionState { get; }
        bool UseFullVelocity { get; }
        void AfterMove(float deltaTime, uint tick);
    }
}
