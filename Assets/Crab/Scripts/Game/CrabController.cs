// Moving the crab: camera-relative, and crab-like — when it just walks, it scuttles sideways
// (the body turns across the direction of travel, to whichever side is closer); when it aims,
// strikes or creates, it faces where the camera looks. Running, jumping, gravity, knockback.
// In a first-person boss scene the body turns with the view. In the storm (Gale) the wind pushes
// it — less when it wears armour; holding the anchor it stands fast (and can still fight, not walk);
// a jump into a gust is carried far, rising air (Updraft) lifts it, and holding jump it glides.
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
        /// <summary>Slowed by despair (the circle of Despondency), on top of speedScale.</summary>
        [System.NonSerialized] public float moodScale = 1;
        /// <summary>Movement input from a script instead of the player (tests, cutscenes).</summary>
        [System.NonSerialized] public Vector2? autopilot;
        [System.NonSerialized] public bool autopilotRun;
        /// <summary>Wings (level 9): holding jump in the air slows the fall to a glide.</summary>
        [System.NonSerialized] public bool canGlide;
        /// <summary>The storm's circle: the anchor, and a glide on the wind while jump is held.</summary>
        [System.NonSerialized] public bool stormCraft;
        /// <summary>Scripted presses (tests, cutscenes): a jump now; jump held; the anchor held.</summary>
        [System.NonSerialized] public bool autopilotJump;
        [System.NonSerialized] public bool? autopilotHold, autopilotAnchor;
        /// <summary>How much the wind moves it (armour makes it heavier), set by Hero.</summary>
        [System.NonSerialized] public float windScale = 1;
        public bool Anchored { get; private set; }
        /// <summary>The wind's push on it now (m/s).</summary>
        public Vector3 Wind => wind;

        CharacterController cc;
        Vector3 velocity, knock, wind;
        float yaw, mantleCd;
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
            velocity = knock = wind = Vector3.zero;
        }

        public void Knock(Vector3 push) => knock += push;

        /// <summary>How much of the wind moves it on the ground; at the very edge, less: time to react.</summary>
        const float GroundGrip = 0.38f, EdgeGrip = 0.15f;

        /// <summary>The land ends just ahead, where the wind blows.</summary>
        bool EdgeDownwind(Gale gale) => !Physics.Raycast(transform.position + gale.Dir * 1.2f + Vector3.up * 0.5f, Vector3.down, 3, ~0, QueryTriggerInteraction.Ignore);

        /// <summary>In the air, struck against an edge a little above it: up onto the ledge (a jump that
        /// all but made it, made — a gap is meant to be crossed, not missed by a hand's breadth).</summary>
        void Mantle(CollisionFlags flags, Vector3 moving)
        {
            mantleCd -= Time.deltaTime;
            if (cc.isGrounded || (flags & CollisionFlags.Sides) == 0 || velocity.y > 2 || mantleCd > 0) return;
            var ahead = new Vector3(moving.x, 0, moving.z);
            if (ahead.sqrMagnitude < 0.5f) return;
            ahead.Normalize();
            var feet = transform.position;
            if (!Physics.Raycast(feet + Vector3.up * 1.8f + ahead * (cc.radius + 0.35f), Vector3.down, out var hit, 2.0f, ~0, QueryTriggerInteraction.Ignore)) return;
            float rise = hit.point.y - feet.y;
            if (rise < 0.3f || rise > 1.5f || hit.normal.y < 0.7f || hit.collider is CharacterController) return;
            mantleCd = 0.3f;
            cc.Move(Vector3.up * (rise + 0.08f));
            cc.Move(ahead * (cc.radius + 0.3f));
            velocity.y = 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            Vector2 input = frozen ? Vector2.zero : autopilot ?? Controls.Move.ReadValue<Vector2>();
            input = Vector2.ClampMagnitude(input, 1);
            float viewYaw = cam ? cam.Yaw : yaw;
            var fwd = Quaternion.Euler(0, viewYaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, viewYaw, 0) * Vector3.right;
            var wish = fwd * input.y + right * input.x;
            bool running = !frozen && (autopilot.HasValue ? autopilotRun : Controls.Sprint.IsPressed()) && !faceView;
            float speed = (running ? run : walk) * speedScale * moodScale;
            bool jumpNow = !frozen && (autopilotJump || Controls.Jump.WasPressedThisFrame());
            bool jumpHeld = !frozen && (autopilotHold ?? Controls.Jump.IsPressed());
            autopilotJump = false;
            // the anchor: claws into the ground — the wind can't move it, nor can it walk
            Anchored = stormCraft && cc.isGrounded && !frozen && (autopilotAnchor ?? Controls.Anchor.IsPressed());
            if (Anchored) wish = Vector3.zero;
            var horiz = Vector3.MoveTowards(new Vector3(velocity.x, 0, velocity.z), wish * speed, (cc.isGrounded ? 30 : 8) * dt);

            // the storm: on the ground it shoves, in the air it carries (in a gust, far)
            var gale = Gale.Instance;
            var push = gale && !Anchored ? gale.Force(transform.position) * windScale : Vector3.zero;
            float grip = !cc.isGrounded ? 1f : gale && EdgeDownwind(gale) ? EdgeGrip : GroundGrip;
            wind = Vector3.MoveTowards(wind, push * grip, (cc.isGrounded ? 14 : 8) * dt);
            float rise = stormCraft ? Updraft.At(transform.position) : 0;
            if (cc.isGrounded && rise <= 0)
            {
                velocity.y = -2;
                if (jumpNow) velocity.y = jump;
            }
            else
            {
                velocity.y -= gravity * dt * (gale && gale.Gusting && !cc.isGrounded ? 0.6f : 1);   // a gust holds it up
                if (rise > 0) velocity.y = Mathf.MoveTowards(velocity.y, rise, 30 * dt);
                // the Lord of Matter glides on its wings while the jump is held; in the storm, on the wind
                if (canGlide && velocity.y < -1.5f && jumpHeld) velocity.y = -1.5f;
                else if (stormCraft && velocity.y < -3f && jumpHeld) velocity.y = -3f;
            }
            velocity = new Vector3(horiz.x, velocity.y, horiz.z);
            knock = Vector3.MoveTowards(knock, Vector3.zero, 12 * dt);
            var flags = cc.Move((velocity + knock + wind) * dt);
            Mantle(flags, velocity + wind);

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
