namespace OrangeGuidanceTomestone.Ui.MainWindowTabs;

public interface ITab : IDisposable {
    public string Name { get; }
    public void Draw();
}
