using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BigPictureMonitores {
    static class SteamTheme {
        public static readonly Color Background=Color.FromArgb(16,23,32);
        public static readonly Color Card=Color.FromArgb(24,36,51);
        public static readonly Color Raised=Color.FromArgb(33,49,67);
        public static readonly Color Border=Color.FromArgb(43,61,80);
        public static readonly Color Text=Color.FromArgb(232,239,245);
        public static readonly Color Muted=Color.FromArgb(143,165,186);
        public static readonly Color Accent=Color.FromArgb(102,192,244);
        public static readonly Color Green=Color.FromArgb(119,212,163);
        public static float Scale=1f;
        static readonly string Family=FindFamily();
        static string FindFamily() {
            using(var installed=new InstalledFontCollection()) foreach(var font in installed.Families)
                if(String.Equals(font.Name,"Bahnschrift",StringComparison.OrdinalIgnoreCase)) return font.Name;
            return "Segoe UI";
        }
        public static float Px(float value) { return value*Scale; }
        public static Font UI(float size,bool bold=false) { return new Font(Family,size*96f/72f*Scale,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel); }
        public static Font Title(float size) { return UI(size,true); }
        public static float Unit(Graphics graphics) { return Scale; }
        public static Color Surface(Control control) {
            if(control==null)return Background;
            if(control.BackColor.A==255)return control.BackColor;
            return ParentSurface(control);
        }
        public static Color ParentSurface(Control control) {
            var parent=control==null?null:control.Parent;
            var card=parent as SmoothCard;
            if(card!=null)return card.Fill;
            if(parent!=null && parent.BackColor.A==255)return parent.BackColor;
            return Background;
        }
        public static GraphicsPath Round(RectangleF rect,float radius) {
            var path=new GraphicsPath();
            if(rect.Width<=0 || rect.Height<=0) return path;
            float diameter=Math.Min(Math.Min(rect.Width,rect.Height),Math.Max(1,radius*2));
            path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);
            path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);
            path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);
            path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);
            path.CloseFigure(); return path;
        }
        public static void DrawText(Graphics graphics,string text,Font font,Color color,RectangleF rect,StringAlignment align=StringAlignment.Near) {
            if(rect.Width<=0 || rect.Height<=0 || String.IsNullOrEmpty(text)) return;
            graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            using(var brush=new SolidBrush(color)) using(var format=new StringFormat()) {
                format.Alignment=align; format.LineAlignment=StringAlignment.Center;
                format.Trimming=StringTrimming.EllipsisCharacter;
                format.FormatFlags=StringFormatFlags.LineLimit;
                graphics.DrawString(text,font,brush,rect,format);
            }
        }
        public static void DrawLine(Graphics graphics,string text,Font font,Color color,RectangleF rect,StringAlignment align=StringAlignment.Near) {
            if(rect.Width<=0 || rect.Height<=0 || String.IsNullOrEmpty(text)) return;
            graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            using(var brush=new SolidBrush(color)) using(var format=new StringFormat()) {
                format.Alignment=align;format.LineAlignment=StringAlignment.Center;
                format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags=StringFormatFlags.NoWrap;
                graphics.DrawString(text,font,brush,rect,format);
            }
        }
        public static void Icon(Graphics graphics,string kind,RectangleF bounds,Color color) {
            if(String.IsNullOrEmpty(kind)) return;
            var saved=graphics.Save();
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X,bounds.Y);
            graphics.ScaleTransform(bounds.Width/24f,bounds.Height/24f);
            using(var pen=new Pen(color,1.6f)) using(var fill=new SolidBrush(color)) {
                pen.StartCap=LineCap.Round; pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;
                switch(kind.ToLowerInvariant()) {
                    case "monitor":
                        using(var shape=Round(new RectangleF(2.5f,3.5f,19,13),2)) graphics.DrawPath(pen,shape);
                        graphics.DrawLine(pen,12,17,12,21); graphics.DrawLine(pen,7,21,17,21);
                        break;
                    case "tv":
                        using(var shape=Round(new RectangleF(1.5f,3.5f,21,14),2)) graphics.DrawPath(pen,shape);
                        graphics.DrawLine(pen,7,17.5f,5,21); graphics.DrawLine(pen,17,17.5f,19,21);
                        break;
                    case "mouse":
                        using(var shape=Round(new RectangleF(5.5f,1.8f,13,20.5f),6.5f)) graphics.DrawPath(pen,shape);
                        graphics.DrawLine(pen,12,2,12,10); graphics.DrawLine(pen,6,10,18,10);
                        graphics.DrawLine(pen,12,5,12,7);
                        break;
                    case "speaker":
                        graphics.DrawPolygon(pen,new[]{new PointF(3,9),new PointF(7,9),new PointF(12,5),new PointF(12,19),new PointF(7,15),new PointF(3,15)});
                        graphics.DrawArc(pen,9,6,11,12,-55,110);graphics.DrawArc(pen,9,2,16,20,-50,100);
                        break;
                    case "gamepad":
                        using(var shape=new GraphicsPath()) {
                            shape.AddBezier(5,7,3,7,2.5f,10,1.5f,17);
                            shape.AddBezier(1.5f,17,1,21,4,22,7.5f,16.5f);
                            shape.AddLine(7.5f,16.5f,16.5f,16.5f);
                            shape.AddBezier(16.5f,16.5f,20,22,23,21,22.5f,17);
                            shape.AddBezier(22.5f,17,21.5f,10,21,7,19,7);
                            shape.CloseFigure(); graphics.DrawPath(pen,shape);
                        }
                        graphics.DrawLine(pen,7,9.8f,7,14.2f); graphics.DrawLine(pen,4.8f,12,9.2f,12);
                        graphics.FillEllipse(fill,16,9.5f,2,2); graphics.FillEllipse(fill,18.5f,12.5f,2,2);
                        graphics.DrawLine(pen,9,5,15,5); break;
                    case "restore":
                        graphics.DrawArc(pen,4,4,16,16,210,290);
                        graphics.DrawLine(pen,3.5f,4.5f,3.5f,10); graphics.DrawLine(pen,3.5f,10,9,10);
                        graphics.DrawLine(pen,12,7,12,12); graphics.DrawLine(pen,12,12,15.5f,14);
                        break;
                    case "play":
                        using(var shape=new GraphicsPath()) { shape.AddPolygon(new[]{new PointF(8,4.5f),new PointF(20,12),new PointF(8,19.5f)}); graphics.FillPath(fill,shape); }
                        break;
                    case "chevron": graphics.DrawLines(pen,new[]{new PointF(7,9),new PointF(12,14),new PointF(17,9)}); break;
                    case "close": graphics.DrawLine(pen,7,7,17,17); graphics.DrawLine(pen,17,7,7,17); break;
                    case "minimize": graphics.DrawLine(pen,6,13,18,13); break;
                    case "check": graphics.DrawLines(pen,new[]{new PointF(5,12),new PointF(10,17),new PointF(19,7)}); break;
                }
            }
            graphics.Restore(saved);
        }
    }

    class SmoothCard : Panel {
        Color fill=SteamTheme.Card,edge=SteamTheme.Border;
        int radius=16;
        public Color Fill {get{return fill;}set{fill=value;Invalidate();}}
        public Color Edge {get{return edge;}set{edge=value;Invalidate();}}
        public int Radius {get{return radius;}set{radius=value;Invalidate();}}
        public SmoothCard() {
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
            BackColor=Color.Transparent;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.ParentSurface(this)); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics; g.Clear(SteamTheme.ParentSurface(this)); g.SmoothingMode=SmoothingMode.AntiAlias; float u=SteamTheme.Unit(g);
            using(var path=SteamTheme.Round(new RectangleF(0.5f*u,0.5f*u,Width-u,Height-u),radius*u)) {
                using(var brush=new SolidBrush(fill)) g.FillPath(brush,path);
                if(edge.A>0) using(var pen=new Pen(edge,u)) g.DrawPath(pen,path);
            }
            base.OnPaint(e);
        }
    }

    sealed class SteamButton : Button {
        bool hover,pressed;
        public string IconKind {get;set;}
        public bool Primary {get;set;}
        public bool Quiet {get;set;}
        public SteamButton() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
            FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0; UseVisualStyleBackColor=false;
            BackColor=Color.Transparent; ForeColor=SteamTheme.Text; Font=SteamTheme.UI(10,true);
            Cursor=Cursors.Hand; Height=44; TabStop=true;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this)); }
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;pressed=false;Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);pressed=true;Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);pressed=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override bool IsInputKey(Keys keyData) {
            Keys key=keyData&Keys.KeyCode;
            return key==Keys.Up || key==Keys.Down || base.IsInputKey(keyData);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics; g.Clear(SteamTheme.Surface(this)); g.SmoothingMode=SmoothingMode.AntiAlias; float u=SteamTheme.Unit(g);
            Color color=!Enabled?Color.FromArgb(35,45,56):Primary?(hover?Color.FromArgb(65,163,222):Color.FromArgb(39,134,193)):(hover?Color.FromArgb(48,68,89):SteamTheme.Raised);
            if(pressed) color=ControlPaint.Dark(color,0.12f);
            var rect=new RectangleF(0.5f*u,0.5f*u,Width-u,Height-u);
            using(var path=SteamTheme.Round(rect,11*u)) {
                if(!Quiet || hover || pressed) using(var brush=new SolidBrush(color)) g.FillPath(brush,path);
                if(!Primary && !Quiet) using(var pen=new Pen(hover?Color.FromArgb(70,101,128):SteamTheme.Border,u)) g.DrawPath(pen,path);
                if(Focused && ShowFocusCues) using(var pen=new Pen(SteamTheme.Accent,1.5f*u)) g.DrawPath(pen,path);
            }
            var foreground=Enabled?SteamTheme.Text:SteamTheme.Muted;
            bool left=TextAlign==ContentAlignment.MiddleLeft || TextAlign==ContentAlignment.TopLeft || TextAlign==ContentAlignment.BottomLeft;
            if(String.IsNullOrEmpty(Text)) SteamTheme.Icon(g,IconKind,new RectangleF((Width-22*u)/2f,(Height-22*u)/2f,22*u,22*u),foreground);
            else if(String.IsNullOrEmpty(IconKind)) SteamTheme.DrawLine(g,Text,Font,foreground,new RectangleF(16*u,0,Width-32*u,Height),left?StringAlignment.Near:StringAlignment.Center);
            else {
                SizeF measured=g.MeasureString(Text,Font); float start=left?16*u:Math.Max(12*u,(Width-measured.Width-30*u)/2f);
                SteamTheme.Icon(g,IconKind,new RectangleF(start,(Height-20*u)/2f,20*u,20*u),foreground);
                SteamTheme.DrawLine(g,Text,Font,foreground,new RectangleF(start+30*u,0,Width-start-42*u,Height));
            }
        }
    }

    sealed class SteamSwitch : CheckBox {
        bool hover;
        float position;
        readonly System.Windows.Forms.Timer animation;
        public SteamSwitch() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
            AutoSize=false; AutoCheck=false; Height=60; Width=280; TabStop=true;
            Cursor=Cursors.Hand; Font=SteamTheme.UI(10,true); BackColor=Color.Transparent; ForeColor=SteamTheme.Text;
            animation=new System.Windows.Forms.Timer{Interval=15};
            animation.Tick+=delegate {float target=Checked?1:0;position+=(target-position)*0.38f;if(Math.Abs(target-position)<0.012f){position=target;animation.Stop();}Invalidate();};
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this)); }
        protected override void OnCheckedChanged(EventArgs e) {
            base.OnCheckedChanged(e); if(animation==null)return;
            if(IsHandleCreated && Visible) animation.Start(); else position=Checked?1:0;
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override bool IsInputKey(Keys keyData) {
            Keys key=keyData&Keys.KeyCode;
            return key==Keys.Left || key==Keys.Right || key==Keys.Enter || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Left || e.KeyCode==Keys.Right || e.KeyCode==Keys.Enter) {
                if(e.KeyCode==Keys.Enter || Checked!=(e.KeyCode==Keys.Right)) OnClick(EventArgs.Empty);
                e.Handled=true;e.SuppressKeyPress=true;return;
            }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(SteamTheme.Surface(this));g.SmoothingMode=SmoothingMode.AntiAlias;float u=SteamTheme.Unit(g);
            using(var path=SteamTheme.Round(new RectangleF(0.5f*u,0.5f*u,Width-u,Height-u),12*u)) {
                using(var brush=new SolidBrush(hover?Color.FromArgb(37,55,74):SteamTheme.Raised))g.FillPath(brush,path);
                using(var pen=new Pen(Focused && ShowFocusCues?SteamTheme.Accent:SteamTheme.Border,u))g.DrawPath(pen,path);
            }
            Color on=SteamTheme.Accent;
            var track=new RectangleF(Width-73*u,(Height-28*u)/2f,56*u,28*u);
            using(var path=SteamTheme.Round(track,14*u))using(var brush=new SolidBrush(!Enabled?Color.FromArgb(60,70,80):Checked?Color.FromArgb(51,149,208):Color.FromArgb(67,83,102)))g.FillPath(brush,path);
            using(var brush=new SolidBrush(Enabled?Color.FromArgb(244,249,252):Color.Silver))g.FillEllipse(brush,track.Left+(4+28*position)*u,track.Top+4*u,20*u,20*u);
            SteamTheme.DrawLine(g,Text,Font,Enabled?SteamTheme.Text:SteamTheme.Muted,new RectangleF(16*u,9*u,Width-104*u,23*u));
            using(var small=SteamTheme.UI(8.5f))SteamTheme.DrawText(g,Checked?"Ativado":"Desativado",small,Checked?on:SteamTheme.Muted,new RectangleF(16*u,33*u,Width-104*u,17*u));
        }
        protected override void Dispose(bool disposing){if(disposing && animation!=null)animation.Dispose();base.Dispose(disposing);}
    }

    sealed class SteamSelect : Button {
        public string IconKind {get;set;}
        public readonly List<string> Options=new List<string>();
        int selected=-1;
        bool hover;
        ToolStripDropDown popup;
        readonly List<ToolStripDropDown> retiredPopups=new List<ToolStripDropDown>();
        public event Action<int> SelectionChanged;
        public int SelectedIndex {get{return selected;}set{selected=value;Invalidate();}}
        public SteamSelect() {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
            AutoSize=false;Height=50;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;
            BackColor=Color.Transparent;ForeColor=SteamTheme.Text;Font=SteamTheme.UI(10);Cursor=Cursors.Hand;
            AccessibleRole=AccessibleRole.ComboBox;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(SteamTheme.Surface(this)); }
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);hover=true;Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);hover=false;Invalidate();}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override bool IsInputKey(Keys keyData) {
            Keys key=keyData&Keys.KeyCode;
            return key==Keys.Up || key==Keys.Down || key==Keys.Enter || key==Keys.Escape || base.IsInputKey(keyData);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics;g.Clear(SteamTheme.Surface(this));g.SmoothingMode=SmoothingMode.AntiAlias;float u=SteamTheme.Unit(g);
            using(var path=SteamTheme.Round(new RectangleF(0.5f*u,0.5f*u,Width-u,Height-u),11*u)) {
                using(var brush=new SolidBrush(hover?Color.FromArgb(40,59,79):SteamTheme.Raised))g.FillPath(brush,path);
                using(var pen=new Pen((Focused && ShowFocusCues)||popup!=null?SteamTheme.Accent:SteamTheme.Border,u))g.DrawPath(pen,path);
            }
            SteamTheme.Icon(g,String.IsNullOrEmpty(IconKind)?"monitor":IconKind,new RectangleF(16*u,(Height-21*u)/2f,21*u,21*u),SteamTheme.Accent);
            SteamTheme.DrawLine(g,selected>=0 && selected<Options.Count?Options[selected]:"Selecionar monitor",Font,SteamTheme.Text,new RectangleF(49*u,0,Width-91*u,Height));
            SteamTheme.Icon(g,"chevron",new RectangleF(Width-34*u,(Height-20*u)/2f,20*u,20*u),SteamTheme.Muted);
        }
        protected override void OnClick(EventArgs e) {base.OnClick(e);OpenPopup();}
        protected override void OnKeyDown(KeyEventArgs e) {
            if(e.KeyCode==Keys.Down || e.KeyCode==Keys.Up || e.KeyCode==Keys.Enter) {
                OpenPopup();e.Handled=true;e.SuppressKeyPress=true;return;
            }
            if(e.KeyCode==Keys.Escape && popup!=null){ClosePopup();Focus();e.Handled=true;e.SuppressKeyPress=true;return;}
            base.OnKeyDown(e);
        }
        void Choose(int index) {
            selected=index;ClosePopup();Invalidate();Focus();
            if(SelectionChanged!=null)SelectionChanged(index);
        }
        public void ClosePopup() {
            var open=popup;
            if(open==null)return;
            popup=null;
            if(!open.IsDisposed) {
                open.Close(ToolStripDropDownCloseReason.CloseCalled);
                RetirePopup(open);
            }
            if(!IsDisposed && !Disposing)Invalidate();
        }
        void RetirePopup(ToolStripDropDown closed) {
            if(closed.IsDisposed)return;
            if(!retiredPopups.Contains(closed))retiredPopups.Add(closed);
            // ToolStrip still uses the dropdown after raising Closed, especially
            // while processing Escape. Release it only after that input dispatch.
            if(!IsHandleCreated || IsDisposed || Disposing)return;
            try {
                BeginInvoke(new MethodInvoker(delegate {
                    if(!closed.IsDisposed && !closed.Visible)closed.Dispose();
                    if(closed.IsDisposed)retiredPopups.Remove(closed);
                }));
            } catch(InvalidOperationException) {
                // If the owner is losing its handle, Dispose releases the list.
            }
        }
        void OpenPopup() {
            if(popup!=null){ClosePopup();return;}
            if(Options.Count==0)return;
            var list=new List<SteamButton>();
            float u;using(var g=CreateGraphics())u=SteamTheme.Unit(g);
            int pad=(int)(8*u),gap=(int)(3*u),itemHeight=(int)(43*u);
            var area=Screen.FromControl(this).WorkingArea;
            int popupWidth=Math.Min(Math.Max(Width,(int)(260*u)),area.Width-16);
            int availableHeight=Math.Max((int)(120*u),area.Height-(int)(40*u));
            int popupHeight=Math.Min(Options.Count*itemHeight+2*pad,Math.Min((int)(380*u),availableHeight));
            var container=new SmoothCard{Size=new Size(popupWidth,popupHeight),Fill=SteamTheme.Card,Edge=SteamTheme.Border,Radius=13,Padding=new Padding(pad)};
            var scroll=new FlowLayoutPanel{Location=new Point(pad,pad),Size=new Size(popupWidth-2*pad,popupHeight-2*pad),BackColor=SteamTheme.Card,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
            container.Controls.Add(scroll);
            for(int i=0;i<Options.Count;i++) {
                int choice=i;
                var option=new SteamButton{Text=Options[i],Height=itemHeight-gap,Width=popupWidth-(int)(34*u),Margin=new Padding(0,0,0,gap),Quiet=true,IconKind=i==selected?"check":null,AccessibleName=Options[i],TextAlign=ContentAlignment.MiddleLeft,Font=SteamTheme.UI(10)};
                option.Click+=delegate{Choose(choice);};
                option.KeyDown+=delegate(object sender,KeyEventArgs e){
                    if(e.KeyCode==Keys.Escape){ClosePopup();Focus();e.Handled=true;e.SuppressKeyPress=true;}
                    else if(e.KeyCode==Keys.Down || e.KeyCode==Keys.Up){int next=(choice+(e.KeyCode==Keys.Down?1:-1)+list.Count)%list.Count;list[next].Focus();scroll.ScrollControlIntoView(list[next]);e.Handled=true;e.SuppressKeyPress=true;}
                };
                list.Add(option);scroll.Controls.Add(option);
            }
            var dropdown=new ToolStripDropDown{AutoSize=false,Size=container.Size,Padding=Padding.Empty,Margin=Padding.Empty,BackColor=SteamTheme.Card,DropShadowEnabled=false};
            var host=new ToolStripControlHost(container){AutoSize=false,Size=container.Size,Padding=Padding.Empty,Margin=Padding.Empty};
            dropdown.Items.Add(host);popup=dropdown;
            using(var shape=SteamTheme.Round(new RectangleF(0,0,dropdown.Width,dropdown.Height),13*u))dropdown.Region=new Region(shape);
            dropdown.Closed+=delegate {
                if(Object.ReferenceEquals(popup,dropdown))popup=null;
                if(!IsDisposed && !Disposing)Invalidate();
                RetirePopup(dropdown);
            };
            dropdown.KeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.KeyCode==Keys.Escape){ClosePopup();Focus();e.Handled=true;e.SuppressKeyPress=true;}
            };
            Point start=PointToScreen(new Point(0,Height+6));
            if(start.Y+dropdown.Height>area.Bottom)start.Y=PointToScreen(Point.Empty).Y-dropdown.Height-6;
            start.X=Math.Max(area.Left,Math.Min(start.X,area.Right-dropdown.Width));
            start.Y=Math.Max(area.Top,start.Y);
            dropdown.Show(start);Invalidate();
            int first=selected>=0 && selected<list.Count?selected:0;
            list[first].Focus();scroll.ScrollControlIntoView(list[first]);
        }
        protected override void Dispose(bool disposing) {
            if(disposing) {
                ClosePopup();
                foreach(var closed in retiredPopups.ToArray())if(!closed.IsDisposed)closed.Dispose();
                retiredPopups.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
