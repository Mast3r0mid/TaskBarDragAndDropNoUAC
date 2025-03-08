using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;
using Serilog;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace TaskBarDragAndDropNoUAC.LoggingCS
{
    public class LoggingInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            // Skip logging for Form.WndProc
            if (invocation.Method.Name == "WndProc" && invocation.Method.DeclaringType == typeof(Form))
            {
                invocation.Proceed(); // Skip logging and proceed with the method call
                return;

            }
            // Log the method being called
            Log.Information($"Calling method: {invocation.Method.DeclaringType?.Name}.{invocation.Method.Name}");

            // Log the parameters
            ParameterInfo[] parameters = invocation.Method.GetParameters();
            for (int i = 0; i < invocation.Arguments.Length; i++)
            {
                Log.Information($"Parameter {i + 1} ({parameters[i].Name}): {invocation.Arguments[i]}");
            }

            try
            {
                // Proceed with the method call
                invocation.Proceed();

                // Log the return value (if any)
                if (invocation.Method.ReturnType != typeof(void))
                {
                    Log.Information($"Method {invocation.Method.Name} returned: {invocation.ReturnValue}");
                }
            }
            catch (Exception ex)
            {
                // Log any exceptions
                Log.Error($"Method {invocation.Method.Name} threw an exception: {ex}");
                throw; // Re-throw the exception
            }
        }
    }
}