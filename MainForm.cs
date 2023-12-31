﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using TaskBarDragAndDrop;
using Serilog;
using Serilog.Sinks;
using System.Text.RegularExpressions;
using Serilog.Core;
using System.Linq;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace TaskBarDragAndDropNoUAC
{


    public partial class MainForm : Form
    {



        ////////////////////////////////
        //
        // DLL imports and Global Vars
        //
        ////////////////////////////////
        ///

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        // Windows API function to set a hook
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        // Windows API function to unhook a hook
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        // Windows API function to call the next hook in the chain
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // Windows API function to get a module handle
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);



        // Get Localized .MUI File
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(string lpFileName);


        // Get Localized Strng from .MUI File
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int LoadString(IntPtr hInstance, uint uID, [Out] System.Text.StringBuilder lpBuffer, int nBufferMax);


        // Find Window Handle
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);


        //Get Window Bounds Area
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECTOUT lpRect);
        //stract for upper dll
        [StructLayout(LayoutKind.Sequential)]
        public struct RECTOUT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private int mouseintsignal = 0x01;
        private static bool isDragging, clicked, focused, waitforFunc = false;
        private static System.Drawing.Point dragStartPoint = new System.Drawing.Point(0, 0);

        private String TrayhWnd = "Shell_TrayWnd"; //default name
        static readonly Mutex mutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC)");
        static readonly Mutex aboutMutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC) AboutBoxMutex");

        public bool aboutBoxOpen, mainFormOpen, myEnd = false;
        AboutBox1 aboutForm = new AboutBox1();
        CultureInfo cultureInfo = CultureInfo.CurrentUICulture;
        StreamWriter fileWriter;
        public static AutomationElement DesktopRootElement, selectedIcon = null;
        AutomationElementCollection TaskBarIconCollection = null;
        Rect ItemsRectArea = Rect.Empty;



        // Define the delegate for the mouse hook procedure
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14; // Hook type for low-level mouse hook
        private const int WM_MOUSEMOVE = 0x0200; // Windows message code for mouse movement

        private static LowLevelMouseProc _mouseProc;
        private static IntPtr _hookID = IntPtr.Zero;




        // Structure to hold mouse information
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT_HOOK pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Structure to hold mouse coordinates
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT_HOOK
        {
            public int x;
            public int y;
        }


        //About
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
            Log.Information("InitializeComponent();");
           InitializeComponent();

        }


        ///convert system.windows.Points to system.Draw.Point
        public System.Windows.Point ConvertDraw2system(System.Drawing.Point Draw2sys)
        {
            
            // Works for me / Need to test more to make sure_
            //if it really works
            System.Windows.Point Drawsystem = new System.Windows.Point(int.Parse(Draw2sys.X.ToString()), int.Parse(Draw2sys.Y.ToString()));

            return Drawsystem;
        }



        // get cursor system.Point by Unhex systemDrawingPoint 
        private bool CheckCurrentMouseareaWithRectArea(Rect showme)
        {


            // get cursor system.Point by Unhex systemDrawingPoint 

            string cursorXsystempnt = Cursor.Position.X.ToString();
            string cursorYsystempnt = Cursor.Position.Y.ToString();

            // create new systemPoint for cursor
            System.Windows.Point SystemWindowsCursorPoint = ConvertDraw2system(Cursor.Position);// new System.Windows.Point(int.Parse(cursorXsystempnt), int.Parse(cursorYsystempnt));

            //check if mouse is in Icon area

            if (showme.Contains(SystemWindowsCursorPoint))
            {

                return true;

            }
            else
            {
                return false;
            }

        }
        

        private void MainForm_Load(object sender, EventArgs e)
        {

            if (!Directory.Exists($"{Application.StartupPath}\\logs\\"))
            {
                Directory.CreateDirectory($"{Application.StartupPath}\\logs\\");
            }

            if (Conf.Default.showlog)
            {

                //create console window
                AllocConsole();
 
                Log.Logger = new LoggerConfiguration().WriteTo.Console(theme: AnsiConsoleTheme.Sixteen ).WriteTo.Async(a => a.File($"{Application.StartupPath}\\logs\\TaskBarDrag&drop.log", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)).CreateLogger();
                

                Log.Information($" Log Path : {Application.StartupPath}\\logs\\TaskBarDrag&drop.log");
              
            }
           
            // Specify properties to identify the target application's main window by class name
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();


            }
            else
            {
                Log.Error("Another App Found- Exit...");
                MessageBox.Show("Another instance of the application is already running.", "Application Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                myEnd = true;
                this.Close();

            }

            try
            {
                Log.Information("load settings");
                chekbox_log.Checked = Conf.Default.showlog;
                MouseIsDragging.Interval = Conf.Default.mousehookint;
                SelectedTimer.Interval = Conf.Default.clickInterval; //////////// check for later  SelectedTimer.Interval = 500; Conf.Default.mousehookint;
                checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
                checkbox_closeTray.Checked = Conf.Default.closetotray;
                checkbox_Runatstart.Checked = Conf.Default.Runatstart;
                txt_mousehook.Text = Conf.Default.mousehookint.ToString();
                txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
                btn_resetsetting.Visible = false;
                Log.Information($"clickInterval: {Conf.Default.clickInterval}");
                btn_savesetting.Visible = false;
                ShowInTaskbar = false;

                //
                if (Conf.Default.DisLan == null || Conf.Default.DisLan != cultureInfo.DisplayName)
                {
                    notifyIcon1_MouseClick(sender, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
                    btn_localize_Click(sender, e);
                }
                // Define the callback function for the mouse hook
                _mouseProc = HookCallback;
                
                // Set up the low-level mouse hook
                _hookID = SetHook(_mouseProc);

               
                Log.Information($"Log Window {Environment.NewLine} {aboutForm.AssemblyProduct} {Assembly.GetExecutingAssembly().GetName().Version.ToString()}: {Environment.NewLine} Current Language Pack: {cultureInfo} {DateTime.Now} {Environment.NewLine} Initial Setup Strings: '{Conf.Default.RunningWin}' And '{Conf.Default.multiWin}'");

            }
            catch (COMException ex)
            {
               Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }


        }



        //check for Multi-scren and active screen
        private string MyScreen(out string hWsc)
        {
            
            Screen screen = Screen.FromPoint(Cursor.Position);
            if (screen.Primary) // use active desktop
            {
                hWsc = "Shell_TrayWnd";

            }
            else
            {
                hWsc = "Shell_SecondaryTrayWnd";

            }
            Log.Information($"My Screen FN => Display: {hWsc}");

            return hWsc;
        }


        //beta 1.0.7 awesome new function
        // best resouce manager and speed and small function
        public bool SearchIconAndFocusNEW(String trayClassName, System.Drawing.Point cursorPnt)
        {
            SelectedTimer.Stop();
           
           Log.Information($"Search For Icon And Focus NEW FN ");

            try
            {
                if (selectedIcon != null)
                {
                    if (CheckCurrentMouseareaWithRectArea(selectedIcon.Current.BoundingRectangle))
                    {

                        Log.Warning($"Mouse is over the Last Selected Icon  - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} Area : {selectedIcon.Current.BoundingRectangle} - Exit Search Function");
                            waitforFunc = true;
                         SelectedTimer.Start();
                        selectedIcon = null;
                        return true;
                    }

                }
                Rect searchArea = new Rect(cursorPnt.X, cursorPnt.Y, 1, 1);
                AutomationElement taskbar = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, trayClassName));

                if (taskbar == null)
                {
                    
                    Log.Warning($"TaskBar  is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y}, Display: {trayClassName} ,Search Area : {searchArea} - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    //selectedIcon = null;
                    return false;
                }


                Condition condition = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");

                AutomationElement taskbarElement = taskbar.FindFirst(TreeScope.Descendants, condition);
                if (taskbarElement == null)
                {

                    Log.Warning($"TaskBar Elements list Is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} ,  Display: {trayClassName} - Exit Search Function");
                    waitforFunc = false;
                    SelectedTimer.Start();
                    return false;
                }
                TreeWalker walker = TreeWalker.ControlViewWalker;
            

                while (taskbarElement != null && !taskbarElement.Current.BoundingRectangle.Contains(new System.Windows.Point(Cursor.Position.X, Cursor.Position.Y)))
                {

                    taskbarElement = walker.GetNextSibling(taskbarElement);

                }

                if (taskbarElement == null)
                {

                    Log.Warning($"Taskbar Icon is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} ,    Display: {trayClassName} - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    
                    return false;
                }

                
               

                if (Conf.Default.ClickPinApp)
                {
                    Thread.Sleep(Conf.Default.clickInterval);
                    InvokePattern invokePattern = taskbarElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();

                    Log.Information("Auto Click, done");
                    waitforFunc = false;
                    SelectedTimer.Stop();

                }
                
                else if (!Conf.Default.ClickPinApp  && (taskbarElement.Current.Name.Replace(" ","").Contains(Conf.Default.RunningWin) || taskbarElement.Current.Name.Replace(" ", "").Contains(Conf.Default.multiWin)))
                {

                   
                    Thread.Sleep(Conf.Default.clickInterval);
                    InvokePattern invokePattern = taskbarElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();
                    Log.Information("Running Window Click, done");
                    waitforFunc = false;
                    SelectedTimer.Stop();
                }
                else
                {
                    
                    Log.Information("just Focus- no running Window"); // focus: show default win tooltip
                    waitforFunc = false;
                    taskbarElement.SetFocus();
                    SelectedTimer.Stop();

                }

                selectedIcon = taskbarElement;
                SelectedTimer.Stop();
            }

            catch (COMException ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
                waitforFunc = false;
            }
            catch (Exception ex)
            {
               Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
                waitforFunc = false;
            }



            return true;
        }



        /// Main Function : select Element and invoke or Focus Based on mouse position
        /// 
        /// OLD FUNCTION/ big bugy  high resource usage function
       /* public void SearchIconAndFocus(AutomationElementCollection TaskbarItemCollectinHolder, System.Drawing.Point MousePos, out AutomationElement SelectedIconfn)
        {
            SelectedIconfn = null; // Initialize to null
            int counter = 0;
            Rect MousePosRec = new Rect(MousePos.X, MousePos.Y, 1, 1);
            bool tmpbool = false;


            try
            {

                foreach (AutomationElement item in TaskbarItemCollectinHolder)
                {
                    tmpbool = false;
                    if (item.Current.AutomationId.Contains("Appid:") || item.Current.AutomationId.Contains("Window:"))
                    {
                        tmpbool = true;
                        counter++;
                    }

                    if (tmpbool && item.Current.BoundingRectangle.Contains(MousePosRec) && isDragging && Cursor.Position != dragStartPoint)

                    {

                        SelectedIconfn = item;
                        InvokePattern selection = item.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                        if (selection != null)
                        {
                            if (Conf.Default.ClickPinApp) //&& !SelectedIconfn.Current.Name.Contains("running window"))
                            {
                                Thread.Sleep(Conf.Default.clickInterval);
                                if (CheckCurrentMouseareaWithRectArea(SelectedIconfn.Current.BoundingRectangle))
                                {
                                    selection.Invoke();
                                    break;
                                }

                            }

                           // if (SelectedIconfn.Current.Name.Contains(Conf.Default.RunningWin)) //(Regex.IsMatch(SelectedIconfn.Current.Name,Conf.Default.RunningWin) || Regex.IsMatch(SelectedIconfn.Current.Name, Conf.Default.multiWin))
                           if (Regex.IsMatch(SelectedIconfn.Current.Name, Conf.Default.RunningWin) || Regex.IsMatch(SelectedIconfn.Current.Name, Conf.Default.multiWin))
                            {
                                Log.Information("Select Func: Regex search and select running app");
                                Thread.Sleep(Conf.Default.clickInterval);

                                if (CheckCurrentMouseareaWithRectArea(SelectedIconfn.Current.BoundingRectangle))
                                {
                                    selection.Invoke();
                                    break;
                                }

                               
                            }
                            else
                            {
                                Log.Information("Select Func: Regex search and focus Item");
                                SelectedIconfn.SetFocus();
                                break;

                            }



                        }
                       

                    }
                }
            }

            catch (COMException ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");

            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }


        }

        */

        // add/remove to/from start-up
        private void checkbox_Runatstart_CheckedChanged(object sender, EventArgs e)
        {

            try
            {
                if (checkbox_Runatstart.Checked)
                {

                    RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

                    if (key != null)
                    {
                        string appPath = Assembly.GetEntryAssembly().Location;
                        key.SetValue("TaskBar DragAndDrop", appPath);
                        key.Close();

                        Conf.Default.Runatstart = true;
                        Conf.Default.Save();
                        Conf.Default.Reload();
                    }

                }
                else if (!checkbox_Runatstart.Checked)
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    if (key != null)
                    {
                        key.DeleteValue("TaskBar DragAndDrop", false);
                        key.Close();
                        Conf.Default.Runatstart = false;
                        Conf.Default.Save();
                        Conf.Default.Reload();
                    }

                }
            }
            catch (COMException ex)
            {
                // Handle the specific COMException here
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }

        }


        // Auto Click checkbox listener
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


        //save changed intervals 
        private void btn_savesetting_Click(object sender, EventArgs e)
        {
            Conf.Default.clickInterval = int.Parse(txt_clickInterval.Text.ToString());
            Conf.Default.mousehookint = int.Parse(txt_mousehook.Text.ToString());
            SelectedTimer.Interval = Conf.Default.mousehookint;
            MouseIsDragging.Interval = Conf.Default.mousehookint;
            Conf.Default.Save();
            Conf.Default.Reload();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;

        }


        ///reset intervals to default
        private void btn_resetsetting_Click(object sender, EventArgs e)
        {
            Conf.Default.clickInterval = 500;
            Conf.Default.mousehookint = 5;
            SelectedTimer.Interval = 500;
            MouseIsDragging.Interval = 5;
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


        //invoke main form from notificationTray
        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {

            try
            {
                Log.Information("Invoke NotifyIcon1");
                var msbtn = MouseButtons.Left;
                if (SystemInformation.MouseButtonsSwapped) { msbtn = MouseButtons.Right; }
                if (e.Button == msbtn)
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
                        //fix 1.0.2-beta
                        this.WindowState = FormWindowState.Minimized;
                        this.Hide();
                        mainFormOpen = false;
                        ShowInTaskbar = false;


                    }

                }
            }
            catch (COMException ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
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

        private void SelectedTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                
                Thread getdesk = new Thread(() => MyScreen(out TrayhWnd));
                getdesk.Start();
                getdesk.Join();
                Log.Information("thread GetDesk");
                RECTOUT TRAY_RECTOUT = new RECTOUT();
               
                var TrayHANDLE = FindWindow(TrayhWnd, null);
               Thread getwinrect = new Thread(() => GetWindowRect(TrayHANDLE, out TRAY_RECTOUT));
                getwinrect.Start();
                getwinrect.Join();
                Log.Information("thread GetWinRect");

                Rect TRAY_rect_AREA = new Rect(TRAY_RECTOUT.Left, TRAY_RECTOUT.Top, Math.Abs(TRAY_RECTOUT.Left - TRAY_RECTOUT.Right), Math.Abs(TRAY_RECTOUT.Top - TRAY_RECTOUT.Bottom));

                if (isDragging && TRAY_rect_AREA.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 1, 1)))
                {

                    waitforFunc = false;
                    isDragging = false;
                    clicked = false;
                    Log.Warning($"Timer  Check => Mouse drag on Tray Area  X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y}");
                    //selectedIcon = null;
                    SelectedTimer.Stop();
                    return;


                }
                else
                {

                    if ( selectedIcon != null)
                    {
                        
                        if (isDragging && !selectedIcon.Current.BoundingRectangle.Contains(new Rect(ConvertDraw2system(Cursor.Position).X, ConvertDraw2system(Cursor.Position).Y, 5, 5)))
                        {
                            Log.Information("MainSearchthread 1");
                            //SearchIconAndFocusNEW(TrayhWnd, Cursor.Position);
                            Thread MainSearchthread = new Thread(() => SearchIconAndFocusNEW(TrayhWnd, Cursor.Position));
                            MainSearchthread.Start();
                            MainSearchthread.Join();
                        }
                        else
                        {

                            Log.Warning(" Timer Check => mouse on same Old Icon Again- no action just focus");
                            selectedIcon.SetFocus(); //////////////////// maybe we neeed to check if element has keyboard focus here later : DONE
                            SelectedTimer.Stop();
                            selectedIcon = null;
                           
                            waitforFunc = false;
                        }

                    }
                    else
                    {
                        if (isDragging && CheckCurrentMouseareaWithRectArea(TRAY_rect_AREA)) /// && !TRAY_rect_AREA.Contains(new Rect(ConvertDraw2system(dragStartPoint).X,     ConvertDraw2system(dragStartPoint).Y, 5, 5)))// && SelectedIcon == null )//|| !CheckCurrentMouseareaWithRectArea(SelectedIcon.Current.BoundingRectangle)))
                        {
                            Log.Information("MainSearchthread 2");
                            // If we are already dragging and the mouse is within the TRAY_rect_AREA but not within the small 5x5 rect around drag start point
                            // SearchIconAndFocus(TaskBarIconCollection, Cursor.Position, out SelectedIcon); // Search for an icon in the taskbar and focus on it
                            Thread MainSearchthread = new Thread(() => SearchIconAndFocusNEW(TrayhWnd, Cursor.Position));
                            MainSearchthread.Start();
                           MainSearchthread.Join();
                          
                            //SearchIconAndFocusNEW(TrayhWnd, Cursor.Position);
                        }
                        else
                        {
                            Log.Warning(" Timer Check => mouse out of Tray Area - no action");

                            SelectedTimer.Stop();
                            selectedIcon = null;
                            waitforFunc = false;
                        }

                    }


                }

                SelectedTimer.Stop();
                selectedIcon = null;
                waitforFunc = false;
                Log.Information("Timer Done,");

            }
            catch (COMException ex)
            {
                // Handle the specific COMException here

                // Log or display error information, or take appropriate action
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
                SelectedTimer.Stop();
                selectedIcon = null;
                waitforFunc = false;
            }

        }

        private void btn_localize_Click(object sender, EventArgs e)
        {

            var initresult = MessageBox.Show("For the initial setup, it is required to locate certain inputs.", "initial Setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (initresult == DialogResult.OK)
            {
                uint stringID = 11114;
                uint stringID2 = 11115;

                string muiFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\" + cultureInfo + "\\explorer.exe.mui";
            HERE:
                IntPtr hInstance = IntPtr.Zero;
                if (File.Exists(muiFilePath))
                {
                    hInstance = LoadLibrary(muiFilePath);
                }
                // Get the display language


                string displayLanguage = cultureInfo.DisplayName;
                int Hresult = 0;
                if (hInstance != IntPtr.Zero)
                {
                    // Define a buffer to receive the localized string
                    const int bufferSize = 1024;
                    StringBuilder buffer = new StringBuilder(bufferSize);

                    // Load the localized string
                    int stringLength = LoadString(hInstance, stringID, buffer, bufferSize);

                    if (stringLength > 0)
                    {
                        string localizedString = buffer.ToString(0, stringLength);
                        
                        Conf.Default.DisLan = cultureInfo.DisplayName;
                        string pattern = @"[–——\-\u2010\u2011\u2012\u2013\u2014]";
                        localizedString = Regex.Replace(localizedString, pattern, "-");
                        var tmparray = localizedString.Split('-');
                        string tmpstring = Regex.Replace(tmparray[1], @"\d", "");
                        

                        Conf.Default.RunningWin = tmpstring.Replace(" ","");
                        Log.Information($"First Localized String is : {Conf.Default.RunningWin}");
                        Conf.Default.Save();
                        Conf.Default.Reload();
                        Hresult++;
                    }


                    stringLength = LoadString(hInstance, stringID2, buffer, bufferSize);
                    if (stringLength > 0)
                    {
                        string localizedString = buffer.ToString(0, stringLength);
                        string pattern = @"[–——\-\u2010\u2011\u2012\u2013\u2014]";
                        localizedString = Regex.Replace(localizedString, pattern, "-");
                        var tmparray = localizedString.Split('-');
                        string tmpstring = Regex.Replace(tmparray[1], "%d", "");
                        tmpstring= tmpstring.Replace(" ", "");
                        

                        Log.Information($"Second Localized String is : {tmpstring}");
                        Conf.Default.multiWin = tmpstring;
                        Conf.Default.Save();
                        Conf.Default.Reload();
                        Hresult++;
                    }


                    if (Hresult >= 2)
                    {
                        MessageBox.Show("Done...", "initial Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Log.Warning("initial setup OK");
                    }
                    else
                    {
                        hInstance = IntPtr.Zero;
                        Log.Warning($"initial setup Failed - Hresult : {Hresult} ");
                    }



                }

                if (hInstance == IntPtr.Zero)
                {

                    var retrycan = MessageBox.Show("Failed to load the Localized .mui file.  " + Environment.NewLine + muiFilePath + Environment.NewLine + "Do you want to load it manually?", "Failed...!!", MessageBoxButtons.YesNo);
                    if (retrycan == DialogResult.Yes)
                    {

                    HERE2:
                        OpenFileDialog openFile = new OpenFileDialog();
                        openFile.FileName = "explorer.exe.mui";
                        openFile.Filter = "explorer.exe.mui|*.mui";
                        var resopen = openFile.ShowDialog();
                        if (resopen == DialogResult.OK)
                        {

                            if (openFile.FileName.Contains("explorer.exe.mui"))
                            {
                                muiFilePath = openFile.FileName;
                                goto HERE;
                            }
                            else
                            {

                                MessageBox.Show("Please select the 'explorer.exe.mui' file.", "Invalid File Selection");
                                goto HERE2;
                            }


                        }
                        else
                        {

                            MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.", "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }


                    }
                    else
                    {
                        MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.", "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }

            }

            else
            {
                Log.Warning("initial setup Failed - User Based");
                MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.", "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }



        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Conf.Default.closetotray && !myEnd)
            {
                e.Cancel = true;
                this.Hide();
                mainFormOpen = false;
            }
            else
            {
                Log.Warning("MainForm Closing..."); 
                // Unhook the mouse hook when the application exits
                UnhookWindowsHookEx(_hookID);
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

        private void btn_openLog_Click(object sender, EventArgs e)
        {
            Process.Start($"{Application.StartupPath}\\logs\\");
        }

        private void ntf_logfile_Click(object sender, EventArgs e)
        {
            Process.Start($"{Application.StartupPath}\\logs\\");
        }

        private void ntf_gamemode_Click(object sender, EventArgs e)
        {
            if (ntf_gamemode.Text== "Pause Mouse Hook")
            {
                try
                {
                    Log.Warning("Temp Pause Hook");
                    UnhookWindowsHookEx(_hookID);
                    ntf_gamemode.Text = " Resume Mouse Hook";
                    ntf_gamemode.Checked = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed, You May Restart The App.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log.Error($"Failed to pause Hook : {ex.ToString()}");
                }
            }
            else
            {

                try
                {
                    Log.Warning("Resume Pause Hook");

                    // Define the callback function for the mouse hook
                    _mouseProc = HookCallback;

                    // Set up the low-level mouse hook
                    _hookID = SetHook(_mouseProc);
                    ntf_gamemode.Text = "Pause Mouse Hook";
                    ntf_gamemode.Checked = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed, You May Restart The App.", "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log.Error($"Failed to pause Hook : {ex.ToString()}");
                }
               
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

        }

        private void chekbox_log_CheckedChanged(object sender, EventArgs e)
        {
            if (chekbox_log.Checked)
            {

                Conf.Default.showlog = true;
                Conf.Default.Save();
                Conf.Default.Reload();


            }
            else
            {

                Conf.Default.showlog = false;
                Conf.Default.Save();
                Conf.Default.Reload();


            }
         
        }

        // Method to set up the mouse hook
        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (ProcessModule module = Process.GetCurrentProcess().MainModule)
            {
                // Set up the low-level mouse hook using SetWindowsHookEx
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        // Callback function for the mouse hook




        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                int winMsg = (int)wParam;
                MSLLHOOKSTRUCT mouseInfo = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if (nCode >= 0)
                {

                    switch (winMsg)
                    {

                        case 514: // Mouse Up
                            HandleMouseUp(mouseInfo);
                            break;
                        case 513: // Mouse Down
                            HandleMouseDown(mouseInfo);
                            break;

                        case 512: // Mouse Move
                            HandleMouseMove(mouseInfo);
                            break;


                    }
                }
            }
            catch (COMException ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void HandleMouseUp(MSLLHOOKSTRUCT mouseInfo)
        {
            clicked = false;
            isDragging = false;
            waitforFunc = false;
            SelectedTimer.Stop();
            selectedIcon = null;
            Log.Warning("Mouse Up: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
        }

        private void HandleMouseMove(MSLLHOOKSTRUCT mouseInfo)
        {
            try
            {
               /* Rect tmprect;
                if (selectedIcon != null)
                {
                    tmprect = selectedIcon.Current.BoundingRectangle;
                }
                else
                {
                    tmprect = new Rect(0, 0, 1, 1);
                }*/

                if (clicked && !waitforFunc )//&& !tmprect.Contains(Cursor.Position.X,Cursor.Position.Y))
                {
                    //ResetTimerAndStartDragging(mouseInfo);

                    SelectedTimer.Stop();
                    SelectedTimer.Start();
                    
                    isDragging = true;
                    Log.Warning("Dragging: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
                    waitforFunc = true;
                   
                }
            }
            catch (COMException ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }
        }

        private static void HandleMouseDown(MSLLHOOKSTRUCT mouseInfo)
        {
            dragStartPoint.X = mouseInfo.pt.x;
            dragStartPoint.Y = mouseInfo.pt.y;
            clicked = true;
            Log.Warning("Mouse Down: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
        }



    }

   
}
