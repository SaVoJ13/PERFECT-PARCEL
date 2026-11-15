using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-06d: фони ігрового екрана й меню мають розмір 540x960, градієнт (не однотонні) і не знижують контраст:
    /// центральна ігрова зона темна й рівна, щоб світлі значки й білий текст читалися (WCAG AA).
    /// </summary>
    public sealed class BackgroundTests
    {
        private static Texture2D Load(string name)
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Art", name + ".png");
            Assert.IsTrue(File.Exists(path), "немає файла: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), "не читається PNG: " + name);
            return tex;
        }

        private static float Lin(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private static float Luminance(Color c)
        {
            return (0.2126f * Lin(c.r)) + (0.7152f * Lin(c.g)) + (0.0722f * Lin(c.b));
        }

        private static void Zone(Texture2D t, int x0, int x1, int y0, int y1, out float mean, out float max)
        {
            float sum = 0f;
            int n = 0;
            max = 0f;
            for (int x = x0; x < x1; x += 3)
            {
                for (int y = y0; y < y1; y += 3)
                {
                    float l = Luminance(t.GetPixel(x, y));
                    sum += l;
                    max = Mathf.Max(max, l);
                    n++;
                }
            }

            mean = sum / n;
        }

        [TestCase("bg_bar")]
        [TestCase("bg_menu")]
        public void Background_HasSizeAndRichGradient(string name)
        {
            Texture2D tex = Load(name);
            Assert.AreEqual(540, tex.width, name);
            Assert.AreEqual(960, tex.height, name);
            var colors = new HashSet<Color32>();
            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i += 7)
            {
                colors.Add(px[i]);
            }

            Assert.GreaterOrEqual(colors.Count, 500, name + ": фон майже однотонний");
            Assert.AreEqual(255, tex.GetPixel(10, 10).a, name + ": фон має бути непрозорим");
        }

        [Test]
        public void GameBackground_PlayZoneStaysDarkAndEven()
        {
            Texture2D tex = Load("bg_bar");
            // Unity: y=0 унизу; ігрова зона (дошка, гриль) - нижня частина зображення.
            Zone(tex, 135, 405, tex.height - 860, tex.height - 580, out float mean, out float max);
            Assert.Less(max, 0.20f, "прилавок надто світлий: контраст значків/тексту впаде");
            Assert.Greater(mean, 0.03f, "прилавок надто темний: глибини нема");
            Assert.GreaterOrEqual((1f + 0.05f) / (max + 0.05f), 4.5f, "білий текст на найсвітлішому місці < AA");
            // Вікно на вулицю світліше за прилавок (глибина), але лишається вище за ігрову зону.
            Zone(tex, 60, 480, tex.height - 330, tex.height - 150, out float windowMean, out _);
            Assert.Greater(windowMean, mean, "вікно має бути світлішим за прилавок");
        }

        [Test]
        public void MenuBackground_CenterStaysDarkForMapAndText()
        {
            Texture2D tex = Load("bg_menu");
            Zone(tex, 135, 405, tex.height - 700, tex.height - 300, out float mean, out _);
            Assert.Less(mean, 0.10f, "центр меню надто світлий для кремового тексту");
            // Небо вгорі темніше за обрій (градієнт знизу вгору).
            float top = Luminance(tex.GetPixel(270, tex.height - 5));
            float horizon = Luminance(tex.GetPixel(270, tex.height - 650));
            Assert.Greater(horizon, top, "має бути світліший обрій");
        }
    }
}
