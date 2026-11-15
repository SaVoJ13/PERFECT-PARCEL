using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>Вид частинки; кожен вид має власний пул із фіксованим розміром елемента.</summary>
    public enum FxKind
    {
        Steam = 0,
        Smoke = 1,
        Coin = 2,
        Flyer = 3,
        Text = 4,
        Confetti = 5,
        Spark = 6,
    }

    /// <summary>
    /// Шар ефектів екрана зміни (Q-05d, QB-SHIFT-14/17/21/25): пара, дим, монети-частинки, політ інгредієнта, «+N».
    /// Це прості VisualElement, створені один раз у пулах (без алокацій щокадру); рухаються лише transform і opacity
    /// (QB-ALL-16), заповнені пули тихо відкидають нові частинки (ліміт <see cref="MaxActive"/>). Час - ігровий
    /// (<see cref="UiMotion.Now"/>), тож пауза зупиняє рух. У режимі «менше анімацій» (<see cref="UiMotion.Reduced"/>)
    /// частинки не створюються, а «+N» показується на місці. Випадковість - власний генератор із seed.
    /// Правил гри тут немає.
    /// </summary>
    public sealed class FxLayer
    {
        public const string LayerName = "fx-layer";

        // Розміри пулів: сума <= 150 частинок на екрані (QB-ALL-16, припущення).
        public const int SteamPool = 40;
        public const int SmokePool = 30;
        public const int CoinPool = 24;
        public const int FlyerPool = 8;
        public const int TextPool = 8;
        public const int MaxActive = SteamPool + SmokePool + CoinPool + FlyerPool + TextPool;

        // Q-05e: ефекти результату (QB-RES-02/04). Пускаються лише після Clear() на екрані результату,
        // коли ігрових частинок уже немає, тож разом з ними ліміт 150 не перевищується.
        public const int ConfettiPool = 60;
        public const int SparkPool = 30;
        public const int ResultMaxActive = ConfettiPool + SparkPool;
        public const float ConfettiLifeMin = 1.5f;
        public const float ConfettiLifeMax = 2.5f;
        public const float ConfettiStaggerSec = 0.3f;
        public const float SparkLifeSec = 0.5f;
        public const float SparkReachMin = 60f;
        public const float SparkReachMax = 110f;
        public const float DefaultLayerWidth = 1080f;
        public const float DefaultLayerHeight = 1920f;

        // QB-SHIFT-17: пара - підйом 40-80 px, життя 0,8-1,5 с. QB-SHIFT-21: дим темніший і повільніший.
        public const float SteamRiseMin = 40f;
        public const float SteamRiseMax = 80f;
        public const float SteamLifeMin = 0.8f;
        public const float SteamLifeMax = 1.5f;
        public const float SmokeRiseMin = 30f;
        public const float SmokeRiseMax = 60f;
        public const float SmokeLifeMin = 1.2f;
        public const float SmokeLifeMax = 2f;

        // QB-SHIFT-14: 3-8 монет, політ 400-700 мс, «+N» піднімається 40-60 px і гасне за 600-800 мс.
        public const int CoinsMin = 3;
        public const int CoinsMax = 8;
        public const float CoinFlightMin = 0.4f;
        public const float CoinFlightMax = 0.7f;
        public const float CoinStaggerSec = 0.08f;
        public const float CoinSpreadPx = 60f;
        public const float PlusRisePx = 50f;
        public const float PlusSec = 0.7f;

        // QB-SHIFT-25: інгредієнт летить зі станції на дошку дугою за 150-250 мс.
        public const float FlightSec = 0.2f;
        public const float FlightArcPx = 60f;

        private const float SteamSize = 28f;
        private const float SmokeSize = 36f;
        private const float CoinSize = 44f;
        private const float FlyerSize = 88f;
        private const float TextWidth = 220f;
        private const float TextHeight = 64f;
        private const float TextFontSize = 48f;
        private const float SteamStartOpacity = 0.55f;
        private const float SmokeStartOpacity = 0.6f;
        private const float EmitSpreadPx = 36f;
        private const float EmitLiftPx = 40f;
        private const float TipOffsetPx = 90f;
        private const float ConfettiSize = 18f;
        private const float SparkSize = 14f;
        private const float ConfettiDriftPx = 120f;
        private const float FullTurn = 2f * (float)Math.PI;
        private const int CoinsPerParticle = 4;
        private const float Half = 0.5f;

        private static readonly Color SteamColor = new Color(0.96f, 0.96f, 0.96f);
        private static readonly Color DarkSteamColor = new Color(0.32f, 0.3f, 0.3f);
        private static readonly Color SmokeColor = new Color(0.12f, 0.11f, 0.11f);
        private static readonly Color PlusColor = UiColors.Coin;
        private static readonly Color TipColor = new Color(0.55f, 0.95f, 0.55f);
        private static readonly Color[] ConfettiColors =
        {
            UiColors.Saffron, UiColors.Coin, UiColors.EmberRed, UiColors.ReadyText, Color.white,
        };

        private readonly VisualElement _root;
        private readonly Pool[] _pools = new Pool[7];
        private readonly System.Random _random;
        private VisualElement _coinTarget;

        public FxLayer(int seed = 1)
        {
            _random = new System.Random(seed);
            _root = new VisualElement { name = LayerName, pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;

            _pools[(int)FxKind.Steam] = new Pool(_root, SteamPool, SteamSize, "fx-steam-");
            _pools[(int)FxKind.Smoke] = new Pool(_root, SmokePool, SmokeSize, "fx-smoke-");
            _pools[(int)FxKind.Coin] = new Pool(_root, CoinPool, CoinSize, "fx-coin-");
            _pools[(int)FxKind.Flyer] = new Pool(_root, FlyerPool, FlyerSize, "fx-flyer-");
            _pools[(int)FxKind.Text] = new Pool(_root, TextPool, TextWidth, "fx-text-", TextHeight);
            _pools[(int)FxKind.Confetti] = new Pool(_root, ConfettiPool, ConfettiSize, "fx-confetti-", 0f, false);
            _pools[(int)FxKind.Spark] = new Pool(_root, SparkPool, SparkSize, "fx-spark-");
        }

        /// <summary>Елемент шару; додається в корінь екрана поверх панелей.</summary>
        public VisualElement Root => _root;

        /// <summary>Куди летять монети (значок монети в HUD); null - до верхнього краю шару.</summary>
        public VisualElement CoinTarget
        {
            get => _coinTarget;
            set => _coinTarget = value;
        }

        /// <summary>Скільки частинок зараз показано.</summary>
        public int ActiveCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _pools.Length; i++)
                {
                    sum += _pools[i].Active;
                }

                return sum;
            }
        }

        public int ActiveOf(FxKind kind)
        {
            return _pools[(int)kind].Active;
        }

        public int CapacityOf(FxKind kind)
        {
            return _pools[(int)kind].Count;
        }

        /// <summary>Скільки частинок цього виду відкинуто, бо пул був повний.</summary>
        public int DroppedOf(FxKind kind)
        {
            return _pools[(int)kind].Dropped;
        }

        /// <summary>Скільки разів стиль частинок справді записано (для тесту «без записів на паузі»).</summary>
        public int Writes
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _pools.Length; i++)
                {
                    sum += _pools[i].Writes;
                }

                return sum;
            }
        }

        /// <summary>Поточний текст і колір «+N» у пулі (для тестів); null, якщо елемент вільний.</summary>
        public string TextOf(int index)
        {
            Pool pool = _pools[(int)FxKind.Text];
            return pool.Items[index].Active ? ((Label)pool.Elements[index]).text : null;
        }

        /// <summary>Пара над м'ясом (QB-SHIFT-17); <paramref name="dark"/> - темна пара (QB-SHIFT-20).</summary>
        public bool Steam(VisualElement source, bool dark)
        {
            if (UiMotion.Reduced)
            {
                return false;
            }

            Pool pool = _pools[(int)FxKind.Steam];
            int index = pool.Take();
            if (index < 0)
            {
                return false;
            }

            Vector2 from = Anchor(source) + new Vector2(Range(-EmitSpreadPx, EmitSpreadPx), -EmitLiftPx);
            float rise = Range(SteamRiseMin, SteamRiseMax);
            pool.Start(index, UiMotion.Now, Range(SteamLifeMin, SteamLifeMax), from, from + new Vector2(Range(-12f, 12f), -rise), 0f);
            pool.Items[index].Opacity = SteamStartOpacity;
            pool.Items[index].Scale1 = 1.6f;
            pool.Elements[index].style.backgroundColor = dark ? DarkSteamColor : SteamColor;
            return true;
        }

        /// <summary>Дим над пригорілим м'ясом (QB-SHIFT-21): темніший і повільніший за пару.</summary>
        public bool Smoke(VisualElement source)
        {
            if (UiMotion.Reduced)
            {
                return false;
            }

            Pool pool = _pools[(int)FxKind.Smoke];
            int index = pool.Take();
            if (index < 0)
            {
                return false;
            }

            Vector2 from = Anchor(source) + new Vector2(Range(-EmitSpreadPx, EmitSpreadPx), -EmitLiftPx);
            float rise = Range(SmokeRiseMin, SmokeRiseMax);
            pool.Start(index, UiMotion.Now, Range(SmokeLifeMin, SmokeLifeMax), from, from + new Vector2(Range(-20f, 20f), -rise), 0f);
            pool.Items[index].Opacity = SmokeStartOpacity;
            pool.Items[index].Scale1 = 1.8f;
            pool.Elements[index].style.backgroundColor = SmokeColor;
            return true;
        }

        /// <summary>Інгредієнт-значок летить зі станції на місце на дошці дугою (QB-SHIFT-25). true - політ запущено.</summary>
        public bool Fly(VisualElement from, VisualElement to, string iconName)
        {
            if (UiMotion.Reduced)
            {
                return false;
            }

            Pool pool = _pools[(int)FxKind.Flyer];
            int index = pool.Take();
            if (index < 0)
            {
                return false;
            }

            pool.Start(index, UiMotion.Now, FlightSec, Anchor(from), Anchor(to), FlightArcPx);
            pool.Items[index].Opacity = 1f;
            pool.Items[index].Scale1 = 1f;
            ArtLibrary.Apply(pool.Elements[index], iconName);
            return true;
        }

        /// <summary>
        /// Оплата (QB-SHIFT-14): 3-8 монет від клієнта до лічильника і «+N» над клієнтом; чайові - окремий «+N»
        /// іншого відтінку. Кількість монет залежить від суми. Повертає, скільки монет запущено.
        /// </summary>
        public int Payment(VisualElement source, long paid, long tip)
        {
            Vector2 origin = Anchor(source);
            if (paid > 0L)
            {
                Plus(origin, "+" + paid, PlusColor);
            }

            if (tip > 0L)
            {
                Plus(origin + new Vector2(TipOffsetPx, 0f), "+" + tip, TipColor);
            }

            if (UiMotion.Reduced)
            {
                return 0;
            }

            Pool pool = _pools[(int)FxKind.Coin];
            Vector2 target = _coinTarget != null ? Anchor(_coinTarget) : new Vector2(origin.x, 0f);
            int count = Mathf.Clamp((int)((paid + tip) / CoinsPerParticle), CoinsMin, CoinsMax);
            int started = 0;
            float now = UiMotion.Now;
            for (int i = 0; i < count; i++)
            {
                int index = pool.Take();
                if (index < 0)
                {
                    break;
                }

                Vector2 from = origin + new Vector2(Range(-CoinSpreadPx, CoinSpreadPx), Range(-CoinSpreadPx, CoinSpreadPx) * Half);
                pool.Start(index, now + (i * CoinStaggerSec), Range(CoinFlightMin, CoinFlightMax), from, target, Range(40f, 120f));
                pool.Items[index].Opacity = 1f;
                pool.Items[index].Scale1 = 0.8f;
                if (!ArtLibrary.Apply(pool.Elements[index], ArtLibrary.Coin))
                {
                    pool.Elements[index].style.backgroundColor = PlusColor;
                }

                started++;
            }

            return started;
        }

        /// <summary>
        /// Конфеті (QB-RES-04): <paramref name="count"/> квадратиків кольорів палітри падають згори на 1,5-2,5 с.
        /// У «менше анімацій» не створюється. Повертає, скільки запущено.
        /// </summary>
        public int Confetti(int count)
        {
            if (UiMotion.Reduced)
            {
                return 0;
            }

            Pool pool = _pools[(int)FxKind.Confetti];
            float width = LayerSize(_root.layout.width, DefaultLayerWidth);
            float height = LayerSize(_root.layout.height, DefaultLayerHeight);
            float now = UiMotion.Now;
            int started = 0;
            for (int i = 0; i < count; i++)
            {
                int index = pool.Take();
                if (index < 0)
                {
                    break;
                }

                float x = Range(0f, width);
                Vector2 from = new Vector2(x, -ConfettiSize);
                Vector2 to = new Vector2(x + Range(-ConfettiDriftPx, ConfettiDriftPx), height * Range(0.55f, 0.95f));
                pool.Start(index, now + Range(0f, ConfettiStaggerSec), Range(ConfettiLifeMin, ConfettiLifeMax), from, to, 0f);
                pool.Items[index].Opacity = 1f;
                pool.Items[index].Scale1 = 1f;
                pool.Elements[index].style.backgroundColor = ConfettiColors[_random.Next(ConfettiColors.Length)];
                started++;
            }

            return started;
        }

        /// <summary>Іскри навколо зірки (QB-RES-02): 6-10 крапок розлітаються за 0,5 с. У «менше анімацій» не створюються.</summary>
        public int Sparks(VisualElement source)
        {
            if (UiMotion.Reduced)
            {
                return 0;
            }

            Pool pool = _pools[(int)FxKind.Spark];
            Vector2 origin = Anchor(source);
            int count = _random.Next(ResultMotion.SparksMin, ResultMotion.SparksMax + 1);
            float now = UiMotion.Now;
            int started = 0;
            for (int i = 0; i < count; i++)
            {
                int index = pool.Take();
                if (index < 0)
                {
                    break;
                }

                float angle = FullTurn * ((i + (float)_random.NextDouble()) / count);
                float reach = Range(SparkReachMin, SparkReachMax);
                Vector2 to = origin + new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach);
                pool.Start(index, now, SparkLifeSec, origin, to, 0f);
                pool.Items[index].Opacity = 1f;
                pool.Items[index].Scale1 = 1f;
                pool.Elements[index].style.backgroundColor = PlusColor;
                started++;
            }

            return started;
        }

        /// <summary>Рух усіх активних частинок за ігровим часом; викликається щокадру, сміття не створює.</summary>
        public void Update()
        {
            float now = UiMotion.Now;
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i].Update(now, (FxKind)i);
            }
        }

        /// <summary>Зупиняє всі частинки (кінець зміни).</summary>
        public void Clear()
        {
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i].Clear();
            }
        }

        /// <summary>Положення частинки (px у шарі) на частці <paramref name="t"/> польоту: лінія плюс дуга вгору.</summary>
        public static Vector2 PathAt(Vector2 from, Vector2 to, float arcPx, float t)
        {
            float k = Mathf.Clamp01(t);
            float eased = 1f - ((1f - k) * (1f - k));
            Vector2 p = Vector2.LerpUnclamped(from, to, eased);
            p.y -= arcPx * Mathf.Sin((float)Math.PI * k);
            return p;
        }

        private void Plus(Vector2 origin, string text, Color color)
        {
            Pool pool = _pools[(int)FxKind.Text];
            int index = pool.Take();
            if (index < 0)
            {
                return;
            }

            float rise = UiMotion.Reduced ? 0f : PlusRisePx;
            pool.Start(index, UiMotion.Now, PlusSec, origin + new Vector2(0f, -EmitLiftPx), origin + new Vector2(0f, -EmitLiftPx - rise), 0f);
            pool.Items[index].Opacity = 1f;
            pool.Items[index].Scale1 = 1f;
            var label = (Label)pool.Elements[index];
            label.text = text;
            label.style.color = color;
        }

        /// <summary>Центр елемента в координатах шару; немає панелі (тести) - початок шару.</summary>
        private Vector2 Anchor(VisualElement element)
        {
            if (element == null || element.panel == null || _root.panel == null)
            {
                return Vector2.zero;
            }

            Rect box = element.worldBound;
            if (float.IsNaN(box.x) || float.IsNaN(box.y))
            {
                return Vector2.zero;
            }

            return _root.WorldToLocal(box.center);
        }

        private static float LayerSize(float measured, float fallback)
        {
            return float.IsNaN(measured) || measured < 1f ? fallback : measured;
        }

        private float Range(float min, float max)
        {
            return min + ((max - min) * (float)_random.NextDouble());
        }

        private struct Item
        {
            public bool Active;
            public float Start;
            public float Duration;
            public Vector2 From;
            public Vector2 To;
            public float Arc;
            public float Opacity;
            public float Scale1;
        }

        private sealed class Pool
        {
            private readonly float _halfW;
            private readonly float _halfH;
            private readonly MotionTarget[] _targets;

            public Pool(VisualElement parent, int count, float size, string prefix, float height = 0f, bool round = true)
            {
                float h = height > 0f ? height : size;
                _halfW = size * Half;
                _halfH = h * Half;
                Count = count;
                Items = new Item[count];
                Elements = new VisualElement[count];
                _targets = new MotionTarget[count];
                bool text = height > 0f;
                for (int i = 0; i < count; i++)
                {
                    VisualElement element;
                    if (text)
                    {
                        var label = new Label();
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                        label.style.fontSize = TextFontSize;
                        label.style.unityTextAlign = TextAnchor.MiddleCenter;
                        label.style.textShadow = new TextShadow { offset = new Vector2(2f, 3f), blurRadius = 0f, color = new Color(0.1f, 0.06f, 0.03f) };
                        element = label;
                    }
                    else
                    {
                        element = new VisualElement();
                        if (round)
                        {
                            element.style.borderTopLeftRadius = size * Half;
                            element.style.borderTopRightRadius = size * Half;
                            element.style.borderBottomLeftRadius = size * Half;
                            element.style.borderBottomRightRadius = size * Half;
                        }
                    }

                    element.name = prefix + i;
                    element.pickingMode = PickingMode.Ignore;
                    element.style.position = Position.Absolute;
                    element.style.left = 0;
                    element.style.top = 0;
                    element.style.width = size;
                    element.style.height = h;
                    element.style.visibility = Visibility.Hidden;
                    parent.Add(element);
                    Elements[i] = element;
                    _targets[i] = new MotionTarget(element);
                }
            }

            public int Count { get; }

            public Item[] Items { get; }

            public VisualElement[] Elements { get; }

            public int Active { get; private set; }

            public int Dropped { get; private set; }

            public int Writes
            {
                get
                {
                    int sum = 0;
                    for (int i = 0; i < _targets.Length; i++)
                    {
                        sum += _targets[i].Writes;
                    }

                    return sum;
                }
            }

            /// <summary>Вільний елемент пулу; -1 і лічильник відкинутих, якщо всі зайняті.</summary>
            public int Take()
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (!Items[i].Active)
                    {
                        Items[i].Active = true;
                        Active++;
                        return i;
                    }
                }

                Dropped++;
                return -1;
            }

            public void Start(int index, float start, float duration, Vector2 from, Vector2 to, float arc)
            {
                Items[index].Start = start;
                Items[index].Duration = Mathf.Max(0.01f, duration);
                Items[index].From = from;
                Items[index].To = to;
                Items[index].Arc = arc;
            }

            public void Update(float now, FxKind kind)
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (!Items[i].Active)
                    {
                        continue;
                    }

                    float t = (now - Items[i].Start) / Items[i].Duration;
                    if (t >= 1f)
                    {
                        Release(i);
                        continue;
                    }

                    if (t < 0f)
                    {
                        continue; // ще чекає свого часу (розкид монет у часі)
                    }

                    if (Elements[i].style.visibility.value != Visibility.Visible)
                    {
                        Elements[i].style.visibility = Visibility.Visible;
                    }

                    Vector2 p = PathAt(Items[i].From, Items[i].To, Items[i].Arc, t);
                    float scale = kind == FxKind.Steam || kind == FxKind.Smoke
                        ? Mathf.Lerp(0.6f, Items[i].Scale1, t)
                        : Mathf.Lerp(Items[i].Scale1, 1f, t);
                    float opacity = Opacity(kind, t, Items[i].Opacity);
                    _targets[i].Set(scale, scale, p.x - _halfW, p.y - _halfH, opacity);
                }
            }

            public void Clear()
            {
                for (int i = 0; i < Items.Length; i++)
                {
                    if (Items[i].Active)
                    {
                        Release(i);
                    }
                }
            }

            private static float Opacity(FxKind kind, float t, float start)
            {
                switch (kind)
                {
                    case FxKind.Steam:
                    case FxKind.Smoke:
                        return start * (1f - t);
                    case FxKind.Text:
                        return t < 0.6f ? 1f : 1f - ((t - 0.6f) / 0.4f);
                    default:
                        return t < 0.85f ? 1f : 1f - ((t - 0.85f) / 0.15f);
                }
            }

            private void Release(int index)
            {
                Items[index].Active = false;
                Active--;
                Elements[index].style.visibility = Visibility.Hidden;
            }
        }
    }
}
