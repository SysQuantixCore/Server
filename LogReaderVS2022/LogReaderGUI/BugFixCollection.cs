using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LogReaderGUI;

/// <summary>
/// Carrega e gerencia bugs/correções do arquivo Fix-Correções.txt
/// </summary>
public class BugFixCollection
{
    private readonly List<BugFixEntry> _bugs = new();

    public IReadOnlyList<BugFixEntry> Bugs => _bugs.AsReadOnly();

    /// <summary>
    /// Carrega bugs do arquivo Fix-Correções.txt
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        _bugs.Clear();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");
        }

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        ParseBugs(content);
    }

    /// <summary>
    /// Analisa o conteúdo do arquivo e extrai bugs
    /// </summary>
    private void ParseBugs(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        var currentCategory = "Geral";
        var bugId = 0;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();

            // Pula linhas vazias e comentários
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            // Detecta categorias (linhas com apenas "-" ou similar)
            if (trimmed.StartsWith("---", StringComparison.Ordinal) || 
                (trimmed.StartsWith("-") && trimmed.Length < 10 && !trimmed.Contains("Bug")))
            {
                currentCategory = trimmed.TrimStart('-').Trim();
                if (string.IsNullOrWhiteSpace(currentCategory))
                {
                    currentCategory = "Geral";
                }
                continue;
            }

            // Processa bugs (linhas iniciadas com "- Bug" ou "- No se")
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                bugId++;
                var description = trimmed.Substring(2).Trim();
                
                var entry = new BugFixEntry
                {
                    Id = bugId,
                    Category = currentCategory,
                    Description = description,
                    Keywords = ExtractKeywords(description),
                    DateAdded = DateTime.Now
                };

                _bugs.Add(entry);
            }
        }
    }

    /// <summary>
    /// Extrai palavras-chave do descrição do bug para busca
    /// </summary>
    private static List<string> ExtractKeywords(string description)
    {
        var keywords = new List<string>();
        var words = description.Split(new[] { ' ', ',', '.', ':', ';', '-', '+', '/' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (word.Length >= 3 && !IsCommonWord(word))
            {
                keywords.Add(word.ToLowerInvariant());
            }
        }

        return keywords;
    }

    /// <summary>
    /// Identifica palavras comuns que não são úteis para busca
    /// </summary>
    private static bool IsCommonWord(string word)
    {
        var common = new[]
        {
            "bug", "não", "que", "para", "com", "del", "de", "en", "el", "y", "o", "a",
            "foi", "ser", "est", "tem", "seu", "há", "se", "é", "em", "do", "e", "se"
        };

        return common.Contains(word.ToLowerInvariant());
    }

    /// <summary>
    /// Busca bugs por palavras-chave
    /// </summary>
    public List<BugFixEntry> SearchByKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return _bugs.ToList();
        }

        var searchKey = keyword.ToLowerInvariant();
        return _bugs
            .Where(b => 
                b.Description.Contains(searchKey, StringComparison.OrdinalIgnoreCase) ||
                b.Category.Contains(searchKey, StringComparison.OrdinalIgnoreCase) ||
                b.Keywords.Any(k => k.Contains(searchKey))
            )
            .ToList();
    }

    /// <summary>
    /// Obtém bugs por categoria
    /// </summary>
    public List<BugFixEntry> GetByCategory(string category)
    {
        return _bugs.Where(b => b.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Retorna todas as categorias únicas
    /// </summary>
    public List<string> GetCategories()
    {
        return _bugs.Select(b => b.Category).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Exporta bugs para formato de relatório
    /// </summary>
    public string ExportAsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== RELATÓRIO DE BUGS/CORREÇÕES ===");
        sb.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total de bugs: {_bugs.Count}");
        sb.AppendLine();

        var categories = GetCategories();
        foreach (var category in categories)
        {
            var bugsInCategory = GetByCategory(category);
            sb.AppendLine($"[{category}] - {bugsInCategory.Count} bug(s)");
            sb.AppendLine(new string('-', 60));

            foreach (var bug in bugsInCategory)
            {
                sb.AppendLine($"  #{bug.Id}: {bug.Description}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>
/// Representa uma entrada de bug/correção
/// </summary>
public class BugFixEntry
{
    public int Id { get; set; }
    public string Category { get; set; } = "Geral";
    public string Description { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public DateTime DateAdded { get; set; }

    public override string ToString()
    {
        return $"[{Category}] #{Id}: {Description}";
    }
}
