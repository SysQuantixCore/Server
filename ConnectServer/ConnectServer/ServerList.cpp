// ServerList.cpp: implementation of the CServerList class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "ServerList.h"
#include "MemScript.h"
#include "pugixml.hpp"
#include "Util.h"

CServerList gServerList;
//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CServerList::CServerList() // OK
{
	this->m_JoinServerState = 0;

	this->m_JoinServerStateTime = 0;

	this->m_JoinServerQueueSize = 0;

	this->m_ServerListInfo.clear();
}

CServerList::~CServerList() // OK
{

}

void CServerList::Load(char* path) // OK
{
	pugi::xml_document file;
	pugi::xml_parse_result res = file.load_file(path);

	if (res.status != pugi::status_ok)
	{
		ErrorMessageBox("File %s load fail. Error: %s", path, res.description());
		return;
	}

	this->m_ServerListInfo.clear();

	pugi::xml_node ServerList = file.child("ServerList");

	if (ServerList.empty())
	{
		ErrorMessageBox("File %s is missing ServerList node.", path);
		return;
	}

	for (pugi::xml_node Section = ServerList.child("Section"); Section; Section = Section.next_sibling("Section"))
	{
		int section = Section.attribute("Index").as_int();

		if (section < 0 || section >= 10)
		{
			continue;
		}

		for (pugi::xml_node Server = Section.child("Server"); Server; Server = Server.next_sibling("Server"))
		{
			SERVER_LIST_INFO info;

			memset(&info, 0, sizeof(SERVER_LIST_INFO));

			info.ServerCode = Server.attribute("Code").as_int() + (section * 20);

			strcpy_s(info.ServerClient, Server.attribute("Client").as_string());

			strcpy_s(info.ServerName, Server.attribute("Name").as_string());

			strcpy_s(info.ServerAddress, Server.attribute("IP").as_string());

			info.ServerPort = Server.attribute("Port").as_int();

			info.ServerShow = Server.attribute("Visible").as_bool();

			this->m_ServerListInfo.insert(std::pair<int, SERVER_LIST_INFO>(info.ServerCode, info));
		}
	}

	LogAdd(LOG_BLUE, "[ServerList] ServerList loaded successfully");
}

void CServerList::MainProc() // OK
{
	if (this->m_JoinServerState != 0 && (GetTickCount() - this->m_JoinServerStateTime) > 10000)
	{
		this->m_JoinServerState = 0;
		this->m_JoinServerStateTime = 0;
		LogAdd(LOG_RED, "[ServerList] JoinServer offline");
	}

	for (std::map<int, SERVER_LIST_INFO>::iterator it = this->m_ServerListInfo.begin(); it != this->m_ServerListInfo.end(); it++)
	{
		if (it->second.ServerState != 0 && (GetTickCount() - it->second.ServerStateTime) > 10000)
		{
			it->second.ServerState = 0;
			it->second.ServerStateTime = 0;
			LogAdd(LOG_BLACK, "[ServerList] GameServer offline (%s) (%d)", it->second.ServerName, it->second.ServerCode);
		}
	}
}

bool CServerList::CheckJoinServerState() // OK
{
	if (this->m_JoinServerState == 0)
	{
		return 0;
	}

	if (this->m_JoinServerQueueSize > MAX_JOIN_SERVER_QUEUE_SIZE)
	{
		return 0;
	}

	return 1;
}

long CServerList::GenerateServerList(BYTE* lpMsg, int* size) // OK
{
	int count = 0;

	PMSG_SERVER_LIST info;

	if (this->CheckJoinServerState() != 0)
	{
		for (std::map<int, SERVER_LIST_INFO>::iterator it = this->m_ServerListInfo.begin(); it != this->m_ServerListInfo.end(); it++)
		{
			if (it->second.ServerShow != 0 && it->second.ServerState != 0)
			{
				info.ServerCode = it->second.ServerCode;

				info.UserTotal = it->second.UserTotal;

				info.type = 0xCC;

				strcpy_s(info.ServerName, it->second.ServerClient);

				memcpy(&lpMsg[(*size)], &info, sizeof(info));
				(*size) += sizeof(info);

				count++;
			}
		}
	}

	return count;
}

SERVER_LIST_INFO* CServerList::GetServerListInfo(int ServerCode) // OK
{
	std::map<int, SERVER_LIST_INFO>::iterator it = this->m_ServerListInfo.find(ServerCode);

	if (it == this->m_ServerListInfo.end())
	{
		return 0;
	}
	else
	{
		return &it->second;
	}
}

void CServerList::ServerProtocolCore(BYTE head, BYTE* lpMsg, int size) // OK
{
	switch (head)
	{
	case 0x01:
		this->GCGameServerLiveRecv((SDHP_GAME_SERVER_LIVE_RECV*)lpMsg);
		break;
	case 0x02:
		this->JCJoinServerLiveRecv((SDHP_JOIN_SERVER_LIVE_RECV*)lpMsg);
		break;
	}
}

void CServerList::GCGameServerLiveRecv(SDHP_GAME_SERVER_LIVE_RECV* lpMsg) // OK
{
	SERVER_LIST_INFO* lpServerListInfo = this->GetServerListInfo(lpMsg->ServerCode);

	if (lpServerListInfo == 0)
	{
		return;
	}

	if (lpServerListInfo->ServerState == 0)
	{
		LogAdd(LOG_BLACK, "[ServerList] GameServer online (%s) (%d)", lpServerListInfo->ServerName, lpServerListInfo->ServerCode);
	}

	lpServerListInfo->ServerState = 1;

	lpServerListInfo->ServerStateTime = GetTickCount();

	lpServerListInfo->UserTotal = lpMsg->UserTotal;

	lpServerListInfo->UserCount = lpMsg->UserCount;

	lpServerListInfo->AccountCount = lpMsg->AccountCount;

	lpServerListInfo->PCPointCount = lpMsg->PCPointCount;

	lpServerListInfo->MaxUserCount = lpMsg->MaxUserCount;
}

void CServerList::JCJoinServerLiveRecv(SDHP_JOIN_SERVER_LIVE_RECV* lpMsg) // OK
{
	if (this->m_JoinServerState == 0)
	{
		LogAdd(LOG_GREEN, "[ServerList] JoinServer online");
	}

	this->m_JoinServerState = 1;

	this->m_JoinServerStateTime = GetTickCount();

	this->m_JoinServerQueueSize = lpMsg->QueueSize;
}
