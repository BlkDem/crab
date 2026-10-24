// The camera: third person orbiting the crab (pulled in by walls), over the shoulder when
// aiming, first person from the crab's eyes for boss fights, and scripted shots for story
// moments. Changes between them are blended; shake on hits and explosions.
using UnityEngine;

namespace Crab.Game
{
    public enum CamMode { Third, Aim, First, Scripted }

    public sealed class CameraRig : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Where the first-person view sits (between the crab's eyes)")]
        public Transform eye;
        public Camera cam;
        public float distance = 3.6f, minDistance = 1.6f, maxDistance = 7f, height = 0.75f;
        public CamMode Mode { get; private set; } = CamMode.Third;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 18;
        [System.NonSerialized] public bool inputLocked;

        Vector3 scriptedPos, scriptedLook;
        float blend = 1, shake, fovKick;
        Vector3 lastPos;
        Quaternion lastRot;

        void Awake()
        {
            if (!cam) cam = GetComponent<Camera>();
            Controls.Ensure();
            if (target) Yaw = target.eulerAngles.y;
        }

        public void SetMode(CamMode m)
        {
            if (m == Mode) return;
            Mode = m;
            blend = 0;
            lastPos = transform.position;
            lastRot = transform.rotation;
        }

        /// <summary>Look from pos at look (story moments).</summary>
        public void Shot(Vector3 pos, Vector3 look)
        {
            scriptedPos = pos;
            scriptedLook = look;
            SetMode(CamMode.Scripted);
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        public void Kick(float fov) => fovKick = Mathf.Max(fovKick, fov);

        /// <summary>Turn the view to face a direction (after a respawn, entering a boss scene).</summary>
        public void Face(float yaw, float pitch = 15)
        {
            Yaw = yaw;
            Pitch = pitch;
        }

        void LateUpdate()
        {
            if (!target) return;
            float dt = Time.unscaledDeltaTime;
            if (!inputLocked && Time.timeScale > 0)
            {
                var d = Controls.LookDelta;
                Yaw += d.x;
                Pitch = Mathf.Clamp(Pitch - d.y, Mode == CamMode.First ? -70 : -35, Mode == CamMode.First ? 70 : 75);
                if (Mode == CamMode.Third)
                {
                    float wheel = Controls.Scroll.ReadValue<float>();
                    if (Mathf.Abs(wheel) > 0.01f && !Controls.Aim.IsPressed()) distance = Mathf.Clamp(distance * (1 - Mathf.Sign(wheel) * 0.08f), minDistance, maxDistance);
                }
            }
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 pos;
            Quaternion look;
            float fov = 60;
            switch (Mode)
            {
                case CamMode.First:
                    pos = eye ? eye.position : target.position + Vector3.up * 0.35f;
                    look = rot;
                    fov = 72;
                    break;
                case CamMode.Scripted:
                    pos = scriptedPos;
                    look = Quaternion.LookRotation(scriptedLook - scriptedPos);
                    fov = 50;
                    break;
                default:
                {
                    bool aim = Mode == CamMode.Aim;
                    var pivot = target.position + Vector3.up * (aim ? height + 0.1f : height);
                    float dist = aim ? 1.9f : distance;
                    var offset = rot * new Vector3(aim ? 0.7f : 0, 0, -dist);
                    pos = pivot + offset;
                    // walls pull the camera in
                    var dir = pos - pivot;
                    if (Physics.SphereCast(pivot, 0.22f, dir.normalized, out var hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(target))
                        pos = pivot + dir.normalized * Mathf.Max(0.4f, hit.distance - 0.05f);
                    look = rot;
                    fov = aim ? 48 : 60;
                    break;
                }
            }
            if (shake > 0)
            {
                pos += Random.insideUnitSphere * shake * 0.12f;
                look *= Quaternion.Euler(Random.Range(-1f, 1f) * shake * 2, Random.Range(-1f, 1f) * shake * 2, 0);
                shake = Mathf.MoveTowards(shake, 0, dt * 3);
            }
            blend = Mathf.MoveTowards(blend, 1, dt * (Mode == CamMode.Scripted ? 0.8f : 3.5f));
            float k = Mathf.SmoothStep(0, 1, blend);
            transform.SetPositionAndRotation(Vector3.Lerp(lastPos, pos, k), Quaternion.Slerp(lastRot, look, k));
            if (blend >= 1) { lastPos = pos; lastRot = look; }
            fovKick = Mathf.MoveTowards(fovKick, 0, dt * 30);
            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov + fovKick, dt * 8);
        }

        /// <summary>The ray the crab aims along (from the centre of the screen).</summary>
        public Ray AimRay => cam ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f)) : new Ray(transform.position, transform.forward);
    }
}
