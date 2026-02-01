using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.ViewModels;

namespace Storyboard.Views;

public partial class UpdateNotificationView : UserControl
{
    public UpdateNotificationView()
    {
        InitializeComponent();

        // 直接从 DI 容器获取 ViewModel，避免绑定问题
        if (Design.IsDesignMode)
            return;

        try
        {
            var viewModel = App.Services.GetRequiredService<UpdateNotificationViewModel>();
            DataContext = viewModel;
        }
        catch
        {
            // 设计时或初始化失败时忽略
        }
    }
}
