namespace KebabStreet.Game
{
    /// <summary>Пошук тексту за ключем. Реалізація — <see cref="JsonLocalizer"/> (файли en/uk/pl).</summary>
    public interface ILocalizer
    {
        /// <summary>Текст за ключем; невідомий ключ повертається як "[ключ]", щоб помилку було видно.</summary>
        string Get(string key);
    }
}
