// ServerDisplayer.cpp: implementation of the CServerDisplayer class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "resource.h"
#include "ServerDisplayer.h"
#include "Log.h"
#include "Protect.h"
#include "ServerList.h"
#include "SocketManager.h"
#include "Util.h"

CServerDisplayer gServerDisplayer;

namespace
{
    // ----- Layout do log -----
    const int      LOG_AREA_TOP = 105;  // topo da área de log
    const int      LOG_LINE_HEIGHT = 15;   // altura de cada linha
    const int      LOG_SIDE_MARGIN = 10;   // margem lateral
    const int      LOG_STATUS_GAP = 10;   // espaço entre o log e a status bar
    const COLORREF LOG_BG_COLOR = RGB(18, 21, 38);

    // ----- Status bar -----
    const int STATUS_VISIBLE_PARTS = 7;    // partes com texto (deve bater com o header)
    const int STATUS_PART_PADDING = 14;   // margem interna + separação por parte

    COLORREF GetLogColor(int color)
    {
        switch (color)
        {
        case LOG_RED:    return RGB(255, 0, 0);
        case LOG_GREEN:  return RGB(110, 255, 0);
        case LOG_BLUE:   return RGB(0, 110, 255);
        case LOG_ORANGE: return RGB(255, 110, 0);
        case LOG_PURPLE: return RGB(160, 70, 160);
        case LOG_PINK:   return RGB(255, 0, 128);
        case LOG_YELLOW: return RGB(255, 240, 0);
        case LOG_BLACK:
        default:         return RGB(192, 192, 192);
        }
    }

    // Desenha um bitmap em (0,0) do DC de destino
    void DrawBitmapAt(HDC dst, HBITMAP bmp)
    {
        if (bmp == nullptr) return;

        BITMAP info;
        if (GetObject(bmp, sizeof(info), &info) == 0) return;

        HDC src = CreateCompatibleDC(dst);
        HGDIOBJ old = SelectObject(src, bmp);
        BitBlt(dst, 0, 0, info.bmWidth, info.bmHeight, src, 0, 0, SRCCOPY);
        SelectObject(src, old);
        DeleteDC(src);
    }

    // Retorna a coordenada Y (cliente) do topo da status bar
    // ou 'fallback' se ela não existir/estiver oculta
    int GetStatusBarTop(HWND hwnd, int fallback)
    {
        HWND hStatus = GetDlgItem(hwnd, IDC_STATUSBAR);
        if (hStatus == nullptr || !IsWindowVisible(hStatus))
            return fallback;

        RECT rc;
        GetWindowRect(hStatus, &rc);
        POINT pt = { rc.left, rc.top };
        ScreenToClient(hwnd, &pt);
        return min(pt.y, fallback);
    }

    // Copia 'src' para 'out' cortando com "..." se não couber em maxWidth.
    // 'out' deve ter pelo menos srcLen + 4 bytes. Retorna o comprimento
    // do texto resultante (0 se nem "..." couber).
    int BuildFittedText(HDC hdc, const char* src, int srcLen, int maxWidth, char* out)
    {
        SIZE sz = { 0, 0 };
        GetTextExtentPoint32A(hdc, src, srcLen, &sz);
        if (sz.cx <= maxWidth)
        {
            memcpy(out, src, srcLen);
            out[srcLen] = '\0';
            return srcLen;
        }

        const char ellipsis[] = "...";
        const int  ellLen = 3;

        // Busca binária pelo maior prefixo que, somado a "...", caiba
        int lo = 0, hi = srcLen - 1, best = -1;
        while (lo <= hi)
        {
            const int mid = (lo + hi) / 2;
            memcpy(out, src, mid);
            memcpy(out + mid, ellipsis, ellLen);
            out[mid + ellLen] = '\0';

            GetTextExtentPoint32A(hdc, out, mid + ellLen, &sz);
            if (sz.cx <= maxWidth) { best = mid; lo = mid + 1; }
            else { hi = mid - 1; }
        }

        if (best < 0) return 0;

        memcpy(out, src, best);
        memcpy(out + best, ellipsis, ellLen);
        out[best + ellLen] = '\0';
        return best + ellLen;
    }

    // Calcula as larguras das partes da status bar a partir do texto real
    // medido com a fonte da própria status bar. Ocupa exatamente 'avail' px.
    void ApplyStatusBarLayout(HWND hStatus, int avail)
    {
        // Textos de referência com o maior tamanho que cada campo pode ter
        const std::string templates[STATUS_VISIBLE_PARTS] = {
            "QueueSize: 99999",
            "Connected: 99999",
            "GameServers: 999/999",
            "JoinServer: OFF",
            std::string("Vers\xE3" "o: ") + VERSION,
            "Modo: Standby",
            "Licen\xE7" "a: Premium"
        };

        HFONT font = (HFONT)SendMessage(hStatus, WM_GETFONT, 0, 0);
        if (font == nullptr)
            font = (HFONT)GetStockObject(DEFAULT_GUI_FONT);

        HDC dc = GetDC(hStatus);
        HGDIOBJ oldFont = SelectObject(dc, font);

        int widths[STATUS_VISIBLE_PARTS] = {};
        int sum = 0;
        for (int i = 0; i < STATUS_VISIBLE_PARTS; ++i)
        {
            SIZE sz = { 0, 0 };
            GetTextExtentPoint32A(dc, templates[i].c_str(), (int)templates[i].size(), &sz);
            widths[i] = sz.cx + STATUS_PART_PADDING;
            sum += widths[i];
        }

        SelectObject(dc, oldFont);
        ReleaseDC(hStatus, dc);

        if (sum > avail)
        {
            // Não cabe: reduz proporcionalmente
            for (int i = 0; i < STATUS_VISIBLE_PARTS; ++i)
                widths[i] = widths[i] * avail / sum;
        }
        else if (sum < avail)
        {
            // Sobra espaço: distribui igualmente entre as partes
            const int extra = (avail - sum) / STATUS_VISIBLE_PARTS;
            for (int i = 0; i < STATUS_VISIBLE_PARTS; ++i)
                widths[i] += extra;
        }

        // Converte larguras em posições de borda direita
        int edges[STATUS_VISIBLE_PARTS] = {};
        int acc = 0;
        for (int i = 0; i < STATUS_VISIBLE_PARTS - 1; ++i)
        {
            acc += widths[i];
            edges[i] = acc;
        }
        edges[STATUS_VISIBLE_PARTS - 1] = -1; // última parte ocupa o restante

        SendMessage(hStatus, SB_SETPARTS, STATUS_VISIBLE_PARTS, (LPARAM)edges);
    }

    // Envia texto para uma parte da status bar apenas se ele mudou
    void SetStatusPart(HWND hStatus, std::string* cache, int part, const char* text)
    {
        if (cache[part] == text) return;

        cache[part] = text;
        SendMessage(hStatus, SB_SETTEXTA, part, (LPARAM)text);
    }
}

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CServerDisplayer::CServerDisplayer() // OK
{
    for (int n = 0; n < MAX_LOG_TEXT_LINE; n++)
    {
        memset(&this->m_log[n], 0, sizeof(this->m_log[n]));
    }
    m_count = 0;
}

CServerDisplayer::~CServerDisplayer() // OK
{
    if (m_hBackground1 != nullptr)
    {
        DeleteObject(m_hBackground1);
        m_hBackground1 = nullptr;
    }
    if (m_hBackground2 != nullptr)
    {
        DeleteObject(m_hBackground2);
        m_hBackground2 = nullptr;
    }
}

void CServerDisplayer::Init(HWND hWnd)
{
    m_hwnd = hWnd;

    // Carrega os bitmaps de fundo uma única vez
    m_hBackground1 = LoadBitmap(GetModuleHandle(nullptr), MAKEINTRESOURCE(IDB_BITMAP1));
    m_hBackground2 = LoadBitmap(GetModuleHandle(nullptr), MAKEINTRESOURCE(IDB_BITMAP2));

    // Título: definido uma única vez
    std::string title = std::string("ConnectServer | XML ") + VERSION;
    SetWindowText(m_hwnd, title.c_str());

    // Estilo da status bar: aplicado uma única vez
    HWND hStatus = GetDlgItem(m_hwnd, IDC_STATUSBAR);
    if (hStatus != nullptr)
    {
        DWORD style = GetWindowLong(hStatus, GWL_STYLE);
        style |= SBARS_SIZEGRIP | WS_CLIPSIBLINGS;
        style &= ~WS_BORDER;
        SetWindowLong(hStatus, GWL_STYLE, style);
        SetWindowPos(hStatus, nullptr, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        ShowWindow(hStatus, SW_SHOW);
    }

    // Adiciona o log
    gLog.AddLog(1, "Log");
}

void CServerDisplayer::Run() // OK
{
    this->LogTextPaint();
    this->PaintStatusBar();
}

void CServerDisplayer::LogTextPaint()
{
    if (m_hwnd == nullptr) return;

    RECT client;
    GetClientRect(m_hwnd, &client);
    const int width = client.right - client.left;
    if (width <= 0) return;

    // Área de pintura termina acima da status bar
    const int paintHeight = GetStatusBarTop(m_hwnd, client.bottom);
    if (paintHeight <= 0) return;

    // Última linha fica LOG_STATUS_GAP px acima da status bar
    const int areaBottom = paintHeight - LOG_STATUS_GAP;
    const int maxLines = max(0, (areaBottom - LOG_AREA_TOP) / LOG_LINE_HEIGHT);
    const int maxTextWidth = width - 2 * LOG_SIDE_MARGIN;

    HDC hdc = GetDC(m_hwnd);
    if (hdc == nullptr) return;

    // Buffer offscreen: evita flicker
    HDC     memDC = CreateCompatibleDC(hdc);
    HBITMAP memBmp = CreateCompatibleBitmap(hdc, width, paintHeight);
    HGDIOBJ oldBmp = SelectObject(memDC, memBmp);

    // 1) Fundo
    RECT fill = { 0, 0, width, paintHeight };
    HBRUSH bgBrush = CreateSolidBrush(LOG_BG_COLOR);
    FillRect(memDC, &fill, bgBrush);
    DeleteObject(bgBrush);

    DrawBitmapAt(memDC, m_hBackground1);
    DrawBitmapAt(memDC, m_hBackground2);

    // 2) Linhas: da mais recente (embaixo) para a mais antiga (em cima)
    SetBkMode(memDC, OPAQUE);
    SetBkColor(memDC, LOG_BG_COLOR);

    char visible[MAX_LOG_TEXT_SIZE + 4];
    int drawn = 0;

    for (int k = 0; k < MAX_LOG_TEXT_LINE && drawn < maxLines; ++k)
    {
        const int idx = ((m_count - 1 - k) % MAX_LOG_TEXT_LINE + MAX_LOG_TEXT_LINE) % MAX_LOG_TEXT_LINE;
        const auto& entry = m_log[idx];

        if (entry.text[0] == '\0')
            continue;

        const int len = (int)strlen(entry.text);
        const int fitted = BuildFittedText(memDC, entry.text, len, maxTextWidth, visible);
        if (fitted <= 0)
            continue;

        SIZE sz = { 0, 0 };
        GetTextExtentPoint32A(memDC, visible, fitted, &sz);

        const int y = areaBottom - (drawn + 1) * LOG_LINE_HEIGHT;
        const int boxW = sz.cx + 4;
        const int boxX = (width - boxW) / 2;
        RECT box = { boxX, y, boxX + boxW, y + LOG_LINE_HEIGHT };

        SetTextColor(memDC, GetLogColor(entry.color));
        ExtTextOutA(memDC, boxX + 2, y + (LOG_LINE_HEIGHT - sz.cy) / 2,
            ETO_OPAQUE, &box, visible, fitted, nullptr);

        ++drawn;
    }

    // 3) Copia somente a área do log para a janela
    BitBlt(hdc, 0, 0, width, paintHeight, memDC, 0, 0, SRCCOPY);

    SelectObject(memDC, oldBmp);
    DeleteObject(memBmp);
    DeleteDC(memDC);
    ReleaseDC(m_hwnd, hdc);
}

void CServerDisplayer::LogAddText(eLogColor color, const char* text, int size)
{
    if (text == nullptr) return;

    size = max(0, min(size, MAX_LOG_TEXT_SIZE - 1));

    auto& entry = m_log[m_count];
    memset(entry.text, 0, sizeof(entry.text));
    memcpy(entry.text, text, size);

    // Substitui caracteres de controle (\t, \r, \n...) que quebram o desenho
    for (int i = 0; i < size; ++i)
    {
        if ((unsigned char)entry.text[i] < 32)
            entry.text[i] = ' ';
    }

    entry.color = color;
    m_count = (m_count + 1) % MAX_LOG_TEXT_LINE;

    if (size > 9)
        gLog.Output(LOG_GENERAL, "%s", &text[9]);
}

void CServerDisplayer::PaintStatusBar()
{
    HWND hStatus = GetDlgItem(m_hwnd, IDC_STATUSBAR);
    if (hStatus == nullptr) return;

    // Reaplica as larguras somente quando a status bar muda de tamanho
    RECT rc;
    GetClientRect(hStatus, &rc);
    const int avail = rc.right - rc.left;

    if (avail > 0 && avail != m_statusLayoutWidth)
    {
        ApplyStatusBarLayout(hStatus, avail);
        m_statusLayoutWidth = avail;

        // Partes foram recriadas: força reenvio dos textos
        for (int i = 0; i < STATUS_VISIBLE_PARTS; ++i)
            m_statusCache[i].clear();
    }

    // Lê o estado uma única vez
    const int joinState = gServerList.CheckJoinServerState();

    char text[128];

    snprintf(text, sizeof(text), "QueueSize: %d", gSocketManager.GetQueueSize());
    SetStatusPart(hStatus, m_statusCache, 0, text);

    snprintf(text, sizeof(text), "Connected: %d", GetUserCount());
    SetStatusPart(hStatus, m_statusCache, 1, text);

    snprintf(text, sizeof(text), "GameServers: %d/%d",
        gServerList.m_GameServersList, gServerList.m_GameServersCount);
    SetStatusPart(hStatus, m_statusCache, 2, text);

    snprintf(text, sizeof(text), "JoinServer: %s", (joinState == 1) ? "ON" : "OFF");
    SetStatusPart(hStatus, m_statusCache, 3, text);

    // "Versão" em CP1252 (ã = \xE3)
    snprintf(text, sizeof(text), "Vers\xE3" "o: %s", VERSION);
    SetStatusPart(hStatus, m_statusCache, 4, text);

    snprintf(text, sizeof(text), "Modo: %s", (joinState == 0) ? "Standby" : "Active");
    SetStatusPart(hStatus, m_statusCache, 5, text);

    // "Licença" em CP1252 (ç = \xE7)
    SetStatusPart(hStatus, m_statusCache, 6, "Licen\xE7" "a: Premium");
}