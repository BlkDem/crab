// The circle of Envy is an illusion: a paradise laid over hell. Now and then it slips — for a
// moment the sky goes red, the fog thickens, and the bones and spikes beneath the flowers show.
// The director makes the slips come more often as the story goes on.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class IllusionGlitch : MonoBehaviour
    {
        [Tooltip("What is really there: shown only in the slips")]
        public List<GameObject> truth = new List<GameObject>();
        [Tooltip("What only seems to be there: hidden in the slips")]
        public List<GameObject> seeming = new List<GameObject>();
        public Material trueSky;
        public Color trueFog = new Color(0.35f, 0.05f, 0.04f), trueAmbient = new Color(0.4f, 0.08f, 0.06f);
        [Tooltip("Seconds between slips (0: none)")]
        public float interval = 0;
        public float duration = 0.35f;
        public int Count { get; private set; }

        Material sky;
        Color fog, ambSky, ambEq, ambGround;
        float fogDensity, nextT;
        bool showing;

        void Start()
        {
            foreach (var g in truth) if (g) g.SetActive(false);
            nextT = Time.time + Random.Range(6f, 10f);
        }

        void Update()
        {
            if (interval <= 0 || showing) return;
            if (Time.time < nextT) return;
            nextT = Time.time + interval * Random.Range(0.7f, 1.3f);
            Slip(duration);
        }

        /// <summary>Show the truth for a moment.</summary>
        public void Slip(float seconds)
        {
            if (!showing) StartCoroutine(Show(seconds));
        }

        IEnumerator Show(float seconds)
        {
            showing = true;
            Count++;
            sky = RenderSettings.skybox;
            fog = RenderSettings.fogColor;
            fogDensity = RenderSettings.fogDensity;
            ambSky = RenderSettings.ambientSkyColor;
            ambEq = RenderSettings.ambientEquatorColor;
            ambGround = RenderSettings.ambientGroundColor;
            if (trueSky) RenderSettings.skybox = trueSky;
            RenderSettings.fogColor = trueFog;
            RenderSettings.fogDensity = fogDensity * 3;
            RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = trueAmbient;
            RenderSettings.ambientGroundColor = trueAmbient * 0.5f;
            foreach (var g in truth) if (g) g.SetActive(true);
            foreach (var g in seeming) if (g) g.SetActive(false);
            Sfx.Play("glitch", null, 0.8f);
            Hero.Instance?.Cam.Shake(0.3f);
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime) yield return null;
            RenderSettings.skybox = sky;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;
            foreach (var g in truth) if (g) g.SetActive(false);
            foreach (var g in seeming) if (g) g.SetActive(true);
            showing = false;
        }
    }
}
