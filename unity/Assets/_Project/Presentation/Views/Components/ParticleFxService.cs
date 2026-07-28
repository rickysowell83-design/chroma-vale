using System.Collections;
using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    public class ParticleFxService : MonoBehaviour
    {
        private ParticleSystem _snapImpact;
        private ParticleSystem _traceShortFx;
        private ParticleSystem _traceShortRingFx;
        private ParticleSystem _colorFusionVortex;
        private ParticleSystem _cascadingBloom;
        private ParticleSystem _cascadingBloomBurst;
        private ParticleSystem _victoryFireworks;
        private ParticleSystem _flowHead;
        private ParticleSystem _restorationSpark;
        private ParticleSystem _restorationIgnition;
        private ParticleSystem _restorationRing;
        private ParticleSystem _restorationSustain;

        private void Awake()
        {
            BuildSnapImpact();
            BuildPipeBurst();
            BuildColorFusionVortex();
            BuildCascadingBloom();
            BuildVictoryFireworks();
            BuildFlowHead();
            BuildRestorationPulse();
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

        private void BuildSnapImpact()
        {
            _snapImpact = BuildPooledSystem("SnapImpact", 64);
            var main = _snapImpact.main;
            main.startLifetime = 0.2f;
            main.startSpeed = 0.8f;
            main.startSize = 0.12f;
            main.startColor = Color.white;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _snapImpact.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 12)
            });

            var shape = _snapImpact.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.1f;

            var colorOverLifetime = _snapImpact.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.cyan, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = _snapImpact.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        public void PlacementPuff(Vector3 position, Color color)
        {
            var main = _snapImpact.main;
            main.startColor = color;
            _snapImpact.transform.position = position;
            _snapImpact.Play();
        }

        private void BuildPipeBurst()
        {
            _traceShortFx = BuildPooledSystem("TraceShort", 64);
            var main = _traceShortFx.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 4.0f;
            main.startSize = 0.22f;
            main.gravityModifier = 0.5f;
            main.loop = false;
            main.playOnAwake = false;

            var colorOverLifetime = _traceShortFx.colorOverLifetime;
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

            var emission = _traceShortFx.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 50)
            });

            var shape = _traceShortFx.shape;
            shape.enabled = false;

            // Sub-emitter ring
            _traceShortRingFx = BuildPooledSystem("TraceShortRing", 32);
            var ringMain = _traceShortRingFx.main;
            ringMain.startLifetime = 0.4f;
            ringMain.startSpeed = 2.0f;
            ringMain.startSize = 0.15f;
            ringMain.startColor = new Color(1f, 0.3f, 0f);
            ringMain.loop = false;
            ringMain.playOnAwake = false;
            ringMain.startDelay = 0.15f;

            var ringEmission = _traceShortRingFx.emission;
            ringEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 12)
            });

            var ringShape = _traceShortRingFx.shape;
            ringShape.enabled = true;
            ringShape.shapeType = ParticleSystemShapeType.Sphere;
            ringShape.radius = 0.3f;

            var ringSizeOLT = _traceShortRingFx.sizeOverLifetime;
            ringSizeOLT.enabled = true;
            var ringCurve = new AnimationCurve();
            ringCurve.AddKey(0f, 0.5f);
            ringCurve.AddKey(1f, 1.5f);
            ringSizeOLT.size = new ParticleSystem.MinMaxCurve(1f, ringCurve);

            // Link sub-emitter
            var subEmitters = _traceShortFx.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(_traceShortRingFx, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritEverything);

            // Noise module
            var noise = _traceShortFx.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f);
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.1f;

            // Trails module
            var trails = _traceShortFx.trails;
            trails.enabled = true;
            trails.ratio = 0.3f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.15f);
            trails.minVertexDistance = 0.05f;
        }

        public void BurstExplosion(Vector3 position)
        {
            _traceShortFx.transform.position = position;
            _traceShortFx.Play();
        }

        private void BuildColorFusionVortex()
        {
            _colorFusionVortex = BuildPooledSystem("ColorFusionVortex", 64);
            var main = _colorFusionVortex.main;
            main.startLifetime = 0.7f;
            main.startSpeed = 1.5f;
            main.startSize = 0.14f;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _colorFusionVortex.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 20)
            });

            var shape = _colorFusionVortex.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.15f;

            var rotationOverLifetime = _colorFusionVortex.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(45f, -45f);

            var sizeOverLifetime = _colorFusionVortex.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 0.5f);
            curve.AddKey(0.5f, 1.0f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        public void MixSwirl(Vector3 position, Color colorA, Color colorB)
        {
            var main = _colorFusionVortex.main;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            _colorFusionVortex.transform.position = position;
            _colorFusionVortex.Play();
        }

        private void BuildCascadingBloom()
        {
            _cascadingBloom = BuildPooledSystem("CascadingBloom", 64);
            var main = _cascadingBloom.main;
            main.startLifetime = 1.0f;
            main.startSpeed = 0.3f;
            main.startSize = 0.15f;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _cascadingBloom.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 35)
            });

            var shape = _cascadingBloom.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.arc = 360f;

            var sizeOverLifetime = _cascadingBloom.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = _cascadingBloom.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(Color.clear, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Sub-emitter burst
            _cascadingBloomBurst = BuildPooledSystem("CascadingBloomBurst", 32);
            var burstMain = _cascadingBloomBurst.main;
            burstMain.startLifetime = 0.5f;
            burstMain.startSpeed = 1.5f;
            burstMain.startSize = 0.1f;
            burstMain.startColor = Color.white;
            burstMain.loop = false;
            burstMain.playOnAwake = false;
            burstMain.startDelay = 0.4f;

            var burstEmission = _cascadingBloomBurst.emission;
            burstEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 8)
            });

            var burstShape = _cascadingBloomBurst.shape;
            burstShape.enabled = true;
            burstShape.shapeType = ParticleSystemShapeType.Circle;
            burstShape.radius = 0.2f;
            burstShape.arc = 360f;

            var burstSizeOLT = _cascadingBloomBurst.sizeOverLifetime;
            burstSizeOLT.enabled = true;
            var burstCurve = new AnimationCurve();
            burstCurve.AddKey(0f, 1f);
            burstCurve.AddKey(1f, 2f);
            burstSizeOLT.size = new ParticleSystem.MinMaxCurve(1f, burstCurve);

            // Link sub-emitter
            var subEmitters = _cascadingBloom.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(_cascadingBloomBurst, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritEverything);
        }

        public void TargetBloom(Vector3 position, Color color)
        {
            var main = _cascadingBloom.main;
            main.startColor = color;
            _cascadingBloom.transform.position = position;
            _cascadingBloom.Play();
        }

        private void BuildVictoryFireworks()
        {
            _victoryFireworks = BuildPooledSystem("VictoryFireworks", 64);
            var main = _victoryFireworks.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.5f;
            main.startSize = 0.2f;
            main.loop = false;
            main.playOnAwake = false;

            var emission = _victoryFireworks.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 40)
            });

            var shape = _victoryFireworks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;

            var sizeOverLifetime = _victoryFireworks.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = _victoryFireworks.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.cyan, 0f),
                    new GradientColorKey(Color.magenta, 0.5f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var noise = _victoryFireworks.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.2f);
        }

        public void WinCascade(Vector3[] positions)
        {
            StartCoroutine(VictoryFireworksRoutine(positions));
        }

        private IEnumerator VictoryFireworksRoutine(Vector3[] positions)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                _victoryFireworks.transform.position = positions[i];
                float t = positions.Length > 1 ? (float)i / (positions.Length - 1) : 0f;
                var main = _victoryFireworks.main;
                main.startColor = Color.Lerp(Color.white, Color.cyan, t);
                _victoryFireworks.Play();
                yield return new WaitForSeconds(0.04f);
            }
        }

        private void BuildFlowHead()
        {
            _flowHead = BuildPooledSystem("FlowHead", 32);
            var main = _flowHead.main;
            main.startLifetime = 0.15f;
            main.startSpeed = 0f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.5f, 0.8f, 1f);
            main.loop = false;
            main.playOnAwake = false;

            var emission = _flowHead.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 3)
            });

            var shape = _flowHead.shape;
            shape.enabled = false;
        }

        // ── Restoration Pulse (v3) ─────────────────────────────────────
        // SPARK → IGNITION → RING EXPANSION → SUSTAIN

        private void BuildRestorationPulse()
        {
            // Phase 1: SPARK — bright initial flash burst
            _restorationSpark = BuildPooledSystem("RestorationSpark", 32);
            var sparkMain = _restorationSpark.main;
            sparkMain.startLifetime = 0.25f;
            sparkMain.startSpeed = 0.3f;
            sparkMain.startSize = 0.08f;
            sparkMain.startColor = Color.white;
            sparkMain.loop = false;
            sparkMain.playOnAwake = false;

            var sparkEmission = _restorationSpark.emission;
            sparkEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 16)
            });

            var sparkShape = _restorationSpark.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.05f;

            var sparkGradient = new Gradient();
            sparkGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(ChromaPalette.ViaCyan, 0.5f),
                    new GradientColorKey(ChromaPalette.NeonCyan, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            var sparkColor = _restorationSpark.colorOverLifetime;
            sparkColor.enabled = true;
            sparkColor.color = new ParticleSystem.MinMaxGradient(sparkGradient);

            // Phase 2: IGNITION — expanding fire orange/cyan burst
            _restorationIgnition = BuildPooledSystem("RestorationIgnition", 48);
            var ignMain = _restorationIgnition.main;
            ignMain.startLifetime = 0.6f;
            ignMain.startSpeed = 1.5f;
            ignMain.startSize = 0.12f;
            ignMain.startColor = new Color(1f, 0.8f, 0.2f); // Hot orange
            ignMain.loop = false;
            ignMain.playOnAwake = false;
            ignMain.startDelay = 0.1f; // Follows spark

            var ignEmission = _restorationIgnition.emission;
            ignEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 24)
            });

            var ignShape = _restorationIgnition.shape;
            ignShape.enabled = true;
            ignShape.shapeType = ParticleSystemShapeType.Sphere;
            ignShape.radius = 0.15f;

            var ignGradient = new Gradient();
            ignGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                    new GradientColorKey(ChromaPalette.ViaCyan, 0.7f),
                    new GradientColorKey(Color.clear, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            var ignColor = _restorationIgnition.colorOverLifetime;
            ignColor.enabled = true;
            ignColor.color = new ParticleSystem.MinMaxGradient(ignGradient);

            // Phase 3: RING EXPANSION — expanding circle ring
            _restorationRing = BuildPooledSystem("RestorationRing", 64);
            var ringMain = _restorationRing.main;
            ringMain.startLifetime = 0.8f;
            ringMain.startSpeed = 1.0f;
            ringMain.startSize = 0.06f;
            ringMain.startColor = ChromaPalette.ViaCyan;
            ringMain.loop = false;
            ringMain.playOnAwake = false;
            ringMain.startDelay = 0.2f; // Follows ignition

            var ringEmission = _restorationRing.emission;
            ringEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 36)
            });

            var ringShape = _restorationRing.shape;
            ringShape.enabled = true;
            ringShape.shapeType = ParticleSystemShapeType.Circle;
            ringShape.radius = 0.1f;
            ringShape.arc = 360f;

            var ringSize = _restorationRing.sizeOverLifetime;
            ringSize.enabled = true;
            var ringCurve = new AnimationCurve();
            ringCurve.AddKey(0f, 0.2f);
            ringCurve.AddKey(0.5f, 1.5f);
            ringCurve.AddKey(1f, 2.5f);
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, ringCurve);

            var ringAlpha = _restorationRing.colorOverLifetime;
            ringAlpha.enabled = true;
            var ringAlphaGrad = new Gradient();
            ringAlphaGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(ChromaPalette.ViaCyan, 0f),
                    new GradientColorKey(ChromaPalette.NeonCyan, 0.5f),
                    new GradientColorKey(Color.clear, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            ringAlpha.color = new ParticleSystem.MinMaxGradient(ringAlphaGrad);

            // Phase 4: SUSTAIN — lingering cyan glow particles
            _restorationSustain = BuildPooledSystem("RestorationSustain", 32);
            var susMain = _restorationSustain.main;
            susMain.startLifetime = 1.5f;
            susMain.startSpeed = 0.15f;
            susMain.startSize = 0.05f;
            susMain.startColor = ChromaPalette.ViaCyan;
            susMain.loop = false;
            susMain.playOnAwake = false;
            susMain.startDelay = 0.4f; // After ring starts

            var susEmission = _restorationSustain.emission;
            susEmission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 20)
            });

            var susShape = _restorationSustain.shape;
            susShape.enabled = true;
            susShape.shapeType = ParticleSystemShapeType.Sphere;
            susShape.radius = 0.3f;

            var susNoise = _restorationSustain.noise;
            susNoise.enabled = true;
            susNoise.strength = new ParticleSystem.MinMaxCurve(0.15f);
            susNoise.frequency = 0.2f;

            var susColor = _restorationSustain.colorOverLifetime;
            susColor.enabled = true;
            var susGrad = new Gradient();
            susGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(ChromaPalette.ViaCyan, 0f),
                    new GradientColorKey(ChromaPalette.NeonCyan, 0.6f),
                    new GradientColorKey(Color.clear, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.3f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            susColor.color = new ParticleSystem.MinMaxGradient(susGrad);
        }

        /// <summary>
        /// Fire the Restoration Pulse at a target via pad — all 4 phases:
        /// SPARK → IGNITION → RING EXPANSION → SUSTAIN
        /// </summary>
        public void RestorationPulse(Vector3 position)
        {
            _restorationSpark.transform.position = position;
            _restorationIgnition.transform.position = position;
            _restorationRing.transform.position = position;
            _restorationSustain.transform.position = position;

            _restorationSpark.Play();
            _restorationIgnition.Play();
            _restorationRing.Play();
            _restorationSustain.Play();
        }

        public void FlowHeadPulse(Vector3 position, Color color)
        {
            var main = _flowHead.main;
            main.startColor = color;
            _flowHead.transform.position = position;
            _flowHead.Play();
        }
    }
}
