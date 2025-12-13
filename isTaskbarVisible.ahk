#Requires AutoHotkey v1.1.17+
#Include %A_ScriptDir%
#Include .\lib\MonitorExGetUtils.ahk
#Include .\lib\winGetWhichMonitor.ahk
;==============================================================
; isTaskbarVisible — Check whether the Windows taskbar is currently visible
;
; GitHub: https://github.com/SevenKeyboard/is-taskbar-visible
; Author: SevenKeyboard Ltd. (2025)
; License: MIT License
;
; Documentation / References:
;   ABM_GETSTATE message
;     https://learn.microsoft.com/en-us/windows/win32/shell/abm-getstate
;==============================================================

/*
Example Usage:
    F5::tooltip % isTaskbarVisible()
*/

class VersionManager_isTaskbarVisible
{
    static _ := VersionManager_isTaskbarVisible._init()
    _init()    {
        global
        ISTASKBARVISIBLE_VERSION := "1.0.1"
        if (!this._verCheck(MONITOREXGETUTILS_VERSION, "1.0.0"))
            throw exception("MonitorExGetUtils version 1.x is required (minimum 1.0.0).")
        if (!this._verCheck(WINGETWHICHMONITOR_VERSION, "1.0.1"))
            throw exception("winGetWhichMonitor version 1.x is required (minimum 1.0.1).")
        return true
    }
    _verCheck(byRef actual, required)    {
        if !isSet(actual)
            return false
        actualMajor     := strSplit(actual, ".",, 2)[1]
        requiredMajor   := strSplit(required, ".",, 2)[1]
        if (actualMajor != requiredMajor)
            return false
        return verCompare(actual, ">=" required)
    }
}
isTaskbarVisible()    {
    static GWL_EXSTYLE:=-20, WS_EX_TOPMOST:=0x00000008
        ,GWL_STYLE:=-16, WS_POPUP:=0x80000000
        ,ABM_GETSTATE:=0x4, ABS_ALWAYSONTOP:=0x2, ABS_AUTOHIDE:=0x1
    hWnd:=dllCall("User32.dll\FindWindowEx", "Ptr",0, "Ptr",0, "Str","Shell_TrayWnd", "Ptr",0, "Ptr")
    if (!hWnd)
        return false
    exstyle:=dllCall("User32.dll\GetWindowLong" (A_PtrSize==8?"Ptr":""), "Ptr",hWnd, "Int",GWL_EXSTYLE, (A_PtrSize==8?"Ptr":"Int"))
    style:=dllCall("User32.dll\GetWindowLong" (A_PtrSize==8?"Ptr":""), "Ptr",hWnd, "Int",GWL_STYLE, (A_PtrSize==8?"Ptr":"Int"))
    isTopMost:=!!(exstyle&WS_EX_TOPMOST)
    ;  isPopup:=!!(style&WS_POPUP)
    ;---------------------------
    varSetCapacity(APPBARDATA,cbSize:=A_PtrSize==8?48:36,0)
    numPut(cbSize,APPBARDATA,0,"UInt") ;  Pointer to an APPBARDATA structure. You must specify the cbSize member when sending this message; all other members are ignored.
    abmState:=dllCall("Shell32.dll\SHAppBarMessage", "UInt",ABM_GETSTATE, "Ptr",&APPBARDATA, "UPtr")
    ;  isAlwaysOnTop:=!!(abmState&ABS_ALWAYSONTOP)
    isAutoHide:=!!(abmState&ABS_AUTOHIDE)
    ;----------------------------
    switch (isAutoHide)
    {
        default:        return (isTopMost)
        case true:
            if (!isTopMost)
                return false
            if !(N:=winGetWhichMonitor(hWnd))
                return false
            info:=monitorExGetInfo(N)
            if (errorLevel)
                return false
            varSetCapacity(RECT,16,0)
            dllCall("User32.dll\GetClientRect", "Ptr",hWnd, "Ptr",&RECT)
            dllCall("User32.dll\ClientToScreen", "Ptr",hWnd, "Ptr",&RECT)
            x:=numGet(&RECT,0,"Int"), y:=numGet(&RECT,4,"Int"), w:=numGet(&RECT,8,"Int"), h:=numGet(&RECT,12,"Int")
            l:=x, t:=y, r:=x+w, b:=y+h
            return (info.rcWork.left<=l
                && info.rcWork.top<=t
                && r<=info.rcWork.right
                && b<=info.rcWork.bottom)
    }
}