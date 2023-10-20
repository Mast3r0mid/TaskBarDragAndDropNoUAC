using System;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using TaskBarDragAndDrop;
using System.Globalization;
using System.IO;
using System.Text;


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
        private static bool isDragging,clicked , focused ,waitforFunc = false;
        private static System.Drawing.Point dragStartPoint = new System.Drawing.Point(0,0);
        
        private String TrayhWnd = "Shell_TrayWnd"; //default name
        static readonly Mutex mutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC)");
        static readonly Mutex aboutMutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC) AboutBoxMutex");
        
        public bool aboutBoxOpen, mainFormOpen, myEnd = false;
        AboutBox1 aboutForm = new AboutBox1();
        CultureInfo cultureInfo = CultureInfo.CurrentUICulture;
        StreamWriter fileWriter;
        public static AutomationElement DesktopRootElement , selectedIcon = null;
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

        // Main Function to find taskBar UI Elements
        public void FindTaskBarIcons(string DesktophWnd, out AutomationElement DesktopRootElementfn , out AutomationElementCollection TaskBarIconCollectionfn, out Rect ItemsRectAreafn)
        {
            
            DesktopRootElementfn = null; // Initialize to null
            TaskBarIconCollectionfn = null; // Initialize to null
            ItemsRectAreafn = Rect.Empty;
            try
            {


                PropertyCondition classNameCondition = new PropertyCondition(AutomationElement.ClassNameProperty, DesktophWnd);

                // Search for the main window of the target application
                DesktopRootElementfn = AutomationElement.RootElement.FindFirst(System.Windows.Automation.TreeScope.Children, classNameCondition);

                if (DesktopRootElementfn != null)
                {
                    DesktopRootElement = DesktopRootElementfn;

                    PropertyCondition taskbarelementsCondition = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");

                    // Find all Icons in the Taskbar by its class name
                    TaskBarIconCollectionfn = DesktopRootElement.FindAll(System.Windows.Automation.TreeScope.Descendants, taskbarelementsCondition);
                    double RectSize = 0;


                    ///fix 1.0.6 difrent windows langiage pack and RTL
                    ///

                    int minX = int.MaxValue;
                    int minY = int.MaxValue;
                    int maxX = int.MinValue;
                    int maxY = int.MinValue;


                    foreach (AutomationElement item in TaskBarIconCollectionfn)
                    {
                        if (item.Current.AutomationId.Contains("Appid:") || item.Current.AutomationId.Contains("Window:"))
                        {
                            if (((int)item.Current.BoundingRectangle.Left) < minX)
                                minX = (int)item.Current.BoundingRectangle.Left;
                            if ((int)item.Current.BoundingRectangle.Top < minY)
                                minY = (int)item.Current.BoundingRectangle.Top;
                            if ((int)item.Current.BoundingRectangle.Right > maxX)
                                maxX = (int)item.Current.BoundingRectangle.Right;
                            if ((int)item.Current.BoundingRectangle.Bottom > maxY)
                                maxY = (int)item.Current.BoundingRectangle.Bottom;
                            RectSize += item.Current.BoundingRectangle.Width;
                        }
                    }

                     ItemsRectAreafn = new Rect(minX, minY, maxX - minX, maxY - minY);

                   // ItemsRectAreafn = new Rect(TaskBarIconCollectionfn[0].Current.BoundingRectangle.X, TaskBarIconCollectionfn[0].Current.BoundingRectangle.Y, RectSize, 48);
                    TaskBarIconCollection = TaskBarIconCollectionfn;

                }
            }
            catch (COMException ex)
            {
                // Handle the specific COMException here
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
                // MessageBox.Show( ex.Message + Environment.NewLine + ex.StackTrace +Environment.NewLine + ex.Source);
            }

        }




        private void MainForm_Load(object sender, EventArgs e)
        {
            if (Conf.Default.showlog)
            {
                AllocConsole();
                // Create a StreamWriter to write to a file

                // Create a TextWriter to capture the console output
                StreamWriter fileWriter = new StreamWriter($"TaskBar Drag&drop {DateTime.Now.ToString().Replace("/", "-").Replace(":", "-")}.log");
                var dualWriter = new DualTextWriter(Console.Out, fileWriter);
                Console.SetOut(dualWriter);

            }
            else
            {

            }
            // Specify properties to identify the target application's main window by class name
            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                mutex.ReleaseMutex();
                

            }
            else
            {
                Console.WriteLine("Another App Found- Exit...");
                MessageBox.Show("Another instance of the application is already running.", "Application Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
                myEnd = true;
                this.Close();

            }

            try
            {

                chekbox_log.Checked = Conf.Default.showlog;
                MouseIsDragging.Interval = Conf.Default.mousehookint;
                SelectedTimer.Interval = Conf.Default.mousehookint;
                checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
                checkbox_closeTray.Checked = Conf.Default.closetotray;
                checkbox_Runatstart.Checked = Conf.Default.Runatstart;
                txt_mousehook.Text = Conf.Default.mousehookint.ToString();
                txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
                btn_resetsetting.Visible = false;
                SelectedTimer.Interval = 500;
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
               
                    
                Console.WriteLine($"Log Window {Environment.NewLine} {aboutForm.AssemblyProduct} {Assembly.GetExecutingAssembly().GetName().Version.ToString()}: {Environment.NewLine} Current Language Pack: {cultureInfo} {DateTime.Now} {Environment.NewLine} Initial Setup String: {Conf.Default.RunningWin.ToString()}");

            }
            catch (COMException ex) {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }


        }



        //check for Multi-scren and active screen
        private string MyScreen()
        {
            string hWsc;
            Screen screen = Screen.FromPoint(Cursor.Position);
            if (screen.Primary) // use active desktop
            {
                hWsc = "Shell_TrayWnd";

            }
            else
            {
                hWsc = "Shell_SecondaryTrayWnd";

            }
            Console.WriteLine($"Display: {hWsc}");

            return hWsc;
        }


        //beta 1.0.7 awesome new function
        // best resouce manager and speed and small function
        public bool SearchIconAndFocusNEW(String trayClassName, System.Drawing.Point cursorPnt)
        {
            SelectedTimer.Stop();
            Console.WriteLine($"Search For Icon And Focus NEW FN " );

            try
            {
                if (selectedIcon != null)
                {
                    if (CheckCurrentMouseareaWithRectArea(selectedIcon.Current.BoundingRectangle))
                    {
                        StackTrace stackTrace = new StackTrace(true);
                        StackFrame frame = stackTrace.GetFrame(0);
                        int lineNumber = frame.GetFileLineNumber();
                        string callerMethod = frame.GetMethod().Name;
                        Console.WriteLine($"Mouse is On Last Selected Icon  - Mouse X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y} ,   Method: {callerMethod}, Line: {lineNumber} ,{selectedIcon.Current.BoundingRectangle.ToString()} - Exit Search Function");
                        waitforFunc = false;
                        SelectedTimer.Start();
                        return true;
                    }

                }
                Rect searchArea = new Rect(cursorPnt.X, cursorPnt.Y, 1, 1);
                AutomationElement taskbar = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, trayClassName));

                if (taskbar == null)
                {
                    StackTrace stackTrace = new StackTrace(true);
                    StackFrame frame = stackTrace.GetFrame(0);
                    int lineNumber = frame.GetFileLineNumber();
                    string callerMethod = frame.GetMethod().Name;
                    Console.WriteLine($"TRAY  IS Null - Mouse X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y} ,   Method: {callerMethod}, Line: {lineNumber} , Display: {trayClassName} ,Search Area : {searchArea.ToString()} - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    return false;
                }


                Condition condition = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");

                AutomationElement taskbarElement = taskbar.FindFirst(TreeScope.Descendants, condition);
                if (taskbarElement == null)
                {
                    StackTrace stackTrace = new StackTrace(true);
                    StackFrame frame = stackTrace.GetFrame(0);
                    int lineNumber = frame.GetFileLineNumber();
                    string callerMethod = frame.GetMethod().Name;
                    Console.WriteLine($"TaskBar ICONS Is Null - Mouse X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y} ,   Method: {callerMethod}, Line: {lineNumber} , Display: {trayClassName} - Exit Search Function");
                    waitforFunc = true;
                    SelectedTimer.Start();
                    return false;
                }
                TreeWalker walker = TreeWalker.ControlViewWalker;
                AutomationElement nextElement = walker.GetNextSibling(taskbarElement);
                while (nextElement != null && !nextElement.Current.BoundingRectangle.Contains(new System.Windows.Point(cursorPnt.X, cursorPnt.Y)))
                {
                    nextElement = walker.GetNextSibling(nextElement);
                }
                if (nextElement == null)
                {
                    StackTrace stackTrace = new StackTrace(true);
                    StackFrame frame = stackTrace.GetFrame(0);
                    int lineNumber = frame.GetFileLineNumber();
                    string callerMethod = frame.GetMethod().Name;
                    Console.WriteLine($"NextElemnt IS Null - Mouse X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y} ,   Method: {callerMethod}, Line: {lineNumber} , Display: {trayClassName} - Exit Search Function");
                    SelectedTimer.Start();
                    waitforFunc = true;
                    return false;
                }

                if (Conf.Default.ClickPinApp)
                {
                    Thread.Sleep(Conf.Default.clickInterval);
                    InvokePattern invokePattern = nextElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();

                    Console.WriteLine("auto Click");
                    waitforFunc = false;

                }

                else if (!Conf.Default.ClickPinApp && nextElement.Current.Name.Contains(Conf.Default.RunningWin))
                {
                    Thread.Sleep(Conf.Default.clickInterval);
                    InvokePattern invokePattern = nextElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
                    invokePattern.Invoke();
                    Console.WriteLine("Running Window Click");
                    waitforFunc = false;
                }
                else
                {

                    Console.WriteLine("just Focus- no running Window");
                    waitforFunc = false;
                    nextElement.SetFocus();
                }
                selectedIcon = nextElement;
            }
           
            catch (COMException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }



            return true;
        }



        /// Main Function : select Element and invoke or Focus Based on mouse position
        /// 
        /// OLD FUNCTION/ big bugy  high resource usage function
        public void SearchIconAndFocus(AutomationElementCollection TaskbarItemCollectinHolder, System.Drawing.Point MousePos, out AutomationElement SelectedIconfn)
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
                            if (Conf.Default.ClickPinApp ) //&& !SelectedIconfn.Current.Name.Contains("running window"))
                            {
                                Thread.Sleep(Conf.Default.clickInterval);
                                if (CheckCurrentMouseareaWithRectArea(SelectedIconfn.Current.BoundingRectangle))
                                {
                                    selection.Invoke();
                                    break;
                                }

                            }

                             if (SelectedIconfn.Current.Name.Contains(Conf.Default.RunningWin))
                            {

                                Thread.Sleep(Conf.Default.clickInterval);
                                
                                if (CheckCurrentMouseareaWithRectArea(SelectedIconfn.Current.BoundingRectangle))
                                {
                                    selection.Invoke();
                                     break;
                                }

                                //fix 1.0.5 // change this function to invoke running apps and just set focus on non-running one 
                                //you can choose to run pinned app automatically


                                /* // Simulate Pressing the Win+T key
                                 keybd_event(VK_LWIN, 0, KEYEVENTF_KEYDOWN, 0);
                                 Thread.Sleep(1);
                                 keybd_event(VK_T, 0, KEYEVENTF_KEYDOWN, 0);
                                 Thread.Sleep(1);
                                 // Simulate releasing the Win+T key
                                 keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);
                                 Thread.Sleep(1);
                                 keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);


                                 // SetForegroundWindow((IntPtr)SelectedIcon.Current.NativeWindowHandle);
                                 while (!SelectedIcon.Current.HasKeyboardFocus)
                                 {




                                     if (SelectedIcon.Current.BoundingRectangle.Left > oldpoint.X)
                                     {

                                         keybd_event(VK_T, 0,KEYEVENTF_KEYDOWN,0);
                                         Thread.Sleep(1);
                                         keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);


                                     }
                                     double aaa = ConvertDraw2system(Cursor.Position).X;

                                     if (SelectedIcon.Current.BoundingRectangle.Left + 1 < oldpoint.Y)
                                     {
                                         /*  // Simulate Pressing the Win+SHIFT+T key
                                           keybd_event(VK_LWIN, 0, KEYEVENTF_KEYDOWN, 0);
                                           Thread.Sleep(1);
                                           keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, 0);
                                           Thread.Sleep(1);
                                           keybd_event(VK_T, 0, KEYEVENTF_KEYDOWN, 0);
                                           Thread.Sleep(1);

                                           // Simulate releasing the Win+SHIFT+T key
                                           keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);
                                           Thread.Sleep(1);
                                           keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                                           Thread.Sleep(1);
                                           keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
                                         if (GetAsyncKeyState((int)Keys.ShiftKey) != KEYEVENTF_KEYDOWN)
                                         {
                                             keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYDOWN, 0);
                                         }

                                         keybd_event(VK_T, 0, KEYEVENTF_KEYDOWN, 0);
                                         Thread.Sleep(1);
                                         keybd_event(VK_T, 0, KEYEVENTF_KEYUP, 0);*/



                                //AutomationElement element = /* Your UI Automation element */;

                                // Get the HoverPattern



                                // }




                                // }
                                // keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
                                // keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);

                                //  oldpoint.X = SelectedIcon.Current.BoundingRectangle.Left;
                                // oldpoint.Y = SelectedIcon.Current.BoundingRectangle.Right;
                            }
                            else
                            {

                                // Automation.AddAutomationEventHandler(AutomationElement.AutomationFocusChangedEvent,SelectedIcon,TreeScope.Element, (sender, e) =>
                                // {
                                // Create and display the custom popup form
                                // MessageBox.Show("hi");
                                // });
                                // notifyIcon1.Text=" NO Runnng App..." + Environment.NewLine + " Need Auto Run ??!!";
                                // notifyIcon1.ShowBalloonTip(1, "No Runnng Window...", "Need Auto Run ??!!", ToolTipIcon.Info);

                                SelectedIconfn.SetFocus();
                                break;

                            }



                        }
                            //break;

                        }
                    }
                }
            
            catch (COMException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }


        }


        // Main Function : mouse move and click check //OLD FUNC
        private async void MouseIsDragging_Tick(object sender, EventArgs e)
        { 
            //chech if LeftBtn is  pressed (  drag or Start dragging)
           // if (SystemInformation.MouseButtonsSwapped) { mouseintsignal = 0x02; }
            //int hr = 0;
            

          /*  if (DesktopRootElement == null || TaskBarIconCollection == null || ItemsRectArea == null)
            {
                Thread myscreenthread = new Thread(() =>
                {
                    TrayhWnd = MyScreen(); // Call the MyScreen method and assign its result to TrayhWnd
                });
                myscreenthread.Start();
                myscreenthread.Join();

                Thread taskthread = new Thread(() => FindTaskBarIcons(TrayhWnd, out DesktopRootElement, out TaskBarIconCollection, out ItemsRectArea));
                taskthread.Start();
                taskthread.Join();
               
            }*/
            TrayhWnd = MyScreen();
            RECTOUT TRAY_RECTOUT;

            var TrayHANDLE = FindWindow(TrayhWnd, null);
            GetWindowRect(TrayHANDLE, out TRAY_RECTOUT);
            Rect TRAY_rect_AREA = new Rect(TRAY_RECTOUT.Left, TRAY_RECTOUT.Top,Math.Abs(TRAY_RECTOUT.Left- TRAY_RECTOUT.Right),Math.Abs(TRAY_RECTOUT.Top - TRAY_RECTOUT.Bottom));

            
            ///////1.0.6 Thread.Sleep(2);
            ///
           
            try
            {
                if ((GetAsyncKeyState(mouseintsignal) & 0x8000) != 0 && !isDragging && !CheckCurrentMouseareaWithRectArea(TRAY_rect_AREA) && !focused )//(DesktopRootElement.Current.BoundingRectangle)) // Check if mouse button is pressed
                {
                   
                        dragStartPoint = Cursor.Position; // Store the current mouse position as the drag start point
                        isDragging = true; // Set the dragging flag to true
                }
                    
               if (isDragging && CheckCurrentMouseareaWithRectArea(TRAY_rect_AREA) && !TRAY_rect_AREA.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 5, 5)))// && SelectedIcon == null )//|| !CheckCurrentMouseareaWithRectArea(SelectedIcon.Current.BoundingRectangle)))
                {
                    // If we are already dragging and the mouse is within the TRAY_rect_AREA but not within the small 5x5 rect around drag start point
                    // SearchIconAndFocus(TaskBarIconCollection, Cursor.Position, out SelectedIcon); // Search for an icon in the taskbar and focus on it
                    SearchIconAndFocusNEW(TrayhWnd, Cursor.Position);


                }
               

                
                if ((GetAsyncKeyState(mouseintsignal) & 0x8000) == 0) // Check if the mouse button is not pressed
                {
                    focused = false;
                    dragStartPoint = new System.Drawing.Point(0, 0); // Reset the drag start point to (0,0)
                    isDragging = false; // Set the dragging flag to false
                }
            }
            catch (COMException ex)
            {
                // Handle the specific COMException here

                // Log or display error information, or take appropriate action
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }

        }

     
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
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
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
            SelectedTimer.Interval = 5;
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



        //
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
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
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
                TrayhWnd = MyScreen();
                RECTOUT TRAY_RECTOUT;
                if ((GetAsyncKeyState(mouseintsignal) & 0x8000) == 0)
                {
                    isDragging = false;
                    waitforFunc = false;
                    StackTrace stackTrace = new StackTrace(true);
                    StackFrame frame = stackTrace.GetFrame(0);
                    int lineNumber = frame.GetFileLineNumber();
                    string callerMethod = frame.GetMethod().Name;
                    Console.WriteLine($"using timer Mouse UP  X:  {Cursor.Position.X} , Mouse Y:  {Cursor.Position.Y} ,   Method: {callerMethod}, Line: {lineNumber}");
                    SelectedTimer.Stop();
                }
                var TrayHANDLE = FindWindow(TrayhWnd, null);
                GetWindowRect(TrayHANDLE, out TRAY_RECTOUT);
                Rect TRAY_rect_AREA = new Rect(TRAY_RECTOUT.Left, TRAY_RECTOUT.Top, Math.Abs(TRAY_RECTOUT.Left - TRAY_RECTOUT.Right), Math.Abs(TRAY_RECTOUT.Top - TRAY_RECTOUT.Bottom));





                if (selectedIcon != null)
                {
                    if (!selectedIcon.Current.BoundingRectangle.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 5, 5)))
                    {
                        SearchIconAndFocusNEW(TrayhWnd, Cursor.Position);
                    }

                }
                else
                {
                    if (clicked && isDragging && CheckCurrentMouseareaWithRectArea(TRAY_rect_AREA) && !TRAY_rect_AREA.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 5, 5)))// && SelectedIcon == null )//|| !CheckCurrentMouseareaWithRectArea(SelectedIcon.Current.BoundingRectangle)))
                    {
                        // If we are already dragging and the mouse is within the TRAY_rect_AREA but not within the small 5x5 rect around drag start point
                        // SearchIconAndFocus(TaskBarIconCollection, Cursor.Position, out SelectedIcon); // Search for an icon in the taskbar and focus on it
                        SearchIconAndFocusNEW(TrayhWnd, Cursor.Position);


                    }
                }


            }
            catch (COMException ex)
            {
                // Handle the specific COMException here

                // Log or display error information, or take appropriate action
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }

        }

        private void btn_localize_Click(object sender, EventArgs e)
        {

            var initresult = MessageBox.Show("For the initial setup, it is required to locate certain inputs.", "initial Setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (initresult == DialogResult.OK)
            {
                uint stringID = 11114;
               
                string muiFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\" + cultureInfo + "\\explorer.exe.mui";
            HERE:
                IntPtr hInstance = IntPtr.Zero;
                if (File.Exists(muiFilePath)) 
                {
                    hInstance = LoadLibrary(muiFilePath);
                }
            // Get the display language

                string displayLanguage = cultureInfo.DisplayName;
                 
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
                        //MessageBox.Show("Localized String: " + localizedString);
                        Conf.Default.DisLan = cultureInfo.DisplayName;
                        Conf.Default.RunningWin = localizedString.Remove(0, 7);
                        Conf.Default.Save();
                        Conf.Default.Reload();
                    }

                   
                    MessageBox.Show("Done...", "initial Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
 
                else 
                {

                var retrycan =MessageBox.Show("Failed to load the Localized .mui file.  " + Environment.NewLine + muiFilePath +Environment.NewLine + "Do you want to load it manually?", "Failed...!!",MessageBoxButtons.YesNo);
                    if (retrycan == DialogResult.Yes)
                    {

                        HERE2:
                        OpenFileDialog openFile = new OpenFileDialog();
                        openFile.FileName = "explorer.exe.mui";
                        openFile.Filter = "explorer.exe.mui|*.mui";
                        var resopen = openFile.ShowDialog();
                        if (resopen==DialogResult.OK)
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
               // fileWriter.Flush();
               /// fileWriter.Close();
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




        private  IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try 
            {
                int winMsg = (int)wParam;

                if (nCode >= 0)
                {
                    MSLLHOOKSTRUCT mouseInfo = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    switch (winMsg)
                    {
                        case 514: // Mouse Up
                            HandleMouseUp(mouseInfo);
                            break;

                        case 512: // Mouse Move
                            HandleMouseMove(mouseInfo);
                            break;

                        case 513: // Mouse Down
                            HandleMouseDown(mouseInfo);
                            break;
                    }
                }
            }
            catch (COMException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private  void HandleMouseUp(MSLLHOOKSTRUCT mouseInfo)
        {
            clicked = false;
            isDragging = false;
            waitforFunc = false;
            Console.WriteLine("Mouse Up: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
        }

        private void HandleMouseMove(MSLLHOOKSTRUCT mouseInfo)
        {
            try
            {
                if (clicked && !waitforFunc)
                {

                    if (selectedIcon != null)
                    {
                        var npt = ConvertDraw2system(Cursor.Position);
                        if (selectedIcon.Current.BoundingRectangle.Contains(npt))
                        {
                            ResetTimerAndStartDragging(mouseInfo);
                        }
                    }
                    else
                    {
                        ResetTimerAndStartDragging(mouseInfo);
                    }
                }
            }
            catch (COMException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.ErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.Source} {ex.StackTrace} {ex.InnerException}");
            }
        }

        private static void HandleMouseDown(MSLLHOOKSTRUCT mouseInfo)
        {
            dragStartPoint.X = mouseInfo.pt.x;
            dragStartPoint.Y = mouseInfo.pt.y;
            clicked = true;
            Console.WriteLine("Mouse Down: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
        }

        private void ResetTimerAndStartDragging(MSLLHOOKSTRUCT mouseInfo)
        {
            
            SelectedTimer.Stop();
            SelectedTimer.Start();
            isDragging = true;
            Console.WriteLine("Dragging: X=" + mouseInfo.pt.x + ", Y=" + mouseInfo.pt.y);
            waitforFunc = true;
        }





        /*  private static IntPtr HookCallback(int nCode, IntPtr  wParam, IntPtr lParam)
        {
            int WinMsg = (int)wParam;
            // Check if the hook is active and if the event is a mouse movement event
            if (nCode >= 0)
            {

                // Extract mouse information from lParam
                MSLLHOOKSTRUCT mouseInfo = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                
                switch (WinMsg)
                {
                    case 514:////Main Mouse UP(default mouse left button or maybe swapped right mouse botton 
                        {
                            clicked = false;
                            isDragging = false;
                            waitforFunc = false;
                            
                            Console.WriteLine("Mouse UP Mouse X: " + mouseInfo.pt.x + ", Mouse Y: " + mouseInfo.pt.y);
                            break;
                        }
                    case 512: // mouse move
                        {
                            
                            if (clicked && !waitforFunc)
                            {
                                MainForm mainForm = new MainForm();
                                if (mainForm.selectedIcon != null)
                                {
                                    var npt = mainForm.ConvertDraw2system(Cursor.Position);
                                   if (!mainForm.selectedIcon.Current.BoundingRectangle.Contains(npt))
                                    {
                                        mainForm.SelectedTimer.Stop();
                                        mainForm.SelectedTimer.Start();
                                        isDragging = true;
                                        Console.WriteLine("Draging Mouse X: " + mouseInfo.pt.x + ", Mouse Y: " + mouseInfo.pt.y);
                                        waitforFunc = true;
                                    }
                                }
                                else 
                                {
                                    mainForm.SelectedTimer.Stop();
                                    mainForm.SelectedTimer.Start();
                                    isDragging = true;
                                    Console.WriteLine("Draging Mouse X: " + mouseInfo.pt.x + ", Mouse Y: " + mouseInfo.pt.y);
                                    waitforFunc = true;
                                }
                                
                               
                            }
                            break;
                        }
                    
                    case 513: //Main Mouse Down(default mouse left button or maybe swapped right mouse botton 
                        {
                            dragStartPoint.X = mouseInfo.pt.x;
                            dragStartPoint.Y = mouseInfo.pt.y;
                            clicked =true;
                            Console.WriteLine("CLICK Mouse X: " + mouseInfo.pt.x + ", Mouse Y: " + mouseInfo.pt.y);
                            break;
                        }
                    

                }
                
                

                //Console.WriteLine(wParam.ToString());
                // Mouse move event - you can track the mouse coordinates here
               // Console.WriteLine("Mouse X: " + mouseInfo.pt.x + ", Mouse Y: " + mouseInfo.pt.y);
            }
            
           

            // Call the next hook in the chain
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }*/


    }

    class DualTextWriter : TextWriter
    {
        private TextWriter consoleWriter;
        private TextWriter fileWriter;

        public DualTextWriter(TextWriter consoleWriter, TextWriter fileWriter)
        {
            this.consoleWriter = consoleWriter;
            this.fileWriter = fileWriter;
        }

        public override Encoding Encoding => Encoding.Default;

        public override void Write(char value)
        {
             
            if (Conf.Default.showlog) {
                // Write to console
                consoleWriter.Write(value);
                // Write to file
                fileWriter.Write(value); 
                fileWriter.FlushAsync().Wait();
            }
               
        }
    }
}
