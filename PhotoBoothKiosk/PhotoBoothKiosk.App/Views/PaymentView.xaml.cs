using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoBoothKiosk.App.ViewModels;

namespace PhotoBoothKiosk.App.Views
{
    public partial class PaymentView : UserControl
    {
        public PaymentView()
        {
            InitializeComponent();
        }

        private PaymentViewModel? VM => DataContext as PaymentViewModel;

        // Nút chọn nhanh 1/2/4
        private void SelectCount1_Click(object sender, System.Windows.RoutedEventArgs e) => VM?.SelectCount("1");
        private void SelectCount2_Click(object sender, System.Windows.RoutedEventArgs e) => VM?.SelectCount("2");
        private void SelectCount4_Click(object sender, System.Windows.RoutedEventArgs e) => VM?.SelectCount("4");

        // Áp dụng số lượng tuỳ chọn từ ô nhập
        private void ApplyCustom_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var text = CustomCountBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text)) VM?.SelectCount(text);
        }

        // Chỉ cho phép số
        private static readonly Regex _onlyDigits = new Regex("^[0-9]+$");
        private void CustomCountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_onlyDigits.IsMatch(e.Text);
        }

        // Cập nhật giá xem trước khi người dùng gõ số
        private void CustomCountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = CustomCountBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text) && _onlyDigits.IsMatch(text))
                VM?.SelectCount(text);
        }
    }
}
