# Integração de Varredura Automática de Bugs - GameServer/Client

## 📋 Visão Geral

Este documento descreve como integrar o sistema de varredura automática de bugs nos servidores MU (GameServer) e Cliente após autenticação do jogador.

## 🎯 Objetivos

1. ✅ Detectar bugs automaticamente após login do cliente
2. ✅ Gerar logs de correções em intervalo configurável
3. ✅ Manter histórico centralizado de erros detectados
4. ✅ Sincronizar com LogReaderGUI para análise em tempo real

## 📂 Estrutura de Arquivos

```
Client_2/
├── MuCorrections.log          ← Log de correções gerado automaticamente
├── MuError.log                ← Log de erros (existente)
└── MuRuntime.log              ← Log de runtime (existente)

MuServer/
├── Logs/
│   ├── corrections_*.log      ← Logs de servidor de correções
│   └── server_state.json      ← Estado atual do servidor
└── Tools/
    └── BugScanRunner.exe      ← Executável de varredura (opcional)
```

## 🔧 Implementação no Client

### Opção 1: C# WinForms (Recomendado)

Adicione ao código de inicialização do cliente após login bem-sucedido:

```csharp
// Em seu arquivo de inicialização principal (ex: GameMain.cs)

private AutoBugScanService _bugScanService;
private BugFixCollection _bugFixes;
private BugLogAnalyzer _bugAnalyzer;

public void InitializeBugScanning()
{
    try
    {
        // Carregar Fix-Correções.txt
        string fixPath = Path.Combine(
            AppContext.BaseDirectory, 
            "Fix-Correções.txt"
        );

        if (!File.Exists(fixPath))
        {
            Console.WriteLine("[BugScan] Fix-Correções.txt não encontrado");
            return;
        }

        _bugFixes = new BugFixCollection();
        _bugFixes.LoadFromFile(fixPath);

        _bugAnalyzer = new BugLogAnalyzer(_bugFixes);
        
        _bugScanService = new AutoBugScanService(_bugFixes, _bugAnalyzer);
        
        // Iniciar varredura a cada 5 minutos
        string correctionLog = Path.Combine(
            AppContext.BaseDirectory,
            "MuCorrections.log"
        );
        
        _bugScanService.ScanCompleted += OnBugScanCompleted;
        _bugScanService.StartAutoScan(correctionLog, intervalSeconds: 300);
        
        Console.WriteLine("[BugScan] Varredura automática iniciada");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[BugScan] Erro ao inicializar: {ex.Message}");
    }
}

private void OnBugScanCompleted(object sender, ScanResultEventArgs e)
{
    if (e.TotalBugsFound > 0)
    {
        Console.WriteLine($"[BugScan] {e.TotalBugsFound} bug(s) detectado(s)");
        
        // Enviar notificação para servidor (opcional)
        SendBugReportToServer(e);
    }
}

private void SendBugReportToServer(ScanResultEventArgs results)
{
    // Implementar comunicação com servidor aqui
    // Ex: Enviar dados via socket para GameServer
}

// Chamar durante login bem-sucedido:
public void OnLoginSuccess()
{
    // ... código existente ...
    
    InitializeBugScanning();
}

// Limpar ao desconectar:
public void OnLogout()
{
    _bugScanService?.StopAutoScan();
    _bugScanService?.Dispose();
}
```

### Opção 2: C++ (Se usar C++ puro)

Criar classe equivalente em C++:

```cpp
// BugScanService.h
#pragma once

class BugScanService
{
public:
    BugScanService();
    ~BugScanService();
    
    void Initialize(const std::string& fixPath, const std::string& logPath);
    void StartAutoScan(int intervalSeconds = 300);
    void StopAutoScan();
    
private:
    void PerformScan();
    
    std::unique_ptr<BugFixCollection> m_bugCollection;
    std::unique_ptr<BugLogAnalyzer> m_analyzer;
    std::thread m_scanThread;
    std::atomic<bool> m_running{false};
};

// Uso:
void OnClientLogin()
{
    static BugScanService scanner;
    scanner.Initialize("Fix-Correções.txt", "MuCorrections.log");
    scanner.StartAutoScan(300);  // 5 minutos
}
```

## 🔧 Implementação no GameServer

### Varredura de Logs do Servidor

Adicione ao seu GameServer.exe após inicialização:

```csharp
// Em GameServer.cs

public class GameServer
{
    private AutoBugScanService _serverBugScanService;
    private BugFixCollection _serverBugFixes;
    
    public void InitializeServerBugScanning()
    {
        try
        {
            // Encontrar Fix-Correções.txt
            string fixPath = Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "Fix-Correções.txt"
            );
            fixPath = Path.GetFullPath(fixPath);
            
            if (!File.Exists(fixPath))
            {
                Console.WriteLine("[Server-BugScan] Fix-Correções.txt não encontrado");
                return;
            }
            
            _serverBugFixes = new BugFixCollection();
            _serverBugFixes.LoadFromFile(fixPath);
            
            var analyzer = new BugLogAnalyzer(_serverBugFixes);
            _serverBugScanService = new AutoBugScanService(_serverBugFixes, analyzer);
            
            string serverLogPath = Path.Combine(
                AppContext.BaseDirectory,
                "Logs",
                $"corrections_{DateTime.Now:yyyyMMdd}.log"
            );
            
            _serverBugScanService.ScanCompleted += OnServerBugScanCompleted;
            _serverBugScanService.StartAutoScan(serverLogPath, intervalSeconds: 600);
            
            Console.WriteLine("[Server-BugScan] Varredura automática do servidor iniciada");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server-BugScan] Erro: {ex.Message}");
        }
    }
    
    private void OnServerBugScanCompleted(object sender, ScanResultEventArgs e)
    {
        if (e.TotalBugsFound > 0)
        {
            // Registrar no banco de dados ou arquivo de configuração
            LogServerBugDetection(e);
        }
    }
    
    private void LogServerBugDetection(ScanResultEventArgs results)
    {
        // Implementar persistência aqui
        try
        {
            string reportFile = Path.Combine(
                AppContext.BaseDirectory,
                "Logs",
                $"bug_report_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );
            
            // Serializar resultados para JSON
            // var json = JsonConvert.SerializeObject(results);
            // File.WriteAllText(reportFile, json);
        }
        catch { }
    }
    
    public void Shutdown()
    {
        _serverBugScanService?.StopAutoScan();
        _serverBugScanService?.Dispose();
    }
}
```

## 📡 Comunicação Cliente-Servidor

### Protocolo de Relatório de Bugs (Opcional)

Se desejar sincronizar bugs detectados entre cliente e servidor:

```csharp
// Estrutura para envio de relatório
[Serializable]
public class BugReportPacket
{
    public uint ClientId { get; set; }
    public string CharacterName { get; set; }
    public DateTime Timestamp { get; set; }
    public int BugCount { get; set; }
    public List<BugOccurrence> Bugs { get; set; }
}

[Serializable]
public class BugOccurrence
{
    public int BugId { get; set; }
    public string Description { get; set; }
    public int LineNumber { get; set; }
    public string LogType { get; set; }  // MuError, MuRuntime
}

// Enviar do cliente para servidor:
public void SendBugReportToServer(uint clientId, string charName, ScanResultEventArgs results)
{
    var packet = new BugReportPacket
    {
        ClientId = clientId,
        CharacterName = charName,
        Timestamp = DateTime.Now,
        BugCount = results.TotalBugsFound,
        Bugs = new List<BugOccurrence>()
    };
    
    // Serializar e enviar via socket
    // SendPacket(packet);
}
```

## 🔄 Fluxo Automático

### Timeline de Execução

```
T+0s       Cliente inicia Main.exe
   ↓
T+10s      Tela de login mostrada
   ↓
T+30s      Jogador faz login
   ↓
T+31s      ✓ Login bem-sucedido → InitializeBugScanning() é chamado
   ↓
T+31s      Carrega Fix-Correções.txt
   ↓
T+31s      Inicia varredura automática (intervalo: 300s)
   ↓
T+31s      Primeira varredura executa imediatamente
   ↓
T+31s      Analisa MuError.log e MuRuntime.log
   ↓
T+32s      Gera/atualiza MuCorrections.log
   ↓
T+331s     Segunda varredura (5 min depois)
   ↓
T+331s     Gera relatório atualizado
   ↓
T+∞        Continua em intervalos até desconexão
```

## 📊 Formato do Log de Correções

Exemplo de `MuCorrections.log`:

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

═══════════════════════════════════════════════════════════════
[2026-05-18 14:40:42] VARREDURA DE CORREÇÕES
═══════════════════════════════════════════════════════════════
Arquivo: MuError.log
Bugs únicos: 2
Total de ocorrências: 4

Bugs Detectados:
  • #12: Inventário x700 não ajustado
    Ocorrências: 1
    Confiança: 85.0%
  • #45: Bug do TuisterSlash BK
    Ocorrências: 3
    Confiança: 92.0%
```

## ⚙️ Configuração

### Arquivo de Configuração (config.ini)

```ini
[BugScanning]
Enabled=true
IntervalSeconds=300
ClientLogPath=Client_2/MuCorrections.log
ServerLogPath=MuServer/Logs/corrections.log
FixDefinitionPath=Fix-Correções.txt
AutoStartAfterLogin=true
SyncWithServer=true
```

## 🔍 Monitoramento e Debug

### Ativar Logs Detalhados

```csharp
// Em AutoBugScanService.cs
private bool _debugMode = true;

private void Log(string message)
{
    if (_debugMode)
    {
        Console.WriteLine($"[AutoBugScan] {message}");
    }
}
```

### Exemplo de Saída de Debug

```
[AutoBugScan] Iniciando varredura automática...
[AutoBugScan] Intervalo: 300 segundos
[AutoBugScan] Log de correções: C:\Client_2\MuCorrections.log
[AutoBugScan] Procurando MuError.log...
[AutoBugScan] Encontrado: C:\Client_2\MuError.log
[AutoBugScan] Analisando conteúdo...
[AutoBugScan] Bugs detectados: 3
[AutoBugScan] Escrevendo log de correções...
[AutoBugScan] ✓ Varredura concluída com sucesso
```

## 🚨 Tratamento de Erros

### Cenários de Erro e Soluções

| Erro | Causa | Solução |
|------|-------|---------|
| "Fix-Correções.txt não encontrado" | Arquivo em local errado | Verificar caminho em FindBugFixFile() |
| "MuError.log bloqueado" | Acesso concorrente | Implementar lock/mutex |
| "Intervalo < 30 segundos" | Configuração inválida | Validar input, mínimo 30s |
| "Falha ao escrever log" | Permissões insuficientes | Verificar permissões de pasta |

## ✅ Checklist de Implementação

- [ ] Adicionar classes ao projeto (BugFixCollection, BugLogAnalyzer, AutoBugScanService)
- [ ] Integrar InitializeBugScanning() após autenticação bem-sucedida
- [ ] Configurar intervalos apropriados (300s para cliente, 600s para servidor)
- [ ] Implementar tratamento de erros robusto
- [ ] Testar em ambiente de desenvolvimento
- [ ] Configurar rotação de logs (opcional)
- [ ] Documentar em servidor para ops team
- [ ] Adicionar monitoramento/alertas (opcional)

## 📝 Exemplo Completo de Integração

```csharp
// Lugar ideal para chamar:
// 1. Em GameMain.cs - OnLoginSuccess()
// 2. Em Client.cs - ConnectToGameServer() após sucesso
// 3. Em ServerMain.cs - na inicialização do servidor

public class GameMain
{
    private BugScanService _bugScanner;
    
    private void InitializeGame()
    {
        // ... código existente ...
        
        // Inicializar bug scanning
        _bugScanner = new BugScanService();
        
        if (IsPlayerLoggedIn())
        {
            _bugScanner.Initialize("Fix-Correções.txt", "MuCorrections.log");
            _bugScanner.StartAutoScan(intervalSeconds: 300);
        }
    }
    
    private void OnPlayerLogout()
    {
        // ... código existente ...
        
        _bugScanner?.StopAutoScan();
    }
    
    private void OnGameExit()
    {
        _bugScanner?.Dispose();
    }
}
```

## 📞 Suporte e Troubleshooting

Para problemas:
1. Verificar logs em `MuCorrections.log`
2. Executar análise manual via LogReaderGUI
3. Validar Fix-Correções.txt em local correto
4. Verificar permissões de arquivo/pasta

---

**Versão**: 1.0  
**Data**: 2026-05-18  
**Status**: Pronto para Implementação
