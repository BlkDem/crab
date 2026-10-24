// Moving the crab: camera-relative, and crab-like — when it just walks, it scuttles sideways
// (the body turns across the direction of travel, to whichever side is closer); when it aims,
// strikes or creates, it faces where the camera looks. Running, jumping, gravity, knockback.
// In a first-person boss scene the body turns with the view.
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CrabController : MonoBehaviour
    {
        public float walk = 3.0f, run = 5.6f, jump = 5.2f, gravity = 18f, turn = 9f;
        public CrabRig rig;
        public CameraRig cam;

        /// <summary>Movement is ignored (cutscenes, menus, the moments after the Spark).</summary>
        [System.NonSerialized] public bool frozen;
        /// <summary>Face the camera's direction (aiming, striking, creating, first person).</summary>
        [System.NonSerialized] public bool faceView;
        /// <summary>Slowed (channelling, creating).</summary>
        [System.NonSerialized] public float speedScale = 1;

        CharacterController cc;
        Vector3 velocity, knock;
        float yaw;
        public bool Grounded => cc && cc.isGrounded;
        public Vector3 Velocity => velocity;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            yaw = transform.eulerAngles.y;
            Controls.Ensure();
        }

        /// <summary>Put the crab somewhere at once (respawn, scene start).</summary>
        public void Teleport(Vector3 pos, float facing)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, facing, 0));
            cc.enabled = true;
            yaw = facing;
            velocity = knock = Vector3.zero;
        }

        public void Knock(Vector3 push) => knock += push;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector2 input = frozen ? Vector2.zero : Controls.Move.ReadValue<Vector2>();
            input = Vector2.ClampMagnitude(input, 1);
            float viewYaw = cam ? cam.Yaw : yaw;
            var fwd = Quaternion.Euler(0, viewYaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, viewYaw, 0) * Vector3.right;
            var wish = fwd * input.y + right * input.x;
            bool running = !frozen && Controls.Sprint.IsPressed() && !faceView;
            float speed = (running ? run : walk) * speedScale;
            var horiz = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), wish * speed, (cc.isGrounded ? 30 : 8) * dt);

            if (cc.isGrounded)
            {
                velocity.y = -2;
                if (!frozen && Controls.Jump.WasPressedThisFrame()) velocity.y = jump;
            }
            else velocity.y -= gravity * dt;
            velocity = new Vector3(horiz.x, velocity.y, horiz.z);
            knock = Vector3.MoveTowards(knock, Vector3.zero, 12 * dt);
            cc.Move((velocity + knock) * dt);

            // facing
            float target = yaw;
            if (faceView) target = viewYaw;
            else if (wish.sqrMagnitude > 0.01f)
            {
                float travel = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
                // sideways: the body's right (+90) or left (-90) leads, whichever needs less turning
                float a = Mathf.DeltaAngle(yaw, travel + 90), b = Mathf.DeltaAngle(yaw, travel - 90);
                // moving roughly toward the view: rather walk straight, so the face is seen
                float straight = Mathf.DeltaAngle(yaw, travel);
                target = Mathf.Abs(Mathf.DeltaAngle(travel, viewYaw)) < 40 ? yaw + straight : Mathf.Abs(a) < Mathf.Abs(b) ? yaw + a : yaw + b;
            }
            yaw = Mathf.LerpAngle(yaw, target, 1 - Mathf.Exp(-turn * dt));
            transform.rotation = Quaternion.Euler(0, yaw, 0);

            if (rig)
            {
                rig.velocity = velocity;
                rig.grounded = cc.isGrounded;
                if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                    rig.groundNormal = hit.normal;
            }
        }
    }
}
