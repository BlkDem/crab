using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>The storm: light dims to a bruised grey, rain, lightning and thunder.</summary>
    public sealed class Storm : MonoBehaviour
    {
        public Light sun;
        public ParticleSystem rain;
        [Range(0, 1)] public float amount;
        float target, boltT = 3;
        Color sunDay, ambientDay, fogDay;
        float sunI, fogD;

        void Start()
        {
            if (sun) { sunDay = sun.color; sunI = sun.intensity; }
            ambientDay = RenderSettings.ambientLight;
            fogDay = RenderSettings.fogColor;
            fogD = RenderSettings.fogDensity;
        }

        public void Brew(float to) => target = to;

        void Update()
        {
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * 0.15f);
            if (sun)
            {
                sun.color = Color.Lerp(sunDay, new Color(0.45f, 0.5f, 0.62f), amount);
                sun.intensity = Mathf.Lerp(sunI, sunI * 0.25f, amount);
            }
            RenderSettings.ambientLight = Color.Lerp(ambientDay, new Color(0.2f, 0.23f, 0.3f), amount);
            RenderSettings.fogColor = Color.Lerp(fogDay, new Color(0.25f, 0.28f, 0.33f), amount);
            RenderSettings.fogDensity = Mathf.Lerp(fogD, fogD * 3, amount);
            if (rain)
            {
                var em = rain.emission;
                em.rateOverTime = amount * 900;
                if (Hero.Instance) rain.transform.position = Hero.Instance.transform.position + Vector3.up * 12;
            }
            Sfx.Loop("rain", amount * 0.7f);
            if (amount > 0.6f)
            {
                boltT -= Time.deltaTime;
                if (boltT <= 0)
                {
                    boltT = Random.Range(3f, 8f);
                    var at = (Hero.Instance ? Hero.Instance.transform.position : Vector3.zero) + new Vector3(Random.Range(-40f, 40f), 0, Random.Range(-60f, -20f));
                    Bolt(at);
                }
            }
        }

        public void Bolt(Vector3 at)
        {
            Fx.Lightning(at + Vector3.up * 60, at, new Color(0.85f, 0.9f, 1f));
            StartCoroutine(Thunder(Vector3.Distance(at, Camera.main ? Camera.main.transform.position : at) / 340f));
        }

        IEnumerator Thunder(float delay)
        {
            yield return new WaitForSeconds(Mathf.Min(delay, 3));
            Sfx.Play("thunder", null, 0.9f, Random.Range(0.85f, 1.1f));
        }
    }
}
