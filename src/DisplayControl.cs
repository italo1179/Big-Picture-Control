using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BigPictureMonitores {
  [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; }
  [StructLayout(LayoutKind.Sequential)] public struct Rational { public uint Num, Den; }
  [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, Mode, Status; }
  [StructLayout(LayoutKind.Sequential)] public struct Target {
    public Luid Adapter; public uint Id, Mode, Technology, Rotation, Scaling;
    public Rational Refresh; public uint Scan; public int Available; public uint Status;
  }
  [StructLayout(LayoutKind.Sequential)] public struct DisplayPath { public Source Source; public Target Target; public uint Flags; }
  [StructLayout(LayoutKind.Explicit, Size=64)] public struct DisplayMode {
    [FieldOffset(0)] public uint Type;
    [FieldOffset(4)] public uint Id;
    [FieldOffset(8)] public Luid Adapter;
    [FieldOffset(16)] public ulong Data0;
    [FieldOffset(24)] public ulong Data1;
    [FieldOffset(32)] public ulong Data2;
    [FieldOffset(40)] public ulong Data3;
    [FieldOffset(48)] public ulong Data4;
    [FieldOffset(56)] public ulong Data5;
    [FieldOffset(16)] public uint Width;
    [FieldOffset(20)] public uint Height;
    [FieldOffset(28)] public int X;
    [FieldOffset(32)] public int Y;
  }
  [StructLayout(LayoutKind.Sequential)] struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct SourceName {
    public Header Header;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Name;
  }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct TargetName {
    public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product;
    public uint Connector;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=64)] public string FriendlyName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string DevicePath;
  }
  [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct MonitorInfo {
    public uint Size; public Rect Monitor, Work; public uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string Device;
  }
  public class Snapshot { public DisplayPath[] Paths; public DisplayMode[] Modes; }
  public class SteamWindow { public long Handle; public string Title, Monitor; public bool Visible, Minimized, Foreground; }
  public class ScreenInfo { public string Device, Name; public uint TargetId; public int X,Y,Width,Height; public bool Primary; }
  public static class Native {
    [StructLayout(LayoutKind.Sequential)] struct Message {
      public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam;
      public uint Time; public int X,Y; public uint Private;
    }
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message message,IntPtr window,uint min,uint max,uint remove);
    public static bool RegisterToggle() { return RegisterHotKey(IntPtr.Zero,0x4250,0x4000|0x02|0x01,0x79); }
    public static void UnregisterToggle() { UnregisterHotKey(IntPtr.Zero,0x4250); }
    public static bool ReadToggle() {
      Message m; bool toggle=false;
      while(PeekMessage(out m,IntPtr.Zero,0x0312,0x0312,1)) if(m.WParam.ToUInt64()==0x4250) toggle=!toggle;
      return toggle;
    }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr param);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DisplayPath[] pathArray, ref uint modes, [Out] DisplayMode[] modeArray, IntPtr topology);
    [DllImport("user32.dll")] static extern int SetDisplayConfig(uint paths, DisplayPath[] pathArray, uint modes, DisplayMode[] modeArray, uint flags);
    [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetSourceName(ref SourceName name);
    [DllImport("user32.dll", EntryPoint="DisplayConfigGetDeviceInfo")] static extern int GetTargetName(ref TargetName name);
    static void Check(int code, string action) { if(code != 0) throw new System.ComponentModel.Win32Exception(code, action + " (Windows " + code + ")"); }
    public static void CheckLayout() {
      if(Marshal.SizeOf(typeof(DisplayPath)) != 72 || Marshal.SizeOf(typeof(DisplayMode)) != 64 || Marshal.SizeOf(typeof(TargetName)) != 420)
        throw new Exception("Tamanho inesperado das estruturas do Windows.");
    }
    public static Snapshot Capture() {
      CheckLayout();
      for(int attempt=0; attempt<5; attempt++) {
        uint np,nm; Check(GetDisplayConfigBufferSizes(2,out np,out nm),"Consultar monitores");
        var p=new DisplayPath[np]; var m=new DisplayMode[nm];
        int code=QueryDisplayConfig(2,ref np,p,ref nm,m,IntPtr.Zero);
        if(code==122) continue;
        Check(code,"Ler monitores"); Array.Resize(ref p,(int)np); Array.Resize(ref m,(int)nm);
        if(np==0) throw new Exception("Nenhum monitor ativo encontrado.");
        return new Snapshot { Paths=p, Modes=m };
      }
      throw new Exception("Os monitores estao mudando. Tente novamente.");
    }
    public static string Device(DisplayPath path) {
      var n=new SourceName(); n.Header.Type=1; n.Header.Size=(uint)Marshal.SizeOf(typeof(SourceName));
      n.Header.Adapter=path.Source.Adapter; n.Header.Id=path.Source.Id;
      Check(GetSourceName(ref n),"Identificar monitor"); return n.Name;
    }
    public static ScreenInfo[] Screens() {
      var s=Capture(); var result=new List<ScreenInfo>();
      foreach(var p in s.Paths) {
        var n=new TargetName(); n.Header.Type=2; n.Header.Size=(uint)Marshal.SizeOf(typeof(TargetName));
        n.Header.Adapter=p.Target.Adapter; n.Header.Id=p.Target.Id;
        int code=GetTargetName(ref n); int x=0,y=0;
        if(p.Source.Mode<s.Modes.Length) { x=s.Modes[p.Source.Mode].X; y=s.Modes[p.Source.Mode].Y; }
        result.Add(new ScreenInfo { Device=Device(p),Name=code==0?n.FriendlyName:"Monitor",TargetId=p.Target.Id,X=x,Y=y,Width=p.Source.Mode<s.Modes.Length?(int)s.Modes[p.Source.Mode].Width:0,Height=p.Source.Mode<s.Modes.Length?(int)s.Modes[p.Source.Mode].Height:0,Primary=x==0&&y==0 });
      }
      return result.ToArray();
    }
    public static SteamWindow[] Windows() {
      var ids=new HashSet<int>();
      foreach(string name in new[]{"steam","steamwebhelper"}) foreach(var p in Process.GetProcessesByName(name)) { ids.Add(p.Id); p.Dispose(); }
      var result=new List<SteamWindow>(); IntPtr fg=GetForegroundWindow();
      EnumWindows(delegate(IntPtr hwnd,IntPtr unused) {
        uint pid; GetWindowThreadProcessId(hwnd,out pid); if(!ids.Contains((int)pid)) return true;
        var title=new StringBuilder(1024); GetWindowText(hwnd,title,title.Capacity); if(title.Length==0) return true;
        var mi=new MonitorInfo(); mi.Size=(uint)Marshal.SizeOf(typeof(MonitorInfo));
        bool valid=GetMonitorInfo(MonitorFromWindow(hwnd,0),ref mi);
        result.Add(new SteamWindow { Handle=hwnd.ToInt64(),Title=title.ToString(),Monitor=valid?mi.Device:null,Visible=IsWindowVisible(hwnd),Minimized=IsIconic(hwnd),Foreground=hwnd==fg });
        return true;
      },IntPtr.Zero);
      return result.ToArray();
    }
    // A duplicated desktop does not identify one physical screen unambiguously.
    public static Snapshot Single(Snapshot original,string device) {
      var matches=new List<DisplayPath>(); foreach(var p in original.Paths) if(Device(p)==device) matches.Add(p);
      if(matches.Count!=1) throw new Exception("Monitor ambiguo ou ausente. Use telas estendidas, nao duplicadas.");
      var path=matches[0]; var modes=(DisplayMode[])original.Modes.Clone();
      if(path.Source.Mode>=modes.Length || modes[path.Source.Mode].Type!=1) throw new Exception("Modo de video de origem invalido.");
      var source=modes[path.Source.Mode]; source.X=0; source.Y=0; modes[path.Source.Mode]=source;
      path.Flags=1;
      return new Snapshot { Paths=new[]{path},Modes=modes };
    }
    public static Snapshot WithPrimary(Snapshot original,string device) {
      if(String.IsNullOrEmpty(device)) return original;
      uint index=UInt32.MaxValue;
      foreach(var path in original.Paths) if(Device(path)==device) { index=path.Source.Mode; break; }
      // A disconnected preferred screen must never prevent the saved layout from returning.
      if(index>=original.Modes.Length || original.Modes[index].Type!=1) return original;
      var modes=(DisplayMode[])original.Modes.Clone();
      int dx=modes[index].X,dy=modes[index].Y;
      for(int i=0;i<modes.Length;i++) if(modes[i].Type==1) { modes[i].X-=dx; modes[i].Y-=dy; }
      return new Snapshot {Paths=(DisplayPath[])original.Paths.Clone(),Modes=modes};
    }
    public static void Validate(Snapshot s) { Check(SetDisplayConfig((uint)s.Paths.Length,s.Paths,(uint)s.Modes.Length,s.Modes,0x20|0x40),"Validar configuracao"); }
    public static void Apply(Snapshot s) {
      Validate(s);
      // Temporary change: do not overwrite the Windows persisted desktop layout.
      Check(SetDisplayConfig((uint)s.Paths.Length,s.Paths,(uint)s.Modes.Length,s.Modes,0x20|0x80),"Aplicar configuracao");
    }
    public static void RestoreDatabase() { Check(SetDisplayConfig(0,null,0,null,0x80|0x0f),"Restaurar configuracao salva do Windows"); }
    static byte[] Pack<T>(T value) {
      int size=Marshal.SizeOf(typeof(T)); var bytes=new byte[size]; IntPtr mem=Marshal.AllocHGlobal(size);
      try { Marshal.StructureToPtr(value,mem,false); Marshal.Copy(mem,bytes,0,size); return bytes; } finally { Marshal.FreeHGlobal(mem); }
    }
    static T Unpack<T>(BinaryReader reader) {
      int size=Marshal.SizeOf(typeof(T)); byte[] bytes=reader.ReadBytes(size); if(bytes.Length!=size) throw new IOException("Backup incompleto.");
      IntPtr mem=Marshal.AllocHGlobal(size); try { Marshal.Copy(bytes,0,mem,size); return (T)Marshal.PtrToStructure(mem,typeof(T)); } finally { Marshal.FreeHGlobal(mem); }
    }
    public static void Save(Snapshot s,string file) {
      using(var w=new BinaryWriter(File.Open(file,FileMode.CreateNew))) {
        w.Write(0x42504D31); w.Write(s.Paths.Length); w.Write(s.Modes.Length);
        foreach(var p in s.Paths) w.Write(Pack(p)); foreach(var m in s.Modes) w.Write(Pack(m));
        w.Flush(); ((FileStream)w.BaseStream).Flush(true);
      }
    }
    public static Snapshot Load(string file) {
      using(var r=new BinaryReader(File.OpenRead(file))) {
        if(r.ReadInt32()!=0x42504D31) throw new IOException("Backup desconhecido.");
        int np=r.ReadInt32(),nm=r.ReadInt32(); if(np<1 || np>128 || nm<1 || nm>1024) throw new IOException("Backup invalido.");
        var s=new Snapshot {Paths=new DisplayPath[np],Modes=new DisplayMode[nm]};
        for(int i=0;i<np;i++) s.Paths[i]=Unpack<DisplayPath>(r);
        for(int i=0;i<nm;i++) s.Modes[i]=Unpack<DisplayMode>(r);
        if(r.BaseStream.Position!=r.BaseStream.Length) throw new IOException("Tamanho de backup invalido.");
        return s;
      }
    }
  }
}
