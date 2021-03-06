using Android.OS;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;
using System;
using TonyHoyle.EH;
using Android.Support.V7.App;
using System.Globalization;
using Android.Content;

using NotificationCompat = Android.Support.V7.App.NotificationCompat;
using PendingIntent = Android.App.PendingIntent;
using PendingIntentFlags = Android.App.PendingIntentFlags;
using NotificationManager = Android.App.NotificationManager;
using Android.Util;

namespace ClockworkHighway.Android
{
    public class ChargingFragment : Fragment
    {
        private string _sessionId;
        private int _pumpId;
        private int _connectorId;

        private TextView _chargeStatus;
        private TextView _chargeTime;
        private TextView _chargePower;
        private TextView _chargeCost;
        private ProgressBar _progressBar;
        private Button _chargeStop;
        private Handler _handler = new Handler();
        private TextView _messageStop;
        private bool _completed;
        private bool _started;

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.charging, container, false);
        }

        public override void OnActivityCreated(Bundle savedInstanceState)
        {
            base.OnActivityCreated(savedInstanceState);

            _sessionId = Arguments.GetString("sessionId");
            _pumpId = Arguments.GetInt("pumpId");
            _connectorId = Arguments.GetInt("connectorId");

            _chargeStatus = View.FindViewById<TextView>(Resource.Id.chargeStatus);
            _chargeTime = View.FindViewById<TextView>(Resource.Id.chargeTime);
            _chargePower = View.FindViewById<TextView>(Resource.Id.chargePower);
            _chargeCost = View.FindViewById<TextView>(Resource.Id.chargeCost);
            _progressBar = View.FindViewById<ProgressBar>(Resource.Id.progressBar);
            _chargeStop = View.FindViewById<Button>(Resource.Id.chargeStop);
            _messageStop = View.FindViewById<TextView>(Resource.Id.messageStop);

            _messageStop.Visibility = ViewStates.Gone;

            _chargeStop.Click += OnStopCharge;
            _chargeStop.LongClick += OnTerminateCharge;

            _completed = false;
            _started = false;

            UpdateDisplay(true);
        }

        private void OnStopCharge(object sender, EventArgs e)
        {
            if (_completed || !_started)
            {
                _handler.RemoveCallbacks(OnTimer);
                Activity.Finish();
                return;
            }

            OnTerminateCharge(sender, e);
        }

        private void OnTerminateCharge(object sender, EventArgs e)
        {
            int msg;

            if (!_completed)
                msg = Resource.String.areYouSureStop;
            else
                msg = Resource.String.areYouSureStop2;

            var dlg = new AlertDialog.Builder(Context)
                .SetTitle(Resource.String.stopCharge)
                .SetMessage(msg)
                .SetPositiveButton("Yes", (obj, args) => { OnStopChargeYes(); })
                .SetNegativeButton("No", (obj, args) => { })
                .Create();

            dlg.Show();
        }

        private async void OnStopChargeYes()
        {
            try
            {
                EHApi.BoolResult res = await SharedData.api.stopChargeSessionAsync(_pumpId, _connectorId, _sessionId);
                if (!res.result)
                {
                    Toast.MakeText(Context.ApplicationContext, res.message != null ? res.message : "Unable to stop charge", ToastLength.Short).Show();
                }
            }
            catch(EHApi.EHApiException e)
            {
                Toast.MakeText(Context.ApplicationContext, e.Message, ToastLength.Short).Show();
			}
        }

        private void OnTimer()
        {
            UpdateDisplay(false);
            if(!_completed)
                _handler.PostDelayed(OnTimer, 5000);
        }

        private async void UpdateDisplay(bool updateOnly)
        {
			try
			{
				var status = await SharedData.api.getChargeStatusAsync(_pumpId, _connectorId, _sessionId);

				/* We are sometimes called with a null activity */
				if (Activity == null)
				{
					_completed = status.completed;
                    _started = status.started != null;
					return;
				}

				Activity.RunOnUiThread(() =>
				{
					_chargeStatus.Text = status.message;
					_chargePower.Text = String.Format(Context.GetString(Resource.String.powerSupplied), status.energyConsumption);
					_chargeCost.Text = String.Format(Context.GetString(Resource.String.powerCost), status.cost);
					long mins;
					TimeSpan diff;  
					DateTime started;

					if (status.status == "Retry")
						status.completed = true;

					if (string.IsNullOrEmpty(status.started))
						started = DateTime.Now;
					else
						started = DateTime.ParseExact(status.started, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);

					if (string.IsNullOrEmpty(status.finished))
						diff = DateTime.Now - started;
					else
					{
						DateTime finished = DateTime.ParseExact(status.finished, "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
						diff = finished - started;
					};

					mins = (long)diff.TotalMinutes;
					_chargeTime.Text = String.Format(Context.GetString(Resource.String.chargingMinutes), mins);
					_progressBar.Max = 45;
					_progressBar.Progress = (int)Math.Min(45, mins);

					if (status.completed)
					{
						Activity.SetTitle(Resource.String.lastCharge);
						_chargeStop.SetText(Resource.String.chargeFinished);
						_messageStop.Visibility = ViewStates.Visible;
					}

					if (!updateOnly)
					{
						if (!_completed && status.completed)
							SendUpdateNotification();
					}

					_completed = status.completed;
                    _started = status.started != null;
				});
			}
            catch (EHApi.EHApiException e)
			{
				Log.Debug(SharedData.APP, "Failed to update display: " + e.Message);
			}
        }

        public override void OnPause()
        {
            base.OnPause();
        }

        public override void OnResume()
        {
            base.OnResume();

            // Force an update just in case
            OnTimer();
        }

        private void SendUpdateNotification()
        {
            // Creates an explicit intent for an Activity in your app
            var resultIntent = new Intent(Context, typeof(ChargingActivity));

            var bundle = new Bundle();
            bundle.PutString("sessionId", _sessionId);
            bundle.PutInt("pumpId", _pumpId);
            bundle.PutInt("connectorId", _connectorId);

            resultIntent.PutExtras(bundle);

            // The stack builder object will contain an artificial back stack for the
            // started Activity.
            // This ensures that navigating backward from the Activity leads out of
            // your application to the Home screen.
            TaskStackBuilder stackBuilder = TaskStackBuilder.Create(Context);
            // Adds the back stack for the Intent (but not the Intent itself)
            stackBuilder.AddParentStack(Java.Lang.Class.FromType(typeof(ChargingActivity)));

            // Adds the Intent that starts the Activity to the top of the stack
            stackBuilder.AddNextIntent(resultIntent);

            var resultPendingIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.UpdateCurrent);

            var builder =
                    new NotificationCompat.Builder(Context)
                    .SetSmallIcon(Resource.Drawable.ic_directions_car_white_24dp)
                    .SetContentTitle(Context.GetString(Resource.String.ApplicationName))
                    .SetContentText(Context.GetString(Resource.String.chargeFinishedNotification))
                    .SetContentIntent(resultPendingIntent);
            NotificationManager notificationManager =
                (NotificationManager)Context.GetSystemService(Context.NotificationService);
            // mId allows you to update the notification later on.
            notificationManager.Notify(1, builder.Build());
        }
    }
}