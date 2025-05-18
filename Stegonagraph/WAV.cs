using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stegonagraph
{
    class WAV
    {
        public int NumberOfChannels { get; set; }
        public int BlockAlignBytes { get; set; }
        public int BitsPerSample { get; set; }
        public ulong DataStartPosition { get; set; }
        public ulong AudioDataLength { get; set; }
        private byte[] wavData;

        public WAV(string filePath)
        {
            // Зчитуємо весь WAV-файл у байтовий масив
            wavData = File.ReadAllBytes(filePath);

            // Позиція курсора для читання заголовка
            ulong cursorPosition = 0;

            // Перескакуємо до поля "NumberOfChannels" (offset 22)
            cursorPosition += 22;
            string bitBuffer = "";

            // Зчитуємо 2 байти кількості каналів, у форматі little-endian
            for (ulong i = cursorPosition; i < cursorPosition + 2; i++)
            {
                string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[i], 2), 8);
                bitBuffer = byteString + bitBuffer;
            }
            NumberOfChannels = Convert.ToInt32(bitBuffer, 2);
            bitBuffer = "";

            // Перескакуємо до поля "BlockAlign" (через 10 байт після каналів)
            cursorPosition += 10;
            for (ulong i = cursorPosition; i < cursorPosition + 2; i++)
            {
                string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[i], 2), 8);
                bitBuffer = byteString + bitBuffer;
            }
            BlockAlignBytes = Convert.ToInt32(bitBuffer, 2);
            bitBuffer = "";

            // Далі зчитуємо "BitsPerSample" (2 байти)
            cursorPosition += 2;
            for (ulong i = cursorPosition; i < cursorPosition + 2; i++)
            {
                string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[i], 2), 8);
                bitBuffer = byteString + bitBuffer;
            }
            BitsPerSample = Convert.ToInt32(bitBuffer, 2);
            bitBuffer = "";

            // Шукаємо заголовок "data" для початку звукових даних
            while (!(bitBuffer == "data") && cursorPosition < (ulong)wavData.Length - 4)
            {
                bitBuffer = ((char)wavData[cursorPosition]).ToString()
                          + ((char)wavData[cursorPosition + 1]).ToString()
                          + ((char)wavData[cursorPosition + 2]).ToString()
                          + ((char)wavData[cursorPosition + 3]).ToString();
                cursorPosition++;
            }

            // Перескакуємо до довжини блоку даних
            cursorPosition += 4;
            bitBuffer = "";
            for (ulong i = cursorPosition; i < cursorPosition + 4; i++)
            {
                string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[i], 2), 8);
                bitBuffer = byteString + bitBuffer;
            }
            AudioDataLength = Convert.ToUInt64(bitBuffer, 2);

            // Встановлюємо початкову позицію звукових семплів
            DataStartPosition = cursorPosition + 4;
        }

        public void WavEncode(List<byte> hiddenData, string outputPath, byte[] key)
        {
            // Поточна позиція у звукових байтах
            ulong samplePosition = DataStartPosition;
            uint keyPosition = 0;
            // Буфер біт прихованої інформації
            string dataBitBuffer = "";
            // Скільки семплів на фрейм (моно=1, стерео=2)
            int sampleCount = NumberOfChannels == 1 ? 1 : 2;

            // Вбудовуємо дані по байтах
            foreach (byte dataByte in hiddenData)
            {
                // Додаємо байт у бітовий буфер
                dataBitBuffer += HelpTools.AutoAddByte(Convert.ToString(dataByte, 2), 8);

                // Поки накопичилося достатньо біт для одного блоку
                while (dataBitBuffer.Length >= sampleCount * key[keyPosition % key.Length])
                {
                    // Зберігаємо початкову позицію блоку
                    ulong blockStartPos = samplePosition;
                    // Зчитуємо семпл у двічі оберненому порядку бітів
                    string sampleBits = "";
                    for (int i = 0; i < BlockAlignBytes; i++)
                    {
                        string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[samplePosition++], 2), 8);
                        sampleBits = byteString + sampleBits;
                    }

                    // Відновлюємо позицію для переписування
                    samplePosition = blockStartPos;

                    // Пересуваємо семпл по бітовим порціям (розбивання)
                    int totalSamples = BlockAlignBytes * 8 / BitsPerSample - 1;
                    for (int i = 0; i < totalSamples; i++)
                    {
                        sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);
                    }

                    // Для кожного каналу семпл-байт
                    for (int channel = 0; channel < sampleCount; channel++)
                    {
                        // Відокремлюємо поточні біти семпла
                        string channelBits = sampleBits.Substring(0, BitsPerSample);
                        // Обрізаємо біт за ключем
                        int bitsToHide = key[keyPosition % key.Length];
                        channelBits = channelBits.Substring(0, channelBits.Length - bitsToHide)
                                    + dataBitBuffer.Substring(0, bitsToHide);
                        dataBitBuffer = dataBitBuffer.Substring(bitsToHide);

                        // Записуємо назад у wavData по байтах
                        for (int bitIndex = channelBits.Length; bitIndex > 0; bitIndex -= 8)
                        {
                            wavData[samplePosition++] = Convert.ToByte(channelBits.Substring(bitIndex - 8, 8), 2);
                        }

                        // Зсуваємо семпл на наступний канал
                        sampleBits = sampleBits.Substring(BitsPerSample);
                    }

                    // Перехід до наступного фрейма
                    samplePosition = blockStartPos + (ulong)BlockAlignBytes;
                    keyPosition++;
                }
            }

            // Доповнюємо нулями, щоб вирівняти до блоку
            while (dataBitBuffer.Length % (sampleCount * key[keyPosition % key.Length]) != 0)
            {
                dataBitBuffer += "0";
            }

            // Запис залишається тих самих дій, допоки є біт у буфері
            while (dataBitBuffer.Length > 0)
            {
                ulong blockStartPos = samplePosition;
                string sampleBits = "";
                for (int i = 0; i < BlockAlignBytes; i++)
                {
                    string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[samplePosition++], 2), 8);
                    sampleBits = byteString + sampleBits;
                }
                samplePosition = blockStartPos;
                int totalSamples = BlockAlignBytes * 8 / BitsPerSample - 1;
                for (int i = 0; i < totalSamples; i++)
                {
                    sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);
                }

                for (int channel = 0; channel < sampleCount; channel++)
                {
                    string channelBits = sampleBits.Substring(0, BitsPerSample);
                    int bitsToHide = key[keyPosition % key.Length];
                    channelBits = channelBits.Substring(0, channelBits.Length - bitsToHide)
                                + dataBitBuffer.Substring(0, bitsToHide);
                    dataBitBuffer = dataBitBuffer.Substring(bitsToHide);

                    for (int bitIndex = channelBits.Length; bitIndex > 0; bitIndex -= 8)
                    {
                        wavData[samplePosition++] = Convert.ToByte(channelBits.Substring(bitIndex - 8, 8), 2);
                    }

                    sampleBits = sampleBits.Substring(BitsPerSample);
                }

                samplePosition = blockStartPos + (ulong)BlockAlignBytes;
                keyPosition++;
            }

            // Зберігаємо модифікований WAV-файл
            File.WriteAllBytes(outputPath, wavData);
        }

        public List<byte> WavDecode(int byteCount, byte[] key)
        {
            // Початкова позиція даних
            ulong samplePosition = DataStartPosition;
            uint keyPosition = 0;
            // Список для збережених байтів
            List<byte> extractedData = new List<byte>();
            // Бітовий буфер для видобування
            string extractBitBuffer = "";
            int sampleCount = NumberOfChannels == 1 ? 1 : 2;

            // Читаємо доти, поки не отримаємо потрібну кількість байтів
            while (extractedData.Count < byteCount)
            {
                ulong blockStartPos = samplePosition;
                string sampleBits = "";

                // Зчитуємо блок даних
                for (int i = 0; i < BlockAlignBytes; i++)
                {
                    string byteString = HelpTools.AutoAddByte(Convert.ToString(wavData[samplePosition++], 2), 8);
                    sampleBits = byteString + sampleBits;
                }
                samplePosition = blockStartPos;

                // Реконструюємо порядок бітів
                int totalSamples = BlockAlignBytes * 8 / BitsPerSample - 1;
                for (int i = 0; i < totalSamples; i++)
                {
                    sampleBits = sampleBits.Substring(BitsPerSample) + sampleBits.Substring(0, BitsPerSample);
                }

                // Для кожного каналу витягуємо приховані біти
                for (int channel = 0; channel < sampleCount; channel++)
                {
                    string channelBits = sampleBits.Substring(0, BitsPerSample);
                    int bitsToExtract = key[keyPosition % key.Length];
                    extractBitBuffer += channelBits.Substring(channelBits.Length - bitsToExtract, bitsToExtract);
                    sampleBits = sampleBits.Substring(BitsPerSample);
                }

                samplePosition = blockStartPos + (ulong)BlockAlignBytes;
                keyPosition++;

                // Щоразу, як накопичилось >=8 біт, конвертуємо в байт
                while (extractBitBuffer.Length >= 8)
                {
                    extractedData.Add(Convert.ToByte(extractBitBuffer.Substring(0, 8), 2));
                    extractBitBuffer = extractBitBuffer.Substring(8);
                }
            }

            return extractedData;
        }
    }
}
