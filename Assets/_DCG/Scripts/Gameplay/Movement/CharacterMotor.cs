using DCG.Core;
using UnityEngine;

namespace DCG.Gameplay
{
    public interface IMovementPolicy
    {
        void Receive(PlayerCommand command);
        Vector3 DesiredVelocity(float deltaTime);
        void Stop();
    }
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        CharacterController controller;
        float verticalSpeed;
        public Vector3 Velocity { get; private set; }
        public MovementState State => controller != null && controller.isGrounded
            ? MovementState.Grounded : MovementState.Airborne;
        void Awake() { controller = GetComponent<CharacterController>(); }
        public void Step(Vector3 planarVelocity, float gravity, float deltaTime)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (!CommandValidation.IsFinite(planarVelocity) || deltaTime <= 0) return;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += gravity * deltaTime;
            Vector3 before = transform.position;
            controller.Move((planarVelocity + Vector3.up * verticalSpeed) * deltaTime);
            Velocity = (transform.position - before) / deltaTime;
        }
        public void Jump(float speed)
        {
            if (controller != null && controller.isGrounded) verticalSpeed = Mathf.Max(0, speed);
        }
        public void ClearVerticalVelocity() { verticalSpeed = 0; }
        public void StepFullVelocity(Vector3 velocity, float deltaTime)
        {
            if (!CommandValidation.IsFinite(velocity) || deltaTime <= 0) return;
            if (controller == null) controller = GetComponent<CharacterController>();
            Vector3 before = transform.position;
            controller.Move(velocity * deltaTime);
            Velocity = (transform.position - before) / deltaTime;
            verticalSpeed = 0;
        }
        public void Face(Vector3 direction, float degreesPerSecond, float deltaTime)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), degreesPerSecond * deltaTime);
        }
    }
}
