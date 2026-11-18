// ShopManager.cpp: implementation of the CShopManager class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "ShopManager.h"
#include "CastleSiegeSync.h"
#include "MemScript.h"
#include "NpcTalk.h"
#include "Path.h"
#include "Util.h"

CShopManager gShopManager;

//////////////////////////////////////////////////////////////////////
// Lista de arquivos das lojas (sem extensao)
// Para alterar uma loja, edite o nome aqui.
// Para acrescentar uma loja, inclua uma linha e o indice correspondente
// deve existir no arquivo de configuracao de shops (CShopManager::Load).
//////////////////////////////////////////////////////////////////////

struct SHOP_FILE_ENTRY
{
	int Index;
	const char* Name;
};

static const SHOP_FILE_ENTRY SHOP_FILE_LIST[] =
{
	{   0, "000 - Hanzo the Blacksmith"          },
	{   1, "001 - Liaman the Barmaid"            },
	{   2, "002 - Pasi the Wizard"               },
	{   3, "003 - Wandering Merchant Harold"     },
	{   4, "004 - Wandering Merchant Martin"     },
	{   5, "005 - Potion Girl Amy"               },
	{   6, "006 - Lumen the Barmaid"             },
	{   7, "007 - Wizard Isabel"                 },
	{   8, "008 - Weapon Merchant Zienna"        },
	{   9, "009 - Elf Ellio"                     },
	{  10, "010 - Fairy Lala"                    },
	{  11, "011 - Alex"                          },
	{  12, "012 - Merchant Thompson"             },
	{  13, "013 - Shopkeeper Pamela"             },
	{  14, "014 - Shopkeeper Angella"            },
	{  15, "015 - Merchant Natasha"              },
	{  16, "016 - Sivia"                         },
	{  17, "017 - Leah"                          },
	{  18, "018 - Marseille"                     },
	{  19, "019 - Merchant Moss"                 },
	{  20, "020 - Shopkeeper Christine"          },
	{  21, "021 - Arena Guard"                   },
	{  22, "022 - Wizard Reina"                  },
	{  23, "023 - Merchant Rehina"               },
	{  24, "024 - Merchant Volo"                 },
};

// Extensao usada por todos os arquivos da lista acima
static const char SHOP_FILE_EXT[] = ".xml";

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CShopManager::CShopManager() // OK
{
	this->m_ShopManagerInfo.clear();
}

CShopManager::~CShopManager() // OK
{

}

void CShopManager::Load(char* path) // OK
{
	CMemScript* lpMemScript = new CMemScript;

	if (lpMemScript == 0)
	{
		ErrorMessageBox(MEM_SCRIPT_ALLOC_ERROR, path);
		return;
	}

	if (lpMemScript->SetBuffer(path) == 0)
	{
		ErrorMessageBox(lpMemScript->GetLastError());
		delete lpMemScript;
		return;
	}

	this->m_ShopManagerInfo.clear();

	try
	{
		while (true)
		{
			if (lpMemScript->GetToken() == TOKEN_END)
			{
				break;
			}

			if (strcmp("end", lpMemScript->GetString()) == 0)
			{
				break;
			}

			SHOP_MANAGER_INFO info;

			info.Index = lpMemScript->GetNumber();

			info.MonsterClass = lpMemScript->GetAsNumber();

			info.Map = lpMemScript->GetAsNumber();

			info.X = lpMemScript->GetAsNumber();

			info.Y = lpMemScript->GetAsNumber();

			this->m_ShopManagerInfo.insert(std::pair<int, SHOP_MANAGER_INFO>(info.Index, info));
		}
	}
	catch (...)
	{
		ErrorMessageBox(lpMemScript->GetLastError());
	}

	delete lpMemScript;
}

void CShopManager::LoadShop() // OK
{
	int count = sizeof(SHOP_FILE_LIST) / sizeof(SHOP_FILE_LIST[0]);

	for (int n = 0; n < count; n++)
	{
		std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.find(SHOP_FILE_LIST[n].Index);

		if (it == this->m_ShopManagerInfo.end())
		{
			continue;
		}

		char path[MAX_PATH];

		wsprintf(path, "Shop\\%s%s", SHOP_FILE_LIST[n].Name, SHOP_FILE_EXT);

		it->second.Shop.Load(gPath.GetFullPath(path));
	}
}

void CShopManager::ReloadShopInterface() // OK
{
	for (int n = OBJECT_START_USER; n < MAX_OBJECT; n++)
	{
		if (gObjIsConnectedGP(n) != 0)
		{
			if (gObj[n].Interface.use != 0 && gObj[n].Interface.type == INTERFACE_SHOP)
			{
				gObj[n].Interface.state = 1;

				PMSG_NPC_TALK_SEND pMsg;

				pMsg.header.setE(0x30, sizeof(pMsg));

				pMsg.result = 0;

				DataSend(n, (BYTE*)&pMsg, pMsg.header.size);

				this->GCShopItemListSendByIndex(gObj[n].TargetShopNumber, n);

				GCTaxInfoSend(n, 2, gCastleSiegeSync.GetTaxRateStore(n));
			}
		}
	}
}

long CShopManager::GetShopNumber(int MonsterClass, int Map, int X, int Y) // OK
{
	for (std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.begin(); it != this->m_ShopManagerInfo.end(); it++)
	{
		if (it->second.MonsterClass == MonsterClass && it->second.Map == Map && it->second.X == X && it->second.Y == Y)
		{
			return it->second.Index;
		}

	}
	for (std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.begin(); it != this->m_ShopManagerInfo.end(); it++)
	{
		if (it->second.MonsterClass != -1 && it->second.MonsterClass == MonsterClass)
		{
			return it->second.Index;

		}
	}

	return -1;
}

bool CShopManager::GetItemByIndex(int index, CItem* lpItem, int slot) // OK
{
	std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.find(index);

	if (it == this->m_ShopManagerInfo.end())
	{
		return 0;
	}
	else
	{
		return it->second.Shop.GetItem(lpItem, slot);
	}
}

bool CShopManager::GetItemByMonsterClass(int MonsterClass, CItem* lpItem, int slot) // OK
{
	for (std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.begin(); it != this->m_ShopManagerInfo.end(); it++)
	{
		if (it->second.MonsterClass != -1 && it->second.MonsterClass == MonsterClass)
		{
			return it->second.Shop.GetItem(lpItem, slot);
		}
	}

	return 0;
}

long CShopManager::GetItemCountByIndex(int index) // OK
{
	std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.find(index);

	if (it == this->m_ShopManagerInfo.end())
	{
		return 0;
	}
	else
	{
		return it->second.Shop.GetItemCount();
	}
}

CItem* CShopManager::GetInventory(int index)
{
	std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.find(index);

	if (it != this->m_ShopManagerInfo.end())
	{
		return it->second.Shop.GetInventory();
	}

	return NULL;
}

long CShopManager::GetItemCountByMonsterClass(int MonsterClass) // OK
{
	for (std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.begin(); it != this->m_ShopManagerInfo.end(); it++)
	{
		if (it->second.MonsterClass != -1 && it->second.MonsterClass == MonsterClass)
		{
			return it->second.Shop.GetItemCount();
		}
	}

	return 0;
}

bool CShopManager::GCShopItemListSendByIndex(int index, int aIndex) // OK
{
	std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.find(index);

	if (it == this->m_ShopManagerInfo.end())
	{
		return 0;
	}
	else
	{
		return it->second.Shop.GCShopItemListSend(aIndex);
	}
}

bool CShopManager::GCShopItemListSendByMonsterClass(int MonsterClass, int Map, int X, int Y, int aIndex) // OK
{
	for (std::map<int, SHOP_MANAGER_INFO>::iterator it = this->m_ShopManagerInfo.begin(); it != this->m_ShopManagerInfo.end(); it++)
	{
		if (it->second.MonsterClass == MonsterClass && it->second.Map == Map && it->second.X == X && it->second.Y == Y)
		{
			return it->second.Shop.GCShopItemListSend(aIndex);
		}
	}

	return 0;
}