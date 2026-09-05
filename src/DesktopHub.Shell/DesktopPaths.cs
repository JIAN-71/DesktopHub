namespace DesktopHub.Shell;

/// <summary>当前用户的桌面与所有用户公共桌面路径。</summary>
public static class DesktopPaths
{
    public static string UserDesktop =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string CommonDesktop =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
}
