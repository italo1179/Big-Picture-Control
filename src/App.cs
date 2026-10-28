using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BigPictureMonitores {
    static class Files {
        public static readonly string BinaryRoot=AppDomain.CurrentDomain.BaseDirectory;
        public static string Root {get;private set;}
        static string previewRoot;
        public static void Initialize(bool preview) {
            Root=preview?Path.Combine(Path.GetTempPath(),"BigPictureControle-Preview-"+Guid.NewGuid().ToString("N")):
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BigPictureControle");
            Directory.CreateDirectory(Root);
            if(preview)previewRoot=Root;
        }
        public static void CleanPreview() {
            if(previewRoot==null)return;
            string temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string parent=Path.GetDirectoryName(Path.GetFullPath(previewRoot));
            if(String.Equals(parent,temp,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(previewRoot).StartsWith("BigPictureControle-Preview-",StringComparison.Ordinal))
                try{Directory.Delete(previewRoot,true);}catch(IOException){}catch(UnauthorizedAccessException){}
            previewRoot=null;
        }
        public static void ImportSettings(string source) {
            string directory=Path.GetFullPath(source);
            if(!Directory.Exists(directory))throw new DirectoryNotFoundException("A pasta de configurações não existe.");
            foreach(string name in new[]{"principal.txt","audio-automatico.txt","audio-nome.txt","audio-saida.txt","barra-automatica.txt"}) {
                string input=Path.Combine(directory,name),output=At(name);
                if(!File.Exists(input)||File.Exists(output))continue;
                if(new FileInfo(input).Length>16384)throw new IOException("Arquivo de configuração muito grande: "+name);
                string value=File.ReadAllText(input,Encoding.UTF8).Trim();
                if((name=="audio-automatico.txt"||name=="barra-automatica.txt")&&value!="0"&&value!="1")continue;
                File.WriteAllText(output,value,Encoding.UTF8);
            }
        }
        public static string At(string name) { return Path.Combine(Root,name); }
        public static string AudioBackup {get{return At("audio-originais.bin");}}
        public static string TaskbarBackup {get{return At("barra-restaurar.bin");}}
        public static void Log(string text) {
            try { File.AppendAllText(At("app.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ")+text+Environment.NewLine,Encoding.UTF8); } catch { }
        }
        public static void Restore() {
            string backup=At("monitores-originais.bin");
            try {
                if(File.Exists(backup)) {
                    try { Native.Apply(Native.WithPrimary(Native.Load(backup),Settings.Primary)); }
                    catch(Exception ex) { Log("Usando layout salvo no Windows: "+ex.Message); Native.RestoreDatabase(); Native.Apply(Native.WithPrimary(Native.Capture(),Settings.Primary)); }
                    File.Delete(backup);
                    Log("Monitores restaurados.");
                }
            } finally {
                try {
                    if(File.Exists(At("cursor-oculto.flag"))) {
                        BPCursor.Show(); File.Delete(At("cursor-oculto.flag")); Log("Cursor restaurado.");
                    }
                }finally{
                    try{AudioSession.RestorePending(AudioBackup,Log);}
                    finally{TaskbarSession.RestorePending(TaskbarBackup,Log);}
                }
            }
        }
        public static bool Acquire(Mutex mutex,int timeout) {
            try { return mutex.WaitOne(timeout); }
            catch(AbandonedMutexException) { return true; }
        }
    }

    static class Settings {
        public static string Primary=File.Exists(Files.At("principal.txt"))?File.ReadAllText(Files.At("principal.txt")).Trim():"";
        public static void SetPrimary(string device) { File.WriteAllText(Files.At("principal.txt"),device); Primary=device; }
        static string Read(string name){try{return File.Exists(Files.At(name))?File.ReadAllText(Files.At(name)).Trim():"";}catch{return "";}}
        public static string AudioOutput=Read("audio-saida.txt");
        public static string AudioName=Read("audio-nome.txt");
        public static bool AudioEnabled=Read("audio-automatico.txt")=="1" && AudioOutput.Length>0;
        public static bool TaskbarEnabled=Read("barra-automatica.txt")!="0";
        public static void SetTaskbarEnabled(bool enabled) {
            File.WriteAllText(Files.At("barra-automatica.txt"),enabled?"1":"0");TaskbarEnabled=enabled;
        }
        public static void SetAudioOutput(string id,string name) {
            File.WriteAllText(Files.At("audio-nome.txt"),name,Encoding.UTF8);
            File.WriteAllText(Files.At("audio-saida.txt"),id,Encoding.UTF8);
            File.WriteAllText(Files.At("audio-automatico.txt"),id.Length>0?"1":"0");
            AudioOutput=id;AudioName=name;AudioEnabled=id.Length>0;
        }
        public static void SetAudioEnabled(bool enabled) {
            enabled=enabled && AudioOutput.Length>0;
            File.WriteAllText(Files.At("audio-automatico.txt"),enabled?"1":"0");AudioEnabled=enabled;
        }
    }

    static class Program {
        public const string MutexName="Local\\ItaloBigPictureAppV1";
        public static readonly string[] Commands={"show","toggle-pause","toggle-cursor","show-cursor","restore","exit","resume","startup"};
        public static string EventName(string command) { return "Local\\ItaloBigPictureAppV1-"+command; }
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args) {
            SetProcessDPIAware();
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool preview=args.Length>0 && (args[0]=="--preview"||args[0]=="--design");
            try{Files.Initialize(preview);}catch(Exception ex){MessageBox.Show(ex.Message,"Big Picture Controle",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
            if(args.Length>0 && args[0]=="--guard") { Guard(args); return; }
            if(args.Length>0 && args[0]=="--import-settings") {
                using(var single=new Mutex(false,MutexName)) {
                    bool acquired=Files.Acquire(single,8000);
                    try{if(!acquired)throw new IOException("Feche o aplicativo antes de importar configurações.");if(args.Length!=2)throw new ArgumentException("Informe a pasta das configurações.");Files.ImportSettings(args[1]);}
                    catch(Exception ex){Files.Log(ex.Message);Environment.ExitCode=1;}
                    finally{if(acquired)single.ReleaseMutex();}
                }
                return;
            }
            if(args.Length>0 && args[0]=="--preview") { try{Preview(args);}finally{Files.CleanPreview();}return; }
            if(args.Length>0 && args[0]=="--design") {
                try{using(var c=new Controller()) using(var f=new MainForm(c,true)) Application.Run(f);}finally{Files.CleanPreview();}
                return;
            }
            string command=args.Length>0?args[0].TrimStart('-'):"show";
            if(!Commands.Contains(command)) command="show";
            using(var single=new Mutex(false,MutexName)) {
                if(!Files.Acquire(single,1000)) {
                    using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,EventName(command))) signal.Set();
                    return;
                }
                var locks=new List<Mutex>();
                try {
                    // Stop the older scripts cleanly, then hold their locks to prevent duplicates.
                    File.WriteAllText(Files.At("parar.flag"),"parar");
                    File.WriteAllText(Files.At("cursor-parar.flag"),"parar");
                    foreach(string name in new[]{"Local\\ItaloBigPictureMonitoresV1","Local\\ItaloBigPictureCursorV1"}) {
                        var m=new Mutex(false,name);
                        if(!Files.Acquire(m,15000)) { m.Dispose(); throw new Exception("O auxiliar anterior ainda esta encerrando. Tente abrir novamente."); }
                        locks.Add(m);
                    }
                    Files.Restore();
                    if(command=="exit") return;
                    using(var controller=new Controller())
                    using(var form=new MainForm(controller,false)) {
                        StartGuard();
                        form.InitialCommand=command;
                        Application.Run(form);
                    }
                } catch(Exception ex) {
                    Files.Log("ERRO: "+ex);
                    try { Files.Restore(); } catch(Exception restoreError) { Files.Log("Falha na recuperacao: "+restoreError); }
                    MessageBox.Show(ex.Message,"Big Picture Controle",MessageBoxButtons.OK,MessageBoxIcon.Error);
                } finally {
                    foreach(var m in locks) { m.ReleaseMutex(); m.Dispose(); }
                    single.ReleaseMutex();
                }
            }
        }
        static void StartGuard() {
            var p=Process.GetCurrentProcess();
            var info=new ProcessStartInfo(Application.ExecutablePath,"--guard "+p.Id+" "+p.StartTime.ToUniversalTime().Ticks);
            info.UseShellExecute=false; info.CreateNoWindow=true; info.WindowStyle=ProcessWindowStyle.Hidden;
            Process.Start(info).Dispose();
        }
        static void Guard(string[] args) {
            try {
                int id=int.Parse(args[1]); long ticks=long.Parse(args[2]);
                try {
                    using(var p=Process.GetProcessById(id)) {
                        if(p.StartTime.ToUniversalTime().Ticks==ticks) p.WaitForExit();
                    }
                } catch(ArgumentException) { }
                using(var mutex=new Mutex(false,MutexName)) {
                    if(!Files.Acquire(mutex,1000)) return;
                    try {
                        Files.Restore();
                        var taskbarRecovery=new TaskbarSession(new WindowsTaskbarBackend(),Files.TaskbarBackup,Files.Log);
                        taskbarRecovery.Restore();
                        for(int attempt=0;attempt<4 && (File.Exists(Files.AudioBackup)||File.Exists(Files.TaskbarBackup));attempt++) {
                            Thread.Sleep(1000);AudioSession.RestorePending(Files.AudioBackup,Files.Log);
                            taskbarRecovery.Restore();
                        }
                    } finally { mutex.ReleaseMutex(); }
                }
            } catch(Exception ex) { Files.Log("Recuperacao: "+ex.Message); }
        }
        static void Preview(string[] args) {
            float scale=0;
            if(args.Length>1) Single.TryParse(args[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out scale);
            using(var c=new Controller()) using(var f=new MainForm(c,true,scale)) {
                f.Show(); f.Refresh(); Application.DoEvents();
                using(var bitmap=new Bitmap(f.ClientSize.Width,f.ClientSize.Height)) {
                    f.DrawToBitmap(bitmap,f.ClientRectangle);
                    string output=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(Files.BinaryRoot,"app-preview.png");
                    bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);
                }
                f.Close();
            }
        }
    }

    sealed class Controller : IDisposable {
        public bool Automatic { get; private set; }
        public bool Gaming { get; private set; }
        public bool CursorHidden { get; private set; }
        public string SelectedDevice { get; private set; }
        public string Error { get; private set; }
        public ScreenInfo[] KnownScreens { get; private set; }
        public HashSet<string> ActiveDevices=new HashSet<string>();
        readonly AudioSession audio;
        readonly TaskbarSession taskbars;
        string taskbarConfigError="",taskbarRuntimeError="";
        DateTime nextTaskbarUpdate=DateTime.MinValue;
        public string TaskbarError {get{return taskbarConfigError.Length>0?taskbarConfigError:taskbarRuntimeError.Length>0?taskbarRuntimeError:taskbars.Error;}}
        public string TaskbarStatus {get{return taskbars.PendingRestore?taskbars.Status:!Settings.TaskbarEnabled?"Barra de tarefas mantida no modo normal.":taskbars.Status;}}
        public AudioDevice[] AudioOutputs {get;private set;}
        string audioListError="",audioConfigError="";
        DateTime nextAudioUpdate=DateTime.MinValue;
        public string AudioError {get{return audioConfigError.Length>0?audioConfigError:audio.Error.Length>0?audio.Error:audioListError;}}
        public string AudioStatus {get{return audio.PendingRestore?audio.Status:!Settings.AudioEnabled?"Ative para trocar o som só com o Big Picture aberto.":audio.Status;}}
        Point mousePosition=Cursor.Position; DateTime mouseMoved=DateTime.UtcNow; bool cursorEnabled=true;
        string candidate="";
        DateTime stableSince=DateTime.UtcNow, absentSince=DateTime.MinValue, lastRefresh=DateTime.MinValue;
        bool dirty=true;
        public event Action Changed;
        public Controller() {
            Automatic=true; SelectedDevice=""; Error="";
            audio=new AudioSession(new WindowsAudioBackend(),Files.AudioBackup,Files.Log);
            taskbars=new TaskbarSession(new WindowsTaskbarBackend(),Files.TaskbarBackup,Files.Log);
            AudioOutputs=new AudioDevice[0];RefreshAudio();
            KnownScreens=new ScreenInfo[0]; RefreshScreens();
        }
        public void RefreshAudio() {
            try{AudioOutputs=AudioNative.Outputs();audioListError="";}
            catch(Exception ex){audioListError="Não foi possível listar as saídas de áudio: "+ex.Message;}
        }
        public void ToggleTaskbar() {
            try{
                Settings.SetTaskbarEnabled(!Settings.TaskbarEnabled);taskbarConfigError="";nextTaskbarUpdate=DateTime.MinValue;
                if(!Settings.TaskbarEnabled)taskbars.Restore();
            }catch(Exception ex){taskbarConfigError="Não foi possível salvar a opção da barra: "+ex.Message;}
            Notify();
        }
        void UpdateTaskbar(SteamWindow[] windows) {
            if(DateTime.UtcNow<nextTaskbarUpdate)return;
            nextTaskbarUpdate=DateTime.UtcNow.AddMilliseconds(500);
            string before=taskbars.Status+TaskbarError+taskbars.HiddenCount+taskbars.PendingRestore;
            try{taskbars.Update(Automatic && Settings.TaskbarEnabled?TaskbarNative.FullscreenMonitors(windows):new string[0]);taskbarRuntimeError="";}
            catch(Exception ex){taskbarRuntimeError="Não foi possível ajustar a barra: "+ex.Message;taskbars.Restore();}
            if(before!=taskbars.Status+TaskbarError+taskbars.HiddenCount+taskbars.PendingRestore)Notify();
        }
        public void SetAudioOutput(string id,string name) {
            try{Settings.SetAudioOutput(id,name);audioConfigError="";nextAudioUpdate=DateTime.MinValue;if(id.Length==0)audio.Restore();}
            catch(Exception ex){audioConfigError="Não foi possível salvar a saída de áudio: "+ex.Message;}
            Notify();
        }
        public void ToggleAudio() {
            try{Settings.SetAudioEnabled(!Settings.AudioEnabled);audioConfigError="";nextAudioUpdate=DateTime.MinValue;if(!Settings.AudioEnabled)audio.Restore();}
            catch(Exception ex){audioConfigError="Não foi possível salvar a opção de áudio: "+ex.Message;}
            Notify();
        }
        void UpdateAudio(bool bigPicturePresent) {
            if(DateTime.UtcNow<nextAudioUpdate)return;
            nextAudioUpdate=DateTime.UtcNow.AddSeconds(2);
            string before=audio.Status+audio.Error+audio.Switched+audio.PendingRestore;
            audio.Update(Automatic && Settings.AudioEnabled && bigPicturePresent,Settings.AudioOutput);
            if(before!=audio.Status+audio.Error+audio.Switched+audio.PendingRestore)Notify();
        }
        public void RefreshScreens() {
            var screens=Native.Screens();
            ActiveDevices=new HashSet<string>(screens.Select(x=>x.Device));
            // Keep the original names while other outputs are temporarily disabled.
            if(!Gaming) KnownScreens=screens;
            lastRefresh=DateTime.UtcNow;
        }
        public string SelectedName {
            get { var s=KnownScreens.FirstOrDefault(x=>x.Device==SelectedDevice); return s==null?SelectedDevice:s.Name; }
        }
        public void TogglePause() { SetAutomatic(!Automatic); }
        public void SetAutomatic(bool enabled) {
            if(!enabled) RestoreSession();
            Automatic=enabled; candidate=""; Error="";nextAudioUpdate=DateTime.MinValue;
            Files.Log(enabled?"Automatico ativado.":"Automatico pausado; telas disponiveis para troca.");
            Notify();
        }
        public void RestoreSession() {
            Files.Restore(); Gaming=false; CursorHidden=false; SelectedDevice="";
            audio.Restore();nextAudioUpdate=DateTime.MinValue;
            taskbars.Restore();nextTaskbarUpdate=DateTime.MinValue;
            absentSince=DateTime.MinValue; candidate=""; RefreshScreens();
        }
        public void RestoreAll() { Automatic=false; RestoreSession(); Error=""; Notify(); }
        public bool CursorEnabled { get { return cursorEnabled; } }
        public void ToggleCursor() {
            cursorEnabled=!cursorEnabled; mouseMoved=DateTime.UtcNow;
            if(CursorHidden) ShowCursor();
            Notify();
        }
        void UpdateCursor(bool bigPictureOpen) {
            Point position=Cursor.Position;
            if(position!=mousePosition) { mousePosition=position; mouseMoved=DateTime.UtcNow; }
            bool hide=cursorEnabled && Automatic && Gaming && bigPictureOpen && (DateTime.UtcNow-mouseMoved).TotalSeconds>=5;
            if(hide && !CursorHidden) {
                File.WriteAllText(Files.At("cursor-oculto.flag"),"oculto");
                BPCursor.Hide(); CursorHidden=true; Notify();
            } else if(!hide && CursorHidden) ShowCursor();
        }        public void ShowCursor() {
            mouseMoved=DateTime.UtcNow; BPCursor.Show(); File.Delete(Files.At("cursor-oculto.flag"));
            CursorHidden=false; Files.Log("Cursor visivel."); Notify();
        }
        static bool GameRunning() {
            try {
                using(var key=Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam")) {
                    object value=key==null?null:key.GetValue("RunningAppID");
                    return value==null || Convert.ToInt64(value)!=0;
                }
            } catch { return true; }
        }
        public void Tick() {
            bool bigPictureOpen=false;
            var windows=Automatic?Native.Windows().Where(w=>Regex.IsMatch(w.Title,@"^Steam\b.*\bBig Picture\b",RegexOptions.IgnoreCase)).ToArray():new SteamWindow[0];
            // Audio follows the existing Big Picture window, including while a launched game covers it.
            // This runs before monitor isolation, so the original HDMI output is still discoverable.
            UpdateAudio(windows.Length>0);
            if(Automatic) {
                bigPictureOpen=windows.Any(w=>w.Visible && !w.Minimized);
                if(!Gaming) {
                    var front=windows.Where(w=>w.Foreground && w.Visible && !w.Minimized && w.Monitor!=null).ToArray();
                    if(front.Length==1) {
                        var w=front[0]; string key=w.Handle+":"+w.Monitor;
                        if(key!=candidate) { candidate=key; stableSince=DateTime.UtcNow; }
                        else if((DateTime.UtcNow-stableSince).TotalSeconds>=2) {
                            var original=Native.Capture();
                            if(original.Paths.Length>1) {
                                RefreshScreens(); var one=Native.Single(original,w.Monitor); Native.Validate(one);
                                Native.Save(original,Files.At("monitores-originais.bin"));
                                try { Native.Apply(one); }
                                catch { Files.Restore(); throw; }
                            }
                            Gaming=true; mouseMoved=DateTime.UtcNow; mousePosition=Cursor.Position; SelectedDevice=w.Monitor; RefreshScreens();
                            Files.Log("Big Picture: somente "+SelectedName+" ativo.");
                            candidate=""; Notify();
                        }
                    } else candidate="";
                } else {
                    if(windows.Length>0 || GameRunning()) absentSince=DateTime.MinValue;
                    else if(absentSince==DateTime.MinValue) absentSince=DateTime.UtcNow;
                    else if((DateTime.UtcNow-absentSince).TotalSeconds>=8) { RestoreSession(); Notify(); }
                }
            }
            UpdateCursor(bigPictureOpen);
            UpdateTaskbar(windows);
            if((DateTime.UtcNow-lastRefresh).TotalSeconds>=4) { RefreshScreens();RefreshAudio();Notify(); }
            if(dirty) { WriteStatus(); dirty=false; }
        }
        public void Fail(Exception ex) {
            Automatic=false; candidate="";
            try { RestoreSession(); } catch(Exception restoreError) { Files.Log("Restauracao pendente: "+restoreError.Message); }
            Error=ex.Message; Files.Log("ERRO: "+ex); Notify();
        }
        void WriteStatus() {
            File.WriteAllText(Files.At("app-status.txt"),"Automatic="+Automatic+"\r\nGaming="+Gaming+"\r\nCursorHidden="+CursorHidden+"\r\nActiveDisplays="+ActiveDevices.Count+"\r\nSelected="+SelectedName+"\r\nError="+Error+"\r\nAudioAutomatic="+Settings.AudioEnabled+"\r\nAudioSwitched="+audio.Switched+"\r\nAudioOutput="+Settings.AudioName+"\r\nAudioStatus="+AudioStatus+"\r\nAudioError="+AudioError+"\r\nAudioRestorePending="+audio.PendingRestore+"\r\nTaskbarAutomatic="+Settings.TaskbarEnabled+"\r\nHiddenTaskbars="+taskbars.HiddenCount+"\r\nTaskbarStatus="+TaskbarStatus+"\r\nTaskbarError="+TaskbarError+"\r\nTaskbarRestorePending="+taskbars.PendingRestore+"\r\n",Encoding.UTF8);
        }
        public void Notify() { dirty=true; if(Changed!=null) Changed(); }
        public void Dispose() {try{audio.Restore();}finally{taskbars.Restore();}}
    }

}
