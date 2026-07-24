using System.Collections;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class ParticleFxService : MonoBehaviour
    {
        private ParticleSystem _placementPuff;
        private ParticleSystem _burstExplosion;
        private ParticleSystem _mixSwirl;
        private ParticleSystem _targetBloom;
        private ParticleSystem _winCascade;

        private void Awake()
        {
            BuildPlacementPuff();
            BuildBurstExplosion();
            BuildMixSwirl();
            BuildTargetBloom();
            BuildWinCascade();
        }

        private static Material GetParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            return new Material(shader);
        }

        private ParticleSystem BuildPooledSystem(string name, int maxParticles = 64)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetParticleMaterial();
            renderer.sortingOrder = 50;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private void BuildPlacementPuff()
        {
            _placementPuff = BuildPooledSystem("PlacementPuff", 64);
            var main = _placementPuff.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 0.3f;
            main.startSize = 0.08f;
            main.startColor = Color.white;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _placementPuff.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 8)
            });

            var shape = _placementPuff.shape;
            shape.enabled = false;
        }

        public void PlacementPuff(Vector3 position, Color color)
        {
            var main = _placementPuff.main;
            main.startColor = color;
            _placementPuff.transform.position = position;
            _placementPuff.Play();
        }

        private void BuildBurstExplosion()
        {
            _burstExplosion = BuildPooledSystem("BurstExplosion", 64);
            var main = _burstExplosion.main;
            main.startLifetime = 0.6f;
            main.startSpeed = 2.5f;
            main.startSize = 0.15f;
            main.gravityModifier = 0.5f;
            main.loop = false;
            main.playOnAwake = false;

            var colorOverLifetime = _burstExplosion.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 0f),
                    new GradientColorKey(new Color(0.5f, 0f, 0f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var emission = _burstExplosion.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 30)
            });

            var shape = _burstExplosion.shape;
            shape.enabled = false;
        }

        public void BurstExplosion(Vector3 position)
        {
            _burstExplosion.transform.position = position;
            _burstExplosion.Play();
        }

        private void BuildMixSwirl()
        {
            _mixSwirl = BuildPooledSystem("MixSwirl", 64);
            var main = _mixSwirl.main;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.startSize = 0.1f;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _mixSwirl.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 12)
            });

            var shape = _mixSwirl.shape;
            shape.enabled = false;
        }

        public void MixSwirl(Vector3 position, Color colorA, Color colorB)
        {
            var main = _mixSwirl.main;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            _mixSwirl.transform.position = position;
            _mixSwirl.Play();
        }

        private void BuildTargetBloom()
        {
            _targetBloom = BuildPooledSystem("TargetBloom", 64);
            var main = _targetBloom.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            main.loop = false;
            main.playOnAwake = false;

            var shape = _targetBloom.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;

            var sizeOverLifetime = _targetBloom.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var emission = _targetBloom.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 20)
            });
        }

        public void TargetBloom(Vector3 position, Color color)
        {
            var main = _targetBloom.main;
            main.startColor = color;
            _targetBloom.transform.position = position;
            _targetBloom.Play();
        }

        private void BuildWinCascade()
        {
            _winCascade = BuildPooledSystem("WinCascade", 64);
            var main = _winCascade.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 0f;
            main.startSize = 0.15f;
            main.loop = false;
            main.playOnAwake = false;

            var shape = _winCascade.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;

            var sizeOverLifetime = _winCascade.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var emission = _winCascade.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 20)
            });
        }

        public void WinCascade(Vector3[] positions)
        {
            StartCoroutine(WinCascadeRoutine(positions));
        }

        private IEnumerator WinCascadeRoutine(Vector3[] positions)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                _winCascade.transform.position = positions[i];
                float t = positions.Length > 1 ? (float)i / (positions.Length - 1) : 0f;
                var main = _winCascade.main;
                main.startColor = Color.Lerp(Color.white, Color.cyan, t);
                _winCascade.Play();
                yield return new WaitForSeconds(0.04f);
            }
        }
    }
}
