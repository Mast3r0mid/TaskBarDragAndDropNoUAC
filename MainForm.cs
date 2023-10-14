using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using TaskBarDragAndDrop;

namespace TaskBarDragAndDropNoUAC
{


    public partial class MainForm : Form
    {



        ////////////////////////////////
        //
        // DLL imports and Global Vars
        //
        ////////////////////////////////



        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        const int KEYEVENTF_KEYUP = 0x2;
        const int VK_LWIN = 0x5B; // Left Win key
        const int VK_T = 0x54; // 'T' key

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);


        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private const int VK_LBUTTON = 0x01;
        private static bool isDragging = false;
        private static System.Drawing.Point dragStartPoint = System.Drawing.Point.Empty;
        private static bool ImDone, focused = false;
        private static Rect TrayRectangle;
        private static AutomationElement ChosentaskIcon;
        static Mutex mutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC)");
        static Mutex aboutMutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC) AboutBoxMutex");
        public bool aboutBoxOpen, mainFormOpen, myEnd = false;
        AboutBox1 aboutForm = new AboutBox1();
        private void ShowAboutPage()
        {

            if (aboutMutex.WaitOne(TimeSpan.Zero, true) && !aboutBoxOpen)
            {

                {
                    aboutBoxOpen = true; // Set the flag to indicate that the About box is open.
                    aboutForm.FormClosed += (s, ev) => aboutBoxOpen = false;
                    aboutForm.ShowDialog();
                    aboutMutex.ReleaseMutex();
                }
            }
            else
            {
                aboutForm.Select();

            }
        }

        public MainForm()
        {
            InitializeComponent();
        }

        private System.Windows.Point ConvertDraw2system(System.Drawing.Point Draw2sys)
        {

            ///convert system.windows.Points to system.Draw.Point // Works for me / Need to test more to make sure_
            //if it really works
            System.Windows.Point Drawsystem = new System.Windows.Point(int.Parse(Draw2sys.X.ToString()), int.Parse(Draw2sys.Y.ToString()));

            return Drawsystem;
        }

        private bool CheckMousearea(Rect showme)
        {


            // get cursor Point by Unhex systemDrawingPoint 

            string cursorXsystempnt = Cursor.Position.X.ToString();
            string cursorYsystempnt = Cursor.Position.Y.ToString();

            // create new systemPoint for cursor
            System.Windows.Point SystemWindowsCursorPoint = ConvertDraw2system(Cursor.Position);// new System.Windows.Point(int.Parse(cursorXsystempnt), int.Parse(cursorYsystempnt));

            //check if mouse is in Icon area

            if (showme.Contains(SystemWindowsCursorPoint) && isDragging)
            {

                return true;

            }
            else
            {
                return false;
            }

        }




        private int FindTaskbar(int justtray)
        {

            //check for Multi-scren and active screen
            System.Drawing.Point cursorpos = Cursor.Position;
            Screen screen = Screen.FromPoint(cursorpos);
            String TrayhWnd = "";

            if (screen.Primary) // use active desktop
            {
                TrayhWnd = "Shell_TrayWnd";

            }
            else
            {
                TrayhWnd = "Shell_SecondaryTrayWnd";

            }

            PropertyCondition classNameCondition = new PropertyCondition(AutomationElement.ClassNameProperty, TrayhWnd);

            // Search for the main window of the target application
            AutomationElement targetAppWindow = AutomationElement.RootElement.FindFirst(System.Windows.Automation.TreeScope.Children, classNameCondition);

            if (targetAppWindow != null)
            {
                PropertyCondition taskbarelements = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");

                // Find all Icons in the Taskbar by its class name
                AutomationElementCollection panel = targetAppWindow.FindAll(System.Windows.Automation.TreeScope.Descendants, taskbarelements);

                if (justtray == 28 && panel.Count > 0)
                { // if i only need taskbar area // random number // my fav number :)

                    TrayRectangle = new Rect(panel[0].Current.BoundingRectangle.Left, panel[0].Current.BoundingRectangle.Top, panel[panel.Count - 1].Current.BoundingRectangle.Right, panel[0].Current.BoundingRectangle.Bottom);


                    // TrayRectangle = targetAppWindow.Current.BoundingRectangle;
                    return 1;
                }

                // for each Icon in Task Bar we found

                foreach (AutomationElement taskbaricons in panel)
                {

                    //if mouse is over the Icon in TaskBar  and draging and dragstart and current is not the dame

                    Rect itemrect = taskbaricons.Current.BoundingRectangle;

                    /// check AutomationID if its a real icon not a widget or other items
                    /// 
                    //Fix 1.0.1 Detect Separate icons in TaskBar : Contains("Window:")
                    if ((taskbaricons.Current.AutomationId.Contains("Appid:") || taskbaricons.Current.AutomationId.Contains("Window:")) && CheckMousearea(itemrect) && isDragging && Cursor.Position != dragStartPoint)

                    {

                        //save area in global val, Just in case , maybe for future
                        ChosentaskIcon = taskbaricons;
                        //use old trick to create taskSwitch thumbnail live preview

                        while (!focused)
                        {

                            SetForegroundWindow(FindWindow(TrayhWnd, null));
                            Thread.Sleep(1);

                            //check if item focused
                            if (taskbaricons.Current.HasKeyboardFocus)
                            {
                                focused = true;
                            }
                            else
                            {
                                // Simulate releasing the Win+T key
                                keybd_event(VK_LWIN, 0, 0, 0);
                                keybd_event(VK_T, 0, 0, 0);
                                Thread.Sleep(1);
                                // Simulate releasing the Win+T key
                                keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);
                                keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
                                taskbaricons.SetFocus();
                            }

                        }


                        if (Conf.Default.ClickPinApp) // if auto click enabled
                        {
                            //due check if you moved your cursor during wait time
                            System.Drawing.Point LastCursorCheck = Cursor.Position;
                            Rect tmprect = new Rect(ConvertDraw2system(LastCursorCheck).X, ConvertDraw2system(LastCursorCheck).Y, 10, 10);
                            Thread.Sleep(Conf.Default.clickInterval);
                            if (CheckMousearea(tmprect))
                            {

                                InvokePattern invokePattern = (InvokePattern)taskbaricons.GetCurrentPattern(InvokePattern.Pattern);
                                invokePattern.Invoke();

                            }
                            else
                            {

                                ImDone = false;
                                return 0;
                            }
                        }


                        //much better this way
                        //for ever click any icon in taskbar as long as LBTN is down
                        //ImDone = true;
                        return 1;

                    }
                    else
                    {
                        //mouse it out of Tray Area
                    }

                }


            }
            else
            {

                return 0;

            }

            return 1;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Specify properties to identify the target application's main window by class name
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();

            }
            else
            {
                MessageBox.Show("Another instance of the application is already running.", "Application Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                myEnd = true;
                this.Close();

            }

            checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
            checkbox_closeTray.Checked = Conf.Default.closetotray;
            checkbox_Runatstart.Checked = Conf.Default.Runatstart;
            txt_mousehook.Text = Conf.Default.mousehookint.ToString();
            txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;
            ShowInTaskbar = false;


        }

        private void MouseIsDragging_Tick(object sender, EventArgs e)
        { //chech if LeftBtn is  pressed (  drag or Start dragging)
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
            {

                FindTaskbar(28); // find taskbar and save rectangle area in global val TrayRectangle 
                if (!isDragging && !CheckMousearea(TrayRectangle)) // if no drag happend before and mouse is not on taskbar
                {

                    isDragging = true;
                    //check drag start position
                    dragStartPoint = Cursor.Position;//save drag start point into global val


                }



                //temp rect from mouse drag start pos
                Rect tmprect = new Rect(ConvertDraw2system(dragStartPoint).X + 2, ConvertDraw2system(dragStartPoint).Y + 2, 1, 1);
                if (isDragging && CheckMousearea(TrayRectangle) && !TrayRectangle.Contains(tmprect) && !ImDone)
                {
                    focused = false;
                    FindTaskbar(1); //random number except 28

                }



            }
            else
            {
                //chech if LeftBtn is not pressed ( no drag or finish dragging)
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
                {
                    isDragging = false;
                    ImDone = false;
                    focused = false;

                }
            }
        }

        private void MouseInTaskBarClick_Tick(object sender, EventArgs e)
        {



        }

        private void checkbox_Runatstart_CheckedChanged(object sender, EventArgs e)
        {
            if (checkbox_Runatstart.Checked)
            {

                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                if (key != null)
                {
                    string appPath = Assembly.GetEntryAssembly().Location;
                    key.SetValue("TaskBar DragAndDrop(NO UAC)", appPath);
                    key.Close();


                }
                Conf.Default.Runatstart = true;
                Conf.Default.Save();
                Conf.Default.Reload();
            }
            else if (!checkbox_Runatstart.Checked)
            {

                RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                key.DeleteValue("TaskBar DragAndDrop(NO UAC)", false);
                key.Close();
                Conf.Default.Runatstart = false;
                Conf.Default.Save();
                Conf.Default.Reload();

            }


        }

        private void checkbox_ClickPinApp_CheckedChanged(object sender, EventArgs e)
        {
            if (checkbox_ClickPinApp.Checked)
            {
                Conf.Default.ClickPinApp = true;
                Conf.Default.Save();
                Conf.Default.Reload();

            }
            else if (!checkbox_ClickPinApp.Checked)
            {
                Conf.Default.ClickPinApp = false;
                Conf.Default.Save();
                Conf.Default.Reload();
            }
        }

        private void btn_savesetting_Click(object sender, EventArgs e)
        {
            Conf.Default.clickInterval = int.Parse(txt_clickInterval.Text.ToString());
            Conf.Default.mousehookint = int.Parse(txt_mousehook.Text.ToString());
            Conf.Default.Save();
            Conf.Default.Reload();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;

        }

        private void btn_resetsetting_Click(object sender, EventArgs e)
        {
            Conf.Default.clickInterval = 500;
            Conf.Default.mousehookint = 5;
            txt_mousehook.Text = "5";
            txt_clickInterval.Text = "500";
            Conf.Default.Save();
            Conf.Default.Reload();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;
        }

        private void txt_clickInterval_TextChanged(object sender, EventArgs e)
        {
            btn_resetsetting.Visible = true;
            btn_savesetting.Visible = true;
        }

        private void txt_mousehook_TextChanged(object sender, EventArgs e)
        {
            btn_resetsetting.Visible = true;
            btn_savesetting.Visible = true;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                ShowInTaskbar = false;
                mainFormOpen = false;
            }

        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.flaticon.com/free-icons/drag-and-drop");
        }

        private void pictureBox1_MouseHover(object sender, EventArgs e)
        {
            toolTip1.Show("Drag and drop icons created by Freepik" + Environment.NewLine + "- Flaticon ( Click To Open Website)", pictureBox1);
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.paypal.com/donate/?hosted_button_id=H8J45TXLNUQKW");
        }

        private void pictureBox2_MouseHover(object sender, EventArgs e)
        {
            toolTip1.Show("If you like it, you Can Buy me Anything", pictureBox2);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ShowAboutPage();

        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!mainFormOpen)
                {
                    this.Show();
                    WindowState = FormWindowState.Normal;
                    mainFormOpen = true;
                    ShowInTaskbar = true;
                }
                else
                {
                    aboutForm.Close();
                    // this.Hide();
                    this.WindowState = FormWindowState.Minimized;
                    mainFormOpen = false;
                    ShowInTaskbar = false;


                }

            }


        }

        private void ntf_exit_Click(object sender, EventArgs e)
        {
            myEnd = true;
            this.Close();
        }

        private void ntf_about_Click(object sender, EventArgs e)
        {
            ShowAboutPage();
        }

        private void ntf_issue_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Mast3r0mid/TaskBarDragAndDropNoUAC/issues");
        }

        private void ntf_checkupdate_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Mast3r0mid/TaskBarDragAndDropNoUAC");
        }

        private void checkbox_closeTray_CheckedChanged(object sender, EventArgs e)
        {
            if (checkbox_closeTray.Checked)
            {
                Conf.Default.closetotray = true;
                Conf.Default.Save();
            }
            else
            {
                Conf.Default.closetotray = false;
                Conf.Default.Save();
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Mast3r0mid");

        }

        private void pictureBox3_MouseHover(object sender, EventArgs e)
        {
            toolTip1.Show("Find me on GitHub", pictureBox3);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Conf.Default.closetotray && !myEnd)
            {
                e.Cancel = true;
                this.Hide();
                mainFormOpen = false;
            }
        }

        private void ntf_settings_Click(object sender, EventArgs e)
        {

            if (!mainFormOpen)
            {
                this.Show();
                WindowState = FormWindowState.Normal;
                mainFormOpen = true;
                ShowInTaskbar = true;
            }


        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Mast3r0mid/TaskBarDragAndDropNoUAC");
        }
    }
}
