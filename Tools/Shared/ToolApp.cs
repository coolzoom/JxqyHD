using System.Text;
using Eto.Forms;

namespace JxqyTools
{
    /// <summary>
    /// Shared bootstrap for the Eto tools: registers the GBK code-page
    /// provider (required on .NET Core for Encoding 936) and quits the app
    /// when the main form is closed. The form is created after the
    /// Application/platform instance, so use Run&lt;T&gt; rather than
    /// constructing the form in the caller.
    /// </summary>
    public static class ToolApp
    {
        public static void Run<T>() where T : Form, new()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var app = new Application(Eto.Platform.Detect);
            var form = new T();
            form.Closed += (s, e) => app.Quit();
            app.Run(form);
        }
    }
}
