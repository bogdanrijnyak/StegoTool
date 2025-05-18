using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stegonagraph
{
    // обробка контейнера BMP та PNG
    static class BMP
    {
        // Метод для узгодження значення arrColor з логікою розрахунку ємності
        private static int GetActualArrColor(byte rawArrColorValue)
        {
            return (rawArrColorValue == 0) ? 1 : Math.Min((int)rawArrColorValue, 8);
        }

        // кодування
        static public void bmpEncode(List<Byte> dataToEmbed, String outputPath, Bitmap targetBitmap, byte[] stegoKey)
        {
            int currentPixelIndex = 0;
            int keyPosition = 0;
            String dataBitStream = "";
            int totalPixels = targetBitmap.Width * targetBitmap.Height;

            for (int dataByteIndex = 0; dataByteIndex < dataToEmbed.Count; dataByteIndex++)
            {
                dataBitStream += HelpTools.AutoAddByte(Convert.ToString(dataToEmbed[dataByteIndex], 2), 8);

                while (dataBitStream.Length > 0) // Продовжуємо, поки є біти для запису
                {
                    if (currentPixelIndex >= totalPixels)
                    {
                        // Недостатньо місця в контейнері, хоча це не повинно трапитися,
                        // якщо розмір даних перевірено заздалегідь.
                        MessageBox.Show("Помилка кодування: недостатньо місця в BMP контейнері для всіх даних.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        // Зберігаємо те, що встигли закодувати, якщо потрібно, або просто виходимо.
                        // Для простоти, зараз просто вийдемо, але краще обробити цю ситуацію.
                        if (File.Exists(outputPath)) File.Delete(outputPath); // Видаляємо частково записаний файл
                        return;
                    }

                    byte currentKeyByte = stegoKey[keyPosition % stegoKey.Length];
                    int actualArrColor = GetActualArrColor(currentKeyByte);
                    int bitsToProcessInPixel = 3 * actualArrColor;

                    if (dataBitStream.Length >= bitsToProcessInPixel)
                    {
                        dataBitStream = WriteToBitmap(currentPixelIndex++, currentKeyByte, dataBitStream, targetBitmap);
                        keyPosition++;
                    }
                    else
                    {
                        // Недостатньо біт у dataBitStream для повного заповнення пікселя з поточним actualArrColor.
                        // Це може статися в кінці даних.
                        break; // Виходимо з внутрішнього циклу, щоб отримати більше біт з dataToEmbed або завершити.
                    }
                }
            }

            // Обробка залишку біт у dataBitStream (доповнення нулями та запис)
            if (dataBitStream.Length > 0 && currentPixelIndex < totalPixels)
            {
                byte currentKeyByte = stegoKey[keyPosition % stegoKey.Length];
                int actualArrColor = GetActualArrColor(currentKeyByte);
                int bitsForLastPixel = 3 * actualArrColor;

                // Доповнюємо нулями до потрібної довжини для останнього пікселя
                dataBitStream = dataBitStream.PadRight(bitsForLastPixel, '0');
                WriteToBitmap(currentPixelIndex++, currentKeyByte, dataBitStream, targetBitmap);
                // keyPosition++; // Не обов'язково, оскільки це кінець
            }
            else if (dataBitStream.Length > 0 && currentPixelIndex >= totalPixels)
            {
                MessageBox.Show("Помилка кодування: недостатньо місця для запису залишку біт.", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                return;
            }

            targetBitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Bmp);
        }

        // декодування
        static public List<Byte> bmpDecode(Bitmap myBitmap, int maxLenToReadInBytes, byte[] Key)
        {
            List<byte> findInfo = new List<byte>();
            int totalPixels = myBitmap.Width * myBitmap.Height;
            int bmpPos = 0; // Лінійний індекс поточного пікселя
            String strInfo = ""; // Рядок для накопичення біт
            int stegPos = 0; // Позиція в ключі

            // Цикл, доки не вилучено потрібну кількість байт АБО не закінчилися пікселі
            while (findInfo.Count < maxLenToReadInBytes && bmpPos < totalPixels)
            {
                // Накопичуємо біти для формування наступного байта
                while (strInfo.Length < 8 && bmpPos < totalPixels) // Також перевіряємо bmpPos тут
                {
                    // Читаємо біти з поточного пікселя
                    strInfo += ReadFromBitmap(bmpPos++, Key[stegPos % Key.Length], myBitmap);
                    stegPos++;
                }

                // Якщо накопичено достатньо біт для одного або більше байт
                // і ми ще не досягли потрібної кількості байт
                while (strInfo.Length >= 8 && findInfo.Count < maxLenToReadInBytes)
                {
                    findInfo.Add(Convert.ToByte(strInfo.Substring(0, 8), 2));
                    strInfo = strInfo.Substring(8);
                }
            }
            return findInfo;
        }

        // Читання біт з одного пікселя
        static private String ReadFromBitmap(int pos, byte rawArrColorValue, Bitmap myBitmap)
        {
            String retStr = "";
            int actualArrColor = GetActualArrColor(rawArrColorValue); // Використовуємо узгоджену логіку

            // Перевірка, чи pos не виходить за межі зображення (хоча зовнішній цикл вже має це робити)
            if (pos >= myBitmap.Width * myBitmap.Height)
            {
                // Це не повинно відбуватися, якщо bmpPos контролюється правильно в bmpDecode
                return ""; // Повертаємо порожній рядок, якщо вийшли за межі
            }

            int posY = pos / myBitmap.Width,
                posX = pos - posY * myBitmap.Width;

            Color pixel = myBitmap.GetPixel(posX, posY);

            int[] pixelColor = { pixel.R, pixel.G, pixel.B };

            for (int j = 0; j < 3; j++) // Для кожного колірного каналу (R, G, B)
            {
                String str = HelpTools.AutoAddByte(Convert.ToString(pixelColor[j], 2), 8);
                // Вилучаємо потрібну кількість молодших біт
                str = str.Substring(8 - actualArrColor, actualArrColor);
                retStr += str;
            }
            return retStr;
        }

        // Запис біт в один піксель
        static private String WriteToBitmap(int pos, byte rawArrColorValue, String hideStr, Bitmap myBitmap)
        {
            int actualArrColor = GetActualArrColor(rawArrColorValue); // Використовуємо узгоджену логіку

            // Перевірка, чи pos не виходить за межі зображення
            if (pos >= myBitmap.Width * myBitmap.Height)
            {
                // Це не повинно відбуватися, якщо currentPixelIndex контролюється правильно в bmpEncode
                return hideStr; // Повертаємо залишок hideStr, нічого не записавши
            }

            int posY = pos / myBitmap.Width,
                posX = pos - posY * myBitmap.Width;

            Color pixel = myBitmap.GetPixel(posX, posY);
            int[] pixelColor = { pixel.R, pixel.G, pixel.B };

            for (int j = 0; j < 3; j++) // Для кожного колірного каналу (R, G, B)
            {
                if (hideStr.Length < actualArrColor)
                {
                    // Недостатньо біт у hideStr для поточного каналу.
                    // Це може статися, якщо hideStr закінчився раніше, ніж очікувалося.
                    // Доповнюємо нулями, якщо потрібно, або обробляємо помилку.
                    // Поточна логіка bmpEncode має доповнювати hideStr заздалегідь.
                    // Якщо ми тут, це, ймовірно, помилка в логіці вище.
                    // Для безпеки, можна доповнити hideStr нулями або просто пропустити.
                    // Або, якщо hideStr порожній, то вийти.
                    if (hideStr.Length == 0) break;
                    hideStr = hideStr.PadRight(actualArrColor, '0'); // Доповнюємо, якщо частково
                }

                String str = HelpTools.AutoAddByte(Convert.ToString(pixelColor[j], 2), 8);
                // Замінюємо молодші біти на біти з hideStr
                str = str.Substring(0, 8 - actualArrColor);
                str += hideStr.Substring(0, actualArrColor);
                pixelColor[j] = Convert.ToInt32(str, 2);
                // Видаляємо використані біти з hideStr
                hideStr = hideStr.Substring(actualArrColor);
            }

            myBitmap.SetPixel(posX, posY, Color.FromArgb(pixelColor[0], pixelColor[1], pixelColor[2]));
            return hideStr; // Повертаємо залишок hideStr
        }
    }
}
