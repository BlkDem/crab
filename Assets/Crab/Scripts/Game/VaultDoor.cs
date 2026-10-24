// A golden door that opens only for a price (circle of Greed): even the doors here want gold.
using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class VaultDoor : Interactable
    {
        public int price = 15;
        public Transform doorL, doorR;
        public bool opened;
        public event Action Opened;
        float swing;

        public override string Prompt => $"[E] Открыть золотую дверь — {price} золота";
        public override bool Usable => !opened;
        public override Vector3 Point => transform.position + Vector3.up * 2;

        public override void Use(Hero hero)
        {
            var st = hero.State;
            if (st.Matter[Matter.Gold] < price)
            {
                Story.Hint($"Нужно {price} золота. Здесь даже дверь хочет платы.");
                Sfx.Play("click", null, 1, 0.5f);
                return;
            }
            st.Matter.Remove(Matter.Gold, price);
            Fx.Stream(hero.Core.position, transform, new Color(1f, 0.82f, 0.25f), 12);
            Open(true);
        }

        public void Open(bool show)
        {
            if (opened) return;
            opened = true;
            foreach (var d in new[] { doorL, doorR })
                if (d) foreach (var c in d.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (show)
            {
                Sfx.Play("slam", Point, 0.8f, 0.6f);
                Sfx.Play("beacon", Point, 0.6f, 0.7f);
            }
            else swing = 1;
            Opened?.Invoke();
        }

        void Update()
        {
            if (!opened || swing >= 1) { Pose(); return; }
            swing = Mathf.Min(1, swing + Time.deltaTime / 2.5f);
            Pose();
        }

        void Pose()
        {
            float a = Mathf.SmoothStep(0, 1, swing) * 100;
            if (doorL) doorL.localRotation = Quaternion.Euler(0, -a, 0);
            if (doorR) doorR.localRotation = Quaternion.Euler(0, a, 0);
        }
    }
}
