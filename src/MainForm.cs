using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static GhostMacro.Native;

namespace GhostMacro
{
    internal sealed class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool IsWindowEnabled(IntPtr hwnd);

        readonly AppSettings cfg;
        List<Step> steps;
        string curFile;
        bool dirty;

        readonly InputHooks hooks = new InputHooks();
        readonly Recorder recorder = new Recorder();
        readonly Player player = new Player();
        readonly Timer timer = new Timer { Interval = 50 };
        readonly uint myPid = (uint)Process.GetCurrentProcess().Id;

        // kısayollar
        struct Hk { public int Mods, Vk; }
        enum HkKind { Record, Play, Stop, Capture }
        readonly Dictionary<HkKind, Hk> hotkeys = new Dictionary<HkKind, Hk>();
        readonly HashSet<int> swallowed = new HashSet<int>();

        // oynatma durumu
        bool stopOnMove;
        int playingIndex = -1;
        int lastShownIndex = -1;

        // kontroller
        DataGridView grid;
        Button btnRecord, btnPlay, btnPlayFrom, btnStop;
        RadioButton rbStepMode, rbFullMode, rbRealDelay, rbFixedDelay;
        CheckBox chkRecordKeys, chkInfinite, chkStopOnMove, chkMinimize;
        NumericUpDown numFixed, numRepeat, numGap, numSpeed, numJitter, numSmooth;
        FlowLayoutPanel sidePanel, quickAdd;
        MenuStrip menu;
        ToolStripMenuItem miFile, miSettings, miTopMost, miLangTr, miLangEn;
        ToolStripStatusLabel stStatus, stCount, stPos;
        ContextMenuStrip addMenu, gridMenu;

        public MainForm()
        {
            cfg = Store.LoadSettings();
            L.En = cfg.Lang == "en";
            steps = cfg.Steps ?? new List<Step>();
            curFile = cfg.LastFile;
            dirty = cfg.Dirty;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(1060, 680);
            MinimumSize = new Size(820, 520);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildMenu();
            BuildGrid();
            var top = BuildTop();
            BuildSide();
            var status = BuildStatus();

            Controls.Add(grid);
            Controls.Add(sidePanel);
            Controls.Add(top);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            LoadOptions();
            ParseHotkeys();
            UpdateHotkeyTexts();
            ResumeLayout(false);
            PerformLayout();

            recorder.IsOwnWindow = IsOwnWindow;
            recorder.IsHotkey = IsHotkeyKey;
            hooks.Key += OnHookKey;
            hooks.Mouse += OnHookMouse;
            player.Finished += r =>
            {
                try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => OnPlayFinished(r))); } catch { }
            };
            timer.Tick += OnTick;

            Load += (s, e) =>
            {
                hooks.Install();
                timer.Start();
                grid.RowCount = steps.Count;
                UpdateUi();
                UpdateTitle();
                SetStatus(L.T("Hazır.", "Ready."));
            };
            FormClosing += OnClosing;
        }

        // ───────────────────────── arayüz ─────────────────────────

        void BuildMenu()
        {
            menu = new MenuStrip();
            miFile = new ToolStripMenuItem(L.T("&Dosya", "&File"));
            miFile.DropDownItems.Add(Mi(L.T("Yeni", "New"), Keys.Control | Keys.N, (s, e) => NewMacro()));
            miFile.DropDownItems.Add(Mi(L.T("Aç...", "Open..."), Keys.Control | Keys.O, (s, e) => OpenMacro()));
            miFile.DropDownItems.Add(Mi(L.T("Kaydet", "Save"), Keys.Control | Keys.S, (s, e) => SaveMacro(false)));
            miFile.DropDownItems.Add(Mi(L.T("Farklı kaydet...", "Save as..."), Keys.Control | Keys.Shift | Keys.S, (s, e) => SaveMacro(true)));
            miFile.DropDownItems.Add(new ToolStripSeparator());
            miFile.DropDownItems.Add(Mi(L.T("Çıkış", "Exit"), Keys.None, (s, e) => Close()));

            miSettings = new ToolStripMenuItem(L.T("&Ayarlar", "&Settings"));
            miSettings.DropDownItems.Add(Mi(L.T("Kısayol tuşları...", "Hotkeys..."), Keys.None, (s, e) => EditHotkeys()));
            miSettings.DropDownItems.Add(Mi(L.T("Varsayılan bekleme süresi...", "Default wait time..."), Keys.None, (s, e) =>
            {
                var v = NumberDialog.AskSeconds(this, L.T("Varsayılan bekleme", "Default wait"),
                    L.T("Yeni eklenen adımların bekleme süresi:", "Wait time for newly added steps:"), cfg.DefaultDelay);
                if (v.HasValue) cfg.DefaultDelay = v.Value;
            }));
            miTopMost = Mi(L.T("Her zaman üstte", "Always on top"), Keys.None, (s, e) =>
            {
                cfg.TopMost = !cfg.TopMost;
                miTopMost.Checked = TopMost = cfg.TopMost;
            });
            miTopMost.Checked = TopMost = cfg.TopMost;
            miSettings.DropDownItems.Add(miTopMost);
            var miLang = new ToolStripMenuItem(L.T("Dil / Language", "Language / Dil"));
            miLangTr = Mi("Türkçe", Keys.None, (s, e) => ChangeLang("tr"));
            miLangEn = Mi("English", Keys.None, (s, e) => ChangeLang("en"));
            miLangTr.Checked = !L.En;
            miLangEn.Checked = L.En;
            miLang.DropDownItems.Add(miLangTr);
            miLang.DropDownItems.Add(miLangEn);
            miSettings.DropDownItems.Add(miLang);

            var miHelp = new ToolStripMenuItem(L.T("&Yardım", "&Help"));
            miHelp.DropDownItems.Add(Mi(L.T("Nasıl kullanılır", "How to use"), Keys.F1, (s, e) => ShowHelp()));
            miHelp.DropDownItems.Add(new ToolStripSeparator());
            miHelp.DropDownItems.Add(Mi(L.T("GitHub sayfası", "GitHub page"), Keys.None, (s, e) => AboutDialog.OpenUrl(AboutDialog.RepoUrl)));
            miHelp.DropDownItems.Add(Mi("ilkayderici.com.tr", Keys.None, (s, e) => AboutDialog.OpenUrl(AboutDialog.SiteUrl)));
            miHelp.DropDownItems.Add(Mi(L.T("Hakkında", "About"), Keys.None, (s, e) => AboutDialog.Show(this)));

            menu.Items.AddRange(new ToolStripItem[] { miFile, miSettings, miHelp });
        }

        static ToolStripMenuItem Mi(string text, Keys keys, EventHandler click)
        {
            var m = new ToolStripMenuItem(text, null, click);
            if (keys != Keys.None) m.ShortcutKeys = keys;
            return m;
        }

        static FlowLayoutPanel Row(params Control[] cs)
        {
            var f = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 1, 0, 1) };
            f.Controls.AddRange(cs);
            return f;
        }

        static Label Lb(string t) => new Label { Text = t, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };

        static NumericUpDown Num(int min, int max, int inc, int width = 75) =>
            new NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Width = width, ThousandsSeparator = true, Margin = new Padding(3, 3, 3, 3) };

        static GroupBox Group(string title, Control content)
        {
            var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 4, 8, 6), Margin = new Padding(6, 4, 6, 4) };
            content.Location = new Point(8, 20);
            g.Controls.Add(content);
            return g;
        }

        static FlowLayoutPanel Stack(params Control[] cs)
        {
            var f = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = Padding.Empty };
            f.Controls.AddRange(cs);
            return f;
        }

        static NumericUpDown SecNum(int width = 80) =>
            new NumericUpDown { Minimum = 0, Maximum = 86400, DecimalPlaces = 2, Increment = 0.5m, Width = width, Margin = new Padding(3, 3, 3, 3) };

        static void SetSec(NumericUpDown n, int ms) => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, ms / 1000m));

        static int Ms(NumericUpDown n) => (int)Math.Round(n.Value * 1000m);

        Control BuildTop()
        {
            // ── Kayıt
            btnRecord = new Button { Width = 170, Height = 40, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(3, 3, 3, 3), UseVisualStyleBackColor = true };
            btnRecord.Click += (s, e) => ToggleRecord();

            rbStepMode = new RadioButton { Text = L.T("Adım adım (tıklamalar + tuşlar)", "Step by step (clicks + keys)"), AutoSize = true };
            rbFullMode = new RadioButton { Text = L.T("Tam kayıt (gerçek zamanlı, her hareket)", "Full recording (real time, every movement)"), AutoSize = true };
            var modePanel = Stack(rbStepMode, rbFullMode);
            chkRecordKeys = new CheckBox { Text = L.T("Klavyeyi de kaydet", "Record keyboard too"), AutoSize = true, Margin = new Padding(3, 6, 3, 0) };

            rbRealDelay = new RadioButton { Text = L.T("Gerçek bekleme süreleri", "Real wait times"), AutoSize = true };
            rbFixedDelay = new RadioButton { Text = L.T("Her adım arası sabit:", "Fixed between steps:"), AutoSize = true, Margin = new Padding(3, 5, 0, 0) };
            numFixed = SecNum();
            var delayPanel = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
            var delayStack = Stack(rbRealDelay, Row(rbFixedDelay, numFixed, Lb(L.T("sn", "s"))));
            delayPanel.Controls.Add(delayStack);
            rbFullMode.CheckedChanged += (s, e) => UpdateUi();
            rbFixedDelay.CheckedChanged += (s, e) => UpdateUi();

            var recGroup = Group(L.T("Kayıt", "Recording"), Stack(modePanel, chkRecordKeys, new Label { Height = 4, AutoSize = false, Width = 10 }, delayPanel));

            // ── Oynatma
            numRepeat = Num(1, 1000000, 1, 70);
            chkInfinite = new CheckBox { Text = L.T("Sonsuz", "Forever"), AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
            chkInfinite.CheckedChanged += (s, e) => numRepeat.Enabled = !chkInfinite.Checked;
            numGap = SecNum();
            numSpeed = Num(10, 1000, 10, 60);
            numJitter = Num(0, 50, 5, 50);
            numSmooth = Num(0, 5000, 50, 60);
            chkStopOnMove = new CheckBox { Text = L.T("Fareyi oynatırsam dursun", "Stop if I move the mouse"), AutoSize = true };
            chkMinimize = new CheckBox { Text = L.T("Oynatırken pencereyi küçült", "Minimize window while playing"), AutoSize = true };

            var opts = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
            opts.Controls.Add(Lb(L.T("Hız:", "Speed:")));
            opts.Controls.Add(Row(numSpeed, Lb("%")));
            opts.Controls.Add(Lb(L.T("Turlar arası:", "Between loops:")));
            opts.Controls.Add(Row(numGap, Lb(L.T("sn", "s"))));
            opts.Controls.Add(Lb(L.T("Rastgelelik:", "Randomness:")));
            opts.Controls.Add(Row(Lb("±"), numJitter, Lb("%")));
            opts.Controls.Add(Lb(L.T("Yumuşak hareket:", "Smooth movement:")));
            opts.Controls.Add(Row(numSmooth, Lb("ms")));
            var checks = Stack(chkStopOnMove, chkMinimize);
            opts.Controls.Add(checks);
            opts.SetColumnSpan(checks, 4);

            var tip = new ToolTip();
            tip.SetToolTip(numSpeed, L.T("100 = normal, 200 = iki kat hızlı, 50 = yarı hız", "100 = normal, 200 = twice as fast, 50 = half speed"));
            tip.SetToolTip(numJitter, L.T("Bekleme sürelerini rastgele bu kadar değiştirir (insan gibi görünsün)", "Randomly varies wait times by this much (looks more human)"));
            tip.SetToolTip(numSmooth, L.T("0 = fare anında ışınlanır; >0 = hedefe bu sürede kayarak gider", "0 = cursor jumps instantly; >0 = glides to the target in this time"));

            btnPlay = new Button { Width = 125, Height = 40, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(3, 3, 3, 3), UseVisualStyleBackColor = true };
            btnPlay.Click += (s, e) => StartPlay(false);
            btnPlayFrom = new Button { Text = L.T("▶ Seçili adımdan oynat", "▶ Play from selected"), AutoSize = true, Height = 30, UseVisualStyleBackColor = true };
            btnPlayFrom.Click += (s, e) => StartPlay(true);
            btnStop = new Button { Width = 125, Height = 40, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), Margin = new Padding(3, 3, 3, 3), UseVisualStyleBackColor = true };
            btnStop.Click += (s, e) => StopAll();
            var playGroup = Group(L.T("Oynatma", "Playback"), Stack(opts, btnPlayFrom));

            // ── Ana satır: Kaydet · Oynat · Durdur · Tekrar · + Tıklama / + Tuş / + Bekle · Gelişmiş
            Button Quick(string t, EventHandler h)
            {
                var b = new Button { Text = t, AutoSize = true, Height = 40, Margin = new Padding(3, 3, 3, 3), UseVisualStyleBackColor = true };
                b.Click += h;
                return b;
            }
            quickAdd = Row(
                Quick(L.T("＋ Tıklama", "＋ Click"), (s, e) => AddPickedClick()),
                Quick(L.T("＋ Tuş", "＋ Key"), (s, e) => AddWithEditor(StepAction.KeyCombo)),
                Quick(L.T("＋ Bekle", "＋ Wait"), (s, e) => AddWithEditor(StepAction.Wait)));
            quickAdd.Margin = new Padding(14, 0, 0, 0);

            var advPanel = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty, Visible = false };
            advPanel.Controls.Add(recGroup);
            advPanel.Controls.Add(playGroup);

            var btnAdv = new Button { AutoSize = true, Height = 40, Margin = new Padding(8, 3, 3, 3), UseVisualStyleBackColor = true };
            void AdvText() => btnAdv.Text = advPanel.Visible ? L.T("Gelişmiş ▴", "Advanced ▴") : L.T("Gelişmiş ▾", "Advanced ▾");
            AdvText();
            btnAdv.Click += (s, e) => { advPanel.Visible = !advPanel.Visible; AdvText(); };

            var tipQ = new ToolTip();
            tipQ.SetToolTip(quickAdd.Controls[0], L.T("Ekranda bir noktaya tıklayarak tıklama adımı ekler", "Adds a click step by picking a point on screen"));
            tipQ.SetToolTip(quickAdd.Controls[1], L.T("Listeden tuş veya kombinasyon seçip adım ekler (Ctrl+C, Enter...)", "Adds a key or combination picked from a list (Ctrl+C, Enter...)"));
            tipQ.SetToolTip(quickAdd.Controls[2], L.T("Sadece bekleme adımı ekler", "Adds a wait-only step"));

            var repeatRow = Row(Lb(L.T("Tekrar:", "Repeat:")), numRepeat, chkInfinite);
            repeatRow.Margin = new Padding(14, 6, 0, 0);
            var mainRow = Row(btnRecord, btnPlay, btnStop, repeatRow, quickAdd, btnAdv);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Padding = new Padding(6, 4, 4, 2) };
            top.Controls.Add(mainRow);
            top.Controls.Add(advPanel);
            return top;
        }

        void BuildGrid()
        {
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                Margin = new Padding(6),
            };
            grid.RowTemplate.Height = 24;
            grid.Columns.Add(Col("#", 48, true));
            grid.Columns.Add(Col(L.T("İşlem", "Action"), 190, true));
            var det = Col(L.T("Ayrıntı", "Details"), 260, true);
            det.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(det);
            var d = Col(L.T("Bekle (sn)", "Wait (s)"), 100, false);
            d.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            d.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
            grid.Columns.Add(d);
            var n = Col(L.T("Not", "Note"), 170, false);
            grid.Columns.Add(n);
            grid.Columns[0].DefaultCellStyle.ForeColor = SystemColors.GrayText;

            grid.CellValueNeeded += Grid_CellValueNeeded;
            grid.CellValuePushed += Grid_CellValuePushed;
            grid.CellFormatting += Grid_CellFormatting;
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && (e.ColumnIndex < 3)) EditSelected(); };
            grid.KeyDown += Grid_KeyDown;
            grid.DataError += (s, e) => { e.ThrowException = false; };
            grid.CellValidating += (s, e) =>
            {
                if (e.ColumnIndex == 3 && !TryParseSec(Convert.ToString(e.FormattedValue), out _))
                {
                    MessageBox.Show(this, L.T("Saniye olarak bir sayı girin (örn. 1,5).", "Enter a number of seconds (e.g. 1.5)."),
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                }
            };
            grid.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var h = grid.HitTest(e.X, e.Y);
                if (h.RowIndex >= 0 && !grid.Rows[h.RowIndex].Selected)
                {
                    grid.ClearSelection();
                    grid.CurrentCell = grid[0, h.RowIndex];
                    grid.Rows[h.RowIndex].Selected = true;
                }
            };
        }

        static DataGridViewTextBoxColumn Col(string header, int width, bool ro) => new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            Width = width,
            ReadOnly = ro,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };

        void BuildSide()
        {
            addMenu = new ContextMenuStrip();
            addMenu.Items.Add(L.T("Ekrandan seçerek tıklama...", "Click — pick on screen..."), null, (s, e) => AddPickedClick());
            addMenu.Items.Add(L.T("Tuş / kombinasyon...", "Key / combination..."), null, (s, e) => AddWithEditor(StepAction.KeyCombo));
            addMenu.Items.Add(L.T("Metin yaz...", "Type text..."), null, (s, e) => AddWithEditor(StepAction.TypeText));
            addMenu.Items.Add(L.T("Sadece bekle...", "Wait only..."), null, (s, e) => AddWithEditor(StepAction.Wait));
            addMenu.Items.Add(L.T("Tekerlek...", "Mouse wheel..."), null, (s, e) => AddWithEditor(StepAction.Wheel));
            addMenu.Items.Add(L.T("Sürükle-bırak...", "Drag and drop..."), null, (s, e) => AddWithEditor(StepAction.Drag));
            addMenu.Items.Add(L.T("Diğer...", "Other..."), null, (s, e) => AddWithEditor(StepAction.Click));

            Button B(string t, EventHandler h)
            {
                var b = new Button { Text = t, Width = 160, Height = 30, Margin = new Padding(3, 2, 3, 2), TextAlign = ContentAlignment.MiddleLeft, UseVisualStyleBackColor = true };
                b.Click += h;
                return b;
            }

            var btnAdd = B(L.T("＋ Adım ekle  ▾", "＋ Add step  ▾"), null);
            btnAdd.Font = new Font(Font, FontStyle.Bold);
            btnAdd.Click += (s, e) => addMenu.Show(btnAdd, new Point(0, btnAdd.Height));

            sidePanel = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(2, 6, 6, 4) };
            sidePanel.Controls.AddRange(new Control[]
            {
                btnAdd,
                B(L.T("Düzenle...  (Enter)", "Edit...  (Enter)"), (s, e) => EditSelected()),
                B(L.T("Sil  (Del)", "Delete  (Del)"), (s, e) => DeleteSelected()),
                B(L.T("Yukarı  (Alt+↑)", "Up  (Alt+↑)"), (s, e) => MoveSelected(-1)),
                B(L.T("Aşağı  (Alt+↓)", "Down  (Alt+↓)"), (s, e) => MoveSelected(1)),
                B(L.T("Çoğalt  (Ctrl+D)", "Duplicate  (Ctrl+D)"), (s, e) => DuplicateSelected()),
                new Label { Height = 8, AutoSize = false },
                B(L.T("Bekleme sürelerini ayarla...", "Set wait times..."), (s, e) => SetDelays()),
                B(L.T("Tümünü temizle", "Clear all"), (s, e) => ClearAll()),
                new Label
                {
                    AutoSize = false, Width = 160, Height = 120, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 10, 3, 0),
                    Text = L.T("İpucu: \"Bekle (sn)\" ve \"Not\" hücrelerine tıklayıp doğrudan yazabilirsiniz.",
                               "Tip: you can click the \"Wait (s)\" and \"Note\" cells and type directly."),
                },
            });

            gridMenu = new ContextMenuStrip();
            var gmAdd = new ToolStripMenuItem(L.T("Adım ekle", "Add step"));
            foreach (ToolStripItem it in addMenu.Items)
            {
                var src = it;
                gmAdd.DropDownItems.Add(src.Text, null, (s, e) => src.PerformClick());
            }
            gridMenu.Items.Add(gmAdd);
            gridMenu.Items.Add(L.T("Düzenle...", "Edit..."), null, (s, e) => EditSelected());
            gridMenu.Items.Add(L.T("Sil", "Delete"), null, (s, e) => DeleteSelected());
            gridMenu.Items.Add(L.T("Çoğalt", "Duplicate"), null, (s, e) => DuplicateSelected());
            gridMenu.Items.Add(L.T("Yukarı taşı", "Move up"), null, (s, e) => MoveSelected(-1));
            gridMenu.Items.Add(L.T("Aşağı taşı", "Move down"), null, (s, e) => MoveSelected(1));
            gridMenu.Items.Add(new ToolStripSeparator());
            gridMenu.Items.Add(L.T("Bekleme sürelerini ayarla...", "Set wait times..."), null, (s, e) => SetDelays());
            gridMenu.Items.Add(L.T("Buradan oynat", "Play from here"), null, (s, e) => StartPlay(true));
            gridMenu.Opening += (s, e) => { if (recorder.Active || player.IsRunning) e.Cancel = true; };
            grid.ContextMenuStrip = gridMenu;
        }

        StatusStrip BuildStatus()
        {
            var st = new StatusStrip();
            stStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            stCount = new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(6, 0, 6, 0) };
            stPos = new ToolStripStatusLabel { BorderSides = ToolStripStatusLabelBorderSides.Left, Padding = new Padding(6, 0, 6, 0), AutoSize = false, Width = 140 };
            st.Items.AddRange(new ToolStripItem[] { stStatus, stCount, stPos });
            return st;
        }

        void LoadOptions()
        {
            rbFullMode.Checked = cfg.FullMode;
            rbStepMode.Checked = !cfg.FullMode;
            chkRecordKeys.Checked = cfg.RecordKeys;
            rbRealDelay.Checked = cfg.RealDelays;
            rbFixedDelay.Checked = !cfg.RealDelays;
            SetSec(numFixed, cfg.FixedDelay);
            SetNum(numRepeat, cfg.Repeat);
            chkInfinite.Checked = cfg.Infinite;
            numRepeat.Enabled = !cfg.Infinite;
            SetSec(numGap, cfg.RepeatGap);
            SetNum(numSpeed, cfg.Speed);
            SetNum(numJitter, cfg.Jitter);
            SetNum(numSmooth, cfg.Smooth);
            chkStopOnMove.Checked = cfg.StopOnMove;
            chkMinimize.Checked = cfg.Minimize;
        }

        void SaveOptions()
        {
            cfg.FullMode = rbFullMode.Checked;
            cfg.RecordKeys = chkRecordKeys.Checked;
            cfg.RealDelays = rbRealDelay.Checked;
            cfg.FixedDelay = Ms(numFixed);
            cfg.Repeat = (int)numRepeat.Value;
            cfg.Infinite = chkInfinite.Checked;
            cfg.RepeatGap = Ms(numGap);
            cfg.Speed = (int)numSpeed.Value;
            cfg.Jitter = (int)numJitter.Value;
            cfg.Smooth = (int)numSmooth.Value;
            cfg.StopOnMove = chkStopOnMove.Checked;
            cfg.Minimize = chkMinimize.Checked;
            cfg.Steps = steps;
            cfg.LastFile = curFile;
            cfg.Dirty = dirty;
        }

        static void SetNum(NumericUpDown n, int v) => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        static string HkText(string hk) => string.IsNullOrEmpty(hk) ? "" : "  (" + hk + ")";

        void UpdateHotkeyTexts()
        {
            btnPlay.Text = L.T("▶ Oynat", "▶ Play") + HkText(cfg.HkPlay);
            btnStop.Text = L.T("■ Durdur", "■ Stop") + HkText(cfg.HkStop);
            UpdateRecordButton();
        }

        void UpdateRecordButton()
        {
            if (recorder.Active)
            {
                btnRecord.Text = L.T("■ Kaydı bitir", "■ Stop recording") + HkText(cfg.HkRecord);
                btnRecord.BackColor = Color.FromArgb(220, 40, 40);
                btnRecord.ForeColor = Color.White;
            }
            else
            {
                btnRecord.Text = L.T("● Kayda başla", "● Start recording") + HkText(cfg.HkRecord);
                btnRecord.BackColor = SystemColors.Control;
                btnRecord.ForeColor = Color.FromArgb(200, 30, 30);
                btnRecord.UseVisualStyleBackColor = true;
            }
        }

        void UpdateUi()
        {
            bool rec = recorder.Active, play = player.IsRunning, busy = rec || play;
            btnRecord.Enabled = !play;
            btnPlay.Enabled = btnPlayFrom.Enabled = !busy;
            btnStop.Enabled = busy;
            rbStepMode.Enabled = rbFullMode.Enabled = chkRecordKeys.Enabled = !rec;
            bool full = rbFullMode.Checked;
            rbRealDelay.Enabled = rbFixedDelay.Enabled = !rec && !full;
            numFixed.Enabled = !rec && !full && rbFixedDelay.Checked;
            sidePanel.Enabled = !busy;
            quickAdd.Enabled = !busy;
            miFile.Enabled = miSettings.Enabled = !busy;
            grid.ReadOnly = busy;
            if (!busy) { grid.Columns[3].ReadOnly = false; grid.Columns[4].ReadOnly = false; for (int i = 0; i < 3; i++) grid.Columns[i].ReadOnly = true; }
            UpdateRecordButton();
        }

        void UpdateTitle()
        {
            string name = string.IsNullOrEmpty(curFile) ? L.T("Adsız", "Untitled") : Path.GetFileNameWithoutExtension(curFile);
            Text = "GhostMacro — " + name + (dirty ? " *" : "");
        }

        void SetStatus(string s) => stStatus.Text = s;

        void Changed()
        {
            dirty = true;
            UpdateTitle();
            grid.RowCount = steps.Count;
            grid.Invalidate();
        }

        // ───────────────────────── tablo ─────────────────────────

        void Grid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= steps.Count) return;
            var s = steps[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 0: e.Value = e.RowIndex + 1; break;
                case 1: e.Value = s.ActionText; break;
                case 2: e.Value = s.Detail; break;
                case 3: e.Value = Sec(s.DelayMs); break;
                case 4: e.Value = s.Note; break;
            }
        }

        void Grid_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= steps.Count) return;
            var s = steps[e.RowIndex];
            if (e.ColumnIndex == 3)
            {
                if (TryParseSec(Convert.ToString(e.Value), out int v))
                {
                    if (v != s.DelayMs) { s.DelayMs = v; dirty = true; UpdateTitle(); }
                }
            }
            else if (e.ColumnIndex == 4)
            {
                string t = Convert.ToString(e.Value) ?? "";
                if (t != (s.Note ?? "")) { s.Note = t; dirty = true; UpdateTitle(); }
            }
        }

        void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= steps.Count) return;
            if (e.RowIndex == playingIndex)
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 230, 120);
                e.CellStyle.SelectionBackColor = Color.FromArgb(230, 190, 60);
                e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Color.Black;
            }
            else if (steps[e.RowIndex].Action == StepAction.Move && e.ColumnIndex != 0)
            {
                e.CellStyle.ForeColor = SystemColors.GrayText;
            }
        }

        /// <summary>ms → "1,5" gibi saniye metni (yerel ondalık ayırıcı).</summary>
        static string Sec(int ms) => (ms / 1000m).ToString("0.###", CultureInfo.CurrentCulture);

        /// <summary>"1,5" / "1.5" / "2" saniye metnini ms'ye çevirir.</summary>
        static bool TryParseSec(string text, out int ms)
        {
            ms = 0;
            string t = (text ?? "").Trim().Replace(',', '.');
            t = t.TrimEnd('s', 'S', 'n', 'N', ' ');
            if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal sec) || sec < 0) return false;
            ms = (int)Math.Round(Math.Min(sec, 86400m) * 1000m);
            return true;
        }

        void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (grid.IsCurrentCellInEditMode || recorder.Active || player.IsRunning) return;
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None) { EditSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.D && e.Control) { DuplicateSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up && e.Alt) { MoveSelected(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down && e.Alt) { MoveSelected(1); e.Handled = true; }
        }

        List<int> SelectedIndices() =>
            grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).Where(i => i >= 0 && i < steps.Count).OrderBy(i => i).ToList();

        int InsertIndex()
        {
            var sel = SelectedIndices();
            return sel.Count > 0 ? sel[sel.Count - 1] + 1 : steps.Count;
        }

        void SelectRange(int start, int count)
        {
            grid.RowCount = steps.Count;
            if (steps.Count == 0 || count <= 0) { grid.ClearSelection(); return; }
            start = Math.Max(0, Math.Min(steps.Count - 1, start));
            try
            {
                grid.CurrentCell = grid[0, start];
                grid.ClearSelection();
                for (int i = start; i < Math.Min(steps.Count, start + count); i++) grid.Rows[i].Selected = true;
            }
            catch { }
        }

        void SelectIndices(IEnumerable<int> idx)
        {
            grid.RowCount = steps.Count;
            var list = idx.Where(i => i >= 0 && i < steps.Count).ToList();
            if (list.Count == 0) return;
            try
            {
                grid.CurrentCell = grid[0, list[0]];
                grid.ClearSelection();
                foreach (var i in list) grid.Rows[i].Selected = true;
            }
            catch { }
        }

        void InsertStep(Step s)
        {
            int at = InsertIndex();
            steps.Insert(at, s);
            Changed();
            SelectRange(at, 1);
        }

        void AddPickedClick()
        {
            var p = PointPicker.PickPoint();
            if (!p.HasValue) return;
            var s = new Step { Action = StepAction.Click, Button = MouseBtn.Left, X = p.Value.X, Y = p.Value.Y, DelayMs = cfg.DefaultDelay };
            if (StepEditor.Edit(this, s, L.T("Tıklama adımı", "Click step"))) InsertStep(s);
        }

        void AddWithEditor(StepAction a)
        {
            var c = Input.CursorPos();
            var s = new Step { Action = a, X = c.X, Y = c.Y, X2 = c.X + 100, Y2 = c.Y, DelayMs = cfg.DefaultDelay };
            if (a == StepAction.Wheel) s.Amount = -3;
            if (a == StepAction.Drag) s.Amount = 300;
            if (StepEditor.Edit(this, s, L.T("Yeni adım", "New step"))) InsertStep(s);
        }

        void EditSelected()
        {
            var sel = SelectedIndices();
            if (sel.Count == 0) return;
            int i = sel[0];
            var copy = steps[i].Clone();
            if (StepEditor.Edit(this, copy, L.T($"Adım {i + 1}", $"Step {i + 1}")))
            {
                steps[i] = copy;
                Changed();
                SelectRange(i, 1);
            }
        }

        void DeleteSelected()
        {
            var sel = SelectedIndices();
            if (sel.Count == 0) return;
            for (int k = sel.Count - 1; k >= 0; k--) steps.RemoveAt(sel[k]);
            Changed();
            SelectRange(Math.Min(sel[0], steps.Count - 1), 1);
        }

        void DuplicateSelected()
        {
            var sel = SelectedIndices();
            if (sel.Count == 0) return;
            int at = sel[sel.Count - 1] + 1;
            steps.InsertRange(at, sel.Select(i => steps[i].Clone()).ToList());
            Changed();
            SelectRange(at, sel.Count);
        }

        void MoveSelected(int dir)
        {
            var sel = SelectedIndices();
            if (sel.Count == 0) return;
            if (dir < 0 && sel[0] == 0) return;
            if (dir > 0 && sel[sel.Count - 1] == steps.Count - 1) return;
            var order = dir < 0 ? sel : Enumerable.Reverse(sel).ToList();
            foreach (var i in order)
            {
                var t = steps[i];
                steps[i] = steps[i + dir];
                steps[i + dir] = t;
            }
            Changed();
            SelectIndices(sel.Select(i => i + dir));
        }

        void SetDelays()
        {
            var sel = SelectedIndices();
            bool onlySel = sel.Count > 1;
            if (steps.Count == 0) return;
            string label = onlySel
                ? L.T($"Seçili {sel.Count} adımın bekleme süresi:", $"Wait time for the {sel.Count} selected steps:")
                : L.T($"TÜM {steps.Count} adımın bekleme süresi:", $"Wait time for ALL {steps.Count} steps:");
            int start = onlySel ? steps[sel[0]].DelayMs : cfg.DefaultDelay;
            var v = NumberDialog.AskSeconds(this, L.T("Bekleme süreleri", "Wait times"), label, start);
            if (!v.HasValue) return;
            foreach (var i in onlySel ? sel : Enumerable.Range(0, steps.Count)) steps[i].DelayMs = v.Value;
            Changed();
        }

        void ClearAll()
        {
            if (steps.Count == 0) return;
            if (MessageBox.Show(this, L.T($"{steps.Count} adımın hepsi silinsin mi?", $"Delete all {steps.Count} steps?"), Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            steps.Clear();
            Changed();
        }

        // ───────────────────────── dosya ─────────────────────────

        bool ConfirmDiscard()
        {
            if (!dirty || steps.Count == 0) return true;
            var r = MessageBox.Show(this, L.T("Mevcut makroda kaydedilmemiş değişiklikler var. Kaydedilsin mi?",
                    "The current macro has unsaved changes. Save it?"), Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) return SaveMacro(false);
            return true;
        }

        void NewMacro()
        {
            if (!ConfirmDiscard()) return;
            steps.Clear();
            curFile = null;
            dirty = false;
            grid.RowCount = 0;
            grid.Invalidate();
            UpdateTitle();
            SetStatus(L.T("Yeni makro.", "New macro."));
        }

        const string Filter = "GhostMacro (*.gmacro)|*.gmacro|XML (*.xml)|*.xml|*.*|*.*";

        void OpenMacro()
        {
            if (!ConfirmDiscard()) return;
            using (var d = new OpenFileDialog { Filter = Filter, Title = L.T("Makro aç", "Open macro") })
            {
                if (!string.IsNullOrEmpty(curFile)) try { d.InitialDirectory = Path.GetDirectoryName(curFile); } catch { }
                if (d.ShowDialog(this) != DialogResult.OK) return;
                OpenFile(d.FileName);
            }
        }

        void OpenFile(string path)
        {
            try
            {
                var list = Store.LoadMacro(path);
                steps.Clear();
                steps.AddRange(list);
                curFile = path;
                dirty = false;
                grid.RowCount = steps.Count;
                grid.Invalidate();
                UpdateTitle();
                SelectRange(0, 1);
                SetStatus(L.T($"Açıldı: {Path.GetFileName(path)}", $"Opened: {Path.GetFileName(path)}"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("Dosya açılamadı:\n", "Could not open the file:\n") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        bool SaveMacro(bool saveAs)
        {
            string path = curFile;
            if (saveAs || string.IsNullOrEmpty(path))
            {
                using (var d = new SaveFileDialog { Filter = Filter, DefaultExt = "gmacro", AddExtension = true, Title = L.T("Makroyu kaydet", "Save macro") })
                {
                    if (!string.IsNullOrEmpty(curFile))
                    {
                        try { d.InitialDirectory = Path.GetDirectoryName(curFile); d.FileName = Path.GetFileName(curFile); } catch { }
                    }
                    if (d.ShowDialog(this) != DialogResult.OK) return false;
                    path = d.FileName;
                }
            }
            try
            {
                Store.SaveMacro(path, steps);
                curFile = path;
                dirty = false;
                UpdateTitle();
                SetStatus(L.T($"Kaydedildi: {Path.GetFileName(path)}", $"Saved: {Path.GetFileName(path)}"));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.T("Kaydedilemedi:\n", "Could not save:\n") + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ───────────────────────── ayarlar ─────────────────────────

        void EditHotkeys()
        {
            if (HotkeyDialog.Edit(this, cfg))
            {
                ParseHotkeys();
                UpdateHotkeyTexts();
                Store.SaveSettings(SnapshotSettings());
            }
        }

        AppSettings SnapshotSettings()
        {
            SaveOptions();
            return cfg;
        }

        void ChangeLang(string lang)
        {
            if (cfg.Lang == lang) return;
            cfg.Lang = lang;
            Program.RestartRequested = true;
            Close();
        }

        void ShowHelp()
        {
            string hr = cfg.HkRecord, hp = cfg.HkPlay, hs = cfg.HkStop, hc = cfg.HkCapture;
            MessageBox.Show(this, L.T(
                "ADIM ADIM KAYIT (varsayılan)\n" +
                $"1) \"Kayda başla\"ya basın ({hr}).\n" +
                "2) Ekranda istediğiniz yerlere sırayla tıklayın, gerekirse tuşlara basın.\n" +
                $"3) Bitince yine {hr} (veya {hs}).\n" +
                "Fare yolu kaydedilmez; sadece tıklamalar, sürüklemeler, tekerlek ve tuşlar adım olur.\n" +
                "Sonra tablodaki \"Bekle (sn)\" hücresine tıklayıp o adımdan ÖNCE beklenecek saniyeyi yazın (örn. 1,5).\n\n" +
                "ELLE ADIM EKLEME\n" +
                "\"＋ Tıklama\", \"＋ Tuş\", \"＋ Bekle\" düğmeleri seçili satırın altına adım ekler.\n" +
                $"\"Adım ekle\" menüsü: ekrandan nokta seçerek tıklama, tuş/kombinasyon (Ctrl+C, Win+R...), metin, bekleme...\n" +
                $"{hc}: imlecin o an bulunduğu yere tıklama adımı ekler (kayıt yapmadan).\n\n" +
                "TAM KAYIT\n" +
                "Her fare hareketi ve tuş gerçek zamanlamasıyla kaydedilir.\n\n" +
                "OYNATMA\n" +
                $"{hp}: oynat, {hs}: durdur. Fareyi oynatırsanız da durur (seçenek).\n" +
                "Tekrar, hız, rastgelelik ve yumuşak hareket ayarlanabilir.\n\n" +
                "NOT: Yönetici olarak çalışan bir programı kontrol etmek için GhostMacro'yu da\n" +
                "\"Yönetici olarak çalıştır\" ile açın. Kısayollar Ayarlar menüsünden değişir.",
                "STEP-BY-STEP RECORDING (default)\n" +
                $"1) Press \"Start recording\" ({hr}).\n" +
                "2) Click the places you want in order, press keys if needed.\n" +
                $"3) When done press {hr} again (or {hs}).\n" +
                "The mouse path is not recorded; only clicks, drags, wheel and keys become steps.\n" +
                "Then click a \"Wait (s)\" cell and type the seconds to wait BEFORE that step (e.g. 1.5).\n\n" +
                "ADDING STEPS BY HAND\n" +
                "The \"＋ Click\", \"＋ Key\", \"＋ Wait\" buttons add a step below the selected row.\n" +
                "\"Add step\" menu: click picked on screen, key/combination (Ctrl+C, Win+R...), text, wait...\n" +
                $"{hc}: adds a click step at the current cursor position (without recording).\n\n" +
                "FULL RECORDING\n" +
                "Every mouse movement and key is recorded with real timing.\n\n" +
                "PLAYBACK\n" +
                $"{hp}: play, {hs}: stop. Moving the mouse also stops it (option).\n" +
                "Repeat, speed, randomness and smooth movement can be adjusted.\n\n" +
                "NOTE: To control a program running as administrator, start GhostMacro with\n" +
                "\"Run as administrator\" too. Hotkeys can be changed in the Settings menu."),
                L.T("Nasıl kullanılır", "How to use"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ───────────────────────── kısayollar / kancalar ─────────────────────────

        void ParseHotkeys()
        {
            hotkeys.Clear();
            void P(HkKind k, string text)
            {
                if (string.IsNullOrWhiteSpace(text) || !KeyNames.TryParse(text, out var vks, out _)) return;
                var h = new Hk();
                foreach (var vk in vks)
                {
                    switch (KeyNames.ModGroup(vk))
                    {
                        case KeyNames.VK_CONTROL: h.Mods |= 1; break;
                        case KeyNames.VK_SHIFT: h.Mods |= 2; break;
                        case KeyNames.VK_MENU: h.Mods |= 4; break;
                        case KeyNames.VK_LWIN: h.Mods |= 8; break;
                        default: h.Vk = vk; break;
                    }
                }
                if (h.Vk != 0) hotkeys[k] = h;
            }
            P(HkKind.Record, cfg.HkRecord);
            P(HkKind.Play, cfg.HkPlay);
            P(HkKind.Stop, cfg.HkStop);
            P(HkKind.Capture, cfg.HkCapture);
        }

        static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        static int CurrentMods() =>
            (Down(0x11) ? 1 : 0) | (Down(0x10) ? 2 : 0) | (Down(0x12) ? 4 : 0) | (Down(0x5B) || Down(0x5C) ? 8 : 0);

        bool IsHotkeyKey(int vk)
        {
            if (swallowed.Contains(vk)) return true;
            int m = CurrentMods();
            return hotkeys.Values.Any(h => h.Vk == vk && h.Mods == m);
        }

        bool Applicable(HkKind k)
        {
            bool rec = recorder.Active, play = player.IsRunning;
            switch (k)
            {
                case HkKind.Stop: return rec || play;
                case HkKind.Play: return !rec && !play;
                case HkKind.Record: return !play;
                case HkKind.Capture: return !rec && !play;
            }
            return false;
        }

        bool OnHookKey(int msg, int vk, bool injected)
        {
            recorder.OnKey(msg, vk, injected);
            if (injected) return false;
            bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            if (!down) return swallowed.Remove(vk);
            if (swallowed.Contains(vk)) return true; // otomatik tekrar
            if (!IsHandleCreated || !IsWindowEnabled(Handle)) return false; // açık bir pencere/ileti kutusu var

            int mods = CurrentMods();
            foreach (var kv in hotkeys)
            {
                if (kv.Value.Vk != vk || kv.Value.Mods != mods || !Applicable(kv.Key)) continue;
                swallowed.Add(vk);
                var kind = kv.Key;
                BeginInvoke((Action)(() => RunHotkey(kind)));
                return true;
            }
            return false;
        }

        void RunHotkey(HkKind k)
        {
            if (!Applicable(k)) return;
            switch (k)
            {
                case HkKind.Record: ToggleRecord(); break;
                case HkKind.Play: StartPlay(false); break;
                case HkKind.Stop: StopAll(); break;
                case HkKind.Capture: CaptureAtCursor(); break;
            }
        }

        void OnHookMouse(int msg, POINT pt, int wheel, bool injected)
        {
            if (player.IsRunning)
            {
                if (stopOnMove && !injected && msg == WM_MOUSEMOVE &&
                    Math.Max(Math.Abs(pt.X - player.LastX), Math.Abs(pt.Y - player.LastY)) > 25)
                    player.Stop(L.T("Fare hareket ettirildi — durduruldu.", "Mouse was moved — stopped."));
                return;
            }
            recorder.OnMouse(msg, pt, wheel, injected);
        }

        bool IsOwnWindow(POINT pt)
        {
            IntPtr h = WindowFromPoint(pt);
            if (h == IntPtr.Zero) return false;
            IntPtr root = GetAncestor(h, GA_ROOT);
            GetWindowThreadProcessId(root != IntPtr.Zero ? root : h, out uint pid);
            return pid == myPid;
        }

        void CaptureAtCursor()
        {
            var p = Input.CursorPos();
            var s = new Step { Action = StepAction.Click, Button = MouseBtn.Left, X = p.X, Y = p.Y, DelayMs = cfg.DefaultDelay };
            steps.Add(s);
            Changed();
            SelectRange(steps.Count - 1, 1);
            grid.FirstDisplayedScrollingRowIndex = Math.Max(0, steps.Count - 1);
            SetStatus(L.T($"Tıklama eklendi: X {p.X}, Y {p.Y}", $"Click added: X {p.X}, Y {p.Y}"));
            System.Media.SystemSounds.Asterisk.Play();
        }

        // ───────────────────────── kayıt ─────────────────────────

        int recordStart;

        void ToggleRecord()
        {
            if (player.IsRunning) return;
            if (recorder.Active) { EndRecord(); return; }

            grid.EndEdit();
            if (!hooks.Installed) hooks.Install();
            recorder.FullMode = rbFullMode.Checked;
            recorder.RecordKeys = chkRecordKeys.Checked;
            recorder.RealDelays = rbRealDelay.Checked;
            recorder.FixedDelay = Ms(numFixed);
            recordStart = steps.Count;
            recorder.Begin(steps);
            UpdateUi();
            SetStatus(recorder.FullMode
                ? L.T($"TAM KAYIT sürüyor... Bitirmek için {cfg.HkRecord}.", $"FULL RECORDING... Press {cfg.HkRecord} to finish.")
                : L.T($"Kayıt sürüyor: tıklayın / tuşlara basın. Bitirmek için {cfg.HkRecord}.", $"Recording: click / press keys. Press {cfg.HkRecord} to finish."));
        }

        void EndRecord()
        {
            bool full = recorder.FullMode;
            recorder.End();
            if (full) TrimFullRecording();
            int added = steps.Count - recordStart;
            if (added > 0) dirty = true;
            grid.RowCount = steps.Count;
            grid.Invalidate();
            UpdateTitle();
            UpdateUi();
            if (added > 0) SelectRange(recordStart, added);
            SetStatus(L.T($"Kayıt bitti: {added} adım eklendi.", $"Recording finished: {added} steps added."));
        }

        /// <summary>Tam kayıtta kısayol tuşlarından kalan artıkları temizler.</summary>
        void TrimFullRecording()
        {
            // başta: kayıttan önce basılmış tuşların bırakılması
            var downs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = recordStart; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s.Action == StepAction.KeyDown) downs.Add(s.Keys);
                else if (s.Action == StepAction.KeyUp && !downs.Contains(s.Keys))
                {
                    if (i + 1 < steps.Count) steps[i + 1].DelayMs += s.DelayMs;
                    steps.RemoveAt(i);
                    i--;
                }
            }
            // sonda: bitirme kısayolunun değiştirici tuşları ve pencereye doğru fare hareketleri
            while (steps.Count > recordStart)
            {
                var s = steps[steps.Count - 1];
                if (s.Action == StepAction.Move || s.Action == StepAction.KeyDown) steps.RemoveAt(steps.Count - 1);
                else break;
            }
        }

        // ───────────────────────── oynatma ─────────────────────────

        void StartPlay(bool fromSelected)
        {
            if (player.IsRunning || recorder.Active) return;
            grid.EndEdit();
            if (steps.Count == 0)
            {
                MessageBox.Show(this, L.T("Oynatılacak adım yok. Önce kayıt yapın veya adım ekleyin.", "There are no steps to play. Record or add steps first."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (Step.IsKeyAction(s.Action) && !KeyNames.TryParse(s.Keys, out _, out string err))
                {
                    SelectRange(i, 1);
                    MessageBox.Show(this, L.T($"Adım {i + 1}: ", $"Step {i + 1}: ") + err, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            int start = 0;
            if (fromSelected)
            {
                var sel = SelectedIndices();
                if (sel.Count > 0) start = sel[0];
            }

            var o = new PlayOptions
            {
                Repeat = chkInfinite.Checked ? 0 : (int)numRepeat.Value,
                RepeatGapMs = Ms(numGap),
                SpeedPct = (int)numSpeed.Value,
                JitterPct = (int)numJitter.Value,
                SmoothMs = (int)numSmooth.Value,
                StartIndex = start,
            };
            stopOnMove = chkStopOnMove.Checked;
            playingIndex = -1;
            lastShownIndex = -1;
            if (chkMinimize.Checked) WindowState = FormWindowState.Minimized;
            player.Start(steps, o);
            UpdateUi();
            SetStatus(L.T($"Oynatılıyor... Durdurmak için {cfg.HkStop}.", $"Playing... Press {cfg.HkStop} to stop."));
        }

        void StopAll()
        {
            if (recorder.Active) EndRecord();
            if (player.IsRunning) player.Stop();
        }

        void OnPlayFinished(string result)
        {
            int last = playingIndex;
            playingIndex = -1;
            if (last >= 0 && last < grid.RowCount) grid.InvalidateRow(last);
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            try { Activate(); } catch { }
            UpdateUi();
            SetStatus(result);
        }

        void OnTick(object sender, EventArgs e)
        {
            var p = Input.CursorPos();
            stPos.Text = $"X {p.X}   Y {p.Y}";
            stCount.Text = L.T($"{steps.Count} adım", $"{steps.Count} steps");

            if (recorder.Active)
            {
                if (grid.RowCount != steps.Count)
                {
                    grid.RowCount = steps.Count;
                    if (steps.Count > 0)
                        try { grid.FirstDisplayedScrollingRowIndex = steps.Count - 1; } catch { }
                }
                grid.Invalidate();
            }
            else if (player.IsRunning)
            {
                int idx = player.CurrentIndex;
                if (idx != lastShownIndex)
                {
                    int old = playingIndex;
                    playingIndex = idx;
                    lastShownIndex = idx;
                    if (old >= 0 && old < grid.RowCount) grid.InvalidateRow(old);
                    if (idx >= 0 && idx < grid.RowCount)
                    {
                        grid.InvalidateRow(idx);
                        int first = grid.FirstDisplayedScrollingRowIndex;
                        int vis = Math.Max(1, grid.DisplayedRowCount(false));
                        if (idx < first || idx >= first + vis)
                            try { grid.FirstDisplayedScrollingRowIndex = Math.Max(0, idx - vis / 3); } catch { }
                    }
                    string loop = chkInfinite.Checked ? $"{player.CurrentLoop}/∞" : $"{player.CurrentLoop}/{numRepeat.Value}";
                    SetStatus(L.T($"Oynatılıyor: adım {idx + 1}/{steps.Count}, tur {loop}  —  durdurmak için {cfg.HkStop}",
                                  $"Playing: step {idx + 1}/{steps.Count}, loop {loop}  —  press {cfg.HkStop} to stop"));
                }
            }
        }

        // ───────────────────────── kapanış ─────────────────────────

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (recorder.Active) EndRecord();
            if (player.IsRunning) player.Stop();
            grid.EndEdit();
            timer.Stop();
            hooks.Uninstall();
            Store.SaveSettings(SnapshotSettings());
        }
    }
}
