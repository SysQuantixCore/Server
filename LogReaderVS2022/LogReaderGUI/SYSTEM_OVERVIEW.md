# Sistema Completo de Análise e Correção de Bugs - MU Log Reader

## 📦 Componentes do Sistema

Este projeto implementa um sistema integrado para identificar, analisar e rastrear bugs em logs de jogo do servidor MU.

### 1️⃣ **LogReaderGUI** (Aplicação Principal)
Ferramenta visual para análise de logs em tempo real com suporte a Bug Fixes.

#### Arquivos:
- `Program.cs` - Ponto de entrada
- `MainForm.cs` - Interface principal (modificado +250 linhas)
- `LogReaderGUI.csproj` - Configuração do projeto

#### Funcionalidades:
- ✅ Monitor de logs em tempo real (MuError.log, MuRuntime.log)
- ✅ Leitura e decodificação de logs XOR
- ✅ Detecção de eventos críticos

### 2️⃣ **BugFixCollection** 
Gerencia o carregamento e parsing do arquivo Fix-Correções.txt

```csharp
// Uso:
var collection = new BugFixCollection();
collection.LoadFromFile("Fix-Correções.txt");
var bugs = collection.SearchByKeyword("inventory");
var categories = collection.GetCategories();
```

**Características**:
- Parse automático com detecção de categorias
- Extração de palavras-chave para matching
- Busca por keyword, categoria ou ID
- Exportação em formato texto

### 3️⃣ **BugLogAnalyzer**
Analisa logs contra bugs conhecidos

```csharp
// Uso:
var analyzer = new BugLogAnalyzer(bugCollection);
var result = analyzer.AnalyzeLog(logContent);

// Resultado contém:
// - MatchedBugs: Lista de bugs encontrados
// - GenericErrors: Padrões de erro genéricos
// - UniqueBugsCount: Total de bugs únicos
// - GenerateReport(): Relatório formatado
```

**Padrões Detectados**:
- CRASH_DETECTED
- EXCEPTION
- ACCESS_VIOLATION
- STACK_OVERFLOW
- DATABASE_ERROR
- TIMEOUT
- MEMORY_ERROR
- TOKEN_ERROR

### 4️⃣ **BugFixReport**
Gera relatórios em múltiplos formatos

```csharp
// Uso:
var report = new BugFixReport(bugCollection);
report.AddAnalysisResult(analysisResult, "MuError.log");

// Exportar:
report.SaveReport("report.html", ReportFormat.Html);
report.SaveReport("report.md", ReportFormat.Markdown);
report.SaveReport("report.txt", ReportFormat.Text);
```

**Formatos Suportados**:
- 📄 **HTML** - Visual com CSS, tabelas e gráficos
- 📝 **Markdown** - GitHub-compatível com tabelas
- 📋 **Texto** - Formatado e legível

### 5️⃣ **AutoBugScanService** ⭐ NOVO
Serviço de varredura automática pós-login

```csharp
// Uso:
var scanner = new AutoBugScanService(collection, analyzer);
scanner.ScanCompleted += (s, e) => Console.WriteLine($"Bugs detectados: {e.TotalBugsFound}");
scanner.StartAutoScan("MuCorrections.log", intervalSeconds: 300);

// Depois:
scanner.StopAutoScan();
```

**Funcionalidades**:
- Varredura automática em intervalos configuráveis
- Gera log de correções estruturado
- Event-based para integração fácil
- Tratamento robusto de erros

## 🎯 Fluxo de Uso

### Cenário 1: Análise Manual via GUI

```
1. Abrir LogReaderGUI
2. Clicar "Carregar Fix-Correções.txt"
   → Sistema auto-detecta arquivo
3. Carregar log (MuError.log ou MuRuntime.log)
4. Clicar "Analisar Bugs"
   → Exibe bugs encontrados
5. Clicar "Gerar Relatório"
   → Salva em HTML/MD/TXT
```

### Cenário 2: Varredura Automática (Novo)

```
1. Carregar Fix-Correções.txt (GUI)
2. Configurar intervalo de varredura (segundos)
3. Clicar "▶ Iniciar" (botão verde)
   → Inicia monitoramento contínuo
4. Sistema verifica logs a cada N segundos
5. Gera MuCorrections.log com resultados
6. Lista de críticos atualiza em tempo real
7. Clicar "⏹ Parar" para interromper
```

### Cenário 3: Integração no GameServer

```
1. Copiar classes para projeto GameServer
2. Chamar InitializeBugScanning() após login bem-sucedido
3. Sistema gera servidor/Logs/corrections_YYYYMMDD.log
4. Sincronizar com LogReaderGUI para análise
```

## 📊 Estrutura de Dados

### BugFixEntry
```csharp
public class BugFixEntry
{
    public int Id { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public List<string> Keywords { get; set; }
    public DateTime DateAdded { get; set; }
}
```

### ScanResult
```csharp
public class ScanResult
{
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    public string LogFilePath { get; set; }
    public int BugsDetected { get; set; }
    public int TotalOccurrences { get; set; }
    public List<BugMatch> MatchedBugs { get; set; }
}
```

## 🖥️ Interface da Aplicação

### Painel Principal (LogReaderGUI)

```
┌─────────────────────────────────────────────────────────────────────┐
│ MU Log Reader GUI (VS2022)                                           │
├─────────────────────────────────────────────────────────────────────┤
│ Main.exe: [/path/to/Main.exe] [Selecionar] [Auto]                   │
│ Arquivo de log: [/path/to/log] [Selecionar] [Ler/Decodificar]      │
│ [Forcar XOR] [MuError] [MuRuntime] [Salvar] [Limpar Críticos]       │
│ [Carregar Fix-Correções] [Analisar Bugs] [Gerar Relatório]         │
│                                                                       │
│ Varredura Auto: [300] seg [▶ Iniciar] [⏹ Parar] [Status: Pronto]  │
│                                                                       │
│ Dica: Use Ctrl+C para copiar eventos selecionados                   │
│ Status: ✓ Varredura automática iniciada (intervalo: 300s)           │
├─────────────────────────────────────────────────────────────────────┤
│ [Conteúdo do Log]              │ [Eventos Críticos]                 │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │ • #12: Inventário x700...        │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │ • #45: Bug TuisterSlash BK...    │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ │ • #67: Bug Aura Party Elf...     │
│                                │                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## 📂 Arquivos Gerados

### MuCorrections.log (Exemplo)

```
═══════════════════════════════════════════════════════════════
[2026-05-18 14:35:42] VARREDURA DE CORREÇÕES
═══════════════════════════════════════════════════════════════
Arquivo: MuError.log
Bugs únicos: 3
Total de ocorrências: 7

Bugs Detectados:
  • #12: Inventário x700 não ajustado
    Ocorrências: 2
    Confiança: 85.0%
  • #45: Bug do TuisterSlash BK
    Ocorrências: 3
    Confiança: 92.0%
  • #67: Bug Aura "Party" de Elf em OffMove
    Ocorrências: 2
    Confiança: 78.5%
```

## 🔧 Instalação e Setup

### Requisitos
- .NET 8.0 ou superior
- Windows 10+ ou .NET Core compatible
- Arquivo Fix-Correções.txt presente

### Build

```bash
cd LogReaderVS2022/LogReaderGUI
dotnet build -c Release
dotnet publish -c Release
```

### Execução

```bash
# Via GUI
LogReaderGUI.exe

# Via CLI (planned)
BugScanService.exe --config config.ini
```

## 📋 Configuração

### config.ini (Suportado futuramente)

```ini
[BugScanning]
Enabled=true
IntervalSeconds=300
ClientLogPath=MuCorrections.log
FixDefinitionPath=Fix-Correções.txt
AutoStartAfterLogin=true
```

## 🔗 Integração com GameServer

Veja [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) para detalhes completos.

**Resumo rápido**:
1. Copiar classes (BugFixCollection, BugLogAnalyzer, AutoBugScanService)
2. Chamar `InitializeBugScanning()` após login
3. Usar `_autoScanService.StartAutoScan(logPath, interval)`
4. Processar eventos via `ScanCompleted` e `ScanError`

## 📊 Performance

| Operação | Tempo Estimado |
|----------|----------------|
| Carregar Fix-Correções.txt | <50ms |
| Analisar log 10MB | 200-500ms |
| Gerar relatório HTML | 100-300ms |
| Varredura cíclica | Depende de intervalo |

**Recomendações**:
- Intervalo mínimo de varredura: 30 segundos
- Tamanho máximo de log: 100MB
- Manter < 50MB para performance ótima

## 🐛 Troubleshooting

### Problema: "Fix-Correções.txt não encontrado"
**Solução**: Verificar se arquivo está em um desses caminhos:
```
- Diretório atual
- Client_2/
- MuServer/
- Raiz do workspace
```

### Problema: "Nenhum bug detectado"
**Solução**:
1. Verificar se Fix-Correções.txt foi carregado
2. Verificar se log contém strings relevantes
3. Usar "Analisar Bugs" manualmente para debug

### Problema: Varredura muito lenta
**Solução**:
1. Aumentar intervalo (300s+ recomendado)
2. Usar logs menores (split logs se necessário)
3. Verificar performance do disco

## 📝 Histórico de Mudanças

### v1.0 (2026-05-18)
- ✅ Implementação inicial BugFixCollection
- ✅ Implementação BugLogAnalyzer
- ✅ Implementação BugFixReport (HTML/MD/Text)
- ✅ Integração no MainForm
- ✅ AutoBugScanService para varredura automática
- ✅ Documentação completa

## 📚 Documentação Relacionada

- [BUG_FIX_SYSTEM_README.md](BUG_FIX_SYSTEM_README.md) - Guide do usuário
- [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) - Integração técnica
- [VALIDATION_CHECKLIST.md](VALIDATION_CHECKLIST.md) - Testes e validação

## 📞 Suporte

Para issues ou sugestões:
1. Gerar relatório de erro via LogReaderGUI
2. Salvar MuCorrections.log como anexo
3. Descrever passos de reprodução
4. Informar versão do sistema

---

**Versão**: 1.0  
**Data**: 2026-05-18  
**Linguagem**: C# (.NET 8.0)  
**Status**: ✅ Completo e Pronto para Produção
