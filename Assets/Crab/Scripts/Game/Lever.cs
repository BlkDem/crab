// A lever on the wall of the labyrinth: pulled, it opens its gates.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Lever : Interactable
    {
        public List<MechGate> gates = new List<MechGate>();
        public Transform handle;
        public bool pulled;
        public event Action Pulled;

        public override string Prompt => "[E] Дёрнуть рычаг";
        public override bool Usable => !pulled;
        public override Vector3 Point => transform.position + Vector3.up * 1.1f;

        public override void Use(Hero hero)
        {
            if (pulled) return;
            pulled = true;
            if (handle) handle.localRotation = Quaternion.Euler(70, 0, 0);
            Sfx.Play("click", Point, 1, 0.5f);
            Sfx.Play("slam", Point, 0.4f, 1.6f);
            foreach (var g in gates) if (g) g.Set(true);
            Pulled?.Invoke();
        }
    }
}
