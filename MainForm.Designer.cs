namespace TaskBarDragAndDropNoUAC
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ntf_settings = new System.Windows.Forms.ToolStripMenuItem();
            this.ntf_checkupdate = new System.Windows.Forms.ToolStripMenuItem();
            this.ntf_issue = new System.Windows.Forms.ToolStripMenuItem();
            this.ntf_about = new System.Windows.Forms.ToolStripMenuItem();
            this.ntf_exit = new System.Windows.Forms.ToolStripMenuItem();
            this.MouseIsDragging = new System.Windows.Forms.Timer(this.components);
            this.checkbox_Runatstart = new System.Windows.Forms.CheckBox();
            this.checkbox_ClickPinApp = new System.Windows.Forms.CheckBox();
            this.txt_mousehook = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.txt_clickInterval = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btn_savesetting = new System.Windows.Forms.Button();
            this.btn_resetsetting = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.checkbox_closeTray = new System.Windows.Forms.CheckBox();
            this.pictureBox3 = new System.Windows.Forms.PictureBox();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.SelectedTimer = new System.Windows.Forms.Timer(this.components);
            this.btn_localize = new System.Windows.Forms.Button();
            this.chekbox_log = new System.Windows.Forms.CheckBox();
            this.contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.notifyIcon1.BalloonTipText = "TaskBar DragAndDrop(NO UAC)";
            this.notifyIcon1.BalloonTipTitle = "TaskBar DragAndDrop(NO UAC)";
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "TaskBar DragAndDrop(NO UAC)";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ntf_settings,
            this.ntf_checkupdate,
            this.ntf_issue,
            this.ntf_about,
            this.ntf_exit});
            this.contextMenuStrip1.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(175, 114);
            // 
            // ntf_settings
            // 
            this.ntf_settings.Name = "ntf_settings";
            this.ntf_settings.Size = new System.Drawing.Size(174, 22);
            this.ntf_settings.Text = "Open TaskBar D&&D";
            this.ntf_settings.Click += new System.EventHandler(this.ntf_settings_Click);
            // 
            // ntf_checkupdate
            // 
            this.ntf_checkupdate.Name = "ntf_checkupdate";
            this.ntf_checkupdate.Size = new System.Drawing.Size(174, 22);
            this.ntf_checkupdate.Text = "&Check For Update";
            this.ntf_checkupdate.Click += new System.EventHandler(this.ntf_checkupdate_Click);
            // 
            // ntf_issue
            // 
            this.ntf_issue.Name = "ntf_issue";
            this.ntf_issue.Size = new System.Drawing.Size(174, 22);
            this.ntf_issue.Text = "&Report Issue";
            this.ntf_issue.Click += new System.EventHandler(this.ntf_issue_Click);
            // 
            // ntf_about
            // 
            this.ntf_about.Name = "ntf_about";
            this.ntf_about.Size = new System.Drawing.Size(174, 22);
            this.ntf_about.Text = "About";
            this.ntf_about.Click += new System.EventHandler(this.ntf_about_Click);
            // 
            // ntf_exit
            // 
            this.ntf_exit.Name = "ntf_exit";
            this.ntf_exit.Size = new System.Drawing.Size(174, 22);
            this.ntf_exit.Text = "Exit";
            this.ntf_exit.Click += new System.EventHandler(this.ntf_exit_Click);
            // 
            // MouseIsDragging
            // 
            this.MouseIsDragging.Interval = 5;
            this.MouseIsDragging.Tick += new System.EventHandler(this.MouseIsDragging_Tick);
            // 
            // checkbox_Runatstart
            // 
            this.checkbox_Runatstart.AutoSize = true;
            this.checkbox_Runatstart.Location = new System.Drawing.Point(12, 12);
            this.checkbox_Runatstart.Name = "checkbox_Runatstart";
            this.checkbox_Runatstart.Size = new System.Drawing.Size(142, 17);
            this.checkbox_Runatstart.TabIndex = 0;
            this.checkbox_Runatstart.Text = "Run at Windows Startup";
            this.checkbox_Runatstart.UseVisualStyleBackColor = true;
            this.checkbox_Runatstart.CheckedChanged += new System.EventHandler(this.checkbox_Runatstart_CheckedChanged);
            // 
            // checkbox_ClickPinApp
            // 
            this.checkbox_ClickPinApp.AutoSize = true;
            this.checkbox_ClickPinApp.Location = new System.Drawing.Point(12, 35);
            this.checkbox_ClickPinApp.Name = "checkbox_ClickPinApp";
            this.checkbox_ClickPinApp.Size = new System.Drawing.Size(152, 17);
            this.checkbox_ClickPinApp.TabIndex = 1;
            this.checkbox_ClickPinApp.Text = "Automate Run Pinned App";
            this.checkbox_ClickPinApp.UseVisualStyleBackColor = true;
            this.checkbox_ClickPinApp.CheckedChanged += new System.EventHandler(this.checkbox_ClickPinApp_CheckedChanged);
            // 
            // txt_mousehook
            // 
            this.txt_mousehook.Location = new System.Drawing.Point(153, 79);
            this.txt_mousehook.MaxLength = 4;
            this.txt_mousehook.Name = "txt_mousehook";
            this.txt_mousehook.Size = new System.Drawing.Size(49, 20);
            this.txt_mousehook.TabIndex = 2;
            this.txt_mousehook.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txt_mousehook.Visible = false;
            this.txt_mousehook.WordWrap = false;
            this.txt_mousehook.TextChanged += new System.EventHandler(this.txt_mousehook_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(25, 82);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(122, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Mouse Hook Interval ms";
            this.label1.Visible = false;
            // 
            // txt_clickInterval
            // 
            this.txt_clickInterval.Location = new System.Drawing.Point(153, 105);
            this.txt_clickInterval.MaxLength = 4;
            this.txt_clickInterval.Name = "txt_clickInterval";
            this.txt_clickInterval.Size = new System.Drawing.Size(49, 20);
            this.txt_clickInterval.TabIndex = 4;
            this.txt_clickInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txt_clickInterval.WordWrap = false;
            this.txt_clickInterval.TextChanged += new System.EventHandler(this.txt_clickInterval_TextChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(25, 108);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(109, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "Auto Click Interval ms";
            // 
            // btn_savesetting
            // 
            this.btn_savesetting.Location = new System.Drawing.Point(208, 58);
            this.btn_savesetting.Name = "btn_savesetting";
            this.btn_savesetting.Size = new System.Drawing.Size(75, 39);
            this.btn_savesetting.TabIndex = 6;
            this.btn_savesetting.Text = "Save Changes";
            this.btn_savesetting.UseVisualStyleBackColor = true;
            this.btn_savesetting.Visible = false;
            this.btn_savesetting.Click += new System.EventHandler(this.btn_savesetting_Click);
            // 
            // btn_resetsetting
            // 
            this.btn_resetsetting.Location = new System.Drawing.Point(208, 103);
            this.btn_resetsetting.Name = "btn_resetsetting";
            this.btn_resetsetting.Size = new System.Drawing.Size(75, 39);
            this.btn_resetsetting.TabIndex = 7;
            this.btn_resetsetting.Text = "Reset Default";
            this.btn_resetsetting.UseVisualStyleBackColor = true;
            this.btn_resetsetting.Visible = false;
            this.btn_resetsetting.Click += new System.EventHandler(this.btn_resetsetting_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 237);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(63, 23);
            this.button1.TabIndex = 11;
            this.button1.Text = "About Me";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(196, 12);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(81, 40);
            this.button2.TabIndex = 12;
            this.button2.Text = "Check For Update";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // checkbox_closeTray
            // 
            this.checkbox_closeTray.AutoSize = true;
            this.checkbox_closeTray.Location = new System.Drawing.Point(12, 58);
            this.checkbox_closeTray.Name = "checkbox_closeTray";
            this.checkbox_closeTray.Size = new System.Drawing.Size(125, 17);
            this.checkbox_closeTray.TabIndex = 13;
            this.checkbox_closeTray.Text = "Close to System Tray";
            this.checkbox_closeTray.UseVisualStyleBackColor = true;
            this.checkbox_closeTray.CheckedChanged += new System.EventHandler(this.checkbox_closeTray_CheckedChanged);
            // 
            // pictureBox3
            // 
            this.pictureBox3.Image = global::TaskBarDragAndDrop.Properties.Resources.GitHub_Logo_650x366;
            this.pictureBox3.Location = new System.Drawing.Point(175, 207);
            this.pictureBox3.Name = "pictureBox3";
            this.pictureBox3.Size = new System.Drawing.Size(102, 50);
            this.pictureBox3.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox3.TabIndex = 14;
            this.pictureBox3.TabStop = false;
            this.pictureBox3.Click += new System.EventHandler(this.pictureBox3_Click);
            this.pictureBox3.MouseHover += new System.EventHandler(this.pictureBox3_MouseHover);
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::TaskBarDragAndDrop.Properties.Resources.donate1;
            this.pictureBox2.Location = new System.Drawing.Point(283, 12);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(81, 40);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox2.TabIndex = 10;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Click += new System.EventHandler(this.pictureBox2_Click);
            this.pictureBox2.MouseHover += new System.EventHandler(this.pictureBox2_MouseHover);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::TaskBarDragAndDrop.Properties.Resources.drag_and_drop;
            this.pictureBox1.Location = new System.Drawing.Point(283, 207);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(81, 63);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 9;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            this.pictureBox1.MouseHover += new System.EventHandler(this.pictureBox1_MouseHover);
            // 
            // SelectedTimer
            // 
            this.SelectedTimer.Interval = 500;
            this.SelectedTimer.Tick += new System.EventHandler(this.SelectedTimer_Tick);
            // 
            // btn_localize
            // 
            this.btn_localize.Location = new System.Drawing.Point(289, 67);
            this.btn_localize.Name = "btn_localize";
            this.btn_localize.Size = new System.Drawing.Size(75, 43);
            this.btn_localize.TabIndex = 15;
            this.btn_localize.Text = "First Time Setup";
            this.btn_localize.UseVisualStyleBackColor = true;
            this.btn_localize.Click += new System.EventHandler(this.btn_localize_Click);
            // 
            // chekbox_log
            // 
            this.chekbox_log.AutoSize = true;
            this.chekbox_log.Location = new System.Drawing.Point(12, 81);
            this.chekbox_log.Name = "chekbox_log";
            this.chekbox_log.Size = new System.Drawing.Size(132, 17);
            this.chekbox_log.TabIndex = 16;
            this.chekbox_log.Text = "Show Logs(Next Start)";
            this.chekbox_log.UseVisualStyleBackColor = true;
            this.chekbox_log.CheckedChanged += new System.EventHandler(this.chekbox_log_CheckedChanged);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(376, 272);
            this.Controls.Add(this.chekbox_log);
            this.Controls.Add(this.btn_localize);
            this.Controls.Add(this.pictureBox3);
            this.Controls.Add(this.checkbox_closeTray);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.pictureBox2);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.btn_resetsetting);
            this.Controls.Add(this.btn_savesetting);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txt_clickInterval);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txt_mousehook);
            this.Controls.Add(this.checkbox_ClickPinApp);
            this.Controls.Add(this.checkbox_Runatstart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.Text = "TaskBar DragAndDrop(NO UAC)";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.contextMenuStrip1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.Timer MouseIsDragging;
        private System.Windows.Forms.CheckBox checkbox_Runatstart;
        private System.Windows.Forms.CheckBox checkbox_ClickPinApp;
        private System.Windows.Forms.TextBox txt_mousehook;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txt_clickInterval;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btn_savesetting;
        private System.Windows.Forms.Button btn_resetsetting;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ntf_settings;
        private System.Windows.Forms.ToolStripMenuItem ntf_checkupdate;
        private System.Windows.Forms.ToolStripMenuItem ntf_issue;
        private System.Windows.Forms.ToolStripMenuItem ntf_about;
        private System.Windows.Forms.ToolStripMenuItem ntf_exit;
        private System.Windows.Forms.CheckBox checkbox_closeTray;
        private System.Windows.Forms.PictureBox pictureBox3;
        private System.Windows.Forms.Timer SelectedTimer;
        private System.Windows.Forms.Button btn_localize;
        private System.Windows.Forms.CheckBox chekbox_log;
    }
}

