using System;

namespace Stegonagraph
{
    /// <summary>
    /// Допоміжний клас із статичними методами для роботи з бітовими рядками.
    /// </summary>
    static public class HelpTools
    {
        /// <summary>
        /// Додає провідні нулі до бітового рядка, щоб він мав довжину targetLength.
        /// </summary>
        /// <param name="binaryString">Початковий рядок бітів (без провідних нулів).</param>
        /// <param name="targetLength">Бажана довжина результатного рядка.</param>
        /// <returns>Рядок бітів довжиною targetLength із провідними нулями.</returns>
        static public string AutoAddByte(string binaryString, int targetLength)
        {
            // Поки довжина менша за потрібну — додаємо '0' зліва
            while (binaryString.Length < targetLength)
            {
                binaryString = "0" + binaryString;
            }

            return binaryString;
        }
    }
}
