using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BigPictureMonitores {
    public interface ITaskbarBackend {
        TaskbarInfo[] Bars();
        bool Matches(TaskbarInfo bar);
        void SetVisible(TaskbarInfo bar,bool visible);
    }
    sealed class WindowsTaskbarBackend : ITaskbarBackend {
        public TaskbarInfo[] Bars(){return TaskbarNative.Bars();}
        public bool Matches(TaskbarInfo bar){return TaskbarNative.Matches(bar);}
        public void SetVisible(TaskbarInfo bar,bool visible){TaskbarNative.SetVisible(bar,visible);}
    }

    // The journal contains only Explorer taskbars that were visible before our hide request.
    // It is committed before hiding; recovery only touches the same process/window identity.
    public sealed class TaskbarSession {
        readonly ITaskbarBackend backend;
        readonly string journal;
        readonly Action<string> log;
        readonly List<TaskbarInfo> owned=new List<TaskbarInfo>();
        readonly HashSet<TaskbarInfo> showRequested=new HashSet<TaskbarInfo>();
        bool recovering,restoring,invalidJournal,journalDirty;
        public int HiddenCount {get;private set;}
        public string Status {get;private set;}
        public string Error {get;private set;}
        public bool PendingRestore {get{return invalidJournal || journalDirty || owned.Count>0&&(recovering||restoring||showRequested.Count>0);}}
        public TaskbarSession(ITaskbarBackend backend,string journal,Action<string> log=null) {
            this.backend=backend;this.journal=journal;this.log=log;
            Status="Aguardando o Big Picture em tela cheia.";Error="";
            if(File.Exists(journal)) {
                try{owned.AddRange(Load(journal));recovering=owned.Count>0;}
                catch(Exception ex){invalidJournal=true;SetError("Recuperação da barra pendente",ex);}
            }
        }
        public void Update(string[] fullscreenMonitors) {
            if(invalidJournal)return;
            try{Sync(fullscreenMonitors??new string[0]);}
            catch(Exception ex){SetError("Não foi possível ajustar a barra de tarefas",ex);}
        }
        public void Restore() {
            restoring=true;Update(new string[0]);
        }
        void Sync(string[] monitors) {
            if(journalDirty)Save();
            var desired=new HashSet<string>(monitors,StringComparer.OrdinalIgnoreCase);
            if(recovering||restoring)desired.Clear();
            var bars=backend.Bars();
            bool changed=false;
            int hidden=0;
            foreach(var entry in owned.ToArray()) {
                if(!backend.Matches(entry)) {
                    // Explorer exited or the handle was reused: never show an unrelated replacement.
                    owned.Remove(entry);showRequested.Remove(entry);changed=true;continue;
                }
                var current=bars.FirstOrDefault(bar=>SameIdentity(bar,entry));
                bool keepHidden=current!=null && desired.Contains(current.Monitor) && !showRequested.Contains(entry);
                if(keepHidden) {
                    if(current.Visible)backend.SetVisible(entry,false);
                    else hidden++;
                }else {
                    // Async requests are not completion. Keep the journal until a later observation
                    // confirms that this exact taskbar is visible again.
                    if(showRequested.Contains(entry) && current!=null && current.Visible) {
                        owned.Remove(entry);showRequested.Remove(entry);changed=true;
                    }else {
                        // Always enqueue a show at least once, even when currently visible:
                        // a previous asynchronous hide can still be waiting in Explorer's queue.
                        backend.SetVisible(entry,true);showRequested.Add(entry);
                    }
                }
            }
            if(changed)Save();
            if(owned.Count==0){recovering=false;restoring=false;}
            // Finish restoration in its own pass before starting another hide cycle.
            if(desired.Count>0) {
                foreach(var bar in bars) {
                    if(!desired.Contains(bar.Monitor) || !bar.Visible || owned.Any(entry=>SameIdentity(entry,bar)))continue;
                    if(!backend.Matches(bar))continue;
                    owned.Add(Clone(bar));
                    try{Save();}catch{owned.RemoveAt(owned.Count-1);throw;}
                    backend.SetVisible(bar,false);
                }
            }
            HiddenCount=hidden;
            Error="";
            if(owned.Any(entry=>{
                var current=bars.FirstOrDefault(bar=>SameIdentity(bar,entry));
                return current==null||!desired.Contains(current.Monitor);
            }))Status="Restaurando a barra de tarefas...";
            else if(hidden>0)Status="Barra oculta só na tela do Big Picture.";
            else if(owned.Count>0)Status="Ocultando a barra na tela do Big Picture...";
            else if(desired.Count>0)Status="Barra já oculta ou indisponível nesta tela.";
            else Status="Aguardando o Big Picture em tela cheia.";
        }
        void SetError(string prefix,Exception ex) {
            string message=prefix+": "+ex.Message;
            if(Error!=message && log!=null)log(message);
            Error=message;Status=prefix+". Tentaremos novamente.";
        }
        static TaskbarInfo Clone(TaskbarInfo bar) {
            return new TaskbarInfo{Handle=bar.Handle,ProcessId=bar.ProcessId,ProcessStarted=bar.ProcessStarted,ClassName=bar.ClassName,Monitor=bar.Monitor,Visible=true};
        }
        static bool SameIdentity(TaskbarInfo a,TaskbarInfo b) {
            return a.Handle==b.Handle && a.ProcessId==b.ProcessId && a.ProcessStarted==b.ProcessStarted && a.ClassName==b.ClassName;
        }
        void Save() {
            journalDirty=true;Persist();journalDirty=false;
        }
        void Persist() {
            if(owned.Count==0){File.Delete(journal);return;}
            string temp=journal+".new";
            try {
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
                using(var writer=new BinaryWriter(stream,Encoding.UTF8)) {
                    writer.Write("BPTASKBAR1");writer.Write(owned.Count);
                    foreach(var entry in owned){writer.Write(entry.Handle);writer.Write(entry.ProcessId);writer.Write(entry.ProcessStarted);writer.Write(entry.ClassName);writer.Write(entry.Monitor??"");}
                    writer.Flush();stream.Flush(true);
                }
                if(File.Exists(journal))File.Replace(temp,journal,null);
                else File.Move(temp,journal);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        static TaskbarInfo[] Load(string path) {
            using(var reader=new BinaryReader(File.OpenRead(path),Encoding.UTF8)) {
                if(reader.ReadString()!="BPTASKBAR1")throw new IOException("Registro de recuperação desconhecido.");
                int count=reader.ReadInt32();if(count<1 || count>64)throw new IOException("Registro de recuperação inválido.");
                var bars=new List<TaskbarInfo>();
                for(int i=0;i<count;i++) {
                    var bar=new TaskbarInfo{Handle=reader.ReadInt64(),ProcessId=reader.ReadUInt32(),ProcessStarted=reader.ReadInt64(),ClassName=reader.ReadString(),Monitor=reader.ReadString(),Visible=true};
                    if(bar.Handle==0 || bar.ProcessId==0 || bar.ProcessStarted<=0 || bar.ClassName!="Shell_TrayWnd" && bar.ClassName!="Shell_SecondaryTrayWnd")throw new IOException("Identidade de barra inválida.");
                    if(bars.Any(previous=>SameIdentity(previous,bar)))throw new IOException("Registro de recuperação duplicado.");
                    bars.Add(bar);
                }
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new IOException("Registro de recuperação incompleto.");
                return bars.ToArray();
            }
        }
        public static bool RestorePending(string path,Action<string> log=null) {
            if(!File.Exists(path))return true;
            var session=new TaskbarSession(new WindowsTaskbarBackend(),path,log);session.Restore();
            return !File.Exists(path);
        }
    }
}
