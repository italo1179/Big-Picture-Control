using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace BigPictureMonitores {
    static class UiLayout {
        public static int P(float value) { return (int)Math.Round(value*SteamTheme.Scale); }
        public static Rectangle R(float x,float y,float w,float h) { return new Rectangle(P(x),P(y),P(w),P(h)); }
        public static void At(Control c,float x,float y,float w,float h) { c.Bounds=R(x,y,w,h); }
        public static void Text(Graphics g,string value,float size,Color color,float x,float y,float w,float h,bool bold=false,StringAlignment align=StringAlignment.Near) {
            using(var font=SteamTheme.UI(size,bold)) SteamTheme.DrawText(g,value,font,color,R(x,y,w,h),align);
        }
        public static void Line(Graphics g,string value,float size,Color color,float x,float y,float w,float h,bool bold=false) {
            using(var font=SteamTheme.UI(size,bold)) SteamTheme.DrawLine(g,value,font,color,R(x,y,w,h));
        }
    }

    sealed class SteamText : Control {
        public SteamText() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent; ForeColor=SteamTheme.Text; AccessibleRole=AccessibleRole.StaticText;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this)); }
        protected override void OnTextChanged(EventArgs e) {base.OnTextChanged(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this));SteamTheme.DrawText(e.Graphics,Text,Font,ForeColor,ClientRectangle); }
    }

    sealed class SetupHero : SmoothCard {
        public bool Automatic, Gaming;
        public string MonitorName="";
        public string Error="";
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=SteamTheme.Round(new RectangleF(1,1,Width-2,Height-2),UiLayout.P(16))) {
                var save=g.Save(); g.SetClip(path);
                using(var brush=new LinearGradientBrush(ClientRectangle,Color.FromArgb(25,48,68),Color.FromArgb(22,35,51),10)) g.FillPath(brush,path);
                using(var pen=new Pen(Color.FromArgb(27,78,107),UiLayout.P(1))) {
                    for(int i=0;i<5;i++) g.DrawEllipse(pen,UiLayout.R(660-i*24,-65-i*24,210+i*48,210+i*48));
                }
                SteamTheme.Icon(g,"gamepad",UiLayout.R(725,16,96,90),Color.FromArgb(62,116,145));
                g.Restore(save);
            }
            var stateColor=Error.Length>0?Color.FromArgb(242,170,126):Automatic?SteamTheme.Green:SteamTheme.Muted;
            using(var brush=new SolidBrush(stateColor)) g.FillEllipse(brush,UiLayout.R(25,21,6,6));
            UiLayout.Text(g,Error.Length>0?"PRECISA DE ATENÇÃO":Gaming?"MODO DE JOGO":Automatic?"AUTOMAÇÃO ATIVA":"AUTOMAÇÃO PAUSADA",8.5f,stateColor,39,14,620,21,true);
            UiLayout.Text(g,Error.Length>0?"Vamos recuperar seu setup":Gaming?"Jogando em "+MonitorName:Automatic?"Seu setup está pronto para jogar.":"Você está no controle das telas.",21,SteamTheme.Text,23,43,690,34,true);
            UiLayout.Text(g,Error.Length>0?Error:Gaming?"Ao sair do Big Picture, suas telas voltam ao layout salvo.":Automatic?"Abra o Big Picture. As telas e o mouse se ajustam automaticamente.":"Ative a automação abaixo quando quiser entrar no modo de jogo.",10,SteamTheme.Muted,24,85,680,23);
        }
    }

    sealed class MonitorTile : Button {
        public ScreenInfo Display;
        public bool Active,Preferred,InGame,DefaultPrimary;
        bool hover;
        public MonitorTile() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
            FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; UseVisualStyleBackColor=false;
            BackColor=Color.Transparent; Cursor=Cursors.Hand; TabStop=true;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this)); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover=true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover=false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) {base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e) {base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(SteamTheme.Surface(this));g.SmoothingMode=SmoothingMode.AntiAlias;
            bool focus=Focused && ShowFocusCues;
            Color edge=Preferred||focus?SteamTheme.Accent:hover?Color.FromArgb(75,113,143):SteamTheme.Border;
            using(var path=SteamTheme.Round(new RectangleF(1,1,Width-2,Height-2),UiLayout.P(14))) {
                using(var brush=new SolidBrush(Preferred?Color.FromArgb(28,52,72):hover?Color.FromArgb(31,47,65):SteamTheme.Card)) g.FillPath(brush,path);
                using(var pen=new Pen(edge,Preferred||focus?UiLayout.P(1.5f):UiLayout.P(1))) g.DrawPath(pen,path);
            }
            if(Display==null) return;
            string digits=new string(Display.Device.Where(Char.IsDigit).ToArray());
            string name=Display.Name;
            bool tv=name.IndexOf("TV",StringComparison.OrdinalIgnoreCase)>=0;
            UiLayout.Text(g,(tv?"TV ":"MONITOR ")+digits,8,SteamTheme.Muted,16,12,105,20,true);
            using(var brush=new SolidBrush(Active?SteamTheme.Green:Color.FromArgb(93,111,129))) g.FillEllipse(brush,UiLayout.R(Width/SteamTheme.Scale-61,19,5,5));
            UiLayout.Text(g,Active?"Ativo":"Inativo",7.5f,Active?SteamTheme.Green:SteamTheme.Muted,Width/SteamTheme.Scale-51,12,41,18);
            float center=Width/SteamTheme.Scale/2;
            using(var glow=new SolidBrush(Color.FromArgb(Preferred?30:20,102,192,244))) g.FillEllipse(glow,UiLayout.R(center-49,35,98,64));
            var displayRect=UiLayout.R(center-33,40,66,44);
            using(var path=SteamTheme.Round(displayRect,UiLayout.P(4))) {
                using(var brush=new LinearGradientBrush(displayRect,Color.FromArgb(38,80,109),Color.FromArgb(22,43,62),65))g.FillPath(brush,path);
                using(var pen=new Pen(Active?SteamTheme.Accent:SteamTheme.Muted,UiLayout.P(2)))g.DrawPath(pen,path);
            }
            UiLayout.Text(g,digits,18,SteamTheme.Text,center-28,43,56,36,true,StringAlignment.Center);
            using(var pen=new Pen(Active?SteamTheme.Accent:SteamTheme.Muted,UiLayout.P(2))) {
                pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;
                if(tv) {g.DrawLine(pen,UiLayout.P(center-23),UiLayout.P(85),UiLayout.P(center-28),UiLayout.P(92));g.DrawLine(pen,UiLayout.P(center+23),UiLayout.P(85),UiLayout.P(center+28),UiLayout.P(92));}
                else {g.DrawLine(pen,UiLayout.P(center),UiLayout.P(86),UiLayout.P(center),UiLayout.P(94));g.DrawLine(pen,UiLayout.P(center-14),UiLayout.P(95),UiLayout.P(center+14),UiLayout.P(95));}
            }
            UiLayout.Line(g,name,11.5f,SteamTheme.Text,15,104,Width/SteamTheme.Scale-30,25,true);
            string detail=(Display.Width>0?Display.Width+" × "+Display.Height:"Tela conectada")+(tv?"  ·  TV":"");
            UiLayout.Text(g,detail,8.5f,SteamTheme.Muted,15,131,Width/SteamTheme.Scale-30,19);
            string badge=InGame?"EM JOGO":Preferred&&!DefaultPrimary?"PRINCIPAL AO VOLTAR":Display.Primary?"PRINCIPAL DO DESKTOP":"";
            if(badge.Length>0) UiLayout.Text(g,badge,7.2f,InGame?SteamTheme.Green:Preferred?SteamTheme.Accent:SteamTheme.Muted,15,152,Width/SteamTheme.Scale-30,15,true);
        }
    }

    sealed class MainForm : Form {
        readonly Controller controller;
        readonly bool preview;
        readonly Dictionary<string,EventWaitHandle> events=new Dictionary<string,EventWaitHandle>();
        readonly List<MonitorTile> tiles=new List<MonitorTile>();
        readonly List<Control> monitorControls=new List<Control>();
        readonly ToolTip tips=new ToolTip();
        SteamSwitch automaticSwitch,cursorSwitch,audioSwitch,taskbarSwitch,trayAutomaticSwitch,trayCursorSwitch,trayAudioSwitch,trayTaskbarSwitch;
        SteamSelect primarySelect,audioSelect;
        SteamText monitorCount,cursorState,notice,returnHint,audioHint,taskbarHint;
        readonly List<AudioDevice> audioChoices=new List<AudioDevice>();
        string audioKey="";
        SetupHero hero;
        NotifyIcon tray;
        Icon appIcon;
        System.Windows.Forms.Timer timer;
        bool quitting,resourcesDisposed,runtimeInitialized,initializationQueued,pauseHotkey,cursorHotkey;
        string screenKey="";
        public string InitialCommand="show";
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int size);
        public MainForm(Controller controller,bool preview,float designScale=0) {
            this.controller=controller;this.preview=preview;
            float dpi;using(var g=Graphics.FromHwnd(IntPtr.Zero)) dpi=g.DpiX/96f;
            var work=Screen.FromPoint(Cursor.Position).WorkingArea;
            SteamTheme.Scale=Math.Min(dpi,Math.Min((work.Width-40)/1120f,(work.Height-40)/1032f));
            SteamTheme.Scale=Math.Max(0.65f,SteamTheme.Scale);
            if(preview && designScale>=0.75f && designScale<=2f)SteamTheme.Scale=designScale;
            AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            Text="Big Picture Controle";BackColor=SteamTheme.Background;ForeColor=SteamTheme.Text;Font=SteamTheme.UI(10);
            ClientSize=new Size(UiLayout.P(1120),UiLayout.P(1032));StartPosition=FormStartPosition.CenterScreen;
            MaximizeBox=false;KeyPreview=true;
            appIcon=MakeIcon();Icon=appIcon;Build();controller.Changed+=UpdateView;UpdateView();
            tips.BackColor=SteamTheme.Raised;tips.ForeColor=SteamTheme.Text;tips.ShowAlways=true;tips.OwnerDraw=true;
            tips.Popup+=delegate(object sender,PopupEventArgs e) {
                using(var font=SteamTheme.UI(8.5f)) {var size=TextRenderer.MeasureText(tips.GetToolTip(e.AssociatedControl),font);e.ToolTipSize=new Size(size.Width+UiLayout.P(16),size.Height+UiLayout.P(10));}
            };
            tips.Draw+=delegate(object sender,DrawToolTipEventArgs e) {
                e.Graphics.Clear(SteamTheme.Raised);
                using(var pen=new Pen(SteamTheme.Border))e.Graphics.DrawRectangle(pen,0,0,e.Bounds.Width-1,e.Bounds.Height-1);
                using(var font=SteamTheme.UI(8.5f))SteamTheme.DrawLine(e.Graphics,e.ToolTipText,font,SteamTheme.Text,new RectangleF(UiLayout.P(8),0,e.Bounds.Width-UiLayout.P(16),e.Bounds.Height));
            };
            if(!preview) {
                foreach(string command in Program.Commands)events[command]=new EventWaitHandle(false,EventResetMode.AutoReset,Program.EventName(command));
                MakeTray();
                timer=new System.Windows.Forms.Timer {Interval=250};
                timer.Tick+=delegate {try {foreach(var item in events){if(item.Value.WaitOne(0))Execute(item.Key);if(quitting)return;}controller.Tick();}catch(Exception ex){controller.Fail(ex);}};
                Shown+=delegate {InitializeRuntime();};
            }
            KeyDown+=delegate(object sender,KeyEventArgs e) {if(e.KeyCode==Keys.Escape){HideToTray();e.Handled=true;}};
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            try {int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);int corners=2;DwmSetWindowAttribute(Handle,33,ref corners,4);}catch(DllNotFoundException){}
            using(var path=SteamTheme.Round(new RectangleF(0,0,Width,Height),UiLayout.P(16))) {var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var brush=new SolidBrush(Color.FromArgb(12,19,28)))g.FillRectangle(brush,UiLayout.R(0,0,184,1032));
            using(var brush=new SolidBrush(Color.FromArgb(12,19,28)))g.FillRectangle(brush,UiLayout.R(184,0,936,34));
            using(var pen=new Pen(Color.FromArgb(29,43,57)))g.DrawLine(pen,UiLayout.P(184),0,UiLayout.P(184),Height);
            using(var pen=new Pen(SteamTheme.Border))using(var path=SteamTheme.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),UiLayout.P(16)))g.DrawPath(pen,path);
            using(var brush=new LinearGradientBrush(UiLayout.R(24,66,46,46),Color.FromArgb(50,133,183),Color.FromArgb(30,72,105),45))
            using(var path=SteamTheme.Round(UiLayout.R(24,66,46,46),UiLayout.P(13)))g.FillPath(brush,path);
            SteamTheme.Icon(g,"gamepad",UiLayout.R(33,75,28,28),SteamTheme.Text);
            UiLayout.Text(g,"BIG PICTURE",12,SteamTheme.Text,24,128,146,24,true);
            UiLayout.Text(g,"CONTROLE DO SETUP",7.2f,SteamTheme.Muted,24,153,144,17,true);
            using(var path=SteamTheme.Round(UiLayout.R(14,202,156,44),UiLayout.P(10)))using(var brush=new SolidBrush(Color.FromArgb(26,47,65)))g.FillPath(brush,path);
            SteamTheme.Icon(g,"monitor",UiLayout.R(27,213,21,21),SteamTheme.Accent);
            UiLayout.Text(g,"Visão geral",10,SteamTheme.Accent,58,207,106,31,true);
            UiLayout.Text(g,"DO DESKTOP AO SOFÁ",7.5f,SteamTheme.Muted,24,286,140,20,true);
            UiLayout.Text(g,"Seu setup.\nSeu jeito de jogar.",11,SteamTheme.Text,24,319,142,54);
            UiLayout.Text(g,"Telas, som e mouse\nprontos para jogar.\nSeu setup volta ao sair.",8.5f,SteamTheme.Muted,24,387,142,68);
            UiLayout.Text(g,"BIG PICTURE CONTROLE",7.5f,SteamTheme.Muted,211,8,400,20,true);
            UiLayout.Text(g,"CONTROLE LOCAL  /  2.2",7,SteamTheme.Muted,24,995,150,20);
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);if(e.Button==MouseButtons.Left && e.Y<UiLayout.P(38)){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
        }
        SteamText TextAt(Control parent,string text,float x,float y,float w,float h,float size,Color color,bool bold=false) {
            var label=new SteamText{Text=text,Font=SteamTheme.UI(size,bold),ForeColor=color};UiLayout.At(label,x,y,w,h);parent.Controls.Add(label);return label;
        }
        SmoothCard Card(float x,float y,float w,float h) {
            var card=new SmoothCard();UiLayout.At(card,x,y,w,h);card.Radius=16;Controls.Add(card);return card;
        }
        SteamButton ButtonAt(Control parent,string text,float x,float y,float w,float h,string icon,Action action,bool primary=false,bool quiet=false) {
            var b=new SteamButton{Text=text,IconKind=icon,Primary=primary,Quiet=quiet,AccessibleName=String.IsNullOrEmpty(text)?icon:text};
            UiLayout.At(b,x,y,w,h);b.Click+=delegate{Safe(action);};parent.Controls.Add(b);return b;
        }
        SteamSwitch SwitchAt(Control parent,string text,float x,float y,float w,Action action) {
            var sw=new SteamSwitch{Text=text,AccessibleName=text};UiLayout.At(sw,x,y,w,60);sw.Click+=delegate{Safe(action);};parent.Controls.Add(sw);return sw;
        }
        void Build() {
            var minimize=ButtonAt(this,"",1032,4,32,26,"minimize",HideToTray,false,true);tips.SetToolTip(minimize,"Minimizar para a bandeja");
            var close=ButtonAt(this,"",1072,4,32,26,"close",HideToTray,false,true);tips.SetToolTip(close,"Fechar painel · manter automação");
            minimize.AccessibleName="Minimizar painel";close.AccessibleName="Fechar painel";
            TextAt(this,"Seu setup, no controle.",210,52,565,36,23,SteamTheme.Text,true);
            TextAt(this,"Do trabalho ao jogo, sem reorganizar suas telas.",212,88,640,19,9.5f,SteamTheme.Muted);
            ButtonAt(this,"Abrir Big Picture",878,55,214,44,"play",delegate{Process.Start(new ProcessStartInfo("steam://open/bigpicture"){UseShellExecute=true});},true);
            hero=new SetupHero();UiLayout.At(hero,212,122,880,122);Controls.Add(hero);
            TextAt(this,"Monitores",212,258,204,26,15,SteamTheme.Text,true);
            monitorCount=TextAt(this,"",875,260,217,21,9,SteamTheme.Muted);
            TextAt(this,"Clique em uma tela para definir o principal ao voltar ao desktop.",212,286,870,19,9,SteamTheme.Muted);
            var automation=Card(212,498,432,154);
            TextAt(automation,"Automação de telas",20,14,260,25,12,SteamTheme.Text,true);
            TextAt(automation,"Pronta ao iniciar o Windows.  Ctrl + Alt + F10",20,43,392,19,8.5f,SteamTheme.Muted);
            automaticSwitch=SwitchAt(automation,"Acompanhar o Big Picture",18,77,396,delegate{controller.TogglePause();});
            var cursor=Card(660,498,432,154);
            TextAt(cursor,"Mouse sem distrações",20,14,260,25,12,SteamTheme.Text,true);
            cursorState=TextAt(cursor,"",20,43,392,19,8.5f,SteamTheme.Muted);
            cursorSwitch=SwitchAt(cursor,"Ocultar após 5 s sem mover",18,77,396,delegate{controller.ToggleCursor();});
            var sound=Card(212,670,880,118);
            TextAt(sound,"Áudio do Big Picture",20,10,285,26,12,SteamTheme.Text,true);
            audioHint=TextAt(sound,"",330,13,532,20,8.5f,SteamTheme.Muted);
            audioSwitch=SwitchAt(sound,"Trocar saída de áudio",18,45,290,delegate{
                if(Settings.AudioOutput.Length==0 && !Settings.AudioEnabled){audioSelect.PerformClick();return;}
                controller.ToggleAudio();
            });
            audioSelect=new SteamSelect{AccessibleName="Saída de áudio do Big Picture",IconKind="speaker"};
            UiLayout.At(audioSelect,330,49,532,50);
            audioSelect.SelectionChanged+=delegate(int index){
                if(index<0||index>=audioChoices.Count){UpdateView();return;}
                var choice=audioChoices[index];controller.SetAudioOutput(choice.Id,choice.Name);
            };
            sound.Controls.Add(audioSelect);
            tips.SetToolTip(audioSwitch,"Muda o áudio de reprodução só enquanto o Big Picture estiver aberto. Ao sair, restaura a saída anterior.");
            var taskbar=Card(212,806,880,86);
            TextAt(taskbar,"Big Picture sem a barra de tarefas",20,10,490,26,12,SteamTheme.Text,true);
            taskbarHint=TextAt(taskbar,"",20,43,490,22,8.5f,SteamTheme.Muted);
            taskbarSwitch=SwitchAt(taskbar,"Ocultar barra nesta tela",548,13,314,delegate{controller.ToggleTaskbar();});
            tips.SetToolTip(taskbarSwitch,"Oculta só a barra da tela do Big Picture em tela cheia, mesmo sem foco. Restaura ao minimizar, sair ou pausar a automação.");
            var restore=Card(212,910,880,82);
            TextAt(restore,"De volta ao desktop",20,11,275,26,12,SteamTheme.Text,true);
            returnHint=TextAt(restore,"Layout e posições preservados.",20,42,290,19,8.5f,SteamTheme.Muted);
            primarySelect=new SteamSelect{AccessibleName="Monitor principal ao voltar ao desktop"};
            UiLayout.At(primarySelect,330,16,532,50);
            primarySelect.SelectionChanged+=delegate(int index){Safe(delegate{if(index<0||index>controller.KnownScreens.Length){UpdateView();return;}SelectPrimary(index==0?"":controller.KnownScreens[index-1].Device);});};
            restore.Controls.Add(primarySelect);
            var restoreButton=ButtonAt(this,"Restaurar",16,575,152,44,"restore",delegate{controller.RestoreAll();});
            restoreButton.BackColor=Color.FromArgb(12,19,28);
            tips.SetToolTip(restoreButton,"Restaurar monitores e cursor");
            ButtonAt(this,"Liberar telas",16,629,152,44,"monitor",delegate{controller.SetAutomatic(false);},false,true).BackColor=Color.FromArgb(12,19,28);
            ButtonAt(this,"Encerrar app",16,683,152,44,"close",Quit,false,true).BackColor=Color.FromArgb(12,19,28);
            notice=TextAt(this,"Ao fechar o painel, a automação continua na bandeja do Windows.",212,1004,880,18,8,SteamTheme.Muted);
        }
        void SelectPrimary(string device) {
            Settings.SetPrimary(device);UpdateView();
        }
        void UpdateView() {
            hero.Automatic=controller.Automatic;hero.Gaming=controller.Gaming;hero.MonitorName=controller.SelectedName;hero.Error=controller.Error;hero.Invalidate();
            hero.AccessibleName=controller.Error.Length>0?controller.Error:controller.Gaming?"Jogando em "+controller.SelectedName:controller.Automatic?"Automação ativa. Pronto para jogar.":"Automação pausada.";
            automaticSwitch.Checked=controller.Automatic;cursorSwitch.Checked=controller.CursorEnabled;
            audioSwitch.Checked=Settings.AudioEnabled;
            taskbarSwitch.Checked=Settings.TaskbarEnabled;
            taskbarHint.Text=controller.TaskbarError.Length>0?controller.TaskbarError:controller.TaskbarStatus;
            taskbarHint.ForeColor=controller.TaskbarError.Length>0?Color.FromArgb(242,170,126):SteamTheme.Muted;
            tips.SetToolTip(taskbarHint,taskbarHint.Text);
            string audioDescription=controller.AudioError.Length>0?controller.AudioError:controller.AudioStatus;
            audioHint.Text=audioDescription;
            audioHint.ForeColor=controller.AudioError.Length>0?Color.FromArgb(242,170,126):SteamTheme.Muted;
            tips.SetToolTip(audioHint,audioDescription);
            string nextAudioKey=String.Join("|",controller.AudioOutputs.Select(x=>x.Id+":"+x.Name))+"|"+Settings.AudioOutput+"|"+Settings.AudioName;
            if(audioKey!=nextAudioKey) {
                audioKey=nextAudioKey;audioSelect.ClosePopup();audioSelect.Options.Clear();audioChoices.Clear();
                audioChoices.Add(new AudioDevice{Id="",Name=""});audioSelect.Options.Add("Selecionar saída de áudio");
                foreach(var device in controller.AudioOutputs){audioChoices.Add(device);audioSelect.Options.Add(device.Name);}
                if(Settings.AudioOutput.Length>0 && !audioChoices.Any(x=>x.Id==Settings.AudioOutput)) {
                    audioChoices.Add(new AudioDevice{Id=Settings.AudioOutput,Name=Settings.AudioName});
                    audioSelect.Options.Add((Settings.AudioName.Length>0?Settings.AudioName:"Saída salva")+" (indisponível)");
                }
            }
            audioSelect.SelectedIndex=audioChoices.FindIndex(x=>x.Id==Settings.AudioOutput);
            if(trayAutomaticSwitch!=null)trayAutomaticSwitch.Checked=controller.Automatic;
            if(trayCursorSwitch!=null)trayCursorSwitch.Checked=controller.CursorEnabled;
            if(trayAudioSwitch!=null)trayAudioSwitch.Checked=Settings.AudioEnabled;
            if(trayTaskbarSwitch!=null)trayTaskbarSwitch.Checked=Settings.TaskbarEnabled;
            if(tray!=null)tray.Text="Big Picture Controle — "+(controller.Automatic?"automático ligado":"pausado");
            cursorState.Text=controller.CursorHidden?"Cursor oculto. Mova o mouse para mostrar.":"Só no Big Picture. Mova o mouse para mostrar.";
            monitorCount.Text=controller.ActiveDevices.Count+" ativas  /  "+controller.KnownScreens.Length+" no setup";
            string key=String.Join("|",controller.KnownScreens.Select(x=>x.Device+":"+x.Name+":"+x.Width+":"+x.Height+":"+x.Primary));
            if(key!=screenKey) {
                primarySelect.ClosePopup();
                screenKey=key;
                foreach(var tile in tiles)tile.Dispose();tiles.Clear();
                foreach(var old in monitorControls)old.Dispose();monitorControls.Clear();
                primarySelect.Options.Clear();primarySelect.Options.Add("Usar o principal anterior ao Big Picture");
                foreach(var screen in controller.KnownScreens)primarySelect.Options.Add(screen.Device.Replace(@"\\.\","")+"  ·  "+screen.Name);
                int count=controller.KnownScreens.Length;
                int visible=Math.Min(4,count);float width=visible==0?208:(880-(visible-1)*14)/visible;
                for(int i=0;i<count;i++) {
                    var screen=controller.KnownScreens[i];
                    if(i>=4) break;
                    var tile=new MonitorTile{Display=screen,AccessibleName="Monitor "+screen.Device.Replace(@"\\.\","")+": "+screen.Name+". Definir como principal ao voltar."};
                    UiLayout.At(tile,212+i*(width+14),316,width,166);tile.Click+=delegate{Safe(delegate{SelectPrimary(screen.Device);});};
                    tips.SetToolTip(tile,"Usar "+screen.Name+" como principal ao voltar ao desktop");
                    tiles.Add(tile);Controls.Add(tile);
                }
            }
            primarySelect.SelectedIndex=Array.FindIndex(controller.KnownScreens,x=>x.Device==Settings.Primary)+1;
            bool preferredFound=controller.KnownScreens.Any(x=>x.Device==Settings.Primary);
            returnHint.Text=Settings.Primary.Length>0&&!preferredFound?"Tela preferida ausente. Layout anterior preservado.":"Layout e posições preservados.";
            foreach(var tile in tiles) {
                tile.Active=controller.ActiveDevices.Contains(tile.Display.Device);
                tile.Preferred=Settings.Primary.Length>0?tile.Display.Device==Settings.Primary:tile.Display.Primary;
                tile.DefaultPrimary=Settings.Primary.Length==0;
                tile.InGame=controller.Gaming&&controller.SelectedDevice==tile.Display.Device;tile.Invalidate();
            }
        }
        void InitializeRuntime() {
            if(runtimeInitialized||preview)return;runtimeInitialized=true;
            pauseHotkey=RegisterHotKey(Handle,0x4250,0x4000|0x02|0x01,0x79);cursorHotkey=RegisterHotKey(Handle,0x4251,0x4000|0x02|0x01,0x78);
            Files.Log("App iniciado. Atalho monitores="+pauseHotkey+", cursor="+cursorHotkey);
            if(!pauseHotkey||!cursorHotkey)notice.Text="Um atalho está ocupado. Os controles do painel continuam disponíveis.";
            timer.Start();Execute(InitialCommand);
        }
        protected override void SetVisibleCore(bool value) {
            if(!preview&&InitialCommand=="startup"&&!runtimeInitialized) {
                if(!IsHandleCreated)CreateHandle();
                if(!initializationQueued){initializationQueued=true;BeginInvoke(new MethodInvoker(InitializeRuntime));}
                base.SetVisibleCore(false);return;
            }
            base.SetVisibleCore(value);
        }
        void MakeTray() {
            var menu=new ContextMenuStrip{BackColor=SteamTheme.Card,ForeColor=SteamTheme.Text,ShowImageMargin=false,Font=SteamTheme.UI(10)};
            menu.Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors());
            menu.Items.Add("Abrir painel",null,delegate{ShowPanel();});
            trayAutomaticSwitch=new SteamSwitch{Text="Automação",Checked=controller.Automatic,AccessibleName="Automação"};
            UiLayout.At(trayAutomaticSwitch,0,0,310,60);trayAutomaticSwitch.Click+=delegate{Safe(delegate{controller.TogglePause();});};
            menu.Items.Add(new ToolStripControlHost(trayAutomaticSwitch){AutoSize=false,Size=trayAutomaticSwitch.Size,Margin=new Padding(UiLayout.P(4))});
            trayCursorSwitch=new SteamSwitch{Text="Ocultar mouse após 5 s",Checked=controller.CursorEnabled,AccessibleName="Ocultação automática do cursor"};
            UiLayout.At(trayCursorSwitch,0,0,310,60);trayCursorSwitch.Click+=delegate{Safe(delegate{controller.ToggleCursor();});};
            menu.Items.Add(new ToolStripControlHost(trayCursorSwitch){AutoSize=false,Size=trayCursorSwitch.Size,Margin=new Padding(UiLayout.P(4))});
            trayAudioSwitch=new SteamSwitch{Text="Áudio do Big Picture",Checked=Settings.AudioEnabled,AccessibleName="Troca automática de áudio"};
            UiLayout.At(trayAudioSwitch,0,0,310,60);trayAudioSwitch.Click+=delegate{
                if(Settings.AudioOutput.Length==0 && !Settings.AudioEnabled){ShowPanel();audioSelect.PerformClick();return;}
                controller.ToggleAudio();
            };
            menu.Items.Add(new ToolStripControlHost(trayAudioSwitch){AutoSize=false,Size=trayAudioSwitch.Size,Margin=new Padding(UiLayout.P(4))});
            trayTaskbarSwitch=new SteamSwitch{Text="Ocultar barra no Big Picture",Checked=Settings.TaskbarEnabled,AccessibleName="Ocultar barra de tarefas no Big Picture"};
            UiLayout.At(trayTaskbarSwitch,0,0,310,60);trayTaskbarSwitch.Click+=delegate{controller.ToggleTaskbar();};
            menu.Items.Add(new ToolStripControlHost(trayTaskbarSwitch){AutoSize=false,Size=trayTaskbarSwitch.Size,Margin=new Padding(UiLayout.P(4))});
            menu.Items.Add("Restaurar setup",null,delegate{Safe(delegate{controller.RestoreAll();});});
            menu.Items.Add("Encerrar e restaurar",null,delegate{Safe(Quit);});
            tray=new NotifyIcon{Icon=appIcon,Text="Big Picture Controle",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=delegate{ShowPanel();};
        }
        sealed class DarkMenuColors : ProfessionalColorTable {
            public override Color ToolStripDropDownBackground {get{return SteamTheme.Card;}}
            public override Color MenuBorder {get{return SteamTheme.Border;}}
            public override Color MenuItemSelected {get{return SteamTheme.Raised;}}
            public override Color MenuItemBorder {get{return SteamTheme.Border;}}
            public override Color ImageMarginGradientBegin {get{return SteamTheme.Card;}}
            public override Color ImageMarginGradientMiddle {get{return SteamTheme.Card;}}
            public override Color ImageMarginGradientEnd {get{return SteamTheme.Card;}}
        }
        static Icon MakeIcon() {
            using(var bitmap=new Bitmap(64,64))using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);
                using(var path=SteamTheme.Round(new RectangleF(1,1,62,62),16))using(var brush=new LinearGradientBrush(new Rectangle(0,0,64,64),Color.FromArgb(47,137,190),Color.FromArgb(19,45,66),50))g.FillPath(brush,path);
                SteamTheme.Icon(g,"gamepad",new RectangleF(12,10,40,40),SteamTheme.Text);
                IntPtr handle=bitmap.GetHicon();try{return(Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
            }
        }
        void Safe(Action action){try{action();}catch(Exception ex){controller.Fail(ex);ShowPanel();}}
        void ShowPanel(){Show();WindowState=FormWindowState.Normal;Activate();}
        void HideToTray(){if(preview)Close();else Hide();}
        void Execute(string command) {
            switch(command) {
                case "toggle-pause":controller.TogglePause();break;
                case "resume":controller.SetAutomatic(true);break;
                case "startup":controller.SetAutomatic(true);HideToTray();break;
                case "toggle-cursor":controller.ToggleCursor();break;
                case "show-cursor":controller.ShowCursor();break;
                case "restore":controller.RestoreAll();break;
                case "exit":Quit();break;
                default:ShowPanel();break;
            }
        }
        void Quit(){controller.RestoreAll();quitting=true;Close();}
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x0312){if(m.WParam.ToInt64()==0x4250)Safe(delegate{controller.TogglePause();});if(m.WParam.ToInt64()==0x4251)Safe(delegate{controller.ToggleCursor();});}
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if(!preview&&!quitting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideToTray();return;}
            if(!preview&&!quitting)try{controller.RestoreAll();}catch(Exception ex){Files.Log("Encerramento: "+ex.Message);}
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing) {
            if(disposing&&!resourcesDisposed) {
                resourcesDisposed=true;controller.Changed-=UpdateView;tips.Dispose();
                if(timer!=null){timer.Stop();timer.Dispose();}
                if(pauseHotkey)UnregisterHotKey(Handle,0x4250);if(cursorHotkey)UnregisterHotKey(Handle,0x4251);
                if(tray!=null){tray.Visible=false;tray.ContextMenuStrip.Dispose();tray.Dispose();}
                foreach(var signal in events.Values)signal.Dispose();if(appIcon!=null)appIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
