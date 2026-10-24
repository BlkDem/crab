// Bridges of matter (circle of Lies, level 8): B lays a slab of stone and metal ahead of the crab,
// level with its feet — a path where there was none. They crumble after a while.
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class BridgeBuilder : MonoBehaviour
    {
        public Hero hero;
        public float length = 5, life = 60;
        public int max = 8;
        public static readonly Cost Price = new Cost(0, 1, 1);
        readonly List<GameObject> slabs = new List<GameObject>();

        void Update()
        {
            if (hero && hero.Alive && !hero.Combat.locked && Time.timeScale > 0 && Controls.Bridge.WasPressedThisFrame()) Lay();
        }

        /// <summary>Lay a slab ahead (the B key; tests call it).</summary>
        public GameObject Lay()
        {
            var st = hero.State;
            if (!st.Matter.Pay(Price))
            {
                Story.Hint($"Для моста нужно: {Price}");
                return null;
            }
            slabs.RemoveAll(s => !s);
            if (slabs.Count >= max) { Destroy(slabs[0]); slabs.RemoveAt(0); }
            var fwd = hero.Cam ? Quaternion.Euler(0, hero.Cam.Yaw, 0) * Vector3.forward : hero.transform.forward;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "matter bridge";
            go.transform.SetPositionAndRotation(hero.transform.position + fwd * (length / 2 + 0.3f) + Vector3.down * 0.15f, Quaternion.LookRotation(fwd));
            go.transform.localScale = new Vector3(2.2f, 0.3f, length);
            var lib = Library.Current;
            var r = go.GetComponent<Renderer>();
            if (lib && lib.bridge) r.sharedMaterial = lib.bridge;
            slabs.Add(go);
            Destroy(go, life);
            Fx.Assemble(go.transform, new Color(0.8f, 0.85f, 1f), 0.5f);
            Sfx.Play("create", go.transform.position, 0.7f, 0.8f);
            return go;
        }
    }
}
