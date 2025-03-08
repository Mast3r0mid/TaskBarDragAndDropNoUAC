using Autofac;
using Autofac.Extras.DynamicProxy;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using TaskBarDragAndDrop;
using TaskBarDragAndDropNoUAC.LoggingCS;

namespace TaskBarDragAndDropNoUAC
{
    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Initialize Serilog (before Autofac!)
            if (!Directory.Exists($"{Application.StartupPath}\\logs\\"))
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

                Log.Information($"Log Path: {Application.StartupPath}\\logs\\TaskBarDrag&drop.log");
            }



            var container = ConfigureAutofac();

            if (container.IsRegistered<IMainForm>())
            {
               // var mainForm = container.Resolve<IMainForm>(); // Resolve as IMainForm

                //Application.Run((MainForm)mainForm); // Cast to MainForm for Application.Run

                var mainForm = container.Resolve<MainForm>();
                Application.Run(mainForm);
            }
            else
            {
               // throw new InvalidOperationException("IMainForm is not registered in the Autofac container.");
                Application.Run(new MainForm());
            }
        

           // Application.Run(new MainForm());
        }

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
                //.EnableInterfaceInterceptors()
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
    }
}