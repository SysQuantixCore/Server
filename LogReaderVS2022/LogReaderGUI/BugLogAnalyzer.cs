using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LogReaderGUI;

/// <summary>
/// Analisa logs procurando por padrões e erros relacionados aos bugs conhecidos
/// </summary>
public class BugLogAnalyzer
{
    private readonly BugFixCollection _bugCollection;

    public BugLogAnalyzer(BugFixCollection bugCollection)
    {
        _bugCollection = bugCollection ?? throw new ArgumentNullException(nameof(bugCollection));
    }

    /// <summary>
    /// Analisa um log e retorna bugs detectados
    /// </summary>
    public BugAnalysisResult AnalyzeLog(string logContent)
    {
        var result = new BugAnalysisResult();

        if (string.IsNullOrWhiteSpace(logContent))
        {
            return result;
        }

        using var reader = new StringReader(logContent);
        string? line;
        var lineNumber = 0;

        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            var lowerLine = line.ToLowerInvariant();

            // Detecta erros conhecidos
            foreach (var bug in _bugCollection.Bugs)
            {
                if (MatchesBug(lowerLine, bug))
                {
                    result.MatchedBugs.Add(new BugMatch
                    {
                        Bug = bug,
                        LineNumber = lineNumber,
                        LineContent = line,
                        Confidence = CalculateConfidence(lowerLine, bug)
                    });
                }
            }

            // Detecta padrões de erro genéricos
            var errorPattern = ExtractErrorPattern(line);
            if (!string.IsNullOrWhiteSpace(errorPattern))
            {
                result.GenericErrors.Add(new ErrorOccurrence
                {
                    LineNumber = lineNumber,
                    Pattern = errorPattern,
                    FullLine = line
                });
            }
        }

        // Deduplica bugs encontrados
        var uniqueBugIds = result.MatchedBugs.Select(m => m.Bug.Id).Distinct().ToList();
        result.UniqueBugsCount = uniqueBugIds.Count;

        return result;
    }

    /// <summary>
    /// Analisa um arquivo de log
    /// </summary>
    public BugAnalysisResult AnalyzeLogFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Arquivo de log não encontrado: {filePath}");
        }

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return AnalyzeLog(content);
    }

    /// <summary>
    /// Verifica se uma linha de log corresponde a um bug conhecido
    /// </summary>
    private bool MatchesBug(string logLine, BugFixEntry bug)
    {
        var bugKeywords = bug.Keywords.Where(k => k.Length > 2).ToList();
        if (bugKeywords.Count == 0)
        {
            return false;
        }

        // Requer pelo menos 1 palavra-chave para match
        return bugKeywords.Any(keyword => logLine.Contains(keyword, StringComparison.Ordinal));
    }

    /// <summary>
    /// Calcula confiança do match (0.0 a 1.0)
    /// </summary>
    private double CalculateConfidence(string logLine, BugFixEntry bug)
    {
        var matchedKeywords = bug.Keywords.Count(k => logLine.Contains(k, StringComparison.Ordinal));
        var totalKeywords = bug.Keywords.Count;

        if (totalKeywords == 0)
        {
            return 0.0;
        }

        var keywordScore = Math.Min(1.0, (double)matchedKeywords / totalKeywords);

        // Bônus se o ID do bug aparece no log
        var bugIdMatch = logLine.Contains($"bug", StringComparison.OrdinalIgnoreCase) ? 0.1 : 0.0;

        return Math.Min(1.0, keywordScore + bugIdMatch);
    }

    /// <summary>
    /// Extrai padrões de erro genéricos
    /// </summary>
    private static string? ExtractErrorPattern(string line)
    {
        var lowerLine = line.ToLowerInvariant();

        var patterns = new Dictionary<string, string>
        {
            { "crash", "CRASH_DETECTED" },
            { "exception", "EXCEPTION" },
            { "access_violation", "ACCESS_VIOLATION" },
            { "stack overflow", "STACK_OVERFLOW" },
            { "fatal", "FATAL_ERROR" },
            { "failed", "OPERATION_FAILED" },
            { "could not", "OPERATION_FAILED" },
            { "database error", "DATABASE_ERROR" },
            { "timeout", "TIMEOUT" },
            { "memory", "MEMORY_ERROR" },
            { "token_error", "TOKEN_ERROR" },
            { "invalid parameter", "INVALID_PARAMETER" }
        };

        foreach (var pattern in patterns)
        {
            if (lowerLine.Contains(pattern.Key, StringComparison.Ordinal))
            {
                return pattern.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// Resultado da análise de um log
/// </summary>
public partial class BugAnalysisResult
{
    public List<BugMatch> MatchedBugs { get; } = new();
    public List<ErrorOccurrence> GenericErrors { get; } = new();
    public int UniqueBugsCount { get; set; }

    public int TotalMatches => MatchedBugs.Count + GenericErrors.Count;

    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== RELATÓRIO DE ANÁLISE DE LOG ===");
        sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Bugs únicos detectados: {UniqueBugsCount}");
        sb.AppendLine($"Total de ocorrências: {TotalMatches}");
        sb.AppendLine();

        if (MatchedBugs.Count > 0)
        {
            sb.AppendLine("--- BUGS CONHECIDOS DETECTADOS ---");
            var groupedByBug = MatchedBugs.GroupBy(m => m.Bug.Id);

            foreach (var group in groupedByBug)
            {
                var firstMatch = group.First();
                sb.AppendLine($"\n#{firstMatch.Bug.Id}: {firstMatch.Bug.Description}");
                sb.AppendLine($"  Categoria: {firstMatch.Bug.Category}");
                sb.AppendLine($"  Ocorrências: {group.Count()}");
                sb.AppendLine($"  Confiança média: {group.Average(m => m.Confidence):P}");

                foreach (var match in group.Take(3))
                {
                    sb.AppendLine($"    - Linha {match.LineNumber}: {match.LineContent.Substring(0, Math.Min(70, match.LineContent.Length))}");
                }

                if (group.Count() > 3)
                {
                    sb.AppendLine($"    ... e {group.Count() - 3} mais ocorrência(s)");
                }
            }
        }

        if (GenericErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- ERROS GENÉRICOS DETECTADOS ---");
            var groupedByPattern = GenericErrors.GroupBy(e => e.Pattern);

            foreach (var group in groupedByPattern)
            {
                sb.AppendLine($"\n{group.Key}: {group.Count()} ocorrência(s)");

                foreach (var error in group.Take(3))
                {
                    sb.AppendLine($"  - Linha {error.LineNumber}");
                }

                if (group.Count() > 3)
                {
                    sb.AppendLine($"  ... e {group.Count() - 3} mais ocorrência(s)");
                }
            }
        }

        if (TotalMatches == 0)
        {
            sb.AppendLine("\nNenhum erro ou bug detectado neste log.");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Representa um match entre um bug conhecido e uma linha de log
/// </summary>
public class BugMatch
{
    public BugFixEntry Bug { get; set; } = new();
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public double Confidence { get; set; }

    public override string ToString()
    {
        return $"Linha {LineNumber}: {Bug.Description} ({Confidence:P})";
    }
}

/// <summary>
/// Representa uma ocorrência de erro genérico
/// </summary>
public class ErrorOccurrence
{
    public int LineNumber { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string FullLine { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"Linha {LineNumber}: {Pattern}";
    }
}
