using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigPictureMonitores;

class AudioSessionTests {
    sealed class FakeAudio : IAudioBackend {
        public readonly Dictionary<int,string> Defaults=new Dictionary<int,string>{{0,"desktop-console"},{1,"desktop-media"},{2,"chat-headset"}};
        public readonly HashSet<string> Connected=new HashSet<string>{"desktop-console","desktop-media","chat-headset","tv","headphones","windows-fallback"};
        public readonly List<string> Calls=new List<string>();
        public Func<string,int,bool> FailSet;
        public bool FailRead;
        public AudioDevice[] Outputs(){return Connected.Select(id=>new AudioDevice{Id=id,Name=id}).ToArray();}
        public string DefaultId(int role){if(FailRead)throw new IOException("temporary read failure");return Defaults[role];}
        public bool Available(string id){return Connected.Contains(id);}
        public void SetDefault(string id,int role){
            Calls.Add(role+":"+id);
            if(FailSet!=null && FailSet(id,role))throw new IOException("temporary apply failure");
            if(!Connected.Contains(id))throw new IOException("endpoint missing");
            Defaults[role]=id;
        }
    }
    static int passed;
    static readonly string Root=Path.Combine(Environment.GetEnvironmentVariable("BPC_TEST_DATA_ROOT")??Path.GetTempPath(),"BigPictureControleTests",Guid.NewGuid().ToString("N"));
    static string Backup(string name){return Path.Combine(Root,name+".bin");}
    static void Assert(bool result,string message){if(!result)throw new Exception(message);}
    static void Defaults(FakeAudio audio,string console,string media){Assert(audio.Defaults[0]==console,"console mismatch");Assert(audio.Defaults[1]==media,"multimedia mismatch");Assert(audio.Defaults[2]=="chat-headset","communications changed");Assert(audio.Calls.All(call=>!call.StartsWith("2:")),"communications was written");}
    static void Case(string name,Action<string> run){
        string path=Backup(name);
        if(File.Exists(path))File.Delete(path);
        if(File.Exists(path+".new"))File.Delete(path+".new");
        try{run(path);passed++;Console.WriteLine("PASS "+name);}
        finally{if(File.Exists(path))File.Delete(path);if(File.Exists(path+".new"))File.Delete(path+".new");}
    }
    static void Main(){
        try{Run();}
        finally{if(Directory.Exists(Root) && !Directory.EnumerateFileSystemEntries(Root).Any())Directory.Delete(Root);}
    }
    static void Run(){
        Directory.CreateDirectory(Root);
        Case("outside-big-picture",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);
            session.Update(false,"tv");session.Update(false,"headphones");
            Assert(fake.Calls.Count==0,"applied outside Big Picture");Assert(!File.Exists(path),"created unnecessary backup");Defaults(fake,"desktop-console","desktop-media");
        });
        Case("activate-close-distinct-roles",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);
            session.Update(true,"tv");Assert(File.Exists(path),"backup missing: "+session.Error+" ["+path+"]");Assert(session.Switched,"not marked switched");Defaults(fake,"tv","tv");
            int calls=fake.Calls.Count;session.Update(true,"tv");Assert(fake.Calls.Count==calls,"rewrote already selected output");
            session.Update(false,"tv");Defaults(fake,"desktop-console","desktop-media");Assert(!File.Exists(path),"backup not removed");Assert(!session.PendingRestore && !session.Switched,"session not cleared");
        });
        Case("selection-change-preserves-baseline",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);session.Update(true,"tv");byte[] baseline=File.ReadAllBytes(path);
            session.Update(true,"headphones");Defaults(fake,"headphones","headphones");Assert(baseline.SequenceEqual(File.ReadAllBytes(path)),"baseline overwritten");
            session.Restore();Defaults(fake,"desktop-console","desktop-media");
        });
        Case("capture-before-preferred-availability",delegate(string path){
            var fake=new FakeAudio();fake.Connected.Remove("tv");var session=new AudioSession(fake,path);session.Update(true,"tv");
            Assert(fake.Calls.Count==0,"wrote unavailable output");Assert(File.Exists(path),"did not capture before endpoint appeared");
            fake.Defaults[0]="windows-fallback";fake.Defaults[1]="windows-fallback";fake.Connected.Add("tv");session.Update(true,"tv");Defaults(fake,"tv","tv");
            session.Restore();Defaults(fake,"desktop-console","desktop-media");
        });
        Case("partial-application-rollback",delegate(string path){
            var fake=new FakeAudio();bool fail=true;fake.FailSet=delegate(string id,int role){return id=="tv" && role==1 && fail;};
            var session=new AudioSession(fake,path);session.Update(true,"tv");Defaults(fake,"desktop-console","desktop-media");
            Assert(File.Exists(path),"partial failure lost recovery backup");Assert(session.Error.Length>0,"partial failure not reported");
            fail=false;session.Update(true,"tv");Defaults(fake,"tv","tv");session.Restore();Defaults(fake,"desktop-console","desktop-media");
        });
        Case("rollback-pending-retry",delegate(string path){
            var fake=new FakeAudio();bool fail=true;fake.FailSet=delegate(string id,int role){return fail && (id=="tv" && role==1 || id=="desktop-console" && role==0);};
            var session=new AudioSession(fake,path);session.Update(true,"tv");Assert(session.PendingRestore,"failed rollback not pending");Assert(File.Exists(path),"recovery backup lost");
            fail=false;session.Update(false,"tv");Defaults(fake,"desktop-console","desktop-media");Assert(!session.PendingRestore,"rollback did not finish");
        });
        Case("original-hdmi-disconnected",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);session.Update(true,"tv");fake.Connected.Remove("desktop-media");
            session.Restore();Assert(session.PendingRestore,"missing original endpoint not pending");Assert(File.Exists(path),"missing endpoint lost backup");Assert(fake.Defaults[0]=="desktop-console" && fake.Defaults[1]=="tv","unexpected partial restore");
            fake.Connected.Add("desktop-media");session.Update(false,"tv");Defaults(fake,"desktop-console","desktop-media");Assert(!session.PendingRestore && !File.Exists(path),"did not finish after reconnect");
        });
        Case("crash-backup-recovery",delegate(string path){
            var fake=new FakeAudio();var first=new AudioSession(fake,path);first.Update(true,"tv");
            var recovered=new AudioSession(fake,path);Assert(recovered.PendingRestore,"existing backup not loaded");recovered.Update(false,"tv");
            Defaults(fake,"desktop-console","desktop-media");Assert(!File.Exists(path),"crash recovery kept completed backup");
        });
        Case("recover-before-new-session",delegate(string path){
            var fake=new FakeAudio();var first=new AudioSession(fake,path);first.Update(true,"tv");byte[] before=File.ReadAllBytes(path);
            var recovered=new AudioSession(fake,path);recovered.Update(true,"headphones");Defaults(fake,"headphones","headphones");Assert(before.SequenceEqual(File.ReadAllBytes(path)),"new session captured previous game target");
            recovered.Restore();Defaults(fake,"desktop-console","desktop-media");
        });
        Case("empty-preference-restores",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);session.Update(true,"tv");session.Update(true,"");Defaults(fake,"desktop-console","desktop-media");Assert(!File.Exists(path),"disabled selection not restored");
        });
        Case("corrupt-backup-preserved",delegate(string path){
            byte[] corrupt=new byte[]{1,2,3,4,5};File.WriteAllBytes(path,corrupt);var fake=new FakeAudio();var session=new AudioSession(fake,path);
            session.Update(true,"tv");session.Restore();Assert(fake.Calls.Count==0,"applied over corrupt backup");Assert(session.Error.Length>0 && session.PendingRestore,"corrupt backup not surfaced");Assert(corrupt.SequenceEqual(File.ReadAllBytes(path)),"corrupt backup overwritten");
        });
        Case("default-query-error-retry",delegate(string path){
            var fake=new FakeAudio{FailRead=true};var session=new AudioSession(fake,path);session.Update(true,"tv");Assert(fake.Calls.Count==0 && !File.Exists(path),"applied without original defaults");Assert(session.Error.Length>0,"query error hidden");
            fake.FailRead=false;session.Update(true,"tv");Defaults(fake,"tv","tv");session.Restore();Defaults(fake,"desktop-console","desktop-media");
        });
        Case("missing-original-default-blocks-apply",delegate(string path){
            var fake=new FakeAudio();fake.Defaults[1]="";var session=new AudioSession(fake,path);session.Update(true,"tv");Assert(fake.Calls.Count==0 && !File.Exists(path),"applied without all originals");Assert(session.Error.Length>0,"missing original not reported");
        });
        Case("restore-apply-error-retry",delegate(string path){
            var fake=new FakeAudio();var session=new AudioSession(fake,path);session.Update(true,"tv");bool fail=true;fake.FailSet=delegate(string id,int role){return fail && id=="desktop-media";};
            session.Restore();Assert(session.PendingRestore && File.Exists(path),"restore failure not recoverable");fail=false;session.Update(false,"tv");Defaults(fake,"desktop-console","desktop-media");Assert(!session.PendingRestore && !File.Exists(path),"restore retry failed");
        });
        Console.WriteLine("All "+passed+" audio session scenarios passed. Fake backend only; no Windows audio output changed.");
    }
}
