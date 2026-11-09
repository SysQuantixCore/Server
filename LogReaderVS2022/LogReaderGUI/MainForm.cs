using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LogReaderGUI
{
    public sealed class MainForm : Form
    {
        // Auto Bug Scan fields
        private AutoBugScanService? _autoScanService;
        private Button? _startAutoScanButton;
        private Button? _stopAutoScanButton;
        private TextBox? _scanIntervalBox;
        private Label? _scanStatusLabel;
    private readonly Panel _topPanel;
    private readonly TextBox _mainExePathBox;
    private readonly TextBox _pathBox;
    private readonly CheckBox _forceXorCheck;
    private readonly Button _openMainExeButton;
    private readonly Button _autoMainExeButton;
    private readonly Button _startMonitorButton;
    private readonly Button _stopMonitorButton;
    private readonly Button _useMuErrorButton;
    private readonly Button _useMuRuntimeButton;
    private readonly Button _openButton;
    private readonly Button _loadButton;
    private readonly Button _saveButton;
    private readonly Button _clearCriticalButton;
    private readonly Button _copyAlertButton;
    private readonly TextBox _contentBox;
    private readonly ListBox _criticalListBox;
    private readonly SplitContainer _mainSplit;
    private readonly Label _manualHintLabel;
    private readonly Label _statusLabel;
    private readonly ContextMenuStrip _criticalMenu;
    private readonly ContextMenuStrip _statusMenu;

    private readonly System.Windows.Forms.Timer _monitorTimer;
    private Process? _gameProcess;
    private string? _muErrorPath;
    private string? _muRuntimePath;
    private string _lastMuErrorText = string.Empty;
    private string _lastMuRuntimeText = string.Empty;
    private readonly HashSet<string> _reportedCriticalLines = new(StringComparer.Ordinal);

    private string _decodedText = string.Empty;
    private string _lastAlertText = string.Empty;

    // Bug Fix System
    private BugFixCollection? _bugFixCollection;
    private BugLogAnalyzer? _bugLogAnalyzer;
    private Button? _loadBugFixButton;
    private Button? _analyzeBugsButton;
    private Button? _generateReportButton;

    public MainForm()
    {
        Text = "MU Log Reader GUI (VS2022)";
        Width = 1200;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(1000, 640);

        _topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 260
        };

        var mainExeLabel = new Label
        {
            Text = "Main.exe:",
            Left = 12,
            Top = 14,
            Width = 110
        };

        _mainExePathBox = new TextBox
        {
            Left = 125,
            Top = 10,
            Width = 350,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _openMainExeButton = new Button
        {
            Text = "Selecionar Main.exe...",
            Left = 620,
            Top = 8,
            Width = 160,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _openMainExeButton.Click += OpenMainExeButton_Click;

        // Evento para verificar Fix-Correções.txt ao mudar o caminho do Main.exe
        _mainExePathBox.TextChanged += (s, e) =>
        {
            var mainPath = _mainExePathBox.Text.Trim();
            var fixPath = Path.Combine(Path.GetDirectoryName(mainPath) ?? "", "Fix-Correções.txt");
            if (File.Exists(fixPath))
            {
                ShowStatus($"✓ Fix-Correções.txt encontrado em: {fixPath}");
                _loadBugFixButton!.Enabled = true;
            }
            else
            {
                ShowStatus("✗ Fix-Correções.txt não encontrado na mesma pasta do Main.exe.");
                _loadBugFixButton!.Enabled = false;
            }
        };

        _autoMainExeButton = new Button
        {
            Text = "Auto Main.exe",
            Left = 785,
            Top = 8,
            Width = 120,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _autoMainExeButton.Click += AutoMainExeButton_Click;

        _startMonitorButton = new Button
        {
            Text = "Iniciar Jogo + Monitor",
            Left = 910,
            Top = 8,
            Width = 170,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _startMonitorButton.Click += StartMonitorButton_Click;

        _stopMonitorButton = new Button
        {
            Text = "Parar Monitor",
            Left = 1085,
            Top = 8,
            Width = 95,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Enabled = false
        };
        _stopMonitorButton.Click += StopMonitorButton_Click;

        var pathLabel = new Label
        {
            Text = "Arquivo de log:",
            Left = 12,
            Top = 46,
            Width = 110
        };

        _pathBox = new TextBox
        {
            Left = 125,
            Top = 42,
            Width = 300,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _openButton = new Button
        {
            Text = "Selecionar...",
            Left = 935,
            Top = 40,
            Width = 110,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _openButton.Click += OpenButton_Click;

        _loadButton = new Button
        {
            Text = "Ler / Decodificar",
            Left = 1052,
            Top = 40,
            Width = 125,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _loadButton.Click += LoadButton_Click;

        _forceXorCheck = new CheckBox
        {
            Text = "Forcar XOR",
            Left = 125,
            Top = 77,
            Width = 150
        };

        _useMuErrorButton = new Button
        {
            Text = "Usar MuError.log",
            Left = 280,
            Top = 73,
            Width = 140
        };
        _useMuErrorButton.Click += UseMuErrorButton_Click;

        _useMuRuntimeButton = new Button
        {
            Text = "Usar MuRuntime.log",
            Left = 427,
            Top = 73,
            Width = 150
        };
        _useMuRuntimeButton.Click += UseMuRuntimeButton_Click;

        _saveButton = new Button
        {
            Text = "Salvar como .txt",
            Left = 585,
            Top = 73,
            Width = 135
        };
        _saveButton.Click += SaveButton_Click;

        _clearCriticalButton = new Button
        {
            Text = "Limpar Criticos",
            Left = 728,
            Top = 73,
            Width = 130
        };
        _clearCriticalButton.Click += ClearCriticalButton_Click;

        // Bug Fix System Buttons
        _loadBugFixButton = new Button
        {
            Text = "Carregar Fix-Correções.txt",
            Left = 125,
            Top = 105,
            Width = 180
        };
        _loadBugFixButton.Click += LoadBugFixButton_Click;


        // Botão para iniciar verificação manual após entrar no jogo
        _analyzeBugsButton = new Button
        {
            Text = "Verificar Melhorias/Bugs",
            Left = 312,
            Top = 105,
            Width = 180
        };
        _analyzeBugsButton.Click += (s, e) =>
        {
            // Executa análise manual usando BugLogAnalyzer
            if (_bugFixCollection == null || _bugLogAnalyzer == null)
            {
                ShowStatus("Carregue Fix-Correções.txt primeiro.");
                return;
            }
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(_mainExePathBox.Text.Trim()) ?? "", "MuError.log");
                if (!File.Exists(logPath))
                {
                    ShowStatus("MuError.log não encontrado na pasta do Main.exe.");
                    return;
                }
                var results = _bugLogAnalyzer.AnalyzeLogFile(logPath);
                ShowStatus($"Análise concluída: {results.TotalMatches} bugs/melhorias detectados.");
                // Aqui pode-se abrir um painel ou exibir detalhes conforme desejado
            }
            catch (Exception ex)
            {
                ShowStatus($"Erro na análise: {ex.Message}");
            }
        };
        _analyzeBugsButton.Enabled = false;

        _generateReportButton = new Button
        {
            Text = "Gerar Relatório",
            Left = 449,
            Top = 105,
            Width = 130
        };
        _generateReportButton.Click += GenerateReportButton_Click;
        _generateReportButton.Enabled = false;
        // Auto Scan Controls
        var autoScanLabel = new Label
        {
            Text = "Varredura Auto (seg):",
            Left = 588,
            Top = 108,
            Width = 130
        };

        _scanIntervalBox = new TextBox
        {
            Text = "300",
            Left = 725,
            Top = 105,
            Width = 50
        };

        _startAutoScanButton = new Button
        {
            Text = "▶ Iniciar",
            Left = 783,
            Top = 105,
            Width = 75
        };
        _startAutoScanButton.Click += StartAutoScanButton_Click;
        _startAutoScanButton.Enabled = false;

        _stopAutoScanButton = new Button
        {
            Text = "⏹ Parar",
            Left = 865,
            Top = 105,
            Width = 75
        };
        _stopAutoScanButton.Click += StopAutoScanButton_Click;
        _stopAutoScanButton.Enabled = false;

        _scanStatusLabel = new Label
        {
            Text = "Pronto",
            Left = 947,
            Top = 108,
            Width = 100,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };


        _copyAlertButton = new Button
        {
            Text = "Copiar Alerta",
            Left = 1052,
            Top = 138,
            Width = 125,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _copyAlertButton.Click += CopyLastAlert_Click;

        _manualHintLabel = new Label
        {
            Left = 125,
            Top = 108,
            Width = 1048,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "Dica: monitor em tempo real; clique com botao direito nos eventos/status para copiar (Ctrl+C nos eventos selecionados)."
        };

        _statusLabel = new Label
        {
            Left = 12,
            Top = 142,
            Width = 1160,
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Text = "Pronto"
        };

        _topPanel.Controls.Add(mainExeLabel);
        _topPanel.Controls.Add(_mainExePathBox);
        _topPanel.Controls.Add(_openMainExeButton);
        _topPanel.Controls.Add(_autoMainExeButton);
        _topPanel.Controls.Add(_startMonitorButton);
        _topPanel.Controls.Add(_stopMonitorButton);
        _topPanel.Controls.Add(pathLabel);
        _topPanel.Controls.Add(_pathBox);
        _topPanel.Controls.Add(_openButton);
        _topPanel.Controls.Add(_loadButton);
        _topPanel.Controls.Add(_forceXorCheck);
        _topPanel.Controls.Add(_useMuErrorButton);
        _topPanel.Controls.Add(_useMuRuntimeButton);
        _topPanel.Controls.Add(_saveButton);
        _topPanel.Controls.Add(_clearCriticalButton);
        _topPanel.Controls.Add(_loadBugFixButton);
        _topPanel.Controls.Add(_analyzeBugsButton);
        _topPanel.Controls.Add(_generateReportButton);
        _topPanel.Controls.Add(_copyAlertButton);
        _topPanel.Controls.Add(_manualHintLabel);
        _topPanel.Controls.Add(_statusLabel);
    _topPanel.Controls.Add(autoScanLabel);
    _topPanel.Controls.Add(_scanIntervalBox);
    _topPanel.Controls.Add(_startAutoScanButton);
    _topPanel.Controls.Add(_stopAutoScanButton);
    _topPanel.Controls.Add(_scanStatusLabel);

        _contentBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new System.Drawing.Font("Consolas", 10.0f),
            ReadOnly = true
        };

        _criticalListBox = new ListBox
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
            Font = new System.Drawing.Font("Consolas", 9.0f),
            SelectionMode = SelectionMode.MultiExtended
        };
        _criticalListBox.KeyDown += CriticalListBox_KeyDown;
        _criticalListBox.MouseDown += CriticalListBox_MouseDown;

        _criticalMenu = new ContextMenuStrip();
        var copyCriticalSelectedItem = new ToolStripMenuItem("Copiar selecionado", null, CopySelectedCritical_Click);
        var copyCriticalAllItem = new ToolStripMenuItem("Copiar todos", null, CopyAllCritical_Click);
        _criticalMenu.Items.Add(copyCriticalSelectedItem);
        _criticalMenu.Items.Add(copyCriticalAllItem);
        _criticalListBox.ContextMenuStrip = _criticalMenu;

        _statusMenu = new ContextMenuStrip();
        var copyStatusItem = new ToolStripMenuItem("Copiar status atual", null, CopyStatus_Click);
        var copyAlertItem = new ToolStripMenuItem("Copiar ultimo alerta", null, CopyLastAlert_Click);
        _statusMenu.Items.Add(copyStatusItem);
        _statusMenu.Items.Add(copyAlertItem);
        _statusLabel.ContextMenuStrip = _statusMenu;

        var criticalPanel = new Panel
        {
            Dock = DockStyle.Fill
        };

        var criticalLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Text = "Eventos Criticos (tempo real)",
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        criticalPanel.Controls.Add(_criticalListBox);
        criticalPanel.Controls.Add(criticalLabel);

        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };

        _contentBox.Dock = DockStyle.Fill;
        _mainSplit.Panel1.Controls.Add(_contentBox);
        _mainSplit.Panel2.Controls.Add(criticalPanel);

        Controls.Add(_mainSplit);
        Controls.Add(_topPanel);

        _monitorTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };
        _monitorTimer.Tick += MonitorTimer_Tick;

        Resize += (_, _) => ReflowTopPanelLayout();
        Shown += (_, _) =>
        {
            _mainSplit.Panel1MinSize = 420;
            _mainSplit.Panel2MinSize = 240;
            ReflowTopPanelLayout();
            TryAutoFillMainExePath();
        };
    }

    private void ReflowTopPanelLayout()
    {
        const int leftMargin = 12;
        const int rightMargin = 12;
        const int gap = 8;

        var right = _topPanel.ClientSize.Width - rightMargin;

        _stopMonitorButton.Left = right - _stopMonitorButton.Width;
        _startMonitorButton.Left = _stopMonitorButton.Left - gap - _startMonitorButton.Width;
        _autoMainExeButton.Left = _startMonitorButton.Left - gap - _autoMainExeButton.Width;
        _openMainExeButton.Left = _autoMainExeButton.Left - gap - _openMainExeButton.Width;

        var mainPathLeft = _mainExePathBox.Left;
        var maxMainPathWidth = _openMainExeButton.Left - gap - mainPathLeft;
        _mainExePathBox.Width = Math.Max(220, maxMainPathWidth);

        _loadButton.Left = right - _loadButton.Width;
        _openButton.Left = _loadButton.Left - gap - _openButton.Width;

        var pathLeft = _pathBox.Left;
        var maxPathWidth = _openButton.Left - gap - pathLeft;
        _pathBox.Width = Math.Max(220, maxPathWidth);

        _statusLabel.Left = leftMargin;
        _copyAlertButton.Left = right - _copyAlertButton.Width;
        _statusLabel.Width = Math.Max(220, _copyAlertButton.Left - gap - leftMargin);

        _manualHintLabel.Left = _pathBox.Left;
        _manualHintLabel.Width = Math.Max(220, _topPanel.ClientSize.Width - _manualHintLabel.Left - rightMargin);

        UpdateMainSplitLayout();

        var clearBtnRight = _saveButton.Left + _saveButton.Width + gap + _clearCriticalButton.Width;
        if (clearBtnRight > _openButton.Left)
        {
            _clearCriticalButton.Visible = false;
        }
        else
        {
            _clearCriticalButton.Visible = true;
        }
    }

    private void UpdateMainSplitLayout()
    {
        var minDistance = _mainSplit.Panel1MinSize;
        var maxDistance = _mainSplit.Width - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth;

        // During early layout the control can be too small for min constraints.
        if (maxDistance < minDistance)
        {
            return;
        }

        var preferred = _mainSplit.Width - 320;
        var target = Math.Max(minDistance, Math.Min(maxDistance, preferred));

        if (_mainSplit.SplitterDistance != target)
        {
            _mainSplit.SplitterDistance = target;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopMonitoring();
        base.OnFormClosing(e);
    }

    private void OpenMainExeButton_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Selecionar Main.exe",
            Filter = "Main executavel (Main.exe)|Main.exe|Executavel (*.exe)|*.exe|Todos (*.*)|*.*"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _mainExePathBox.Text = ofd.FileName;
            TryAutoSetLogPathsFromMainExe(ofd.FileName);
        }
    }

    private void AutoMainExeButton_Click(object? sender, EventArgs e)
    {
        if (TryAutoFillMainExePath())
        {
            ShowStatus($"Main.exe localizado: {_mainExePathBox.Text}");
        }
        else
        {
            ShowStatus("Nao foi possivel localizar Main.exe automaticamente. Selecione manualmente.");
        }
    }

    private void StartMonitorButton_Click(object? sender, EventArgs e)
    {
        var mainExePath = _mainExePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(mainExePath) || !File.Exists(mainExePath))
        {
            ShowStatus("Informe um caminho valido para Main.exe.");
            return;
        }

        try
        {
            StopMonitoring();

            TryAutoSetLogPathsFromMainExe(mainExePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = mainExePath,
                WorkingDirectory = Path.GetDirectoryName(mainExePath) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            };

            _gameProcess = Process.Start(startInfo);
            if (_gameProcess != null)
            {
                _gameProcess.EnableRaisingEvents = true;
                _gameProcess.Exited += GameProcess_Exited;
            }

            _lastMuErrorText = string.Empty;
            _lastMuRuntimeText = string.Empty;
            _reportedCriticalLines.Clear();
            _monitorTimer.Start();

            _startMonitorButton.Enabled = false;
            _stopMonitorButton.Enabled = true;

            ShowStatus("Monitor ativo. Aplicativo continuara aberto mesmo se o client fechar/crashar.");
        }
        catch (Exception ex)
        {
            ShowStatus($"Erro ao iniciar monitor: {ex.Message}");
        }
    }

    private void StopMonitorButton_Click(object? sender, EventArgs e)
    {
        StopMonitoring();
        ShowStatus("Monitor parado.");
    }

    private void StopMonitoring()
    {
        _monitorTimer.Stop();

        if (_gameProcess != null)
        {
            try
            {
                _gameProcess.Exited -= GameProcess_Exited;
            }
            catch
            {
                // Ignore detach failures.
            }

            _gameProcess.Dispose();
            _gameProcess = null;
        }

        _startMonitorButton.Enabled = true;
        _stopMonitorButton.Enabled = false;
    }

    private void CriticalListBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C)
        {
            CopySelectedCriticalLinesToClipboard();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void CriticalListBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var index = _criticalListBox.IndexFromPoint(e.Location);
        if (index >= 0 && !_criticalListBox.SelectedIndices.Contains(index))
        {
            _criticalListBox.SelectedIndices.Clear();
            _criticalListBox.SelectedIndex = index;
        }
    }

    private void CopySelectedCritical_Click(object? sender, EventArgs e)
    {
        CopySelectedCriticalLinesToClipboard();
    }

    private void CopyAllCritical_Click(object? sender, EventArgs e)
    {
        if (_criticalListBox.Items.Count == 0)
        {
            ShowStatus("Nao ha eventos criticos para copiar.");
            return;
        }

        var lines = _criticalListBox.Items.Cast<object>().Select(static item => item?.ToString() ?? string.Empty);
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        ShowStatus($"{_criticalListBox.Items.Count} evento(s) critico(s) copiado(s) para a area de transferencia.");
    }

    private void CopyStatus_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_statusLabel.Text))
        {
            ShowStatus("Nao ha mensagem de status para copiar.");
            return;
        }

        Clipboard.SetText(_statusLabel.Text);
        ShowStatus("Mensagem de status copiada para a area de transferencia.");
    }

    private void CopyLastAlert_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastAlertText))
        {
            ShowStatus("Nenhum alerta registrado para copiar.");
            return;
        }

        Clipboard.SetText(_lastAlertText);
        ShowStatus("Ultimo alerta copiado para a area de transferencia.");
    }

    private void ClearCriticalButton_Click(object? sender, EventArgs e)
    {
        _criticalListBox.Items.Clear();
        _reportedCriticalLines.Clear();
        ShowStatus("Lista de eventos criticos limpa.");
    }

    private void GameProcess_Exited(object? sender, EventArgs e)
    {
        BeginInvoke((Action)(() =>
        {
            var code = -1;
            try
            {
                if (_gameProcess != null)
                {
                    code = _gameProcess.ExitCode;
                }
            }
            catch
            {
                // Ignore.
            }

            ShowStatus($"Main.exe encerrou com codigo {code}. Monitor continua ativo para leitura dos logs.");
            _startMonitorButton.Enabled = true;
        }));
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var changed = false;
            var critical = new List<string>();

            if (!string.IsNullOrWhiteSpace(_muErrorPath) && File.Exists(_muErrorPath))
            {
                var text = ReadLogText(_muErrorPath, true);
                if (!string.Equals(text, _lastMuErrorText, StringComparison.Ordinal))
                {
                    var delta = GetDelta(_lastMuErrorText, text);
                    _lastMuErrorText = text;
                    changed = true;
                    _decodedText = text;
                    _contentBox.Text = text;

                    foreach (var line in ExtractCriticalLines(delta))
                    {
                        if (_reportedCriticalLines.Add(line))
                        {
                            critical.Add(line);
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(_muRuntimePath) && File.Exists(_muRuntimePath))
            {
                var text = ReadLogText(_muRuntimePath, false);
                if (!string.Equals(text, _lastMuRuntimeText, StringComparison.Ordinal))
                {
                    var delta = GetDelta(_lastMuRuntimeText, text);
                    _lastMuRuntimeText = text;

                    foreach (var line in ExtractCriticalLines(delta))
                    {
                        if (_reportedCriticalLines.Add(line))
                        {
                            critical.Add(line);
                        }
                    }

                    if (!changed)
                    {
                        _decodedText = text;
                        _contentBox.Text = text;
                        changed = true;
                    }
                }
            }

            if (critical.Count > 0)
            {
                foreach (var line in critical)
                {
                    AddCriticalEvent(line);
                }

                ShowStatus("ALERTA: erro(s) critico(s) detectado(s): " + string.Join(" | ", critical.Take(2)));
            }
            else if (changed)
            {
                ShowStatus("Monitor ativo. Logs atualizados em tempo real.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Erro no monitor: {ex.Message}");
        }
    }

    private void UseMuErrorButton_Click(object? sender, EventArgs e)
    {
        var foundPath = FindLikelyClientLogPath("MuError.log", _mainExePathBox.Text.Trim());
        if (foundPath is null)
        {
            ShowStatus("Nao foi possivel localizar MuError.log automaticamente. Informe o caminho manualmente.");
            return;
        }

        _pathBox.Text = foundPath;
        ShowStatus($"Caminho preenchido: {foundPath}");
    }

    private void UseMuRuntimeButton_Click(object? sender, EventArgs e)
    {
        var foundPath = FindLikelyClientLogPath("MuRuntime.log", _mainExePathBox.Text.Trim());
        if (foundPath is null)
        {
            ShowStatus("Nao foi possivel localizar MuRuntime.log automaticamente. Informe o caminho manualmente.");
            return;
        }

        _pathBox.Text = foundPath;
        ShowStatus($"Caminho preenchido: {foundPath}");
    }

    private void OpenButton_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Selecionar log",
            Filter = "Logs (*.log;*.txt)|*.log;*.txt|Todos (*.*)|*.*"
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = ofd.FileName;
        }
    }

    private void LoadButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var path = _pathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowStatus("Informe o caminho do arquivo.");
                return;
            }

            if (!File.Exists(path))
            {
                ShowStatus("Arquivo nao encontrado.");
                return;
            }

            var bytes = File.ReadAllBytes(path);
            var decodeXor = ShouldDecodeXor(path, _forceXorCheck.Checked);
            if (decodeXor)
            {
                XorConvertBuffer(bytes, 0);
            }

            _decodedText = Encoding.Default.GetString(bytes);
            _contentBox.Text = _decodedText;

            ShowStatus($"OK - {(decodeXor ? "XOR" : "texto")}: {path}");
        }
        catch (Exception ex)
        {
            ShowStatus($"Erro: {ex.Message}");
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_decodedText))
            {
                ShowStatus("Nada para salvar. Leia um log primeiro.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Salvar texto decodificado",
                Filter = "Texto (*.txt)|*.txt|Log (*.log)|*.log|Todos (*.*)|*.*",
                FileName = "log_decodificado.txt"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            File.WriteAllText(sfd.FileName, _decodedText, Encoding.UTF8);
            ShowStatus($"Arquivo salvo: {sfd.FileName}");
        }
        catch (Exception ex)
        {
            ShowStatus($"Erro ao salvar: {ex.Message}");
        }
    }

    private void ShowStatus(string text)
    {
        _statusLabel.Text = text;

        if (text.StartsWith("ALERTA:", StringComparison.OrdinalIgnoreCase))
        {
            _lastAlertText = text;
        }
    }

    private void CopySelectedCriticalLinesToClipboard()
    {
        if (_criticalListBox.SelectedItems.Count == 0)
        {
            ShowStatus("Selecione ao menos um evento critico para copiar.");
            return;
        }

        var lines = _criticalListBox.SelectedItems.Cast<object>().Select(static item => item?.ToString() ?? string.Empty);
        Clipboard.SetText(string.Join(Environment.NewLine, lines));
        ShowStatus($"{_criticalListBox.SelectedItems.Count} evento(s) critico(s) copiado(s) para a area de transferencia.");
    }

    private static bool ShouldDecodeXor(string path, bool forceXor)
    {
        if (forceXor)
        {
            return true;
        }

        return path.EndsWith("MuError.log", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryAutoFillMainExePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Client_2", "Main.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Client_2", "Main.exe"),
            Path.Combine(Environment.CurrentDirectory, "Client_2", "Main.exe"),
            Path.Combine(Environment.CurrentDirectory, "Main.exe")
        }
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _mainExePathBox.Text = candidate;
                TryAutoSetLogPathsFromMainExe(candidate);
                return true;
            }
        }

        return false;
    }

    private void TryAutoSetLogPathsFromMainExe(string mainExePath)
    {
        var dir = Path.GetDirectoryName(mainExePath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return;
        }

        var muError = Path.Combine(dir, "MuError.log");
        var muRuntime = Path.Combine(dir, "MuRuntime.log");

        _muErrorPath = muError;
        _muRuntimePath = muRuntime;

        if (_pathBox.Text.Length == 0 || _pathBox.Text.EndsWith("MuError.log", StringComparison.OrdinalIgnoreCase) || _pathBox.Text.EndsWith("MuRuntime.log", StringComparison.OrdinalIgnoreCase))
        {
            _pathBox.Text = muError;
        }
    }

    private static string? FindLikelyClientLogPath(string logFileName, string mainExePath)
    {
        var clientDir = string.Empty;
        if (!string.IsNullOrWhiteSpace(mainExePath) && File.Exists(mainExePath))
        {
            clientDir = Path.GetDirectoryName(mainExePath) ?? string.Empty;
        }

        var candidates = new[]
        {
            clientDir.Length > 0 ? Path.Combine(clientDir, logFileName) : string.Empty,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Client_2", logFileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Client_2", logFileName),
            Path.Combine(Environment.CurrentDirectory, "Client_2", logFileName),
            Path.Combine(Environment.CurrentDirectory, logFileName)
        }
        .Where(static p => !string.IsNullOrWhiteSpace(p))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ReadLogText(string path, bool decodeXor)
    {
        var bytes = File.ReadAllBytes(path);
        if (decodeXor)
        {
            XorConvertBuffer(bytes, 0);
        }

        return Encoding.Default.GetString(bytes);
    }

    private static string GetDelta(string oldText, string newText)
    {
        if (string.IsNullOrEmpty(oldText))
        {
            return newText;
        }

        if (newText.Length < oldText.Length)
        {
            return newText;
        }

        if (newText.StartsWith(oldText, StringComparison.Ordinal))
        {
            return newText.Substring(oldText.Length);
        }

        return newText;
    }

    private static IEnumerable<string> ExtractCriticalLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        using var reader = new StringReader(text);
        while (true)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                yield break;
            }

            var normalizedLine = NormalizeCriticalLine(line);
            if (normalizedLine.Length == 0)
            {
                continue;
            }

            var lower = line.ToLowerInvariant();
            var isCritical = lower.Contains("crash") ||
                             lower.Contains("exception") ||
                             lower.Contains("access_violation") ||
                             lower.Contains("stack") ||
                             lower.Contains("fatal") ||
                             lower.Contains("error") ||
                             lower.Contains("failed") ||
                             lower.Contains("could not") ||
                             lower.Contains("wsa") ||
                             lower.Contains("database") ||
                             lower.Contains("mem_script") ||
                             lower.Contains("token_error") ||
                             lower.Contains("invalid parameter") ||
                             lower.Contains("purecall");

            if (isCritical)
            {
                yield return normalizedLine;
            }
        }
    }

    private void AddCriticalEvent(string line)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var item = $"[{timestamp}] {NormalizeCriticalLine(line)}";

        _criticalListBox.Items.Add(item);
        if (_criticalListBox.Items.Count > 1500)
        {
            _criticalListBox.Items.RemoveAt(0);
        }

        _criticalListBox.TopIndex = _criticalListBox.Items.Count - 1;
    }

    private static readonly byte[] XorKey =
    {
        0x7C, 0xBD, 0x81, 0x9F, 0x3D, 0x93, 0xE2, 0x56,
        0x2A, 0x73, 0xD2, 0x3E, 0xF2, 0x83, 0x95, 0xBF
    };

    private static int XorConvertBuffer(byte[] buffer, int keyIndex)
    {
        var sizeLeft = buffer.Length;
        var current = 0;

        var convertSize = Math.Min((16 - keyIndex) % 16, buffer.Length);
        for (var i = 0; i < convertSize; i++)
        {
            buffer[current + i] ^= XorKey[i + keyIndex];
        }

        current += convertSize;
        sizeLeft -= convertSize;

        if (sizeLeft <= 0)
        {
            return convertSize + keyIndex;
        }

        while (sizeLeft >= 16)
        {
            convertSize = 16;
            for (var i = 0; i < convertSize; i++)
            {
                buffer[current + i] ^= XorKey[i];
            }

            current += convertSize;
            sizeLeft -= convertSize;
        }

        convertSize = sizeLeft;
        for (var i = 0; i < convertSize; i++)
        {
            buffer[current + i] ^= XorKey[i];
        }

        return convertSize;
    }

    #region Bug Fix System Methods

    private void LoadBugFixButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var fixFilePath = FindBugFixFile();

            if (string.IsNullOrEmpty(fixFilePath))
            {
                using var ofd = new OpenFileDialog
                {
                    Title = "Selecionar Fix-Correções.txt",
                    Filter = "Fix-Correções (Fix-Correções.txt)|Fix-Correções.txt|Texto (*.txt)|*.txt|Todos (*.*)|*.*"
                };

                if (ofd.ShowDialog(this) != DialogResult.OK)
                {
                    ShowStatus("Carregamento cancelado.");
                    return;
                }

                fixFilePath = ofd.FileName;
            }

            _bugFixCollection = new BugFixCollection();
            _bugFixCollection.LoadFromFile(fixFilePath);
            _bugLogAnalyzer = new BugLogAnalyzer(_bugFixCollection);

            _analyzeBugsButton!.Enabled = true;
            _generateReportButton!.Enabled = true;
            _startAutoScanButton!.Enabled = true;

            ShowStatus($"✓ Fix-Correções carregado: {Path.GetFileName(fixFilePath)} ({_bugFixCollection.Bugs.Count} bugs)");
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ Erro ao carregar Fix-Correções: {ex.Message}");
        }
    }

    private void AnalyzeBugsButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_bugFixCollection == null || _bugLogAnalyzer == null)
            {
                ShowStatus("Carregue Fix-Correções.txt primeiro.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_decodedText))
            {
                ShowStatus("Carregue um arquivo de log primeiro.");
                return;
            }

            var result = _bugLogAnalyzer.AnalyzeLog(_decodedText);

            var reportContent = result.GenerateReport();
            _contentBox.Text = reportContent;

            _lastAlertText = $"Análise completa: {result.UniqueBugsCount} bug(s) único(s) detectado(s), {result.TotalMatches} total de ocorrência(s).";
            ShowStatus($"✓ {_lastAlertText}");

            // Limpar lista de críticos e adicionar bugs encontrados
            _criticalListBox.Items.Clear();
            _reportedCriticalLines.Clear();

            var groupedByBug = result.MatchedBugs.GroupBy(m => m.Bug.Id);
            foreach (var group in groupedByBug)
            {
                var firstMatch = group.First();
                var item = $"[BUG #{firstMatch.Bug.Id}] {firstMatch.Bug.Description} ({group.Count()} ocorrência(s))";
                _criticalListBox.Items.Add(item);

                // Adicionar detalhes de cada ocorrência
                foreach (var match in group.Take(2))
                {
                    var detail = $"  └─ Linha {match.LineNumber}: {match.LineContent.Substring(0, Math.Min(50, match.LineContent.Length))}";
                    _criticalListBox.Items.Add(detail);
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ Erro ao analisar bugs: {ex.Message}");
        }
    }

    private void GenerateReportButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_bugFixCollection == null)
            {
                ShowStatus("Carregue Fix-Correções.txt primeiro.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Salvar Relatório de Bugs",
                Filter = "HTML (*.html)|*.html|Markdown (*.md)|*.md|Texto (*.txt)|*.txt",
                FileName = $"bug_report_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
            {
                ShowStatus("Geração de relatório cancelada.");
                return;
            }

            var report = new BugFixReport(_bugFixCollection);

            // Adicionar análise do log atual se disponível
            if (!string.IsNullOrWhiteSpace(_decodedText) && _bugLogAnalyzer != null)
            {
                var analysisResult = _bugLogAnalyzer.AnalyzeLog(_decodedText);
                report.AddAnalysisResult(analysisResult, _pathBox.Text);
            }

            var format = sfd.FileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? ReportFormat.Html :
                         sfd.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? ReportFormat.Markdown :
                         ReportFormat.Text;

            report.SaveReport(sfd.FileName, format);
            ShowStatus($"✓ Relatório salvo: {sfd.FileName}");

            // Abrir arquivo no editor padrão se for HTML
            if (format == ReportFormat.Html)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
                catch
                {
                    // Ignorar se não conseguir abrir o arquivo
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ Erro ao gerar relatório: {ex.Message}");
        }
    }

    private string? FindBugFixFile()
    {
        var candidates = new[]
        {
            "Fix-Correções.txt",
            Path.Combine(Path.GetDirectoryName(_mainExePathBox.Text.Trim()) ?? "", "Fix-Correções.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fix-Correções.txt"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fix-Correções.txt"),
            Path.Combine(Environment.CurrentDirectory, "Fix-Correções.txt"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "Fix-Correções.txt"),
            Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "Fix-Correções.txt")
        }
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    #endregion

    #region Auto Bug Scan Methods

    private void StartAutoScanButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_bugFixCollection == null || _bugLogAnalyzer == null)
            {
                ShowStatus("Carregue Fix-Correções.txt primeiro.");
                return;
            }

            if (!int.TryParse(_scanIntervalBox?.Text ?? "300", out var interval) || interval < 30)
            {
                ShowStatus("Intervalo deve ser >= 30 segundos.");
                return;
            }

            var correctionLogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Client_2", "MuCorrections.log");
            correctionLogPath = Path.GetFullPath(correctionLogPath);

            _autoScanService = new AutoBugScanService(_bugFixCollection, _bugLogAnalyzer);
            _autoScanService.ScanCompleted += AutoScanService_ScanCompleted;
            _autoScanService.ScanError += AutoScanService_ScanError;

            _autoScanService.StartAutoScan(correctionLogPath, interval);

            _startAutoScanButton!.Enabled = false;
            _stopAutoScanButton!.Enabled = true;
            _scanIntervalBox!.Enabled = false;
            _loadBugFixButton!.Enabled = false;

            ShowStatus($"✓ Varredura automática iniciada (intervalo: {interval}s) -> {correctionLogPath}");
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ Erro ao iniciar varredura: {ex.Message}");
        }
    }

    private void StopAutoScanButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _autoScanService?.StopAutoScan();
            _autoScanService?.Dispose();
            _autoScanService = null;

            _startAutoScanButton!.Enabled = true;
            _stopAutoScanButton!.Enabled = false;
            _scanIntervalBox!.Enabled = true;
            _loadBugFixButton!.Enabled = true;

            ShowStatus("✓ Varredura automática parada.");
        }
        catch (Exception ex)
        {
            ShowStatus($"✗ Erro ao parar varredura: {ex.Message}");
        }
    }

    private void AutoScanService_ScanCompleted(object? sender, ScanResultEventArgs e)
    {
        BeginInvoke((Action)(() =>
        {
            if (e.Results.Count == 0)
            {
                return;
            }

            var message = $"Varredura: {e.TotalBugsFound} bug(s) - {e.TotalOccurrences} ocorrência(s)";
            _scanStatusLabel!.Text = message;

            var successfulResults = e.Results.FindAll(r => r.Success && r.BugsDetected > 0);
            if (successfulResults.Count > 0)
            {
                _lastAlertText = message;
                ShowStatus($"⚠ {message}");

                // Atualizar lista de críticos
                foreach (var result in successfulResults)
                {
                    foreach (var match in result.MatchedBugs)
                    {
                        if (_reportedCriticalLines.Add($"{match.Bug.Id}_{match.LineNumber}"))
                        {
                            AddCriticalEvent($"[AUTO-SCAN] Bug #{match.Bug.Id}: {match.LineContent.Substring(0, Math.Min(50, match.LineContent.Length))}");
                        }
                    }
                }
            }
        }));
    }

    private void AutoScanService_ScanError(object? sender, ErrorEventArgs e)
    {
        BeginInvoke((Action)(() =>
        {
            ShowStatus($"✗ Erro na varredura automática: {e.Exception.Message}");
        }));
    }

    #endregion

    private static string NormalizeCriticalLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var cleaned = line.Replace('\0', ' ').Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        return cleaned;
    }
    }
}
