namespace ServerApplicationMobile;

public partial class ProductDetailPage : ContentPage
{
    private Product _product;

    public ProductDetailPage(Product product)
    {
        InitializeComponent();

        _product = product;
        BindingContext = _product;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Optional: add validation or persistence logic here

        await DisplayAlert("Saved", "Product details updated successfully.", "OK");
        await Navigation.PopAsync();
    }
}
