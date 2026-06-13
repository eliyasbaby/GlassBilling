using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class NewBillStep2Page : ContentPage
{
    public NewBillStep2Page(NewBillViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
