using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BigPictureMonitores {
    public sealed class TaskbarInfo {
        public long Handle;
        public uint ProcessId;
        public long ProcessStarted;
        public string ClassName,Monitor;
        public bool Visible;
    }

    public static class TaskbarNative {
        [StructLayout(LayoutKind.Sequential)] struct WindowRect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MonitorDetails {
            public uint Size;
            public WindowRect Monitor,Work;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;
        }
        delegate bool EnumWindowCallback(IntPtr window,IntPtr state);
        [DllImport("user32.dll",SetLastError=true)] static extern bool EnumWindows(EnumWindowCallback callback,IntPtr state);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern int GetClassName(IntPtr window,StringBuilder name,int maximum);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll",SetLastError=true)] static extern bool GetWindowRect(IntPtr window,out WindowRect rectangle);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorDetails details);
        [DllImport("user32.dll",SetLastError=true)] static extern bool ShowWindowAsync(IntPtr window,int command);

        static bool ShellClass(string name) {
            return String.Equals(name,"Shell_TrayWnd",StringComparison.Ordinal) ||
                String.Equals(name,"Shell_SecondaryTrayWnd",StringComparison.Ordinal);
        }
        static string ClassOf(IntPtr window) {
            var name=new StringBuilder(256);
            return GetClassName(window,name,name.Capacity)>0?name.ToString():"";
        }
        static bool ProcessIdentity(uint id,out long started) {
            started=0;
            if(id==0 || id>Int32.MaxValue)return false;
            try {
                using(var process=Process.GetProcessById((int)id)) {
                    if(!String.Equals(process.ProcessName,"explorer",StringComparison.OrdinalIgnoreCase))return false;
                    started=process.StartTime.ToUniversalTime().Ticks;
                    return started>0 && !process.HasExited;
                }
            } catch(ArgumentException) {return false;}
            catch(InvalidOperationException) {return false;}
            catch(Win32Exception) {return false;}
        }
        static bool MonitorOf(IntPtr window,out MonitorDetails details) {
            details=new MonitorDetails();details.Size=(uint)Marshal.SizeOf(typeof(MonitorDetails));
            IntPtr monitor=MonitorFromWindow(window,0);
            return monitor!=IntPtr.Zero && GetMonitorInfo(monitor,ref details) && !String.IsNullOrEmpty(details.Device);
        }
        public static TaskbarInfo[] Bars() {
            var result=new List<TaskbarInfo>();
            bool enumerated=EnumWindows(delegate(IntPtr window,IntPtr state) {
                string name=ClassOf(window);
                if(!ShellClass(name))return true;
                uint id;GetWindowThreadProcessId(window,out id);
                long started;if(!ProcessIdentity(id,out started))return true;
                MonitorDetails monitor;
                if(!MonitorOf(window,out monitor))return true;
                result.Add(new TaskbarInfo {
                    Handle=window.ToInt64(),ProcessId=id,ProcessStarted=started,
                    ClassName=name,Monitor=monitor.Device,Visible=IsWindowVisible(window)
                });
                return true;
            },IntPtr.Zero);
            if(!enumerated)throw new Win32Exception(Marshal.GetLastWin32Error(),"Consultar barras de tarefas");
            return result.ToArray();
        }
        public static bool Matches(TaskbarInfo bar) {
            if(bar==null || bar.Handle==0 || !ShellClass(bar.ClassName) || bar.ProcessStarted<=0)return false;
            var window=new IntPtr(bar.Handle);
            if(!IsWindow(window) || !String.Equals(ClassOf(window),bar.ClassName,StringComparison.Ordinal))return false;
            uint id;GetWindowThreadProcessId(window,out id);
            if(id!=bar.ProcessId)return false;
            long started;
            return ProcessIdentity(id,out started) && started==bar.ProcessStarted;
        }
        public static void SetVisible(TaskbarInfo bar,bool visible) {
            // Explorer can restart or recycle a window handle. Never affect a replacement window.
            if(!Matches(bar))throw new InvalidOperationException("A barra de tarefas foi recriada pelo Windows.");
            var window=new IntPtr(bar.Handle);
            // Always enqueue the requested state: a prior asynchronous hide may still be pending even
            // when IsWindowVisible currently says true. A restore must follow that hide in the queue.
            // Never raise/activate Big Picture or modify z-order. Restoring the taskbar does not activate it.
            // Completion is confirmed by the session's next poll.
            // Unlike ShowWindow, ShowWindowAsync reports whether the request was successfully posted.
            if(!ShowWindowAsync(window,visible?4:0)) {
                int error=Marshal.GetLastWin32Error();
                string action=visible?"Restaurar barra de tarefas":"Ocultar barra de tarefas";
                if(error!=0)throw new Win32Exception(error,action);
                throw new InvalidOperationException(action+": o Windows não confirmou a solicitação.");
            }
        }
        public static string[] FullscreenMonitors(SteamWindow[] windows) {
            var monitors=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(windows==null)return new string[0];
            foreach(var candidate in windows) {
                if(candidate==null || candidate.Handle==0 || !candidate.Visible || candidate.Minimized)continue;
                var window=new IntPtr(candidate.Handle);
                if(!IsWindow(window) || !IsWindowVisible(window) || IsIconic(window))continue;
                WindowRect rectangle;MonitorDetails monitor;
                if(!GetWindowRect(window,out rectangle) || !MonitorOf(window,out monitor))continue;
                if(rectangle.Right<=rectangle.Left || rectangle.Bottom<=rectangle.Top)continue;
                // Use full monitor bounds, never WorkingArea (which excludes the taskbar).
                // A few physical pixels allow harmless border/compositor rounding.
                const int tolerance=3;
                if(rectangle.Left<=monitor.Monitor.Left+tolerance && rectangle.Top<=monitor.Monitor.Top+tolerance &&
                    rectangle.Right>=monitor.Monitor.Right-tolerance && rectangle.Bottom>=monitor.Monitor.Bottom-tolerance)
                    monitors.Add(monitor.Device);
            }
            var result=new string[monitors.Count];monitors.CopyTo(result);return result;
        }
    }
}
