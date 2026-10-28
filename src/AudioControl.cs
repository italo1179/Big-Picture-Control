using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BigPictureMonitores {
    public sealed class AudioDevice {
        public string Id,Name;
    }

    // Core Audio endpoint IDs are opaque. Friendly names are for display only.
    public static class AudioNative {
        const uint Active=1;
        const int NotFound=unchecked((int)0x80070490);
        static readonly PropertyKey FriendlyName=new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),14);

        [StructLayout(LayoutKind.Sequential)] struct PropertyKey {
            public Guid Format; public uint Id;
            public PropertyKey(Guid format,uint id) {Format=format;Id=id;}
        }
        // This application is x64; PROPVARIANT is 24 bytes on that ABI.
        [StructLayout(LayoutKind.Explicit,Size=24)] struct PropVariant {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public IntPtr Pointer;
        }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);

        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceEnumerator {
            [PreserveSig] int EnumAudioEndpoints(int flow,uint state,out IDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }
        [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceCollection {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index,out IDevice device);
        }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevice {
            [PreserveSig] int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);
            [PreserveSig] int OpenPropertyStore(uint mode,out IPropertyStore store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
            [PreserveSig] int GetState(out uint state);
        }
        [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index,out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key,out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key,ref PropVariant value);
            [PreserveSig] int Commit();
        }

        // Windows exposes enumeration publicly, but default routing still requires
        // the PolicyConfig COM interface used by Windows desktop audio utilities.
        // Preserve every vtable slot before SetDefaultEndpoint.
        [ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class PolicyConfigClient { }
        [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPolicyConfig {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)]string id,out IntPtr format);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string id,int mode,out IntPtr format);
            [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string id);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr endpoint,IntPtr mix);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)]string id,int mode,out long defaultPeriod,out long minimumPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)]string id,ref long period);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr mode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)]string id,IntPtr mode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)]string id,ref PropertyKey key,out PropVariant value);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)]string id,ref PropertyKey key,ref PropVariant value);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)]string id,int visible);
        }

        static void Release(object value) {if(value!=null && Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        static void Check(int result,string message) {if(result<0)throw new COMException(message+" (Windows 0x"+result.ToString("X8")+")",result);}
        static void CheckRole(int role) {if(role<0 || role>2)throw new ArgumentOutOfRangeException("role");}
        static string DeviceId(IDevice device) {string id;Check(device.GetId(out id),"Identificar saída de áudio");return id;}
        static string DeviceName(IDevice device) {
            IPropertyStore store=null;PropVariant value=new PropVariant();
            try {
                Check(device.OpenPropertyStore(0,out store),"Ler saída de áudio");
                var key=FriendlyName;Check(store.GetValue(ref key,out value),"Ler nome da saída de áudio");
                string name=value.Type==31 && value.Pointer!=IntPtr.Zero?Marshal.PtrToStringUni(value.Pointer):null;
                return String.IsNullOrEmpty(name)?"Saída de áudio":name;
            } finally {PropVariantClear(ref value);Release(store);}
        }
        public static AudioDevice[] Outputs() {
            IDeviceEnumerator enumerator=null;IDeviceCollection collection=null;
            try {
                enumerator=(IDeviceEnumerator)new DeviceEnumerator();
                Check(enumerator.EnumAudioEndpoints(0,Active,out collection),"Listar saídas de áudio");
                uint count;Check(collection.GetCount(out count),"Contar saídas de áudio");
                var outputs=new List<AudioDevice>();
                for(uint i=0;i<count;i++) {
                    IDevice device=null;
                    try {
                        Check(collection.Item(i,out device),"Ler saída de áudio");
                        outputs.Add(new AudioDevice{Id=DeviceId(device),Name=DeviceName(device)});
                    } catch(COMException) {
                        // An HDMI/Bluetooth endpoint can vanish during enumeration.
                        // The next refresh will pick up its new state.
                    } finally {Release(device);}
                }
                outputs.Sort(delegate(AudioDevice a,AudioDevice b){int name=StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);return name!=0?name:StringComparer.Ordinal.Compare(a.Id,b.Id);});
                return outputs.ToArray();
            } finally {Release(collection);Release(enumerator);}
        }
        public static string DefaultId(int role) {
            CheckRole(role);IDeviceEnumerator enumerator=null;IDevice device=null;
            try {
                enumerator=(IDeviceEnumerator)new DeviceEnumerator();
                int result=enumerator.GetDefaultAudioEndpoint(0,role,out device);
                if(result==NotFound)return "";
                Check(result,"Consultar saída de áudio padrão");return DeviceId(device);
            } finally {Release(device);Release(enumerator);}
        }
        public static bool Available(string id) {
            if(String.IsNullOrEmpty(id))return false;
            IDeviceEnumerator enumerator=null;IDevice device=null;
            try {
                enumerator=(IDeviceEnumerator)new DeviceEnumerator();
                int result=enumerator.GetDevice(id,out device);
                if(result<0)return false;
                uint state;Check(device.GetState(out state),"Consultar disponibilidade da saída de áudio");
                return (state&Active)!=0;
            } finally {Release(device);Release(enumerator);}
        }
        public static void SetDefault(string id,int role) {
            CheckRole(role);
            if(String.IsNullOrEmpty(id))throw new ArgumentException("Selecione uma saída de áudio.","id");
            if(!Available(id))throw new InvalidOperationException("A saída de áudio selecionada não está conectada.");
            IPolicyConfig policy=null;
            try {
                policy=(IPolicyConfig)new PolicyConfigClient();
                Check(policy.SetDefaultEndpoint(id,role),"Trocar saída de áudio");
            } finally {Release(policy);}
        }
        // Read-only probe: instantiates the routing interface without changing it.
        public static void CheckSupported() {
            IPolicyConfig policy=null;
            try {policy=(IPolicyConfig)new PolicyConfigClient();}
            finally {Release(policy);}
        }
    }
}
