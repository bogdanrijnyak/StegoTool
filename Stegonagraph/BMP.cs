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
        // кодування
        static public void bmpEncode(List<Byte> dataToEmbed, String outputPath, Bitmap targetBitmap, byte[] stegoKey)
        {
            int currentPixelIndex = 0;
            int keyPosition = 0;
            String dataBitStream = "";

            for (int dataByteIndex = 0; dataByteIndex < dataToEmbed.Count; dataByteIndex++)
            {
                // конвертація байта у двійкову систему числення
                dataBitStream += HelpTools.AutoAddByte(Convert.ToString(dataToEmbed[dataByteIndex], 2), 8);

                while (dataBitStream.Length >= 3 * stegoKey[keyPosition % stegoKey.Length])
                {
                    // кодування за допомогою методу LSB
                    dataBitStream = WriteToBitmap(currentPixelIndex++, stegoKey[keyPosition % stegoKey.Length], dataBitStream, targetBitmap);
                    keyPosition++;
                }
            }

            // доповнення нулями, щоб довжина бітів була кратною 3 * кількість бітів на канал
            while (dataBitStream.Length % (3 * stegoKey[keyPosition % stegoKey.Length]) != 0)
                dataBitStream += "0";

            while (dataBitStream.Length != 0)
            {
                // кодування за допомогою методу LSB
                dataBitStream = WriteToBitmap(currentPixelIndex++, stegoKey[keyPosition % stegoKey.Length], dataBitStream, targetBitmap);
                keyPosition++;
            }

            if (dataBitStream.Length != 0)
                MessageBox.Show("Помилка!"); // відладкове повідомлення про помилку

            // збереження BMP-зображення з прихованою інформацією
            targetBitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Bmp);
            return;
        }

        // декодування
        static public List<Byte> bmpDecode(Bitmap myBitmap, int len, byte[] Key)
        {
            List<byte> findInfo = new List<byte>();

            int bmpPos = 0;
            String strInfo = "";
            int stegPos = 0;

            while (findInfo.Count != len)
            {
                while (strInfo.Length < 8)
                {
                    // декодування за допомогою методу LSB
                    strInfo += ReadFromBitmap(bmpPos++, Key[stegPos % Key.Length], myBitmap);
                    stegPos++;
                }

                while (strInfo.Length >= 8)
                {
                    findInfo.Add(Convert.ToByte(strInfo.Substring(0, 8), 2));
                    strInfo = strInfo.Substring(8);
                }
            }

            return findInfo;
        }

        static private String ReadFromBitmap(int pos, int arrColor, Bitmap myBitmap)
        {
            String retStr = "";

            int posY = pos / myBitmap.Width,
                posX = pos - posY * myBitmap.Width;

            // RGB-палітра конкретного пікселя
            Color pixel = myBitmap.GetPixel(posX, posY);

            int[] pixelColor = new int[3];
            pixelColor[0] = pixel.R;
            pixelColor[1] = pixel.G;
            pixelColor[2] = pixel.B;

            for (int j = 0; j < 3; j++)
            {
                // декодування секретного файлу з молодших бітів (метод LSB)
                String str = HelpTools.AutoAddByte(Convert.ToString(pixelColor[j], 2), 8);
                str = str.Substring(8 - arrColor, arrColor);

                retStr += str;
            }

            return retStr;
        }

        static private String WriteToBitmap(int pos, int arrColor, String hideStr, Bitmap myBitmap)
        {
            int posY = pos / myBitmap.Width,
                posX = pos - posY * myBitmap.Width;

            // RGB-палітра конкретного пікселя
            Color pixel = myBitmap.GetPixel(posX, posY);

            int[] pixelColor = new int[3];
            pixelColor[0] = pixel.R;
            pixelColor[1] = pixel.G;
            pixelColor[2] = pixel.B;

            for (int j = 0; j < 3; j++)
            {
                // заміна молодших бітів на біти секретного файлу (метод LSB)
                String str = HelpTools.AutoAddByte(Convert.ToString(pixelColor[j], 2), 8);
                str = str.Substring(0, 8 - arrColor);
                str += hideStr.Substring(0, arrColor);
                pixelColor[j] = Convert.ToInt32(str, 2);
                hideStr = hideStr.Substring(arrColor);
            }

            // змінити колір пікселя на закодований
            myBitmap.SetPixel(posX, posY, Color.FromArgb(pixelColor[0], pixelColor[1], pixelColor[2]));
            return hideStr;
        }

    }
}
