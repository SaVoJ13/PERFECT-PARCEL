using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-06c: значки страв, інгредієнтів і станів м'яса мають об'єм (градієнт), темну обводку, прозорий фон,
    /// а стани м'яса розрізняються яскравістю у відтінках сірого (читається і без кольору).
    /// </summary>
    public sealed class DishIconsTests
    {
        private static readonly string[] Names =
        {
            "icon_meat_raw", "icon_meat_cooking", "icon_meat_done", "icon_meat_warning", "icon_meat_burnt",
            "ing_pita", "ing_meat", "ing_salad", "ing_onion", "ing_sauce_garlic", "ing_sauce_hot",
        };

        private static readonly string[] MeatStates =
        {
            "icon_meat_raw", "icon_meat_cooking", "icon_meat_done", "icon_meat_warning", "icon_meat_burnt",
        };

        private static Texture2D Load(string name)
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Art", name + ".png");
            Assert.IsTrue(File.Exists(path), "немає файла: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), "не читається PNG: " + name);
            return tex;
        }

        [Test]
        public void Icons_HaveVolume_Outline_AndTransparentBackground()
        {
            foreach (string name in Names)
            {
                Texture2D tex = Load(name);
                Assert.AreEqual(128, tex.width, name);
                Assert.AreEqual(128, tex.height, name);
                Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 0.01f, "кут має бути прозорим: " + name);

                var colors = new HashSet<Color32>();
                int opaque = 0;
                int dark = 0;
                foreach (Color32 p in tex.GetPixels32())
                {
                    if (p.a < 200)
                    {
                        continue;
                    }

                    opaque++;
                    colors.Add(p);
                    if (p.r < 80 && p.g < 50 && p.b < 40)
                    {
                        dark++;
                    }
                }

                Assert.GreaterOrEqual(colors.Count, 150, "плоска заливка без градієнта: " + name);
                Assert.GreaterOrEqual(dark, 400, "немає темної обводки: " + name);
                Assert.GreaterOrEqual(opaque, 128 * 128 / 5, "значок замалий у кадрі: " + name);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void MeatStates_DifferInGrayscaleBrightness_ForColorBlindPlayers()
        {
            float previous = float.MaxValue;
            foreach (string name in MeatStates)
            {
                Texture2D tex = Load(name);
                float sum = 0f;
                int count = 0;
                for (int y = 38; y < 94; y++)
                {
                    for (int x = 20; x < 70; x++)
                    {
                        Color c = tex.GetPixel(x, y);
                        if (c.a > 0.78f)
                        {
                            sum += (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
                            count++;
                        }
                    }
                }

                Object.DestroyImmediate(tex);
                Assert.Greater(count, 0, name);
                float mean = sum / count;
                Assert.Less(mean, previous - 0.04f, "стан не темніший за попередній: " + name);
                previous = mean;
            }
        }
    }
}
