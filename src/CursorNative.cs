using System;
using System.Runtime.InteropServices;
public static class BPCursor {
    [StructLayout(LayoutKind.Sequential)] struct Message {
        public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public int X,Y; public uint Private;
    }
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr w,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr w,int id);
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message m,IntPtr w,uint min,uint max,uint remove);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr CreateCursor(IntPtr instance,int x,int y,int width,int height,byte[] andMask,byte[] xorMask);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetSystemCursor(IntPtr cursor,uint id);
    [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] static extern bool SystemParametersInfo(uint action,uint param,IntPtr data,uint flags);
    public static bool Register() { return RegisterHotKey(IntPtr.Zero,0x4251,0x4000|0x02|0x01,0x78); }
    public static void Unregister() { UnregisterHotKey(IntPtr.Zero,0x4251); }
    public static bool ReadToggle() {
        Message m; bool result=false;
        while(PeekMessage(out m,IntPtr.Zero,0x312,0x312,1)) if(m.WParam.ToUInt64()==0x4251) result=!result;
        return result;
    }
    public static void Show() {
        if(!SystemParametersInfo(0x57,0,IntPtr.Zero,0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Restaurar cursores");
    }
    public static void Hide() {
        int width=GetSystemMetrics(13),height=GetSystemMetrics(14);
        if(width<=0 || height<=0) throw new Exception("Dimensoes do cursor invalidas.");
        int length=((width+15)/16)*2*height;
        byte[] andMask=new byte[length],xorMask=new byte[length];
        for(int i=0;i<length;i++) andMask[i]=255;
        try {
            foreach(uint id in new uint[]{32512,32513,32514,32515,32516,32642,32643,32644,32645,32646,32648,32649,32650,32651}) {
                IntPtr c=CreateCursor(IntPtr.Zero,0,0,width,height,andMask,xorMask);
                if(c==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Criar cursor transparente");
                if(!SetSystemCursor(c,id)) {
                    int error=Marshal.GetLastWin32Error(); DestroyCursor(c);
                    throw new System.ComponentModel.Win32Exception(error,"Ocultar cursor");
                }
                // SetSystemCursor owns and destroys c after success.
            }
        } catch { Show(); throw; }
    }
}