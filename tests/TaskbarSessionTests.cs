using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigPictureMonitores;

class TaskbarSessionTests {
    sealed class FakeBars : ITaskbarBackend {
        public readonly Dictionary<long,TaskbarInfo> Windows=new Dictionary<long,TaskbarInfo>();
        public readonly List<string> Calls=new List<string>();
        public readonly List<KeyValuePair<long,bool>> Queue=new List<KeyValuePair<long,bool>>();
        public readonly HashSet<long> Omitted=new HashSet<long>();
        public bool Deferred,FailRead,FailHide,FailShow;
        public Action<TaskbarInfo,bool> BeforeSet;
        public FakeBars(){Add(1,"A",true);Add(2,"B",true);Add(3,"C",true);}
        public TaskbarInfo Add(long id,string monitor,bool visible){var b=new TaskbarInfo{Handle=id,ProcessId=77,ProcessStarted=1000,ClassName=id==1?"Shell_TrayWnd":"Shell_SecondaryTrayWnd",Monitor=monitor,Visible=visible};Windows[id]=b;return b;}
        public static TaskbarInfo Clone(TaskbarInfo b){return new TaskbarInfo{Handle=b.Handle,ProcessId=b.ProcessId,ProcessStarted=b.ProcessStarted,ClassName=b.ClassName,Monitor=b.Monitor,Visible=b.Visible};}
        public TaskbarInfo[] Bars(){if(FailRead)throw new IOException("temporary query failure");return Windows.Values.Where(b=>!Omitted.Contains(b.Handle)).Select(Clone).ToArray();}
        public bool Matches(TaskbarInfo b){TaskbarInfo current;return Windows.TryGetValue(b.Handle,out current) && current.ProcessId==b.ProcessId && current.ProcessStarted==b.ProcessStarted && current.ClassName==b.ClassName;}
        public void SetVisible(TaskbarInfo b,bool visible){
            if(!Matches(b))throw new IOException("stale handle");
            if(BeforeSet!=null)BeforeSet(b,visible);
            Calls.Add(b.Handle+":"+visible);
            if(visible?FailShow:FailHide)throw new IOException("temporary visibility failure");
            if(Deferred)Queue.Add(new KeyValuePair<long,bool>(b.Handle,visible));else Windows[b.Handle].Visible=visible;
        }
        public void Complete(){foreach(var request in Queue)if(Windows.ContainsKey(request.Key))Windows[request.Key].Visible=request.Value;Queue.Clear();}
    }
    static int passed;
    static readonly string Root=Path.Combine(Environment.GetEnvironmentVariable("BPC_TEST_DATA_ROOT")??Path.GetTempPath(),"BigPictureControleTests",Guid.NewGuid().ToString("N"));
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Case(string name,Action<string> run){
        string path=Path.Combine(Root,name+".bin");
        if(File.Exists(path))File.Delete(path);if(File.Exists(path+".new"))File.Delete(path+".new");
        try{run(path);passed++;Console.WriteLine("PASS "+name);}
        finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".new"))File.Delete(path+".new");}
    }
    static void Finish(TaskbarSession s){s.Restore();s.Update(new string[0]);}
    static void Main(){
        try{Run();}
        finally{if(Directory.Exists(Root) && !Directory.EnumerateFileSystemEntries(Root).Any())Directory.Delete(Root);}
    }
    static void Run(){
        Directory.CreateDirectory(Root);
        Case("outside-big-picture-no-op",delegate(string path){var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new string[0]);s.Update(null);Assert(f.Calls.Count==0 && !File.Exists(path),"changed taskbar without scope");});
        Case("only-requested-monitor",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});s.Update(new[]{"B"});
            Assert(f.Windows[1].Visible && !f.Windows[2].Visible && f.Windows[3].Visible,"affected another monitor");Assert(s.HiddenCount==1,"hidden count mismatch");
            int count=f.Calls.Count;s.Update(new[]{"B"});Assert(f.Calls.Count==count,"rewrote already hidden state");Finish(s);
            Assert(f.Windows[1].Visible&&f.Windows[2].Visible&&f.Windows[3].Visible&&!File.Exists(path),"failed exact restore");
        });
        Case("originally-hidden-not-owned",delegate(string path){var f=new FakeBars();f.Windows[2].Visible=false;var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Finish(s);Assert(!f.Windows[2].Visible && f.Calls.Count==0 && !File.Exists(path),"showed externally hidden taskbar");});
        Case("journal-before-hide",delegate(string path){var f=new FakeBars();f.BeforeSet=delegate(TaskbarInfo b,bool show){if(!show)Assert(File.Exists(path)&&new FileInfo(path).Length>0,"hide before durable journal");};var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Finish(s);});
        Case("async-show-retains-journal",delegate(string path){
            var f=new FakeBars{Deferred=true};var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Assert(f.Windows[2].Visible,"fake async completed early");f.Complete();s.Update(new[]{"B"});Assert(s.HiddenCount==1,"hide not observed");
            s.Restore();Assert(File.Exists(path)&&s.PendingRestore&&!f.Windows[2].Visible,"removed recovery before show confirmation");s.Update(new string[0]);Assert(File.Exists(path),"dropped unconfirmed show");f.Complete();s.Update(new string[0]);Assert(f.Windows[2].Visible&&!File.Exists(path)&&!s.PendingRestore,"async restore not completed");
        });
        Case("leave-before-async-hide-completes",delegate(string path){
            var f=new FakeBars{Deferred=true};var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Assert(f.Windows[2].Visible,"initial pending hide already completed");
            s.Update(new string[0]);Assert(f.Queue.Any(q=>q.Key==2&&q.Value),"no show queued after pending hide");Assert(File.Exists(path),"removed journal on first visible observation");
            f.Complete();s.Update(new string[0]);Assert(f.Windows[2].Visible&&!File.Exists(path),"late hide orphaned taskbar");
        });
        Case("crash-before-hide-dispatch",delegate(string path){
            var f=new FakeBars{Deferred=true};var first=new TaskbarSession(f,path);first.Update(new[]{"B"});var recovered=new TaskbarSession(f,path);recovered.Restore();
            Assert(File.Exists(path)&&f.Queue.Any(q=>q.Key==2&&q.Value),"recovery failed to queue show after pending hide");f.Complete();recovered.Restore();Assert(f.Windows[2].Visible&&!File.Exists(path),"crash left delayed hide unhandled");
        });
        Case("reused-handle-not-touched",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});int calls=f.Calls.Count;f.Windows[2].ProcessId=99;f.Windows[2].ProcessStarted=2000;
            Finish(s);Assert(!f.Windows[2].Visible&&f.Calls.Count==calls&&!File.Exists(path),"changed replacement window");
        });
        Case("destroyed-window-retires-journal",delegate(string path){var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});f.Windows.Remove(2);int calls=f.Calls.Count;Finish(s);Assert(f.Calls.Count==calls&&!File.Exists(path),"touched destroyed taskbar");});
        Case("explorer-restart-new-identity",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});f.Windows.Remove(2);var replacement=f.Add(22,"B",true);replacement.ProcessId=100;replacement.ProcessStarted=3000;
            s.Update(new[]{"B"});Assert(!replacement.Visible&&File.Exists(path),"did not acquire replacement safely");Finish(s);Assert(replacement.Visible&&!File.Exists(path),"replacement taskbar not restored");
        });
        Case("monitor-switch-restores-previous",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});s.Update(new[]{"A"});s.Update(new[]{"A"});Assert(f.Windows[2].Visible&&!f.Windows[1].Visible&&f.Windows[3].Visible,"monitor handoff wrong");Finish(s);Assert(f.Windows[1].Visible&&!File.Exists(path),"handoff restore wrong");
        });
        Case("same-window-moved-monitor",delegate(string path){var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});f.Windows[2].Monitor="C";s.Update(new[]{"B"});s.Update(new[]{"B"});Assert(f.Windows[2].Visible&&!File.Exists(path),"monitor move prevented valid identity restore");});
        Case("temporary-monitor-enumeration-gap",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});f.Omitted.Add(2);s.Restore();Assert(File.Exists(path)&&s.PendingRestore,"dropped matching identity missing from monitor enumeration");f.Omitted.Clear();s.Restore();Assert(f.Windows[2].Visible&&!File.Exists(path),"gap did not recover");
        });
        Case("hide-failure-retry",delegate(string path){var f=new FakeBars{FailHide=true};var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Assert(File.Exists(path)&&s.Error.Length>0&&f.Windows[2].Visible,"hide failure lost state");f.FailHide=false;s.Update(new[]{"B"});Assert(!f.Windows[2].Visible,"hide retry failed");Finish(s);Assert(!File.Exists(path),"hide failure recovery did not finish");});
        Case("show-failure-retry",delegate(string path){var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});f.FailShow=true;s.Restore();Assert(File.Exists(path)&&s.PendingRestore&&s.Error.Length>0,"restore failure not retained");f.FailShow=false;Finish(s);Assert(f.Windows[2].Visible&&!File.Exists(path),"restore retry failed");});
        Case("query-failure-no-mutation",delegate(string path){var f=new FakeBars{FailRead=true};var s=new TaskbarSession(f,path);s.Update(new[]{"B"});Assert(f.Calls.Count==0&&!File.Exists(path)&&s.Error.Length>0,"query failure changed taskbar");});
        Case("corrupt-journal-preserved",delegate(string path){byte[] corrupt={1,2,3,4,5};File.WriteAllBytes(path,corrupt);var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});s.Restore();Assert(s.PendingRestore&&s.Error.Length>0&&f.Calls.Count==0&&corrupt.SequenceEqual(File.ReadAllBytes(path)),"corrupt journal overwritten or ignored");});
        Case("crash-recovery-completes-before-new-hide",delegate(string path){
            var f=new FakeBars();var first=new TaskbarSession(f,path);first.Update(new[]{"B"});var recovered=new TaskbarSession(f,path);recovered.Update(new[]{"A"});Assert(f.Windows[2].Visible&&f.Windows[1].Visible,"new hide started before restoration");recovered.Update(new[]{"A"});Assert(!File.Exists(path)&&f.Windows[1].Visible,"restore pass unexpectedly hid new scope");recovered.Update(new[]{"A"});Assert(!f.Windows[1].Visible,"new scope never started");Finish(recovered);
        });
        Case("journal-cleanup-failure-retry",delegate(string path){
            var f=new FakeBars();var s=new TaskbarSession(f,path);s.Update(new[]{"B"});s.Restore();
            using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){s.Update(new string[0]);Assert(s.PendingRestore&&s.Error.Length>0&&File.Exists(path),"failed cleanup not marked pending");}
            s.Update(new string[0]);Assert(!File.Exists(path)&&!s.PendingRestore&&s.Error.Length==0,"cleanup retry not completed");
        });
        Console.WriteLine("All "+passed+" taskbar scenarios passed. Fake backend only; no Windows taskbar changed.");
    }
}
