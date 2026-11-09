// Message.cpp: implementation of the CMessage class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "Message.h"
#include "MemScript.h"
#include "Util.h"

CMessage gMessage;
//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CMessage::CMessage() // OK
{
	memset(this->m_DefaultMessage,0,sizeof(this->m_DefaultMessage));

	this->m_MessageInfo.clear();

	for(int i = 0; i < MAX_SERVER_LANGUAGES; i++)
	{
		this->m_LanguageMessages[i].clear();
	}
}

CMessage::~CMessage() // OK
{

}

void CMessage::Load(char* path) // OK
{
	CMemScript* lpMemScript = new CMemScript;

	if(lpMemScript == 0)
	{
		ErrorMessageBox(MEM_SCRIPT_ALLOC_ERROR,path);
		return;
	}

	if(lpMemScript->SetBuffer(path) == 0)
	{
		ErrorMessageBox(lpMemScript->GetLastError());
		delete lpMemScript;
		return;
	}

	this->m_MessageInfo.clear();

	try
	{
		while(true)
		{
			if(lpMemScript->GetToken() == TOKEN_END)
			{
				break;
			}

			if(strcmp("end",lpMemScript->GetString()) == 0)
			{
				break;
			}

			MESSAGE_INFO info;

			info.Index = lpMemScript->GetNumber();

			strcpy_s(info.Message,lpMemScript->GetAsString());

			this->m_MessageInfo.insert(std::pair<int,MESSAGE_INFO>(info.Index,info));
		}
	}
	catch(...)
	{
		ErrorMessageBox(lpMemScript->GetLastError());
	}

	delete lpMemScript;
}

char* CMessage::GlobalText(int index) // OK
{
	std::map<int,MESSAGE_INFO>::iterator it = this->m_MessageInfo.find(index);

	if(it == this->m_MessageInfo.end())
	{
		wsprintf(this->m_DefaultMessage,"Could not find message %d!",index);
		return this->m_DefaultMessage;
	}
	else
	{
		return it->second.Message;
	}
}

void CMessage::LoadLanguage(char* path, BYTE language) // OK
{
	if(language >= MAX_SERVER_LANGUAGES)
	{
		return;
	}

	CMemScript* lpMemScript = new CMemScript;

	if(lpMemScript == 0)
	{
		ErrorMessageBox(MEM_SCRIPT_ALLOC_ERROR,path);
		return;
	}

	if(lpMemScript->SetBuffer(path) == 0)
	{
		delete lpMemScript;
		return;
	}

	this->m_LanguageMessages[language].clear();

	try
	{
		while(true)
		{
			if(lpMemScript->GetToken() == TOKEN_END)
			{
				break;
			}

			if(strcmp("end",lpMemScript->GetString()) == 0)
			{
				break;
			}

			MESSAGE_INFO info;

			info.Index = lpMemScript->GetNumber();

			strcpy_s(info.Message,lpMemScript->GetAsString());

			this->m_LanguageMessages[language].insert(std::pair<int,MESSAGE_INFO>(info.Index,info));
		}
	}
	catch(...)
	{
		ErrorMessageBox(lpMemScript->GetLastError());
	}

	delete lpMemScript;
}

char* CMessage::GetText(int index, BYTE language) // OK
{
	if(language < MAX_SERVER_LANGUAGES && !this->m_LanguageMessages[language].empty())
	{
		std::map<int,MESSAGE_INFO>::iterator it = this->m_LanguageMessages[language].find(index);

		if(it != this->m_LanguageMessages[language].end())
		{
			return it->second.Message;
		}
	}

	return this->GlobalText(index);
}
