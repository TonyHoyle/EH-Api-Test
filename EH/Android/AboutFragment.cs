using Android.Support.V4.App;
using Android.OS;
using Android.Support.V7.App;

namespace EH.Android
{
    public class AboutFragment : DialogFragment
    {
        public override global::Android.App.Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(Activity)
                .SetTitle(Resource.String.ApplicationName)
                .SetView(Resource.Layout.AboutDialog)
                .SetPositiveButton(Resource.String.ok, (sender, args) => { Dismiss(); });
            return builder.Create();
        }
    }
}