// Shop.cpp: implementation of the CShop class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
//#include "ItemValue.h"
#include "Shop.h"
#include "SocketItemOption.h"
#include "SocketItemType.h"
#include "MemScript.h"
#include "User.h"
#include "Util.h"
#include "pugixml.hpp"

CShop gShop;
//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CShop::CShop() // OK
{
	this->Init();
}

CShop::~CShop() // OK
{

}

void CShop::Init() // OK
{
	for (int n = 0;n < SHOP_SIZE;n++)
	{
		this->m_Item[n].Clear();
		this->m_InventoryMap[n] = -1;
	}
}

//////////////////////////////////////////////////////////////////////
// Le até "count" numeros separados por espaco de "text".
// Valores que faltarem recebem "def".
//////////////////////////////////////////////////////////////////////

static void ReadNumbers(const char* text, int* values, int count, int def)
{
	for (int i = 0;i < count;i++)
	{
		values[i] = def;
	}

	if (text == 0)
	{
		return;
	}

	const char* p = text;

	for (int i = 0;i < count;i++)
	{
		while (*p == ' ' || *p == '\t')
		{
			p++;
		}

		if (*p == 0)
		{
			break;
		}

		char* end = 0;

		long value = strtol(p, &end, 10);

		if (end == p)
		{
			break;
		}

		values[i] = (int)value;

		p = end;
	}
}

void CShop::Load(char* path) // OK
{
	pugi::xml_document doc;

	pugi::xml_parse_result result = doc.load_file(path);

	if (!result)
	{
		char msg[512];
		wsprintf(msg, "Erro ao ler XML da loja:\n%s\n%s (offset %d)", path, result.description(), (int)result.offset);
		ErrorMessageBox(msg);
		return;
	}

	pugi::xml_node shop = doc.child("shop");

	if (!shop)
	{
		char msg[512];
		wsprintf(msg, "Tag <shop> nao encontrada em:\n%s", path);
		ErrorMessageBox(msg);
		return;
	}

	this->Init();

	for (pugi::xml_node node = shop.child("item"); node; node = node.next_sibling("item"))
	{
		// Item sem group/index nao e valido. Ignora em vez de criar a Sword Kris (0/0).
		if (!node.attribute("group") || !node.attribute("index"))
		{
			continue;
		}

		int group = node.attribute("group").as_int(0);
		int index = node.attribute("index").as_int(0);

		int ItemIndex = SafeGetItem(GET_ITEM(group, index));

		int ItemLevel = node.attribute("level").as_int(0);

		int ItemDurability = node.attribute("durability").as_int(0);

		int ItemOption1 = node.attribute("skill").as_int(0);

		int ItemOption2 = node.attribute("luck").as_int(0);

		int ItemOption3 = node.attribute("option").as_int(0);

		// "excellent" corresponde a coluna ExeOpt do txt, que e o NewOption
		int ItemNewOption = node.attribute("excellent").as_int(0);

		// extra = Anc JOH OpEx (colunas 9 a 11 do txt)
		int extra[3];
		ReadNumbers(node.attribute("extra").value(), extra, 3, 0);

		// sockets = Socket1 a Socket5 (colunas 12 a 16 do txt)
		int sockets[5];
		ReadNumbers(node.attribute("sockets").value(), sockets, 5, 255);

		this->InsertItemNew(ItemIndex, ItemLevel, ItemDurability, ItemOption1, ItemOption2, ItemOption3, ItemNewOption, extra[0], extra[1], extra[2], sockets[0], sockets[1], sockets[2], sockets[3], sockets[4], 0);
	}
}

void CShop::ShopItemSet(int slot, BYTE type) // OK
{
	if (SHOP_INVENTORY_RANGE(slot) == 0)
	{
		return;
	}

	ITEM_INFO ItemInfo;

	if (gItemManager.GetInfo(this->m_Item[slot].m_Index, &ItemInfo) == 0)
	{
		return;
	}

	int x = slot % 8;
	int y = slot / 8;

	if ((x + ItemInfo.Width) > 8 || (y + ItemInfo.Height) > 15)
	{
		return;
	}

	for (int sy = 0;sy < ItemInfo.Height;sy++)
	{
		for (int sx = 0;sx < ItemInfo.Width;sx++)
		{
			this->m_InventoryMap[(((sy + y) * 8) + (sx + x))] = type;
		}
	}
}

BYTE CShop::ShopRectCheck(int x, int y, int width, int height) // OK
{
	if ((x + width) > 8 || (y + height) > 15)
	{
		return 0xFF;
	}

	for (int sy = 0;sy < height;sy++)
	{
		for (int sx = 0;sx < width;sx++)
		{
			if (this->m_InventoryMap[(((sy + y) * 8) + (sx + x))] != 0xFF)
			{
				return 0xFF;
			}
		}
	}

	return ((y * 8) + x);
}

void CShop::InsertItem(int ItemIndex, int ItemLevel, int ItemDurability, int ItemOption1, int ItemOption2, int ItemOption3, int ItemNewOption, int ItemValue) // OK
{
	ITEM_INFO ItemInfo;

	if (gItemManager.GetInfo(ItemIndex, &ItemInfo) == 0)
	{
		return;
	}

	for (int y = 0;y < 15;y++)
	{
		for (int x = 0;x < 8;x++)
		{
			if (this->m_InventoryMap[((y * 8) + x)] == 0xFF)
			{
				BYTE slot = this->ShopRectCheck(x, y, ItemInfo.Width, ItemInfo.Height);

				if (slot != 0xFF)
				{
					this->m_Item[slot].m_Level = ItemLevel;
					this->m_Item[slot].m_Durability = (float)((ItemDurability == 0) ? gItemManager.GetItemDurability(ItemIndex, ItemLevel, ItemNewOption, 0) : ItemDurability);
					this->m_Item[slot].Convert(ItemIndex, ItemOption1, ItemOption2, ItemOption3, ItemNewOption, 0, 0, 0, 0, 0xFF);
					this->m_Item[slot].m_PcPointValue = ItemValue;
					this->ShopItemSet(slot, 1);
					return;
				}
			}
		}
	}
}

void CShop::InsertItemNew(int ItemIndex, int ItemLevel, int ItemDurability, int ItemOption1, int ItemOption2, int ItemOption3, int ItemNewOption, int Anc, int JOH, int OpEx, int Socket1, int Socket2, int Socket3, int Socket4, int Socket5, int ItemValue) // OK
{
	ITEM_INFO ItemInfo;

	if (gItemManager.GetInfo(ItemIndex, &ItemInfo) == 0)
	{
		return;
	}

	for (int y = 0;y < 15;y++)
	{
		for (int x = 0;x < 8;x++)
		{
			if (this->m_InventoryMap[((y * 8) + x)] == 0xFF)
			{
				BYTE slot = this->ShopRectCheck(x, y, ItemInfo.Width, ItemInfo.Height);

				if (slot != 0xFF)
				{
					this->m_Item[slot].m_Level = ItemLevel;
					this->m_Item[slot].m_Durability = (float)((ItemDurability == 0) ? gItemManager.GetItemDurability(ItemIndex, ItemLevel, ItemNewOption, 0) : ItemDurability);


					BYTE ItemSocketOption[MAX_SOCKET_OPTION] = { 0xFF,0xFF,0xFF,0xFF,0xFF };

					int qtd;

					if (gSocketItemType.CheckSocketItemType(ItemIndex) == 1)
					{
						qtd = gSocketItemType.GetSocketItemMaxSocket(ItemIndex);

						ItemSocketOption[0] = (BYTE)((qtd > 0) ? ((Socket1 != 255) ? Socket1 : 255) : 255);
						ItemSocketOption[1] = (BYTE)((qtd > 1) ? ((Socket2 != 255) ? Socket2 : 255) : 255);
						ItemSocketOption[2] = (BYTE)((qtd > 2) ? ((Socket3 != 255) ? Socket3 : 255) : 255);
						ItemSocketOption[3] = (BYTE)((qtd > 3) ? ((Socket4 != 255) ? Socket4 : 255) : 255);
						ItemSocketOption[4] = (BYTE)((qtd > 4) ? ((Socket5 != 255) ? Socket5 : 255) : 255);
					}


					this->m_Item[slot].Convert(ItemIndex, ItemOption1, ItemOption2, ItemOption3, ItemNewOption, Anc, JOH, OpEx, ItemSocketOption, 0xFF);

					this->m_Item[slot].m_PcPointValue = ItemValue;

					this->ShopItemSet(slot, 1);
					return;
				}
			}
		}
	}
}

bool CShop::GetItem(CItem* lpItem, int slot) // OK
{
	if (SHOP_INVENTORY_RANGE(slot) != 0)
	{
		if (this->m_Item[slot].IsItem() != 0)
		{
			(*lpItem) = this->m_Item[slot];
			return 1;
		}
	}

	return 0;
}

CItem* CShop::GetInventory()
{
	return m_Item;
}

long CShop::GetItemCount() // OK
{
	int count = 0;

	for (int n = 0;n < SHOP_SIZE;n++)
	{
		if (this->m_Item[n].IsItem() != 0)
		{
			count++;
		}
	}

	return count;
}

bool CShop::GCShopItemListSend(int aIndex) // OK
{
	BYTE send[2048];

	PMSG_SHOP_ITEM_LIST_SEND pMsg;

	pMsg.header.set(0x31, 0);

	int size = sizeof(pMsg);

	pMsg.type = 0;

	pMsg.count = 0;

	PMSG_SHOP_ITEM_LIST info;

	for (int n = 0;n < SHOP_SIZE;n++)
	{
		if (this->m_Item[n].IsItem() != 0)
		{
			info.slot = n;

			gItemManager.ItemByteConvert(info.ItemInfo, this->m_Item[n]);

			memcpy(&send[size], &info, sizeof(info));
			size += sizeof(info);

			pMsg.count++;
		}
	}

	pMsg.header.size[0] = SET_NUMBERHB(size);
	pMsg.header.size[1] = SET_NUMBERLB(size);

	memcpy(send, &pMsg, sizeof(pMsg));

	DataSend(aIndex, send, size);

	this->GCItemValueSend(aIndex);

	return 1;
}

void CShop::GCItemValueSend(int Index)
{
#if (GAMESERVER_CLIENTE_UPDATE >= 9)
	if (gObjIsConnected(Index) == false)
	{
		return;
	}

	LPOBJ lpObj = &gObj[Index];

	if ((GetTickCount() - lpObj->ShopValueSendDelay) < (DWORD)1200)
	{
		return;
	}

	BYTE send[8192];

	PMSG_ITEM_VALUE_SEND pMsg;

	pMsg.header.set(0xF3, 0xE9, 0);

	int size = sizeof(pMsg);

	pMsg.count = 0;

	ITEM_VALUE_DATA info;

	for (int n = 0;n < SHOP_SIZE;n++)
	{
		if (this->m_Item[n].IsItem() != 0)
		{
			info.index = this->m_Item[n].m_Index;

			info.level = this->m_Item[n].m_Level;

			info.newopt = this->m_Item[n].m_NewOption;

			info.type = 0;

			info.value = this->m_Item[n].m_BuyMoney;

			info.buysell = 0;

			if (this->m_Item[n].Coin1 > 0)
			{
				info.type = 1;
				info.value = this->m_Item[n].Coin1;
			}
			if (this->m_Item[n].Coin2 > 0)
			{
				info.type = 2;
				info.value = this->m_Item[n].Coin2;
			}
			if (this->m_Item[n].Coin3 > 0)
			{
				info.type = 3;
				info.value = this->m_Item[n].Coin3;
			}

			info.sellvalue = 0;

			if (this->m_Item[n].Sell > 0)
			{
				info.sellvalue = this->m_Item[n].m_SellMoney;
			}

			pMsg.count++;

			memcpy(&send[size], &info, sizeof(info));
			size += sizeof(info);
		}
	}


	for (int n = 0;n < INVENTORY_FULL_SIZE;n++)
	{
		if (lpObj->Inventory[n].IsItem() != 0)
		{
			info.index = lpObj->Inventory[n].m_Index;

			info.level = lpObj->Inventory[n].m_Level;

			info.newopt = lpObj->Inventory[n].m_NewOption;

			info.type = 0;

			if (lpObj->Inventory[n].Coin1 > 0)
			{
				info.type = 1;
			}
			if (lpObj->Inventory[n].Coin2 > 0)
			{
				info.type = 2;
			}
			if (lpObj->Inventory[n].Coin3 > 0)
			{
				info.type = 3;
			}

			info.value = lpObj->Inventory[n].m_BuyMoney;

			info.buysell = 1;

			//info.sellvalue = 0;

			if (lpObj->Inventory[n].Sell > 0)
			{
				info.sellvalue = lpObj->Inventory[n].m_SellMoney;
			}
			else
			{
				info.sellvalue = 0;
			}

			pMsg.count++;

			memcpy(&send[size], &info, sizeof(info));
			size += sizeof(info);
		}
	}
#if(GAMESERVER_UPDATE>=701)

	if (lpObj->Inventory[236].IsItem() != 0)
	{
		info.index = lpObj->Inventory[236].m_Index;

		info.level = lpObj->Inventory[236].m_Level;

		info.newopt = lpObj->Inventory[236].m_NewOption;

		info.type = 0;

		if (lpObj->Inventory[236].Coin1 > 0)
		{
			info.type = 1;
		}
		if (lpObj->Inventory[236].Coin2 > 0)
		{
			info.type = 2;
		}
		if (lpObj->Inventory[236].Coin3 > 0)
		{
			info.type = 3;
		}

		info.value = lpObj->Inventory[236].m_BuyMoney;

		info.buysell = 1;

		//info.sellvalue = 0;

		if (lpObj->Inventory[236].Sell > 0)
		{
			info.sellvalue = lpObj->Inventory[236].m_SellMoney;
		}
		else
		{
			info.sellvalue = 0;
		}

		pMsg.count++;

		memcpy(&send[size], &info, sizeof(info));
		size += sizeof(info);
	}
#endif

	pMsg.header.size[0] = SET_NUMBERHB(size);
	pMsg.header.size[1] = SET_NUMBERLB(size);
	// ---
	memcpy(send, &pMsg, sizeof(pMsg));

	DataSend(Index, send, size);

	lpObj->ShopValueSendDelay = GetTickCount();

#endif
	return;
}