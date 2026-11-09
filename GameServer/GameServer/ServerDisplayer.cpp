// ServerDisplayer.cpp
// Layout Modern Dark: header com IDB_BITMAP2 e paineis de vidro com blur de IDB_BITMAP1.
// O header nao exibe logo GS, nome do servidor, versao ou update.
// Card unico com todas as categorias (Todas, Eventos, Custom Arena, Invasion, Global Message),
// lista rolavel e submenu aberto pelo cabecalho do card.
// SERVER LOG e card de eventos podem ser rolados com a roda do mouse.
// Otimizado: fundo, header e paineis ficam em uma camada estatica, refeita somente
// quando o tamanho da janela muda.

#include "stdafx.h"
#include "ServerDisplayer.h"
#include "CustomArena.h"
#include "GameMain.h"
#include "Log.h"
#include "Protect.h"
#include "resource.h"
#include "ServerInfo.h"
#include "SocketManager.h"
#include "User.h"
#include "Util.h"

#include <stdio.h>
#include <string.h>

#pragma comment(lib, "msimg32.lib")

CServerDisplayer gServerDisplayer;

namespace
{
	// ===================== Layout =====================
	const int SD_MARGIN = 14;
	const int SD_GAP = 10;
	const int SD_HEADER_H = 68;
	const int SD_CARD_Y = SD_HEADER_H + SD_MARGIN;
	const int SD_CARD_H = 60;
	const int SD_BODY_Y = SD_CARD_Y + SD_CARD_H + SD_GAP;
	const int SD_SIDE_W = 320;
	const int SD_PANEL_HDR = 32;
	const int SD_PANEL_RADIUS = 12;
	const int SD_MAIN_CARD_H = 316;	// card unico
	const int SD_EVENT_SLOTS = 18;
	const int SD_ARENA_SLOTS = 30;
	const int SD_INVASION_SLOTS = 30;
	const int SD_MIN_W = 520;
	const int SD_MIN_H = 300;
	const int SD_MAX_ITEMS = 160;	// capacidade da lista do card unico

	// ===================== Efeitos =====================
	const BYTE SD_HEADER_OVERLAY_ALPHA = 50;	// menor = imagem do painel mais visivel
	const BYTE SD_PANEL_TINT_ALPHA = 130;
	const int SD_BLUR_DOWNSCALE = 4;
	const int SD_SHADOW_FADE = 3;

	// ===================== Lista rolavel =====================
	const int LST_ROW_H = 34;
	const int LST_ROW_GAP = 4;
	const int LST_SEC_H = 22;		// cabecalho de secao (categoria Todas)
	const int LST_SEC_GAP = 4;
	const int LST_STEP_WHEEL = LST_ROW_H + LST_ROW_GAP;
	const int LST_PAD = 8;
	const int LST_SCROLL_W = 6;
	const int LST_ACCENT_W = 4;

	// ===================== Paleta =====================
	const COLORREF CLR_BG = RGB(13, 16, 24);
	const COLORREF CLR_HEADER_TOP = RGB(26, 32, 50);
	const COLORREF CLR_HEADER_BOTTOM = RGB(17, 21, 33);
	const COLORREF CLR_SHADOW = RGB(30, 32, 38);
	const COLORREF CLR_PANEL = RGB(22, 27, 39);
	const COLORREF CLR_PANEL_BORDER = RGB(40, 47, 66);
	const COLORREF CLR_ACCENT = RGB(0, 170, 255);
	const COLORREF CLR_PURPLE = RGB(160, 110, 255);
	const COLORREF CLR_ORANGE = RGB(255, 160, 64);
	const COLORREF CLR_TEXT = RGB(236, 240, 247);
	const COLORREF CLR_TEXT_DIM = RGB(130, 140, 163);
	const COLORREF CLR_SUCCESS = RGB(46, 204, 113);
	const COLORREF CLR_WARNING = RGB(255, 184, 0);
	const COLORREF CLR_DANGER = RGB(235, 77, 75);
	const COLORREF CLR_INFO = RGB(72, 149, 239);
	const COLORREF CLR_PILL_OFF_BG = RGB(34, 40, 56);
	const COLORREF CLR_PILL_ON_BG = RGB(18, 52, 38);

	// ===================== Categorias do card unico =====================
	enum SdCategory
	{
		SD_CAT_ALL = 0,
		SD_CAT_EVENTS = 1,
		SD_CAT_ARENA = 2,
		SD_CAT_INVASION = 3,
		SD_CAT_GLOBAL = 4
	};

	const UINT SD_MENU_ALL = 1000;
	const UINT SD_MENU_EVENTS = 1004;
	const UINT SD_MENU_ARENA = 1001;
	const UINT SD_MENU_INVASION = 1002;
	const UINT SD_MENU_GLOBAL = 1003;

	// ===================== Adaptador de CEventName =====================
	// Unico ponto que depende da API de CEventName.h.
	bool        EvtArenaActive(int n) { return gEventName.IsArena(n); }
	const char* EvtArenaName(int n) { return gEventName.ArenaName(n); }
	int         EvtArenaRemain(int n) { return gEventName.ArenaRemainTime(n); }
	bool        EvtInvasionActive(int n) { return gEventName.IsInvasion(n); }
	const char* EvtInvasionName(int n) { return gEventName.InvasionName(n); }
	int         EvtInvasionRemain(int n) { return gEventName.InvasionRemainTime(n); }

	// ===================== Imagens e buffers =====================
	HBITMAP s_bgBmp = nullptr;          // IDB_BITMAP1 (fundo e base do blur)
	HBITMAP s_panelBmp = nullptr;       // header (GAMESERVER_HEADER_BITMAP)
	bool    s_imagesLoaded = false;

	HDC     s_memDC = nullptr;          // quadro final (double buffering)
	HBITMAP s_memBmp = nullptr;
	HGDIOBJ s_memOldBmp = nullptr;
	int     s_memW = 0;
	int     s_memH = 0;

	HDC     s_staticDC = nullptr;       // camada estatica: fundo, header e paineis
	HBITMAP s_staticBmp = nullptr;
	HGDIOBJ s_staticOldBmp = nullptr;
	int     s_staticW = 0;
	int     s_staticH = 0;

	HDC     s_blurDC = nullptr;         // fonte do vidro, existe somente durante BuildStaticLayer

	// ===================== Estado do frame =====================
	HDC     s_frame = nullptr;
	int     s_W = 0;
	int     s_lineH = 20;
	bool    s_statusInit = false;
	char    s_statusCache[6][64] = {};

	HFONT   s_fTitle = nullptr;
	HFONT   s_fValue = nullptr;
	HFONT   s_fBody = nullptr;

	int     s_category = SD_CAT_ALL;

	RECT    s_rcLog = {};
	RECT    s_rcConnect = {};
	RECT    s_rcUnique = {};
	RECT    s_hitUnique = {};

	RECT    s_logScrollArea = {};		// area rolavel do SERVER LOG
	int     s_logScrollOffset = 0;		// deslocamento do SERVER LOG, em pixels
	int     s_logMaxOffset = 0;			// deslocamento maximo calculado no desenho

	// ===================== Auxiliares de GDI =====================
	struct FontScope
	{
		HDC dc;
		HGDIOBJ old;

		FontScope(HDC d, HFONT f) : dc(d), old(SelectObject(d, f))
		{
		}

		~FontScope()
		{
			SelectObject(dc, old);
		}
	};

	struct ClipScope
	{
		HDC dc;

		ClipScope(HDC d, const RECT& rc) : dc(d)
		{
			HRGN r = CreateRectRgnIndirect(&rc);
			SelectClipRgn(dc, r);
			DeleteObject(r);
		}

		~ClipScope()
		{
			SelectClipRgn(dc, nullptr);
		}
	};

	HFONT MakeFont(int height, int weight)
	{
		return CreateFontA(height, 0, 0, 0, weight, FALSE, FALSE, FALSE,
			DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
			CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, "Segoe UI");
	}

	// Preenchimento com o pincel de estoque DC_BRUSH: nao cria nem destroi objeto GDI.
	void FillRectColor(HDC hdc, const RECT& rc, COLORREF color)
	{
		SetDCBrushColor(hdc, color);
		FillRect(hdc, &rc, static_cast<HBRUSH>(GetStockObject(DC_BRUSH)));
	}

	void HLine(HDC hdc, int x1, int x2, int y, COLORREF color)
	{
		RECT rc = { x1, y, x2, y + 1 };
		FillRectColor(hdc, rc, color);
	}

	// Retangulo arredondado com pincel e caneta de estoque.
	void RoundPanel(HDC hdc, const RECT& rc, COLORREF fill, COLORREF border, int radius)
	{
		HGDIOBJ ob = SelectObject(hdc, GetStockObject(DC_BRUSH));
		HGDIOBJ op = SelectObject(hdc, GetStockObject(DC_PEN));

		SetDCBrushColor(hdc, fill);
		SetDCPenColor(hdc, border);

		RoundRect(hdc, rc.left, rc.top, rc.right, rc.bottom, radius * 2, radius * 2);

		SelectObject(hdc, ob);
		SelectObject(hdc, op);
	}

	// Desenha a imagem cobrindo todo o retangulo, mantendo a proporcao e recortando o excedente.
	void DrawImageCover(HDC hdc, const RECT& rc, HBITMAP bmp)
	{
		int dw = rc.right - rc.left;
		int dh = rc.bottom - rc.top;

		if (!bmp || dw <= 0 || dh <= 0)
		{
			return;
		}

		BITMAP bm = {};

		if (GetObject(bmp, sizeof(bm), &bm) == 0)
		{
			return;
		}

		if (bm.bmWidth <= 0 || bm.bmHeight <= 0)
		{
			return;
		}

		double sx = (double)dw / bm.bmWidth;
		double sy = (double)dh / bm.bmHeight;
		double scale = (sy > sx) ? sy : sx;

		int srcW = (int)(dw / scale + 0.5);
		int srcH = (int)(dh / scale + 0.5);

		if (srcW > bm.bmWidth) srcW = bm.bmWidth;
		if (srcH > bm.bmHeight) srcH = bm.bmHeight;

		int srcX = (bm.bmWidth - srcW) / 2;
		int srcY = (bm.bmHeight - srcH) / 2;

		HDC src = CreateCompatibleDC(hdc);
		if (!src)
		{
			return;
		}

		HGDIOBJ old = SelectObject(src, bmp);
		int prevMode = SetStretchBltMode(hdc, HALFTONE);
		SetBrushOrgEx(hdc, 0, 0, nullptr);

		StretchBlt(hdc, rc.left, rc.top, dw, dh,
			src, srcX, srcY, srcW, srcH, SRCCOPY);

		SetStretchBltMode(hdc, prevMode);
		SelectObject(src, old);
		DeleteDC(src);
	}

	// Camada de cor translucida sobre um retangulo.
	void FillOverlay(HDC hdc, const RECT& rc, COLORREF color, BYTE alpha)
	{
		int w = rc.right - rc.left;
		int h = rc.bottom - rc.top;

		if (w <= 0 || h <= 0)
		{
			return;
		}

		HDC dc = CreateCompatibleDC(hdc);
		if (!dc)
		{
			return;
		}

		HBITMAP bmp = CreateCompatibleBitmap(hdc, 1, 1);
		if (!bmp)
		{
			DeleteDC(dc);
			return;
		}

		HGDIOBJ old = SelectObject(dc, bmp);
		SetPixelV(dc, 0, 0, color);

		BLENDFUNCTION bf = { AC_SRC_OVER, 0, alpha, 0 };
		AlphaBlend(hdc, rc.left, rc.top, w, h, dc, 0, 0, 1, 1, bf);

		SelectObject(dc, old);
		DeleteObject(bmp);
		DeleteDC(dc);
	}

	void DrawWindowBackground(HDC hdc, const RECT& rc)
	{
		if (s_bgBmp)
		{
			DrawImageCover(hdc, rc, s_bgBmp);
		}
		else
		{
			FillRectColor(hdc, rc, CLR_BG);
		}
	}

	void DrawHeaderBackground(HDC hdc, const RECT& rc)
	{
		if (s_panelBmp)
		{
			DrawImageCover(hdc, rc, s_panelBmp);
			FillOverlay(hdc, rc, CLR_BG, SD_HEADER_OVERLAY_ALPHA);
			return;
		}

		// Fallback: gradiente quando o bitmap nao carrega.
		TRIVERTEX v[2] = {};

		v[0].x = rc.left;
		v[0].y = rc.top;
		v[0].Red = (COLOR16)(GetRValue(CLR_HEADER_TOP) << 8);
		v[0].Green = (COLOR16)(GetGValue(CLR_HEADER_TOP) << 8);
		v[0].Blue = (COLOR16)(GetBValue(CLR_HEADER_TOP) << 8);

		v[1].x = rc.right;
		v[1].y = rc.bottom;
		v[1].Red = (COLOR16)(GetRValue(CLR_HEADER_BOTTOM) << 8);
		v[1].Green = (COLOR16)(GetGValue(CLR_HEADER_BOTTOM) << 8);
		v[1].Blue = (COLOR16)(GetBValue(CLR_HEADER_BOTTOM) << 8);

		GRADIENT_RECT g = { 0, 1 };
		GradientFill(hdc, v, 2, &g, 1, GRADIENT_FILL_RECT_V);
	}

	// Sombra cinza escura abaixo do painel superior, substituindo a faixa azul.
	void DrawHeaderShadow(HDC hdc, int w, int y)
	{
		RECT solid = { 0, y - 2, w, y };
		FillRectColor(hdc, solid, CLR_SHADOW);

		static const BYTE fade[SD_SHADOW_FADE] = { 110, 65, 30 };

		for (int i = 0; i < SD_SHADOW_FADE; ++i)
		{
			RECT band = { 0, y + (i * 2), w, y + (i * 2) + 2 };
			FillOverlay(hdc, band, CLR_SHADOW, fade[i]);
		}
	}

	// Painel de vidro: fundo borrado (s_blurDC) com camada escura e borda arredondada.
	// Usado somente na construcao da camada estatica.
	void DrawGlassPanel(HDC hdc, const RECT& rc, COLORREF border, int radius)
	{
		int w = rc.right - rc.left;
		int h = rc.bottom - rc.top;

		if (w <= 0 || h <= 0)
		{
			return;
		}

		HRGN clip = CreateRoundRectRgn(rc.left, rc.top, rc.right + 1, rc.bottom + 1,
			radius * 2, radius * 2);

		int saved = SaveDC(hdc);
		SelectClipRgn(hdc, clip);

		if (s_blurDC)
		{
			BitBlt(hdc, rc.left, rc.top, w, h, s_blurDC, rc.left, rc.top, SRCCOPY);
		}
		else
		{
			FillRectColor(hdc, rc, CLR_PANEL);
		}

		FillOverlay(hdc, rc, CLR_PANEL, SD_PANEL_TINT_ALPHA);

		RestoreDC(hdc, saved);
		DeleteObject(clip);

		SetDCPenColor(hdc, border);
		HGDIOBJ oldPen = SelectObject(hdc, GetStockObject(DC_PEN));
		HGDIOBJ oldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));

		RoundRect(hdc, rc.left, rc.top, rc.right - 1, rc.bottom - 1,
			radius * 2, radius * 2);

		SelectObject(hdc, oldBrush);
		SelectObject(hdc, oldPen);
	}

	// ===================== Carregamento e liberacao =====================
	void LoadBackgroundImages()
	{
		if (s_imagesLoaded)
		{
			return;
		}

		HINSTANCE inst = GetModuleHandle(nullptr);
		s_bgBmp = LoadBitmap(inst, MAKEINTRESOURCE(IDB_BITMAP1));
		s_panelBmp = LoadBitmap(inst, MAKEINTRESOURCE(GAMESERVER_HEADER_BITMAP));
		s_imagesLoaded = true;
	}

	void ReleaseBackgroundImages()
	{
		if (s_bgBmp) DeleteObject(s_bgBmp);
		if (s_panelBmp) DeleteObject(s_panelBmp);

		s_bgBmp = nullptr;
		s_panelBmp = nullptr;
		s_imagesLoaded = false;
	}

	void ReleaseFrameBuffer()
	{
		if (s_memDC)
		{
			SelectObject(s_memDC, s_memOldBmp);
			DeleteObject(s_memBmp);
			DeleteDC(s_memDC);
		}

		s_memDC = nullptr;
		s_memBmp = nullptr;
		s_memOldBmp = nullptr;
		s_memW = 0;
		s_memH = 0;
	}

	void EnsureFrameBuffer(HWND hwnd, int w, int h)
	{
		if (s_memDC && s_memW == w && s_memH == h)
		{
			return;
		}

		ReleaseFrameBuffer();

		HDC wnd = GetDC(hwnd);
		if (!wnd)
		{
			return;
		}

		s_memDC = CreateCompatibleDC(wnd);
		s_memBmp = CreateCompatibleBitmap(wnd, w, h);
		ReleaseDC(hwnd, wnd);

		if (!s_memDC || !s_memBmp)
		{
			ReleaseFrameBuffer();
			return;
		}

		s_memOldBmp = SelectObject(s_memDC, s_memBmp);
		s_memW = w;
		s_memH = h;
	}

	void ReleaseStaticLayer()
	{
		if (s_staticDC)
		{
			SelectObject(s_staticDC, s_staticOldBmp);
			DeleteObject(s_staticBmp);
			DeleteDC(s_staticDC);
		}

		s_staticDC = nullptr;
		s_staticBmp = nullptr;
		s_staticOldBmp = nullptr;
		s_staticW = 0;
		s_staticH = 0;
	}

	// Reduz o fundo e amplia de volta com HALFTONE, gerando a fonte do vidro em dst.
	void BuildBlurSource(HDC dst, int w, int h)
	{
		int sw = w / SD_BLUR_DOWNSCALE;
		int sh = h / SD_BLUR_DOWNSCALE;

		if (sw < 1) sw = 1;
		if (sh < 1) sh = 1;

		HDC tinyDC = CreateCompatibleDC(dst);
		if (!tinyDC)
		{
			return;
		}

		HBITMAP tinyBmp = CreateCompatibleBitmap(dst, sw, sh);
		if (!tinyBmp)
		{
			DeleteDC(tinyDC);
			return;
		}

		HGDIOBJ oldTiny = SelectObject(tinyDC, tinyBmp);

		RECT tinyRc = { 0, 0, sw, sh };
		DrawWindowBackground(tinyDC, tinyRc);

		SetStretchBltMode(dst, HALFTONE);
		SetBrushOrgEx(dst, 0, 0, nullptr);
		StretchBlt(dst, 0, 0, w, h, tinyDC, 0, 0, sw, sh, SRCCOPY);

		SelectObject(tinyDC, oldTiny);
		DeleteObject(tinyBmp);
		DeleteDC(tinyDC);
	}

	// ===================== Formatacao e cores =====================
	void FormatRemainTime(int sec, char* buf, int size)
	{
		if (sec < 0)
		{
			sprintf_s(buf, size, "Disabled");
			return;
		}

		if (sec == 0)
		{
			sprintf_s(buf, size, "Online");
			return;
		}

		int hours = sec / 3600;

		if (hours > 23)
		{
			sprintf_s(buf, size, "%d day(s)+", hours / 24);
			return;
		}

		sprintf_s(buf, size, "%02d:%02d:%02d",
			hours, (sec % 3600) / 60, sec % 60);
	}

	COLORREF RemainColor(int sec)
	{
		if (sec < 0) return CLR_DANGER;
		if (sec == 0) return CLR_SUCCESS;
		if (sec < 300) return CLR_WARNING;
		return CLR_TEXT;
	}

	const char* CsStageName(int st)
	{
		switch (st)
		{
		case -1: return "None";
		case 0:  return "Idle 1";
		case 1:  return "Guild Register";
		case 2:  return "Idle 2";
		case 3:  return "Mark Register";
		case 4:  return "Idle 3";
		case 5:  return "Notify";
		case 6:  return "Ready Siege";
		case 7:  return "Started Siege";
		case 8:  return "Ended Siege";
		case 9:  return "End All";
		default: return "Unknown";
		}
	}

	COLORREF LogColorOf(eLogColor c)
	{
		switch (c)
		{
		case LOG_BLACK: return CLR_TEXT;
		case LOG_RED:   return CLR_DANGER;
		case LOG_GREEN: return CLR_SUCCESS;
		case LOG_BLUE:  return CLR_INFO;
		case LOG_BOT:   return CLR_PURPLE;
		case LOG_USER:  return CLR_ACCENT;
		case LOG_EVENT: return CLR_WARNING;
		case LOG_ALERT: return CLR_ORANGE;
		case LOG_YELOW: return CLR_WARNING;
		default:        return CLR_TEXT_DIM;
		}
	}

	// Indice do i-esimo item mais recente em um buffer circular (i = 0 e o mais novo).
	int RingIndex(int nextIdx, int i, int n)
	{
		int k = (nextIdx - 1 - i) % n;
		if (k < 0) k += n;
		return k;
	}

	// Arena e invasao nao existem no modo Castle Siege, entao caem em Todas.
	int EffectiveCategory()
	{
		if (GAMESERVER_TYPE != 0 &&
			(s_category == SD_CAT_ARENA || s_category == SD_CAT_INVASION))
		{
			return SD_CAT_ALL;
		}

		return s_category;
	}

	const char* CategoryTitle(int cat)
	{
		switch (cat)
		{
		case SD_CAT_ALL:      return "ALL";
		case SD_CAT_EVENTS:   return "EVENTS";
		case SD_CAT_ARENA:    return "CUSTOM ARENA";
		case SD_CAT_INVASION: return "INVASION";
		default:              return "GLOBAL MESSAGE";
		}
	}

	COLORREF CategoryAccent(int cat)
	{
		switch (cat)
		{
		case SD_CAT_ALL:      return CLR_ACCENT;
		case SD_CAT_EVENTS:   return CLR_ACCENT;
		case SD_CAT_ARENA:    return CLR_PURPLE;
		case SD_CAT_INVASION: return CLR_ORANGE;
		default:              return CLR_SUCCESS;
		}
	}

	// ===================== Componentes dinamicos de painel =====================
	// Somente decoracao (barra, titulo, separador). O vidro vem da camada estatica.
	void DrawPanelDecor(HDC hdc, const RECT& rc, const char* title, COLORREF accent)
	{
		RECT bar = { rc.left + 12, rc.top + 10, rc.left + 15, rc.top + SD_PANEL_HDR - 10 };
		FillRectColor(hdc, bar, accent);

		FontScope f(hdc, s_fBody);
		SetBkMode(hdc, TRANSPARENT);
		SetTextColor(hdc, CLR_TEXT);

		RECT tr = { rc.left + 22, rc.top, rc.right - 26, rc.top + SD_PANEL_HDR };
		DrawTextA(hdc, title, -1, &tr,
			DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

		HLine(hdc, rc.left + 1, rc.right - 1,
			rc.top + SD_PANEL_HDR, CLR_PANEL_BORDER);
	}

	// Seta indicando que o cabecalho abre um submenu.
	void DrawMenuChevron(HDC hdc, const RECT& rc)
	{
		int cx = rc.right - 18;
		int cy = rc.top + SD_PANEL_HDR / 2;

		POINT tri[3] = {
			{ cx - 5, cy - 2 },
			{ cx + 5, cy - 2 },
			{ cx,     cy + 3 }
		};

		SetDCBrushColor(hdc, CLR_TEXT_DIM);

		HGDIOBJ ob = SelectObject(hdc, GetStockObject(DC_BRUSH));
		HGDIOBJ op = SelectObject(hdc, GetStockObject(NULL_PEN));

		Polygon(hdc, tri, 3);

		SelectObject(hdc, op);
		SelectObject(hdc, ob);
	}

	// Conteudo de um stat card; o vidro vem da camada estatica.
	void DrawStatCardContent(HDC hdc, const RECT& rc, const char* label, const char* value,
		COLORREF valueColor, COLORREF accent)
	{
		RECT bar = { rc.left + 12, rc.top + 12, rc.left + 15, rc.bottom - 12 };
		FillRectColor(hdc, bar, accent);

		SetBkMode(hdc, TRANSPARENT);

		{
			FontScope f(hdc, s_fBody);
			SetTextColor(hdc, CLR_TEXT_DIM);

			RECT lr = { rc.left + 24, rc.top + 10, rc.right - 10, rc.top + 28 };
			DrawTextA(hdc, label, -1, &lr,
				DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
		}

		{
			FontScope f(hdc, s_fValue);
			SetTextColor(hdc, valueColor);

			RECT vr = { rc.left + 24, rc.top + 28, rc.right - 10, rc.bottom - 6 };
			DrawTextA(hdc, value, -1, &vr,
				DT_LEFT | DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS);
		}
	}

	// ===================== Modelo da lista do card unico =====================
	enum CardItemKind
	{
		ITEM_HEADER = 0,
		ITEM_ROW = 1,
		ITEM_MSG = 2,
		ITEM_EMPTY = 3
	};

	struct CardItem
	{
		int kind;
		char title[128];
		char value[32];
		COLORREF accent;
		COLORREF titleColor;
		COLORREF valueColor;
	};

	CardItem s_items[SD_MAX_ITEMS];
	int s_itemCount = 0;
	RECT s_listArea = {};
	int s_listOffset = 0;

	int ItemHeight(int kind)
	{
		return kind == ITEM_HEADER ? LST_SEC_H : LST_ROW_H;
	}

	int ItemStep(int kind)
	{
		return kind == ITEM_HEADER
			? LST_SEC_H + LST_SEC_GAP
			: LST_ROW_H + LST_ROW_GAP;
	}

	void AddItem(int kind, const char* title, const char* value,
		COLORREF accent, COLORREF titleColor, COLORREF valueColor)
	{
		if (s_itemCount >= SD_MAX_ITEMS)
		{
			return;
		}

		CardItem& it = s_items[s_itemCount++];
		it.kind = kind;

		strncpy_s(it.title, sizeof(it.title), title ? title : "", _TRUNCATE);

		if (value)
		{
			strncpy_s(it.value, sizeof(it.value), value, _TRUNCATE);
		}
		else
		{
			it.value[0] = 0;
		}

		it.accent = accent;
		it.titleColor = titleColor;
		it.valueColor = valueColor;
	}

	void AddSection(const char* title, COLORREF accent)
	{
		AddItem(ITEM_HEADER, title, nullptr, accent, accent, accent);
	}

	int ListContentH()
	{
		if (s_itemCount == 0)
		{
			return 0;
		}

		int h = 0;

		for (int i = 0; i < s_itemCount; ++i)
		{
			h += ItemStep(s_items[i].kind);
		}

		int last = s_items[s_itemCount - 1].kind;
		return h - (ItemStep(last) - ItemHeight(last));
	}

	int ListMaxOffset()
	{
		int m = ListContentH() - (s_listArea.bottom - s_listArea.top);
		return m > 0 ? m : 0;
	}

	void ListClamp()
	{
		int maxOff = ListMaxOffset();

		if (s_listOffset > maxOff) s_listOffset = maxOff;
		if (s_listOffset < 0) s_listOffset = 0;
	}

	void DrawSectionHeader(HDC hdc, const RECT& rc, const char* title, COLORREF accent)
	{
		RECT bar = {
			rc.left + 2,
			rc.top + 4,
			rc.left + 2 + LST_ACCENT_W,
			rc.bottom - 4
		};

		FillRectColor(hdc, bar, accent);

		FontScope f(hdc, s_fBody);
		SetBkMode(hdc, TRANSPARENT);
		SetTextColor(hdc, accent);

		RECT tr = { bar.right + 8, rc.top, rc.right - 8, rc.bottom };
		DrawTextA(hdc, title, -1, &tr,
			DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

		HLine(hdc, rc.left + 2, rc.right - 2, rc.bottom - 1, CLR_PANEL_BORDER);
	}

	void DrawEmptyText(HDC hdc, const RECT& rc, const char* text)
	{
		FontScope f(hdc, s_fBody);
		SetBkMode(hdc, TRANSPARENT);
		SetTextColor(hdc, CLR_TEXT_DIM);

		RECT tr = { rc.left + 10, rc.top, rc.right, rc.bottom };
		DrawTextA(hdc, text, -1, &tr,
			DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
	}

	// Card compacto: faixa de destaque, titulo a esquerda e valor opcional a direita.
	void DrawListCard(HDC hdc, const RECT& rc, COLORREF accent, const char* title,
		const char* value, COLORREF titleColor, COLORREF valueColor)
	{
		RoundPanel(hdc, rc, CLR_PANEL, CLR_PANEL_BORDER, 8);

		RECT bar = {
			rc.left + 2,
			rc.top + 6,
			rc.left + 2 + LST_ACCENT_W,
			rc.bottom - 6
		};

		FillRectColor(hdc, bar, accent);

		FontScope f(hdc, s_fBody);
		SetBkMode(hdc, TRANSPARENT);

		bool hasValue = value && value[0] != 0;

		RECT tr = {
			bar.right + 10,
			rc.top,
			hasValue ? rc.right - 110 : rc.right - 10,
			rc.bottom
		};

		SetTextColor(hdc, titleColor);
		DrawTextA(hdc, title, -1, &tr,
			DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

		if (hasValue)
		{
			RECT vr = { rc.right - 110, rc.top, rc.right - 10, rc.bottom };

			SetTextColor(hdc, valueColor);
			DrawTextA(hdc, value, -1, &vr,
				DT_RIGHT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
		}
	}

	void DrawCardItem(HDC hdc, const CardItem& it, const RECT& rc)
	{
		switch (it.kind)
		{
		case ITEM_HEADER:
			DrawSectionHeader(hdc, rc, it.title, it.accent);
			break;

		case ITEM_EMPTY:
			DrawEmptyText(hdc, rc, it.title);
			break;

		case ITEM_MSG:
			DrawListCard(hdc, rc, it.accent, it.title, nullptr,
				it.titleColor, it.valueColor);
			break;

		default:
			DrawListCard(hdc, rc, it.accent, it.title, it.value,
				it.titleColor, it.valueColor);
			break;
		}
	}

	void DrawListScrollbar(HDC hdc)
	{
		int viewH = s_listArea.bottom - s_listArea.top;
		int contentH = ListContentH();

		if (contentH <= viewH)
		{
			return;
		}

		RECT track = {
			s_listArea.right - LST_SCROLL_W - 2,
			s_listArea.top + 2,
			s_listArea.right - 2,
			s_listArea.bottom - 2
		};

		int trackH = track.bottom - track.top;
		int thumbH = viewH * trackH / contentH;

		if (thumbH < 18) thumbH = 18;
		if (thumbH > trackH) thumbH = trackH;

		int maxOff = ListMaxOffset();
		int thumbTop = track.top;

		if (maxOff > 0)
		{
			thumbTop += (trackH - thumbH) * s_listOffset / maxOff;
		}

		RECT thumb = { track.left, thumbTop, track.right, thumbTop + thumbH };

		FillRectColor(hdc, track, CLR_PANEL_BORDER);
		FillRectColor(hdc, thumb, CLR_TEXT_DIM);
	}

	// Desenha somente os itens visiveis da lista, com clip e barra de rolagem.
	void DrawCardList(HDC hdc)
	{
		ListClamp();

		if (s_itemCount == 0)
		{
			return;
		}

		{
			ClipScope clip(hdc, s_listArea);

			int left = s_listArea.left + LST_PAD;
			int right = s_listArea.right - LST_PAD - LST_SCROLL_W - 4;
			int y = s_listArea.top - s_listOffset;

			for (int i = 0; i < s_itemCount; ++i)
			{
				const CardItem& it = s_items[i];
				int h = ItemHeight(it.kind);

				if (y > s_listArea.bottom)
				{
					break;
				}

				if (y + h >= s_listArea.top)
				{
					RECT rc = { left, y, right, y + h };
					DrawCardItem(hdc, it, rc);
				}

				y += ItemStep(it.kind);
			}
		}

		DrawListScrollbar(hdc);
	}

	// Rolagem do card unico e do SERVER LOG.
	bool ListWheel(const POINT& pt, int delta)
	{
		int step = delta * LST_STEP_WHEEL / WHEEL_DELTA;

		if (PtInRect(&s_listArea, pt))
		{
			s_listOffset -= step;
			ListClamp();
			return true;
		}

		if (PtInRect(&s_logScrollArea, pt))
		{
			// Roda para cima: mostra linhas mais antigas do SERVER LOG.
			s_logScrollOffset += step;

			if (s_logScrollOffset < 0)
			{
				s_logScrollOffset = 0;
			}

			if (s_logScrollOffset > s_logMaxOffset)
			{
				s_logScrollOffset = s_logMaxOffset;
			}

			return true;
		}

		return false;
	}

	// ===================== Linhas de log (de baixo para cima) =====================
	bool LogPanelVisible(const RECT& rc)
	{
		return rc.bottom - rc.top >= SD_PANEL_HDR + s_lineH + 8;
	}

	// getLine(i, text, color) devolve false quando nao ha mais linhas.
	// scrollOffset diferente de nullptr habilita rolagem e barra de rolagem.
	template <typename GetLine>
	void PaintLogBottomUp(HDC hdc, const RECT& rc, const char* title,
		COLORREF accent, GetLine getLine, int* scrollOffset)
	{
		if (!LogPanelVisible(rc))
		{
			if (scrollOffset)
			{
				*scrollOffset = 0;
				s_logMaxOffset = 0;
			}

			return;
		}

		DrawPanelDecor(hdc, rc, title, accent);

		RECT body = {
			rc.left + 1,
			rc.top + SD_PANEL_HDR + 1,
			rc.right - 1,
			rc.bottom - 1
		};

		int visibleH = body.bottom - body.top;
		int totalLines = 0;

		// Conta as linhas para calcular o limite da rolagem.
		for (int i = 0; ; ++i)
		{
			const char* text = nullptr;
			COLORREF color = CLR_TEXT;

			if (!getLine(i, text, color))
			{
				break;
			}

			++totalLines;
		}

		int contentH = totalLines * s_lineH;
		int maxOffset = contentH - visibleH;

		if (maxOffset < 0)
		{
			maxOffset = 0;
		}

		bool scrollable = (scrollOffset != nullptr);
		bool needScroll = scrollable && maxOffset > 0;
		int offset = 0;

		if (scrollOffset)
		{
			s_logMaxOffset = maxOffset;

			if (maxOffset == 0)
			{
				*scrollOffset = 0;
			}
			else
			{
				if (*scrollOffset > maxOffset) *scrollOffset = maxOffset;
				if (*scrollOffset < 0) *scrollOffset = 0;
			}

			offset = *scrollOffset;
		}

		RECT track = {};

		if (needScroll)
		{
			track = {
				body.right - LST_SCROLL_W - 4,
				body.top + 4,
				body.right - 4,
				body.bottom - 4
			};
		}

		int textRight = needScroll ? track.left - 5 : body.right - 8;

		{
			ClipScope clip(hdc, body);
			FontScope f(hdc, s_fBody);
			SetBkMode(hdc, TRANSPARENT);

			int y = body.bottom - s_lineH - 4 + offset;

			for (int i = 0; i < totalLines; ++i)
			{
				const char* text = nullptr;
				COLORREF color = CLR_TEXT;

				if (!getLine(i, text, color))
				{
					break;
				}

				if (y + s_lineH < body.top)
				{
					break;
				}

				if (y < body.bottom && y + s_lineH > body.top)
				{
					SetTextColor(hdc, color);

					RECT lr = {
						body.left + 10,
						y,
						textRight,
						y + s_lineH
					};

					DrawTextA(hdc, text, -1, &lr,
						DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
				}

				y -= s_lineH;
			}
		}

		// Barra de rolagem do SERVER LOG.
		if (needScroll)
		{
			int trackH = track.bottom - track.top;
			int thumbH = visibleH * trackH / contentH;

			if (thumbH < 18) thumbH = 18;
			if (thumbH > trackH) thumbH = trackH;

			int thumbTop = track.top;

			if (maxOffset > 0)
			{
				// Offset zero e a posicao mais recente, na parte inferior da barra.
				thumbTop += (trackH - thumbH) * (maxOffset - offset) / maxOffset;
			}

			RECT thumb = { track.left, thumbTop, track.right, thumbTop + thumbH };

			FillRectColor(hdc, track, CLR_PANEL_BORDER);
			FillRectColor(hdc, thumb, CLR_TEXT_DIM);
		}
	}

	// ===================== Layout =====================
	RECT StatCardRect(int i)
	{
		int inner = s_W - SD_MARGIN * 2 - SD_GAP * 2;
		int cw = inner / 3;

		RECT rc;
		rc.left = SD_MARGIN + i * (cw + SD_GAP);
		rc.right = rc.left + cw;

		if (i == 2)
		{
			rc.right = s_W - SD_MARGIN;
		}

		rc.top = SD_CARD_Y;
		rc.bottom = SD_CARD_Y + SD_CARD_H;

		return rc;
	}

	// Coluna direita: CARD UNICO no topo e CONNECTION LOG abaixo.
	void ComputeLayout(int W, int H)
	{
		int sideX = W - SD_MARGIN - SD_SIDE_W;
		int right = W - SD_MARGIN;
		int bottom = H - SD_MARGIN;

		s_rcLog = { SD_MARGIN, SD_BODY_Y, sideX - SD_GAP, bottom };
		s_rcUnique = { sideX, SD_BODY_Y, right, SD_BODY_Y + SD_MAIN_CARD_H };
		s_rcConnect = { sideX, s_rcUnique.bottom + SD_GAP, right, bottom };

		s_hitUnique = {
			s_rcUnique.left,
			s_rcUnique.top,
			s_rcUnique.right,
			s_rcUnique.top + SD_PANEL_HDR
		};

		s_logScrollArea = {
			s_rcLog.left + 1,
			s_rcLog.top + SD_PANEL_HDR + 1,
			s_rcLog.right - 1,
			s_rcLog.bottom - 1
		};
	}

	// Area da lista dentro do card unico, abaixo do cabecalho.
	RECT UniqueBodyRect()
	{
		RECT r = {
			s_rcUnique.left + 1,
			s_rcUnique.top + SD_PANEL_HDR + 4,
			s_rcUnique.right - 1,
			s_rcUnique.bottom - 1
		};

		return r;
	}

	// Monta a camada estatica: fundo, header, sombra e paineis de vidro.
	// Chamada somente quando o tamanho da janela muda.
	void BuildStaticLayer(HWND hwnd, int w, int h)
	{
		ReleaseStaticLayer();

		HDC wnd = GetDC(hwnd);
		if (!wnd)
		{
			return;
		}

		s_staticDC = CreateCompatibleDC(wnd);

		if (!s_staticDC)
		{
			ReleaseDC(hwnd, wnd);
			return;
		}

		s_staticBmp = CreateCompatibleBitmap(wnd, w, h);

		if (!s_staticBmp)
		{
			DeleteDC(s_staticDC);
			s_staticDC = nullptr;
			ReleaseDC(hwnd, wnd);
			return;
		}

		s_staticOldBmp = SelectObject(s_staticDC, s_staticBmp);
		s_staticW = w;
		s_staticH = h;

		HDC blurDC = CreateCompatibleDC(wnd);
		HBITMAP blurBmp = CreateCompatibleBitmap(wnd, w, h);

		ReleaseDC(hwnd, wnd);

		if (!blurDC || !blurBmp)
		{
			if (blurDC) DeleteDC(blurDC);
			if (blurBmp) DeleteObject(blurBmp);
			ReleaseStaticLayer();
			return;
		}

		HGDIOBJ blurOld = SelectObject(blurDC, blurBmp);

		BuildBlurSource(blurDC, w, h);
		s_blurDC = blurDC;

		HDC hdc = s_staticDC;

		// Altura de linha para as listas e para a visibilidade dos paineis de log.
		{
			FontScope f(hdc, s_fBody);
			TEXTMETRIC tm = {};
			GetTextMetrics(hdc, &tm);
			s_lineH = tm.tmHeight + 4;
		}

		RECT all = { 0, 0, w, h };
		DrawWindowBackground(hdc, all);

		// Painel superior com a imagem do header; sem logo ou textos.
		RECT header = { 0, 0, w, SD_HEADER_H };
		DrawHeaderBackground(hdc, header);
		DrawHeaderShadow(hdc, w, SD_HEADER_H);

		for (int i = 0; i < 3; ++i)
		{
			DrawGlassPanel(hdc, StatCardRect(i), CLR_PANEL_BORDER, 10);
		}

		if (LogPanelVisible(s_rcLog))
		{
			DrawGlassPanel(hdc, s_rcLog, CLR_PANEL_BORDER, SD_PANEL_RADIUS);
		}

		if (LogPanelVisible(s_rcConnect))
		{
			DrawGlassPanel(hdc, s_rcConnect, CLR_PANEL_BORDER, SD_PANEL_RADIUS);
		}

		DrawGlassPanel(hdc, s_rcUnique, CLR_PANEL_BORDER, SD_PANEL_RADIUS);

		s_blurDC = nullptr;

		SelectObject(blurDC, blurOld);
		DeleteObject(blurBmp);
		DeleteDC(blurDC);
	}

	// Moldura dinamica do card unico, cabecalho com submenu e lista da categoria atual.
	void DrawUniqueCard(HDC hdc, int cat)
	{
		DrawPanelDecor(hdc, s_rcUnique, CategoryTitle(cat), CategoryAccent(cat));
		DrawMenuChevron(hdc, s_rcUnique);

		s_listArea = UniqueBodyRect();
		DrawCardList(hdc);
	}

	// Atualiza uma parte da status bar somente quando o texto muda.
	void SetStatusText(HWND hStatus, int part, const char* text)
	{
		if (strcmp(s_statusCache[part], text) == 0)
		{
			return;
		}

		strcpy_s(s_statusCache[part], sizeof(s_statusCache[part]), text);
		SendMessage(hStatus, SB_SETTEXT, part, (LPARAM)text);
	}

} // namespace

// ===================== CServerDisplayer =====================

CServerDisplayer::CServerDisplayer()
{
	m_hwnd = nullptr;
	m_hBitmap = nullptr;

	strcpy_s(m_DisplayerText[0], sizeof(m_DisplayerText[0]), "STANDBY MODE");
	strcpy_s(m_DisplayerText[1], sizeof(m_DisplayerText[1]), "ACTIVE MODE");

	m_font = MakeFont(-22, FW_BOLD);
	m_font2 = MakeFont(-18, FW_SEMIBOLD);
	m_font3 = MakeFont(-13, FW_NORMAL);

	memset(m_log, 0, sizeof(m_log));
	memset(m_logConnect, 0, sizeof(m_logConnect));
	memset(m_logGlobal, 0, sizeof(m_logGlobal));

	m_count = 0;
	m_countConnect = 0;
	m_countGlobal = 0;

	EventCs = 0;
	EventCsState = 0;
	EventCastleDeep = 0;
	EventCryWolf = 0;
	EventCryWolfState = 0;

	s_statusInit = false;
}

CServerDisplayer::~CServerDisplayer()
{
	ReleaseStaticLayer();
	ReleaseFrameBuffer();
	ReleaseBackgroundImages();

	DeleteObject(m_font);
	DeleteObject(m_font2);
	DeleteObject(m_font3);

	if (m_hBitmap)
	{
		DeleteObject(m_hBitmap);
	}
}

void CServerDisplayer::Init(HWND hWnd)
{
	m_hwnd = hWnd;

	// Título: definido uma única vez
#if(GAMESERVER_TYPE==0)
	std::string title = std::string("GameServer | XML ") + VERSION;
#else
	std::string title = std::string("GameServerCS | XML ") + VERSION;
#endif
	SetWindowText(m_hwnd, title.c_str());
}

void CServerDisplayer::Run()
{
	if (!m_hwnd)
	{
		return;
	}

	RECT client = {};
	GetClientRect(m_hwnd, &client);

	int width = client.right - client.left;
	int height = client.bottom - client.top;

	// Desconta a barra de status da area de desenho.
	HWND hStatus = GetDlgItem(m_hwnd, IDC_STATUSBAR);

	if (hStatus)
	{
		RECT sb = {};
		GetWindowRect(hStatus, &sb);

		POINT p = { sb.left, sb.top };
		ScreenToClient(m_hwnd, &p);

		if (p.y > 0 && p.y < height)
		{
			height = p.y;
		}
	}

	if (width < SD_MIN_W || height < SD_MIN_H)
	{
		return;
	}

	LoadBackgroundImages();
	EnsureFrameBuffer(m_hwnd, width, height);

	if (!s_memDC)
	{
		return;
	}

	s_W = width;
	s_fTitle = m_font;
	s_fValue = m_font2;
	s_fBody = m_font3;

	ComputeLayout(width, height);

	// Camada estatica: so e refeita quando o tamanho muda.
	if (!s_staticDC || s_staticW != width || s_staticH != height)
	{
		BuildStaticLayer(m_hwnd, width, height);

		if (!s_staticDC)
		{
			return;
		}
	}

	// Copia a camada estatica; depois desenha somente o conteudo dinamico.
	BitBlt(s_memDC, 0, 0, width, height, s_staticDC, 0, 0, SRCCOPY);

	s_frame = s_memDC;
	SetBkMode(s_frame, TRANSPARENT);

	// O header nao desenha logo, nome, versao ou update.
	PaintAllInfo();
	PaintOnline();
	PaintPremium();
	PaintSeason();

	// Monta a lista do card unico conforme a categoria atual.
	int cat = EffectiveCategory();
	s_itemCount = 0;

	if (cat == SD_CAT_ALL)
	{
		AddSection("EVENTS", CLR_ACCENT);
		PaintEventTime();

		if (GAMESERVER_TYPE == 0)
		{
			AddSection("CUSTOM ARENA", CLR_PURPLE);
			PaintCustomArenaTime();

			AddSection("INVASION", CLR_ORANGE);
			PaintInvasionTime();
		}

		AddSection("GLOBAL MESSAGE", CLR_SUCCESS);
		LogTextPaintGlobalMessage();
	}
	else
	{
		switch (cat)
		{
		case SD_CAT_EVENTS:
			PaintEventTime();
			break;

		case SD_CAT_ARENA:
			PaintCustomArenaTime();
			break;

		case SD_CAT_INVASION:
			PaintInvasionTime();
			break;

		default:
			LogTextPaintGlobalMessage();
			break;
		}
	}

	DrawUniqueCard(s_frame, cat);

	LogTextPaint();
	LogTextPaintConnect();

	HDC hdc = GetDC(m_hwnd);

	if (hdc)
	{
		BitBlt(hdc, 0, 0, width, height, s_frame, 0, 0, SRCCOPY);
		ReleaseDC(m_hwnd, hdc);
	}

	s_frame = nullptr;
}

// Mantido vazio porque PaintName faz parte da interface definida em ServerDisplayer.h.
// O header nao exibe nome do servidor, versao ou update.
void CServerDisplayer::PaintName()
{
}

void CServerDisplayer::PaintAllInfo()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	bool standby = gJoinServerConnection.CheckState() == 0 ||
		gDataServerConnection.CheckState() == 0;

	if (!standby)
	{
		gGameServerDisconnect = 0;
	}

	const char* text = standby ? m_DisplayerText[0] : m_DisplayerText[1];
	COLORREF fg = standby ? CLR_TEXT_DIM : CLR_SUCCESS;
	COLORREF bg = standby ? CLR_PILL_OFF_BG : CLR_PILL_ON_BG;
	COLORREF bd = standby ? CLR_PANEL_BORDER : CLR_SUCCESS;

	FontScope f(hdc, s_fBody);

	SIZE sz = {};
	GetTextExtentPoint32A(hdc, text, (int)strlen(text), &sz);

	int pillW = sz.cx + 28;

	RECT pill = {
		s_W - SD_MARGIN - pillW,
		20,
		s_W - SD_MARGIN,
		48
	};

	RoundPanel(hdc, pill, bg, bd, 14);

	SetBkMode(hdc, TRANSPARENT);
	SetTextColor(hdc, fg);

	DrawTextA(hdc, text, -1, &pill,
		DT_CENTER | DT_VCENTER | DT_SINGLELINE);
}

void CServerDisplayer::PaintOnline()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	char value[32];

	sprintf_s(value, sizeof(value), "%d / %d",
		gObjTotalUser, gServerInfo.m_ServerMaxUserNumber);

	RECT rc = StatCardRect(0);

	DrawStatCardContent(hdc, rc, "ONLINE", value,
		gObjTotalUser > 0 ? CLR_SUCCESS : CLR_TEXT, CLR_ACCENT);
}

void CServerDisplayer::PaintPremium()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	RECT rc = StatCardRect(1);

	DrawStatCardContent(hdc, rc, "SERVER", GAMESERVER_NAME,
		CLR_ORANGE, CLR_ORANGE);
}

void CServerDisplayer::PaintSeason()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	RECT rc = StatCardRect(2);

	DrawStatCardContent(hdc, rc, "SEASON", GAMESERVER_SEASON,
		CLR_PURPLE, CLR_PURPLE);
}

// Itens da categoria EVENTS: eventos globais ou status do Castle Siege.
void CServerDisplayer::PaintEventTime()
{
	if (GAMESERVER_TYPE == 0)
	{
		int shown = 0;

		for (int n = 0; n < SD_EVENT_SLOTS; ++n)
		{
			if (!gEventName.IsGlobal(n))
			{
				continue;
			}

			int sec = gEventName.GlobalRemainTime(n);
			char remain[32];

			FormatRemainTime(sec, remain, sizeof(remain));

			AddItem(ITEM_ROW, gEventName.GlobalName(n), remain,
				CLR_ACCENT, CLR_TEXT, RemainColor(sec));

			++shown;
		}

		if (shown == 0)
		{
			AddItem(ITEM_EMPTY, "No active entries", nullptr,
				CLR_TEXT_DIM, CLR_TEXT_DIM, CLR_TEXT_DIM);
		}

		return;
	}

	// Modo Castle Siege.
	char v[32];

	FormatRemainTime(EventCastleDeep, v, sizeof(v));
	AddItem(ITEM_ROW, "Loren Deep", v,
		CLR_INFO, CLR_TEXT, RemainColor(EventCastleDeep));

	FormatRemainTime(EventCryWolf, v, sizeof(v));
	AddItem(ITEM_ROW, "CryWolf", v,
		CLR_INFO, CLR_TEXT, RemainColor(EventCryWolf));

	if (EventCs == -1)
	{
		AddItem(ITEM_ROW, "Castle Siege", "Disabled",
			CLR_INFO, CLR_TEXT, CLR_DANGER);
	}
	else if (EventCs == 0)
	{
		AddItem(ITEM_ROW, "Castle Siege", "Adjust Date",
			CLR_INFO, CLR_TEXT, CLR_WARNING);
	}
	else
	{
		sprintf_s(v, sizeof(v), "Stage %d", EventCsState);

		AddItem(ITEM_ROW, "Castle Siege", v,
			CLR_INFO, CLR_TEXT, CLR_INFO);
		AddItem(ITEM_ROW, "Stage", CsStageName(EventCsState),
			CLR_INFO, CLR_TEXT, CLR_TEXT);
		AddItem(ITEM_ROW, "Next stage", CsStageName(EventCsState + 1),
			CLR_INFO, CLR_TEXT, CLR_TEXT_DIM);
	}
}

// Itens da categoria CUSTOM ARENA: arenas ativas.
void CServerDisplayer::PaintCustomArenaTime()
{
	int shown = 0;

	for (int n = 0; n < SD_ARENA_SLOTS; ++n)
	{
		if (!EvtArenaActive(n))
		{
			continue;
		}

		int sec = EvtArenaRemain(n);
		char v[32];

		FormatRemainTime(sec, v, sizeof(v));

		AddItem(ITEM_ROW, EvtArenaName(n), v,
			CLR_PURPLE, CLR_TEXT, RemainColor(sec));

		++shown;
	}

	if (shown == 0)
	{
		AddItem(ITEM_EMPTY, "No active entries", nullptr,
			CLR_TEXT_DIM, CLR_TEXT_DIM, CLR_TEXT_DIM);
	}
}

// Itens da categoria INVASION: invasoes ativas.
void CServerDisplayer::PaintInvasionTime()
{
	int shown = 0;

	for (int n = 0; n < SD_INVASION_SLOTS; ++n)
	{
		if (!EvtInvasionActive(n))
		{
			continue;
		}

		int sec = EvtInvasionRemain(n);
		char v[32];

		FormatRemainTime(sec, v, sizeof(v));

		AddItem(ITEM_ROW, EvtInvasionName(n), v,
			CLR_ORANGE, CLR_TEXT, RemainColor(sec));

		++shown;
	}

	if (shown == 0)
	{
		AddItem(ITEM_EMPTY, "No active entries", nullptr,
			CLR_TEXT_DIM, CLR_TEXT_DIM, CLR_TEXT_DIM);
	}
}

void CServerDisplayer::LogTextPaint()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	PaintLogBottomUp(hdc, s_rcLog, "SERVER LOG", CLR_ACCENT,
		[this](int i, const char*& text, COLORREF& color) -> bool
		{
			if (i >= MAX_LOG_TEXT_LINE)
			{
				return false;
			}

			int k = RingIndex(m_count, i, MAX_LOG_TEXT_LINE);

			if (m_log[k].text[0] == 0)
			{
				return false;
			}

			text = m_log[k].text;
			color = LogColorOf(m_log[k].color);
			return true;
		}, &s_logScrollOffset);
}

void CServerDisplayer::LogTextPaintConnect()
{
	HDC hdc = s_frame;

	if (!hdc)
	{
		return;
	}

	PaintLogBottomUp(hdc, s_rcConnect, "CONNECTION LOG", CLR_INFO,
		[this](int i, const char*& text, COLORREF& color) -> bool
		{
			if (i >= MAX_LOGCONNECT_TEXT_LINE)
			{
				return false;
			}

			int k = RingIndex(m_countConnect, i, MAX_LOGCONNECT_TEXT_LINE);

			if (m_logConnect[k].text[0] == 0)
			{
				return false;
			}

			text = m_logConnect[k].text;
			color = LogColorOf(m_logConnect[k].color);
			return true;
		}, nullptr);
}

// Itens da categoria GLOBAL MESSAGE: mais recente primeiro.
void CServerDisplayer::LogTextPaintGlobalMessage()
{
	int count = 0;

	for (int i = 0; i < MAX_LOGGLOBAL_TEXT_LINE; ++i)
	{
		int k = RingIndex(m_countGlobal, i, MAX_LOGGLOBAL_TEXT_LINE);

		if (m_logGlobal[k].text[0] == 0)
		{
			break;
		}

		AddItem(ITEM_MSG, m_logGlobal[k].text, nullptr,
			CLR_SUCCESS, CLR_SUCCESS, CLR_SUCCESS);

		++count;
	}

	if (count == 0)
	{
		AddItem(ITEM_EMPTY, "No messages", nullptr,
			CLR_TEXT_DIM, CLR_TEXT_DIM, CLR_TEXT_DIM);
	}
}

void CServerDisplayer::LogAddText(eLogColor color, char* text, int size)
{
	if (size > (MAX_LOG_TEXT_SIZE - 1))
	{
		size = MAX_LOG_TEXT_SIZE - 1;
	}

	if (size < 0)
	{
		size = 0;
	}

	gLog.Output(LOG_GENERAL, "%s", &text[9]);

	int idx = m_count;

	memset(m_log[idx].text, 0, MAX_LOG_TEXT_SIZE);
	memcpy(m_log[idx].text, text, size);
	m_log[idx].color = color;

	m_count = (idx + 1) % MAX_LOG_TEXT_LINE;
}

void CServerDisplayer::LogAddTextConnect(eLogColor color, char* text, int size)
{
	if (size > (MAX_LOGCONNECT_TEXT_SIZE - 1))
	{
		size = MAX_LOGCONNECT_TEXT_SIZE - 1;
	}

	if (size < 0)
	{
		size = 0;
	}

	gLog.Output(LOG_GENERAL, "%s", &text[9]);

	int idx = m_countConnect;

	memset(m_logConnect[idx].text, 0, MAX_LOGCONNECT_TEXT_SIZE);
	memcpy(m_logConnect[idx].text, text, size);
	m_logConnect[idx].color = color;

	m_countConnect = (idx + 1) % MAX_LOGCONNECT_TEXT_LINE;
}

void CServerDisplayer::LogAddTextGlobal(eLogColor color, char* text, int size)
{
	if (size > (MAX_LOGGLOBAL_TEXT_SIZE - 1))
	{
		size = MAX_LOGGLOBAL_TEXT_SIZE - 1;
	}

	if (size < 0)
	{
		size = 0;
	}

	int idx = m_countGlobal;

	memset(m_logGlobal[idx].text, 0, MAX_LOGGLOBAL_TEXT_SIZE);
	memcpy(m_logGlobal[idx].text, text, size);
	m_logGlobal[idx].color = color;

	m_countGlobal = (idx + 1) % MAX_LOGGLOBAL_TEXT_LINE;
}

// Chamado pelo GameServer.cpp em WM_MOUSEWHEEL.
// WM_MOUSEWHEEL entrega coordenadas em TELA; convertemos para cliente aqui,
// para funcionar independentemente do que o GameServer.cpp enviar.
void ServerDisplayerOnWheel(HWND hWnd, int delta, int xClient, int yClient)
{
	POINT pt = { xClient, yClient };

	// Se o ponto nao cair na area do cliente, assume que veio em tela e converte.
	RECT client = {};
	GetClientRect(hWnd, &client);

	if (!PtInRect(&client, pt))
	{
		ScreenToClient(hWnd, &pt);
	}

	if (ListWheel(pt, delta))
	{
		InvalidateRect(hWnd, nullptr, FALSE);
	}
}

// Chamado pelo GameServer.cpp em WM_LBUTTONDOWN; coordenadas ja em cliente.
// Clique no cabecalho do card unico abre o submenu de categorias.
void ServerDisplayerOnClick(HWND hWnd, int xClient, int yClient)
{
	POINT pt = { xClient, yClient };

	if (!PtInRect(&s_hitUnique, pt))
	{
		return;
	}

	HMENU menu = CreatePopupMenu();

	if (!menu)
	{
		return;
	}

	int cur = EffectiveCategory();

	AppendMenuA(menu, MF_STRING | MF_GRAYED, 0, "Categories");
	AppendMenuA(menu, MF_SEPARATOR, 0, nullptr);

	AppendMenuA(menu,
		MF_STRING | (cur == SD_CAT_ALL ? MF_CHECKED : MF_UNCHECKED),
		SD_MENU_ALL, "All");

	AppendMenuA(menu,
		MF_STRING | (cur == SD_CAT_EVENTS ? MF_CHECKED : MF_UNCHECKED),
		SD_MENU_EVENTS, "Events");

	if (GAMESERVER_TYPE == 0)
	{
		AppendMenuA(menu,
			MF_STRING | (cur == SD_CAT_ARENA ? MF_CHECKED : MF_UNCHECKED),
			SD_MENU_ARENA, "Custom Arena");

		AppendMenuA(menu,
			MF_STRING | (cur == SD_CAT_INVASION ? MF_CHECKED : MF_UNCHECKED),
			SD_MENU_INVASION, "Invasion");
	}

	AppendMenuA(menu,
		MF_STRING | (cur == SD_CAT_GLOBAL ? MF_CHECKED : MF_UNCHECKED),
		SD_MENU_GLOBAL, "Global Message");

	POINT screenPt = pt;
	ClientToScreen(hWnd, &screenPt);

	UINT cmd = TrackPopupMenu(menu,
		TPM_RETURNCMD | TPM_LEFTALIGN | TPM_TOPALIGN,
		screenPt.x, screenPt.y, 0, hWnd, nullptr);

	DestroyMenu(menu);

	int newCat = -1;

	switch (cmd)
	{
	case SD_MENU_ALL:
		newCat = SD_CAT_ALL;
		break;

	case SD_MENU_EVENTS:
		newCat = SD_CAT_EVENTS;
		break;

	case SD_MENU_ARENA:
		newCat = SD_CAT_ARENA;
		break;

	case SD_MENU_INVASION:
		newCat = SD_CAT_INVASION;
		break;

	case SD_MENU_GLOBAL:
		newCat = SD_CAT_GLOBAL;
		break;

	default:
		break;
	}

	if (newCat >= 0 && newCat != s_category)
	{
		s_category = newCat;
		s_listOffset = 0;
	}

	InvalidateRect(hWnd, nullptr, FALSE);
}