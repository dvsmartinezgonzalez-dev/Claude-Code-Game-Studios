using UnityEngine;

namespace BoltSort.Visual
{
    /// <summary>
    /// Creates and manages the atmospheric background:
    /// gradient background, ambient dust particles, and vignette overlay.
    /// Attach to a persistent GameObject in the Gameplay scene via GameBootstrap.
    /// </summary>
    public class BackgroundController : MonoBehaviour
    {
        private void Awake()
        {
            BuildGradientBackground();
            BuildVignette();
            BuildAmbientParticles();
        }

        private void BuildGradientBackground()
        {
            const int w = 4, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            Color top    = BoltSortTheme.BackgroundDeep;
            Color bottom = BoltSortTheme.BackgroundMid;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1); // y=0 bottom in Unity
                Color c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 1f);
            var bgGO   = new GameObject("GradientBackground");
            bgGO.transform.SetParent(transform, false);
            bgGO.transform.localPosition = new Vector3(0f, 0f, 5f);

            Camera cam  = Camera.main;
            float  camH = cam != null ? cam.orthographicSize * 2f : 19.2f;
            float  camW = cam != null ? camH * cam.aspect       : camH * (9f / 16f);
            bgGO.transform.localScale = new Vector3(camW, camH, 1f);

            var sr         = bgGO.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = -100;

            // Override camera background so it matches the gradient bottom
            if (cam != null) cam.backgroundColor = BoltSortTheme.BackgroundDeep;
        }

        private void BuildVignette()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };

            float cx = size * 0.5f, cy = size * 0.5f;
            float maxDist = Mathf.Sqrt(cx * cx + cy * cy);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float t  = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                    float a  = Mathf.Clamp01((t - 0.55f) / 0.45f);
                    a        = a * a * 0.20f; // soft quadratic falloff, max ~20% alpha
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 1f);
            var vigGO  = new GameObject("Vignette");
            vigGO.transform.SetParent(transform, false);
            vigGO.transform.localPosition = Vector3.zero;

            Camera cam  = Camera.main;
            float  camH = cam != null ? cam.orthographicSize * 2f : 19.2f;
            float  camW = cam != null ? camH * cam.aspect       : camH * (9f / 16f);
            vigGO.transform.localScale = new Vector3(camW, camH, 1f);

            var sr         = vigGO.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = 50;
        }

        private void BuildAmbientParticles()
        {
            var go = new GameObject("AmbientDust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop            = true;
            main.maxParticles    = 40;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.70f, 1.00f, 0.18f),   // soft lavender
                new Color(1.00f, 0.90f, 1.00f, 0.45f));  // near-white pink
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            Camera cam  = Camera.main;
            float  camH = cam != null ? cam.orthographicSize * 2f : 19.2f;
            float  camW = cam != null ? camH * cam.aspect       : camH * (9f / 16f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale     = new Vector3(camW, camH, 0.01f);
            shape.position  = Vector3.zero;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x       = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.y       = new ParticleSystem.MinMaxCurve(0.08f,  0.30f);
            vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad    = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f,    0f),
                    new GradientAlphaKey(0.18f, 0.2f),
                    new GradientAlphaKey(0.18f, 0.8f),
                    new GradientAlphaKey(0f,    1f),
                });
            col.color = grad;

            var renderer     = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 5;
        }
    }
}
