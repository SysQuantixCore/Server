// Message.h: interface for the CMessage class.
//
//////////////////////////////////////////////////////////////////////

#pragma once

#define MAX_SERVER_LANGUAGES 5

// Language codes: 0=ENG, 1=POR, 2=SPN, 3=CHS, 4=KOR

struct MESSAGE_INFO
{
	int Index;
	char Message[128];
};

class CMessage
{
public:
	CMessage();
	virtual ~CMessage();
	void Load(char* path);
	void LoadLanguage(char* path, BYTE language);
	char* GlobalText(int index);
	char* GetText(int index, BYTE language);
private:
	char m_DefaultMessage[128];
	std::map<int,MESSAGE_INFO> m_MessageInfo;
	std::map<int,MESSAGE_INFO> m_LanguageMessages[MAX_SERVER_LANGUAGES];
};

extern CMessage gMessage;
