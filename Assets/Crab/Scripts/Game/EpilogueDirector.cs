// The end. Beyond the portal: hell lies broken behind, the crab stands on a stone in open space,
// the Earth turning before it. The AI says the mission is done. The crab looks at the Earth, and
// after a pause says: no. Hell was only the first threat. The camera draws back — and out there
// are thousands of other worlds. Then dark, and the last words.
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class EpilogueDirector : MonoBehaviour
    {
        public Transform earth, worlds;
        public float spin = 3;
        public bool done;

        Hero hero;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            var st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.StopAll();
            Story.Task("");
            GameUI.Cinema = true;
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Power.locked = true;
            var face = hero.transform.position + Vector3.up * 0.4f;
            // the crab before the Earth
            hero.Cam.Shot(face + hero.transform.forward * 2.2f + Vector3.up * 0.6f + hero.transform.right * 0.8f, face);
            yield return Wait(2.5f);
            Story.AI("Миссия выполнена.", 4);
            yield return Wait(4.5f);
            hero.Cam.Shot(face - hero.transform.forward * 1.8f + Vector3.up * 1.1f, earth.position + Vector3.up * 12);
            yield return Wait(3);
            Story.Think("…", 2.5f);
            yield return Wait(3.5f);
            Story.Think("Нет.", 3.5f);
            yield return Wait(4);
            Story.Think("Ад был только первой угрозой.", 4);
            yield return Wait(4.5f);
            // the camera draws back: worlds beyond worlds
            var from = face - hero.transform.forward * 1.8f + Vector3.up * 1.1f;
            var away = (-hero.transform.forward + Vector3.up * 0.35f).normalized;
            for (float t = 0; t < 12; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / 12);
                var p = from + away * Mathf.Lerp(0, 2200, k * k);
                hero.Cam.Shot(p, Vector3.Lerp(earth.position, hero.transform.position, k * 0.6f));
                yield return null;
            }
            for (float t = 0; t < 2; t += Time.deltaTime) { GameSession.Instance.fade = t / 2; yield return null; }
            GameSession.Instance.fade = 1;
            Story.Title("КРАБ ВЕРНЁТСЯ.", "");
            st.SetFlag("game_done");
            GameSession.Instance.Save();
            done = true;
            yield return Wait(7);
            GameSession.Instance.Go("Menu", 1.5f);
        }

        void OnDestroy() => GameUI.Cinema = false;

        void Update()
        {
            if (earth) earth.Rotate(0, spin * Time.deltaTime, 0, Space.Self);
            if (worlds) worlds.Rotate(0, 0.2f * Time.deltaTime, 0, Space.World);
        }

        static IEnumerator Wait(float s)
        {
            for (float t = 0; t < s; t += Time.deltaTime) yield return null;
        }
    }
}
