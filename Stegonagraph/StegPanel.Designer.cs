
// Додано завершальні директиви #region / #endregion та закриваючу дужку класу/namespace.

using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Stegonagraph
{
    public partial class StegPanel : Form
    {
        private IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }


        // ------------------------------------------------ theme -------------
        private readonly Color Accent = ColorTranslator.FromHtml("#4A90E2");
        private readonly Color DarkBack = ColorTranslator.FromHtml("#2E2F33");
        private readonly Color LightBack = ColorTranslator.FromHtml("#F5F7FA");
        private readonly Color TextCol = Color.White;

        private void ApplyTheme()
        {
            Font = new Font("Inter", 11f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = DarkBack;
            ForeColor = TextCol;

            void StyleBtn(Button b)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.Padding = new Padding(14, 6, 14, 6);
                b.Cursor = Cursors.Hand;
                b.MouseEnter += (s, e) => b.BackColor = ControlPaint.Light(Accent);
                b.MouseLeave += (s, e) => b.BackColor = Accent;
            }
            foreach (var ctl in Controls.OfType<Control>().SelectMany(GetAllControls))
            {
                if (ctl is Button btn) StyleBtn(btn);
                else if (ctl is DataGridView dgv) StyleGrid(dgv);
                else if (ctl is CheckBox chk) { chk.ForeColor = TextCol; chk.BackColor = DarkBack; }
                else if (ctl is TextBox tb) { tb.BackColor = LightBack; tb.ForeColor = Color.Black; tb.BorderStyle = BorderStyle.FixedSingle; }
                else if (ctl is Label lb) lb.ForeColor = TextCol;
            }
        }
        private static IEnumerable<Control> GetAllControls(Control root)
        { foreach (Control c in root.Controls) { yield return c; foreach (var cc in GetAllControls(c)) yield return cc; } }
        private void StyleGrid(DataGridView g)
        {
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = DarkBack;
            g.GridColor = ControlPaint.Dark(DarkBack);
            g.ColumnHeadersDefaultCellStyle.BackColor = Accent;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#424449");
            g.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            g.DefaultCellStyle.SelectionBackColor = Accent;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
        }
        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            components = new Container();

            // === root layout ===================================================
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = Color.Gainsboro,
                Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.Controls.Add(root);

            // === top panel =====================================================
            var pnlTop = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            root.Controls.Add(pnlTop, 0, 0);

            ContainerBtn = new Button { Text = "Обрати контейнери", AutoSize = true, BackColor = Color.WhiteSmoke };
            ContainerBtn.Click += SelectContainerBtn_Click;
            pnlTop.Controls.Add(ContainerBtn);

            SelectItemBtn = new Button { Text = "Обрати приховані файли", AutoSize = true, BackColor = Color.WhiteSmoke };
            SelectItemBtn.Click += SelectDataBtn_Click;
            pnlTop.Controls.Add(SelectItemBtn);

            pbPicture = new PictureBox { Size = new Size(28, 28), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(15, 3, 0, 3) };
            pnlTop.Controls.Add(pbPicture);

            // === center (SplitContainer) ======================================
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };
            root.Controls.Add(split, 0, 1);

            // left table – containers
            containerGridView = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false };
            containerGridView.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Назва", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, Name = "cName" },
                new DataGridViewTextBoxColumn { HeaderText = "Розмір (байт)", AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells, Name = "cSize" },
                new DataGridViewTextBoxColumn { HeaderText = "Шлях", Visible = false, Name = "cPath" });
            split.Panel1.Controls.Add(containerGridView);

            // right table – secret files
            dataGridView = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false };
            dataGridView.Columns.AddRange(
                new DataGridViewTextBoxColumn { HeaderText = "Назва", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, Name = "NameCol" },
                new DataGridViewTextBoxColumn { HeaderText = "Розмір (байт)", AutoSizeMode = DataGridViewAutoSizeColumnMode.DisplayedCells, Name = "SizeCol" },
                new DataGridViewTextBoxColumn { HeaderText = "Шлях", Visible = false, Name = "path" });
            split.Panel2.Controls.Add(dataGridView);
            // prevent header text from overlapping
            dataGridView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dataGridView.ColumnHeadersHeight = 50;
            
            // === bottom panel ==================================================
            var pnlBottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, AutoSize = true };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // місткість
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // btnRemove containers
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // spacer
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // розмір
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // start
            root.Controls.Add(pnlBottom, 0, 2);

            containerGridView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            containerGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            containerGridView.ColumnHeadersHeight = 50;
            labelContainer = new Label { Text = "Місткість: 0 б", AutoSize = true, Anchor = AnchorStyles.Left };
            pnlBottom.Controls.Add(labelContainer, 0, 0);

            labelHide = new Label { Text = "Розмір файлів: 0 б", AutoSize = true, Anchor = AnchorStyles.Right };
            pnlBottom.Controls.Add(labelHide, 3, 0);

            btnStart = new Button { Text = "Старт", AutoSize = true, BackColor = Color.WhiteSmoke };
            this.BackColor = Color.FromArgb(255, 230, 230); // ніжно-рожевий фон

            btnStart.Click += btnStart_Click;
            pnlBottom.Controls.Add(btnStart, 4, 0);

            btnContainerRemove = new Button { Text = "Видалити контейнери", AutoSize = true, BackColor = Color.WhiteSmoke };
            btnContainerRemove.Click += BtnContainerRmove_Click;
            pnlBottom.Controls.Add(btnContainerRemove, 0, 1);

            btnRemove = new Button { Text = "Видалити файли", AutoSize = true, BackColor = Color.WhiteSmoke };
            btnRemove.Click += BtnDataRemove_Click;
            pnlBottom.Controls.Add(btnRemove, 1, 1);

            checkBox = new CheckBox { Text = "Шифрування", AutoSize = true, Anchor = AnchorStyles.Left };
            checkBox.CheckedChanged += CheckBox_CheckedChanged;
            pnlBottom.Controls.Add(checkBox, 2, 1);

            encryptTextBox = new TextBox { Enabled = false, Width = 200, Anchor = AnchorStyles.Left };
            pnlBottom.Controls.Add(encryptTextBox, 3, 1);
            pnlBottom.SetColumnSpan(encryptTextBox, 2);

            // dialogs -----------------------------------------------------------
            openFileDialog1 = new OpenFileDialog { Title = "Виберіть файли", Multiselect = true };
            folderBrowserDialog1 = new FolderBrowserDialog { Description = "Виберіть теку" };

            // Form settings -----------------------------------------------------
            this.Text = "StegTool";
            this.ClientSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Icon = new Icon("StegTool.ico");
            this.BackColor = Color.FromArgb(255, 230, 230);
            this.FormClosed += StegPanel_FormClosed;
        }
        #endregion

        #region Controls
        private DataGridView dataGridView;
        private DataGridView containerGridView;
        private Button ContainerBtn;
        private Button SelectItemBtn;
        private Button btnContainerRemove;
        private Button btnRemove;
        private Button btnStart;
        private Label labelContainer;
        private Label labelHide;
        private CheckBox checkBox;
        private TextBox encryptTextBox;
        private PictureBox pbPicture;
        private OpenFileDialog openFileDialog1;
        private FolderBrowserDialog folderBrowserDialog1;
        #endregion
    }
}
