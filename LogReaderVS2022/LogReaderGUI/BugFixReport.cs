using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LogReaderGUI;

/// <summary>
/// Gera relatórios estruturados sobre bugs e correções
/// </summary>
public class BugFixReport
{
    private readonly BugFixCollection _bugCollection;
    private readonly List<BugAnalysisResult> _analysisResults = new();

    public BugFixReport(BugFixCollection bugCollection)
    {
        _bugCollection = bugCollection ?? throw new ArgumentNullException(nameof(bugCollection));
    }

    /// <summary>
    /// Adiciona um resultado de análise ao relatório
    /// </summary>
    public void AddAnalysisResult(BugAnalysisResult result, string sourceFile)
    {
        result.SourceFile = sourceFile;
        _analysisResults.Add(result);
    }

    /// <summary>
    /// Gera relatório em formato HTML
    /// </summary>
    public string GenerateHtmlReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Relatório de Bugs - Log Reader</title>");
        sb.AppendLine(GetCssStyles());
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        AppendHtmlHeader(sb);
        AppendHtmlSummary(sb);
        AppendHtmlBugsList(sb);
        AppendHtmlAnalysisDetails(sb);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Gera relatório em formato markdown
    /// </summary>
    public string GenerateMarkdownReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Relatório de Análise de Bugs - Log Reader");
        sb.AppendLine($"**Data:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        AppendMarkdownSummary(sb);
        AppendMarkdownBugsList(sb);
        AppendMarkdownAnalysisDetails(sb);

        return sb.ToString();
    }

    /// <summary>
    /// Gera relatório em formato texto simples
    /// </summary>
    public string GenerateTextReport()
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("        RELATÓRIO DE ANÁLISE DE BUGS - LOG READER");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Versão: 1.0");
        sb.AppendLine();

        AppendTextSummary(sb);
        AppendTextBugsList(sb);
        AppendTextAnalysisDetails(sb);

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("Fim do Relatório");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// Salva o relatório em arquivo
    /// </summary>
    public void SaveReport(string filePath, ReportFormat format)
    {
        var content = format switch
        {
            ReportFormat.Html => GenerateHtmlReport(),
            ReportFormat.Markdown => GenerateMarkdownReport(),
            ReportFormat.Text => GenerateTextReport(),
            _ => GenerateTextReport()
        };

        File.WriteAllText(filePath, content, Encoding.UTF8);
    }

    #region Métodos Privados

    private string GetCssStyles()
    {
        return @"
<style>
    body {
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        max-width: 1200px;
        margin: 0 auto;
        padding: 20px;
        background-color: #f5f5f5;
        color: #333;
    }
    .header {
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        color: white;
        padding: 30px;
        border-radius: 8px;
        margin-bottom: 20px;
    }
    .summary {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 15px;
        margin-bottom: 20px;
    }
    .stat-card {
        background: white;
        padding: 20px;
        border-radius: 8px;
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        border-left: 4px solid #667eea;
    }
    .stat-number {
        font-size: 28px;
        font-weight: bold;
        color: #667eea;
    }
    .stat-label {
        color: #666;
        font-size: 12px;
        text-transform: uppercase;
        margin-top: 5px;
    }
    .section {
        background: white;
        padding: 20px;
        border-radius: 8px;
        margin-bottom: 20px;
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }
    .section-title {
        font-size: 18px;
        font-weight: bold;
        color: #333;
        border-bottom: 2px solid #667eea;
        padding-bottom: 10px;
        margin-bottom: 15px;
    }
    .bug-item {
        border-left: 3px solid #ff6b6b;
        padding: 10px;
        margin: 10px 0;
        background: #fafafa;
        border-radius: 4px;
    }
    .bug-id {
        font-weight: bold;
        color: #667eea;
    }
    .bug-category {
        color: #666;
        font-size: 12px;
        background: #e8e8ff;
        padding: 2px 8px;
        border-radius: 3px;
        display: inline-block;
        margin: 5px 5px 5px 0;
    }
    .match-high { color: #27ae60; font-weight: bold; }
    .match-medium { color: #f39c12; }
    .match-low { color: #e74c3c; }
    table {
        width: 100%;
        border-collapse: collapse;
        margin-top: 10px;
    }
    th, td {
        padding: 10px;
        text-align: left;
        border-bottom: 1px solid #ddd;
    }
    th {
        background-color: #667eea;
        color: white;
    }
    tr:hover {
        background-color: #f9f9f9;
    }
</style>
";
    }

    private void AppendHtmlHeader(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("<h1>📊 Relatório de Análise de Bugs</h1>");
        sb.AppendLine($"<p>Gerado em {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>");
        sb.AppendLine("</div>");
    }

    private void AppendHtmlSummary(StringBuilder sb)
    {
        var totalBugs = _bugCollection.Bugs.Count;
        var detectedBugs = _analysisResults.Sum(r => r.UniqueBugsCount);
        var totalMatches = _analysisResults.Sum(r => r.TotalMatches);

        sb.AppendLine("<div class=\"summary\">");
        AppendStatCard(sb, totalBugs.ToString(), "Total de Bugs");
        AppendStatCard(sb, detectedBugs.ToString(), "Bugs Detectados");
        AppendStatCard(sb, totalMatches.ToString(), "Total de Ocorrências");
        AppendStatCard(sb, $"{(totalBugs > 0 ? (detectedBugs * 100.0 / totalBugs).ToString("F1") : "0")}%", "Taxa de Detecção");
        sb.AppendLine("</div>");
    }

    private void AppendStatCard(StringBuilder sb, string value, string label)
    {
        sb.AppendLine("<div class=\"stat-card\">");
        sb.AppendLine($"<div class=\"stat-number\">{HtmlEncode(value)}</div>");
        sb.AppendLine($"<div class=\"stat-label\">{HtmlEncode(label)}</div>");
        sb.AppendLine("</div>");
    }

    private void AppendHtmlBugsList(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">📋 Bugs Conhecidos</div>");

        var categories = _bugCollection.GetCategories();
        foreach (var category in categories)
        {
            var bugs = _bugCollection.GetByCategory(category);
            sb.AppendLine($"<h3>{HtmlEncode(category)} ({bugs.Count})</h3>");

            foreach (var bug in bugs.Take(10))
            {
                sb.AppendLine("<div class=\"bug-item\">");
                sb.AppendLine($"<div><span class=\"bug-id\">#{bug.Id}</span> - {HtmlEncode(bug.Description)}</div>");
                sb.AppendLine($"<div class=\"bug-category\">{HtmlEncode(category)}</div>");
                sb.AppendLine("</div>");
            }

            if (bugs.Count > 10)
            {
                sb.AppendLine($"<p><em>... e {bugs.Count - 10} bug(s) adicionais</em></p>");
            }
        }

        sb.AppendLine("</div>");
    }

    private void AppendHtmlAnalysisDetails(StringBuilder sb)
    {
        if (_analysisResults.Count == 0)
        {
            return;
        }

        sb.AppendLine("<div class=\"section\">");
        sb.AppendLine("<div class=\"section-title\">🔍 Detalhes da Análise</div>");

        foreach (var result in _analysisResults)
        {
            sb.AppendLine($"<h3>Arquivo: {HtmlEncode(Path.GetFileName(result.SourceFile ?? "Desconhecido"))}</h3>");
            sb.AppendLine($"<p>Bugs detectados: {result.UniqueBugsCount} | Total de ocorrências: {result.TotalMatches}</p>");

            if (result.MatchedBugs.Count > 0)
            {
                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Bug ID</th><th>Descrição</th><th>Confiança</th><th>Ocorrências</th></tr>");

                var groupedByBug = result.MatchedBugs.GroupBy(m => m.Bug.Id);
                foreach (var group in groupedByBug.OrderByDescending(g => g.Count()))
                {
                    var firstMatch = group.First();
                    var confidence = group.Average(m => m.Confidence);
                    var confidenceClass = confidence >= 0.8 ? "match-high" : confidence >= 0.5 ? "match-medium" : "match-low";

                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>#{firstMatch.Bug.Id}</td>");
                    sb.AppendLine($"<td>{HtmlEncode(firstMatch.Bug.Description)}</td>");
                    sb.AppendLine($"<td class=\"{confidenceClass}\">{confidence:P}</td>");
                    sb.AppendLine($"<td>{group.Count()}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</table>");
            }

            sb.AppendLine("<hr>");
        }

        sb.AppendLine("</div>");
    }

    private void AppendTextSummary(StringBuilder sb)
    {
        var totalBugs = _bugCollection.Bugs.Count;
        var detectedBugs = _analysisResults.Sum(r => r.UniqueBugsCount);
        var totalMatches = _analysisResults.Sum(r => r.TotalMatches);

        sb.AppendLine("RESUMO EXECUTIVO");
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine($"  Total de Bugs Catalogados:     {totalBugs}");
        sb.AppendLine($"  Bugs Detectados:               {detectedBugs}");
        sb.AppendLine($"  Total de Ocorrências:          {totalMatches}");
        sb.AppendLine($"  Taxa de Detecção:              {(totalBugs > 0 ? (detectedBugs * 100.0 / totalBugs).ToString("F1") : "0")}%");
        sb.AppendLine();
    }

    private void AppendTextBugsList(StringBuilder sb)
    {
        sb.AppendLine("BUGS CONHECIDOS POR CATEGORIA");
        sb.AppendLine("───────────────────────────────────────────────────────────────");

        var categories = _bugCollection.GetCategories();
        foreach (var category in categories)
        {
            var bugs = _bugCollection.GetByCategory(category);
            sb.AppendLine();
            sb.AppendLine($"[{category}] - {bugs.Count} bug(s)");
            sb.AppendLine(new string('─', 60));

            foreach (var bug in bugs.Take(15))
            {
                sb.AppendLine($"  #{bug.Id}: {bug.Description}");
            }

            if (bugs.Count > 15)
            {
                sb.AppendLine($"  ... e {bugs.Count - 15} bug(s) adicionais");
            }
        }

        sb.AppendLine();
    }

    private void AppendTextAnalysisDetails(StringBuilder sb)
    {
        if (_analysisResults.Count == 0)
        {
            sb.AppendLine("Nenhuma análise realizada.");
            return;
        }

        sb.AppendLine("DETALHES DA ANÁLISE");
        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine();

        foreach (var result in _analysisResults)
        {
            sb.AppendLine($"Arquivo: {Path.GetFileName(result.SourceFile ?? "Desconhecido")}");
            sb.AppendLine($"  Bugs detectados: {result.UniqueBugsCount}");
            sb.AppendLine($"  Total de ocorrências: {result.TotalMatches}");
            sb.AppendLine();

            if (result.MatchedBugs.Count > 0)
            {
                var groupedByBug = result.MatchedBugs.GroupBy(m => m.Bug.Id);
                foreach (var group in groupedByBug.OrderByDescending(g => g.Count()).Take(10))
                {
                    var firstMatch = group.First();
                    sb.AppendLine($"  #{firstMatch.Bug.Id}: {firstMatch.Bug.Description}");
                    sb.AppendLine($"    Ocorrências: {group.Count()}");
                    sb.AppendLine($"    Confiança média: {group.Average(m => m.Confidence):P}");
                }
            }

            sb.AppendLine(new string('─', 60));
        }
    }

    private void AppendMarkdownSummary(StringBuilder sb)
    {
        var totalBugs = _bugCollection.Bugs.Count;
        var detectedBugs = _analysisResults.Sum(r => r.UniqueBugsCount);
        var totalMatches = _analysisResults.Sum(r => r.TotalMatches);

        sb.AppendLine("## 📊 Resumo Executivo");
        sb.AppendLine();
        sb.AppendLine("| Métrica | Valor |");
        sb.AppendLine("|---------|-------|");
        sb.AppendLine($"| Total de Bugs | {totalBugs} |");
        sb.AppendLine($"| Bugs Detectados | {detectedBugs} |");
        sb.AppendLine($"| Total de Ocorrências | {totalMatches} |");
        sb.AppendLine($"| Taxa de Detecção | {(totalBugs > 0 ? (detectedBugs * 100.0 / totalBugs).ToString("F1") : "0")}% |");
        sb.AppendLine();
    }

    private void AppendMarkdownBugsList(StringBuilder sb)
    {
        sb.AppendLine("## 📋 Bugs Conhecidos");
        sb.AppendLine();

        var categories = _bugCollection.GetCategories();
        foreach (var category in categories)
        {
            var bugs = _bugCollection.GetByCategory(category);
            sb.AppendLine($"### {category} ({bugs.Count})");
            sb.AppendLine();

            foreach (var bug in bugs.Take(20))
            {
                sb.AppendLine($"- **#{bug.Id}**: {bug.Description}");
            }

            if (bugs.Count > 20)
            {
                sb.AppendLine($"- *... e {bugs.Count - 20} bug(s) adicionais*");
            }

            sb.AppendLine();
        }
    }

    private void AppendMarkdownAnalysisDetails(StringBuilder sb)
    {
        if (_analysisResults.Count == 0)
        {
            sb.AppendLine("## 🔍 Detalhes da Análise\n\nNenhuma análise realizada.");
            return;
        }

        sb.AppendLine("## 🔍 Detalhes da Análise");
        sb.AppendLine();

        foreach (var result in _analysisResults)
        {
            sb.AppendLine($"### {Path.GetFileName(result.SourceFile ?? "Desconhecido")}");
            sb.AppendLine($"- **Bugs detectados**: {result.UniqueBugsCount}");
            sb.AppendLine($"- **Total de ocorrências**: {result.TotalMatches}");
            sb.AppendLine();
        }
    }

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return System.Net.WebUtility.HtmlEncode(text);
    }

    #endregion
}

/// <summary>
/// Formato do relatório
/// </summary>
public enum ReportFormat
{
    Text,
    Html,
    Markdown
}

/// <summary>
/// Extensão para armazenar arquivo de origem na análise
/// </summary>
public partial class BugAnalysisResult
{
    public string? SourceFile { get; set; }
}
