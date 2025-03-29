using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Xml;
using Autofac;
using Microsoft.Win32;
using Serilog;
using System.Threading.Tasks;
using TaskBarDragAndDrop;
using Point = System.Drawing.Point;
using Autofac.Extras.DynamicProxy;
using TaskBarDragAndDropNoUAC.LoggingCS;
namespace TaskBarDragAndDropNoUAC
{
    public partial class MainForm : Form, IMainForm
    {
        private const int WH_MOUSE_LL = 14; // Hook type for low-level mouse hook
        public List<string> explorerMui;
       // private IMainForm ImainForm;
        private static bool isDragging, clicked, waitforFunc;
        private static Point dragStartPoint = new Point(0, 0);
        private static readonly Mutex mutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC)");
        private static readonly Mutex aboutMutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC) AboutBoxMutex");

        public static AutomationElement selectedIcon;
        private static LowLevelMouseProc _mouseProc;
        private static IntPtr _hookID = IntPtr.Zero;
        public bool aboutBoxOpen, mainFormOpen, myEnd;
        private readonly AboutBox1 aboutForm = new AboutBox1();
        private readonly CultureInfo cultureInfo = CultureInfo.CurrentUICulture;
        private string TrayhWnd = "Shell_TrayWnd"; //default name


        public static IContainer ConfigureAutofac()
        {
            var builder = new ContainerBuilder();

            // Register the logger
            builder.RegisterInstance(Log.Logger).As<ILogger>();

            // Register the logging interceptor
            builder.RegisterType<LoggingInterceptor>();

            // Register MainForm with interception (both class and interface)
            builder.RegisterType<MainForm>()
                .AsSelf()
                .As<IMainForm>()
                .EnableClassInterceptors()
                .EnableInterfaceInterceptors()
                .InterceptedBy(typeof(LoggingInterceptor));

            // Register other public classes with interception, excluding AboutBox1 and Resources
            var assembly = Assembly.GetExecutingAssembly();

            builder.RegisterAssemblyTypes(assembly)
                .Where(type => type.IsClass && type.IsPublic && !type.IsAbstract && !type.IsSealed && type != typeof(LoggingInterceptor) && type != typeof(AboutBox1) && type != typeof(TaskBarDragAndDrop.Properties.Resources))
                .AsSelf()
                .EnableClassInterceptors()
                .InterceptedBy(typeof(LoggingInterceptor));

            // Register public interfaces with interception
            builder.RegisterAssemblyTypes(assembly)
                .Where(type => type.IsInterface && type.IsPublic)
                .EnableInterfaceInterceptors()
                .InterceptedBy(typeof(LoggingInterceptor));

            return builder.Build();
        }

        public static List<string> ExtractAllExplorerMuiFiles(string xmlFilePath)
        {
            List<string> explorerMuiFiles = new List<string>();

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(xmlFilePath);

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("bm", "http://schemas.microsoft.com/appx/2010/blockmap");

                XmlNodeList fileNodes = doc.SelectNodes("//bm:File", nsmgr);

                if (fileNodes != null)
                {
                    foreach (XmlNode fileNode in fileNodes)
                    {
                        if (fileNode.Attributes["Name"] != null)
                        {
                            string fileName = fileNode.Attributes["Name"].Value;

                            if (fileName.Contains("explorer.exe.mui"))
                            {
                                explorerMuiFiles.Add(fileName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return explorerMuiFiles;
        }
        public static string[] GetLanguagePackInstallLocations()
        {
            string command = "Get-AppxPackage -AllUsers *LanguageExperiencePack* | Select-Object -ExpandProperty InstallLocation";
            string output = RunPowerShellCommand(command);

            if (string.IsNullOrEmpty(output))
            {
                return Array.Empty<string>();
            }

            return output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        }


        public static string RunPowerShellCommand(string command)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        return "";
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return "";
            }
        }
        public  MainForm()
        {
            Log.Information("InitializeComponent();");
            InitializeComponent();
        }
        public new void Show()
        {
            base.Show();
        }

        public new void Hide()
        {
            base.Hide();
        }

        // DLL imports and Global Vars
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int LoadString(IntPtr hInstance, uint uId, [Out] StringBuilder lpBuffer, int nBufferMax);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECTOUT lpRect);

        // About
        private void ShowAboutPage()
        {
            if (aboutMutex.WaitOne(TimeSpan.Zero, true) && !aboutBoxOpen)
            {
                aboutBoxOpen = true;
                aboutForm.FormClosed += (s, ev) => aboutBoxOpen = false;
                aboutForm.ShowDialog();
                aboutMutex.ReleaseMutex();
            }
            else
            {
                aboutForm.Select();
            }
        }

        // Convert system.windows.Points to system.Draw.Point
        public System.Windows.Point ConvertDraw2system(Point Draw2sys)
        {
            var Drawsystem = new System.Windows.Point(int.Parse(Draw2sys.X.ToString()), int.Parse(Draw2sys.Y.ToString()));
            return Drawsystem;
        }

        // Check if mouse is in a specific area
        private bool CheckCurrentMouseareaWithRectArea(Rect showme)
        {
            var SystemWindowsCursorPoint = ConvertDraw2system(Cursor.Position);
            return showme.Contains(SystemWindowsCursorPoint);
        }

        // MainForm Load event


        private void MainForm_Load(object sender, EventArgs e)
        {
            
                /* if (!Directory.Exists($"{Application.StartupPath}\\logs\\"))
                     Directory.CreateDirectory($"{Application.StartupPath}\\logs\\");

                 if (Conf.Default.showlog)
                 {
                     AllocConsole();
                     Log.Logger = new LoggerConfiguration()
                         .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
                         .WriteTo.Async(a => a.File($"{Application.StartupPath}\\logs\\TaskBarDrag&drop.log",
                             rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8))
                         .MinimumLevel.Verbose()
                         .CreateLogger();


                     var container = ConfigureAutofac();
                  //  var  service = container.Resolve<IMainForm>();



                     Log.Information($" Log Path : {Application.StartupPath}\\logs\\TaskBarDrag&drop.log");
                 }*/

                if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();
            }
            else
            {
                Log.Error("Another App Found- Exit...");
                MessageBox.Show("Another instance of the application is already running.", "Application Running",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                myEnd = true;
                Close();
            }

            try
            {
                Log.Information("load settings");
                chekbox_log.Checked = Conf.Default.showlog;
                MouseIsDragging.Interval = Conf.Default.mousehookint;
                SelectedTimer.Interval = Conf.Default.clickInterval;
                checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
                checkbox_closeTray.Checked = Conf.Default.closetotray;
                checkbox_Runatstart.Checked = Conf.Default.Runatstart;
                txt_mousehook.Text = Conf.Default.mousehookint.ToString();
                txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
                btn_resetsetting.Visible = false;
                Log.Information($"clickInterval: {Conf.Default.clickInterval}");
                btn_savesetting.Visible = false;
                ShowInTaskbar = false;

                if (Conf.Default.DisLan == null || Conf.Default.DisLan != cultureInfo.DisplayName)
                {
                    notifyIcon1_MouseClick(sender, new MouseEventArgs(MouseButtons.Left, 2, 0, 0, 0));
                    btn_localize_Click(sender, e);
                }

                _mouseProc = HookCallback;
                _hookID = SetHook(_mouseProc);
                Log.Information($"Hook ID = {_hookID}");
                
                Log.Information(
                    $"Log Window {Environment.NewLine} {aboutForm.AssemblyProduct} {Assembly.GetExecutingAssembly().GetName().Version}: {Environment.NewLine} Current Language Pack: {cultureInfo} {DateTime.Now} {Environment.NewLine} Initial Setup Strings: '{Conf.Default.RunningWin}' And '{Conf.Default.multiWin}'");
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

        // Check for multi-screen and active screen
        private string MyScreen(out string hWsc)
        {
            var screen = Screen.FromPoint(Cursor.Position);
            hWsc = screen.Primary ? "Shell_TrayWnd" : "Shell_SecondaryTrayWnd";
            Log.Information($"My Screen FN => Display: {hWsc}");
            return hWsc;
        }

        // Search for an icon and focus on it
        public bool SearchIconAndFocusNEW(string trayClassName, Point cursorPnt)
        {
            SelectedTimer.Stop();
            Log.Information("Search For Icon And Focus NEW FN ");

            try
            {
                if (selectedIcon != null)
                {
                    if (CheckCurrentMouseareaWithRectArea(selectedIcon.Current.BoundingRectangle))
                    {
                        Log.Warning(
                            $"Mouse is over the Last Selected Icon  - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} Area : {selectedIcon.Current.BoundingRectangle} - Exit Search Function");
                        waitforFunc = true;
                        SelectedTimer.Start();
                        //selectedIcon = null;
                        return true;
                    }
                }
                var searchArea = new Rect(cursorPnt.X, cursorPnt.Y, 1, 1);
                var taskbar = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, trayClassName));

                if (taskbar == null)
                {
                    Log.Warning(
                        $"TaskBar  is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y}, Display: {trayClassName} ,Search Area : {searchArea} - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    return false;
                }

                Condition condition = new PropertyCondition(AutomationElement.ClassNameProperty,
                    "Taskbar.TaskListButtonAutomationPeer");

                var taskbarElement = taskbar.FindFirst(TreeScope.Descendants, condition);
                if (taskbarElement == null)
                {
                    Log.Warning(
                        $"TaskBar Elements list Is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} ,  Display: {trayClassName} - Exit Search Function");
                    waitforFunc = false;
                    SelectedTimer.Start();
                    return false;
                }

                var walker = TreeWalker.ControlViewWalker;

                while (taskbarElement != null &&
                       !taskbarElement.Current.BoundingRectangle.Contains(
                           new System.Windows.Point(Cursor.Position.X, Cursor.Position.Y)))
                    taskbarElement = walker.GetNextSibling(taskbarElement);

                if (taskbarElement == null)
                {
                    Log.Warning(
                        $"Taskbar Icon is Null - Mouse X:  {cursorPnt.X} , Mouse Y:  {cursorPnt.Y} ,    Display: {trayClassName}  - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    return false;
                }

                if (Conf.Default.ClickPinApp)
                {
                    Thread.Sleep(Conf.Default.clickInterval);
                    var invokePattern = taskbarElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();

                    Log.Information($"Auto Click {taskbarElement.Current.Name}, done");
                    waitforFunc = false;
                    SelectedTimer.Stop();
                }
                else if (!Conf.Default.ClickPinApp &&
                         (taskbarElement.Current.Name.Replace(" ", "").Contains(Conf.Default.RunningWin) ||
                          taskbarElement.Current.Name.Replace(" ", "").Contains(Conf.Default.multiWin)))
                {
                    Thread.Sleep(Conf.Default.clickInterval);
                    var invokePattern = taskbarElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();
                    Log.Information($"Running Window Click : {taskbarElement.Current.Name}, done");

                    waitforFunc = false;
                    SelectedTimer.Stop();
                }
                else
                {

                    Log.Information($"just Focus- no running Window: {taskbarElement.Current.Name}");
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

        // Add/remove to/from start-up
        private void checkbox_Runatstart_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (checkbox_Runatstart.Checked)
                {
                    var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                    if (key != null)
                    {
                        var appPath = Assembly.GetEntryAssembly().Location;
                        key.SetValue("TaskBar DragAndDrop", appPath);
                        key.Close();

                        Conf.Default.Runatstart = true;
                        Conf.Default.Save();
                        Conf.Default.Reload();
                    }
                }
                else if (!checkbox_Runatstart.Checked)
                {
                    var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
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
                Log.Fatal($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
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

        // Save changed intervals
        private void btn_savesetting_Click(object sender, EventArgs e)
        {
            Conf.Default.clickInterval = int.Parse(txt_clickInterval.Text);
            Conf.Default.mousehookint = int.Parse(txt_mousehook.Text);
            SelectedTimer.Interval = Conf.Default.mousehookint;
            MouseIsDragging.Interval = Conf.Default.mousehookint;
            Conf.Default.Save();
            Conf.Default.Reload();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;
        }

        // Reset intervals to default
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
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
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
            toolTip1.Show(
                "Drag and drop icons created by Freepik" + Environment.NewLine + "- Flaticon ( Click To Open Website)",
                pictureBox1);
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

        // Invoke main form from notificationTray
        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                Log.Information("Invoke NotifyIcon1");
                var msbtn = MouseButtons.Left;
                if (SystemInformation.MouseButtonsSwapped) msbtn = MouseButtons.Right;
                if (e.Button == msbtn)
                {
                    if (!mainFormOpen)
                    {
                        Show();
                        WindowState = FormWindowState.Normal;
                        mainFormOpen = true;
                        ShowInTaskbar = true;
                    }
                    else
                    {
                        aboutForm.Close();
                        WindowState = FormWindowState.Minimized;
                        Hide();
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
            Close();
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

        private  void SelectedTimer_Tick(object sender, EventArgs e)
        {
            try
            {

                Thread getdesk = new Thread(() => MyScreen(out TrayhWnd));
                getdesk.Start();
                getdesk.Join();
                Log.Information($"thread GetDesk : {TrayhWnd}");
                RECTOUT TRAY_RECTOUT = new RECTOUT();

                var TrayHANDLE = FindWindow(TrayhWnd, null);
                Log.Information($"thread TrayHANDLE : {TrayHANDLE}");
                Thread getwinrect = new Thread(() => GetWindowRect(TrayHANDLE, out TRAY_RECTOUT));
                getwinrect.Start();
                getwinrect.Join();
                Log.Information($"thread GetWinRect : {TRAY_RECTOUT}");

                Rect TRAY_rect_AREA = new Rect(TRAY_RECTOUT.Left, TRAY_RECTOUT.Top, Math.Abs(TRAY_RECTOUT.Left - TRAY_RECTOUT.Right), Math.Abs(TRAY_RECTOUT.Top - TRAY_RECTOUT.Bottom));
                Log.Information($"TRAY_rect_AREA : {TRAY_rect_AREA}");

                if (isDragging && TRAY_rect_AREA.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 1, 1)))
                {

                    waitforFunc = false;
                    isDragging = false;
                    clicked = false;
                    Log.Warning($"Timer  Check => Mouse drag on Tray Area  X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y}");
                    selectedIcon = null;
                    SelectedTimer.Stop();
                    return;


                }
                else
                {

                    if (selectedIcon != null)
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
                           // selectedIcon = null;

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
                            //selectedIcon = null;
                            waitforFunc = false;
                        }

                    }


                }

                SelectedTimer.Stop();
               // selectedIcon = null;
                waitforFunc = false;
                Log.Information("Timer Done,");

            }
            catch (COMException ex)
            {
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
            var initresult = MessageBox.Show("For the initial setup, it is required to locate certain inputs.",
                "initial Setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (initresult == DialogResult.OK)
            {
                string[] result = GetLanguagePackInstallLocations();
                int lst = 0;
                string localisationpath = "";
                foreach (string output in result)
                {
                    if (output.Contains(cultureInfo.ToString()))
                    {
                        localisationpath = output;
                        break;
                    }
                }
                if (File.Exists(localisationpath + "\\AppxBlockMap.xml"))
                {
                    explorerMui = ExtractAllExplorerMuiFiles(localisationpath + "\\AppxBlockMap.xml");
                    if (explorerMui != null)
                    {
                        Console.WriteLine($"\nexplorer.exe.mui: {explorerMui.Count}");
                    }
                    else
                    {
                        Console.WriteLine("\nexplorer.exe.mui not found.");
                    }
                }
                uint stringID = 11114;
                uint stringID2 = 11115;

            HERE:
                string filelist = explorerMui[lst];
                var muiFilePath = $"{localisationpath}\\{filelist}";
                var hInstance = IntPtr.Zero;
            manualload:
                if (File.Exists(muiFilePath))
                {
                    hInstance = LoadLibrary(muiFilePath);
                }
                else
                {
                    return;
                }

                var Hresult = 0;
                if (hInstance != IntPtr.Zero)
                {
                    const int bufferSize = 1024;
                    var buffer = new StringBuilder(bufferSize);
                    var stringLength = LoadString(hInstance, stringID, buffer, bufferSize);

                    if (stringLength > 0)
                    {
                        var localizedString = buffer.ToString(0, stringLength);
                        Conf.Default.DisLan = cultureInfo.DisplayName;
                        var pattern = @"[–——\-\u2010\u2011\u2012\u2013\u2014]";
                        localizedString = Regex.Replace(localizedString, pattern, "-");
                        var tmparray = localizedString.Split('-');
                        var tmpstring = Regex.Replace(tmparray[1], @"\d", "");
                        Conf.Default.RunningWin = tmpstring.Replace(" ", "");
                        Log.Information($"First Localized String is : {Conf.Default.RunningWin}");
                        Conf.Default.Save();
                        Conf.Default.Reload();
                        Hresult++;
                    }
                    else
                    {
                        lst++;
                        if (File.Exists($"{localisationpath}\\{explorerMui[lst]}"))
                        {
                            goto HERE;
                        }
                    }

                    stringLength = LoadString(hInstance, stringID2, buffer, bufferSize);
                    if (stringLength > 0)
                    {
                        var localizedString = buffer.ToString(0, stringLength);
                        var pattern = @"[–——\-\u2010\u2011\u2012\u2013\u2014]";
                        localizedString = Regex.Replace(localizedString, pattern, "-");
                        var tmparray = localizedString.Split('-');
                        var tmpstring = Regex.Replace(tmparray[1], "%d", "");
                        tmpstring = tmpstring.Replace(" ", "");
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

                //if (hInstance == IntPtr.Zero)
                else if(hInstance == IntPtr.Zero)
                {
                    var retrycan = MessageBox.Show(
                        "Failed to load the Localized .mui file.  " + Environment.NewLine + muiFilePath +
                        Environment.NewLine + "Do you want to load it manually?", "Failed...!!",
                        MessageBoxButtons.YesNo);
                    if (retrycan == DialogResult.Yes)
                    {
                    HERE2:
                        var openFile = new OpenFileDialog();
                        openFile.FileName = "explorer.exe.mui";
                        openFile.Filter = "explorer.exe.mui|*.mui";
                        var resopen = openFile.ShowDialog();
                        if (resopen == DialogResult.OK)
                        {
                            if (openFile.FileName.Contains("explorer.exe.mui"))
                            {
                                muiFilePath = openFile.FileName;
                                goto manualload;
                            }

                            MessageBox.Show("Please select the 'explorer.exe.mui' file.", "Invalid File Selection");
                            goto HERE2;
                        }

                        MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.",
                            "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    else
                    {
                        MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.",
                            "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
            else
            {
                Log.Warning("initial setup Failed - User Based");
                MessageBox.Show("If the app isn't working, you can perform this initial setup at a later time.",
                    "Canceling..!!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Conf.Default.closetotray && !myEnd)
            {
                e.Cancel = true;
                Hide();
                mainFormOpen = false;
            }
            else
            {
                Log.Warning("MainForm Closing...");
                UnhookWindowsHookEx(_hookID);
            }
        }

        private void ntf_settings_Click(object sender, EventArgs e)
        {
            if (!mainFormOpen)
            {
                Show();
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
            if (ntf_gamemode.Text == "Pause Mouse Hook")
                try
                {
                    Log.Warning("Temp Pause Hook");
                    UnhookWindowsHookEx(_hookID);
                    Log.Information($"Hook ID : {_hookID}");
                    ntf_gamemode.Text = " Resume Mouse Hook";
                    ntf_gamemode.Checked = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed, You May Restart The App.", "Failed", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Log.Error($"Failed to pause Hook : {ex}");
                }
            else
                try
                {
                    Log.Warning("Resume Pause Hook");
                    _mouseProc = HookCallback;
                    _hookID = SetHook(_mouseProc);
                    Log.Information($"Hook ID : {_hookID}");
                    ntf_gamemode.Text = "Pause Mouse Hook";
                    ntf_gamemode.Checked = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed, You May Restart The App.", "Failed", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    Log.Error($"Failed to pause Hook : {ex}");
                }
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
            using (var module = Process.GetCurrentProcess().MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        // Callback function for the mouse hook
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                var winMsg = (int)wParam;
                var mouseInfo = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if (nCode >= 0)
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
                if (clicked && !waitforFunc)
                {
                    var deltaX = mouseInfo.pt.x - dragStartPoint.X;
                    var deltaY = mouseInfo.pt.y - dragStartPoint.Y;
                    var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                    SelectedTimer.Stop();
                    SelectedTimer.Start();
                    Log.Information($"Mouse Moved for : {distance}{Environment.NewLine}");
                    if (distance >= 10)
                    {
                        isDragging = true;
                        Log.Warning("Dragging: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
                    }
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

        private void MouseIsDragging_Tick(object sender, EventArgs e)
        {
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            Hide();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECTOUT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // Define the delegate for the mouse hook procedure
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

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
    }
}