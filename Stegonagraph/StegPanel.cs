// StegPanel.cs – C# 7.3‑compatible version (no Span/Range)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Stegonagraph
{
    /// <summary>
    ///     Lightweight AES helper that wraps the <see cref="AES"/> class shipped with the project
    ///     and exposes Encrypt / Decrypt that operate on whole byte arrays. 100 % compatible with
    ///     C# 7.3 (no Index/Range/Span).
    /// </summary>
    internal static class AesHelper
    {
        // Цей метод DeriveKey не використовується, оскільки ваш AES клас приймає String ключ
        // і має власну логіку його обробки (дублювання та обрізка до 16 символів).
        // Для відповідності конструктору AES(String key), пароль буде передаватися напряму.
        /*
        private static readonly int[] _legalKeySizes = { 16, 24, 32 };
        private static byte[] DeriveKey(string password)
        {
            byte[] raw = Encoding.UTF8.GetBytes(password ?? string.Empty);
            int target = 16; // Ваш AES(String key) завжди працює з 16-символьним ключем
            byte[] key = new byte[target];
            if (raw.Length == 0)
            {
                for (int i = 0; i < target; i++) key[i] = (byte)' '; // Наприклад, пробілами, якщо порожній пароль
            }
            else
            {
                for (int i = 0; i < target; i++)
                    key[i] = raw[i % raw.Length];
            }
            return key;
        }
        */

        /// <summary>PKCS‑7 padding (adds 1–N bytes, where N is block size, to make length a multiple of N)</summary>
        private static byte[] Pad(byte[] data, int blockSize = 16)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int padLen = blockSize - (data.Length % blockSize);
            // PKCS#7: if data.Length is already a multiple of blockSize, a whole new block of padding is added.
            byte[] padded = new byte[data.Length + padLen];
            Buffer.BlockCopy(data, 0, padded, 0, data.Length);
            for (int i = data.Length; i < padded.Length; i++)
                padded[i] = (byte)padLen;
            return padded;
        }

        /// <summary>Remove PKCS‑7 padding</summary>
        private static byte[] Unpad(byte[] data, int blockSize = 16)
        {
            if (data == null || data.Length == 0) return new byte[0];
            if (data.Length < 1) return data;

            byte padLength = data[data.Length - 1];

            if (padLength > 0 && padLength <= blockSize && padLength <= data.Length)
            {
                bool isValidPadding = true;
                for (int i = 1; i <= padLength; i++)
                {
                    if (data[data.Length - i] != padLength)
                    {
                        isValidPadding = false;
                        break;
                    }
                }

                if (isValidPadding)
                {
                    byte[] unpaddedData = new byte[data.Length - padLength];
                    Buffer.BlockCopy(data, 0, unpaddedData, 0, unpaddedData.Length);
                    return unpaddedData;
                }
            }
            return data;
        }

        public static List<byte> Encrypt(byte[] plainBytes, string password)
        {
            if (plainBytes == null) throw new ArgumentNullException(nameof(plainBytes));

            AES aes = new AES(password);

            byte[] paddedPlainBytes = Pad(plainBytes);

            byte[] cipherBytesResult = aes.Encrypt(paddedPlainBytes);

            return new List<byte>(cipherBytesResult);
        }

        public static List<byte> Decrypt(byte[] cipherBytes, string password)
        {
            if (cipherBytes == null) throw new ArgumentNullException(nameof(cipherBytes));

            if (cipherBytes.Length % 16 != 0)
            {
                throw new ArgumentException("Довжина шифротексту для розшифрування має бути кратною 16 байтам. Дані можуть бути пошкоджені.", nameof(cipherBytes));
            }
            if (cipherBytes.Length == 0)
            {
                return new List<byte>();
            }

            AES aes = new AES(password);

            byte[] decryptedPaddedBytes = aes.DeCrypt(cipherBytes);

            byte[] unpaddedBytes = Unpad(decryptedPaddedBytes);

            return new List<byte>(unpaddedBytes);
        }
    } // Кінець класу AesHelper

    public partial class StegPanel : Form
    {
        private readonly List<Point> contCords = new List<Point>();
        private readonly bool tpHide;
        private Process loadingProcess;
        private readonly byte[] stegKey;

        public StegPanel(bool hideUnhide, byte[] stegKey)
        {
            InitializeComponent();
            containerGridView.Columns[1].SortMode = DataGridViewColumnSortMode.NotSortable;

            this.stegKey = new byte[stegKey.Length];
            Array.Copy(stegKey, this.stegKey, stegKey.Length);


            dataGridView.Font = new Font("Sylfaen", 12);
            containerGridView.Font = new Font("Sylfaen", 12);

            if (!hideUnhide)
            {
                dataGridView.Enabled = false;
                SelectItemBtn.Enabled = false;
                btnRemove.Enabled = false;
            }

            tpHide = hideUnhide;
            // Наступний рядок може спричинити помилку під час виконання, якщо TrackBar не існує 
            // або не ініціалізований у InitializeComponent(). Якщо він не використовується, краще його видалити або закоментувати.
            // contCords.Add(new Point(20, 20 - new TrackBar().Height)); 
        }

        #region Container selection
        private void SelectContainerBtn_Click(object sender, EventArgs e)
        {
            ulong info = 0;
            DialogResult result = openFileDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                string imageResourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Image");
                string waitFormPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WaitForm.exe");

                try
                {
                    if (File.Exists(waitFormPath))
                        loadingProcess = Process.Start(waitFormPath);
                }
                catch { /* ignore */ }

                foreach (String file in openFileDialog1.FileNames)
                {
                    FileInfo fi = new FileInfo(file);
                    Bitmap bp;

                    String displayName = fi.Name;
                    int lastDot = displayName.LastIndexOf('.');
                    if (lastDot > 0 && lastDot < displayName.Length - 1)
                    {
                        string nameWithoutExt = displayName.Substring(0, lastDot);
                        string ext = displayName.Substring(lastDot);
                        if (nameWithoutExt.Length > (255 - ext.Length))
                        {
                            displayName = nameWithoutExt.Substring(0, (255 - ext.Length)) + ext;
                        }
                    }
                    else if (displayName.Length > 255)
                    {
                        displayName = displayName.Substring(0, 255);
                    }

                    bool isDuplicate = false;
                    for (int i = 0; i < containerGridView.Rows.Count; i++)
                    {
                        if (containerGridView.Rows[i].Cells[0].Value != null &&
                            displayName.Equals(containerGridView.Rows[i].Cells[0].Value.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            isDuplicate = true;
                            MessageBox.Show("Файл вже існує!");
                            break;
                        }
                    }

                    if (isDuplicate)
                        continue;

                    try
                    {
                        switch (fi.Extension.ToLowerInvariant())
                        {
                            case ".bmp":
                            case ".png":
                                using (bp = new Bitmap(fi.FullName))
                                {
                                    if (File.Exists(Path.Combine(imageResourcePath, "true.png")))
                                        pbPicture.Image = Image.FromFile(Path.Combine(imageResourcePath, "true.png"));
                                    else pbPicture.Image = null;

                                    info = 0;
                                    for (int i = 0; i < bp.Width * bp.Height; i++)
                                    {
                                        byte keyByte = stegKey[i % stegKey.Length];
                                        // Узгоджуємо з GetActualBitsFromKey, якщо така логіка є в BMP.cs,
                                        // або використовуємо пряме значення, якщо BMP.cs його так інтерпретує.
                                        // Поточна логіка StegPanel (з попередніх версій) використовувала пряме значення:
                                        // int bitsPerChannel = (keyByte == 0) ? 1 : Math.Min((int)keyByte, 8);
                                        // Залишаємо її, якщо BMP.cs очікує це.
                                        // Якщо BMP.cs має GetActualArrColor, то треба використовувати її логіку.
                                        // Для прикладу, припускаємо, що BMP.cs очікує значення з ключа як є (але не 0).
                                        int bitsPerChannel = (keyByte == 0) ? 1 : (int)keyByte;
                                        if (bitsPerChannel > 8) bitsPerChannel = 8; // Обмеження LSB
                                        info += (ulong)(3 * bitsPerChannel);
                                    }
                                    containerGridView.Rows.Add(displayName, info / 8, fi.FullName);
                                }
                                break;
                            case ".wav":
                                WAV wavInfo = new WAV(fi.FullName);
                                pbPicture.Image = Image.FromFile("Image/true.png");

                                info = 0;
                                for (UInt64 i = 0; i < wavInfo.AudioInfoCount / (ulong)wavInfo.BlockAlignBytes; i++)
                                    info += (ulong)stegKey[i % (ulong)stegKey.Length];

                                if (wavInfo.NumberOfChannels == 1)
                                    containerGridView.Rows.Add(fi.Name, info / 8, fi.FullName);
                                else
                                    containerGridView.Rows.Add(fi.Name, (info * 2) / 8, fi.FullName);
                                break;
                            case ".jpg":
                            case ".jpeg":
                                JPEG jpeg = new JPEG(fi.FullName, dataGridView.Enabled);
                                jpeg.GetInfo(stegKey);
                                if (File.Exists(Path.Combine(imageResourcePath, "true.png")))
                                    pbPicture.Image = Image.FromFile(Path.Combine(imageResourcePath, "true.png"));
                                else pbPicture.Image = null;
                                containerGridView.Rows.Add(displayName, jpeg.info, fi.FullName);
                                break;
                            default:
                                if (File.Exists(Path.Combine(imageResourcePath, "false.png")))
                                    pbPicture.Image = Image.FromFile(Path.Combine(imageResourcePath, "false.png"));
                                else pbPicture.Image = null;
                                MessageBox.Show($"Формат файлу {fi.Extension} не підтримується.", "Непідтримуваний формат", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Помилка при обробці файлу {displayName}: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        try
                        {
                            if (File.Exists(Path.Combine(imageResourcePath, "false.png")))
                                pbPicture.Image = Image.FromFile(Path.Combine(imageResourcePath, "false.png"));
                            else pbPicture.Image = null;
                        }
                        catch { }
                    }
                }

                try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }

                containerGridView.ClearSelection();
                if (containerGridView.Rows.Count > 0)
                {
                    containerGridView.CurrentCell = null;
                }
            }
            labelContainer.Text = "Може приховати: " + GetPoints(GetSize(containerGridView)) + " байт";
        }
        #endregion

        #region Data selection
        private void SelectDataBtn_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                foreach (String file in openFileDialog1.FileNames)
                {
                    FileInfo fi = new FileInfo(file);
                    byte[] bytes;
                    try
                    {
                        bytes = File.ReadAllBytes(file);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не вдалося прочитати файл {fi.Name}: {ex.Message}", "Помилка читання файлу", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        continue;
                    }

                    String nameOnly = fi.Name;
                    int lastDot = fi.Name.LastIndexOf('.');
                    if (lastDot > 0 && lastDot < fi.Name.Length - 1)
                    {
                        nameOnly = fi.Name.Substring(0, lastDot);
                    }

                    string finalNameForDisplay = fi.Name;
                    if (lastDot > 0 && lastDot < finalNameForDisplay.Length - 1)
                    {
                        string nameWithoutExtDisplay = finalNameForDisplay.Substring(0, lastDot);
                        string extDisplay = finalNameForDisplay.Substring(lastDot);
                        if (nameWithoutExtDisplay.Length > (255 - extDisplay.Length))
                        {
                            finalNameForDisplay = nameWithoutExtDisplay.Substring(0, (255 - extDisplay.Length)) + extDisplay;
                        }
                    }
                    else if (finalNameForDisplay.Length > 255)
                    {
                        finalNameForDisplay = finalNameForDisplay.Substring(0, 255);
                    }

                    bool isDuplicate = false;
                    for (int i = 0; i < dataGridView.Rows.Count; i++)
                    {
                        if (dataGridView.Rows[i].Cells[0].Value != null &&
                            finalNameForDisplay.Equals(dataGridView.Rows[i].Cells[0].Value.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            isDuplicate = true;
                            MessageBox.Show("Файл вже існує!");
                            break;
                        }
                    }

                    if (isDuplicate)
                        continue;

                    string currentFileExt = fi.Extension.Length > 0 ? fi.Extension.Substring(1) : "";
                    int namePartLength = Math.Min(nameOnly.Length, 255);
                    int extPartLength = currentFileExt.Length;

                    int singleFileMetadataSize = 1 + namePartLength * 2 + 1 + extPartLength + 5;
                    int totalEntrySize = singleFileMetadataSize + bytes.Length;

                    // Логіка для overhead (10 байт для першого файлу)
                    // GetSize(dataGridView) тепер рахує суму "чистих" розмірів (файл + його індивідуальні метадані)
                    // Загальний заголовок (10 байт) буде додано в HideData та враховано в btnStart_Click
                    dataGridView.Rows.Add(finalNameForDisplay, totalEntrySize, fi.FullName);
                }
                dataGridView.ClearSelection();
                if (dataGridView.Rows.Count > 0)
                {
                    dataGridView.CurrentCell = null;
                }
            }

            ulong currentTotalDataSize = GetSize(dataGridView);
            // Додаємо 10 байт загального заголовка, тільки якщо є файли для приховування
            if (dataGridView.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow) > 0)
            {
                currentTotalDataSize += 10;
            }
            labelHide.Text = "Розмір файлу: " + GetPoints(currentTotalDataSize) + " байт";
        }
        #endregion

        #region Helpers
        private static ulong GetSize(DataGridView dgv)
        {
            ulong byteCount = 0;
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (!dgv.Rows[i].IsNewRow && dgv.Rows[i].Cells[1].Value != null)
                    byteCount += Convert.ToUInt64(dgv.Rows[i].Cells[1].Value.ToString());
            }
            return byteCount;
        }
        private static string GetPoints(ulong num)
        {
            string str = num.ToString();
            if (str.Length <= 3) return str;

            StringBuilder endStr = new StringBuilder();
            int firstPartLength = str.Length % 3;
            if (firstPartLength == 0) firstPartLength = 3;

            endStr.Append(str.Substring(0, firstPartLength));

            for (int i = firstPartLength; i < str.Length; i += 3)
            {
                endStr.Append("." + str.Substring(i, 3));
            }
            return endStr.ToString();
        }
        #endregion

        #region Start (encode / decode)
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (containerGridView.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow) == 0)
            {
                MessageBox.Show("Будь ласка, додайте контейнер!");
                return;
            }

            if (tpHide && dataGridView.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow) == 0)
            {
                MessageBox.Show("Будь ласка, додайте інформацію для приховування!");
                return;
            }

            if (checkBox.Checked && string.IsNullOrWhiteSpace(encryptTextBox.Text))
            {
                MessageBox.Show("Будь ласка, введіть пароль для шифрування!");
                return;
            }

            ulong sizeOfDataPayloadOnly = GetSize(dataGridView);
            ulong requiredTotalSizeToHide = sizeOfDataPayloadOnly;
            if (tpHide && dataGridView.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow) > 0)
            {
                requiredTotalSizeToHide += 10;
            }

            if (tpHide && GetSize(containerGridView) < requiredTotalSizeToHide)
            {
                MessageBox.Show($"Розмір даних для приховування ({GetPoints(requiredTotalSizeToHide)} байт) перевищує загальну місткість контейнерів ({GetPoints(GetSize(containerGridView))} байт)!", "Недостатньо місця", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = folderBrowserDialog1.ShowDialog();
            String savePath = "";
            if (result != DialogResult.OK)
            {
                return;
            }

            savePath = folderBrowserDialog1.SelectedPath + Path.DirectorySeparatorChar;

            string waitFormPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WaitForm.exe");
            try
            {
                if (File.Exists(waitFormPath))
                    loadingProcess = Process.Start(waitFormPath);
            }
            catch { /* ignore */ }

            if (tpHide)
                HideData(savePath);
            else
                ExtractData(savePath);
        }

        private void HideData(string savePath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            StringBuilder psnrResults = new StringBuilder();

            List<byte> allFilesCombinedPayload = new List<byte>();
            int actualFileCount = dataGridView.Rows.Cast<DataGridViewRow>().Count(r => !r.IsNewRow);

            if (actualFileCount == 0)
            {
                stopwatch.Stop();
                try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                MessageBox.Show("Немає файлів для приховування.", "Операція скасована", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            for (int i = 0; i < dataGridView.Rows.Count; i++)
            {
                if (dataGridView.Rows[i].IsNewRow) continue;

                string path = dataGridView.Rows[i].Cells[2].Value.ToString();
                FileInfo fileInfo = new FileInfo(path);

                string fileName = fileInfo.Name;
                int lastDotIdx = fileName.LastIndexOf('.');
                string namePart = (lastDotIdx > 0 && lastDotIdx < fileName.Length - 1) ? fileName.Substring(0, lastDotIdx) : fileName;
                string extPart = (lastDotIdx > 0 && lastDotIdx < fileName.Length - 1) ? fileName.Substring(lastDotIdx + 1) : "";

                if (namePart.Length > 255) namePart = namePart.Substring(0, 255);

                allFilesCombinedPayload.Add((byte)namePart.Length);
                allFilesCombinedPayload.AddRange(Encoding.Unicode.GetBytes(namePart));
                allFilesCombinedPayload.Add((byte)extPart.Length);
                allFilesCombinedPayload.AddRange(Encoding.ASCII.GetBytes(extPart));

                byte[] body = File.ReadAllBytes(path);
                ulong bodyLen = (ulong)body.Length;
                for (int k = 4; k >= 0; k--)
                {
                    allFilesCombinedPayload.Add((byte)(bodyLen >> (k * 8)));
                }
                allFilesCombinedPayload.AddRange(body);
            }

            List<byte> dataToEmbedInContainers = new List<byte>();
            ulong totalPayloadSize = (ulong)allFilesCombinedPayload.Count;
            for (int i = 4; i >= 0; i--) dataToEmbedInContainers.Add((byte)(totalPayloadSize >> (i * 8)));

            ulong fileCountHeader = (ulong)actualFileCount;
            for (int i = 4; i >= 0; i--) dataToEmbedInContainers.Add((byte)(fileCountHeader >> (i * 8)));

            dataToEmbedInContainers.AddRange(allFilesCombinedPayload);

            if (checkBox.Checked)
            {
                try
                {
                    dataToEmbedInContainers = AesHelper.Encrypt(dataToEmbedInContainers.ToArray(), encryptTextBox.Text);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                    MessageBox.Show($"Помилка шифрування: {ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            List<byte> remainingDataToEmbed = new List<byte>(dataToEmbedInContainers);
            for (int i = 0; i < containerGridView.Rows.Count && remainingDataToEmbed.Count > 0; i++)
            {
                if (containerGridView.Rows[i].IsNewRow) continue;

                string containerPath = containerGridView.Rows[i].Cells[2].Value.ToString();
                ulong containerCapacity = Convert.ToUInt64(containerGridView.Rows[i].Cells[1].Value);

                int portionSize = (int)Math.Min(containerCapacity, (ulong)remainingDataToEmbed.Count);
                if (portionSize == 0 && remainingDataToEmbed.Count > 0) continue;

                List<byte> slice = remainingDataToEmbed.GetRange(0, portionSize);
                remainingDataToEmbed.RemoveRange(0, portionSize);

                FileInfo containerFi = new FileInfo(containerPath);
                string outputFilePath = Path.Combine(savePath, containerFi.Name);

                try
                {
                    switch (containerFi.Extension.ToLowerInvariant())
                    {
                        case ".png":
                        case ".bmp":
                            using (Bitmap originalImage = new Bitmap(containerPath))
                            using (Bitmap imageToEncode = new Bitmap(originalImage))
                            {
                                BMP.bmpEncode(slice, outputFilePath, imageToEncode, stegKey);
                                if (File.Exists(outputFilePath))
                                {
                                    using (Bitmap stegoImage = new Bitmap(outputFilePath))
                                    {
                                        double psnr = PSNRCalculator.CalculatePSNR(originalImage, stegoImage);
                                        psnrResults.AppendLine($"PSNR для {containerFi.Name}: {psnr:F2} dB");
                                    }
                                }
                                else { psnrResults.AppendLine($"Не вдалося створити стего-файл для {containerFi.Name}."); }
                            }
                            break;
                        case ".wav":
                            new WAV(containerPath).WavEncode(slice, outputFilePath, stegKey);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            using (Bitmap originalImage = new Bitmap(containerPath))
                            {
                                JPEG jpegProcessor = new JPEG(containerPath, true);
                                jpegProcessor.jpegEncode(slice.ToArray(), outputFilePath, stegKey);
                                if (File.Exists(outputFilePath))
                                {
                                    using (Bitmap stegoImage = new Bitmap(outputFilePath))
                                    {
                                        double psnr = PSNRCalculator.CalculatePSNR(originalImage, stegoImage);
                                        psnrResults.AppendLine($"PSNR для {containerFi.Name}: {psnr:F2} dB");
                                    }
                                }
                                else { psnrResults.AppendLine($"Не вдалося створити стего-файл для {containerFi.Name}."); }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    psnrResults.AppendLine($"Помилка обробки контейнера {containerFi.Name}: {ex.Message}");
                }
            }

            stopwatch.Stop();
            try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }

            string finalMessage = "Інформація успішно прихована!";
            finalMessage += $"{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)";
            if (psnrResults.Length > 0)
            {
                finalMessage += Environment.NewLine + Environment.NewLine + "Результати PSNR:" + Environment.NewLine + psnrResults.ToString();
            }
            MessageBox.Show(finalMessage, "Операція завершена", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExtractData(string savePath)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            List<byte> collectedEncryptedOrPlainData = new List<byte>();

            foreach (DataGridViewRow row in containerGridView.Rows)
            {
                if (row.IsNewRow || row.Cells[2].Value == null || row.Cells[1].Value == null) continue;
                string path = row.Cells[2].Value.ToString();
                ulong containerCapacityBytes = Convert.ToUInt64(row.Cells[1].Value);
                if (containerCapacityBytes == 0) continue;

                FileInfo fi = new FileInfo(path);
                List<byte> dataFromCurrentContainer = null;
                try
                {
                    switch (fi.Extension.ToLowerInvariant())
                    {
                        case ".png":
                        case ".bmp":
                            using (Bitmap bmp = new Bitmap(path))
                            {
                                dataFromCurrentContainer = BMP.bmpDecode(bmp, (int)containerCapacityBytes, stegKey);
                            }
                            break;
                        case ".wav":
                            dataFromCurrentContainer = new WAV(path).WavDecode((int)containerCapacityBytes, stegKey);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            JPEG j = new JPEG(path, false);
                            j.jpegDecode(stegKey);
                            dataFromCurrentContainer = j.secretFiles;
                            break;
                    }
                    if (dataFromCurrentContainer != null)
                    {
                        collectedEncryptedOrPlainData.AddRange(dataFromCurrentContainer);
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                    MessageBox.Show($"Помилка вилучення даних з {fi.Name}: {ex.Message}{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)", "Помилка вилучення", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (collectedEncryptedOrPlainData.Count == 0)
            {
                stopwatch.Stop();
                try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                MessageBox.Show($"Не вдалося витягнути дані з контейнерів.{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            List<byte> finalPayloadToProcess;
            if (checkBox.Checked)
            {
                if (collectedEncryptedOrPlainData.Count % 16 != 0)
                {
                    int originalLength = collectedEncryptedOrPlainData.Count;
                    int newLength = (originalLength / 16) * 16;
                    if (newLength < originalLength)
                    {
                        MessageBox.Show($"Попередження: Довжина зібраних зашифрованих даних ({originalLength}) не кратна 16. " +
                                        $"Спроба обрізати до {newLength} байт перед розшифруванням. ",
                                        "Корекція довжини шифротексту", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (newLength > 0)
                        {
                            collectedEncryptedOrPlainData.RemoveRange(newLength, originalLength - newLength);
                        }
                    }
                }
                try
                {
                    finalPayloadToProcess = AesHelper.Decrypt(collectedEncryptedOrPlainData.ToArray(), encryptTextBox.Text);
                }
                catch (ArgumentException argEx) when (argEx.Message.Contains("кратн"))
                {
                    stopwatch.Stop();
                    try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                    MessageBox.Show($"Помилка розшифрування: {argEx.Message}{Environment.NewLine}" +
                                    $"Довжина даних, переданих на розшифрування: {collectedEncryptedOrPlainData.Count} байт.{Environment.NewLine}" +
                                    $"Перевірте пароль або цілісність даних.{Environment.NewLine}" +
                                    $"Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)",
                                    "Помилка розшифрування", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                    MessageBox.Show($"Помилка розшифрування: {ex.Message}{Environment.NewLine}Перевірте пароль або цілісність даних.{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)", "Помилка розшифрування", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                finalPayloadToProcess = collectedEncryptedOrPlainData;
            }

            if (finalPayloadToProcess.Count < 10)
            {
                stopwatch.Stop();
                try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }
                MessageBox.Show($"Витягнуто недостатньо даних для читання заголовка ({finalPayloadToProcess.Count} байт).{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)", "Помилка даних", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ulong totalPayloadSizeFromHeader = 0;
            for (int i = 0; i < 5; i++)
                totalPayloadSizeFromHeader = (totalPayloadSizeFromHeader << 8) | finalPayloadToProcess[i];

            ulong fileCountFromHeader = 0;
            for (int i = 5; i < 10; i++)
                fileCountFromHeader = (fileCountFromHeader << 8) | finalPayloadToProcess[i];

            if (finalPayloadToProcess.Count < 10 + (int)totalPayloadSizeFromHeader)
            {
                MessageBox.Show($"Попередження: Очікуваний розмір корисного навантаження ({totalPayloadSizeFromHeader} байт) " +
                                $"більший, ніж доступно після заголовка ({finalPayloadToProcess.Count - 10} байт). " +
                                $"Можливо, дані пошкоджені або не повністю витягнуті.",
                                "Попередження даних", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (totalPayloadSizeFromHeader > (ulong)(finalPayloadToProcess.Count - 10))
                {
                    totalPayloadSizeFromHeader = (ulong)(finalPayloadToProcess.Count - 10);
                }
            }

            List<byte> actualFilesData = new List<byte>();
            if (finalPayloadToProcess.Count >= 10)
            {
                int startIndex = 10;
                int lengthToTake = (int)Math.Min(totalPayloadSizeFromHeader, (ulong)(finalPayloadToProcess.Count - startIndex));
                if (lengthToTake < 0) lengthToTake = 0;
                if (lengthToTake > 0 && startIndex + lengthToTake <= finalPayloadToProcess.Count)
                {
                    actualFilesData = finalPayloadToProcess.GetRange(startIndex, lengthToTake);
                }
                else if (lengthToTake > 0)
                {
                    actualFilesData.Clear();
                    MessageBox.Show($"Помилка логіки: Неможливо вилучити {lengthToTake} байт з позиції {startIndex} (загальна довжина {finalPayloadToProcess.Count})", "Критична помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            int filesExtractedSuccessfully = 0;
            int currentPositionInActualData = 0;

            for (int f = 0; f < (int)fileCountFromHeader; f++)
            {
                if (currentPositionInActualData >= actualFilesData.Count) break;

                try
                {
                    if (currentPositionInActualData + 1 > actualFilesData.Count) break;
                    int nameLen = actualFilesData[currentPositionInActualData++];

                    if (currentPositionInActualData + nameLen * 2 > actualFilesData.Count) break;
                    StringBuilder sbName = new StringBuilder();
                    for (int k = 0; k < nameLen; k++)
                    {
                        byte lo = actualFilesData[currentPositionInActualData++];
                        byte hi = actualFilesData[currentPositionInActualData++];
                        sbName.Append(Encoding.Unicode.GetString(new[] { lo, hi }));
                    }
                    string name = sbName.ToString();

                    if (currentPositionInActualData + 1 > actualFilesData.Count) break;
                    int extLen = actualFilesData[currentPositionInActualData++];

                    if (currentPositionInActualData + extLen > actualFilesData.Count) break;
                    StringBuilder sbExt = new StringBuilder();
                    for (int k = 0; k < extLen; k++) sbExt.Append((char)actualFilesData[currentPositionInActualData++]);
                    string ext = extLen > 0 ? "." + sbExt.ToString() : "";

                    if (currentPositionInActualData + 5 > actualFilesData.Count) break;
                    ulong bodyLen = 0;
                    for (int k = 0; k < 5; k++) bodyLen = (bodyLen << 8) | actualFilesData[currentPositionInActualData++];

                    if (currentPositionInActualData + (int)bodyLen > actualFilesData.Count)
                    {
                        MessageBox.Show($"Недостатньо даних для тіла файлу '{name + ext}'. Очікується {bodyLen}, доступно {actualFilesData.Count - currentPositionInActualData}.", "Помилка даних", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
                    byte[] body = actualFilesData.GetRange(currentPositionInActualData, (int)bodyLen).ToArray();
                    currentPositionInActualData += (int)bodyLen;

                    File.WriteAllBytes(Path.Combine(savePath, name + ext), body);
                    filesExtractedSuccessfully++;
                }
                catch (ArgumentOutOfRangeException argOutOfRangeEx)
                {
                    MessageBox.Show($"Помилка обробки даних файлу #{f + 1} (вихід за межі масиву): {argOutOfRangeEx.Message}", "Помилка витягнення файлу", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка при витягненні файлу #{f + 1}: {ex.Message}", "Помилка витягнення файлу", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                }
            }

            stopwatch.Stop();
            try { loadingProcess?.Kill(); loadingProcess?.Dispose(); } catch { }

            string resultMessage = filesExtractedSuccessfully > 0 ?
                                   $"Інформація успішно витягнута ({filesExtractedSuccessfully} з {fileCountFromHeader} файл(ів))." :
                                   $"Не вдалося витягнути файли (очікувалось {fileCountFromHeader}).";
            if (fileCountFromHeader == 0 && filesExtractedSuccessfully == 0 && finalPayloadToProcess.Count >= 10) resultMessage = "Заголовок вказує на 0 прихованих файлів.";
            else if (finalPayloadToProcess.Count < 10 && filesExtractedSuccessfully == 0) resultMessage = "Не вдалося прочитати заголовок прихованих даних.";

            resultMessage += $"{Environment.NewLine}Час виконання: {stopwatch.Elapsed.TotalSeconds:F2} с ({stopwatch.Elapsed.TotalMilliseconds:F0} мс)";
            MessageBox.Show(resultMessage, "Операція завершена", MessageBoxButtons.OK, filesExtractedSuccessfully > 0 || (fileCountFromHeader == 0 && finalPayloadToProcess.Count >= 10) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        #endregion

        #region UI misc
        private void BtnContainerRmove_Click(object sender, EventArgs e)
        {
            for (int i = containerGridView.SelectedRows.Count - 1; i >= 0; i--)
            {
                DataGridViewRow r = containerGridView.SelectedRows[i];
                if (!r.IsNewRow) containerGridView.Rows.RemoveAt(r.Index);
            }
            labelContainer.Text = "Може приховати: " + GetPoints(GetSize(containerGridView)) + " байт";
        }

        private void BtnDataRemove_Click(object sender, EventArgs e)
        {
            for (int i = dataGridView.SelectedRows.Count - 1; i >= 0; i--)
            {
                DataGridViewRow r = dataGridView.SelectedRows[i];
                if (!r.IsNewRow) dataGridView.Rows.RemoveAt(r.Index);
            }
            labelHide.Text = "Розмір файлу: " + GetPoints(GetSize(dataGridView)) + " байт";
        }

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            encryptTextBox.Enabled = checkBox.Checked;
        }

        private void StegPanel_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
            if (mainForm != null && !mainForm.IsDisposed)
            {

                mainForm.Activate();
                if (!mainForm.Visible) mainForm.Show();
            }
        }
        #endregion
    } // Кінець класу StegPanel
} // Кінець namespace Stegonagraph
