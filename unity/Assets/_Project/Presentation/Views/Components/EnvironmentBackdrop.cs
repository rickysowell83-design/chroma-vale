using UnityEngine;

namespace ChromaVale.Presentation.Views.Components
{
    /// <summary>
    /// Environment backdrop: warm cream vertical gradient quad behind the board
    /// plus a handful of slow-drifting light motes (warm-white, additive, 10-20% opacity).
    /// Replaces the deleted cyberpunk EnvironmentBackdrop.
    /// </summary>
    public class EnvironmentBackdrop : MonoBehaviour
    {
        private const string QuadShaderName = "Sprites/Default";
        private const string ParticleShaderUrp = "Universal Render Pipeline/Particles/Unlit";
        private const string ParticleShaderFallback = "Sprites/Default";

        // §1.5 — warm cream gradient (top → bottom slightly warmer)
        private static readonly Color GradientTop = ChromaPalette.PCB_Substrate;          // #F8F4E8
        private static readonly Color GradientBottom = new Color(0.9412f, 0.9176f, 0.8471f); // #F0EAD8

        // Warm-white mote: 10-20% opacity, additive
        private static readonly Color MoteColor = new Color(1f, 0.98f, 0.92f, 0.15f);

        public void Build()
        {
            ClearPrevious();
            BuildGradientQuad();
            BuildMotes();
        }

        /// <summary>Drop any backdrop objects from an earlier Build so rebuilds never stack.</summary>
        private void ClearPrevious()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("EnvBackdrop_"))
                    DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>Vertical gradient quad sized to cover the full camera view, placed behind the board.</summary>
        private void BuildGradientQuad()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "EnvBackdrop_Gradient";
            go.transform.SetParent(transform, false);

            // Behind the board (board plane is z=0; camera sits at -z looking toward +z).
            go.transform.localPosition = new Vector3(0f, 0f, 0.5f);

            // Size to the ortho view on the board plane, with margin so edges never peek in.
            float halfH = 6f, halfW = 10f; // conservative fallbacks
            var cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                float tilt = cam.transform.eulerAngles.x * Mathf.Deg2Rad;
                halfH = cam.orthographicSize / Mathf.Max(Mathf.Cos(tilt), 0.5f);
                halfW = halfH * cam.aspect;
            }
            go.transform.localScale = new Vector3(halfW * 2f * 1.25f, halfH * 2f * 1.25f, 1f);

            Object.Destroy(go.GetComponent<Collider>());

            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = CreateGradientMaterial();
            rend.sortingOrder = -10; // behind every board element (ghost quads use -5)
        }

        private static Material CreateGradientMaterial()
        {
            const int height = 128;
            var tex = new Texture2D(2, height, TextureFormat.RGBA32, false);
            tex.name = "EnvBackdrop_GradientTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1); // 0 = bottom of quad, 1 = top
                Color c = Color.Lerp(GradientBottom, GradientTop, t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply();

            var mat = new Material(Shader.Find(QuadShaderName));
            if (mat == null) return null;
            mat.name = "EnvBackdrop_GradientMat";
            mat.mainTexture = tex;
            return mat;
        }

        /// <summary>3-5 warm-white motes drifting slowly across the board plane.</summary>
        private void BuildMotes()
        {
            var go = new GameObject("EnvBackdrop_Motes");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.2f); // just above the board surface

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = MoteColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8;
            main.gravityModifier = -0.01f; // whisper of upward drift

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0.25f; // steady trickle → ~3-5 alive at once
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 5f, 0.2f); // spread across the board area

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.04f); // slow upward drift

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),      // fade in
                    new GradientAlphaKey(0.15f, 0.2f), // 15% opacity mid-life
                    new GradientAlphaKey(0.15f, 0.75f),
                    new GradientAlphaKey(0f, 1f)       // fade out
                });
            colorOver.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateMoteMaterial();
            renderer.sortingOrder = 10; // above board content, below UI
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        /// <summary>URP particle unlit (additive), falling back to Sprites/Default — mirrors ParticleFxService.</summary>
        private static Material CreateMoteMaterial()
        {
            var shader = Shader.Find(ParticleShaderUrp);
            if (shader == null)
                shader = Shader.Find(ParticleShaderFallback);
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.name = "EnvBackdrop_MoteMat";

            // Additive blending where the shader exposes it (URP Particles/Unlit: _BlendMode 1 = Additive).
            if (mat.HasProperty("_BlendMode"))
                mat.SetFloat("_BlendMode", 1f);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);

            mat.SetColor("_Color", MoteColor);
            mat.SetColor("_TintColor", MoteColor);
            return mat;
        }
    }
}
