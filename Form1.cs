using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows;
using System.Windows.Automation;
using System.Runtime.InteropServices;
using TaskBarDragAndDrop;
using Microsoft.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Policy;
using System.Diagnostics;
using TaskBarDragAndDrop.Properties;

namespace CSHARPWindowsFormsApp1
{


    public partial class MainForm : Form
    {



        ///////
        //
        // DLL imports and Global Vars
        //
        ///////
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        private const int VK_LBUTTON = 0x01;
        private static bool isDragging = false;
        private static System.Drawing.Point dragStartPoint = System.Drawing.Point.Empty;
        private static bool ImDone=false;
        private static Rect TrayRectangle;
        private static AutomationElement ChosentaskIcon;
        private static int tik = 0;
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
            

            if (showme.Contains(SystemWindowsCursorPoint) && isDragging )
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

            if (screen.Primary)
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
            if (justtray == 28 )
            { // if i only need taskbar area // random number // my fav number :)

                if(targetAppWindow == null)
                {
                    //if null error handling
                    return 0;
                }
                TrayRectangle = targetAppWindow.Current.BoundingRectangle;
                return 1;
            }
            if (targetAppWindow != null)
            {
                PropertyCondition taskbarelements = new PropertyCondition(AutomationElement.ClassNameProperty, "Taskbar.TaskListButtonAutomationPeer");

                // Find all Icons in the Taskbar by its class name
                AutomationElementCollection panel = targetAppWindow.FindAll(System.Windows.Automation.TreeScope.Descendants, taskbarelements);

                // for each Icon in Task Bar we found

                foreach (AutomationElement taskbaricons in panel)
                {

                    //if mouse is over the Icon in TaskBar  and draging and dragstart and current is not the dame
       
                    Rect itemrect = taskbaricons.Current.BoundingRectangle;

                    /// check AutomationID if its a real icon not a widget or other items
                    if (taskbaricons.Current.AutomationId.Contains("Appid:") && CheckMousearea(itemrect) && isDragging && Cursor.Position != dragStartPoint)

                    {
                        //save area in global val, Just in case
                        ChosentaskIcon = taskbaricons;

                        //focus on icon
                        taskbaricons.SetFocus();

                        if (Conf.Default.ClickPinApp) // if auto click enabled
                        {
                            //due check if you moved your cursor during wait time
                            System.Drawing.Point LastCursorCheck = Cursor.Position;
                            Rect tmprect = new Rect(ConvertDraw2system(LastCursorCheck).X, ConvertDraw2system(LastCursorCheck).Y, 10, 10);
                            Thread.Sleep(Conf.Default.clickInterval);
                            if (CheckMousearea(tmprect)) { 
                            
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

        private void Form1_Load(object sender, EventArgs e)
        {
            // Specify properties to identify the target application's main window by class name
            
            checkbox_ClickPinApp.Checked = Conf.Default.ClickPinApp;
            checkbox_Runatstart.Checked = Conf.Default.Runatstart;
            txt_mousehook.Text = Conf.Default.mousehookint.ToString();
            txt_clickInterval.Text = Conf.Default.clickInterval.ToString();
            btn_resetsetting.Visible = false;
            btn_savesetting.Visible = false;
            ShowInTaskbar = false;
           

        }

      

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

           
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;



        }

        private void MouseIsDragging_Tick(object sender, EventArgs e)
        { //chech if LeftBtn is  pressed (  drag or Start dragging)
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
            {

               // FindTaskbar(28); // find taskbar and save rectangle area in global val TrayRectangle 
                if (!isDragging && !CheckMousearea(TrayRectangle)) // if no drag happend before and mouse is not on taskbar
                {
                    isDragging = true;
                    //check drag start position
                    dragStartPoint = Cursor.Position;//save drag start point into global val
                                  
                    
                }

                FindTaskbar(28); // find taskbar and save rectangle area in global val TrayRectangle
                
                //temp rect from mouse drag start pos
                Rect tmprect = new Rect(ConvertDraw2system(dragStartPoint).X, ConvertDraw2system(dragStartPoint).Y, 1, 1);
                if (isDragging && CheckMousearea(TrayRectangle) && !TrayRectangle.Contains(tmprect) && !ImDone){

                    FindTaskbar(1); //random number except 28

                }



            }
            else
            { 
                //chech if LeftBtn is not pressed ( no drag or finish dragging)
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) {
                    isDragging = false;
                    ImDone= false;
                    
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
                key.DeleteValue("TaskBar DragAndDrop(NO UAC)",false); 
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
            Conf.Default.mousehookint= int.Parse(txt_mousehook.Text.ToString());
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
            if( WindowState== FormWindowState.Minimized ) {
                ShowInTaskbar = false;
            }
            
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.flaticon.com/free-icons/drag-and-drop");
        }

        private void pictureBox1_MouseHover(object sender, EventArgs e)
        {
            toolTip1.Show("Drag and drop icons created by Freepik" + Environment.NewLine + "- Flaticon ( Click To Open Website)",pictureBox1);
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
            AboutBox1 aboutBox1 = new AboutBox1();
            aboutBox1.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Mast3r0mid/TaskBarDragAndDropNoUAC");
        }
    }
}
