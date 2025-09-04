using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoBoothKiosk.App.ViewModels
{
    /// <summary>
    /// Lớp cơ sở cho tất cả ViewModel. Cung cấp INotifyPropertyChanged + helper Set/Raise.
    /// </summary>
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raise PropertyChanged cho property.
        /// </summary>
        protected void Raise([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Gán giá trị mới cho field và raise PropertyChanged nếu có thay đổi.
        /// Trả về true nếu giá trị đã thay đổi.
        /// </summary>
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value!;
            Raise(propertyName);
            return true;
        }

        /// <summary>
        /// Raise cho nhiều property cùng lúc (tiện khi state thay đổi ảnh hưởng nhiều binding).
        /// </summary>
        protected void Raise(params string[] propertyNames)
        {
            if (propertyNames == null) return;
            foreach (var n in propertyNames)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        /// <summary>
        /// Thực thi một action an toàn với UI thread nếu cần (tuỳ trường hợp bạn có thể dùng).
        /// </summary>
        protected void OnUi(Action action)
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher?.CheckAccess() == true) action();
            else app?.Dispatcher?.Invoke(action);
        }
    }
}