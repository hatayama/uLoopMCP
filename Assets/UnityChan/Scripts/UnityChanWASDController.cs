using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityChan
{
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class UnityChanWASDController : MonoBehaviour
    {
        [SerializeField] private float runSpeed = 5.0f;
        [SerializeField] private float walkSpeed = 2.0f;
        [SerializeField] private float jumpPower = 3.0f;
        [SerializeField] private float rotationSmoothTime = 0.1f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int JumpHeightHash = Animator.StringToHash("JumpHeight");
        private static readonly int GravityControlHash = Animator.StringToHash("GravityControl");

        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int LocoState = Animator.StringToHash("Base Layer.Locomotion");
        private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");
        private static readonly int DamagedState = Animator.StringToHash("Base Layer.DAMAGED01");

        private float rotationVelocity;
        private bool jumpRequested;
        private bool colliderDirty;

        private Animator animator;
        private Rigidbody rb;
        private CapsuleCollider col;
        private float orgColHeight;
        private Vector3 orgColCenter;

        private void Start()
        {
            animator = GetComponent<Animator>();
            col = GetComponent<CapsuleCollider>();
            rb = GetComponent<Rigidbody>();
            orgColHeight = col.height;
            orgColCenter = col.center;
        }

        // wasPressedThisFrame is frame-based; FixedUpdate can miss one-frame inputs such as Space and Enter.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                RestartDamagedMotion();
            }
        }

        private void FixedUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector2 input = ReadMoveInput(keyboard);
            Vector3 moveDirection = new Vector3(input.x, 0, input.y);
            float magnitude = Mathf.Min(moveDirection.magnitude, 1f);

            bool isWalking = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            // Locomotion blend tree: 0 = Walks, 0.8 = Runs
            float animSpeed = magnitude > 0.1f
                ? (isWalking ? 0.3f : 1.0f)
                : 0f;
            animator.SetFloat(SpeedHash, animSpeed);

            if (magnitude > 0.1f)
            {
                float currentSpeed = isWalking ? walkSpeed : runSpeed;
                float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0, smoothedAngle, 0);

                Vector3 velocity = transform.forward * (magnitude * currentSpeed);
                transform.localPosition += velocity * Time.fixedDeltaTime;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

            if (jumpRequested)
            {
                HandleJumpInput(currentState);
                jumpRequested = false;
            }

            HandleStateLogic(currentState);
        }

        private static Vector2 ReadMoveInput(Keyboard keyboard)
        {
            float x = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;

            float y = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;

            return new Vector2(x, y);
        }

        private void HandleJumpInput(AnimatorStateInfo currentState)
        {
            int stateHash = currentState.fullPathHash;
            if ((stateHash == LocoState || stateHash == IdleState) && !animator.IsInTransition(0))
            {
                rb.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
                animator.SetBool(JumpHash, true);
            }
        }

        private void HandleStateLogic(AnimatorStateInfo currentState)
        {
            int stateHash = currentState.fullPathHash;

            if (stateHash == LocoState || stateHash == IdleState || stateHash == DamagedState)
            {
                rb.useGravity = true;
                if (colliderDirty)
                {
                    col.height = orgColHeight;
                    col.center = orgColCenter;
                    colliderDirty = false;
                }
            }
            else if (stateHash == JumpState && !animator.IsInTransition(0))
            {
                float gravityControl = animator.GetFloat(GravityControlHash);
                if (gravityControl > 0)
                    rb.useGravity = false;
                else
                    rb.useGravity = true;

                float jumpHeight = animator.GetFloat(JumpHeightHash);
                col.height = orgColHeight - jumpHeight;
                col.center = new Vector3(0, orgColCenter.y + jumpHeight, 0);
                colliderDirty = true;

                animator.SetBool(JumpHash, false);
            }
        }

        private void RestartDamagedMotion()
        {
            Debug.Assert(animator != null, "animator must be initialized before damaged motion is requested");
            // Play with normalizedTime = 0 so repeated Enter presses restart the motion immediately.
            animator.Play(DamagedState, 0, 0f);
        }
    }
}
