using System.IO;
using KebabStreet.Game;
using NUnit.Framework;
using UnityEngine;

namespace KebabStreet.Tests.EditMode
{
    /// <summary>
    /// Q-06b: портрети клієнтів мають 4 вирази, що відрізняються формою обличчя (а не лише кольором),
    /// а вираз береться зі стану терпіння (лише читання).
    /// </summary>
    public sealed class ClientFacesTests
    {
        private static readonly string[] Ids = { "olya", "marko", "hanna" };
        private static readonly ClientMood[] Moods = { ClientMood.Neutral, ClientMood.Happy, ClientMood.Worried, ClientMood.Angry };

        private static Texture2D Load(string name)
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/Art", name + ".png");
            Assert.IsTrue(File.Exists(path), "немає файла: " + path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), "не читається PNG: " + name);
            return tex;
        }

        private static float Gray(Color c)
        {
            return (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
        }

        [Test]
        public void EveryClientHasAllFourExpressions_WithVolumeAndTransparentCorner()
        {
            foreach (string id in Ids)
            {
                foreach (ClientMood mood in Moods)
                {
                    string name = ArtLibrary.MoodName("chr_" + id, mood);
                    Texture2D tex = Load(name);
                    Assert.AreEqual(256, tex.width, name);
                    Assert.AreEqual(256, tex.height, name);
                    Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 0.01f, name);
                    var colors = new System.Collections.Generic.HashSet<Color32>(tex.GetPixels32());
                    Assert.GreaterOrEqual(colors.Count, 300, "плоска заливка без градієнта: " + name);
                    Object.DestroyImmediate(tex);
                }
            }
        }

        [Test]
        public void Expressions_DifferInGrayscaleShape_NotOnlyColor()
        {
            foreach (string id in Ids)
            {
                var faces = new Texture2D[Moods.Length];
                for (int i = 0; i < Moods.Length; i++)
                {
                    faces[i] = Load(ArtLibrary.MoodName("chr_" + id, Moods[i]));
                }

                for (int a = 0; a < faces.Length; a++)
                {
                    for (int b = a + 1; b < faces.Length; b++)
                    {
                        int different = 0;
                        for (int y = 90; y < 180; y++) // область очей, брів і рота (унизу текстури y росте вгору)
                        {
                            for (int x = 70; x < 190; x++)
                            {
                                if (Mathf.Abs(Gray(faces[a].GetPixel(x, y)) - Gray(faces[b].GetPixel(x, y))) > 0.25f)
                                {
                                    different++;
                                }
                            }
                        }

                        Assert.GreaterOrEqual(different, 250, id + ": вирази " + Moods[a] + " і " + Moods[b] + " надто схожі у відтінках сірого");
                    }
                }

                foreach (Texture2D t in faces)
                {
                    Object.DestroyImmediate(t);
                }
            }
        }

        [Test]
        public void Clients_HaveDistinctSilhouettes()
        {
            // Силует = маска непрозорих пікселів; різні клієнти (кучері / кепка / пучок) відрізняються формою.
            var masks = new bool[Ids.Length][];
            for (int i = 0; i < Ids.Length; i++)
            {
                Texture2D tex = Load("chr_" + Ids[i]);
                Color32[] px = tex.GetPixels32();
                masks[i] = new bool[px.Length];
                for (int k = 0; k < px.Length; k++)
                {
                    masks[i][k] = px[k].a > 128;
                }

                Object.DestroyImmediate(tex);
            }

            for (int a = 0; a < Ids.Length; a++)
            {
                for (int b = a + 1; b < Ids.Length; b++)
                {
                    int different = 0;
                    for (int k = 0; k < masks[a].Length; k++)
                    {
                        if (masks[a][k] != masks[b][k])
                        {
                            different++;
                        }
                    }

                    Assert.GreaterOrEqual(different, 1500, Ids[a] + " і " + Ids[b] + ": силуети надто схожі");
                }
            }
        }

        [Test]
        public void MoodOf_FollowsPatience()
        {
            Assert.AreEqual(ClientMood.Happy, ArtLibrary.MoodOf(new PatienceView(3, 6, 100, false, false)));
            Assert.AreEqual(ClientMood.Happy, ArtLibrary.MoodOf(new PatienceView(3, 5, 50, false, true)), "пільговий час");
            Assert.AreEqual(ClientMood.Neutral, ArtLibrary.MoodOf(new PatienceView(3, 4, 70, false, false)));
            Assert.AreEqual(ClientMood.Worried, ArtLibrary.MoodOf(new PatienceView(3, 3, 45, false, false)));
            Assert.AreEqual(ClientMood.Angry, ArtLibrary.MoodOf(new PatienceView(3, 1, 20, true, false)));
        }

        [Test]
        public void MoodName_NeutralIsBase_OthersHaveSuffix()
        {
            Assert.AreEqual("chr_olya", ArtLibrary.MoodName("chr_olya", ClientMood.Neutral));
            Assert.AreEqual("chr_olya_happy", ArtLibrary.MoodName("chr_olya", ClientMood.Happy));
            Assert.AreEqual("chr_marko_worried", ArtLibrary.MoodName("chr_marko", ClientMood.Worried));
            Assert.AreEqual("chr_hanna_angry", ArtLibrary.MoodName("chr_hanna", ClientMood.Angry));
            Assert.IsNull(ArtLibrary.MoodName(null, ClientMood.Angry));
        }
    }
}
