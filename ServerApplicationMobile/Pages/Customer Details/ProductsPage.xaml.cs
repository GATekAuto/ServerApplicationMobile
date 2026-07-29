namespace ServerApplicationMobile.Pages.Customer_Details;

public partial class ProductsPage : ContentPage
{
	public ProductsPage(List<Product> products)
	{
		InitializeComponent();
        ProductsCollectionView.ItemsSource = products;
    }
    private async void OnProductTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is Product tappedProduct)
        {
            // Animate the frame on tap
            await frame.ScaleTo(0.97, 75, Easing.CubicInOut);
            await frame.ScaleTo(1.0, 75, Easing.CubicInOut);

            // Navigate to the detail page
            await Navigation.PushAsync(new ProductDetailPage(tappedProduct));
        }
    }
}