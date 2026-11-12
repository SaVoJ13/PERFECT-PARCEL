using System;

namespace KebabStreet.Core
{
    /// <summary>
    /// Симуляція однієї зміни: стани, таймер і генератор випадковості.
    /// Гарантії детермінізму: час зберігається в цілих тактах; однаковий seed, однакова
    /// конфігурація та однакова послідовність команд і викликів Tick/Advance дають
    /// однакові знімки стану на кожному кроці. Клас не читає системного часу і не має
    /// статичного стану. Команда в неправильному стані повертає false і нічого не змінює.
    /// </summary>
    public sealed class ShiftSimulation
    {
        // Роздільна здатність накопичувача Advance: мільйонна частка такту.
        private const long SubTicksPerTick = 1000000L;

        private readonly IShiftGoal _goal;
        private readonly SplitMix64Random _random;
        private readonly IDishReceiver _dishReceiver;
        private readonly ShiftAction[] _actions;
        private long _endTick;
        private long _pendingSubTicks;
        private int _actionCount;

        /// <summary>
        /// Створює зміну в стані Intro. Непридатна конфігурація — ArgumentException
        /// з переліком помилок (див. <see cref="ShiftConfig.GetErrors"/>).
        /// </summary>
        public ShiftSimulation(ShiftConfig config, ulong seed, IShiftGoal goal)
            : this(config, seed, goal, null)
        {
        }

        /// <summary>
        /// Створює зміну з отримувачем виданих страв (клієнти прилавка). dishReceiver = null —
        /// видана страва просто зникає з дошки і рахується виданою.
        /// </summary>
        public ShiftSimulation(ShiftConfig config, ulong seed, IShiftGoal goal, IDishReceiver dishReceiver)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _goal = goal ?? throw new ArgumentNullException(nameof(goal));

            var errors = config.GetErrors();
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    "Непридатна конфігурація зміни: " + string.Join("; ", errors), nameof(config));
            }

            Seed = seed;
            _random = new SplitMix64Random(seed);
            _endTick = config.DurationTicks;
            _actions = new ShiftAction[config.ActionQueueMax];
            if (config.Grill != null)
            {
                Grill = new Grill(config.Grill, config.TicksPerSecond);
            }

            if (config.Board != null)
            {
                Board = new Board(config.Board, config.TicksPerSecond);
            }

            _dishReceiver = dishReceiver;

            State = ShiftState.Intro;
        }

        /// <summary>Конфігурація, з якою створено зміну.</summary>
        public ShiftConfig Config { get; }

        /// <summary>Seed генератора цієї зміни.</summary>
        public ulong Seed { get; }

        /// <summary>Поточний стан зміни.</summary>
        public ShiftState State { get; private set; }

        /// <summary>Скільки тактів зміна відпрацювала (росте лише в Running).</summary>
        public long ElapsedTicks { get; private set; }

        /// <summary>Скільки тактів лишилось до кінця таймера; ніколи не від'ємне.</summary>
        public long RemainingTicks => _endTick - ElapsedTicks;

        /// <summary>Чи використано продовження в цій спробі.</summary>
        public bool ContinueUsed { get; private set; }

        /// <summary>Єдиний генератор випадковості зміни; підсистеми беруть випадковість лише з нього.</summary>
        public IRandom Random => _random;

        /// <summary>Гриль зміни (лише читання); null — у зміні немає гриля.</summary>
        public Grill Grill { get; }

        /// <summary>Робоча дошка зміни (лише читання); null — у зміні немає дошки.</summary>
        public Board Board { get; }

        /// <summary>Скільки дій гравця в черзі (разом із тією, що виконується).</summary>
        public int QueuedActionCount => _actionCount;

        /// <summary>Скільки тактів уже відпрацьовано над першою дією черги.</summary>
        public long HeadActionProgressTicks { get; private set; }

        /// <summary>Скільки дій виконано з результатом Done.</summary>
        public long ExecutedActionCount { get; private set; }

        /// <summary>Скільки дій проігноровано під час виконання (наприклад, «зняти сире»).</summary>
        public long IgnoredActionCount { get; private set; }

        /// <summary>Остання дія, що дійшла до виконання.</summary>
        public ShiftAction LastAction { get; private set; }

        /// <summary>Результат останньої виконаної дії (для підказок гравцю).</summary>
        public ShiftActionResult LastActionResult { get; private set; }

        /// <summary>Дія в черзі за номером (0 — та, що виконується).</summary>
        public ShiftAction GetQueuedAction(int index)
        {
            if (index < 0 || index >= _actionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _actions[index];
        }

        /// <summary>
        /// Команда гравця «покласти сире м'ясо»: у названий слот або (GrillSlot.Any) у перший
        /// вільний. Команда стає в чергу дій і виконується на такті; чи вдалася — видно
        /// з <see cref="LastActionResult"/>. false: зміна не в Running, гриля немає, слота
        /// з таким номером немає або черга повна — тоді нічого не змінюється.
        /// </summary>
        public bool PlaceMeat(int slot = GrillSlot.Any)
        {
            return Enqueue(ShiftActionType.PlaceMeat, slot);
        }

        /// <summary>
        /// Команда гравця «зняти готове м'ясо» (на дошку): з названого слота або (GrillSlot.Any)
        /// те, що найближче до пригорання. Сире й пригоріле зняти не можна — дія ігнорується.
        /// Якщо в зміні є дошка, зняте м'ясо лягає на неї; на заповнену дошку м'ясо не знімається
        /// (лишається на грилі).
        /// </summary>
        public bool TakeMeat(int slot = GrillSlot.Any)
        {
            return Enqueue(ShiftActionType.TakeMeat, slot);
        }

        /// <summary>
        /// Команда гравця «викинути пригоріле м'ясо»: з названого слота або (GrillSlot.Any)
        /// з першого пригорілого. Штраф — grill.burntPenaltyCoins за шматок.
        /// </summary>
        public bool DiscardBurntMeat(int slot = GrillSlot.Any)
        {
            return Enqueue(ShiftActionType.DiscardBurntMeat, slot);
        }

        /// <summary>
        /// Команда гравця «покласти на дошку інгредієнт зі станції» (піта, салат, соус);
        /// ingredient — номер інгредієнта в конфігу. false: зміна не в Running, дошки немає,
        /// такого інгредієнта немає, його станція недоступна на цьому рівні, це м'ясо
        /// (воно береться лише з гриля) або черга повна — тоді нічого не змінюється.
        /// </summary>
        public bool AddIngredient(int ingredient)
        {
            return EnqueueBoard(ShiftActionType.AddIngredient, ingredient, Board != null && Board.CanAdd(ingredient));
        }

        /// <summary>
        /// Команда гравця «зняти інгредієнт із дошки» (без штрафу): за місцем на дошці або
        /// (BoardTarget.TopItem) останній покладений.
        /// </summary>
        public bool RemoveIngredient(int position = BoardTarget.TopItem)
        {
            return EnqueueBoard(
                ShiftActionType.RemoveIngredient, position, Board != null && Board.IsValidPosition(position));
        }

        /// <summary>
        /// Команда гравця «очистити дошку у смітник»: штраф rules.boardClearPenaltyCoins.
        /// false також, якщо смітник недоступний на цьому рівні.
        /// </summary>
        public bool ClearBoard()
        {
            return EnqueueBoard(ShiftActionType.ClearBoard, 0, Board != null && Board.IsBinAvailable);
        }

        /// <summary>
        /// Команда гравця «видати страву»: target — кому (номер слота прилавка) або
        /// (BoardTarget.AnyCustomer) без названого клієнта. Видається лише правильна повна страва.
        /// </summary>
        public bool ServeDish(int target = BoardTarget.AnyCustomer)
        {
            return EnqueueBoard(
                ShiftActionType.ServeDish, target, Board != null && target >= BoardTarget.AnyCustomer);
        }

        /// <summary>
        /// Скасовує дію в черзі за номером (тап по елементу черги). Скасування першої дії
        /// обнуляє її поступ. false: зміна не в Running або такого номера немає.
        /// </summary>
        public bool CancelQueuedAction(int index)
        {
            if (State != ShiftState.Running || index < 0 || index >= _actionCount)
            {
                return false;
            }

            RemoveAction(index);
            return true;
        }

        /// <summary>Intro → Running.</summary>
        public bool Start()
        {
            return Transition(ShiftState.Intro, ShiftState.Running);
        }

        /// <summary>Running → Paused. Накопичений залишок часу зберігається.</summary>
        public bool Pause()
        {
            return Transition(ShiftState.Running, ShiftState.Paused);
        }

        /// <summary>Paused → Running.</summary>
        public bool Resume()
        {
            return Transition(ShiftState.Paused, ShiftState.Running);
        }

        /// <summary>
        /// Продовження після поразки: Lost → Running з додатковим часом із конфігурації.
        /// Лише раз за спробу; якщо додатковий час дорівнює 0 — недоступне.
        /// </summary>
        public bool Continue()
        {
            if (State != ShiftState.Lost || ContinueUsed || Config.ContinueBonusTicks <= 0)
            {
                return false;
            }

            ContinueUsed = true;
            _endTick += Config.ContinueBonusTicks;
            State = ShiftState.Running;
            return true;
        }

        /// <summary>
        /// Виконує рівно один такт. Поза Running нічого не робить і повертає false.
        /// На такті, де час вийшов, зміна проходить Resolving і стає Won або Lost.
        /// </summary>
        public bool Tick()
        {
            if (State != ShiftState.Running)
            {
                return false;
            }

            ElapsedTicks++;
            if (Grill != null)
            {
                // Спершу таймери м'яса, потім дії гравця: м'ясо, покладене на цьому такті,
                // має вік 0, а зняти можна вже на такті, коли воно стало готовим.
                Grill.Advance();
            }

            RunActions();

            if (ElapsedTicks >= _endTick)
            {
                _pendingSubTicks = 0;
                State = ShiftState.Resolving;
                State = _goal.IsAchieved(this) ? ShiftState.Won : ShiftState.Lost;
            }

            return true;
        }

        /// <summary>
        /// Приймає реальний час кадру і виконує такти, що назбиралися; повертає їх кількість.
        /// Час округлюється до мільйонної частки такту і накопичується цілим числом, тому
        /// для кадрів, кратних цій частці, результат не залежить від нарізки на кадри.
        /// За один виклик виконується щонайбільше MaxCatchUpTicks тактів, надлишок відкидається.
        /// Поза Running час ігнорується. NaN або від'ємне значення — ArgumentOutOfRangeException.
        /// </summary>
        public int Advance(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds), "Час кадру має бути невід'ємним числом.");
            }

            if (State != ShiftState.Running)
            {
                return 0;
            }

            int maxTicks = Config.MaxCatchUpTicks;
            double exactTicks = deltaSeconds * Config.TicksPerSecond;
            long dueTicks = maxTicks + 1L;

            // Час, явно більший за межу, у ціле не переводимо (захист від переповнення);
            // точне порівняння з межею нижче — вже в цілих числах.
            if (exactTicks < dueTicks)
            {
                _pendingSubTicks += (long)Math.Round(exactTicks * SubTicksPerTick);
                dueTicks = _pendingSubTicks / SubTicksPerTick;
                _pendingSubTicks %= SubTicksPerTick;
            }

            if (dueTicks > maxTicks)
            {
                dueTicks = maxTicks;
                _pendingSubTicks = 0;
            }

            int executed = 0;
            while (executed < dueTicks && Tick())
            {
                executed++;
            }

            return executed;
        }

        /// <summary>Знімок повного стану для перевірки детермінізму.</summary>
        public ShiftSnapshot CaptureSnapshot()
        {
            var queued = new ShiftAction[_actionCount];
            Array.Copy(_actions, queued, _actionCount);
            return new ShiftSnapshot(
                State,
                ElapsedTicks,
                RemainingTicks,
                _pendingSubTicks,
                ContinueUsed,
                _random.State,
                Grill == null ? default(GrillSnapshot) : Grill.CaptureSnapshot(),
                new ActionQueueSnapshot(
                    queued,
                    HeadActionProgressTicks,
                    ExecutedActionCount,
                    IgnoredActionCount,
                    LastAction,
                    LastActionResult),
                Board == null ? default(BoardSnapshot) : Board.CaptureSnapshot());
        }

        private bool Enqueue(ShiftActionType type, int slot)
        {
            if (State != ShiftState.Running
                || Grill == null
                || !Grill.IsValidTarget(slot)
                || _actionCount >= _actions.Length)
            {
                return false;
            }

            _actions[_actionCount] = new ShiftAction(type, slot);
            _actionCount++;
            return true;
        }

        private bool EnqueueBoard(ShiftActionType type, int argument, bool valid)
        {
            if (State != ShiftState.Running || !valid || _actionCount >= _actions.Length)
            {
                return false;
            }

            _actions[_actionCount] = new ShiftAction(type, argument);
            _actionCount++;
            return true;
        }

        /// <summary>Тривалість дії в тактах: за станцією, на якій вона виконується.</summary>
        private long ActionTicksOf(ShiftAction action)
        {
            switch (action.Type)
            {
                case ShiftActionType.AddIngredient:
                    return Board.AddActionTicks(action.Slot);
                case ShiftActionType.RemoveIngredient:
                case ShiftActionType.ServeDish:
                    return Board.BoardActionTicks;
                case ShiftActionType.ClearBoard:
                    return Board.BinActionTicks;
                default:
                    return Grill.ActionTicks;
            }
        }

        private void RemoveAction(int index)
        {
            for (int i = index + 1; i < _actionCount; i++)
            {
                _actions[i - 1] = _actions[i];
            }

            _actionCount--;
            _actions[_actionCount] = default(ShiftAction);
            if (index == 0)
            {
                HeadActionProgressTicks = 0L;
            }
        }

        /// <summary>
        /// Один такт роботи над чергою: за такт відпрацьовується щонайбільше один такт
        /// тривалості дії; дія виконується, щойно її тривалість відпрацьовано. Дії з нульовою
        /// тривалістю виконуються одразу, одна за одною в межах того самого такту.
        /// </summary>
        private void RunActions()
        {
            bool workSpent = false;
            while (_actionCount > 0)
            {
                long actionTicks = ActionTicksOf(_actions[0]);
                if (HeadActionProgressTicks < actionTicks)
                {
                    if (workSpent)
                    {
                        return;
                    }

                    HeadActionProgressTicks++;
                    workSpent = true;
                    if (HeadActionProgressTicks < actionTicks)
                    {
                        return;
                    }
                }

                ShiftAction action = _actions[0];
                RemoveAction(0);
                Execute(action);
            }
        }

        private void Execute(ShiftAction action)
        {
            ShiftActionResult result;
            switch (action.Type)
            {
                case ShiftActionType.PlaceMeat:
                    result = Grill.Place(action.Slot);
                    break;
                case ShiftActionType.TakeMeat:
                    result = TakeMeatToBoard(action.Slot);
                    break;
                case ShiftActionType.DiscardBurntMeat:
                    result = Grill.Discard(action.Slot);
                    break;
                case ShiftActionType.AddIngredient:
                    result = Board.Add(action.Slot);
                    break;
                case ShiftActionType.RemoveIngredient:
                    result = Board.Remove(action.Slot);
                    break;
                case ShiftActionType.ClearBoard:
                    result = Board.Clear();
                    break;
                default:
                    result = Board.Serve(_dishReceiver, action.Slot);
                    break;
            }

            LastAction = action;
            LastActionResult = result;
            if (result == ShiftActionResult.Done)
            {
                ExecutedActionCount++;
            }
            else
            {
                IgnoredActionCount++;
            }
        }

        /// <summary>
        /// Зняти готове м'ясо з гриля. З дошкою: м'ясо лягає на неї, а на заповнену дошку
        /// не знімається взагалі (гриль не змінюється). Без дошки — як у зміні лише з грилем.
        /// </summary>
        private ShiftActionResult TakeMeatToBoard(int slot)
        {
            if (Board == null)
            {
                return Grill.Take(slot);
            }

            if (Board.IsFull)
            {
                return ShiftActionResult.BoardFull;
            }

            ShiftActionResult result = Grill.Take(slot);
            if (result == ShiftActionResult.Done)
            {
                Board.Add(Board.MeatIngredient);
            }

            return result;
        }

        private bool Transition(ShiftState from, ShiftState to)
        {
            if (State != from)
            {
                return false;
            }

            State = to;
            return true;
        }
    }
}
