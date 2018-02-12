﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using ResponseTuple = System.Tuple
    <System.Collections.Generic.List<MonikAI.Expression[]>, System.Func<bool>, System.TimeSpan, System.DateTime>;

namespace MonikAI.Behaviours
{
    public class ApplicationBehaviour : IBehaviour
    {
        private readonly Dictionary<string[], ResponseTuple> responseTable = new Dictionary
            <string[], ResponseTuple>
            {
                {
                    new[] {"firefox.exe", "chrome.exe"},
                    new ResponseTuple(new List<Expression[]>
                    {
                        new[] {new Expression("Browsing the web? Have fun!", "k")},
                        new[] {new Expression("I like the internet. There's so much to explore!", "d")},
                        new[] {new Expression("Don't go looking for lewds of me, okay? Ahaha~", "l")},
                        new[] {new Expression("Have fun surfing the web!", "k")}
                    }, () =>
                    {
                        var browserProcesses =
                            Process.GetProcesses()
                                .Where(p =>
                                {
                                    var exe = ApplicationBehaviour.GetProcessExecutable(p.Id);
                                    return exe == "firefox.exe" || exe == "chrome.exe";
                                }).ToList();
                        return browserProcesses.All(p => (DateTime.Now - p.StartTime).TotalSeconds < 10);
                    }, TimeSpan.FromSeconds(10), DateTime.MinValue)
                },

                {
                    new[] {"putty.exe"},
                    new ResponseTuple(new List<Expression[]>
                    {
                        new[] {new Expression("PuTTY, huh? I only have experience with the python shell...", "o")}
                    }, () => true, TimeSpan.FromMinutes(30), DateTime.MinValue)
                }
            };

        private readonly object toSayLock = new object();

        private Expression[] toSay;
        private ManagementEventWatcher w;

        public void Init(MainWindow window)
        {
            //// Process start
            WqlEventQuery q;
            try
            {
                q = new WqlEventQuery {EventClassName = "Win32_ProcessStartTrace"};
                this.w = new ManagementEventWatcher(q);
                this.w.EventArrived += this.WMIEventArrived;
                this.w.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(window,
                    "An error occured: " + ex.Message + "\r\n\r\n(Try running MonikAI as an administrator.)");
            }
        }

        public void Update(MainWindow window)
        {
            lock (this.toSayLock)
            {
                if (this.toSay != null)
                {
                    window.Say(this.toSay);
                    this.toSay = null;
                }
            }
        }

        private static string GetProcessExecutable(int processId)
        {
            var methodResult = "";

            try
            {
                var query = "SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;

                using (var mos = new ManagementObjectSearcher(query))
                {
                    using (var moc = mos.Get())
                    {
                        var ExecutablePath =
                            (from mo in moc.Cast<ManagementObject>() select mo["ExecutablePath"]).First().ToString();

                        methodResult = Path.GetFileName(ExecutablePath).Trim().ToLower();
                    }
                }
            }
            catch
            {
            }

            return methodResult;
        }

        private void WMIEventArrived(object sender, EventArrivedEventArgs e)
        {
            string processName = null;
            foreach (var property in e.NewEvent.Properties)
            {
                if (property.Name == "ProcessName")
                {
                    processName = ((string) property.Value).ToLower();
                    break;
                }
            }

            // Process start has been detected
            if (processName != null)
            {
                foreach (var pair in this.responseTable)
                {
                    if (pair.Key.Contains(processName))
                    {
                        if (DateTime.Now - pair.Value.Item4 > pair.Value.Item3 && pair.Value.Item2())
                        {
                            lock (this.toSayLock)
                            {
                                this.toSay = pair.Value.Item1.Sample();
                            }

                            // Update last executed time
                            this.responseTable[pair.Key] =
                                new ResponseTuple(pair.Value.Item1,
                                    pair.Value.Item2, pair.Value.Item3, DateTime.Now);
                        }

                        break;
                    }
                }
            }
        }
    }
}