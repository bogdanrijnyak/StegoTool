// StegPanel.cs – C# 7.3‑compatible version (no Span/Range)
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
    ///   Lightweight AES helper that wraps the <see cref="AES"/> class shipped with the project
    ///   and exposes Encrypt / Decrypt that operate on whole byte arrays. 100 % compatible with
    ///   C# 7.3 (no Index/Range/Span).
    /// </summary>
    internal static class AesHelper
    {
        private static readonly int[] _legalKeySizes = { 16, 24, 32 };

        /// <summary> Normalises a UTF‑8 password into 16/24/32‑byte key. </summary>
        private static byte[] DeriveKey(string password)
        {
            byte[] raw = Encoding.UTF8.GetBytes(password ?? string.Empty);
            int target = _legalKeySizes.First(size => raw.Length <= size);
            byte[] key = new byte[target];
            // Repeat the password bytes to fill the key
            for (int i = 0; i < target; i++)
                key[i] = raw[i % raw.Length];
            return key;
        }

        /// <summary>PKCS‑7 padding (adds 1–16 bytes)</summary>
        private static byte[] Pad(byte[] data)
        {
            int padLen = 16 - (data.Length % 16);
            if (padLen == 0) padLen = 16;
            byte[] padded = new byte[data.Length + padLen];
            Buffer.BlockCopy(data, 0, padded, 0, data.Length);
            for (int i = data.Length; i < padded.Length; i++)
                padded[i] = (byte)padLen;
            return padded;
        }

        /// <summary>Remove PKCS‑7 padding (tolerant to garbage)</summary>
        private static byte[] Unpad(List<byte> bytes)
        {
            if (bytes.Count == 0) return bytes.ToArray();
            byte pad = bytes[bytes.Count - 1];
            if (pad <= 16 && pad > 0 && pad <= bytes.Count)
            {
                bool ok = true;
                for (int i = 1; i <= pad; i++) if (bytes[bytes.Count - i] != pad) { ok = false; break; }
                if (ok) bytes.RemoveRange(bytes.Count - pad, pad);
            }
            return bytes.ToArray();
        }

        public static List<byte> Encrypt(byte[] plainBytes, string password)
        {
            byte[] key = DeriveKey(password);
            AES aes = new AES(key);
            byte[] padded = Pad(plainBytes);
            List<byte> cipher = new List<byte>(padded.Length);
            byte[] block = new byte[16];
            for (int offset = 0; offset < padded.Length; offset += 16)
            {
                Array.Copy(padded, offset, block, 0, 16);
                cipher.AddRange(aes.Cipher(block, key));
            }
            return cipher;
        }

        public static List<byte> Decrypt(byte[] cipherBytes, string password)
        {
            byte[] key = DeriveKey(password);
            AES aes = new AES(key);
            List<byte> plain = new List<byte>(cipherBytes.Length);
            byte[] block = new byte[16];
            for (int offset = 0; offset < cipherBytes.Length; offset += 16)
            {
                Array.Copy(cipherBytes, offset, block, 0, 16);
                plain.AddRange(aes.InvCipher(block, key));
            }
            return new List<byte>(Unpad(plain));
        }
    }

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

            // Copy key locally (immutability)
            this.stegKey = new byte[stegKey.Length];

            for (int i = 0; i < stegKey.Length; i++)
                this.stegKey[i] = stegKey[i];


            dataGridView.Font = new Font("Sylfaen", 12);
            containerGridView.Font = new Font("Sylfaen", 12);

            if (!hideUnhide)
            {
                dataGridView.Enabled = false;
                SelectItemBtn.Enabled = false;
                btnRemove.Enabled = false;
            }

            tpHide = hideUnhide;
            contCords.Add(new Point(20, 20 - new TrackBar().Height));
        }

        #region Container selection
        private void SelectContainerBtn_Click(object sender, EventArgs e)
        {
            ulong info = 0;
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            try { loadingProcess = Process.Start(Path.Combine(Environment.CurrentDirectory, "Resources", "WaitForm.exe")); }
            catch { /* ignore */ }

            foreach (string file in openFileDialog1.FileNames)
            {
                FileInfo fi = new FileInfo(file);
                string displayName = fi.Name;
                displayName = displayName.Substring(0, displayName.LastIndexOf('.'));
                displayName = displayName.Length > 255 ? displayName.Substring(0, 255) + fi.Extension : displayName + fi.Extension;

                bool duplicate = false;
                for (int i = 0; i < containerGridView.Rows.Count; i++)
                {
                    if (displayName.Equals(containerGridView.Rows[i].Cells[0].Value?.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        MessageBox.Show("File already exists in the list.");
                        break;
                    }
                }
                if (duplicate) continue;

                switch (fi.Extension.ToLowerInvariant())
                {
                    case ".bmp":
                    case ".png":
                        var bmp = new Bitmap(fi.FullName);
                        pbPicture.Image = Image.FromFile(Path.Combine("Image", "true.png"));
                        info = 0;
                        for (int i = 0; i < bmp.Width * bmp.Height; i++)
                            info += (ulong)(3 * stegKey[i % stegKey.Length]);
                        containerGridView.Rows.Add(fi.Name, info / 8, fi.FullName);
                        break;

                    case ".wav":
                        var wav = new WAV(fi.FullName);
                        pbPicture.Image = Image.FromFile(Path.Combine("Image", "true.png"));
                        info = 0;
                        ulong samples = wav.AudioDataLength / (ulong)wav.BlockAlignBytes;
                        for (ulong i = 0; i < samples; i++)
                            info += (ulong)stegKey[i % (ulong)stegKey.Length];
                        ulong capacity = wav.NumberOfChannels == 1 ? info / 8 : (info * 2) / 8;
                        containerGridView.Rows.Add(fi.Name, capacity, fi.FullName);
                        break;

                    case ".jpg":
                    case ".jpeg":
                        var jpeg = new JPEG(fi.FullName, dataGridView.Enabled);
                        jpeg.GetInfo(stegKey);
                        pbPicture.Image = Image.FromFile(Path.Combine("Image", "true.png"));
                        containerGridView.Rows.Add(fi.Name, jpeg.info, fi.FullName);
                        break;

                    default:
                        pbPicture.Image = Image.FromFile(Path.Combine("Image", "false.png"));
                        break;
                }
            }

            try { loadingProcess?.Kill(); } catch { }

            containerGridView.ClearSelection();
            containerGridView.CurrentCell = null;
            labelContainer.Text = "Can Hide: " + GetPoints(GetSize(containerGridView)) + " byte";
        }
        #endregion

        #region Data selection
        private void SelectDataBtn_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;

            foreach (string file in openFileDialog1.FileNames)
            {
                FileInfo fi = new FileInfo(file);
                byte[] bytes = File.ReadAllBytes(file);
                string nameOnly = fi.Name.Substring(0, fi.Name.LastIndexOf('.'));
                string finalName = nameOnly.Length > 255 ? nameOnly.Substring(0, 255) + fi.Extension : fi.Name;

                bool exists = false;
                for (int i = 0; i < dataGridView.Rows.Count; i++)
                {
                    if (finalName.Equals(dataGridView.Rows[i].Cells[0].Value?.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        MessageBox.Show("File already exists in the list.");
                        break;
                    }
                }
                if (exists) continue;

                int overhead = dataGridView.Rows.Count == 0 ? 10 : 0; // marker for first file
                int lengthField = nameOnly.Length > 255 ? 510 : nameOnly.Length * 2;
                int entrySize = overhead + 1 + lengthField + 1 + fi.Extension.Substring(1).Length + 5 + bytes.Length;
                dataGridView.Rows.Add(fi.Name, entrySize, fi.FullName);
            }

            dataGridView.ClearSelection();
            dataGridView.CurrentCell = null;
            labelHide.Text = "File Size: " + GetPoints(GetSize(dataGridView)) + " byte";
        }
        #endregion

        #region Helpers
        private static ulong GetSize(DataGridView dgv)
        {
            ulong total = 0;
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (!row.IsNewRow)
                    total += Convert.ToUInt64(row.Cells[1].Value.ToString());
            }
            return total;
        }

        private static string GetPoints(ulong num)
        {
            string s = num.ToString();
            StringBuilder sb = new StringBuilder();
            while (s.Length > 3)
            {
                string part = s.Substring(s.Length - 3, 3);
                sb.Insert(0, "." + part);
                s = s.Substring(0, s.Length - 3);
            }
            sb.Insert(0, s);
            return sb.ToString();
        }
        #endregion

        #region Start (encode / decode)
        private void btnStart_Click(object sender, EventArgs e)
        {
            if (containerGridView.Rows.Count == 0)
            {
                MessageBox.Show("Будь ласка, додайте контейнер!");
                return;
            }

            if (dataGridView.Enabled && dataGridView.Rows.Count == 0)
            {
                MessageBox.Show("Будь ласка, додайте інформацію для приховування!");
                return;
            }

            if (checkBox.Checked && string.IsNullOrWhiteSpace(encryptTextBox.Text))
            {
                MessageBox.Show("Будь ласка, введіть пароль для шифрування!");
                return;
            }

            if (GetSize(containerGridView) < GetSize(dataGridView))
            {
                MessageBox.Show("Розмір прихованого файлу перевищує місткість контейнера!");
                return;
            }

            if (folderBrowserDialog1.ShowDialog() != DialogResult.OK)
                return;

            string savePath = folderBrowserDialog1.SelectedPath + Path.DirectorySeparatorChar;
            try { loadingProcess = Process.Start(Path.Combine(Environment.CurrentDirectory, "Resources", "WaitForm.exe")); } catch { }

            if (tpHide)
                HideData(savePath);
            else
                ExtractData(savePath);
        }
        private void HideData(string savePath)
        {
            List<byte> hideInfo = new List<byte>();
            ulong totalSecretBytes = GetSize(dataGridView);
            for (int i = 4; i >= 0; i--) hideInfo.Add((byte)(totalSecretBytes >> (i * 8)));

            // Подсчитываем только непустые строки для количества файлов
            int actualFileCount = 0;
            for (int i = 0; i < dataGridView.Rows.Count; i++)
            {
                if (!dataGridView.Rows[i].IsNewRow) actualFileCount++;
            }
            ulong fileCount = (ulong)actualFileCount;

            for (int i = 4; i >= 0; i--) hideInfo.Add((byte)(fileCount >> (i * 8)));

            string[] secretPaths = new string[actualFileCount];
            for (int i = 0; i < actualFileCount; i++)
                secretPaths[i] = dataGridView.Rows[i].Cells[2].Value.ToString();

            foreach (string path in secretPaths)
            {
                FileInfo fi = new FileInfo(path);
                string namePart = fi.Name.Substring(0, fi.Name.LastIndexOf('.')); // Исправлено: LastIndexOf вместо IndexOf
                if (namePart.Length > 255) namePart = namePart.Substring(0, 255);
                string extPart = fi.Extension.Substring(1);

                hideInfo.Add((byte)namePart.Length);
                foreach (char ch in namePart)
                {
                    byte[] u = Encoding.Unicode.GetBytes(ch.ToString());
                    hideInfo.Add(u[0]); hideInfo.Add(u[1]);
                }
                hideInfo.Add((byte)extPart.Length);
                foreach (char ch in extPart) hideInfo.Add((byte)ch);

                byte[] body = File.ReadAllBytes(path);
                ulong bodyLen = (ulong)body.Length;
                for (int i = 4; i >= 0; i--) hideInfo.Add((byte)(bodyLen >> (i * 8)));
                hideInfo.AddRange(body);
            }

            if (checkBox.Checked)
                hideInfo = AesHelper.Encrypt(hideInfo.ToArray(), encryptTextBox.Text);

            // ----- write to containers -----
            // Собираем только непустые строки для путей контейнеров
            List<string> validContainerPathsList = new List<string>();
            List<int> validContainerCapacitiesList = new List<int>();

            for (int i = 0; i < containerGridView.Rows.Count; i++)
            {
                if (!containerGridView.Rows[i].IsNewRow && containerGridView.Rows[i].Cells[2].Value != null && containerGridView.Rows[i].Cells[1].Value != null)
                {
                    validContainerPathsList.Add(containerGridView.Rows[i].Cells[2].Value.ToString());
                    validContainerCapacitiesList.Add(Convert.ToInt32(containerGridView.Rows[i].Cells[1].Value));
                }
            }
            string[] containerPaths = validContainerPathsList.ToArray();
            int[] containerCapacities = validContainerCapacitiesList.ToArray();

            StringBuilder psnrResults = new StringBuilder();

            for (int i = 0; i < containerPaths.Length && hideInfo.Count > 0; i++)
            {
                int capacity = containerCapacities[i];
                int portion = Math.Min(capacity, hideInfo.Count);
                List<byte> slice = hideInfo.GetRange(0, portion);
                hideInfo.RemoveRange(0, portion);

                FileInfo fi = new FileInfo(containerPaths[i]);
                string originalFilePath = containerPaths[i];
                string outputFilePath = Path.Combine(savePath, fi.Name);

                try
                {
                    switch (fi.Extension.ToLowerInvariant())
                    {
                        case ".png":
                        case ".bmp":
                            {
                                using (Bitmap originalImage = new Bitmap(originalFilePath))
                                {
                                    // Создаем копию для кодирования, чтобы не изменять originalImage
                                    using (Bitmap imageToEncode = new Bitmap(originalImage))
                                    {
                                        BMP.bmpEncode(slice, outputFilePath, imageToEncode, stegKey);
                                    }

                                    if (File.Exists(outputFilePath))
                                    {
                                        using (Bitmap stegoImage = new Bitmap(outputFilePath))
                                        {
                                            double psnr = PSNRCalculator.CalculatePSNR(originalImage, stegoImage);
                                            psnrResults.AppendLine($"PSNR для {fi.Name}: {psnr:F2} dB");
                                        }
                                    }
                                    else
                                    {
                                        psnrResults.AppendLine($"Не вдалося створити файл для {fi.Name} для розрахунку PSNR.");
                                    }
                                }
                                break;
                            }
                        case ".wav":
                            new WAV(originalFilePath).WavEncode(slice, outputFilePath, stegKey);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            {
                                using (Bitmap originalImage = new Bitmap(originalFilePath))
                                {
                                    // JPEG класс, вероятно, сам обрабатывает копирование или создает новый файл
                                    new JPEG(originalFilePath, true).jpegEncode(slice.ToArray(), outputFilePath, stegKey);

                                    if (File.Exists(outputFilePath))
                                    {
                                        using (Bitmap stegoImage = new Bitmap(outputFilePath))
                                        {
                                            double psnr = PSNRCalculator.CalculatePSNR(originalImage, stegoImage);
                                            psnrResults.AppendLine($"PSNR для {fi.Name}: {psnr:F2} dB");
                                        }
                                    }
                                    else
                                    {
                                        psnrResults.AppendLine($"Не вдалося створити файл для {fi.Name} для розрахунку PSNR.");
                                    }
                                }
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    psnrResults.AppendLine($"Помилка розрахунку PSNR для {fi.Name}: {ex.Message}");
                }
            }

            try { loadingProcess?.Kill(); } catch { }

            string finalMessage = "Інформація успішно прихована!";
            if (psnrResults.Length > 0)
            {
                finalMessage += Environment.NewLine + Environment.NewLine + "Результати PSNR:" + Environment.NewLine + psnrResults.ToString();
            }
            MessageBox.Show(finalMessage);
        }

        private void ExtractData(string savePath)
        {
            List<byte> collected = new List<byte>();
            foreach (DataGridViewRow row in containerGridView.Rows)
            {
                if (row.IsNewRow) continue;
                string path = row.Cells[2].Value.ToString();
                int expected = Convert.ToInt32(row.Cells[1].Value);
                FileInfo fi = new FileInfo(path);
                switch (fi.Extension.ToLowerInvariant())
                {
                    case ".png":
                    case ".bmp":
                        collected.AddRange(BMP.bmpDecode(new Bitmap(path),expected, stegKey));
                        break;
                    case ".wav":
                        collected.AddRange(new WAV(path).WavDecode(expected, stegKey));
                        break;
                    case ".jpg":
                    case ".jpeg":
                        var j = new JPEG(path, false);
                        j.jpegDecode(stegKey);
                        collected.AddRange(j.secretFiles);
                        break;
                }
            }

            List<byte> headerBytes = collected.GetRange(0, 10);
            if (checkBox.Checked)
                headerBytes = AesHelper.Decrypt(headerBytes.ToArray(), encryptTextBox.Text);

            ulong totalSize = 0;
            for (int i = 0; i < 5; i++)
                totalSize = (totalSize << 8) | headerBytes[i];
            ulong fileCount = 0;
            for (int i = 5; i < 10; i++)
                fileCount = (fileCount << 8) | headerBytes[i];

            // trim to real secret payload
            collected.RemoveRange((int)totalSize, collected.Count - (int)totalSize);
            collected.RemoveRange(0, 10);
            if (checkBox.Checked)
                collected = AesHelper.Decrypt(collected.ToArray(), encryptTextBox.Text);

            for (int f = 0; f < (int)fileCount; f++)
            {
                int pos = 0;
                int nameLen = collected[pos++];
                StringBuilder sbName = new StringBuilder();
                for (int i = 0; i < nameLen; i++)
                {
                    byte lo = collected[pos++];
                    byte hi = collected[pos++];
                    sbName.Append(Encoding.Unicode.GetString(new[] { lo, hi }));
                }
                string name = sbName.ToString();
                int extLen = collected[pos++];
                StringBuilder sbExt = new StringBuilder();
                for (int i = 0; i < extLen; i++) sbExt.Append((char)collected[pos++]);
                string ext = "." + sbExt;
                ulong partLen = 0;
                for (int i = 0; i < 5; i++) partLen = (partLen << 8) | collected[pos++];
                byte[] body = collected.GetRange(pos, (int)partLen).ToArray();
                collected.RemoveRange(0, pos + (int)partLen);
                File.WriteAllBytes(Path.Combine(savePath, name + ext), body);
            }

            try { loadingProcess?.Kill(); } catch { }
            MessageBox.Show("Інформація успішно витягнута!");
        }
        #endregion

        #region UI misc
        private void BtnContainerRmove_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in containerGridView.SelectedRows)
                if (!r.IsNewRow) containerGridView.Rows.RemoveAt(r.Index);
            labelContainer.Text = "Can Hide: " + GetPoints(GetSize(containerGridView)) + " byte";
        }

        private void BtnDataRemove_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow r in dataGridView.SelectedRows)
                if (!r.IsNewRow) dataGridView.Rows.RemoveAt(r.Index);
            labelHide.Text = "File Size: " + GetPoints(GetSize(dataGridView)) + " byte";
        }

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            encryptTextBox.Enabled = checkBox.Checked;
        }

        private void StegPanel_FormClosed(object sender, FormClosedEventArgs e)
        {
            foreach (Form frm in Application.OpenForms)
            { frm.Activate(); frm.Show(); break; }
        }
        #endregion
    }
}
