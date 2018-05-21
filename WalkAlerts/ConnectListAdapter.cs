using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections.Generic;
using System;

namespace WalkAlerts
{
    public class ConnectListAdapter : RecyclerView.Adapter
    {
        public List<ComputerInfo> items { get; set; }
        public event EventHandler<string> ItemClick;

        // Create new views (invoked by the layout manager)
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ComputerButtonView, parent, false);
            // set the view's size, margins, paddings and layout parameters
            var vh = new ComputersViewHolder(itemView, OnClick);
            return vh;
        }

        // Replace the contents of a view (invoked by the layout manager)
        public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position)
        {
            var item = items[position];
            var holder = viewHolder as ComputersViewHolder;
            holder.ComputerButton.Text = items[position].Name + ":" + items[position].IpString;
        }

        public override int ItemCount
        {
            get
            {
                return items.Count;
            }
        }

        public void OnClick(int pos)
        {
            ItemClick?.Invoke(this, items[pos].ToString());
        }

    }

    public class ComputersViewHolder : RecyclerView.ViewHolder
    {
        public Button ComputerButton { get; set; }

        public ComputersViewHolder(View v, Action<int> listener) : base(v)
        {
            ComputerButton = v.FindViewById<Button>(Resource.Id.computerButton);
            ComputerButton.Click += (sender, e) => listener(LayoutPosition);
        }
    }

    public class ComputerInfo
    {
        public string IpString { get; set; }
        public string Name { get; set; }
        public string PortStr { get; set; }
        public override string ToString()
        {
            return IpString + ";" + PortStr;
        }
    }

}