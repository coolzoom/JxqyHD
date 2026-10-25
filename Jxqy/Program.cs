using Engine;
using System.Linq;
using System.Text;

namespace Jxqy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var isEditMode = args.Any(a => a == "--edit" || a == "-edit");
            using (var jxqyGame = new JxqyGame(isEditMode))
            {
                jxqyGame.Run();
            }
        }
    }
}
