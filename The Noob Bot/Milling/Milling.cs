﻿using System.Threading;
using System.Windows.Forms;
using nManager.Helpful;
using nManager.Wow.Bot.States;
using nManager.Wow.Helpers;

namespace Milling
{
    internal class Milling
    {
        public static void Pulse()
        {
            Thread thread = new Thread(ThreadPulse) {Name = "Thread Milling"};
            thread.Start();
        }

        private static void ThreadPulse()
        {
            if (nManager.nManagerSetting.CurrentSetting.HerbsToBeMilled.Count <= 0)
            {
                MessageBox.Show(
                    nManager.Translate.Get(
                        nManager.Translate.Id.Please_add_items_to_mil_in__General_Settings_____Looting_____Milling_List));
                nManager.Products.Products.ProductStop();
                return;
            }

            MillingState milling = new MillingState();
            milling.Run();
            Logging.Write("Milling finished.");
            nManager.Products.Products.ProductStop();
        }
    }
}