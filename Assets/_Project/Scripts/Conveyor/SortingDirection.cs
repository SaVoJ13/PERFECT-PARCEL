namespace PerfectParcel.Conveyor
{
    /// <summary>
    /// 3 сортувальні напрямки згідно з розробленим GAME_DESIGN.md.
    /// Кожен напрямок має асоційований колір та геометричну піктограму.
    /// </summary>
    public enum SortingDirection
    {
        Left = 0,    // 🔵 Синій (Коло) — Міська доставка
        Straight = 1,// 🟢 Зелений (Трикутник) — Наземна пошта
        Right = 2    // 🔴 Червоний (Ромб) — Авіа-експрес
    }
}
