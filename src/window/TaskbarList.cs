using System.Runtime.InteropServices;

namespace WindowMinimize;

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
internal class TaskbarListCoClass {
}

[ComImport]
[Guid("56FDF342-FD6D-11d0-958A-006097C9A090")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList {
    [PreserveSig]
    int HrInit();

    [PreserveSig]
    int AddTab(IntPtr hwnd);

    [PreserveSig]
    int DeleteTab(IntPtr hwnd);

    [PreserveSig]
    int ActivateTab(IntPtr hwnd);

    [PreserveSig]
    int SetActiveAlt(IntPtr hwnd);
}
