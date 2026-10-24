// The crab's factory (circle of Violence): a hall it raises beside the captured forge. Once it
// stands, it builds battle robots one after another — the crab's own army — up to a full company.
using UnityEngine;

namespace Crab.Game
{
    public sealed class RobotFactory : MonoBehaviour
    {
        public Transform door;
        public float every = 10;
        public int company = 6;
        public bool running;
        float t = 2;

        void Update()
        {
            if (!running) return;
            var hero = Hero.Instance;
            var lib = Library.Current;
            if (!hero || !lib || !lib.battleBot) return;
            BattleBot.All.RemoveAll(b => !b);
            if (BattleBot.All.Count >= company) return;
            t -= Time.deltaTime;
            if (t > 0) return;
            t = every;
            Make(hero, lib);
        }

        public BattleBot Make(Hero hero, Library lib)
        {
            var at = door ? door.position : transform.position + transform.forward * 4;
            var go = Instantiate(lib.battleBot, at, transform.rotation);
            go.name = "battle robot";
            var b = go.GetComponent<BattleBot>();
            b.hero = hero;
            int n = BattleBot.All.Count;
            float a = n / (float)company * Mathf.PI * 2;
            b.slot = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.5f;
            Fx.Assemble(go.transform, new Color(0.5f, 0.85f, 1f), 0.8f);
            Sfx.Play("create", at, 0.6f, 1.1f);
            return b;
        }
    }
}
