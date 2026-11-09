// ServerDisplayer.cpp: implementation of the CServerDisplayer class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "resource.h"
#include "ServerDisplayer.h"
#include "Log.h"
#include "Protect.h"
#include "ServerManager.h"
#include "SocketManager.h"

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

	const int SB_PART_COUNT = 4;

	const COLORREF CLR_LOG_BACK = RGB(18, 21, 38);

	HBITMAP s_bmpBack = NULL;  // IDB_BITMAP1
	HBITMAP s_bmpHeader = NULL;  // IDB_BITMAP2

	int  s_sbParts[SB_PART_COUNT] = { 0 };
	char s_sbText[SB_PART_COUNT][128] = { { 0 } };

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
}

CServerDisplayer::~CServerDisplayer() // OK
{
}

void CServerDisplayer::Init(HWND hWnd)
{
	m_hwnd = hWnd;

	// Carrega os bitmaps de fundo uma única vez
	m_hBackground1 = LoadBitmap(GetModuleHandle(nullptr), MAKEINTRESOURCE(IDB_BITMAP1));
	m_hBackground2 = LoadBitmap(GetModuleHandle(nullptr), MAKEINTRESOURCE(IDB_BITMAP2));

	// Título: definido uma única vez
	std::string title = std::string("DataServer | XML ") + VERSION;
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
	this->PaintStatusBar();   // primeiro: garante a altura correta da barra

	this->LogTextPaint();
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

void CServerDisplayer::PaintStatusBar() // OK
{
	HWND hWndStatusBar = GetDlgItem(this->m_hwnd, IDC_STATUSBAR);

	if (hWndStatusBar == NULL)
		return;

	// Janela de tamanho fixo: remove o size grip
	LONG style = GetWindowLong(hWndStatusBar, GWL_STYLE);
	if (style & SBARS_SIZEGRIP)
	{
		SetWindowLong(hWndStatusBar, GWL_STYLE, style & ~SBARS_SIZEGRIP);
		SetWindowPos(hWndStatusBar, NULL, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
	}

	// Larguras calculadas pela fonte da status bar
	int parts[SB_PART_COUNT];

	const char* sample[SB_PART_COUNT - 1] =
	{
		"QueueSize: 0000000",
		"Mode: Standby",
		"Licen\xE7" "a: Premium"
	};

	HDC hdc = GetDC(hWndStatusBar);
	HFONT hFont = (HFONT)SendMessage(hWndStatusBar, WM_GETFONT, 0, 0);
	if (hFont == NULL)
		hFont = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
	HFONT hOldFont = (HFONT)SelectObject(hdc, hFont);

	int borders[3] = { 0, 0, 0 };
	SendMessage(hWndStatusBar, SB_GETBORDERS, 0, (LPARAM)borders);

	const int padding = (borders[0] * 2) + borders[2] + 24;

	int x = 0;
	for (int i = 0; i < SB_PART_COUNT - 1; i++)
	{
		SIZE sz;
		GetTextExtentPoint32A(hdc, sample[i], lstrlenA(sample[i]), &sz);
		x += sz.cx + padding;
		parts[i] = x;
	}
	parts[SB_PART_COUNT - 1] = -1; // última parte ocupa o restante

	SelectObject(hdc, hOldFont);
	ReleaseDC(hWndStatusBar, hdc);

	if (memcmp(parts, s_sbParts, sizeof(parts)) != 0)
	{
		SendMessage(hWndStatusBar, SB_SIMPLE, FALSE, 0);
		SendMessage(hWndStatusBar, SB_SETPARTS, SB_PART_COUNT, (LPARAM)parts);
		memcpy(s_sbParts, parts, sizeof(parts));
		memset(s_sbText, 0, sizeof(s_sbText)); // força reenviar os textos
	}

	// Textos (prefixo \t = centralizado na parte)
	int state = 0;

	for (int n = 0; n < MAX_SERVER; n++)
	{
		if (gServerManager[n].CheckState() == 0)
			continue;

		if ((GetTickCount() - gServerManager[n].m_PacketTime) <= 60000)
		{
			state = 1;
			break;
		}
	}

	char text[SB_PART_COUNT][128];

	wsprintf(text[0], "\tQueueSize: %d", gSocketManager.GetQueueSize());
	wsprintf(text[1], "\tMode: %s", (state == 0) ? "Standby" : "Active");
	wsprintf(text[2], "\tLicen\xE7" "a: Premium");
	wsprintf(text[3], "\tDataServer | XML %s", VERSION);

	for (int i = 0; i < SB_PART_COUNT; i++)
	{
		if (lstrcmpA(text[i], s_sbText[i]) != 0)
		{
			SendMessage(hWndStatusBar, SB_SETTEXT, i, (LPARAM)text[i]);
			lstrcpynA(s_sbText[i], text[i], sizeof(s_sbText[i]));
		}
	}

	if (!IsWindowVisible(hWndStatusBar))
		ShowWindow(hWndStatusBar, SW_SHOW);
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
