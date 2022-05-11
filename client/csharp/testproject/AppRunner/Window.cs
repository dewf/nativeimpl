using System;
using TestLibraryClient;

namespace AppRunner
{
    public class Window : IWindowDelegate
    {
        private readonly WLWindow _wlWindow;
        private int _width;
        private int _height;
        public Window(string title)
        {
            _wlWindow = new WLWindow(title, this);
        }
        public void Show()
        {
            _wlWindow.Show();
        }
        public void SetMenuBar(MenuBar mb)
        {
            _wlWindow.SetMenuBar(mb);
        }
        // interface implementation
        public void OnRepaint(CGContext context)
        {
            context.SetRGBFillColor(0, 0, 0, 1);
            context.FillRect(CGRect.OfSize(_width, _height));
            context.SetRGBFillColor(1, 0, 0, 1);
            context.FillRect(new CGRect(100, 100, 100, 100));
        }
        public void OnResize(int width, int height)
        {
            _width = width;
            _height = height;
            Console.WriteLine("Window resized to: {0} / {1}", width, height);
        }
        public void OnDestroyed()
        {
            Console.WriteLine("C# OnDestroyed() called! Shutting down ...");
            OpenWL.ExitRunloop();
        }
    }
}