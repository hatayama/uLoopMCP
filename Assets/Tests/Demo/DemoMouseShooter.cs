#if ULOOPMCP_HAS_INPUT_SYSTEM
#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

namespace io.github.hatayama.uLoopMCP
{
    public class DemoMouseShooter : MonoBehaviour
    {
        [SerializeField] private float bulletSpeed = 20f;
        [SerializeField] private float bulletLifetime = 3f;
        [SerializeField] private float bulletSpawnOffset = 0.3f;

        private void Update()
        {
            Mouse? mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                FireBullet();
            }
        }

        private void FireBullet()
        {
            Vector3 fireDirection = transform.forward;

            Vector3 spawnPosition = transform.position + fireDirection * bulletSpawnOffset + Vector3.up;
            GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bullet.name = "Bullet";
            bullet.transform.position = spawnPosition;
            bullet.transform.localScale = Vector3.one * 0.3f;

            Renderer renderer = bullet.GetComponent<Renderer>();
            renderer.material.color = Color.yellow;

            Rigidbody rb = bullet.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.AddForce(fireDirection * bulletSpeed, ForceMode.VelocityChange);

            Destroy(bullet, bulletLifetime);
        }
    }
}
#endif
