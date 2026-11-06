// In the storm, a ring on the land under the crab while it is in the air: where it will come down.
// No ring: nothing below it but the abyss. The Gale makes it.
using UnityEngine;

namespace Crab.Game
{
    public sealed class LandingMark : MonoBehaviour
    {
        const int Points = 32;
        const float Radius = 0.7f, Reach = 40;

        public static LandingMark Instance { get; private set; }
        /// <summary>The ring is shown now.</summary>
        public bool Shown => ring && ring.enabled;

        LineRenderer ring;

        void Awake()
        {
            Instance = this;
            ring = gameObject.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = Points;
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2 / Points;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Radius);
            }
            ring.widthMultiplier = 0.09f;
            ring.startColor = ring.endColor = new Color(0.8f, 1f, 0.9f, 0.9f);
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lib = Library.Current;
            if (lib) ring.sharedMaterial = lib.particleAdditive;
            ring.enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            var hero = Hero.Instance;
            bool show = false;
            if (hero && !hero.Controller.Grounded
                && Physics.Raycast(hero.transform.position + Vector3.down * 0.05f, Vector3.down, out var hit, Reach, ~0, QueryTriggerInteraction.Ignore))
            {
                show = true;
                transform.position = hit.point + Vector3.up * 0.08f;
            }
            ring.enabled = show;
        }
    }
}
