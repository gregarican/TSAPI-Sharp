using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Media;
using System.Configuration;
using Microsoft.Office.Interop.Outlook;

namespace Mojo
{
    public partial class frmMojoMain : Form
    {
        // The form initialization method
        public frmMojoMain()
        {
            InitializeComponent();

            // Don't turn on the caller ID timer yet
            this.tmDelivered.Enabled = false;

            // Read the TServer-specific variables from the app.config
            string strServerId = Properties.Settings.Default.ServerId;
            string strLoginId = Properties.Settings.Default.LoginId;
            string strPasswd = Properties.Settings.Default.Passwd;

            // Define the initial set of variables used for opening the ACS Stream
            int invokeIdType = 1;
            System.UInt32 invokeId = 0;
            int streamType = 1;
            char[] serverId = strServerId.ToCharArray();
            char[] loginId = strLoginId.ToCharArray();
            char[] passwd = strPasswd.ToCharArray();
            char[] appName = "Mojo".ToCharArray();
            int acsLevelReq = 1;
            char[] apiVer = "TS1-2".ToCharArray();
            ushort sendQSize = 0;
            ushort sendExtraBufs = 0;
            ushort recvQSize = 0;
            ushort recvExtraBufs = 0;

            // Define the mandatory (but unused) private data structure
            Csta.PrivateData_t privData = new Csta.PrivateData_t();
            privData.vendor = "MERLIN                          ".ToCharArray();
            privData.length = 4;
            privData.data = "N".ToCharArray();
            
            // Define the event buffer pointer that gets data back from the TServer
            ushort numEvents = 0;
            Csta.EventBuf_t eventBuf = new Csta.EventBuf_t();
            ushort eventBufSize = (ushort)Csta.CSTA_MAX_HEAP;

            // Open the ACS stream
            try
            {
                int openStream = Csta.acsOpenStream(ref acsHandle, invokeIdType, invokeId, streamType, serverId, loginId, passwd, appName, acsLevelReq, apiVer, sendQSize, sendExtraBufs, recvQSize, recvExtraBufs, ref privData);
            }
            catch (System.Exception eOpenStream)
            {
                // If we can't open the stream record the error and inform the user
                MessageBox.Show("There was a TServer error. Closing the application and logging the incident.", "TServer Error");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, eOpenStream.Message.ToString(), EventLogEntryType.Warning, 234);
                this.Close();
                return;
            }

            // Wait a second to poll the event buffer
            System.Threading.Thread.Sleep(1000);

            // Poll the event buffer
            try
            {
                int openStreamConf = Csta.acsGetEventPoll(acsHandle, ref eventBuf, ref eventBufSize, ref privData, ref numEvents);
            }
            catch (System.Exception eOpenStreamConf)
            {
                // If we can't get back a confirmation record the error and inform the user
                MessageBox.Show("There was a TServer error. Closing the application and logging the incident.", "TServer Error");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, eOpenStreamConf.Message.ToString(), EventLogEntryType.Warning, 234);
                this.Close();
                return;
            }

            // Parse out the data elements in the event buffer...
            
            // The event header
            UInt32 numAcsHandle = BitConverter.ToUInt32(eventBuf.data, 0);
            ushort numEventClass = BitConverter.ToUInt16(eventBuf.data, 4);
            ushort numEventType = BitConverter.ToUInt16(eventBuf.data, 6);

            // The remainder of the open stream conf structure
            numInvokeId = BitConverter.ToUInt32(eventBuf.data, 8);
            string strApiVer = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf.data, 12, 21);
            string strLibVer = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf.data, 33, 21);
            string strTsrvVer = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf.data, 54, 21);
            string strDrvrVer = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf.data, 75, 21);

            if (numEventClass == Csta.ACSCONFIRMATION && numEventType == Csta.ACS_OPEN_STREAM_CONF)
            {
                // The stream has been successfully opened                
            }
            else
            {
                // If we can't get back the open stream confirmation record the error and inform the user
                string strStreamOpenFailed = "The stream was not opened. The Event Class code returned was " + numEventClass + " and the Event Type returned was " + numEventType + ".";
                MessageBox.Show("The stream was not opened.", "Open Stream Failed");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, strStreamOpenFailed, EventLogEntryType.Warning, 234);
                this.Close();
                return;
            }    
        }

        // The method to check caller ID against Outlook contacts
        private void checkOutlook(string callerId)
        {
            // Read the Outlook-specific variable from the app.config
            string strOutlook = Properties.Settings.Default.Outlook;

            // Exit out of the method if Outlook isn't to be connected to
            if (strOutlook == "false")
            {
                return;
            }

            // Define the various Outlook COM items
            Microsoft.Office.Interop.Outlook.Application application = new Microsoft.Office.Interop.Outlook.Application();
            Microsoft.Office.Interop.Outlook.MAPIFolder contacts;
            Microsoft.Office.Interop.Outlook.Items contactItems;
            Microsoft.Office.Interop.Outlook.NameSpace outlookNamespace = application.GetNamespace("MAPI");
            outlookNamespace.Logon("", null, null, null);
            contacts = outlookNamespace.GetDefaultFolder(Microsoft.Office.Interop.Outlook.OlDefaultFolders.olFolderContacts);
            contactItems = contacts.Items;

            // Iterate through each contact item
            foreach (Microsoft.Office.Interop.Outlook._ContactItem contact in contactItems)
            {                
                // Match the caller ID against contact home telephone number
                if (contact.HomeTelephoneNumber != null)
                {
                    StringBuilder sbHome = new StringBuilder();
                    if (contact.HomeTelephoneNumber.ToString() != string.Empty && contact.HomeTelephoneNumber.ToString() != null)
                    {
                        foreach (char txt in contact.HomeTelephoneNumber.ToString())
                        {
                            // Strip all non-numeric characters from the telephone number
                            if (char.IsDigit(txt))
                            {
                                sbHome.Append(txt);
                            }
                        }
                    }
                    else
                    {
                        sbHome.Append("");
                    }
                    if (sbHome.ToString() == callerId)
                    {
                        // Display the contact match
                        contact.Display(false);
                        return;
                    }
                }

                // Match the caller ID against contact work telephone number
                if (contact.BusinessTelephoneNumber != null)
                {
                    StringBuilder sbWork = new StringBuilder();
                    if (contact.BusinessTelephoneNumber.ToString() != string.Empty && contact.BusinessTelephoneNumber.ToString() != null)
                    {
                        foreach (char txt in contact.BusinessTelephoneNumber.ToString())
                        {
                            // Strip all non-numeric characters from the telephone number
                            if (char.IsDigit(txt))
                            {
                                sbWork.Append(txt);
                            }
                        }
                    }
                    else
                    {
                        sbWork.Append("");
                    }
                    if (sbWork.ToString() == callerId)
                    {
                        // Display the contact match
                        contact.Display(false);
                        return;
                    }
                }

                // Match the caller ID against contact cellular telephone number
                if (contact.MobileTelephoneNumber != null)
                {
                    StringBuilder sbCell = new StringBuilder();
                    if (contact.MobileTelephoneNumber.ToString() != string.Empty && contact.MobileTelephoneNumber.ToString() != null)
                    {
                        foreach (char txt in contact.MobileTelephoneNumber.ToString())
                        {
                            // Strip all non-numeric characters from the telephone number
                            if (char.IsDigit(txt))
                            {
                                sbCell.Append(txt);
                            }
                        }
                    }
                    else
                    {
                        sbCell.Append("");
                    }
                    if (sbCell.ToString() == callerId)
                    {
                        // Display the contact match
                        contact.Display(false);
                        return;
                    }
                }
 
            }
        }
        
        // The event that fires to check for various call events every second
        private void tmDelivered_Tick(object sender, EventArgs e)
        {
            // Define the mandatory (unused) private data buffer
            Csta.PrivateData_t privData = new Csta.PrivateData_t();
            privData.vendor = "MERLIN                          ".ToCharArray();
            privData.length = 4;
            privData.data = "N".ToCharArray();
        
            // Define the event buffer that contains data passed back from TServer
            ushort numEvents3 = 0;
            Csta.EventBuf_t eventBuf3 = new Csta.EventBuf_t();
            ushort eventBufSize3 = (ushort)Csta.CSTA_MAX_HEAP;

            // Poll the event queue to see if any call events are occurring
            try
            {
                int deliveredEvent = Csta.acsGetEventPoll(acsHandle, ref eventBuf3, ref eventBufSize3, ref privData, ref numEvents3);
            }
            catch (System.Exception eEventPoll)
            {
                // If we can't get back a confirmation record the error and inform the user
                MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, eEventPoll.Message.ToString(), EventLogEntryType.Warning, 234);
                return;
            }

            // Parse out the data elements in the event buffer...

            // The event header
            UInt32 numAcsHandle4 = BitConverter.ToUInt32(eventBuf3.data, 0);
            ushort numEventClass4 = BitConverter.ToUInt16(eventBuf3.data, 4);
            ushort numEventType4 = BitConverter.ToUInt16(eventBuf3.data, 6);

            // The remainder of the delivered call structure
            UInt32 numMonitorCrossRefId4;
            UInt32 numCallId4;
            string strDeviceId4;
            char[] chDeviceId4;
            int numDeviceIdType4;
            string strDeviceId5;
            char[] chDeviceId5;
            int numDeviceIdType5;
            int numDeviceIdStatus5;
            string strDeviceId6;
            char[] chDeviceId6;
            int numDeviceIdType6;
            int numDeviceIdStatus6;
            string strDeviceId7;
            char[] chDeviceId7;
            int numDeviceIdType7;
            int numDeviceIdStatus7;
            string strDeviceId8;
            char[] chDeviceId8;
            int numDeviceIdType8;
            int numDeviceIdStatus8;
            int numLocalConnectionState9;
            int numEventCause9;

            // If an alerting call is present...
            if (numEventClass4 == Csta.CSTAUNSOLICITED && numEventType4 == Csta.CSTA_DELIVERED)
            {
                // The cross reference ID
                numMonitorCrossRefId4 = BitConverter.ToUInt32(eventBuf3.data, 8);

                // The connection ID
                numCallId4 = BitConverter.ToUInt32(eventBuf3.data, 12);
                strDeviceId4 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 16, 64);
                chDeviceId4 = strDeviceId4.ToCharArray();
                numDeviceIdType4 = BitConverter.ToInt32(eventBuf3.data, 80);

                // Define the active call to be referenced elsewhere
                activeCallId = numCallId4;
                activeDeviceId = chDeviceId4;
                activeDeviceIdType = numDeviceIdType4;

                // The alerting device
                strDeviceId5 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 84, 64);
                chDeviceId5 = strDeviceId5.ToCharArray();
                numDeviceIdType5 = BitConverter.ToInt32(eventBuf3.data, 148);
                numDeviceIdStatus5 = BitConverter.ToInt32(eventBuf3.data, 152);

                // The calling device
                strDeviceId6 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 156, 64);
                chDeviceId6 = strDeviceId6.ToCharArray();
                numDeviceIdType6 = BitConverter.ToInt32(eventBuf3.data, 220);
                numDeviceIdStatus6 = BitConverter.ToInt32(eventBuf3.data, 224);

                // The called device
                strDeviceId7 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 228, 64);
                chDeviceId7 = strDeviceId7.ToCharArray();
                numDeviceIdType7 = BitConverter.ToInt32(eventBuf3.data, 292);
                numDeviceIdStatus7 = BitConverter.ToInt32(eventBuf3.data, 296);

                // The last redirection device
                strDeviceId8 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 300, 64);
                chDeviceId8 = strDeviceId8.ToCharArray();
                numDeviceIdType8 = BitConverter.ToInt32(eventBuf3.data, 364);
                numDeviceIdStatus8 = BitConverter.ToInt32(eventBuf3.data, 368);

                // A couple of other state and event describers
                numLocalConnectionState9 = BitConverter.ToInt32(eventBuf3.data, 372);
                numEventCause9 = BitConverter.ToInt32(eventBuf3.data, 376);

                // Pop up the caller ID *** if the calling party isn't the device itself ***
                string callerId = strDeviceId6.Trim('\0');
                if (callerId != this.tbExtension.Text)
                    {
                        this.tbCallerId.Text = callerId;
                        string now = System.DateTime.Now.ToShortDateString() + " " + System.DateTime.Now.ToShortTimeString();
                        this.tbCalledAt.Text = now;
                        this.btnCall.Text = "Answer";
                        this.btnCall.ForeColor = System.Drawing.Color.Green;
                        this.WindowState = FormWindowState.Normal;
                        if (!muted)
                        {
                            player.SoundLocation = @"ring.wav";
                            player.Play();
                        }
                        
                        // Check caller ID against Outlook contacts
                        checkOutlook(callerId);                                                
                    }
                else
                    {
                        return;
                    }
                
            }
            // If a connection has been cleared...
            else if (numEventClass4 == Csta.CSTAUNSOLICITED && numEventType4 == Csta.CSTA_CONNECTION_CLEARED)
                {
                    // The cross reference ID
                    numMonitorCrossRefId4 = BitConverter.ToUInt32(eventBuf3.data, 8);

                    // The connection ID
                    numCallId4 = BitConverter.ToUInt32(eventBuf3.data, 12);
                    strDeviceId4 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 16, 64);
                    chDeviceId4 = strDeviceId4.ToCharArray();
                    numDeviceIdType4 = BitConverter.ToInt32(eventBuf3.data, 80);

                    // The releasing device
                    strDeviceId5 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 84, 64);
                    chDeviceId5 = strDeviceId5.ToCharArray();
                    numDeviceIdType5 = BitConverter.ToInt32(eventBuf3.data, 148);
                    numDeviceIdStatus5 = BitConverter.ToInt32(eventBuf3.data, 152);

                    // A couple of other state and event describers
                    numLocalConnectionState9 = BitConverter.ToInt32(eventBuf3.data, 156);
                    numEventCause9 = BitConverter.ToInt32(eventBuf3.data, 160);

                    // Feed the event back to the user
                    if ((this.btnCall.Text == "Hangup") || (this.btnCall.Text == "Answer"))
                    {
                        this.btnCall.Text = "Call";
                        this.btnCall.ForeColor = System.Drawing.Color.Black;
                    }
                }    
            // If a connection has been established...
            else if (numEventClass4 == Csta.CSTAUNSOLICITED && numEventType4 == Csta.CSTA_ESTABLISHED)
                {
                    // The cross reference ID
                    numMonitorCrossRefId4 = BitConverter.ToUInt32(eventBuf3.data, 8);

                    // The connection ID
                    numCallId4 = BitConverter.ToUInt32(eventBuf3.data, 12);
                    strDeviceId4 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 16, 64);
                    chDeviceId4 = strDeviceId4.ToCharArray();
                    numDeviceIdType4 = BitConverter.ToInt32(eventBuf3.data, 80);

                    // The answering device
                    strDeviceId5 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 84, 64);
                    chDeviceId5 = strDeviceId5.ToCharArray();
                    numDeviceIdType5 = BitConverter.ToInt32(eventBuf3.data, 148);
                    numDeviceIdStatus5 = BitConverter.ToInt32(eventBuf3.data, 152);

                    // Define the active call to be referenced elsewhere
                    activeCallId = numCallId4;
                    activeDeviceId = chDeviceId4;
                    activeDeviceIdType = numDeviceIdType4;

                    // The calling device
                    strDeviceId6 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 156, 64);
                    chDeviceId6 = strDeviceId6.ToCharArray();
                    numDeviceIdType6 = BitConverter.ToInt32(eventBuf3.data, 220);
                    numDeviceIdStatus6 = BitConverter.ToInt32(eventBuf3.data, 224);

                    // The called device
                    strDeviceId7 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 228, 64);
                    chDeviceId7 = strDeviceId7.ToCharArray();
                    numDeviceIdType7 = BitConverter.ToInt32(eventBuf3.data, 292);
                    numDeviceIdStatus7 = BitConverter.ToInt32(eventBuf3.data, 296);

                    // The last redirection device
                    strDeviceId8 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 300, 64);
                    chDeviceId8 = strDeviceId8.ToCharArray();
                    numDeviceIdType8 = BitConverter.ToInt32(eventBuf3.data, 364);
                    numDeviceIdStatus8 = BitConverter.ToInt32(eventBuf3.data, 368);

                    // A couple of other state and event describers
                    numLocalConnectionState9 = BitConverter.ToInt32(eventBuf3.data, 372);
                    numEventCause9 = BitConverter.ToInt32(eventBuf3.data, 376);

                    // Feed the event back to the user
                    if ((this.btnCall.Text == "Answer") || (this.btnCall.Text == "Call"))
                    {
                        this.btnCall.Text = "Hangup";
                        this.btnCall.ForeColor = System.Drawing.Color.Red;
                    }
                }             
        }
        
        // The method that places an active monitor on the entered extension
        private void btnLogin_Click(object sender, EventArgs e)
        {
            string strExtension = this.tbExtension.Text;
            chExtension = strExtension.ToCharArray();

            // Define the mandatory (unused) private data structure
            Csta.PrivateData_t privData = new Csta.PrivateData_t();
            privData.vendor = "MERLIN                          ".ToCharArray();
            privData.length = 4;
            privData.data = "N".ToCharArray();
            
            // Define the various event monitor filters...

            // Any filters NOT added will allow those events to be monitored
            Csta.CSTAMonitorFilter_t monitorFilter = new Csta.CSTAMonitorFilter_t();
            monitorFilter.call = 0xFFFF - Csta.CF_DELIVERED - Csta.CF_CONNECTION_CLEARED - Csta.CF_ESTABLISHED;    // Monitor these call events 
            monitorFilter.feature = 0;                                                                                                  // Monitor everything feature-wise
            monitorFilter.agent = 0;                                                                                                    // Monitor everything agent-wise
            monitorFilter.maintenance = 0;                                                                                              // Monitor everything maintenance-wise
            monitorFilter.privateFilter = 1;                                                                                            // Mandatory but unused

            try
            {
                int monitorDevice = Csta.cstaMonitorDevice(acsHandle, numInvokeId, chExtension, ref monitorFilter, ref privData);
            }
            catch (System.Exception eMonitor)
            {
                // If we can't get back a confirmation record the error and inform the user
                MessageBox.Show("There was a TServer error. Closing the application and logging the incident.", "TServer Error");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, eMonitor.Message.ToString(), EventLogEntryType.Warning, 234);
                this.Close();
                return;
            }

            // Wait a second before polling the event queue
            System.Threading.Thread.Sleep(1000);

            // Define the event buffer that contains data passed back from TServer
            ushort numEvents2 = 0;
            Csta.EventBuf_t eventBuf2 = new Csta.EventBuf_t();
            ushort eventBufSize2 = (ushort)Csta.CSTA_MAX_HEAP;

            try
            {
                int monitorDeviceConf = Csta.acsGetEventPoll(acsHandle, ref eventBuf2, ref eventBufSize2, ref privData, ref numEvents2);
            }
            catch (System.Exception eMonitorConf)
            {
                // If we can't get back a confirmation record the error and inform the user
                MessageBox.Show("There was a TServer error. Closing the application and logging the incident.", "TServer Error");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, eMonitorConf.Message.ToString(), EventLogEntryType.Warning, 234);
                this.Close();
                return;
            }

            // Parse out the data elements in the event buffer...

            // The event header
            UInt32 numAcsHandle3 = BitConverter.ToUInt32(eventBuf2.data, 0);
            ushort numEventClass3 = BitConverter.ToUInt16(eventBuf2.data, 4);
            ushort numEventType3 = BitConverter.ToUInt16(eventBuf2.data, 6);

            // The various elements contained in the rest of the event buffer
            UInt32 numInvokeId3;
            UInt32 numMonitorCrossRefId3;
            ushort numCallFilter3;
            byte numFeatureFilter3;
            byte numAgentFilter3;
            byte numMaintenanceFilter3;
            UInt32 numPrivateFilter3;

            // If the device monitor was successful...
            if (numEventClass3 == Csta.CSTACONFIRMATION && numEventType3 == Csta.CSTA_MONITOR_CONF)
            {
                // Parse the elements in the event buffer
                numInvokeId3 = BitConverter.ToUInt32(eventBuf2.data, 8);
                numMonitorCrossRefId3 = BitConverter.ToUInt32(eventBuf2.data, 12);
                numCallFilter3 = BitConverter.ToUInt16(eventBuf2.data, 16);
                numFeatureFilter3 = (byte)eventBuf2.data.GetValue(18);
                numAgentFilter3 = (byte)eventBuf2.data.GetValue(20);
                numMaintenanceFilter3 = (byte)eventBuf2.data.GetValue(22);
                numPrivateFilter3 = BitConverter.ToUInt32(eventBuf2.data, 24);

                
                // Feed back to the user that they are logged in with an active monitor
                this.tbExtension.ReadOnly = true;
                this.btnLogin.Text = "Logged In";
                this.btnLogin.Enabled = false;
                this.tmDelivered.Enabled = true;
                this.tmDelivered.Start();

            }
            else
            {
                string strMonitorDeviceFailed = "The device was not monitored. The Event Class code returned was " + numEventClass3 + " and the Event Type returned was " + numEventType3 + ".";
                MessageBox.Show("The device was not monitored.", "Monitor Device Failed");
                if (!EventLog.SourceExists(strSource))
                {
                    EventLog.CreateEventSource(strSource, strLog);
                }
                EventLog.WriteEntry(strSource, strMonitorDeviceFailed, EventLogEntryType.Warning, 234);                
                this.Close();
                return;
            }
            
        }        

        // The various touchtone buttons on the form...
        private void btn1_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "1";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-1.wav";
                player.Play();
            }
        }

        private void btn2_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "2";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-2.wav";
                player.Play();
            }
        }

        private void btn3_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "3";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-3.wav";
                player.Play();
            }
        }

        private void btn4_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "4";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-4.wav";
                player.Play();
            }
        }

        private void btn5_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "5";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-5.wav";
                player.Play();
            }
        }

        private void btn6_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "6";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-6.wav";
                player.Play();
            }
        }

        private void btn7_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "7";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-7.wav";
                player.Play();
            }
        }

        private void btn8_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "8";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-8.wav";
                player.Play();
            }
        }

        private void btn9_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "9";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-9.wav";
                player.Play();
            }
        }

        private void btn0_Click(object sender, EventArgs e)
        {
            this.tbDialed.Text += "0";
            if (!muted)
            {
                player.SoundLocation = @"dtmf-0.wav";
                player.Play();
            }
        }

        // The event that fires when the Call button is clicked
        private void btnCall_Click(object sender, EventArgs e)
        {
            // If the button is defined as for calling and has an outcall number defined...
            if (this.btnCall.Text == "Call" && this.tbDialed.Text != "")
            {
                // Define the caller and callee for passing to the TServer
                char[] caller = chExtension;
                char[] callee = this.tbDialed.Text.ToCharArray();

                // Define the event buffer that will contain data passed back from TServer
                ushort numEvents3 = 0;
                Csta.EventBuf_t eventBuf3 = new Csta.EventBuf_t();
                ushort eventBufSize3 = (ushort)Csta.CSTA_MAX_HEAP;

                // Define the mandatory (unused) private data buffer 
                Csta.PrivateData_t privData = new Csta.PrivateData_t();
                privData.vendor = "MERLIN                          ".ToCharArray();
                privData.length = 4;
                privData.data = "N".ToCharArray();

                try
                {
                    int makeCall = Csta.cstaMakeCall(acsHandle, numInvokeId, caller, callee, ref privData);
                }
                catch (System.Exception eMakeCall)
                {
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eMakeCall.Message.ToString(), EventLogEntryType.Warning, 234);
                    return;
                }

                // Wait 2 seconds before polling the event queue
                System.Threading.Thread.Sleep(2000);

                try
                {
                    int makeCallConf = Csta.acsGetEventPoll(acsHandle, ref eventBuf3, ref eventBufSize3, ref privData, ref numEvents3);
                }
                catch (System.Exception eMakeCallConf)
                {
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eMakeCallConf.Message.ToString(), EventLogEntryType.Warning, 234);
                    return;
                }

                // Parse out the data elements in the event buffer...

                // The event header
                UInt32 numAcsHandle2 = BitConverter.ToUInt32(eventBuf3.data, 0);
                ushort numEventClass2 = BitConverter.ToUInt16(eventBuf3.data, 4);
                ushort numEventType2 = BitConverter.ToUInt16(eventBuf3.data, 6);

                // The various other elements in the remainder of the event buffer
                UInt32 numInvokeId2;
                UInt32 numCallId2;
                string strDeviceId2;
                char[] chDeviceId2;
                int numDeviceIdType;

                // If the outcall attempt was made successfully (not connected, just attempted)...
                if (numEventClass2 == Csta.CSTACONFIRMATION && numEventType2 == Csta.CSTA_MAKE_CALL_CONF)
                {
                    // Parse out the remainder of the event buffer
                    numInvokeId2 = BitConverter.ToUInt32(eventBuf3.data, 8);
                    numCallId2 = BitConverter.ToUInt32(eventBuf3.data, 12);
                    strDeviceId2 = System.Text.ASCIIEncoding.ASCII.GetString(eventBuf3.data, 16, 64);
                    chDeviceId2 = strDeviceId2.ToCharArray();
                    numDeviceIdType = BitConverter.ToInt32(eventBuf3.data, 80);

                    // Define the active call to be referenced elsewhere
                    activeCallId = numCallId2;
                    activeDeviceId = chDeviceId2;
                    activeDeviceIdType = numDeviceIdType;

                    // Feed back the event to the user
                    this.tbDialed.Text = "";
                    this.btnCall.Text = "Hangup";
                    this.btnCall.ForeColor = System.Drawing.Color.Red;
                }
                else
                {
                    this.tbDialed.Text = "";
                    string strMakeCallFailed = "The call was not made. The Event Class code returned was " + numEventClass2 + " and the Event Type returned was " + numEventType2 + ".";
                    MessageBox.Show("The call was not made.", "Make Call Failed");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, strMakeCallFailed, EventLogEntryType.Warning, 234);
                    return;
                }

            }
            // If the button is defined as Hangup...
            else if (this.btnCall.Text == "Hangup")
            {
                // Define the mandatory (unused) private data buffer
                Csta.PrivateData_t privData = new Csta.PrivateData_t();
                privData.vendor = "MERLIN                          ".ToCharArray();
                privData.length = 4;
                privData.data = "N".ToCharArray();

                // Populate a ConnectionID_t struct with the active call elements
                Csta.ConnectionID_t activeCall = new Csta.ConnectionID_t();
                activeCall.callID = activeCallId;
                activeCall.deviceID.device = activeDeviceId;
                activeCall.devIDType = (Csta.ConnectionID_Device_t)activeDeviceIdType;

                // Define the event buffer that will contain data passed back from TServer
                ushort numEvents4 = 0;
                Csta.EventBuf_t eventBuf4 = new Csta.EventBuf_t();
                ushort eventBufSize4 = (ushort)Csta.CSTA_MAX_HEAP;

                try
                {
                    int clearConnection = Csta.cstaClearConnection(acsHandle, numInvokeId, ref activeCall, ref privData);
                }
                catch (System.Exception eClearConnection)
                {
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eClearConnection.Message.ToString(), EventLogEntryType.Warning, 234);
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                    return;
                }

                // Wait 2 seconds before polling the event queue
                System.Threading.Thread.Sleep(2000);

                try
                {
                    int clearConnectionConf = Csta.acsGetEventPoll(acsHandle, ref eventBuf4, ref eventBufSize4, ref privData, ref numEvents4);
                }
                catch (System.Exception eClearConnectionConf)
                {
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eClearConnectionConf.Message.ToString(), EventLogEntryType.Warning, 234);
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                    return;
                }

                // Parse out the data elements in the event buffer...

                // The event header
                UInt32 numAcsHandle10 = BitConverter.ToUInt32(eventBuf4.data, 0);
                ushort numEventClass10 = BitConverter.ToUInt16(eventBuf4.data, 4);
                ushort numEventType10 = BitConverter.ToUInt16(eventBuf4.data, 6);

                // The only significant portion of the remainder of the event buffer (all else is null)
                UInt32 numInvokeId10;

                // If the call was cleared (hung up) successfully...
                if (numEventClass10 == Csta.CSTACONFIRMATION && numEventType10 == Csta.CSTA_CLEAR_CONNECTION_CONF)
                {
                    numInvokeId10 = BitConverter.ToUInt32(eventBuf4.data, 8);
                    
                    // Feed the event back to the user
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                }
                else
                {
                    string strClearConnectionFailed = "The connection was not cleared. The Event Class code returned was " + numEventClass10 + " and the Event Type returned was " + numEventType10 + ".";
                    MessageBox.Show("The connection was not cleared.", "Clear Connection Failed");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, strClearConnectionFailed, EventLogEntryType.Warning, 234);
                    
                    // Feed the same information back to the user regardless (for now)
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                    return;                    
                }
            }
            // If the button is defined as Answer...
            else if (this.btnCall.Text == "Answer")
            {
                // Define the mandatory (unused) private data buffer
                Csta.PrivateData_t privData = new Csta.PrivateData_t();
                privData.vendor = "MERLIN                          ".ToCharArray();
                privData.length = 4;
                privData.data = "N".ToCharArray();

                // Populate a ConnectionID_t struct with the active call elements
                Csta.ConnectionID_t activeCall = new Csta.ConnectionID_t();
                activeCall.callID = activeCallId;
                activeCall.deviceID.device = activeDeviceId;
                activeCall.devIDType = (Csta.ConnectionID_Device_t)activeDeviceIdType;

                // Define the event buffer that will contain data passed back from TServer
                ushort numEvents11 = 0;
                Csta.EventBuf_t eventBuf11 = new Csta.EventBuf_t();
                ushort eventBufSize11 = (ushort)Csta.CSTA_MAX_HEAP;

                try
                {
                    int answerCall = Csta.cstaAnswerCall(acsHandle, numInvokeId, ref activeCall, ref privData);
                }
                catch (System.Exception eAnswerCall)
                {   
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eAnswerCall.Message.ToString(), EventLogEntryType.Warning, 234);
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                    return;
                }

                // Wait 2 seconds before polling the event queue
                System.Threading.Thread.Sleep(2000);

                try
                {
                    int answerCallConf = Csta.acsGetEventPoll(acsHandle, ref eventBuf11, ref eventBufSize11, ref privData, ref numEvents11);
                }
                catch (System.Exception eAnswerCallConf)
                {
                    // If we can't get back a confirmation record the error and inform the user
                    MessageBox.Show("There was a TServer error. Logging the incident and continuing the application.", "TServer Error");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, eAnswerCallConf.Message.ToString(), EventLogEntryType.Warning, 234);
                    this.btnCall.Text = "Call";
                    this.btnCall.ForeColor = System.Drawing.Color.Black;
                    return;
                }

                // Parse out the data elements in the event buffer...

                // The event header
                UInt32 numAcsHandle11 = BitConverter.ToUInt32(eventBuf11.data, 0);
                ushort numEventClass11 = BitConverter.ToUInt16(eventBuf11.data, 4);
                ushort numEventType11 = BitConverter.ToUInt16(eventBuf11.data, 6);

                // The only significant portion of the remainder of the event buffer (all else is null)
                UInt32 numInvokeId11;

                // If the call was answered successfully...
                if (numEventClass11 == Csta.CSTACONFIRMATION && numEventType11 == Csta.CSTA_ANSWER_CALL_CONF)
                {
                    numInvokeId11 = BitConverter.ToUInt32(eventBuf11.data, 8);

                    // Feed the event back to the user
                    this.btnCall.Text = "Hangup";
                    this.btnCall.ForeColor = System.Drawing.Color.Red;                   
                }
                else
                {
                    string strAnswerCallFailed = "The call was not answered. The Event Class code returned was " + numEventClass11 + " and the Event Type returned was " + numEventType11 + ".";
                    MessageBox.Show("The call was not answered.", "Answer Call Failed");
                    if (!EventLog.SourceExists(strSource))
                    {
                        EventLog.CreateEventSource(strSource, strLog);
                    }
                    EventLog.WriteEntry(strSource, strAnswerCallFailed, EventLogEntryType.Warning, 234);

                    // Feed the same information back to the user regardless (for now)
                    this.btnCall.Text = "Hangup";
                    this.btnCall.ForeColor = System.Drawing.Color.Red;
                    return;
                }
            }
        }


        // The method alternately shows and hides the call control portion of the form
        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (this.Size.Height == 140)
            {
                this.Size = new Size(this.Size.Width, 293);
            }
            else
            {
                this.Size = new Size(this.Size.Width, 140);
            }
        }

        // Ignore all keystrokes except for numbers, backspaces, and tabs.
        private void tbDialed_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (((e.KeyChar < '0') || (e.KeyChar > '9')) && (e.KeyChar != '\b') && (e.KeyChar != '\t'))
            {
                e.Handled = true;
            }
        }

        // Ignore all keystrokes except for numbers, backspaces, and tabs.
        private void tbExtension_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (((e.KeyChar < '0') || (e.KeyChar > '9')) && (e.KeyChar != '\b') && (e.KeyChar != '\t'))
            {
                e.Handled = true;
            }
        }

        // The event that fires when the mute button is clicked
        private void btnMute_Click(object sender, EventArgs e)
        {
            if (muted)
            {
                this.btnMute.Text = "On";
                this.btnMute.BackColor = System.Drawing.Color.ForestGreen;
                muted = false;
            }
            else
            {
                this.btnMute.Text = "Off";
                this.btnMute.BackColor = System.Drawing.Color.DarkRed;
                muted = true;
            }
        }

        // Define the instance variables referenced throughout the class
        UInt32 acsHandle = 0;
        UInt32 numInvokeId;
        char[] chExtension;
        UInt32 activeCallId;
        char[] activeDeviceId;
        int activeDeviceIdType;
        string strSource = "Mojo";
        string strLog = "Application";
        SoundPlayer player = new SoundPlayer();
        bool muted = false;
        
    }

}