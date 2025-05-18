using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stegonagraph
{
    public partial class MainForm : Form
    {

        public MainForm()
        {
            InitializeComponent();
        }

        //1 hide 0 unhide
        private void PbHide_Click(object sender, EventArgs e)
        {
            if (ValidateKey()) 
                OpenNewForm(true, KeyStringToLSBByte(this.textBoxStegKey.Text));
        }

        private void PbUnhide_Click(object sender, EventArgs e)
        {
            if (ValidateKey())
                OpenNewForm(false, KeyStringToLSBByte(this.textBoxStegKey.Text));
        }

        private byte[] KeyStringToLSBByte(String keyStr) {

            byte[] keyByte = new byte[keyStr.Length*4];
            UInt64 keyIndex = 0;

            for (int i = 0; i < keyStr.Length; i++)
            {
                int bt = (byte)keyStr[i];
                for (int j = 0; j < 4; j++)
                {
                    keyByte[keyIndex++] = (byte)(bt & 3);
                    bt = bt >> 2;
                }
            }

            return keyByte;
        }
        private void OpenNewForm(Boolean bl,byte[] keySteg)
        {
            Form ifrm = new StegPanel(bl, keySteg);
            ifrm.Show();
            this.Hide();
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(System.IO.Directory.GetCurrentDirectory() + "//Resources//AboutApplication.pdf");
            }
            catch (Exception error )
            {
                MessageBox.Show("Файл пошкоджено!");
            }
        }

        private void textBoxStegKey_TextChanged(object sender, EventArgs e)
        {
            if (Regex.IsMatch(textBoxStegKey.Text, @"[^\u0000-\u007F]+"))
            {
                textBoxStegKey.Text = Regex.Replace(textBoxStegKey.Text, @"[^\u0000-\u007F]+", string.Empty);
                MessageBox.Show("Це поле приймає лише латинські символи.");
            }
        }

        private Boolean ValidateKey()
        {
            String key = this.textBoxStegKey.Text;
            if (key.Length == 0)
            {
                MessageBox.Show("Будь ласка, введіть ключ стеганографії для продовження!");
                return false;
            }
            if (Regex.IsMatch(textBoxStegKey.Text, @"[^\u0000-\u007F]+"))
            {
                MessageBox.Show("Це поле приймає лише латинські символи.");
                return false;
            }
            return true;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {

            if (ValidateKey())
                OpenNewForm(true, KeyStringToLSBByte(this.textBoxStegKey.Text));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (ValidateKey())
                OpenNewForm(false, KeyStringToLSBByte(this.textBoxStegKey.Text));
        }

        private void unhideLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
