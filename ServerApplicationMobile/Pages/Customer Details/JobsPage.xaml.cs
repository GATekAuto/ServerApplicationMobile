namespace ServerApplicationMobile.Pages.Customer_Details;

public partial class JobsPage : ContentPage
{
	public JobsPage(List<Job> jobs)
	{
		InitializeComponent();
        JobsCollectionView.ItemsSource = jobs;
    }
    private async void OnJobTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is Job tappedJob)
        {
            // Animate the frame on tap
            await frame.ScaleTo(0.97, 75, Easing.CubicInOut);
            await frame.ScaleTo(1.0, 75, Easing.CubicInOut);

            // Navigate to the detail page
            await Navigation.PushAsync(new JobDetailPage(tappedJob));
        }
    }
}