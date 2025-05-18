using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Простір імен для стеганографічних утиліт
namespace Stegonagraph
    {
        // Клас, що реалізує шифрування за алгоритмом AES
        class AES
        {
            // Стандартна таблиця замін (S-Box) для AES. НЕ ЗМІНЮВАТИ!
            private byte[,] SBox = new byte[,]
            {
            { 0x63, 0x7c, 0x77, 0x7b, 0xf2, 0x6b, 0x6f, 0xc5, 0x30, 0x01, 0x67, 0x2b, 0xfe, 0xd7, 0xab, 0x76 },
            { 0xca, 0x82, 0xc9, 0x7d, 0xfa, 0x59, 0x47, 0xf0, 0xad, 0xd4, 0xa2, 0xaf, 0x9c, 0xa4, 0x72, 0xc0 },
            { 0xb7, 0xfd, 0x93, 0x26, 0x36, 0x3f, 0xf7, 0xcc, 0x34, 0xa5, 0xe5, 0xf1, 0x71, 0xd8, 0x31, 0x15 },
            { 0x04, 0xc7, 0x23, 0xc3, 0x18, 0x96, 0x05, 0x9a, 0x07, 0x12, 0x80, 0xe2, 0xeb, 0x27, 0xb2, 0x75 },
            { 0x09, 0x83, 0x2c, 0x1a, 0x1b, 0x6e, 0x5a, 0xa0, 0x52, 0x3b, 0xd6, 0xb3, 0x29, 0xe3, 0x2f, 0x84 },
            { 0x53, 0xd1, 0x00, 0xed, 0x20, 0xfc, 0xb1, 0x5b, 0x6a, 0xcb, 0xbe, 0x39, 0x4a, 0x4c, 0x58, 0xcf },
            { 0xd0, 0xef, 0xaa, 0xfb, 0x43, 0x4d, 0x33, 0x85, 0x45, 0xf9, 0x02, 0x7f, 0x50, 0x3c, 0x9f, 0xa8 },
            { 0x51, 0xa3, 0x40, 0x8f, 0x92, 0x9d, 0x38, 0xf5, 0xbc, 0xb6, 0xda, 0x21, 0x10, 0xff, 0xf3, 0xd2 },
            { 0xcd, 0x0c, 0x13, 0xec, 0x5f, 0x97, 0x44, 0x17, 0xc4, 0xa7, 0x7e, 0x3d, 0x64, 0x5d, 0x19, 0x73 },
            { 0x60, 0x81, 0x4f, 0xdc, 0x22, 0x2a, 0x90, 0x88, 0x46, 0xee, 0xb8, 0x14, 0xde, 0x5e, 0x0b, 0xdb },
            { 0xe0, 0x32, 0x3a, 0x0a, 0x49, 0x06, 0x24, 0x5c, 0xc2, 0xd3, 0xac, 0x62, 0x91, 0x95, 0xe4, 0x79 },
            { 0xe7, 0xc8, 0x37, 0x6d, 0x8d, 0xd5, 0x4e, 0xa9, 0x6c, 0x56, 0xf4, 0xea, 0x65, 0x7a, 0xae, 0x08 },
            { 0xba, 0x78, 0x25, 0x2e, 0x1c, 0xa6, 0xb4, 0xc6, 0xe8, 0xdd, 0x74, 0x1f, 0x4b, 0xbd, 0x8b, 0x8a },
            { 0x70, 0x3e, 0xb5, 0x66, 0x48, 0x03, 0xf6, 0x0e, 0x61, 0x35, 0x57, 0xb9, 0x86, 0xc1, 0x1d, 0x9e },
            { 0xe1, 0xf8, 0x98, 0x11, 0x69, 0xd9, 0x8e, 0x94, 0x9b, 0x1e, 0x87, 0xe9, 0xce, 0x55, 0x28, 0xdf },
            { 0x8c, 0xa1, 0x89, 0x0d, 0xbf, 0xe6, 0x42, 0x68, 0x41, 0x99, 0x2d, 0x0f, 0xb0, 0x54, 0xbb, 0x16 }
            };

            // Стандартна інверсна таблиця замін (Inverse S-Box) для AES. НЕ ЗМІНЮВАТИ!
            private byte[,] InvSBox = new byte[,]
            {
            { 0x52, 0x09, 0x6A, 0xD5, 0x30, 0x36, 0xA5, 0x38, 0xBF, 0x40, 0xA3, 0x9E, 0x81, 0xF3, 0xD7, 0xFB },
            { 0x7C, 0xE3, 0x39, 0x82, 0x9B, 0x2F, 0xFF, 0x87, 0x34, 0x8E, 0x43, 0x44, 0xC4, 0xDE, 0xE9, 0xCB },
            { 0x54, 0x7B, 0x94, 0x32, 0xA6, 0xC2, 0x23, 0x3D, 0xEE, 0x4C, 0x95, 0x0B, 0x42, 0xFA, 0xC3, 0x4E },
            { 0x08, 0x2E, 0xA1, 0x66, 0x28, 0xD9, 0x24, 0xB2, 0x76, 0x5B, 0xA2, 0x49, 0x6D, 0x8B, 0xD1, 0x25 },
            { 0x72, 0xF8, 0xF6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xD4, 0xA4, 0x5C, 0xCC, 0x5D, 0x65, 0xB6, 0x92 },
            { 0x6C, 0x70, 0x48, 0x50, 0xFD, 0xED, 0xB9, 0xDA, 0x5E, 0x15, 0x46, 0x57, 0xA7, 0x8D, 0x9D, 0x84 },
            { 0x90, 0xD8, 0xAB, 0x00, 0x8C, 0xBC, 0xD3, 0x0A, 0xF7, 0xE4, 0x58, 0x05, 0xB8, 0xB3, 0x45, 0x06 },
            { 0xD0, 0x2C, 0x1E, 0x8F, 0xCA, 0x3F, 0x0F, 0x02, 0xC1, 0xAF, 0xBD, 0x03, 0x01, 0x13, 0x8A, 0x6B },
            { 0x3A, 0x91, 0x11, 0x41, 0x4F, 0x67, 0xDC, 0xEA, 0x97, 0xF2, 0xCF, 0xCE, 0xF0, 0xB4, 0xE6, 0x73 },
            { 0x96, 0xAC, 0x74, 0x22, 0xE7, 0xAD, 0x35, 0x85, 0xE2, 0xF9, 0x37, 0xE8, 0x1C, 0x75, 0xDF, 0x6E },
            { 0x47, 0xF1, 0x1A, 0x71, 0x1D, 0x29, 0xC5, 0x89, 0x6F, 0xB7, 0x62, 0x0E, 0xAA, 0x18, 0xBE, 0x1B },
            { 0xFC, 0x56, 0x3E, 0x4B, 0xC6, 0xD2, 0x79, 0x20, 0x9A, 0xDB, 0xC0, 0xFE, 0x78, 0xCD, 0x5A, 0xF4 },
            { 0x1F, 0xDD, 0xA8, 0x33, 0x88, 0x07, 0xC7, 0x31, 0xB1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xEC, 0x5F },
            { 0x60, 0x51, 0x7F, 0xA9, 0x19, 0xB5, 0x4A, 0x0D, 0x2D, 0xE5, 0x7A, 0x9F, 0x93, 0xC9, 0x9C, 0xEF },
            { 0xA0, 0xE0, 0x3B, 0x4D, 0xAE, 0x2A, 0xF5, 0xB0, 0xC8, 0xEB, 0xBB, 0x3C, 0x83, 0x53, 0x99, 0x61 },
            { 0x17, 0x2B, 0x04, 0x7E, 0xBA, 0x77, 0xD6, 0x26, 0xE1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0C, 0x7D }
            };

            // Стандартні константи раундів (Rcon) для AES. НЕ ЗМІНЮВАТИ!
            private byte[] Rcon = new byte[] { 0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36 };

            private int Nb; // Кількість стовпців (32-бітних слів) у State (завжди 4 для AES)
            private int Nk; // Кількість 32-бітних слів у ключі шифрування (4, 6, або 8)
            private int Nr; // Кількість раундів (10, 12, або 14)

            // Конструктор класу AES
            public AES(byte[] masterKey)
            {
                // Nb завжди 4 для AES (4 стовпці по 4 байти = 16 байт блок)
                Nb = 4;

                // Визначення Nk (розмір ключа в словах) та Nr (кількість раундів)
                // на основі довжини наданого ключа
                if (masterKey.Length == 16) // AES-128
                {
                    Nk = 4; // 4 слова * 4 байти/слово = 16 байт
                    Nr = 10; // 10 раундів
                }
                else if (masterKey.Length == 24) // AES-192
                {
                    Nk = 6; // 6 слів * 4 байти/слово = 24 байти
                    Nr = 12; // 12 раундів
                }
                else if (masterKey.Length == 32) // AES-256
                {
                    Nk = 8; // 8 слів * 4 байти/слово = 32 байти
                    Nr = 14; // 14 раундів
                }
                else
                {
                    // Якщо довжина ключа не відповідає стандартам AES, генерується виняток
                    throw new ArgumentException("Некоректна довжина ключа AES. Допустимі довжини: 16, 24 або 32 байти.");
                }
            }

            // Метод шифрування блоку даних
            public byte[] Cipher(byte[] inputDataBlock, byte[] masterKey)
            {
                // State - це двовимірний масив байтів 4xNb, де Nb - кількість стовпців (завжди 4 для AES)
                byte[,] currentState = new byte[4, Nb];
                // Копіювання вхідних даних у State (по стовпцях)
                for (int r = 0; r < 4; r++) // r - рядок
                    for (int c = 0; c < Nb; c++) // c - стовпець
                        currentState[r, c] = inputDataBlock[r + 4 * c];

                // Генерація розширеного набору ключів (раундових ключів)
                byte[,,] expandedKeySet = new byte[Nb * (Nr + 1) / 4, 4, 4]; // [раунд, рядок, стовпець]
                KeyExpansion(masterKey, expandedKeySet);

                // Початкове додавання раундового ключа (AddRoundKey)
                AddRoundKey(currentState, expandedKeySet, 0);

                // Основні раунди шифрування
                for (int currentRound = 1; currentRound < Nr; currentRound++)
                {
                    SubBytes(currentState);      // Операція заміни байтів
                    ShiftRows(currentState);     // Операція зсуву рядків
                    MixColumns(currentState);    // Операція змішування стовпців
                    AddRoundKey(currentState, expandedKeySet, currentRound); // Додавання раундового ключа
                }

                // Останній раунд (без MixColumns)
                SubBytes(currentState);
                ShiftRows(currentState);
                AddRoundKey(currentState, expandedKeySet, Nr);

                // Копіювання результату з State у вихідний масив байтів
                byte[] outputDataBlock = new byte[16];
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < Nb; c++)
                        outputDataBlock[r + 4 * c] = currentState[r, c];

                return outputDataBlock;
            }

            // Метод дешифрування блоку даних
            public byte[] InvCipher(byte[] inputDataBlock, byte[] masterKey)
            {
                byte[,] currentState = new byte[4, Nb];
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < Nb; c++)
                        currentState[r, c] = inputDataBlock[r + 4 * c];

                byte[,,] expandedKeySet = new byte[Nb * (Nr + 1) / 4, 4, 4];
                KeyExpansion(masterKey, expandedKeySet);

                // Початкове додавання раундового ключа (використовується ключ останнього раунду шифрування)
                AddRoundKey(currentState, expandedKeySet, Nr);

                // Основні раунди дешифрування (зворотний порядок операцій)
                for (int currentRound = Nr - 1; currentRound >= 1; currentRound--)
                {
                    InvShiftRows(currentState); // Інверсний зсув рядків
                    InvSubBytes(currentState);  // Інверсна заміна байтів
                    AddRoundKey(currentState, expandedKeySet, currentRound); // Додавання раундового ключа
                    InvMixColumns(currentState); // Інверсне змішування стовпців
                }

                // Останній раунд дешифрування (без InvMixColumns)
                InvShiftRows(currentState);
                InvSubBytes(currentState);
                AddRoundKey(currentState, expandedKeySet, 0);

                byte[] outputDataBlock = new byte[16];
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < Nb; c++)
                        outputDataBlock[r + 4 * c] = currentState[r, c];

                return outputDataBlock;
            }

            // Операція SubBytes: заміна кожного байта в State за допомогою S-Box
            private void SubBytes(byte[,] stateMatrix)
            {
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < Nb; c++)
                        stateMatrix[r, c] = SBox[stateMatrix[r, c] >> 4, stateMatrix[r, c] & 0x0F]; // Старші 4 біти - рядок, молодші 4 - стовпець S-Box
            }

            // Операція InvSubBytes: заміна кожного байта в State за допомогою InvS-Box
            private void InvSubBytes(byte[,] stateMatrix)
            {
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < Nb; c++)
                        stateMatrix[r, c] = InvSBox[stateMatrix[r, c] >> 4, stateMatrix[r, c] & 0x0F];
            }

            // Операція ShiftRows: циклічний зсув байтів у рядках State
            private void ShiftRows(byte[,] stateMatrix)
            {
                byte temporaryByte;
                // Рядок 0: не зсувається
                // Рядок 1: зсув на 1 байт вліво
                temporaryByte = stateMatrix[1, 0];
                stateMatrix[1, 0] = stateMatrix[1, 1];
                stateMatrix[1, 1] = stateMatrix[1, 2];
                stateMatrix[1, 2] = stateMatrix[1, 3];
                stateMatrix[1, 3] = temporaryByte;
                // Рядок 2: зсув на 2 байти вліво
                temporaryByte = stateMatrix[2, 0];
                stateMatrix[2, 0] = stateMatrix[2, 2];
                stateMatrix[2, 2] = temporaryByte;
                temporaryByte = stateMatrix[2, 1];
                stateMatrix[2, 1] = stateMatrix[2, 3];
                stateMatrix[2, 3] = temporaryByte;
                // Рядок 3: зсув на 3 байти вліво
                temporaryByte = stateMatrix[3, 0];
                stateMatrix[3, 0] = stateMatrix[3, 3];
                stateMatrix[3, 3] = stateMatrix[3, 2];
                stateMatrix[3, 2] = stateMatrix[3, 1];
                stateMatrix[3, 1] = temporaryByte;
            }

            // Операція InvShiftRows: інверсний циклічний зсув байтів у рядках State
            private void InvShiftRows(byte[,] stateMatrix)
            {
                byte temporaryByte;
                // Рядок 0: не зсувається
                // Рядок 1: зсув на 1 байт вправо
                temporaryByte = stateMatrix[1, 3];
                stateMatrix[1, 3] = stateMatrix[1, 2];
                stateMatrix[1, 2] = stateMatrix[1, 1];
                stateMatrix[1, 1] = stateMatrix[1, 0];
                stateMatrix[1, 0] = temporaryByte;
                // Рядок 2: зсув на 2 байти вправо
                temporaryByte = stateMatrix[2, 0];
                stateMatrix[2, 0] = stateMatrix[2, 2];
                stateMatrix[2, 2] = temporaryByte;
                temporaryByte = stateMatrix[2, 1];
                stateMatrix[2, 1] = stateMatrix[2, 3];
                stateMatrix[2, 3] = temporaryByte;
                // Рядок 3: зсув на 3 байти вправо
                temporaryByte = stateMatrix[3, 0];
                stateMatrix[3, 0] = stateMatrix[3, 1];
                stateMatrix[3, 1] = stateMatrix[3, 2];
                stateMatrix[3, 2] = stateMatrix[3, 3];
                stateMatrix[3, 3] = temporaryByte;
            }

            // Операція MixColumns: змішування даних у кожному стовпці State
            private void MixColumns(byte[,] stateMatrix)
            {
                byte[,] temporaryState = new byte[4, 4]; // Тимчасовий масив для зберігання результатів
                for (int col = 0; col < 4; col++) // Обробка кожного стовпця
                {
                    temporaryState[0, col] = (byte)(gmul(stateMatrix[0, col], 2, 283) ^ gmul(stateMatrix[1, col], 3, 283) ^ stateMatrix[2, col] ^ stateMatrix[3, col]);
                    temporaryState[1, col] = (byte)(stateMatrix[0, col] ^ gmul(stateMatrix[1, col], 2, 283) ^ gmul(stateMatrix[2, col], 3, 283) ^ stateMatrix[3, col]);
                    temporaryState[2, col] = (byte)(stateMatrix[0, col] ^ stateMatrix[1, col] ^ gmul(stateMatrix[2, col], 2, 283) ^ gmul(stateMatrix[3, col], 3, 283));
                    temporaryState[3, col] = (byte)(gmul(stateMatrix[0, col], 3, 283) ^ stateMatrix[1, col] ^ stateMatrix[2, col] ^ gmul(stateMatrix[3, col], 2, 283));
                }
                // Копіювання результатів з тимчасового масиву назад у stateMatrix
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        stateMatrix[r, c] = temporaryState[r, c];
            }

            // Операція InvMixColumns: інверсне змішування даних у кожному стовпці State
            private void InvMixColumns(byte[,] stateMatrix)
            {
                byte[,] temporaryState = new byte[4, 4];
                for (int col = 0; col < 4; col++)
                {
                    temporaryState[0, col] = (byte)(gmul(stateMatrix[0, col], 14, 283) ^ gmul(stateMatrix[1, col], 11, 283) ^ gmul(stateMatrix[2, col], 13, 283) ^ gmul(stateMatrix[3, col], 9, 283));
                    temporaryState[1, col] = (byte)(gmul(stateMatrix[0, col], 9, 283) ^ gmul(stateMatrix[1, col], 14, 283) ^ gmul(stateMatrix[2, col], 11, 283) ^ gmul(stateMatrix[3, col], 13, 283));
                    temporaryState[2, col] = (byte)(gmul(stateMatrix[0, col], 13, 283) ^ gmul(stateMatrix[1, col], 9, 283) ^ gmul(stateMatrix[2, col], 14, 283) ^ gmul(stateMatrix[3, col], 11, 283));
                    temporaryState[3, col] = (byte)(gmul(stateMatrix[0, col], 11, 283) ^ gmul(stateMatrix[1, col], 13, 283) ^ gmul(stateMatrix[2, col], 9, 283) ^ gmul(stateMatrix[3, col], 14, 283));
                }
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        stateMatrix[r, c] = temporaryState[r, c];
            }

            // Операція AddRoundKey: XOR поточного State з відповідним раундовим ключем
            private void AddRoundKey(byte[,] stateMatrix, byte[,,] expandedKeySet, int roundIndex)
            {
                for (int c = 0; c < Nb; c++) // Для кожного стовпця
                    for (int r = 0; r < 4; r++) // Для кожного рядка
                        stateMatrix[r, c] = (byte)(stateMatrix[r, c] ^ expandedKeySet[roundIndex, r, c]);
            }

            // Розширення ключа: генерація набору раундових ключів з майстер-ключа
            private void KeyExpansion(byte[] masterKeyBytes, byte[,,] expandedKeyStorage)
            {
                byte[] tempWord = new byte[4]; // Тимчасове слово (4 байти) для обчислень

                // Копіювання майстер-ключа в перші Nk слів розширеного ключа
                for (int wordNum = 0; wordNum < Nk; wordNum++)
                {
                    expandedKeyStorage[wordNum / 4, 0, wordNum % 4] = masterKeyBytes[4 * wordNum];
                    expandedKeyStorage[wordNum / 4, 1, wordNum % 4] = masterKeyBytes[4 * wordNum + 1];
                    expandedKeyStorage[wordNum / 4, 2, wordNum % 4] = masterKeyBytes[4 * wordNum + 2];
                    expandedKeyStorage[wordNum / 4, 3, wordNum % 4] = masterKeyBytes[4 * wordNum + 3];
                }

                // Генерація решти слів розширеного ключа
                for (int wordNum = Nk; wordNum < Nb * (Nr + 1); wordNum++)
                {
                    // Копіювання попереднього слова (w[i-1]) у tempWord
                    tempWord[0] = expandedKeyStorage[(wordNum - 1) / 4, 0, (wordNum - 1) % 4];
                    tempWord[1] = expandedKeyStorage[(wordNum - 1) / 4, 1, (wordNum - 1) % 4];
                    tempWord[2] = expandedKeyStorage[(wordNum - 1) / 4, 2, (wordNum - 1) % 4];
                    tempWord[3] = expandedKeyStorage[(wordNum - 1) / 4, 3, (wordNum - 1) % 4];

                    // Якщо це перше слово нового раундового ключа (i % Nk == 0)
                    if (wordNum % Nk == 0)
                    {
                        // RotWord: циклічний зсув байтів у слові вліво
                        byte firstByte = tempWord[0];
                        tempWord[0] = tempWord[1];
                        tempWord[1] = tempWord[2];
                        tempWord[2] = tempWord[3];
                        tempWord[3] = firstByte;

                        // SubWord: заміна кожного байта слова за допомогою S-Box
                        tempWord[0] = SBox[tempWord[0] >> 4, tempWord[0] & 0x0F];
                        tempWord[1] = SBox[tempWord[1] >> 4, tempWord[1] & 0x0F];
                        tempWord[2] = SBox[tempWord[2] >> 4, tempWord[2] & 0x0F];
                        tempWord[3] = SBox[tempWord[3] >> 4, tempWord[3] & 0x0F];

                        // XOR з Rcon[i/Nk] (тільки для першого байта слова)
                        tempWord[0] = (byte)(tempWord[0] ^ Rcon[wordNum / Nk]);
                    }
                    // Для AES-256 (Nk=8), якщо i % Nk == 4, застосовується SubWord до tempWord
                    else if (Nk > 6 && wordNum % Nk == 4)
                    {
                        tempWord[0] = SBox[tempWord[0] >> 4, tempWord[0] & 0x0F];
                        tempWord[1] = SBox[tempWord[1] >> 4, tempWord[1] & 0x0F];
                        tempWord[2] = SBox[tempWord[2] >> 4, tempWord[2] & 0x0F];
                        tempWord[3] = SBox[tempWord[3] >> 4, tempWord[3] & 0x0F];
                    }

                    // w[i] = w[i-Nk] XOR tempWord
                    expandedKeyStorage[wordNum / 4, 0, wordNum % 4] = (byte)(expandedKeyStorage[(wordNum - Nk) / 4, 0, (wordNum - Nk) % 4] ^ tempWord[0]);
                    expandedKeyStorage[wordNum / 4, 1, wordNum % 4] = (byte)(expandedKeyStorage[(wordNum - Nk) / 4, 1, (wordNum - Nk) % 4] ^ tempWord[1]);
                    expandedKeyStorage[wordNum / 4, 2, wordNum % 4] = (byte)(expandedKeyStorage[(wordNum - Nk) / 4, 2, (wordNum - Nk) % 4] ^ tempWord[2]);
                    expandedKeyStorage[wordNum / 4, 3, wordNum % 4] = (byte)(expandedKeyStorage[(wordNum - Nk) / 4, 3, (wordNum - Nk) % 4] ^ tempWord[3]);
                }
            }

            // Множення в полі Галуа GF(2^8) для MixColumns та InvMixColumns
            // irreduciblePolynomial - незвідний поліном (x^8 + x^4 + x^3 + x + 1, або 0x11B)
            private Byte gmul(Byte firstOperand, Byte secondOperand, UInt16 irreduciblePolynomial)
            {
                UInt16 operandA = firstOperand;
                UInt16 operandB = secondOperand;
                UInt16 productSum = 0;
                String binaryRepresentationA = Convert.ToString(operandA, 2); // Двійкове представлення першого операнда

                // Алгоритм "селянського множення" (Peasant multiplication)
                for (int bitIndex = 0; bitIndex < binaryRepresentationA.Length; bitIndex++)
                {
                    if (binaryRepresentationA[binaryRepresentationA.Length - 1 - bitIndex] == '1')
                    {
                        productSum ^= (UInt16)(operandB << bitIndex); // XOR з другим операндом, зсунутим на bitIndex
                    }
                }

                // Зменшення результату за модулем незвідного полінома
                // Поки степінь productSum більша або дорівнює степені irreduciblePolynomial
                while (productSum != 0 && irreduciblePolynomial != 0 && (int)Math.Log(productSum, 2) - (int)Math.Log(irreduciblePolynomial, 2) >= 0)
                {
                    // Визначаємо, на скільки потрібно зсунути поліном
                    UInt16 shiftAmount = (UInt16)((int)Math.Log(productSum, 2) - (int)Math.Log(irreduciblePolynomial, 2));
                    productSum ^= (UInt16)(irreduciblePolynomial << shiftAmount); // XOR з зсунутим поліномом
                }
                return Convert.ToByte(productSum); // Повертаємо результат як байт
            }
        }
    }
