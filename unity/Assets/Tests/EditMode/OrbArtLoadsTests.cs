using NUnit.Framework;
using UnityEngine;

namespace ChromaVale.Tests.EditMode
{
    /// <summary>
    /// Verifies the artist orb art actually loads at runtime. Regression guard for the
    /// "plain colored dots" bug: orb art was dropped as AnimationStrips but OrbVisual
    /// looked for static sprites at Orbs/Art/<color>/<color>_t<tier>; if those are missing
    /// every orb silently falls back to the procedural circle.
    /// </summary>
    [TestFixture]
    public class OrbArtLoadsTests
    {
        private static readonly string[] Colors = { "cyan", "magenta", "yellow", "green", "orange", "purple", "brown" };

        [Test]
        public void AllOrbArt_SpritesLoad_FromResources()
        {
            int missing = 0;
            foreach (var color in Colors)
            {
                for (int tier = 1; tier <= 5; tier++)
                {
                    string path = $"Orbs/Art/{color}/{color}_t{tier}";
                    var sprite = Resources.Load<Sprite>(path);
                    if (sprite == null)
                    {
                        // Mirror OrbVisual's fallback: try a raw texture and wrap it.
                        var tex = Resources.Load<Texture2D>(path);
                        if (tex == null)
                        {
                            missing++;
                            UnityEngine.Debug.LogError($"[OrbArt] MISSING: {path}");
                        }
                    }
                }
            }
            Assert.AreEqual(0, missing, $"{missing} orb art assets failed to load from Resources.");
        }
    }
}
