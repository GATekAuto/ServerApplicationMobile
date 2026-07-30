namespace ServerApplicationMobile.Pages.Customer_Details;

public partial class JobsPage : ContentPage
{
	private bool _isOpeningJob;

	public JobsPage(List<Job> jobs)
	{
		InitializeComponent();
        JobsCollectionView.ItemsSource = jobs;
    }
    private async void OnJobTapped(object sender, EventArgs e)
    {
        if (_isOpeningJob || sender is not Frame frame || frame.BindingContext is not Job tappedJob)
            return;

        _isOpeningJob = true;
        try
        {
            // Animate the frame on tap
            await frame.ScaleToAsync(0.97, 75, Easing.CubicInOut);
            await frame.ScaleToAsync(1.0, 75, Easing.CubicInOut);

            // Navigate to the detail page
            await Navigation.PushAsync(new JobDetailPage(tappedJob));
        }
        finally
        {
            _isOpeningJob = false;
        }
    }
}
