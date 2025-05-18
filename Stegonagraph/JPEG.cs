using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text; // Додано для StringBuilder, якщо він буде потрібен у повній версії класу

// Простір імен, вказаний користувачем
namespace Stegonagraph
{
    // Клас для обробки контейнера JPEG
    class JPEG
    {
        // Скільки байт інформації може зберігати контейнер
        public UInt64 info { set; get; }
        public List<byte> secretFiles { set; get; }

        // JPEG AC DC коефіцієнти
        private int[] compCount = new int[3];
        private List<HuffTree[][]> DHT_DC = new List<HuffTree[][]>();
        private List<HuffTree[][]> DHT_AC = new List<HuffTree[][]>();

        // Байтовий потік JPEG файлу
        private byte[] arrayJpeg;

        public JPEG(String filePath, Boolean convertToBaseline)
        {
            if (convertToBaseline)
                JpegCompressor(filePath); // Конвертація JPEG Progressive в JPEG Baseline
            else
                arrayJpeg = File.ReadAllBytes(filePath); // Отримати jpeg в байтах

            UInt64 currentFilePosition = 0; // Поточна позиція у файлі

            while (true)
            {
                // 0xFFC0, 0xFFC1, 0xFFC2 - ініціалізація початкових коефіцієнтів (SOF - Start Of Frame)
                if ((arrayJpeg[currentFilePosition] == 255 && arrayJpeg[currentFilePosition + 1] == 192)  // SOF0 (Baseline DCT)
                   || (arrayJpeg[currentFilePosition] == 255 && arrayJpeg[currentFilePosition + 1] == 193)  // SOF1 (Extended sequential DCT)
                   || (arrayJpeg[currentFilePosition] == 255 && arrayJpeg[currentFilePosition + 1] == 194)) // SOF2 (Progressive DCT)
                {
                    int[] componentHorizontalSampling = new int[3]; // Горизонтальна дискретизація для компонентів
                    int[] componentVerticalSampling = new int[3];   // Вертикальна дискретизація для компонентів
                    currentFilePosition += 11; // Пропускаємо частину заголовка SOF

                    for (int componentIdx = 0; componentIdx < 3; componentIdx++) // Зазвичай 3 компоненти (Y, Cb, Cr)
                    {
                        componentHorizontalSampling[componentIdx] = arrayJpeg[currentFilePosition] >> 4; // Старші 4 біти
                        componentVerticalSampling[componentIdx] = arrayJpeg[currentFilePosition] & 15;   // Молодші 4 біти
                        currentFilePosition += 3; // Перехід до наступного компонента

                        compCount[componentIdx] = (int)(componentHorizontalSampling[componentIdx] * componentVerticalSampling[componentIdx]);
                    }
                    currentFilePosition -= 2; // Корекція позиції для наступного маркера
                }

                // 0xFFC4 - DHT (Define Huffman Table) - знаходження кодів з дерева Хаффмана
                if (arrayJpeg[currentFilePosition] == 255 && arrayJpeg[currentFilePosition + 1] == 196)
                {
                    currentFilePosition += 4; // Пропускаємо маркер і довжину сегмента DHT
                    // Визначаємо клас таблиці (DC або AC) та її ідентифікатор
                    int huffmanTableClassAndId = Convert.ToInt32(AddByte(Convert.ToString(arrayJpeg[currentFilePosition], 2)).Substring(0, 4), 2);

                    HuffTree[][] huffmanTableStructure = new HuffTree[16][]; // Структура для зберігання таблиці Хаффмана
                    List<int> huffmanCodeValues = new List<int>();     // Список для генерації кодів Хаффмана
                    huffmanCodeValues.Add(0);
                    huffmanCodeValues.Add(1);

                    // Читання кількості кодів кожної довжини (16 байт BITS)
                    for (int lengthIndex = 0; lengthIndex < 16; lengthIndex++)
                    {
                        currentFilePosition++;
                        int numberOfCodesAtThisLength = arrayJpeg[currentFilePosition];
                        huffmanTableStructure[lengthIndex] = new HuffTree[numberOfCodesAtThisLength];

                        for (int codeIdx = 0; codeIdx < huffmanTableStructure[lengthIndex].Length; codeIdx++)
                        {
                            huffmanTableStructure[lengthIndex][codeIdx] = new HuffTree(huffmanCodeValues[0], 0); // Призначаємо код, значення символу буде пізніше
                            huffmanCodeValues.RemoveRange(0, 1);
                        }

                        // Генерація кодів для наступної довжини
                        int currentCodesCount = huffmanCodeValues.Count;
                        for (int genIdx = 0; genIdx < currentCodesCount; genIdx++)
                        {
                            huffmanCodeValues.Add(huffmanCodeValues[genIdx] << 1);
                            huffmanCodeValues.Add((huffmanCodeValues[genIdx] << 1) + 1);
                        }
                        huffmanCodeValues.RemoveRange(0, currentCodesCount);
                    }

                    // Читання значень символів (HUFFVAL)
                    for (int lengthIdx = 0; lengthIdx < 16; lengthIdx++)
                        for (int symbolIdx = 0; symbolIdx < huffmanTableStructure[lengthIdx].Length; symbolIdx++)
                            huffmanTableStructure[lengthIdx][symbolIdx].Val = arrayJpeg[++currentFilePosition];

                    // Ініціалізація DC AC коефіцієнтів
                    if (huffmanTableClassAndId == 0) // DC таблиця
                        DHT_DC.Add(huffmanTableStructure);
                    else // AC таблиця
                        DHT_AC.Add(huffmanTableStructure);
                }

                // 0xFFDA (SOS - Start Of Scan) або 0xFFD9 (EOI - End Of Image) - кінець JPEG файлу (або початок даних сканування)
                if (arrayJpeg[currentFilePosition] == 255 && (arrayJpeg[currentFilePosition + 1] == 217 || arrayJpeg[currentFilePosition + 1] == 218))
                    break; // Вихід з циклу, якщо досягнуто SOS або EOI

                currentFilePosition++;
            }
        }

        // Метод для отримання інформації про те, скільки байт може зберігати JPEG файл
        public void GetInfo(byte[] encryptionKey)
        {
            jpegProcessing(encryptionKey, 0); // Тип 0 - Info
        }

        // Метод для кодування (вбудовування) даних у JPEG файл
        public void jpegEncode(byte[] dataToHide, String outputPath, byte[] encryptionKey)
        {
            jpegProcessing(encryptionKey, 1, dataToHide, outputPath); // Тип 1 - Encode
        }

        // Метод для декодування (вилучення) даних з JPEG файлу
        public void jpegDecode(byte[] encryptionKey)
        {
            jpegProcessing(encryptionKey, 2); // Тип 2 - Decode
        }

        // Основний метод обробки JPEG
        // typeProcess:
        // 0 - Info (отримання інформації про ємність)
        // 1 - Encode (кодування даних)
        // 2 - Decode (декодування даних)
        private void jpegProcessing(byte[] currentKey, byte processMode, byte[] dataToEmbed = null, String outputFilePath = null)
        {
            UInt64 streamPosition = 0;         // Поточна позиція в байтовому потоці JPEG
            UInt32 steganographyPosition = 0;  // Позиція для стеганографічного ключа
            info = 0;                          // Обнуляємо інформацію про ємність

            // Змінні для режиму кодування (Encode)
            int dataToEmbedIndex = 0;          // Індекс поточного байта даних для вбудовування
            String dataToEmbedBitChunk = "";   // Поточний бітовий чанк даних для вбудовування

            List<byte> outputJpegBytes = new List<byte>(); // Список для формування вихідного JPEG (при кодуванні)
            String outputJpegBitChunk = "";    // Поточний бітовий чанк для вихідного JPEG

            // Змінні для режиму декодування (Decode)
            secretFiles = new List<byte>();    // Список для зберігання вилучених секретних файлів
            String extractedDataBitChunk = ""; // Поточний бітовий чанк вилучених даних

            while (true) // Цикл по байтовому потоку JPEG
            {
                // 0xFFDA - SOS (Start Of Scan) - початок читання кодів Хаффмана (даних зображення)
                if (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 218)
                {
                    List<int> dcTableIndices = new List<int>(); // Індекси DC таблиць для компонентів
                    List<int> acTableIndices = new List<int>(); // Індекси AC таблиць для компонентів

                    streamPosition++; // Пропускаємо 0xFF
                    UInt64 sosHeaderStart = streamPosition; // Запам'ятовуємо початок заголовка SOS
                    // Довжина заголовка SOS (2 байти)
                    int sosHeaderLength = (arrayJpeg[++streamPosition] << 8) + arrayJpeg[++streamPosition];
                    // Кількість компонентів у скані (1 байт)
                    int componentsInScan = arrayJpeg[++streamPosition];
                    streamPosition++;

                    // Читання ідентифікаторів таблиць Хаффмана для кожного компонента
                    for (int compIdx = 0; compIdx < componentsInScan; compIdx++)
                    {
                        streamPosition++; // Пропускаємо ідентифікатор компонента
                        // Старші 4 біти - індекс DC таблиці, молодші 4 біти - індекс AC таблиці
                        dcTableIndices.Add(arrayJpeg[streamPosition] >> 4);
                        acTableIndices.Add(arrayJpeg[streamPosition] & 15);
                        streamPosition++;
                    }
                    // Переміщення позиції до початку власне даних сканування (після заголовка SOS)
                    streamPosition = sosHeaderStart + (ulong)sosHeaderLength + 1;

                    // Якщо режим кодування, копіюємо частину файлу до SOS у вихідний потік
                    if (processMode == 1)
                    {
                        for (UInt64 byteIdx = 0; byteIdx < streamPosition; byteIdx++)
                            outputJpegBytes.Add(arrayJpeg[byteIdx]);
                    }

                    String huffmanEncodedDataStream = ""; // Потік даних, закодованих Хаффманом
                    int mcuBlockCounter = 0;              // Лічильник блоків MCU (Minimum Coded Unit)
                    int currentComponentIndex = 0;        // Індекс поточного оброблюваного компонента

                    // Основний цикл обробки даних сканування
                    while (true)
                    {
                        // Визначення кількості блоків для поточного компонента
                        if (mcuBlockCounter < compCount[currentComponentIndex % compCount.Length])
                            mcuBlockCounter++;
                        else
                        { mcuBlockCounter = 1; currentComponentIndex++; }

                        String currentHuffmanCode = "";       // Поточний код Хаффмана, що зчитується
                        int coefficientsProcessed = 0;      // Кількість оброблених коефіцієнтів у поточному блоці (до 64)
                        int streamBitIndex = 0;             // Індекс біта у huffmanEncodedDataStream

                        // Вибір відповідних таблиць Хаффмана (DC та AC) для поточного компонента
                        HuffTree[][] currentDcTable = DHT_DC[dcTableIndices[currentComponentIndex % compCount.Length]];
                        HuffTree[][] currentAcTable = DHT_AC[acTableIndices[currentComponentIndex % compCount.Length]];

                        // Обробка 64 коефіцієнтів DCT у блоці
                        while (coefficientsProcessed < 64)
                        {
                            // Обробка вихідного потоку для режимів кодування/декодування
                            switch (processMode)
                            {
                                case 1: // Encode
                                    // Якщо в outputJpegBitChunk накопичився байт, додаємо його до outputJpegBytes
                                    while (outputJpegBitChunk.Length >= 8)
                                    {
                                        byte byteToWrite = Convert.ToByte(outputJpegBitChunk.Substring(0, 8), 2);
                                        outputJpegBytes.Add(byteToWrite);
                                        // Байт-стаффінг: якщо пишемо 0xFF, додаємо 0x00
                                        if (byteToWrite == 255)
                                            outputJpegBytes.Add(0);
                                        outputJpegBitChunk = outputJpegBitChunk.Substring(8);
                                    }
                                    break;
                                case 2: // Decode
                                    // Якщо в extractedDataBitChunk накопичився байт, додаємо його до secretFiles
                                    while (extractedDataBitChunk.Length >= 8)
                                    {
                                        secretFiles.Add(Convert.ToByte(extractedDataBitChunk.Substring(0, 8), 2));
                                        extractedDataBitChunk = extractedDataBitChunk.Substring(8);
                                    }
                                    break;
                            }

                            // Поповнення huffmanEncodedDataStream, якщо в ньому недостатньо біт
                            while (huffmanEncodedDataStream.Length < 32) // Читаємо порціями для ефективності
                            {
                                // Перевірка на кінець файлу (маркер EOI 0xFFD9)
                                if (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 217)
                                    break; // Кінець даних сканування

                                // Обробка байт-стаффінгу (0xFF00 -> 0xFF)
                                if (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 0)
                                {
                                    huffmanEncodedDataStream += AddByte(Convert.ToString(arrayJpeg[streamPosition], 2)); // Додаємо 0xFF
                                    streamPosition++; // Пропускаємо 0x00
                                }
                                else
                                {
                                    huffmanEncodedDataStream += AddByte(Convert.ToString(arrayJpeg[streamPosition], 2));
                                }
                                streamPosition++;
                            }
                            // Якщо huffmanEncodedDataStream порожній після спроби читання, це може бути кінець даних
                            if (huffmanEncodedDataStream.Length <= streamBitIndex && (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 217)) break;


                            currentHuffmanCode += huffmanEncodedDataStream[streamBitIndex++]; // Додаємо наступний біт до поточного коду

                            // Обробка DC коефіцієнта (перший у блоці)
                            if (coefficientsProcessed == 0)
                            {
                                // Пошук коду в DC таблиці
                                for (int lenIdx = 0; lenIdx < currentDcTable[currentHuffmanCode.Length - 1].Length; lenIdx++)
                                {
                                    if (currentDcTable[currentHuffmanCode.Length - 1][lenIdx].Code == Convert.ToInt32(currentHuffmanCode, 2))
                                    {
                                        if (processMode == 1) // Encode
                                        {
                                            // Додаємо знайдений код Хаффмана до вихідного потоку
                                            outputJpegBitChunk += huffmanEncodedDataStream.Substring(0, currentHuffmanCode.Length);
                                        }
                                        // Видаляємо оброблений код з huffmanEncodedDataStream
                                        huffmanEncodedDataStream = huffmanEncodedDataStream.Substring(currentHuffmanCode.Length);

                                        // Якщо значення символу не нульове, обробляємо амплітуду
                                        if (currentDcTable[currentHuffmanCode.Length - 1][lenIdx].Val != 0)
                                        {
                                            int amplitudeLength = currentDcTable[currentHuffmanCode.Length - 1][lenIdx].Val & 15; // Довжина амплітуди
                                            int zeroRunLength = currentDcTable[currentHuffmanCode.Length - 1][lenIdx].Val >> 4;  // Кількість нулів (для AC)

                                            // Для DC коефіцієнта zeroRunLength не використовується, але структура Val та сама
                                            for (int k = 0; k < zeroRunLength; k++) // Це стосується AC, для DC тут буде 0
                                                coefficientsProcessed++;

                                            if (processMode == 1) // Encode
                                            {
                                                // Додаємо амплітуду до вихідного потоку
                                                outputJpegBitChunk += huffmanEncodedDataStream.Substring(0, amplitudeLength);
                                            }
                                            // Видаляємо амплітуду з huffmanEncodedDataStream
                                            huffmanEncodedDataStream = huffmanEncodedDataStream.Substring(amplitudeLength);
                                        }
                                        streamBitIndex = 0;         // Скидаємо індекс біта
                                        currentHuffmanCode = "";    // Скидаємо поточний код
                                        coefficientsProcessed++;    // Переходимо до наступного коефіцієнта
                                        break; // Вихід з циклу пошуку коду
                                    }
                                }
                            }
                            // Обробка AC коефіцієнтів
                            else
                            {
                                // Пошук коду в AC таблиці
                                for (int lenIdx = 0; lenIdx < currentAcTable[currentHuffmanCode.Length - 1].Length; lenIdx++)
                                {
                                    if (currentAcTable[currentHuffmanCode.Length - 1][lenIdx].Code == Convert.ToInt32(currentHuffmanCode, 2))
                                    {
                                        if (processMode == 1) // Encode
                                        {
                                            outputJpegBitChunk += huffmanEncodedDataStream.Substring(0, currentHuffmanCode.Length);
                                        }
                                        huffmanEncodedDataStream = huffmanEncodedDataStream.Substring(currentHuffmanCode.Length);

                                        // Якщо значення символу 0x00 (EOB - End Of Block) або 0xF0 (ZRL - Zero Run Length)
                                        if (currentAcTable[currentHuffmanCode.Length - 1][lenIdx].Val == 0) // EOB
                                        {
                                            goto block_processed_label; // Перехід до кінця обробки блоку
                                        }
                                        else // Обробка амплітуди та кількості нулів
                                        {
                                            int amplitudeLength = currentAcTable[currentHuffmanCode.Length - 1][lenIdx].Val & 15;
                                            int zeroRunLength = currentAcTable[currentHuffmanCode.Length - 1][lenIdx].Val >> 4;

                                            for (int k = 0; k < zeroRunLength; k++) // Додаємо нулі
                                                coefficientsProcessed++;

                                            // Обробка в залежності від режиму
                                            switch (processMode)
                                            {
                                                case 0: // Info - розрахунок ємності
                                                    if (amplitudeLength > currentKey[steganographyPosition % currentKey.Length])
                                                        info += (ulong)currentKey[steganographyPosition % currentKey.Length];
                                                    else
                                                        info += (ulong)amplitudeLength;
                                                    break;
                                                case 1: // Encode - вбудовування даних
                                                    // Перевірка, чи є ще дані для вбудовування
                                                    if (!(dataToEmbedIndex == dataToEmbed.Length && dataToEmbedBitChunk == ""))
                                                    {
                                                        if (amplitudeLength > currentKey[steganographyPosition % currentKey.Length])
                                                        {
                                                            // Готуємо біти для вбудовування
                                                            dataToEmbedBitChunk = WriteCode(ref dataToEmbedIndex, dataToEmbedBitChunk, dataToEmbed, currentKey[steganographyPosition % currentKey.Length]);
                                                            // Вбудовуємо: частина оригінальних біт + біти даних
                                                            outputJpegBitChunk += huffmanEncodedDataStream.Substring(0, amplitudeLength - currentKey[steganographyPosition % currentKey.Length]) + dataToEmbedBitChunk.Substring(0, currentKey[steganographyPosition % currentKey.Length]);
                                                            dataToEmbedBitChunk = dataToEmbedBitChunk.Substring(currentKey[steganographyPosition % currentKey.Length]); // Залишок біт даних
                                                            info += (ulong)currentKey[steganographyPosition % currentKey.Length]; // Оновлюємо інформацію про записані біти
                                                        }
                                                        else
                                                        {
                                                            dataToEmbedBitChunk = WriteCode(ref dataToEmbedIndex, dataToEmbedBitChunk, dataToEmbed, currentKey[steganographyPosition % currentKey.Length]);
                                                            outputJpegBitChunk += dataToEmbedBitChunk.Substring(0, amplitudeLength); // Вбудовуємо стільки біт, скільки дозволяє амплітуда
                                                            dataToEmbedBitChunk = dataToEmbedBitChunk.Substring(amplitudeLength);
                                                            info += (ulong)amplitudeLength;
                                                        }
                                                    }
                                                    else // Дані для вбудовування закінчилися, просто копіюємо амплітуду
                                                    {
                                                        outputJpegBitChunk += huffmanEncodedDataStream.Substring(0, amplitudeLength);
                                                    }
                                                    break;
                                                case 2: // Decode - вилучення даних
                                                    if (amplitudeLength > currentKey[steganographyPosition % currentKey.Length])
                                                    {
                                                        // Вилучаємо біти з кінця амплітуди
                                                        extractedDataBitChunk += huffmanEncodedDataStream.Substring(amplitudeLength - currentKey[steganographyPosition % currentKey.Length], currentKey[steganographyPosition % currentKey.Length]);
                                                        info += (ulong)currentKey[steganographyPosition % currentKey.Length];
                                                    }
                                                    else
                                                    {
                                                        extractedDataBitChunk += huffmanEncodedDataStream.Substring(0, amplitudeLength);
                                                        info += (ulong)amplitudeLength;
                                                    }
                                                    break;
                                            }
                                            steganographyPosition++; // Перехід до наступного значення ключа
                                            huffmanEncodedDataStream = huffmanEncodedDataStream.Substring(amplitudeLength); // Видаляємо оброблену амплітуду
                                        }
                                        streamBitIndex = 0;
                                        currentHuffmanCode = "";
                                        coefficientsProcessed++;
                                        break; // Вихід з циклу пошуку коду
                                    }
                                }
                            }
                        } // Кінець циклу while (coefficientsProcessed < 64)
                    block_processed_label:; // Мітка для переходу при EOB

                        // Перевірка на кінець даних сканування (маркер EOI)
                        if ((arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 254) /* APPn, COM */ || (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 217) /* EOI */)
                        {
                            // Завершальні операції в залежності від режиму
                            switch (processMode)
                            {
                                case 0: // Info
                                    info = info / 8; // Переводимо біти в байти
                                    break;
                                case 1: // Encode
                                    outputJpegBitChunk += huffmanEncodedDataStream; // Додаємо залишок бітового потоку
                                    // Записуємо останні байти
                                    while (outputJpegBitChunk.Length >= 8)
                                    {
                                        byte byteToWrite = Convert.ToByte(outputJpegBitChunk.Substring(0, 8), 2);
                                        outputJpegBytes.Add(byteToWrite);
                                        if (byteToWrite == 255) outputJpegBytes.Add(0);
                                        outputJpegBitChunk = outputJpegBitChunk.Substring(8);
                                    }
                                    // Додаємо маркер кінця файлу EOI
                                    outputJpegBytes.Add(255);
                                    outputJpegBytes.Add(217);
                                    Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
                                    File.WriteAllBytes(outputFilePath, outputJpegBytes.ToArray()); // Зберігаємо змінений файл
                                    break;
                                case 2: // Decode
                                    // Записуємо останні байти вилучених даних
                                    while (extractedDataBitChunk.Length >= 8)
                                    {
                                        secretFiles.Add(Convert.ToByte(extractedDataBitChunk.Substring(0, 8), 2));
                                        // Тут не потрібен байт-стаффінг, бо це вже розшифровані дані
                                        extractedDataBitChunk = extractedDataBitChunk.Substring(8);
                                    }
                                    break;
                            }
                            return; // Завершення обробки
                        }
                    } // Кінець циклу while (true) - обробка даних сканування
                } // Кінець if (arrayJpeg[streamPosition] == 255 && arrayJpeg[streamPosition + 1] == 218) - SOS
                streamPosition++; // Перехід до наступного байта, якщо не SOS
                                  // Правильне порівняння, без неоднозначності:
                if (streamPosition >= (ulong)(arrayJpeg.Length - 1))
                    break;
            } // Кінець циклу while (true) - потік JPEG
        }

        // Допоміжний метод для доповнення бінарної строки нулями до 8 біт
        private String AddByte(String binaryRepresentation)
        {
            while (binaryRepresentation.Length < 8)
                binaryRepresentation = "0" + binaryRepresentation;
            return binaryRepresentation;
        }

        // Допоміжний метод для доповнення шістнадцяткової строки нулями до 2 символів
        private String AddHexByte(String hexRepresentation)
        {
            return hexRepresentation.Length < 2 ? "0" + hexRepresentation : hexRepresentation;
        }

        // Метод для конвертації JPEG Progressive в JPEG Baseline (спрощено)
        // Цей метод намагається видалити деякі специфічні для Progressive JPEG маркери
        // і зберегти файл з параметрами, що сприяють Baseline формату.
        // УВАГА: Цей метод є спрощенням і може не завжди коректно працювати з усіма Progressive JPEG.
        private void JpegCompressor(String imagePath)
        {
            byte[] originalBytes = File.ReadAllBytes(imagePath);
            List<byte> jpegByteList = originalBytes.OfType<byte>().ToList();

            // Спроба видалити DQT (Define Quantization Table) маркери, якщо їх забагато,
            // що іноді характерно для Progressive JPEG, залишаючи тільки необхідні.
            // Це дуже грубий підхід.
            int dqtMarkerCount = 0;
            for (int idx = 0; idx < jpegByteList.Count - 1; idx++)
                if (jpegByteList[idx] == 255 && jpegByteList[idx + 1] == 219) // 0xFFDB - DQT
                    dqtMarkerCount++;

            int dqtSegmentStartPos = 0;
            // Знаходимо позицію першого DQT сегмента
            for (int idx = 0; idx < jpegByteList.Count - 1; idx++)
                if (jpegByteList[idx] == 255 && jpegByteList[idx + 1] == 219)
                {
                    dqtSegmentStartPos = idx;
                    // Пропускаємо всі DQT сегменти, крім, можливо, перших кількох
                    // (тут логіка пропуску/видалення DQT сегментів спрощена)
                    while (idx < jpegByteList.Count - 1 && jpegByteList[idx] == 255 && jpegByteList[idx + 1] == 219)
                    {
                        idx += 2; // Пропускаємо маркер
                        if (idx + 1 < jpegByteList.Count)
                        {
                            int segmentLength = Convert.ToInt32(AddByte(Convert.ToString(jpegByteList[idx], 2)) + AddByte(Convert.ToString(jpegByteList[idx + 1], 2)), 2);
                            idx += segmentLength; // Пропускаємо тіло сегмента
                        }
                        else { break; }
                    }
                    break; // Обробляємо тільки перший набір DQT
                }

            // Видаляємо частину файлу до знайденого DQT (якщо він не на початку)
            // і вставляємо стандартний заголовок SOI + APP0 (JFIF)
            // Це також дуже спрощений підхід.
            if (dqtSegmentStartPos > 0 && dqtSegmentStartPos < jpegByteList.Count)
            {
                jpegByteList.RemoveRange(0, dqtSegmentStartPos);
            }
            // Вставляємо SOI (0xFFD8) і APP0 (0xFFE0) маркер для JFIF (типово для Baseline)
            // Довжина APP0 (16 байт) + "JFIF\0" + версія + ...
            // Тут вставляється фіктивний APP0 маркер, що може бути некоректним.
            jpegByteList.InsertRange(0, new List<byte>() { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 });


            // Зберігаємо тимчасовий файл "template.jpg"
            string templateFilePath = Path.Combine(Path.GetTempPath(), "template_steg.jpg");
            File.WriteAllBytes(templateFilePath, jpegByteList.ToArray());

            FileInfo originalFileInfo = new FileInfo(imagePath);
            string outputConvertedPath = Path.Combine(Path.GetTempPath(), "baseline_" + originalFileInfo.Name);


            // Використовуємо System.Drawing для перезбереження з високою якістю,
            // що часто призводить до Baseline формату, якщо не вказано інше.
            try
            {
                using (Image sourceImage = Image.FromFile(templateFilePath))
                {
                    ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                    EncoderParameters encoderParams = new EncoderParameters(1); // Один параметр - якість
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L); // Максимальна якість
                    // При збереженні без явного вказання ScanMethod, багато енкодерів за замовчуванням використовують Baseline.
                    sourceImage.Save(outputConvertedPath, jpegCodec, encoderParams);
                }
                arrayJpeg = File.ReadAllBytes(outputConvertedPath); // Читаємо результат
            }
            catch (Exception ex)
            {
                // Якщо сталася помилка, використовуємо оригінальний файл
                Debug.WriteLine($"Помилка при конвертації JPEG: {ex.Message}. Використовується оригінальний файл.");
                arrayJpeg = File.ReadAllBytes(imagePath);
            }
            finally
            {
                // Видалення тимчасових файлів
                if (File.Exists(templateFilePath)) File.Delete(templateFilePath);
                if (File.Exists(outputConvertedPath)) File.Delete(outputConvertedPath);
            }
        }

        // Метод для запису (формування) бітового чанку даних для вбудовування
        private String WriteCode(ref int currentDataByteIndex, String currentBitChunk, byte[] sourceDataArray, int bitsToTakeFromKey)
        {
            // Якщо в поточному бітовому чанку менше біт, ніж потрібно згідно ключа
            if (currentBitChunk.Length < bitsToTakeFromKey)
            {
                // Якщо ще є байти в масиві даних для вбудовування
                if (currentDataByteIndex != sourceDataArray.Length)
                    currentBitChunk += AddByte(Convert.ToString(sourceDataArray[currentDataByteIndex++], 2)); // Додаємо наступний байт даних
                else // Якщо дані закінчилися, доповнюємо нулями
                {
                    while (currentBitChunk.Length % bitsToTakeFromKey != 0 && currentBitChunk.Length < bitsToTakeFromKey) // Доповнюємо до кратності або до потрібної довжини
                        currentBitChunk += "0";
                    // Якщо після доповнення все ще не вистачає, доповнюємо до bitsToTakeFromKey
                    if (currentBitChunk.Length > 0 && currentBitChunk.Length < bitsToTakeFromKey)
                    {
                        currentBitChunk += new string('0', bitsToTakeFromKey - currentBitChunk.Length);
                    }
                }
            }
            return currentBitChunk;
        }
    }
}
