// ExporterBridge.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "MarkdownImpExpBridge.h"

#include <unknwn.h>
#include <tchar.h>
#include <msclr\auto_gcroot.h>

#include "..\..\..\..\ToDoList_7.2\Interfaces\ITasklist.h"
#include "..\..\..\..\ToDoList_7.2\Interfaces\ITransText.h"
#include "..\..\..\..\ToDoList_7.2\Interfaces\IPreferences.h"

////////////////////////////////////////////////////////////////////////////////////////////////

#using <PluginHelpers.dll> as_friend

////////////////////////////////////////////////////////////////////////////////////////////////

using namespace MarkdownImpExp;
using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;
using namespace Abstractspoon::Tdl::PluginHelpers;

////////////////////////////////////////////////////////////////////////////////////////////////

// This is the constructor of a class that has been exported.
// see ExporterBridge.h for the class definition
CMarkdownImpExpBridge::CMarkdownImpExpBridge()
{
	return;
}

void CMarkdownImpExpBridge::Release()
{
	delete this;
}

void CMarkdownImpExpBridge::SetLocalizer(ITransText* /*pTT*/)
{
	// TODO
}

HICON CMarkdownImpExpBridge::GetIcon() const
{
	// TODO
	return NULL;
}

LPCWSTR CMarkdownImpExpBridge::GetMenuText() const
{
	return L"Markdown";
}

LPCWSTR CMarkdownImpExpBridge::GetFileFilter() const
{
	return L"md";
}

LPCWSTR CMarkdownImpExpBridge::GetFileExtension() const
{
	return L"md";
}

LPCWSTR CMarkdownImpExpBridge::GetTypeID() const
{
	return L"49A52D2D-7661-49AF-949A-E60066B300FC";
}

////////////////////////////////////////////////////////////////////////////////////////////////

IIMPORTEXPORT_RESULT CMarkdownImpExpBridge::Export(const ITaskList* pSrcTaskFile, LPCWSTR szDestFilePath, bool bSilent, IPreferences* pPrefs, LPCWSTR szKey)
{
   const ITaskList14* pTasks14 = GetITLInterface<ITaskList14>(pSrcTaskFile, IID_TASKLIST14);

   if (pTasks14 == nullptr)
   {
      MessageBox(NULL, L"You need a minimum ToDoList version of 7.0 to use this plugin", L"Version Not Supported", MB_OK);
      return IIER_BADINTERFACE;
   }

	// call into out sibling C# module to do the actual work
	msclr::auto_gcroot<MarkdownImpExpCore^> expCore = gcnew MarkdownImpExpCore();
	msclr::auto_gcroot<Preferences^> prefs = gcnew Preferences(pPrefs);
	msclr::auto_gcroot<TaskList^> srcTasks = gcnew TaskList(pSrcTaskFile);
	
	// do the export
	if (expCore->Export(srcTasks.get(), gcnew String(szDestFilePath), bSilent, prefs.get(), gcnew String(szKey)))
		return IIER_SUCCESS;

	return IIER_OTHER;
}

IIMPORTEXPORT_RESULT CMarkdownImpExpBridge::Export(const IMultiTaskList* pSrcTaskFile, LPCWSTR szDestFilePath, bool bSilent, IPreferences* pPrefs, LPCWSTR szKey)
{
	// TODO
	return IIER_OTHER;
}
