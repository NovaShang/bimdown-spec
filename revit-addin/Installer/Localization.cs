using System.Globalization;

namespace BimDown.Installer;

static class L
{
    static readonly bool IsChinese =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";

    public static string S(string en, string zh) => IsChinese ? zh : en;

    public static string Title => S("BimDown Installer", "BimDown 安装程序");
    public static string Heading => S("BimDown for Revit", "BimDown Revit 插件");
    public static string Description => S(
        "Export and import BimDown format (CSV + SVG)\nfor AI-native building modeling.\n\nSupported: Revit 2025, 2026",
        "导出和导入 BimDown 格式 (CSV + SVG)\n用于 AI 原生建筑建模。\n\n支持: Revit 2025, 2026");
    public static string Install => S("Install", "安装");
    public static string Reinstall => S("Reinstall", "重新安装");
    public static string Uninstall => S("Uninstall", "卸载");
    public static string StatusInstalled => S("Status: Installed", "状态: 已安装");
    public static string StatusNotInstalled => S("Status: Not installed", "状态: 未安装");
    public static string StatusInstalledOk => S("Status: Installed successfully! Restart Revit to load.", "状态: 安装成功！请重启 Revit 加载插件。");
    public static string StatusUninstalled => S("Status: Uninstalled. Restart Revit to take effect.", "状态: 已卸载。请重启 Revit 使其生效。");
    public static string NothingToUninstall => S("Nothing to uninstall.", "没有可卸载的内容。");
    public static string ConfirmUninstall => S("Remove BimDown plugin from Revit?", "确定要从 Revit 中移除 BimDown 插件吗？");
    public static string ConfirmTitle => S("Confirm Uninstall", "确认卸载");
    public static string ErrorAccessDenied => S("Access denied. Try running the installer as Administrator.", "访问被拒绝。请尝试以管理员身份运行安装程序。");
    public static string ErrorInstallFailed => S("Install failed. Try running as Administrator.", "安装失败。请尝试以管理员身份运行。");
    public static string ErrorUninstallFailed => S("Uninstall failed. Try running as Administrator.", "卸载失败。请尝试以管理员身份运行。");
    public static string OpenInstallLocation => S("Open install location", "打开安装目录");
}
