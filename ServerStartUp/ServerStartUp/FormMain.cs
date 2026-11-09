using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace ServerStartUp
{
    public partial class FormMain : Form
    {
        // ---------------------------------------------------------------
        // Índices de colunas da grid (evita "magic numbers" espalhados)
        // ---------------------------------------------------------------
        private const int ColEnabled = 0;
        private const int ColStatus = 1;
        private const int ColPort = 2;
        private const int ColPath = 3;
        private const int ColArgs = 4;
        private const int ColStart = 5;

        private readonly AppMng AppManager = new AppMng();
        private readonly DataMng DataManager = new DataMng();

        public int windowStyleOnRun = 0;
        public int processCheckTime = 1;

        private TableLayoutPanel _dashboardLayout;
        private StatCardMini _totalCard;
        private StatCardMini _runningCard;
        private StatCardMini _stoppedCard;
        private Label _dashboardStatus;

        public FormMain()
        {
            InitializeComponent();

            // Carrega configurações ANTES de aplicar no timer
            windowStyleOnRun = (int)DataManager.LoadRegSetting(
                "windowStyleOnRun",
                windowStyleOnRun);

            processCheckTime = (int)DataManager.LoadRegSetting(
                "processCheckTime",
                processCheckTime);

            timerMain.Interval = Math.Max(1, processCheckTime) * 1000;

            BuildMuServerDashboardLayout();
        }

        private void timerMain_Tick(object sender, EventArgs e)
        {
            timerMain.Interval = Math.Max(1, processCheckTime) * 1000;

            try
            {
                foreach (DataGridViewRow row in dataGridViewMain.Rows)
                {
                    if (!AppManager.isNull(row.Index) &&
                        Convert.ToBoolean(row.Cells[ColEnabled].Value) &&
                        !AppManager.isRunning(row.Index))
                    {
                        row.ReadOnly = true;

                        AppManager.Run(
                            row.Index,
                            row.Cells[ColPath].Value.ToString(),
                            row.Cells[ColArgs].Value.ToString(),
                            Convert.ToInt32(row.Cells[ColPort].Value),
                            windowStyleOnRun);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[timerMain_Tick] {ex}");
            }
        }

        private void timerMainGrid_Tick(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in dataGridViewMain.Rows)
                {
                    bool running = AppManager.isRunning(row.Index);

                    row.ReadOnly = running;
                    row.Cells[ColStatus].Value = running
                        ? Properties.Resources.on
                        : Properties.Resources.off;

                    UpdateStartButton(row, running);
                }

                statusStripMain.Items[0].Text = AppManager.AnyRunning()
                    ? $" {AppManager.GetCount(false)} servidor(es) online.."
                    : " nenhum servidor online..";

                UpdateDashboardStats();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[timerMainGrid_Tick] {ex}");
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!AppManager.AnyRunning())
            {
                try
                {
                    DataManager.SaveDataToXml(dataGridViewMain);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }

                if (MessageBox.Show(
                    Properties.Resources.Msg_Close,
                    Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
            else
            {
                MessageBox.Show(
                    Properties.Resources.Msg_CloseError,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                e.Cancel = true;
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            try
            {
                DataManager.LoadDataFromXml();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            dataGridViewMain.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            bool hasRows = dataGridViewMain.Rows.Count > 0;
            toolStripButtonDel.Enabled = hasRows;
            toolStripButtonDelAll.Enabled = hasRows;

            statusStripMain.Items[0].Text = " nenhum servidor online..";
            timerMainGrid_Tick(sender, e);
            UpdateDashboardStats();
        }

        private void toolStripButtonAdd_Click(object sender, EventArgs e)
        {
            try
            {
                if (openFileDialogMain.ShowDialog() != DialogResult.OK)
                    return;

                if (!File.Exists(openFileDialogMain.FileName))
                {
                    MessageBox.Show(
                        Properties.Resources.Msg_InvalidFile,
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                dataGridViewMain.Rows.Add(
                    true,
                    Properties.Resources.off,
                    "2000",
                    openFileDialogMain.FileName,
                    "");

                CommitGridEdit();

                toolStripButtonDel.Enabled = true;
                toolStripButtonDelAll.Enabled = true;

                UpdateDashboardStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripButtonDel_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    DataGridViewRow row = dataGridViewMain.Rows[cell.RowIndex];

                    if (MessageBox.Show(
                        Properties.Resources.Msg_DelAsk,
                        Text,
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question) != DialogResult.OK)
                    {
                        continue;
                    }

                    if (!AppManager.isRunning(row.Index))
                    {
                        dataGridViewMain.Rows.RemoveAt(row.Index);

                        if (dataGridViewMain.SelectedRows.Count == 0)
                        {
                            toolStripButtonDel.Enabled = false;
                            toolStripButtonDelAll.Enabled = false;
                        }

                        UpdateDashboardStats();
                    }
                    else
                    {
                        MessageBox.Show(
                            Properties.Resources.Msg_DelError,
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripButtonDelAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (MessageBox.Show(
                    Properties.Resources.Msg_DelAsk2,
                    Text,
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question) != DialogResult.OK)
                {
                    return;
                }

                if (!AppManager.AnyRunning())
                {
                    dataGridViewMain.Rows.Clear();

                    toolStripButtonDel.Enabled = false;
                    toolStripButtonDelAll.Enabled = false;

                    UpdateDashboardStats();
                }
                else
                {
                    MessageBox.Show(
                        Properties.Resources.Msg_DelError3,
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripButtonRun_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppManager.AnyRunning())
                    return;

                SetRunningUiState(running: true, partial: false);
                CommitGridEdit();

                foreach (DataGridViewRow row in dataGridViewMain.Rows)
                {
                    if (AppManager.isNull(row.Index) &&
                        Convert.ToBoolean(row.Cells[ColEnabled].Value))
                    {
                        row.ReadOnly = true;

                        AppManager.Run(
                            row.Index,
                            row.Cells[ColPath].Value.ToString(),
                            row.Cells[ColArgs].Value.ToString(),
                            Convert.ToInt32(row.Cells[ColPort].Value),
                            windowStyleOnRun);
                    }
                }

                timerMain.Start();
                timerMainGrid.Start();
                timerMainGrid_Tick(sender, e);

                WindowState = FormWindowState.Normal;
                Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void openFilePathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                CommitGridEdit();

                HashSet<string> opened = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    DataGridViewRow row = dataGridViewMain.Rows[cell.RowIndex];
                    string path = Convert.ToString(row.Cells[ColPath].Value);

                    // Ignora caminhos vazios e evita abrir o mesmo arquivo duas vezes
                    if (string.IsNullOrWhiteSpace(path) || !opened.Add(path))
                        continue;

                    if (File.Exists(path))
                    {
                        Process.Start("explorer.exe", "/select,\"" + path + "\"");
                    }
                    else
                    {
                        MessageBox.Show(
                            "Arquivo não encontrado:\n" + path,
                            Text,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripButtonEnd_Click(object sender, EventArgs e)
        {
            try
            {
                CommitGridEdit();

                timerMain.Stop();
                timerMainGrid.Stop();

                AppManager.KillAll();

                SetRunningUiState(running: false, partial: false);

                foreach (DataGridViewRow row in dataGridViewMain.Rows)
                {
                    row.ReadOnly = false;
                    row.Cells[ColStatus].Value = Properties.Resources.off;
                    UpdateStartButton(row, false);
                }

                statusStripMain.Refresh();
                dataGridViewMain.Refresh();

                statusStripMain.Items[0].Text = " nenhum servidor online..";
                UpdateDashboardStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripButtonEndSelected_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    if (AppManager.Kill(cell.RowIndex))
                    {
                        DataGridViewRow row = dataGridViewMain.Rows[cell.RowIndex];
                        row.ReadOnly = false;
                        row.Cells[ColStatus].Value = Properties.Resources.off;
                        UpdateStartButton(row, false);
                    }
                }

                if (!AppManager.AnyRunning())
                {
                    timerMain.Stop();
                    timerMainGrid.Stop();

                    SetRunningUiState(running: false, partial: false);
                    statusStripMain.Items[0].Text = " nenhum servidor online..";

                    foreach (DataGridViewRow row in dataGridViewMain.Rows)
                    {
                        row.ReadOnly = false;
                        row.Cells[ColStatus].Value = Properties.Resources.off;
                        UpdateStartButton(row, false);
                    }
                }

                dataGridViewMain.Refresh();
                UpdateDashboardStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void SetRunningUiState(bool running, bool partial)
        {
            toolStripButtonEnd.Visible = running;
            toolStripButtonRun.Visible = !running;

            toolStripButtonAdd.Enabled = !running;
            toolStripButtonDel.Enabled = !running;
            toolStripButtonDelAll.Enabled = !running;

            if (!running)
            {
                toolStripButtonShow.Visible = false;
                toolStripButtonHide.Visible = false;
                return;
            }

            if (partial)
            {
                toolStripButtonHide.Visible = true;
            }
            else
            {
                toolStripButtonShow.Visible = windowStyleOnRun == 0;
                toolStripButtonHide.Visible = windowStyleOnRun != 0;
            }
        }

        private void UpdateStartButton(DataGridViewRow row, bool running)
        {
            if (row == null || row.IsNewRow || row.Cells.Count <= ColStart)
                return;

            row.Cells[ColStart].Value = running
                ? Properties.Resources.stop
                : Properties.Resources.start;

            row.Cells[ColStart].ToolTipText = running
                ? "Parar servidor"
                : "Iniciar servidor";
        }

        private void CommitGridEdit()
        {
            if (dataGridViewMain.IsCurrentCellDirty)
            {
                dataGridViewMain.CommitEdit(
                    DataGridViewDataErrorContexts.Commit);
            }
        }

        private void FormMain_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
                return;

            Hide();

            notifyIconMain.Visible = true;
            notifyIconMain.BalloonTipTitle =
                Text + Properties.Resources.Msg_Minimized;
            notifyIconMain.BalloonTipText =
                Properties.Resources.Msg_MinimizedText;

            notifyIconMain.ShowBalloonTip(
                10000,
                Text + Properties.Resources.Msg_Minimized,
                Properties.Resources.Msg_MinimizedText,
                ToolTipIcon.Info);
        }

        private void notifyIconMain_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIconMain.Visible = false;
        }

        private void toolStripButtonHide_Click(object sender, EventArgs e)
        {
            if (AppManager.AnyRunning() && AppManager.HideAllWindows())
            {
                toolStripButtonHide.Visible = false;
                toolStripButtonShow.Visible = true;
            }
        }

        private void toolStripButtonShow_Click(object sender, EventArgs e)
        {
            if (AppManager.AnyRunning() && AppManager.ShowAllWindows())
            {
                toolStripButtonShow.Visible = false;
                toolStripButtonHide.Visible = true;
            }
        }

        private void dataGridViewMain_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex != ColStart)
                    return;

                CommitGridEdit();

                DataGridViewRow row = dataGridViewMain.Rows[e.RowIndex];

                if (AppManager.isRunning(row.Index))
                {
                    if (AppManager.Kill(row.Index))
                    {
                        row.ReadOnly = false;
                        row.Cells[ColEnabled].Value = false;
                        row.Cells[ColStatus].Value = Properties.Resources.off;
                        UpdateStartButton(row, false);

                        if (!AppManager.AnyRunning())
                        {
                            timerMain.Stop();
                            timerMainGrid.Stop();
                            SetRunningUiState(running: false, partial: false);
                            statusStripMain.Items[0].Text = " nenhum servidor online..";
                        }

                        UpdateDashboardStats();
                    }

                    return;
                }

                string path = Convert.ToString(row.Cells[ColPath].Value);
                string args = Convert.ToString(row.Cells[ColArgs].Value);

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    MessageBox.Show(
                        Properties.Resources.Msg_InvalidFile,
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                int delay;
                if (!int.TryParse(Convert.ToString(row.Cells[ColPort].Value), out delay))
                    delay = 0;

                row.ReadOnly = true;

                AppManager.Run(
                    row.Index,
                    path,
                    args,
                    delay,
                    windowStyleOnRun);

                SetRunningUiState(running: true, partial: true);

                timerMainGrid.Start();
                timerMainGrid_Tick(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void dataGridViewMain_CellDoubleClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 ||
                    e.ColumnIndex != ColPath ||
                    dataGridViewMain.Rows[e.RowIndex].ReadOnly)
                {
                    return;
                }

                if (openFileDialogMain.ShowDialog() != DialogResult.OK)
                    return;

                if (!File.Exists(openFileDialogMain.FileName))
                {
                    MessageBox.Show(
                        Properties.Resources.Msg_InvalidFile,
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                dataGridViewMain.Rows[e.RowIndex].Cells[e.ColumnIndex].Value =
                    openFileDialogMain.FileName;

                CommitGridEdit();

                toolStripButtonDel.Enabled = true;
                toolStripButtonDelAll.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void contextMenuStripMain_Opened(object sender, EventArgs e)
        {
            try
            {
                CommitGridEdit();

                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    bool running = AppManager.isRunning(cell.RowIndex);

                    contextMenuStripMain.Items[0].Visible = !running;
                    contextMenuStripMain.Items[1].Visible = running;
                    contextMenuStripMain.Items[2].Visible = running;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripMenuItemShowSelected_Click(object sender, EventArgs e)
        {
            try
            {
                CommitGridEdit();

                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    if (AppManager.isRunning(cell.RowIndex))
                    {
                        AppManager.ShowSelectedWindow(cell.RowIndex, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void toolStripMenuItemHideSelected_Click(object sender, EventArgs e)
        {
            try
            {
                CommitGridEdit();

                foreach (DataGridViewCell cell in dataGridViewMain.SelectedCells)
                {
                    if (AppManager.isRunning(cell.RowIndex))
                    {
                        AppManager.ShowSelectedWindow(cell.RowIndex, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (AppManager.AnyRunning())
                    return;

                using (FormSettings formSettings = new FormSettings())
                {
                    formSettings.comboBoxWindowStyle.SelectedIndex = windowStyleOnRun;
                    formSettings.numericUpDownCheckTime.Value = processCheckTime;

                    if (formSettings.ShowDialog() != DialogResult.OK)
                        return;

                    windowStyleOnRun = formSettings.comboBoxWindowStyle.SelectedIndex;
                    processCheckTime = Convert.ToInt32(formSettings.numericUpDownCheckTime.Value);

                    DataManager.SetRegSetting("windowStyleOnRun", windowStyleOnRun);
                    DataManager.SetRegSetting("processCheckTime", processCheckTime);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (Form aboutForm = CreateAboutForm())
            {
                aboutForm.ShowDialog(this);
            }
        }

        private sealed class DoubleBufferedForm : Form
        {
            public DoubleBufferedForm()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
                DoubleBuffered = true;
            }
        }
        private Image GetAboutIcon()
        {
            try
            {
                return Properties.Resources.ico.ToBitmap();
            }
            catch
            {
                return SystemIcons.Application.ToBitmap();
            }
        }

        private Form CreateAboutForm()
        {
            Color gold = MuTheme.Gold;
            Color goldDim = MuTheme.GoldDim;
            Color background = MuTheme.Background;
            Color textPrimary = MuTheme.TextPrimary;
            Color textMuted = MuTheme.TextMuted;

            Form form = new DoubleBufferedForm
            {
                FormBorderStyle = FormBorderStyle.None,
                Size = new Size(340, 300),
                MinimumSize = new Size(340, 300),
                MaximumSize = new Size(340, 300),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = background,
                ShowInTaskbar = false
            };

            ApplyRoundedRegion(form, 14);

            // ===== Header compacto =====
            GradientPanel header = new GradientPanel
            {
                Dock = DockStyle.Top,
                Height = 82,
                ColorTop = Color.FromArgb(46, 42, 30),
                ColorBottom = background
            };
            form.Controls.Add(header);
            EnableDrag(header, form);

            CirclePanel logoCircle = new CirclePanel
            {
                Size = new Size(48, 48),
                Location = new Point(18, 17),
                FillColor = Color.FromArgb(40, gold),
                BorderColor = gold,
                IconImage = GetAboutIcon(),
                IconPadding = 10
            };
            header.Controls.Add(logoCircle);

            Label title = new Label
            {
                AutoSize = true,
                Text = "ServerStartUp",
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = textPrimary,
                BackColor = Color.Transparent,
                Location = new Point(78, 24)
            };
            header.Controls.Add(title);

            Label subtitle = new Label
            {
                AutoSize = false,
                Size = new Size(220, 28),
                Text = "Gerenciador de Inicialização de Servidores",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = textMuted,
                BackColor = Color.Transparent,
                Location = new Point(79, 48)
            };
            header.Controls.Add(subtitle);

            Button closeX = new Button
            {
                Size = new Size(26, 26),
                Location = new Point(form.Width - 36, 8),
                FlatStyle = FlatStyle.Flat,
                Text = "✕",
                Font = new Font("Segoe UI", 9F),
                ForeColor = textMuted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            closeX.FlatAppearance.BorderSize = 0;
            closeX.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, gold);
            closeX.MouseEnter += (s, e) => closeX.ForeColor = gold;
            closeX.MouseLeave += (s, e) => closeX.ForeColor = textMuted;
            header.Controls.Add(closeX);
            closeX.BringToFront();

            // ===== Informações =====
            TableLayoutPanel infoGrid = new TableLayoutPanel
            {
                Location = new Point(18, 94),
                Size = new Size(form.Width - 36, 88),
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 4,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 4; i++)
                infoGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

            AddInfoRow(infoGrid, 0, "Versão:", "2.0", goldDim, textPrimary);
            AddInfoRow(infoGrid, 1, "Desenvolvedor:", "Qubit", goldDim, textPrimary);
            AddInfoRow(infoGrid, 2, "Lançamento:", "30 de outubro de 2026", goldDim, textPrimary);
            AddInfoRow(infoGrid, 3, "Plataforma:", ".NET / Windows Forms", goldDim, textPrimary);

            form.Controls.Add(infoGrid);

            // ===== Separador =====
            Panel separator = new Panel
            {
                Location = new Point(18, 191),
                Size = new Size(form.Width - 36, 1),
                BackColor = Color.FromArgb(50, gold)
            };
            form.Controls.Add(separator);

            // ===== Descrição =====
            Label description = new Label
            {
                Location = new Point(18, 202),
                Size = new Size(form.Width - 36, 38),
                Text = "Gerenciador de inicialização para servidores MU Online.\nMonitoramento, controle e automação em um só lugar.",
                Font = new Font("Segoe UI", 8F),
                ForeColor = textMuted,
                BackColor = Color.Transparent
            };
            form.Controls.Add(description);

            // ===== Link =====
            LinkLabel link = new LinkLabel
            {
                AutoSize = true,
                Location = new Point(18, 260),
                Text = "GitHub / ProjectRebirthMu",
                Font = new Font("Segoe UI", 8F),
                LinkColor = gold,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = gold,
                BackColor = Color.Transparent,
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            link.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://github.com/ProjectRebirthMu")
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            form.Controls.Add(link);

            // ===== Botão Fechar =====
            Button closeButton = new Button
            {
                Size = new Size(88, 28),
                Location = new Point(form.Width - 106, form.Height - 40),
                FlatStyle = FlatStyle.Flat,
                Text = "FECHAR",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                BackColor = Color.FromArgb(40, gold),
                ForeColor = gold,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK
            };
            closeButton.FlatAppearance.BorderSize = 1;
            closeButton.FlatAppearance.BorderColor = goldDim;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, gold);
            form.Controls.Add(closeButton);

            form.AcceptButton = closeButton;
            form.CancelButton = closeX;
            closeX.Click += (s, e) => form.Close();

            return form;
        }

        private static void AddInfoRow(TableLayoutPanel grid, int row, string label, string value, Color labelColor, Color valueColor)
        {
            Label lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = label,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = labelColor,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            Label val = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = value,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = valueColor,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(val, 1, row);
        }

        // =================================================================
        //  CONTROLES AUXILIARES DE DESENHO — usados pelo AboutBox
        // =================================================================
        private sealed class GradientPanel : Panel
        {
            public Color ColorTop { get; set; } = Color.Black;
            public Color ColorBottom { get; set; } = Color.Black;

            public GradientPanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);

                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle rect = ClientRectangle;
                if (rect.Width <= 0 || rect.Height <= 0)
                    return;

                using (LinearGradientBrush brush = new LinearGradientBrush(
                    rect, ColorTop, ColorBottom, LinearGradientMode.Vertical))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }

                base.OnPaint(e);
            }
        }

        private sealed class CirclePanel : Panel
        {
            public Color FillColor { get; set; } = Color.Gold;
            public Color BorderColor { get; set; } = Color.Gold;
            public Image IconImage { get; set; }
            public int IconPadding { get; set; } = 14;

            public CirclePanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.SupportsTransparentBackColor,
                    true);

                DoubleBuffered = true;
                BackColor = Color.Transparent;
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                if (Parent != null)
                {
                    using (SolidBrush b = new SolidBrush(Parent.BackColor))
                    {
                        pevent.Graphics.FillRectangle(b, ClientRectangle);
                    }
                }
                else
                {
                    base.OnPaintBackground(pevent);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Rectangle rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));

                using (SolidBrush fillBrush = new SolidBrush(FillColor))
                {
                    e.Graphics.FillEllipse(fillBrush, rect);
                }

                using (Pen borderPen = new Pen(BorderColor, 1.5F))
                {
                    e.Graphics.DrawEllipse(borderPen, rect);
                }

                if (IconImage != null)
                {
                    int size = Math.Max(1, Math.Min(Width, Height) - IconPadding * 2);
                    Rectangle iconRect = new Rectangle(
                        (Width - size) / 2,
                        (Height - size) / 2,
                        size,
                        size);

                    e.Graphics.DrawImage(IconImage, iconRect);
                }

                base.OnPaint(e);
            }
        }

        private sealed class BadgePanel : Panel
        {
            public Color FillColor { get; set; } = Color.Gold;
            public Color BorderColor { get; set; } = Color.Gold;

            public BadgePanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.SupportsTransparentBackColor,
                    true);

                DoubleBuffered = true;
                BackColor = Color.Transparent;
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                if (Parent != null)
                {
                    using (SolidBrush b = new SolidBrush(Parent.BackColor))
                    {
                        pevent.Graphics.FillRectangle(b, ClientRectangle);
                    }
                }
                else
                {
                    base.OnPaintBackground(pevent);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
                int radius = Math.Max(1, Height / 2);

                using (GraphicsPath path = RoundedRect(rect, radius))
                {
                    using (SolidBrush fillBrush = new SolidBrush(FillColor))
                    {
                        e.Graphics.FillPath(fillBrush, path);
                    }

                    using (Pen borderPen = new Pen(BorderColor, 1F))
                    {
                        e.Graphics.DrawPath(borderPen, path);
                    }
                }

                base.OnPaint(e);
            }

            private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                GraphicsPath path = new GraphicsPath();

                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                return path;
            }
        }

        private static void ApplyRoundedRegion(Form form, int radius)
        {
            void Apply()
            {
                GraphicsPath path = new GraphicsPath();
                Rectangle bounds = new Rectangle(0, 0, form.Width, form.Height);
                int d = radius * 2;

                path.StartFigure();
                path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                form.Region = new Region(path);
            }

            Apply();
            form.Resize += (s, e) => Apply();
        }

        private static void EnableDrag(Control control, Form targetForm)
        {
            Point dragStart = Point.Empty;
            bool dragging = false;

            control.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    dragging = true;
                    dragStart = e.Location;
                }
            };

            control.MouseMove += (s, e) =>
            {
                if (dragging)
                {
                    Point screenPoint = control.PointToScreen(e.Location);
                    targetForm.Location = new Point(
                        screenPoint.X - dragStart.X,
                        screenPoint.Y - dragStart.Y);
                }
            };

            control.MouseUp += (s, e) =>
            {
                dragging = false;
            };
        }

        private void menuStripMain_MenuActivate(object sender, EventArgs e)
        {
            optionsToolStripMenuItem.Enabled = !AppManager.AnyRunning();
        }

        // =================================================================
        //  CONTROLE CUSTOMIZADO — Card de estatística compacto (StatCardMini)
        // =================================================================
        private enum StatIconType { List, Signal, Power }

        private sealed class StatCardMini : Panel
        {
            private string _heading = "";
            private string _value = "0";

            public Color AccentColor { get; set; } = Color.Gold;
            public Color SurfaceColor { get; set; } = Color.FromArgb(32, 30, 38);
            public Color ForegroundColor { get; set; } = Color.White;
            public Color MutedColor { get; set; } = Color.Gray;
            public StatIconType IconType { get; set; } = StatIconType.List;

            public StatCardMini()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);

                DoubleBuffered = true;
                BackColor = Color.Transparent;
            }

            public void SetContent(string heading, string value)
            {
                _heading = heading;
                _value = value;
                Invalidate();
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                if (Parent != null)
                {
                    using (SolidBrush b = new SolidBrush(Parent.BackColor))
                    {
                        pevent.Graphics.FillRectangle(b, ClientRectangle);
                    }
                }
                else
                {
                    base.OnPaintBackground(pevent);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                const int radius = 5;
                const int accentWidth = 3;

                Rectangle cardRect = new Rectangle(0, 0, Width - 1, Height - 1);

                // Fundo flat (sem gradiente)
                using (GraphicsPath cardPath = RoundedRect(cardRect, radius))
                using (SolidBrush bgBrush = new SolidBrush(SurfaceColor))
                {
                    g.FillPath(bgBrush, cardPath);

                    using (Pen borderPen = new Pen(Color.FromArgb(50, AccentColor), 1F))
                    {
                        g.DrawPath(borderPen, cardPath);
                    }
                }

                // Barra de destaque lateral (esquerda, vertical)
                Rectangle accentRect = new Rectangle(
                    cardRect.X, cardRect.Y + 2, accentWidth, cardRect.Height - 4);

                using (GraphicsPath accentPath = RoundedRectLeft(accentRect, 2))
                using (SolidBrush accentBrush = new SolidBrush(AccentColor))
                {
                    g.FillPath(accentBrush, accentPath);
                }

                // Dot indicador (bolinha pequena)
                int dotSize = 6;
                int dotX = accentWidth + 10;
                int dotY = (cardRect.Height - dotSize) / 2;
                Rectangle dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);

                using (SolidBrush dotBrush = new SolidBrush(AccentColor))
                {
                    g.FillEllipse(dotBrush, dotRect);
                }

                // Textos: heading (esquerda) + valor (direita), tudo em 1 linha
                int textX = dotX + dotSize + 8;
                int valueWidth = 40;
                int headingWidth = Math.Max(10, cardRect.Width - textX - valueWidth - 10);

                Rectangle headingRect = new Rectangle(
                    textX, 0, headingWidth, cardRect.Height);

                Rectangle valueRect = new Rectangle(
                    cardRect.Width - valueWidth - 8, 0, valueWidth, cardRect.Height);

                TextRenderer.DrawText(
                    g, _heading,
                    new Font("Segoe UI", 7.8F, FontStyle.Bold),
                    headingRect, MutedColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(
                    g, _value,
                    new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                    valueRect, ForegroundColor,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                GraphicsPath path = new GraphicsPath();

                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                return path;
            }

            private static GraphicsPath RoundedRectLeft(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                GraphicsPath path = new GraphicsPath();

                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddLine(bounds.Right, bounds.Y, bounds.Right, bounds.Bottom);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                return path;
            }
        }

        // =================================================================
        //  TEMA VISUAL — "MU Online Server Manager"
        //  Paleta: fundo escuro, dourado de destaque, vinho para ações.
        // =================================================================
        private static class MuTheme
        {
            public static readonly Color Background = Color.FromArgb(18, 18, 24);
            public static readonly Color Panel = Color.FromArgb(28, 28, 36);
            public static readonly Color Surface = Color.FromArgb(38, 36, 46);
            public static readonly Color SurfaceLight = Color.FromArgb(48, 46, 58);
            public static readonly Color Border = Color.FromArgb(70, 60, 40);
            public static readonly Color Gold = Color.FromArgb(212, 175, 55);
            public static readonly Color GoldDim = Color.FromArgb(150, 120, 40);
            public static readonly Color Wine = Color.FromArgb(120, 30, 40);
            public static readonly Color TextPrimary = Color.FromArgb(235, 225, 200);
            public static readonly Color TextMuted = Color.FromArgb(160, 150, 130);
        }

        private void BuildMuServerDashboardLayout()
        {
            Color background = MuTheme.Background;
            Color panel = MuTheme.Panel;
            Color surface = MuTheme.Surface;
            Color border = MuTheme.Border;
            Color foreground = MuTheme.TextPrimary;
            Color muted = MuTheme.TextMuted;
            Color accent = MuTheme.Gold;

            BackColor = background;
            ForeColor = foreground;
            Font = new Font("Segoe UI", 9F);

            Text = string.IsNullOrWhiteSpace(Text)
                ? "MU ServerStartUp"
                : Text;

            StartPosition = FormStartPosition.CenterScreen;

            Control menu = FindControlByName(this, "menuStripMain");
            Control status = FindControlByName(this, "statusStripMain");
            Control grid = FindControlByName(this, "dataGridViewMain");

            if (menu == null || status == null || grid == null)
                return;

            List<ToolStrip> toolStrips = new List<ToolStrip>();
            CollectToolStrips(this, toolStrips);

            ToolStrip mainToolbar = null;

            foreach (ToolStrip strip in toolStrips)
            {
                if (strip.Name != "menuStripMain" && strip.Name != "statusStripMain")
                {
                    mainToolbar = strip;
                    break;
                }
            }

            menu.Parent.Controls.Remove(menu);
            status.Parent.Controls.Remove(status);
            grid.Parent.Controls.Remove(grid);

            if (mainToolbar?.Parent != null)
            {
                mainToolbar.Parent.Controls.Remove(mainToolbar);
            }

            _dashboardLayout = new TableLayoutPanel
            {
                Name = "dashboardLayout",
                Dock = DockStyle.Fill,
                BackColor = background,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(8),
                Margin = Padding.Empty,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };

            _dashboardLayout.ColumnStyles.Clear();
            _dashboardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            _dashboardLayout.RowStyles.Clear();
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _dashboardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); // era 22F

            menu.Dock = DockStyle.Fill;
            menu.Margin = Padding.Empty;
            menu.Padding = new Padding(4, 0, 4, 0);

            StyleToolStripForTheme(menu as ToolStrip, panel, surface, foreground, accent);

            // -------------------------------------------------------
            // Toolbar
            // -------------------------------------------------------
            Panel toolbarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = panel,
                Padding = new Padding(3, 1, 3, 1),
                Margin = new Padding(0, 2, 0, 3)
            };

            if (mainToolbar != null)
            {
                mainToolbar.Dock = DockStyle.Fill;
                mainToolbar.AutoSize = false;
                mainToolbar.Height = 32;
                mainToolbar.MinimumSize = Size.Empty;
                mainToolbar.GripStyle = ToolStripGripStyle.Hidden;
                mainToolbar.Margin = Padding.Empty;
                mainToolbar.Padding = new Padding(2, 0, 2, 0);

                StyleToolStripForTheme(mainToolbar, panel, surface, foreground, accent);

                toolbarPanel.Controls.Add(mainToolbar);
            }

            // -------------------------------------------------------
            // Cards de estatística (StatCardMini)
            // -------------------------------------------------------
            FlowLayoutPanel cards = new FlowLayoutPanel
            {
                Name = "dashboardCards",
                Dock = DockStyle.Fill,
                BackColor = background,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Margin = new Padding(0, 0, 0, 1),
                Padding = Padding.Empty
            };

            _totalCard = CreateStatCardMini(
                "CADASTRADOS", StatIconType.List, surface, foreground, muted, MuTheme.GoldDim);

            _runningCard = CreateStatCardMini(
                "ONLINE", StatIconType.Signal, surface, foreground, muted, MuTheme.Gold);

            _stoppedCard = CreateStatCardMini(
                "OFFLINE", StatIconType.Power, surface, foreground, muted, MuTheme.Wine);

            cards.Controls.Add(_totalCard);
            cards.Controls.Add(_runningCard);
            cards.Controls.Add(_stoppedCard);

            void ResizeCards()
            {
                int count = cards.Controls.Count;
                int availableWidth = cards.ClientSize.Width;

                if (count == 0 || availableWidth <= 0)
                    return;

                const int gap = 8;
                int cardWidth = Math.Max(1, (availableWidth - gap * (count - 1)) / count);
                int cardHeight = Math.Min(32, cards.ClientSize.Height);

                for (int i = 0; i < count; i++)
                {
                    Control card = cards.Controls[i];
                    card.Margin = new Padding(0, 0, i < count - 1 ? gap : 0, 0);
                    card.Width = cardWidth;
                    card.Height = cardHeight;
                }
            }

            cards.SizeChanged += (s, e) => ResizeCards();

            // -------------------------------------------------------
            // Grid
            // -------------------------------------------------------
            Panel gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = panel,
                Padding = new Padding(1),
                Margin = Padding.Empty
            };

            grid.Dock = DockStyle.Fill;
            grid.Margin = Padding.Empty;

            StyleDashboardGrid(grid as DataGridView, background, panel, surface, border, foreground, accent);

            gridPanel.Controls.Add(grid);

            // -------------------------------------------------------
            // Status bar
            // -------------------------------------------------------
            status.Dock = DockStyle.Fill;
            status.BackColor = panel;
            status.ForeColor = muted;
            status.Margin = Padding.Empty;

            if (status is StatusStrip statusStrip)
            {
                statusStrip.SizingGrip = false;
                statusStrip.Height = 28;
                statusStrip.Padding = new Padding(8, 0, 8, 0);

                StyleToolStripForTheme(statusStrip, panel, surface, foreground, accent);

                statusStrip.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;

                // Ajusta o label existente (ex: "nenhum servidor online..")
                if (statusStrip.Items.Count > 0 && statusStrip.Items[0] is ToolStripStatusLabel firstLabel)
                {
                    firstLabel.Margin = new Padding(0, 0, 0, 0);
                    firstLabel.TextAlign = ContentAlignment.MiddleLeft;
                }

                _dashboardStatus = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = muted,
                    BackColor = panel,
                    Text = "Sistema pronto",
                    Font = new Font("Segoe UI", 8F),
                    Padding = new Padding(0, 1, 0, 0)
                };

                statusStrip.Items.Add(
                    new ToolStripControlHost(_dashboardStatus)
                    {
                        AutoSize = false,
                        Size = new Size(220, 24),
                        Alignment = ToolStripItemAlignment.Right,
                        Margin = new Padding(3, 0, 3, 0),
                        Padding = Padding.Empty
                    });
            }

            _dashboardLayout.Controls.Add(menu, 0, 0);
            _dashboardLayout.Controls.Add(toolbarPanel, 0, 1);
            _dashboardLayout.Controls.Add(cards, 0, 2);
            _dashboardLayout.Controls.Add(gridPanel, 0, 3);
            _dashboardLayout.Controls.Add(status, 0, 4);

            Controls.Add(_dashboardLayout);
            _dashboardLayout.BringToFront();

            ResizeCards();
            UpdateDashboardStats();
        }

        private StatCardMini CreateStatCardMini(
            string heading,
            StatIconType iconType,
            Color surface,
            Color foreground,
            Color muted,
            Color accent)
        {
            StatCardMini card = new StatCardMini
            {
                Width = 180,
                Height = 32,
                Margin = Padding.Empty,
                AccentColor = accent,
                SurfaceColor = surface,
                ForegroundColor = foreground,
                MutedColor = muted,
                IconType = iconType
            };

            card.SetContent(heading, "0");
            return card;
        }

        private void UpdateDashboardStats()
        {
            if (_totalCard == null || _runningCard == null || _stoppedCard == null)
                return;

            int total = dataGridViewMain.Rows.Count;
            int running = AppManager.GetCount(false);
            int stopped = Math.Max(0, total - running);

            _totalCard.SetContent("CADASTRADOS", total.ToString());
            _runningCard.SetContent("ONLINE", running.ToString());
            _stoppedCard.SetContent("OFFLINE", stopped.ToString());

            if (_dashboardStatus != null)
            {
                _dashboardStatus.Text = running > 0
                    ? $"●  {running} servidor(es) online"
                    : "●  Sistema pronto";

                _dashboardStatus.ForeColor = running > 0
                    ? MuTheme.Gold
                    : MuTheme.TextMuted;
            }
        }

        private Control FindControlByName(Control parent, string name)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name == name)
                    return control;

                Control found = FindControlByName(control, name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private void CollectToolStrips(Control parent, List<ToolStrip> result)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is ToolStrip strip)
                    result.Add(strip);

                if (control.HasChildren)
                    CollectToolStrips(control, result);
            }
        }

        private void StyleToolStripForTheme(
            ToolStrip strip,
            Color panel,
            Color surface,
            Color foreground,
            Color accent)
        {
            if (strip == null)
                return;

            strip.BackColor = panel;
            strip.ForeColor = foreground;
            strip.Font = new Font("Segoe UI", 9F);
            strip.AutoSize = false;
            strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.RenderMode = ToolStripRenderMode.Professional;

            strip.Renderer = new MuColorTableRenderer(accent, panel, surface);

            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = foreground;
                item.Margin = new Padding(2);
                item.Padding = new Padding(5, 1, 5, 1);

                if (item is ToolStripButton button)
                {
                    button.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                    button.AutoToolTip = true;
                }

                if (item is ToolStripDropDownItem dropDown)
                {
                    StyleDropDownItems(dropDown.DropDownItems, surface, foreground);
                }
            }
        }

        private void StyleDropDownItems(
            ToolStripItemCollection items,
            Color surface,
            Color foreground)
        {
            foreach (ToolStripItem item in items)
            {
                item.BackColor = surface;
                item.ForeColor = foreground;
                item.Font = new Font("Segoe UI", 9F);
                item.Padding = new Padding(8, 5, 8, 5);

                if (item is ToolStripDropDownItem dropDown)
                {
                    StyleDropDownItems(dropDown.DropDownItems, surface, foreground);
                }
            }
        }

        private void StyleDashboardGrid(
            DataGridView grid,
            Color background,
            Color panel,
            Color surface,
            Color border,
            Color foreground,
            Color accent)
        {
            if (grid == null)
                return;

            grid.BackgroundColor = background;
            grid.BorderStyle = BorderStyle.None;
            grid.GridColor = border;
            grid.EnableHeadersVisualStyles = false;

            grid.ColumnHeadersDefaultCellStyle.BackColor = panel;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = accent;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = panel;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = accent;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            grid.ColumnHeadersHeight = 32;

            grid.DefaultCellStyle.BackColor = background;
            grid.DefaultCellStyle.ForeColor = foreground;
            grid.DefaultCellStyle.SelectionBackColor = MuTheme.GoldDim;
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.Padding = new Padding(4, 1, 4, 1);

            grid.AlternatingRowsDefaultCellStyle.BackColor = panel;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = foreground;

            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 30;

            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.AllowUserToResizeRows = false;
        }

        // =================================================================
        //  RENDERER customizado do ProfessionalColorTable (fix do footer)
        // =================================================================
        private sealed class MuColorTableRenderer : ToolStripProfessionalRenderer
        {
            private readonly Color _accent;

            public MuColorTableRenderer(Color accent, Color panel, Color surface)
                : base(new MuColorTable(accent, panel, surface))
            {
                _accent = accent;
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                // Customiza apenas a borda do StatusStrip (footer).
                // Para os demais (MenuStrip, toolbar), mantém o comportamento padrão.
                if (e.ToolStrip is StatusStrip)
                {
                    using (Pen pen = new Pen(Color.FromArgb(70, _accent)))
                    {
                        e.Graphics.DrawLine(
                            pen,
                            e.ToolStrip.ClientRectangle.Left,
                            0,
                            e.ToolStrip.ClientRectangle.Right,
                            0);
                    }

                    return;
                }

                base.OnRenderToolStripBorder(e);
            }
        }

        private sealed class MuColorTable : ProfessionalColorTable
        {
            private readonly Color _accent;
            private readonly Color _panel;
            private readonly Color _surface;

            public MuColorTable(Color accent, Color panel, Color surface)
            {
                _accent = accent;
                _panel = panel;
                _surface = surface;
                UseSystemColors = false;
            }

            public override Color MenuStripGradientBegin => _panel;
            public override Color MenuStripGradientEnd => _panel;

            public override Color ToolStripGradientBegin => _panel;
            public override Color ToolStripGradientMiddle => _panel;
            public override Color ToolStripGradientEnd => _panel;

            public override Color MenuItemSelected => _surface;
            public override Color MenuItemSelectedGradientBegin => _surface;
            public override Color MenuItemSelectedGradientEnd => _surface;

            public override Color MenuItemPressedGradientBegin => _accent;
            public override Color MenuItemPressedGradientEnd => _accent;

            public override Color MenuBorder => _surface;
            public override Color ToolStripBorder => _panel;

            public override Color SeparatorDark => _surface;
            public override Color SeparatorLight => _panel;

            public override Color ImageMarginGradientBegin => _panel;
            public override Color ImageMarginGradientMiddle => _panel;
            public override Color ImageMarginGradientEnd => _panel;

            public override Color ButtonSelectedHighlight => _surface;
            public override Color ButtonSelectedHighlightBorder => _accent;

            public override Color ButtonPressedHighlight => _accent;
            public override Color ButtonPressedHighlightBorder => _accent;
        }
    }
}