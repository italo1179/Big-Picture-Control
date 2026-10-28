using System;
using System.IO;
using System.Text;

namespace BigPictureMonitores {
    public interface IAudioBackend {
        AudioDevice[] Outputs();
        string DefaultId(int role);
        bool Available(string id);
        void SetDefault(string id,int role);
    }
    sealed class WindowsAudioBackend : IAudioBackend {
        public AudioDevice[] Outputs(){return AudioNative.Outputs();}
        public string DefaultId(int role){return AudioNative.DefaultId(role);}
        public bool Available(string id){return AudioNative.Available(id);}
        public void SetDefault(string id,int role){AudioNative.SetDefault(id,role);}
    }

    // Only playback roles (console and multimedia) are changed. Communications stays independent.
    public sealed class AudioSession {
        readonly IAudioBackend backend;
        readonly string backup;
        readonly Action<string> log;
        string[] original;
        bool recovering,restoreRequested,rollbackRequired,invalidBackup;
        public string Status {get;private set;}
        public string Error {get;private set;}
        public bool Switched {get;private set;}
        public bool PendingRestore {get{return invalidBackup || original!=null && (recovering||restoreRequested||rollbackRequired);}}
        public AudioSession(IAudioBackend backend,string backup,Action<string> log=null) {
            this.backend=backend;this.backup=backup;this.log=log;
            Status="Aguardando o Big Picture.";Error="";
            if(File.Exists(backup)) {
                try {original=Load(backup);recovering=true;Status="Recuperando a saída de áudio anterior.";}
                catch(Exception ex){invalidBackup=true;SetError("Não foi possível ler o áudio salvo",ex);}
            }
        }
        public void Update(bool active,string preferred) {
            if(!active || String.IsNullOrEmpty(preferred)){Restore();return;}
            if(invalidBackup)return;
            if(recovering||restoreRequested) {Restore();if(original!=null||invalidBackup)return;}
            if(rollbackRequired && !RestoreDefaults(false))return;
            Error="";
            try {
                // Capture before display automation can disconnect HDMI endpoints and change Windows defaults.
                if(original==null) {
                    var current=new[]{backend.DefaultId(0),backend.DefaultId(1)};
                    if(String.IsNullOrEmpty(current[0])||String.IsNullOrEmpty(current[1]))
                        throw new InvalidOperationException("A saída anterior ainda não está disponível.");
                    Save(backup,current);original=current;
                }
                if(!backend.Available(preferred)) {
                    Switched=false;Status="Saída selecionada indisponível. Aguardando conexão.";return;
                }
                for(int role=0;role<2;role++)if(backend.DefaultId(role)!=preferred)backend.SetDefault(preferred,role);
                bool wasSwitched=Switched;Switched=true;Status="Áudio selecionado ativo no Big Picture.";
                if(!wasSwitched)Log("Saída de áudio do Big Picture aplicada.");
            }catch(Exception ex) {
                // A failure halfway through must not leave console and multimedia on different targets.
                if(original!=null)rollbackRequired=!RestoreDefaults(false);
                SetError("Não foi possível trocar a saída de áudio",ex);
            }
        }
        public void Restore() {
            if(invalidBackup)return;
            if(original==null){Switched=false;Error="";Status="Aguardando o Big Picture.";return;}
            restoreRequested=true;RestoreDefaults(true);
        }
        bool RestoreDefaults(bool finish) {
            if(original==null)return true;
            bool complete=true;
            try {
                for(int role=0;role<2;role++) {
                    if(!backend.Available(original[role])){complete=false;continue;}
                    if(backend.DefaultId(role)!=original[role])backend.SetDefault(original[role],role);
                }
                if(!complete){Status="Aguardando a saída anterior reconectar para restaurar.";return false;}
                Switched=false;rollbackRequired=false;Error="";
                if(finish) {
                    File.Delete(backup);original=null;recovering=false;restoreRequested=false;
                    Status="Aguardando o Big Picture.";Log("Saída de áudio anterior restaurada.");
                }
                return true;
            }catch(Exception ex){SetError("Restauração do áudio pendente",ex);return false;}
        }
        void SetError(string prefix,Exception ex) {
            string message=prefix+": "+ex.Message;
            if(Error!=message)Log(message);
            Error=message;Status=prefix+". Tentaremos novamente.";
        }
        void Log(string text){if(log!=null)log(text);}
        static void Save(string path,string[] ids) {
            string temp=path+".new";
            try {
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
                using(var writer=new BinaryWriter(stream,Encoding.UTF8)) {
                    writer.Write("BPAUDIO1");writer.Write(2);
                    foreach(string id in ids)writer.Write(id);
                    writer.Flush();stream.Flush(true);
                }
                File.Move(temp,path);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        static string[] Load(string path) {
            using(var reader=new BinaryReader(File.OpenRead(path),Encoding.UTF8)) {
                if(reader.ReadString()!="BPAUDIO1" || reader.ReadInt32()!=2)throw new IOException("Backup de áudio desconhecido.");
                var ids=new[]{reader.ReadString(),reader.ReadString()};
                if(String.IsNullOrEmpty(ids[0])||String.IsNullOrEmpty(ids[1])||reader.BaseStream.Position!=reader.BaseStream.Length)
                    throw new IOException("Backup de áudio incompleto.");
                return ids;
            }
        }
        public static bool RestorePending(string path,Action<string> log=null) {
            if(!File.Exists(path))return true;
            var session=new AudioSession(new WindowsAudioBackend(),path,log);session.Restore();
            return !File.Exists(path);
        }
    }
}
