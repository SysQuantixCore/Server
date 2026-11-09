# LogReaderVS2022

Leitor de logs para Visual Studio 2022.

Suporta:
- `MuError.log` (decodifica XOR automaticamente)
- `MuRuntime.log`, `*.crash.log` e outros logs texto

Tambem inclui versao GUI (WinForms) para uso sem terminal.

## Solucao

Abra no VS2022:
- `Source/LogReaderVS2022/LogReaderVS2022.sln`
- `Source/LogReaderVS2022/LogReaderGUI.sln`

## Como usar

No terminal (na pasta de saida do exe):

```bat
LogReaderVS2022.exe "..\..\..\..\Client_2\MuError.log"
```

Gerar txt decodificado:

```bat
LogReaderVS2022.exe "..\..\..\..\Client_2\MuError.log" "MuError_decodificado.txt"
```

Forcar modo XOR em qualquer arquivo:

```bat
LogReaderVS2022.exe "arquivo.log" "saida.txt" --xor
```

Ler log texto puro:

```bat
LogReaderVS2022.exe "..\..\..\..\Client_2\MuRuntime.log"
```

## GUI (WinForms)

Abra no VS2022:
- `Source/LogReaderVS2022/LogReaderGUI.sln`

Funcoes da GUI:
- Selecionar arquivo de log
- Digitar/colar manualmente qualquer caminho de arquivo
- Botoes rapidos para preencher MuError.log e MuRuntime.log automaticamente
- Decodificar XOR automaticamente para `MuError.log`
- Forcar XOR com checkbox
- Salvar resultado decodificado em `.txt`
- Selecionar `Main.exe` e iniciar o jogo pelo app
- Monitorar `MuError.log` e `MuRuntime.log` em tempo real
- Detectar linhas criticas de erro/crash enquanto o jogo roda
- Permanecer aberto mesmo quando o client fecha/crasha
