#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace io.github.hatayama.uLoopMCP
{
    public class DemoMouseLook : MonoBehaviour
    {
        [SerializeField] private float sensitivity = 0.2f;
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 60f;

        private Transform _cameraTransform = null!;
        private float _pitch;

        private void Awake()
        {
            Camera? cam = GetComponentInChildren<Camera>();
            Debug.Assert(cam != null, "DemoMouseLook requires a Camera in children");
            _cameraTransform = cam!.transform;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            if (delta == Vector2.zero)
            {
                return;
            }

            // Horizontal: rotate character around Y axis
            float yaw = delta.x * sensitivity;
            transform.Rotate(0f, yaw, 0f, Space.World);

            // Vertical: pitch camera only (TPS look up/down)
            _pitch -= delta.y * sensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            Vector3 cameraEuler = _cameraTransform.localEulerAngles;
            cameraEuler.x = _pitch;
            _cameraTransform.localEulerAngles = cameraEuler;
        }
    }
}
#endif
