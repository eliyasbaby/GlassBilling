using System.Windows.Input;

namespace GlassBilling.Views.Controls;

public partial class CustomerFormView : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(CustomerFormView), "Customer Details");

    public static readonly BindableProperty NameTextProperty =
        BindableProperty.Create(nameof(NameText), typeof(string), typeof(CustomerFormView), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty LocationTextProperty =
        BindableProperty.Create(nameof(LocationText), typeof(string), typeof(CustomerFormView), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty PhoneTextProperty =
        BindableProperty.Create(nameof(PhoneText), typeof(string), typeof(CustomerFormView), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty AddressTextProperty =
        BindableProperty.Create(nameof(AddressText), typeof(string), typeof(CustomerFormView), string.Empty, BindingMode.TwoWay);

    public static readonly BindableProperty CancelCommandProperty =
        BindableProperty.Create(nameof(CancelCommand), typeof(ICommand), typeof(CustomerFormView), null);

    public static readonly BindableProperty SaveCommandProperty =
        BindableProperty.Create(nameof(SaveCommand), typeof(ICommand), typeof(CustomerFormView), null);

    public CustomerFormView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string NameText
    {
        get => (string)GetValue(NameTextProperty);
        set => SetValue(NameTextProperty, value);
    }

    public string LocationText
    {
        get => (string)GetValue(LocationTextProperty);
        set => SetValue(LocationTextProperty, value);
    }

    public string PhoneText
    {
        get => (string)GetValue(PhoneTextProperty);
        set => SetValue(PhoneTextProperty, value);
    }

    public string AddressText
    {
        get => (string)GetValue(AddressTextProperty);
        set => SetValue(AddressTextProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => (ICommand?)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }
}
