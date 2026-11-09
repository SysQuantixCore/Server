// ServerDisplayer.cpp: implementation of the CServerDisplayer class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "Resource.h"
#include "ServerDisplayer.h"
#include "AccountManager.h"
#include "JoinServerProtocol.h"
#include "Log.h"
#include "Protect.h"
#include "ServerManager.h"
#include "SocketManager.h"

#define LOG_LINE_HEIGHT     15                 // altura de cada linha de log
#define LOG_STATUSBAR_GAP   10                 // espaço mínimo entre o último log e a status bar
#define LOG_TEXT_RAISE      3                  // deslocamento fino do texto para cima
#define LOG_TEXT_COLOR      RGB(0, 110, 255)   // azul do texto do log

CServerDisplayer gServerDisplayer;

//////////////////////////////////////////////////////////////////////
// Funções auxiliares
//////////////////////////////////////////////////////////////////////

static void DrawBitmapResource(HDC hdc, UINT resId)
{
    HBITMAP hBitmap = LoadBitmap(GetModuleHandle(NULL), MAKEINTRESOURCE(resId));
    if (hBitmap == NULL)
    {
        return;
    }

    HDC hdcMem = CreateCompatibleDC(hdc);
    HBITMAP hBitmapOld = (HBITMAP)SelectObject(hdcMem, hBitmap);

    BITMAP bitmap;
    GetObject(hBitmap, sizeof(BITMAP), &bitmap);

    BitBlt(hdc, 0, 0, bitmap.bmWidth, bitmap.bmHeight, hdcMem, 0, 0, SRCCOPY);

    SelectObject(hdcMem, hBitmapOld);
    DeleteDC(hdcMem);
    DeleteObject(hBitmap);
}

static COLORREF GetLogTextColor(eLogColor color)
{
    switch (color)
    {
    case LOG_RED:     return RGB(255, 0, 0);
    case LOG_GREEN:   return RGB(110, 255, 0);
    case LOG_BLUE:    return RGB(0, 110, 255);
    case LOG_ORANGE:  return RGB(255, 110, 0);
    case LOG_PURPLE:  return RGB(160, 70, 160);
    case LOG_PINK:    return RGB(255, 0, 128);
    case LOG_YELLOW:  return RGB(255, 240, 0);
    case LOG_BLACK:
    default:          return RGB(192, 192, 192);
    }
}

// Retorna a coordenada Y (no cliente do pai) do topo de uma janela filha
static int GetChildTopInClient(HWND hwndParent, HWND hwndChild, int fallback)
{
    if (hwndChild == NULL)
    {
        return fallback;
    }

    RECT childRect;
    GetWindowRect(hwndChild, &childRect);

    POINT pt = { childRect.left, childRect.top };
    ScreenToClient(hwndParent, &pt);

    return pt.y;
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
}

CServerDisplayer::~CServerDisplayer() // OK
{
}

void CServerDisplayer::Init(HWND hWnd) // OK
{
    PROTECT_START

        this->m_hwnd = hWnd;

    PROTECT_FINAL

        std::string title = std::string("JoinServer | XML ") + VERSION;
    SetWindowText(m_hwnd, title.c_str());

        this->ResizeStatusBar();

    CreateDirectory("LOGS", 0);

    gLog.AddLog(1, "LOGS\\LOG");

    gLog.AddLog(1, "LOGS\\LOG_ACCOUNT");
}

void CServerDisplayer::Run() // OK
{
    this->ResizeStatusBar();
    this->LogTextPaint();
    this->PaintStatusBar();
}

//////////////////////////////////////////////////////////////////////
// Posicionamento da status bar no rodapé
//////////////////////////////////////////////////////////////////////

void CServerDisplayer::ResizeStatusBar()
{
    HWND hWndStatusBar = GetDlgItem(this->m_hwnd, IDC_STATUSBAR);
    if (hWndStatusBar == NULL)
    {
        return;
    }

    // Faz a status bar recalcular a própria altura
    SendMessage(hWndStatusBar, WM_SIZE, 0, 0);

    RECT clientRect;
    GetClientRect(this->m_hwnd, &clientRect);

    RECT sbRect;
    GetWindowRect(hWndStatusBar, &sbRect);

    int sbHeight = sbRect.bottom - sbRect.top;
    int sbTop = clientRect.bottom - sbHeight;

    // Posição atual da status bar em coordenadas do cliente
    POINT pt = { sbRect.left, sbRect.top };
    ScreenToClient(this->m_hwnd, &pt);

    // Só move se a posição ou a largura mudaram (evita repaint desnecessário)
    if (pt.x != 0 || pt.y != sbTop || (sbRect.right - sbRect.left) != clientRect.right)
    {
        SetWindowPos(hWndStatusBar, NULL, 0, sbTop, clientRect.right, sbHeight,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }
}

//////////////////////////////////////////////////////////////////////
// Log: desenhado logo acima da status bar
//////////////////////////////////////////////////////////////////////

void CServerDisplayer::LogTextPaint()
{
    RECT clientRect;
    GetClientRect(this->m_hwnd, &clientRect);

    HWND hWndStatusBar = GetDlgItem(this->m_hwnd, IDC_STATUSBAR);

    // Topo da status bar
    int statusTop = GetChildTopInClient(this->m_hwnd, hWndStatusBar, clientRect.bottom);

    // Limite inferior do texto (exatamente acima da status bar pelo valor do GAP)
    int textBottom = statusTop - LOG_STATUSBAR_GAP;

    HDC hdc = GetDC(this->m_hwnd);

    // Limita o desenho até o topo da status bar
    IntersectClipRect(hdc, 0, 0, clientRect.right, statusTop);

    DrawBitmapResource(hdc, IDB_BITMAP1);
    DrawBitmapResource(hdc, IDB_BITMAP2);

    SetBkMode(hdc, TRANSPARENT);

    int slot = 0; // 0 = linha mais próxima da status bar
    int count = (this->m_count - 1 >= 0) ? (this->m_count - 1) : (MAX_LOG_TEXT_LINE - 1);

    for (int n = 0; n < MAX_LOG_TEXT_LINE; n++)
    {
        const int size = lstrlenA(this->m_log[count].text);

        if (size > 0)
        {
            // Linhas sobem a partir de textBottom sem o deslocamento extra
            int top = textBottom - (slot + 1) * LOG_LINE_HEIGHT;

            if (top < 0)
            {
                break; // sem espaço acima da área visível
            }

            RECT textRect = { clientRect.left, top, clientRect.right, top + LOG_LINE_HEIGHT };

            SIZE textSize;
            GetTextExtentPoint32A(hdc, this->m_log[count].text, size, &textSize);

            int rectWidth = textSize.cx + 2;
            int rectLeft = (clientRect.right - rectWidth) / 2;

            RECT backgroundRect = { rectLeft, top, rectLeft + rectWidth, top + LOG_LINE_HEIGHT };

            HBRUSH hBrush = CreateSolidBrush(RGB(18, 21, 38));
            FillRect(hdc, &backgroundRect, hBrush);
            DeleteObject(hBrush);

            SetTextColor(hdc, LOG_TEXT_COLOR);

            UINT textFormat = DT_CENTER | DT_SINGLELINE | DT_VCENTER;
            DrawTextA(hdc, this->m_log[count].text, size, &textRect, textFormat);

            ++slot;
        }

        --count;
        if (count < 0)
        {
            count = MAX_LOG_TEXT_LINE - 1;
        }
    }

    ReleaseDC(this->m_hwnd, hdc);
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


//////////////////////////////////////////////////////////////////////
// Status bar
//////////////////////////////////////////////////////////////////////

void CServerDisplayer::PaintStatusBar() // OK
{
    HWND hWndStatusBar = GetDlgItem(this->m_hwnd, IDC_STATUSBAR);
    if (hWndStatusBar == NULL)
    {
        return;
    }

    char szTempText[128];

    // Parte 0: fila de conexões
    wsprintf(szTempText, "QueueSize: %d", gSocketManager.GetQueueSize());
    SendMessage(hWndStatusBar, SB_SETTEXT, 0, (LPARAM)szTempText);

    // Parte 1: contas conectadas
    wsprintf(szTempText, "Connected: %d", gAccountManager.GetAccountCount());
    SendMessage(hWndStatusBar, SB_SETTEXT, 1, (LPARAM)szTempText);

    // Conta os servidores com pacote recente (último minuto)
    int activeServers = 0;

    for (int n = 0; n < MAX_SERVER; n++)
    {
        if (gServerManager[n].CheckState() == 0)
        {
            continue;
        }

        if ((GetTickCount() - gServerManager[n].m_PacketTime) <= 60000)
        {
            activeServers++;
        }
    }

    // Parte 2: modo de operação
    wsprintf(szTempText, "Mode: %s", (activeServers == 0) ? "Standby" : "Active");
    SendMessage(hWndStatusBar, SB_SETTEXT, 2, (LPARAM)szTempText);

    // Parte 3: licença
    wsprintf(szTempText, "License: Premium");
    SendMessage(hWndStatusBar, SB_SETTEXT, 3, (LPARAM)szTempText);

    // Parte 4: servidores ativos
    wsprintf(szTempText, "Servers: %d", activeServers);
    SendMessage(hWndStatusBar, SB_SETTEXT, 4, (LPARAM)szTempText);

    // Parte 5: hora atual
    SYSTEMTIME st;
    GetLocalTime(&st);

    wsprintf(szTempText, "Time: %02d:%02d:%02d", st.wHour, st.wMinute, st.wSecond);
    SendMessage(hWndStatusBar, SB_SETTEXT, 5, (LPARAM)szTempText);

    ShowWindow(hWndStatusBar, SW_SHOW);
}