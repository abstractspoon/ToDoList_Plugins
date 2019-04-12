// PluginHelpers.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "UITheme.h"
#include "ColorUtil.h"

#include "..\..\ToDoList_7.2\Interfaces\UITheme.h"

#using "System.dll"

////////////////////////////////////////////////////////////////////////////////////////////////

using namespace Abstractspoon::Tdl::PluginHelpers;

////////////////////////////////////////////////////////////////////////////////////////////////

UITheme::UITheme() : m_pTheme(nullptr)
{
	m_pTheme = new UITHEME;
	ZeroMemory(m_pTheme, sizeof(UITHEME));
}

UITheme::UITheme(const UITHEME* pTheme)
{
	m_pTheme = new UITHEME;
	ZeroMemory(m_pTheme, sizeof(UITHEME));

	if (pTheme)
		*m_pTheme = *pTheme;
}

UITheme::RenderStyle UITheme::GetRenderStyle()
{
	switch (m_pTheme->nRenderStyle)
	{
	case UIRS_GRADIENT:				return UITheme::RenderStyle::Gradient;
	case UIRS_GLASS:				return UITheme::RenderStyle::Glass;
	case UIRS_GLASSWITHGRADIENT:	return UITheme::RenderStyle::GlassWithGradient;
	}

	return UITheme::RenderStyle::Gradient;
}

Windows::Media::Color UITheme::GetAppMediaColor(AppColor color)
{
	return ColorUtil::MediaColor::GetColor(GetColor(color));
}

System::Drawing::Color UITheme::GetAppDrawingColor(AppColor color)
{
	return ColorUtil::DrawingColor::GetColor(GetColor(color));
}

String^ UITheme::GetToolBarImagePath()
{
	return gcnew String(m_pTheme->szToolbarImage);
}

Windows::Media::Color UITheme::GetToolbarTransparencyMediaColor()
{
	return ColorUtil::MediaColor::GetColor(m_pTheme->crToolbarTransparency);
}

Drawing::Color UITheme::GetToolbarTransparencyDrawingColor()
{
	return ColorUtil::DrawingColor::GetColor(m_pTheme->crToolbarTransparency);
}

UInt32 UITheme::GetColor(AppColor color)
{
	switch (color)
	{
	case UITheme::AppColor::AppBackDark:		return m_pTheme->crAppBackDark;
	case UITheme::AppColor::AppBackLight:		return m_pTheme->crAppBackLight;
	case UITheme::AppColor::AppLinesDark:		return m_pTheme->crAppLinesDark;
	case UITheme::AppColor::AppLinesLight:		return m_pTheme->crAppLinesLight;
	case UITheme::AppColor::AppText:			return m_pTheme->crAppText;
	case UITheme::AppColor::MenuBack:			return m_pTheme->crMenuBack;
	case UITheme::AppColor::ToolbarDark:		return m_pTheme->crToolbarDark;
	case UITheme::AppColor::ToolbarLight:		return m_pTheme->crToolbarLight;
	case UITheme::AppColor::ToolbarHot:			return m_pTheme->crToolbarHot;
	case UITheme::AppColor::StatusBarDark:		return m_pTheme->crStatusBarDark;
	case UITheme::AppColor::StatusBarLight:		return m_pTheme->crStatusBarLight;
	case UITheme::AppColor::StatusBarText:		return m_pTheme->crStatusBarText;
	}

	return 0; // black
}

////////////////////////////////////////////////////////////////////////////////////////////////

UIThemeToolbarRenderer::UIThemeToolbarRenderer()
{
	m_HotFillColor = nullptr;
	m_HotBorderColor = nullptr;
	m_PressedFillColor = nullptr;
	m_BkgndLightColor = nullptr;
	m_BkgndDarkColor = nullptr;
}

void UIThemeToolbarRenderer::SetUITheme(UITheme^ theme)
{
	m_BkgndLightColor = theme->GetAppDrawingColor(UITheme::AppColor::ToolbarLight);
	m_BkgndDarkColor = theme->GetAppDrawingColor(UITheme::AppColor::ToolbarDark);
	m_HotFillColor = theme->GetAppDrawingColor(UITheme::AppColor::ToolbarHot);

	m_HotBorderColor = ColorUtil::DrawingColor::AdjustLighting(m_HotFillColor, -0.3f, false);
	m_PressedFillColor = ColorUtil::DrawingColor::AdjustLighting(m_HotFillColor, -0.15f, false);
}

bool UIThemeToolbarRenderer::RenderButtonBackground(ToolStripItemRenderEventArgs^ e)
{
	auto item = e->Item;
	auto button = dynamic_cast<ToolStripButton^>(item);
	bool checkedButton = ((button != nullptr) && button->Checked);

	if (ValidColours() && (item->Selected || item->Pressed || checkedButton))
	{
		System::Drawing::Rectangle^ rect = gcnew System::Drawing::Rectangle(Point::Empty, item->Size);

		rect->Width--;
		rect->Height--;

		if (item->Pressed || checkedButton)
		{
			Brush^ brush = gcnew SolidBrush(*m_PressedFillColor);
			e->Graphics->FillRectangle(brush, *rect);
		}
		else if (item->Selected)
		{
			Brush^ brush = gcnew SolidBrush(*m_HotFillColor);
			e->Graphics->FillRectangle(brush, *rect);
		}

		Pen^ pen = gcnew Pen(*m_HotBorderColor);
		e->Graphics->DrawRectangle(pen, *rect);

		return true;
	}

	// Use default renderer
	return false;
}

void UIThemeToolbarRenderer::OnRenderButtonBackground(ToolStripItemRenderEventArgs^ e)
{
	if (!RenderButtonBackground(e))
		BaseToolbarRenderer::OnRenderButtonBackground(e);
}

void UIThemeToolbarRenderer::OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs^ e)
{
	if (!RenderButtonBackground(e))
		BaseToolbarRenderer::OnRenderDropDownButtonBackground(e);
}

bool UIThemeToolbarRenderer::ValidColours()
{
	return ((m_HotFillColor != nullptr) && 
			(m_HotBorderColor != nullptr) && 
			(m_PressedFillColor != nullptr));
}

void UIThemeToolbarRenderer::DrawRowBackground(Drawing::Graphics^ g, Drawing::Rectangle^ rowRect, bool firstRow, bool lastRow)
{
	if ((m_BkgndLightColor != nullptr) && 
		(m_BkgndDarkColor != nullptr) && 
		(*m_BkgndLightColor != *m_BkgndDarkColor))
	{
		auto gradientBrush = gcnew Drawing2D::LinearGradientBrush(*rowRect, *m_BkgndLightColor, *m_BkgndDarkColor, Drawing2D::LinearGradientMode::Vertical);

		g->FillRectangle(gradientBrush, *rowRect);
	}

	DrawRowDivider(g, rowRect, firstRow, lastRow);
}
