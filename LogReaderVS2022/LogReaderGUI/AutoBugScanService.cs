using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogReaderGUI;

/// <summary>
/// Serviço de varredura automática de bugs após login
/// Executa em intervalos configuráveis e gera logs de correções
/// </summary>
public class AutoBugScanService : IDisposable
{
    private readonly BugFixCollection _bugCollection;
    private readonly BugLogAnalyzer _bugLogAnalyzer;
    private System.Threading.Timer? _scanTimer;
    private bool _isRunning = false;
    private string _correctionLogPath = string.Empty;
    private int _scanIntervalSeconds = 300; // 5 minutos padrão

    public event EventHandler<ScanResultEventArgs>? ScanCompleted;
    public event EventHandler<ErrorEventArgs>? ScanError;

    public bool IsRunning => _isRunning;
    public int ScanIntervalSeconds
    {
        get => _scanIntervalSeconds;
        set
        {
            if (value < 30)
                throw new ArgumentException("Intervalo mínimo é 30 segundos.");
            _scanIntervalSeconds = value;
        }
    }

    public string CorrectionLogPath
    {
        get => _correctionLogPath;
        set => _correctionLogPath = value;
    }

    public AutoBugScanService(BugFixCollection bugCollection, BugLogAnalyzer bugLogAnalyzer)
    {
        _bugCollection = bugCollection ?? throw new ArgumentNullException(nameof(bugCollection));
        _bugLogAnalyzer = bugLogAnalyzer ?? throw new ArgumentNullException(nameof(bugLogAnalyzer));
    }

    /// <summary>
    /// Inicia a varredura automática após login
    /// </summary>
    public void StartAutoScan(string correctionLogPath, int intervalSeconds = 300)
    {
        if (_isRunning)
        {
            return;
        }

        _correctionLogPath = correctionLogPath;
        _scanIntervalSeconds = Math.Max(30, intervalSeconds);

        _scanTimer = new System.Threading.Timer(async _ => await PerformScanAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(_scanIntervalSeconds));
        _isRunning = true;
    }

    /// <summary>
    /// Para a varredura automática
    /// </summary>
    public void StopAutoScan()
    {
        if (!_isRunning)
        {
            return;
        }

        _scanTimer?.Dispose();
        _scanTimer = null;
        _isRunning = false;
    }

    /// <summary>
    /// Executa uma varredura imediata
    /// </summary>
    public async Task<ScanResult> ExecuteScanAsync(string logFilePath)
    {
        try
        {
            if (!File.Exists(logFilePath))
            {
                return new ScanResult { Success = false, ErrorMessage = "Arquivo de log não encontrado." };
            }

            var logContent = File.ReadAllText(logFilePath, Encoding.UTF8);
            var analysisResult = _bugLogAnalyzer.AnalyzeLog(logContent);

            var scanResult = new ScanResult
            {
                Success = true,
                Timestamp = DateTime.Now,
                LogFilePath = logFilePath,
                BugsDetected = analysisResult.UniqueBugsCount,
                TotalOccurrences = analysisResult.TotalMatches,
                MatchedBugs = analysisResult.MatchedBugs
            };

            // Gerar log de correções
            if (!string.IsNullOrWhiteSpace(_correctionLogPath))
            {
                AppendCorrectionLog(scanResult);
            }

            return scanResult;
        }
        catch (Exception ex)
        {
            return new ScanResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Executa varredura periódica (chamado pelo timer)
    /// </summary>
    private async Task PerformScanAsync()
    {
        try
        {
            var logPaths = FindLogFiles();
            var results = new List<ScanResult>();

            foreach (var logPath in logPaths)
            {
                var result = await ExecuteScanAsync(logPath);
                if (result.Success)
                {
                    results.Add(result);
                }
            }

            ScanCompleted?.Invoke(this, new ScanResultEventArgs(results));
        }
        catch (Exception ex)
        {
            ScanError?.Invoke(this, new ErrorEventArgs(ex));
        }
    }

    /// <summary>
    /// Localiza arquivos de log para varredura
    /// </summary>
    private List<string> FindLogFiles()
    {
        var logFiles = new List<string>();
        var clientDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Client_2");

        var candidates = new[]
        {
            Path.Combine(clientDir, "MuError.log"),
            Path.Combine(clientDir, "MuRuntime.log"),
            Path.Combine(Environment.CurrentDirectory, "MuError.log"),
            Path.Combine(Environment.CurrentDirectory, "MuRuntime.log")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath) && !logFiles.Contains(fullPath))
            {
                logFiles.Add(fullPath);
            }
        }

        return logFiles;
    }

    /// <summary>
    /// Anexa resultado da varredura ao log de correções
    /// </summary>
    private void AppendCorrectionLog(ScanResult result)
    {
        try
        {
            var logDir = Path.GetDirectoryName(_correctionLogPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var logEntry = GenerateLogEntry(result);
            File.AppendAllText(_correctionLogPath, logEntry, Encoding.UTF8);
        }
        catch
        {
            // Ignorar erros de escrita
        }
    }

    /// <summary>
    /// Gera entrada para o log de correções
    /// </summary>
    private string GenerateLogEntry(ScanResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"[{result.Timestamp:yyyy-MM-dd HH:mm:ss}] VARREDURA DE CORREÇÕES");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"Arquivo: {Path.GetFileName(result.LogFilePath)}");
        sb.AppendLine($"Bugs únicos: {result.BugsDetected}");
        sb.AppendLine($"Total de ocorrências: {result.TotalOccurrences}");

        if (result.MatchedBugs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Bugs Detectados:");

            var groupedByBug = new Dictionary<int, List<BugMatch>>();
            foreach (var match in result.MatchedBugs)
            {
                if (!groupedByBug.ContainsKey(match.Bug.Id))
                {
                    groupedByBug[match.Bug.Id] = new List<BugMatch>();
                }
                groupedByBug[match.Bug.Id].Add(match);
            }

            foreach (var group in groupedByBug)
            {
                sb.AppendLine($"  • #{group.Key}: {group.Value[0].Bug.Description}");
                sb.AppendLine($"    Ocorrências: {group.Value.Count}");
                sb.AppendLine($"    Confiança: {(group.Value[0].Confidence * 100):F1}%");
            }
        }
        else
        {
            sb.AppendLine("✓ Nenhum bug detectado.");
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public void Dispose()
    {
        StopAutoScan();
        _scanTimer?.Dispose();
    }
}

/// <summary>
/// Resultado de uma varredura de bugs
/// </summary>
public class ScanResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public string? LogFilePath { get; set; }
    public int BugsDetected { get; set; }
    public int TotalOccurrences { get; set; }
    public List<BugMatch> MatchedBugs { get; set; } = new();

    public override string ToString()
    {
        if (!Success)
        {
            return $"[ERRO] {ErrorMessage}";
        }

        return $"[{Timestamp:HH:mm:ss}] {BugsDetected} bug(s) - {TotalOccurrences} ocorrência(s)";
    }
}

/// <summary>
/// Argumentos de evento para resultado de varredura
/// </summary>
public class ScanResultEventArgs : EventArgs
{
    public List<ScanResult> Results { get; }

    public ScanResultEventArgs(List<ScanResult> results)
    {
        Results = results ?? new List<ScanResult>();
    }

    public int TotalBugsFound => Results.FindAll(r => r.Success).Sum(r => r.BugsDetected);
    public int TotalOccurrences => Results.FindAll(r => r.Success).Sum(r => r.TotalOccurrences);
}

/// <summary>
/// Argumentos de evento para erros de varredura
/// </summary>
public class ErrorEventArgs : EventArgs
{
    public Exception Exception { get; }

    public ErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
