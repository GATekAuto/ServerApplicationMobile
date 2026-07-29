namespace ServerApplicationMobile;

public partial class JobDetailPage : ContentPage
{
    private Job _job;

    public JobDetailPage(Job job)
    {
        InitializeComponent();

        _job = job;
        BindingContext = _job; // Pre-fill with existing data
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Optionally, validate _job here or save it to a database

        await DisplayAlert("Saved", "Job details updated successfully.", "OK");
        await Navigation.PopAsync();
    }
}
