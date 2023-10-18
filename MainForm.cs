using System;
using Microsoft;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Xml;
using TaskBarDragAndDrop;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
        const int KEYEVENTF_KEYDOWN = 0x1;
        const int KEYEVENTF_KEYUP = 0x2;
        const int VK_LWIN = 0x5B; // Left Win key
        const int VK_T = 0x54; // 'T' key
        const int VK_SHIFT = 0x10; //Shift key


        private System.Windows.Forms.ToolTip toolTipDsk = new System.Windows.Forms.ToolTip();

    

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private int mouseintsignal = 0x01;
        private static bool isDragging = false;
        private static System.Drawing.Point dragStartPoint = new System.Drawing.Point(0,0);
        private static bool ImDone, focused = false;
        private static Rect TrayRectangle;
        private String TrayhWnd = "Shell_TrayWnd";
        private static AutomationElement ChosentaskIcon;
        static Mutex mutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC)");
        static Mutex aboutMutex = new Mutex(true, "TaskBar DragAndDrop(NO UAC) AboutBoxMutex");
        
        public bool aboutBoxOpen, mainFormOpen, myEnd = false;
        AboutBox1 aboutForm = new AboutBox1();
       

        public System.Windows.Point oldpoint = new System.Windows.Point(0,0);

        AutomationElement DesktopRootElement, SelectedIcon = null;
        AutomationElementCollection TaskBarIconCollection = null;
        Rect ItemsRectArea = Rect.Empty;



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
        private System.Windows.Point ConvertDraw2system(System.Drawing.Point Draw2sys) 
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
               // MessageBox.Show("FindTaskBarIcons com");
                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
               // MessageBox.Show( ex.Message + Environment.NewLine + ex.StackTrace +Environment.NewLine + ex.Source);
            }

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

            try
            {
                MouseIsDragging.Interval = Conf.Default.mousehookint;
                SelectedTimer.Interval = Conf.Default.mousehookint;
                checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
                checkbox_closeTray.Checked = Conf.Default.closetotray;
                checkbox_Runatstart.Checked = Conf.Default.Runatstart;
                txt_mousehook.Text = Conf.Default.mousehookint.ToString();
                txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
                btn_resetsetting.Visible = false;
                btn_savesetting.Visible = false;
                ShowInTaskbar = false;
                //this.Show();
            }
            catch (COMException ex) {
               // MessageBox.Show("man load COM");
            }
            catch (Exception ex)
            {
               // MessageBox.Show("man lod ex");
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
            return hWsc;
        }

        /// Main Function : select Element and invoke or Focus Based on mouse position
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
                                }

                            }
                            else if (SelectedIconfn.Current.Name.Contains("running window"))
                            {

                                Thread.Sleep(Conf.Default.clickInterval);
                                
                                if (CheckCurrentMouseareaWithRectArea(SelectedIconfn.Current.BoundingRectangle))
                                {
                                    selection.Invoke();
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

                            }



                        }
                            //break;

                        }
                    }
                }
            
            catch (COMException ex)
            {
               // MessageBox.Show("SearchIconAndFocus Com");

            }
            catch (Exception ex)
            {
              //  MessageBox.Show("SearchIconAndFocus ex");
            }


        }


        // Main Function : mouse move and click check
        private void MouseIsDragging_Tick(object sender, EventArgs e)
        { 
            //chech if LeftBtn is  pressed (  drag or Start dragging)
            if (SystemInformation.MouseButtonsSwapped) { mouseintsignal = 0x02; }
            int hr = 0;
            TrayhWnd = MyScreen();
            
            FindTaskBarIcons(TrayhWnd, out DesktopRootElement, out TaskBarIconCollection,out ItemsRectArea );
            Thread.Sleep(2);

            try
            {
                if ((GetAsyncKeyState(mouseintsignal) & 0x8000) != 0) // Check if mouse button is pressed
                {
                    if (!CheckCurrentMouseareaWithRectArea(DesktopRootElement.Current.BoundingRectangle) && !isDragging) // Check if the mouse is not within the desktop area
                    {
                        dragStartPoint = Cursor.Position; // Store the current mouse position as the drag start point
                        isDragging = true; // Set the dragging flag to true
                    }
                    
                    if (isDragging && CheckCurrentMouseareaWithRectArea(ItemsRectArea) && !ItemsRectArea.Contains(new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 5, 5)) && (SelectedIcon == null || !CheckCurrentMouseareaWithRectArea(SelectedIcon.Current.BoundingRectangle)))
                    {
                        // If we are already dragging and the mouse is within the ItemsRectArea but not within the small 5x5 rect around drag start point
                        SearchIconAndFocus(TaskBarIconCollection, Cursor.Position, out SelectedIcon); // Search for an icon in the taskbar and focus on it
                    }

                }
                else if ((GetAsyncKeyState(mouseintsignal) & 0x8000) == 0) // Check if the mouse button is not pressed
                {
                    dragStartPoint = new System.Drawing.Point(0, 0); // Reset the drag start point to (0,0)
                    isDragging = false; // Set the dragging flag to false
                }
            }
            catch (COMException ex)
            {
                // Handle the specific COMException here
                
                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
               
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

                // Log or display error information, or take appropriate action
            }
            catch (Exception ex)
            {
               // MessageBox.Show("Registry Write Error: " + ex.Message, "Failed!!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            var msbtn  = MouseButtons.Left;
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

        }

        private void timer2_Tick(object sender, EventArgs e)
        {

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
