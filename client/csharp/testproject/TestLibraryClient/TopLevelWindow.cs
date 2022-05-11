namespace TestLibraryClient
{
    public class TopLevelWindow : IWindowDelegate
    {
        private readonly WLWindow _wlWindow;
        protected TopLevelWindow(string title)
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
        // override these, if you please ==========================
        public virtual void OnRepaint(CGContext context)
        {
        }
        public virtual void OnResize(int width, int height)
        {
        }
        public virtual void OnDestroyed()
        {
        }
        public virtual void OnCloseRequest(ref bool cancelClose)
        {
        }
    }
}
